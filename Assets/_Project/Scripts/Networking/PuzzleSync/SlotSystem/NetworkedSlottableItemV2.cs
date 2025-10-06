using System;
using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

namespace MetaAvatarsVR.Networking.PuzzleSync.SlotSystem
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(FusionVRGrabbable))]
    [RequireComponent(typeof(Rigidbody))]
    public abstract class NetworkedSlottableItemV2 : NetworkBehaviour, ISlottable
    {
        [Header("Item Configuration")]
        [SerializeField] protected int _itemId = 0;
        [SerializeField] protected float _snapDistance = 0.5f;
        [SerializeField] protected string _slotTag = "SnapSlot";
        [SerializeField] protected LayerMask _slotLayerMask = -1;
        
        [Header("Behavior Options")]
        [SerializeField] protected bool _returnToOriginOnFailedPlacement = false;
        [SerializeField] protected float _returnToOriginDelay = 2f;
        [SerializeField] protected bool _useTagDetection = true;
        [SerializeField] protected bool _useLayerDetection = true;
        
        [Header("Visual Feedback")]
        [SerializeField] protected GameObject _correctPlacementEffect;
        [SerializeField] protected GameObject _incorrectPlacementEffect;
        [SerializeField] protected MeshRenderer _meshRenderer;
        
        [Header("Audio")]
        [SerializeField] protected AudioClip _placementSound;
        [SerializeField] protected AudioClip _removalSound;
        
        [Header("Debug Settings")]
        [SerializeField] protected bool _enableDebugLogs = true;
        [SerializeField] protected bool _showDetectionGizmos = true;
        [SerializeField] protected Color _gizmoColor = Color.yellow;
        
        [Header("Network State")]
        [Networked] public int NetworkedItemId { get; set; }
        [Networked] public NetworkBool IsPlacedInSlot { get; set; }
        [Networked] public int PlacedSlotIndex { get; set; }
        [Networked] public NetworkBool IsCorrectlyPlaced { get; set; }
        
        [Header("Events")]
        public UnityEvent<int> OnItemPlaced = new UnityEvent<int>();
        public UnityEvent OnItemRemoved = new UnityEvent();
        public UnityEvent OnCorrectPlacement = new UnityEvent();
        public UnityEvent OnIncorrectPlacement = new UnityEvent();
        
        protected FusionVRGrabbable _fusionGrabbable;
        protected Grabbable _metaGrabbable;
        protected Rigidbody _rigidbody;
        protected AudioSource _audioSource;
        [SerializeField] protected NetworkedSlotPuzzleController _puzzleController;
        protected Vector3 _originalPosition;
        protected Quaternion _originalRotation;
        protected bool _isBeingGrabbed = false;
        protected Coroutine _returnCoroutine;
        
        public int ItemId => _itemId;
        public Transform Transform => transform;
        public bool IsPlaced => IsPlacedInSlot;
        public int CurrentSlotIndex => PlacedSlotIndex;
        
        protected virtual void Awake()
        {
            DebugLog($"[{name}] Awake - Starting initialization");
            
            _fusionGrabbable = GetComponent<FusionVRGrabbable>();
            _metaGrabbable = GetComponent<Grabbable>();
            _rigidbody = GetComponent<Rigidbody>();
            
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
                
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
            
            ValidateComponents();
            
            DebugLog($"[{name}] Awake - Initialization complete. Original pos: {_originalPosition}");
        }
        
        protected virtual void Start()
        {
            DebugLog($"[{name}] Start - Registering grabbable events");
            
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised += OnMetaGrabbableEvent;
                DebugLog($"[{name}] Meta Grabbable events registered");
            }
            else
            {
                DebugLogWarning($"[{name}] No Meta Grabbable component found!");
            }
            
            // También intentamos detectar eventos del FusionVRGrabbable si es necesario
            StartCoroutine(DelayedSetup());
        }
        
        private IEnumerator DelayedSetup()
        {
            yield return new WaitForSeconds(0.5f);
            
            if (_fusionGrabbable != null)
            {
                DebugLog($"[{name}] FusionVRGrabbable detected and ready");
            }
        }
        
        public override void Spawned()
        {
            DebugLog($"[{name}] Spawned - HasStateAuthority: {HasStateAuthority}, LocalPlayer: {Runner.LocalPlayer}");
            
            if (HasStateAuthority)
            {
                NetworkedItemId = _itemId;
                PlacedSlotIndex = -1;
                IsPlacedInSlot = false;
                IsCorrectlyPlaced = false;
                
                DebugLog($"[{name}] Network state initialized. ItemId: {_itemId}");
            }
            
            OnSpawnedCustom();
        }
        
        protected virtual void OnSpawnedCustom()
        {
        }
        
        protected virtual void OnMetaGrabbableEvent(PointerEvent evt)
        {
            DebugLog($"[{name}] PointerEvent received: {evt.Type}, Position: {transform.position}");
            
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    OnGrabbed();
                    break;
                    
                case PointerEventType.Unselect:
                    OnReleased();
                    break;
                    
                case PointerEventType.Hover:
                    DebugLog($"[{name}] Hover detected");
                    break;
                    
                case PointerEventType.Unhover:
                    DebugLog($"[{name}] Unhover detected");
                    break;
            }
        }
        
        protected virtual void OnGrabbed()
        {
            DebugLog($"[{name}] OnGrabbed - IsPlaced: {IsPlacedInSlot}, CurrentSlot: {PlacedSlotIndex}");
            
            _isBeingGrabbed = true;
            
            // Cancelar cualquier retorno pendiente
            if (_returnCoroutine != null)
            {
                StopCoroutine(_returnCoroutine);
                _returnCoroutine = null;
                DebugLog($"[{name}] Cancelled pending return to origin");
            }
            
            if (IsPlacedInSlot)
            {
                DebugLog($"[{name}] Requesting removal from slot {PlacedSlotIndex}");
                RPC_RequestRemovalFromSlot();
            }
        }
        
        protected virtual void OnReleased()
        {
            DebugLog($"[{name}] OnReleased - Position: {transform.position}, Checking for slots...");
            
            _isBeingGrabbed = false;
            
            // Pequeño delay para asegurar que el objeto esté en su posición final
            StartCoroutine(DelayedSlotCheck());
        }
        
        private IEnumerator DelayedSlotCheck()
        {
            // Esperar un frame para asegurar que la física se actualice
            yield return new WaitForFixedUpdate();
            
            CheckNearbySlot();
        }
        
        protected virtual void CheckNearbySlot()
        {
            DebugLog($"[{name}] CheckNearbySlot - Starting detection at position: {transform.position}");
            DebugLog($"[{name}] Detection settings - Distance: {_snapDistance}, UseTag: {_useTagDetection}, UseLayer: {_useLayerDetection}");
            
            // Primero intentar con OverlapSphere
            Collider[] colliders = Physics.OverlapSphere(transform.position, _snapDistance);
            DebugLog($"[{name}] Found {colliders.Length} colliders in range");
            
            NetworkedSlot closestSlot = null;
            float closestDistance = float.MaxValue;
            
            foreach (var collider in colliders)
            {
                // Verificar por tag si está habilitado
                if (_useTagDetection && !string.IsNullOrEmpty(_slotTag))
                {
                    if (!collider.CompareTag(_slotTag))
                    {
                        DebugLog($"[{name}] Collider {collider.name} tag '{collider.tag}' doesn't match '{_slotTag}'");
                        continue;
                    }
                }
                
                // Verificar por layer si está habilitado
                if (_useLayerDetection && _slotLayerMask != 0)
                {
                    if ((_slotLayerMask & (1 << collider.gameObject.layer)) == 0)
                    {
                        DebugLog($"[{name}] Collider {collider.name} layer {collider.gameObject.layer} not in mask");
                        continue;
                    }
                }
                
                var slot = collider.GetComponent<NetworkedSlot>();
                if (slot != null)
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    DebugLog($"[{name}] Found slot {slot.SlotId} at distance {distance:F2}, IsOccupied: {slot.IsOccupied}");
                    
                    if (!slot.IsOccupied && distance < closestDistance)
                    {
                        closestSlot = slot;
                        closestDistance = distance;
                    }
                }
                else
                {
                    // Intentar buscar en el padre
                    slot = collider.GetComponentInParent<NetworkedSlot>();
                    if (slot != null)
                    {
                        float distance = Vector3.Distance(transform.position, collider.transform.position);
                        DebugLog($"[{name}] Found slot in parent {slot.SlotId} at distance {distance:F2}");
                        
                        if (!slot.IsOccupied && distance < closestDistance)
                        {
                            closestSlot = slot;
                            closestDistance = distance;
                        }
                    }
                }
            }
            
            if (closestSlot != null)
            {
                DebugLog($"[{name}] Requesting placement in closest slot {closestSlot.SlotId}");
                RPC_RequestPlacement(closestSlot.SlotId);
            }
            else
            {
                DebugLog($"[{name}] No available slot found");
                
                if (!IsPlacedInSlot && _returnToOriginOnFailedPlacement)
                {
                    DebugLog($"[{name}] Starting return to origin timer ({_returnToOriginDelay}s)");
                    _returnCoroutine = StartCoroutine(ReturnToOriginAfterDelay());
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        protected virtual void RPC_RequestPlacement(int slotId, RpcInfo info = default)
        {
            DebugLog($"[{name}] RPC_RequestPlacement - SlotId: {slotId}, From: {info.Source}");
            
            if (_puzzleController != null)
            {
                bool success = _puzzleController.TryPlaceItemInSlot(this, slotId);
                
                if (success)
                {
                    var slot = _puzzleController.GetSlot(slotId);
                    bool isCorrect = _puzzleController.ValidatePlacement(this, slot);
                    
                    IsPlacedInSlot = true;
                    PlacedSlotIndex = slotId;
                    IsCorrectlyPlaced = isCorrect;
                    
                    DebugLog($"[{name}] Placement successful. Correct: {isCorrect}");
                    RPC_NotifyPlacement(slotId, isCorrect);
                }
                else
                {
                    DebugLog($"[{name}] Placement failed");
                    RPC_PlacementFailed();
                }
            }
            else
            {
                DebugLogWarning($"[{name}] No puzzle controller set!");
                RPC_PlacementFailed();
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        protected virtual void RPC_RequestRemovalFromSlot(RpcInfo info = default)
        {
            DebugLog($"[{name}] RPC_RequestRemovalFromSlot - From: {info.Source}, CurrentSlot: {PlacedSlotIndex}");
            
            if (_puzzleController != null && PlacedSlotIndex >= 0)
            {
                _puzzleController.RemoveItemFromSlot(PlacedSlotIndex);
                
                int previousSlot = PlacedSlotIndex;
                IsPlacedInSlot = false;
                IsCorrectlyPlaced = false;
                PlacedSlotIndex = -1;
                
                DebugLog($"[{name}] Removed from slot {previousSlot}");
                RPC_NotifyRemoval(previousSlot);
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyPlacement(int slotId, NetworkBool isCorrect)
        {
            DebugLog($"[{name}] RPC_NotifyPlacement - Slot: {slotId}, Correct: {isCorrect}");
            OnPlacedInSlot(slotId, isCorrect);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyRemoval(int slotId)
        {
            DebugLog($"[{name}] RPC_NotifyRemoval - Slot: {slotId}");
            OnRemovedFromSlot(slotId);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_PlacementFailed()
        {
            DebugLog($"[{name}] RPC_PlacementFailed");
            
            if (_returnToOriginOnFailedPlacement)
            {
                ResetToOrigin();
            }
        }
        
        public virtual void OnPlacedInSlot(int slotIndex, bool isCorrect)
        {
            DebugLog($"[{name}] OnPlacedInSlot - Slot: {slotIndex}, Correct: {isCorrect}");
            
            OnItemPlaced?.Invoke(slotIndex);
            
            if (isCorrect)
            {
                OnCorrectPlacement?.Invoke();
                ShowEffect(_correctPlacementEffect);
            }
            else
            {
                OnIncorrectPlacement?.Invoke();
                ShowEffect(_incorrectPlacementEffect);
            }
            
            PlaySound(_placementSound);
            OnPlacedCustom(slotIndex, isCorrect);
        }
        
        public virtual void OnRemovedFromSlot(int slotIndex)
        {
            DebugLog($"[{name}] OnRemovedFromSlot - Slot: {slotIndex}");
            
            OnItemRemoved?.Invoke();
            HideAllEffects();
            PlaySound(_removalSound);
            OnRemovedCustom(slotIndex);
        }
        
        public void SetItemId(int id)
        {
            _itemId = id;
            if (HasStateAuthority)
            {
                NetworkedItemId = id;
            }
            DebugLog($"[{name}] ItemId set to {id}");
        }
        
        public void SetPuzzleController(NetworkedSlotPuzzleController controller)
        {
            _puzzleController = controller;
            DebugLog($"[{name}] Puzzle controller set: {controller?.name ?? "null"}");
        }
        
        public virtual void ResetToOrigin()
        {
            if (!_returnToOriginOnFailedPlacement)
            {
                DebugLog($"[{name}] ResetToOrigin called but disabled by configuration");
                return;
            }
            
            DebugLog($"[{name}] Resetting to origin position: {_originalPosition}");
            
            transform.position = _originalPosition;
            transform.rotation = _originalRotation;
            
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            
            IsPlacedInSlot = false;
            PlacedSlotIndex = -1;
            IsCorrectlyPlaced = false;
            
            HideAllEffects();
        }
        
        protected virtual void ShowEffect(GameObject effect)
        {
            if (effect != null)
            {
                effect.SetActive(true);
                DebugLog($"[{name}] Effect shown: {effect.name}");
            }
        }
        
        protected virtual void HideAllEffects()
        {
            if (_correctPlacementEffect != null)
                _correctPlacementEffect.SetActive(false);
            if (_incorrectPlacementEffect != null)
                _incorrectPlacementEffect.SetActive(false);
        }
        
        protected void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
                DebugLog($"[{name}] Sound played: {clip.name}");
            }
        }
        
        private IEnumerator ReturnToOriginAfterDelay()
        {
            DebugLog($"[{name}] Waiting {_returnToOriginDelay}s before return to origin");
            yield return new WaitForSeconds(_returnToOriginDelay);
            
            if (!IsPlacedInSlot && !_isBeingGrabbed)
            {
                DebugLog($"[{name}] Executing return to origin");
                ResetToOrigin();
            }
            else
            {
                DebugLog($"[{name}] Return to origin cancelled (placed or grabbed)");
            }
            
            _returnCoroutine = null;
        }
        
        protected virtual void ValidateComponents()
        {
            if (_fusionGrabbable == null)
            {
                DebugLogError($"[{name}] Missing FusionVRGrabbable component!");
            }
            
            if (_rigidbody == null)
            {
                DebugLogError($"[{name}] Missing Rigidbody component!");
            }
            
            if (_metaGrabbable == null)
            {
                DebugLogWarning($"[{name}] Missing Grabbable component!");
            }
        }
        
        protected virtual void OnPlacedCustom(int slotIndex, bool isCorrect)
        {
        }
        
        protected virtual void OnRemovedCustom(int slotIndex)
        {
        }
        
        protected virtual void OnDestroy()
        {
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised -= OnMetaGrabbableEvent;
            }
            
            if (_returnCoroutine != null)
            {
                StopCoroutine(_returnCoroutine);
            }
        }
        
        // Debug helpers
        protected void DebugLog(string message)
        {
            if (_enableDebugLogs)
                Debug.Log($"[SlottableItem] {message}");
        }
        
        protected void DebugLogWarning(string message)
        {
            if (_enableDebugLogs)
                Debug.LogWarning($"[SlottableItem] {message}");
        }
        
        protected void DebugLogError(string message)
        {
            Debug.LogError($"[SlottableItem] {message}");
        }
        
        #if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (!_showDetectionGizmos) return;
            
            Gizmos.color = _gizmoColor;
            Gizmos.DrawWireSphere(transform.position, _snapDistance);
            
            if (IsPlacedInSlot)
            {
                Gizmos.color = IsCorrectlyPlaced ? Color.green : Color.red;
                Gizmos.DrawSphere(transform.position, 0.05f);
            }
        }
        
        protected virtual void OnDrawGizmosSelected()
        {
            if (!_showDetectionGizmos) return;
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _snapDistance * 1.5f);
            
            if (_returnToOriginOnFailedPlacement)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, _originalPosition);
                Gizmos.DrawWireCube(_originalPosition, Vector3.one * 0.1f);
            }
        }
        #endif
    }
}