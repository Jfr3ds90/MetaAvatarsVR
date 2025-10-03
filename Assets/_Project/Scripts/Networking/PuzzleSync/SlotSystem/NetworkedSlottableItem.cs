using System;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

namespace MetaAvatarsVR.Networking.PuzzleSync.SlotSystem
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(FusionVRGrabbable))]
    [RequireComponent(typeof(Rigidbody))]
    public abstract class NetworkedSlottableItem : NetworkBehaviour, ISlottable
    {
        [Header("Item Configuration")]
        [SerializeField] protected int _itemId = 0;
        [SerializeField] protected float _snapDistance = 0.3f;
        [SerializeField] protected LayerMask _slotLayerMask = -1;
        
        [Header("Visual Feedback")]
        [SerializeField] protected GameObject _correctPlacementEffect;
        [SerializeField] protected GameObject _incorrectPlacementEffect;
        [SerializeField] protected MeshRenderer _meshRenderer;
        
        [Header("Audio")]
        [SerializeField] protected AudioClip _placementSound;
        [SerializeField] protected AudioClip _removalSound;
        
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
        
        protected FusionVRGrabbable _grabbable;
        protected Rigidbody _rigidbody;
        protected AudioSource _audioSource;
        protected NetworkedSlotPuzzleController _puzzleController;
        protected Vector3 _originalPosition;
        protected Quaternion _originalRotation;
        
        public int ItemId => _itemId;
        public Transform Transform => transform;
        public bool IsPlaced => IsPlacedInSlot;
        public int CurrentSlotIndex => PlacedSlotIndex;
        
        protected virtual void Awake()
        {
            _grabbable = GetComponent<FusionVRGrabbable>();
            _rigidbody = GetComponent<Rigidbody>();
            
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
                
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
            
            ValidateComponents();
        }
        
        protected virtual void Start()
        {
            var metaGrabbable = GetComponent<Grabbable>();
            if (metaGrabbable != null)
            {
                metaGrabbable.WhenPointerEventRaised += OnGrabbableEvent;
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                NetworkedItemId = _itemId;
                PlacedSlotIndex = -1;
                IsPlacedInSlot = false;
                IsCorrectlyPlaced = false;
            }
            
            OnSpawnedCustom();
        }
        
        protected virtual void OnSpawnedCustom()
        {
        }
        
        protected virtual void OnGrabbableEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select)
            {
                OnGrabbed();
            }
            else if (evt.Type == PointerEventType.Unselect)
            {
                OnReleased();
            }
        }
        
        protected virtual void OnGrabbed()
        {
            if (IsPlacedInSlot)
            {
                RPC_RequestRemovalFromSlot();
            }
        }
        
        protected virtual void OnReleased()
        {
            CheckNearbySlot();
        }
        
        protected virtual void CheckNearbySlot()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, _snapDistance, _slotLayerMask);
            
            foreach (var collider in colliders)
            {
                var slot = collider.GetComponent<NetworkedSlot>();
                if (slot != null && !slot.IsOccupied)
                {
                    RPC_RequestPlacement(slot.SlotId);
                    return;
                }
            }
            
            if (!IsPlacedInSlot)
            {
                StartCoroutine(ReturnToOriginAfterDelay());
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        protected virtual void RPC_RequestPlacement(int slotId, RpcInfo info = default)
        {
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
                    
                    RPC_NotifyPlacement(slotId, isCorrect);
                }
                else
                {
                    RPC_PlacementFailed();
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        protected virtual void RPC_RequestRemovalFromSlot(RpcInfo info = default)
        {
            if (_puzzleController != null && PlacedSlotIndex >= 0)
            {
                _puzzleController.RemoveItemFromSlot(PlacedSlotIndex);
                
                IsPlacedInSlot = false;
                IsCorrectlyPlaced = false;
                int previousSlot = PlacedSlotIndex;
                PlacedSlotIndex = -1;
                
                RPC_NotifyRemoval(previousSlot);
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyPlacement(int slotId, NetworkBool isCorrect)
        {
            OnPlacedInSlot(slotId, isCorrect);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyRemoval(int slotId)
        {
            OnRemovedFromSlot(slotId);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_PlacementFailed()
        {
            ResetToOrigin();
        }
        
        public virtual void OnPlacedInSlot(int slotIndex, bool isCorrect)
        {
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
        }
        
        public void SetPuzzleController(NetworkedSlotPuzzleController controller)
        {
            _puzzleController = controller;
        }
        
        public virtual void ResetToOrigin()
        {
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
                effect.SetActive(true);
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
            }
        }
        
        private System.Collections.IEnumerator ReturnToOriginAfterDelay()
        {
            yield return new WaitForSeconds(2f);
            if (!IsPlacedInSlot)
            {
                ResetToOrigin();
            }
        }
        
        protected virtual void ValidateComponents()
        {
            if (_grabbable == null)
            {
                Debug.LogError($"[{GetType().Name}] Missing FusionVRGrabbable component on {name}");
            }
            
            if (_rigidbody == null)
            {
                Debug.LogError($"[{GetType().Name}] Missing Rigidbody component on {name}");
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
            var metaGrabbable = GetComponent<Grabbable>();
            if (metaGrabbable != null)
            {
                metaGrabbable.WhenPointerEventRaised -= OnGrabbableEvent;
            }
        }
        
        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _snapDistance);
        }
        #endif
    }
}