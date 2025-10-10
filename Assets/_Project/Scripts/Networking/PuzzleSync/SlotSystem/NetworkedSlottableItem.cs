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
    public abstract class NetworkedSlottableItem : NetworkBehaviour, ISlottable
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
        protected int _cachedSlotIndex = -1; // Cache del slot anterior
        protected bool _wasPlacedBeforeGrab = false; // Flag para recordar si estaba en un slot antes de agarrar
        
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
            DebugLog($"[{name}] Spawned - IsMasterClient: {Runner.IsSharedModeMasterClient}, LocalPlayer: {Runner.LocalPlayer}");
            
            // In Shared Mode, items maintain their own state but Master Client validates
            NetworkedItemId = _itemId;
            
            // Solo el Master inicializa el estado, los demás lo reciben por sincronización
            if (Runner.IsSharedModeMasterClient)
            {
                PlacedSlotIndex = -1;
                IsPlacedInSlot = false;
                IsCorrectlyPlaced = false;
                DebugLog($"[{name}] Master initialized network state. ItemId: {_itemId}");
            }
            else
            {
                DebugLog($"[{name}] Client waiting for state sync. ItemId: {_itemId}");
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
            DebugLog($"[{name}] ========== OnGrabbed START ==========");
            DebugLog($"[{name}] Runner Info - IsMaster: {Runner.IsSharedModeMasterClient}, Player: {Runner.LocalPlayer}");
            DebugLog($"[{name}] Network State - IsPlaced: {IsPlacedInSlot}, CurrentSlot: {PlacedSlotIndex}");
            DebugLog($"[{name}] Local Cache - WasPlaced: {_wasPlacedBeforeGrab}, CachedSlot: {_cachedSlotIndex}");
            
            _isBeingGrabbed = true;
            
            // Al agarrar, hacer el objeto kinematic para que no caiga mientras lo sostenemos
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                DebugLog($"[{name}] Rigidbody set to kinematic while being held");
            }
            
            // Cancelar cualquier retorno pendiente
            if (_returnCoroutine != null)
            {
                StopCoroutine(_returnCoroutine);
                _returnCoroutine = null;
                DebugLog($"[{name}] Cancelled pending return to origin");
            }
            
            // Guardar el estado actual antes de remover del slot
            if (IsPlacedInSlot)
            {
                _wasPlacedBeforeGrab = true;
                _cachedSlotIndex = PlacedSlotIndex;
                DebugLog($"[{name}] Cached slot {_cachedSlotIndex} before removal");
                
                // NO llamamos RPC_RequestRemovalFromSlot aquí
                // Lo haremos en OnReleased si es necesario
            }
            else
            {
                _wasPlacedBeforeGrab = false;
                _cachedSlotIndex = -1;
                DebugLog($"[{name}] Item was not placed before grab, no cache needed");
            }
            
            DebugLog($"[{name}] ========== OnGrabbed END - Cache set: WasPlaced={_wasPlacedBeforeGrab}, CachedSlot={_cachedSlotIndex} ==========");
        }
        
        protected virtual void OnReleased()
        {
            DebugLog($"[{name}] ========== OnReleased START ==========");
            DebugLog($"[{name}] Position: {transform.position}");
            DebugLog($"[{name}] WasPlaced: {_wasPlacedBeforeGrab}, CachedSlot: {_cachedSlotIndex}");
            DebugLog($"[{name}] Current IsPlacedInSlot: {IsPlacedInSlot}, PlacedSlotIndex: {PlacedSlotIndex}");
            
            _isBeingGrabbed = false;
            
            // Verificar slots inmediatamente
            CheckNearbySlot();
            
            DebugLog($"[{name}] ========== OnReleased END ==========");
        }
        
        protected virtual void CheckNearbySlot()
        {
            DebugLog($"[{name}] CheckNearbySlot - Starting detection at position: {transform.position}");
            DebugLog($"[{name}] Detection settings - Distance: {_snapDistance}, UseTag: {_useTagDetection}, UseLayer: {_useLayerDetection}");
            DebugLog($"[{name}] Current puzzle controller: {(_puzzleController != null ? _puzzleController.name : "NULL")}");
            DebugLog($"[{name}] Cache status - WasPlaced: {_wasPlacedBeforeGrab}, CachedSlot: {_cachedSlotIndex}");
            
            // Ensure we have a puzzle controller reference
            if (_puzzleController == null)
            {
                _puzzleController = FindObjectOfType<NetworkedSlotPuzzleController>();
                if (_puzzleController != null)
                {
                    DebugLog($"[{name}] Found and set puzzle controller: {_puzzleController.name}");
                }
                else
                {
                    DebugLogWarning($"[{name}] No puzzle controller found in scene during slot check!");
                }
            }
            
            // Obtener el collider del item para usar su centro real
            Collider itemCollider = GetComponent<Collider>();
            Vector3 detectionCenter = itemCollider != null ? itemCollider.bounds.center : transform.position;
            
            // Usar OverlapSphere con el centro del collider
            Collider[] colliders = Physics.OverlapSphere(detectionCenter, _snapDistance);
            DebugLog($"[{name}] Found {colliders.Length} colliders in range from center: {detectionCenter}");
            
            NetworkedSlot bestSlot = null;
            float bestScore = float.MaxValue;
            
            foreach (var collider in colliders)
            {
                // Ignorar nuestro propio collider
                if (collider == itemCollider)
                    continue;
                    
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
                    // Usar el centro del collider del slot para mejor precisión
                    Vector3 slotCenter = collider.bounds.center;
                    float centerDistance = Vector3.Distance(detectionCenter, slotCenter);
                    
                    // Calcular si el centro del item está dentro del bounds del slot
                    bool isInsideSlot = collider.bounds.Contains(detectionCenter);
                    
                    // Calcular score: prioridad a slots que contienen el item, luego por distancia
                    float score = isInsideSlot ? centerDistance * 0.1f : centerDistance;
                    
                    DebugLog($"[{name}] Found slot {slot.SlotId} at distance {centerDistance:F2}, Inside: {isInsideSlot}, Score: {score:F2}, IsOccupied: {slot.IsOccupied}");
                    
                    if (!slot.IsOccupied && score < bestScore)
                    {
                        bestSlot = slot;
                        bestScore = score;
                    }
                }
                else
                {
                    // Intentar buscar en el padre
                    slot = collider.GetComponentInParent<NetworkedSlot>();
                    if (slot != null)
                    {
                        Vector3 slotCenter = collider.bounds.center;
                        float centerDistance = Vector3.Distance(detectionCenter, slotCenter);
                        bool isInsideSlot = collider.bounds.Contains(detectionCenter);
                        float score = isInsideSlot ? centerDistance * 0.1f : centerDistance;
                        
                        DebugLog($"[{name}] Found slot in parent {slot.SlotId} at distance {centerDistance:F2}, Score: {score:F2}");
                        
                        if (!slot.IsOccupied && score < bestScore)
                        {
                            bestSlot = slot;
                            bestScore = score;
                        }
                    }
                }
            }
            
            if (bestSlot != null)
            {
                // Caso 1: Se está colocando en un nuevo slot
                int newSlotId = bestSlot.SlotId;
                
                // Si teníamos un slot en cache y es diferente al nuevo, limpiar el anterior primero
                if (_wasPlacedBeforeGrab && _cachedSlotIndex >= 0 && _cachedSlotIndex != newSlotId)
                {
                    DebugLog($"[{name}] Moving from slot {_cachedSlotIndex} to slot {newSlotId}, calling RPC_RequestRemovalFromCachedSlot");
                    DebugLog($"[{name}] About to call RPC for slot movement - Old: {_cachedSlotIndex}, New: {newSlotId}");
                    
                    RPC_RequestRemovalFromCachedSlot(_cachedSlotIndex);
                    
                    DebugLog($"[{name}] RPC called for slot movement");
                }
                else if (_wasPlacedBeforeGrab && _cachedSlotIndex == newSlotId)
                {
                    DebugLog($"[{name}] Returning to same slot {newSlotId}, no removal needed");
                }
                
                // Solicitar colocación en el nuevo slot
                DebugLog($"[{name}] Requesting placement in slot {newSlotId} with score {bestScore:F2}");
                RPC_RequestPlacement(newSlotId);
                
                // Limpiar cache
                _cachedSlotIndex = -1;
                _wasPlacedBeforeGrab = false;
                
                // IMPORTANTE: Mantener kinematic mientras esperamos confirmación del placement
                // El RPC_NotifyPlacement se encargará de hacer el snap y mantener kinematic
            }
            else
            {
                // Caso 2: No se está colocando en ningún slot
                DebugLog($"[{name}] No available slot found");
                
                // Si teníamos un slot en cache, limpiarlo
                if (_wasPlacedBeforeGrab && _cachedSlotIndex >= 0)
                {
                    DebugLog($"[{name}] Item not placed in any slot. Clearing cached slot {_cachedSlotIndex}, calling RPC");
                    DebugLog($"[{name}] About to call RPC_RequestRemovalFromCachedSlot with slotId: {_cachedSlotIndex}");
                    
                    // Llamar el RPC para limpiar el slot en cache
                    RPC_RequestRemovalFromCachedSlot(_cachedSlotIndex);
                    
                    DebugLog($"[{name}] RPC_RequestRemovalFromCachedSlot has been called");
                    
                    // Actualizar estado local inmediatamente
                    IsPlacedInSlot = false;
                    PlacedSlotIndex = -1;
                    IsCorrectlyPlaced = false;
                    
                    DebugLog($"[{name}] Local state updated after removing from cached slot");
                }
                else
                {
                    DebugLog($"[{name}] No cached slot to clear (WasPlaced: {_wasPlacedBeforeGrab}, Cached: {_cachedSlotIndex})");
                }
                
                // Limpiar cache
                _cachedSlotIndex = -1;
                _wasPlacedBeforeGrab = false;
                
                // IMPORTANTE: Activar físicas ya que NO se está colocando en ningún slot
                if (_rigidbody != null)
                {
                    _rigidbody.isKinematic = false;
                    DebugLog($"[{name}] Physics enabled - object will fall with gravity");
                }
                
                // Retornar al origen si está configurado
                if (!IsPlacedInSlot && _returnToOriginOnFailedPlacement)
                {
                    DebugLog($"[{name}] Starting return to origin timer ({_returnToOriginDelay}s)");
                    _returnCoroutine = StartCoroutine(ReturnToOriginAfterDelay());
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_RequestPlacement(int slotId, RpcInfo info = default)
        {
            // In Shared Mode, forward requests to Master Client for processing
            if (!Runner.IsSharedModeMasterClient)
            {
                DebugLog($"[{name}] Forwarding placement request to Master Client");
                return;
            }
            
            DebugLog($"[{name}] RPC_RequestPlacement - SlotId: {slotId}, From: {info.Source}");
            
            // Find the puzzle controller if not set (important for Shared Mode)
            if (_puzzleController == null)
            {
                _puzzleController = FindObjectOfType<NetworkedSlotPuzzleController>();
                if (_puzzleController != null)
                {
                    DebugLog($"[{name}] Found puzzle controller: {_puzzleController.name}");
                }
            }
            
            if (_puzzleController != null)
            {
                // Get the slot first to validate it exists
                var slot = _puzzleController.GetSlot(slotId);
                if (slot == null)
                {
                    DebugLogWarning($"[{name}] Slot {slotId} not found in puzzle controller");
                    RPC_PlacementFailed();
                    return;
                }
                
                DebugLog($"[{name}] Attempting to place in slot {slotId}. Slot occupied: {slot.IsOccupied}");
                
                bool success = _puzzleController.TryPlaceItemInSlot(this, slotId, info.Source);
                
                if (success)
                {
                    bool isCorrect = _puzzleController.ValidatePlacement(this, slot);
                    
                    // El Master actualiza su estado local
                    IsPlacedInSlot = true;
                    PlacedSlotIndex = slotId;
                    IsCorrectlyPlaced = isCorrect;
                    
                    DebugLog($"[{name}] Placement successful. Correct: {isCorrect}");
                    DebugLog($"[{name}] Master state updated - IsPlaced: {IsPlacedInSlot}, Slot: {PlacedSlotIndex}");
                    
                    // Notificar a TODOS los clientes (incluido el master) para que actualicen su estado
                    RPC_NotifyPlacement(slotId, isCorrect);
                }
                else
                {
                    DebugLog($"[{name}] Placement failed - TryPlaceItemInSlot returned false");
                    DebugLog($"[{name}] Possible reasons: Slot occupied, not master client, or slot cannot accept item");
                    RPC_PlacementFailed();
                }
            }
            else
            {
                DebugLogWarning($"[{name}] No puzzle controller found in scene!");
                RPC_PlacementFailed();
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_RequestRemovalFromSlot(RpcInfo info = default)
        {
            // In Shared Mode, forward requests to Master Client for processing
            if (!Runner.IsSharedModeMasterClient)
            {
                DebugLog($"[{name}] Forwarding removal request to Master Client");
                return;
            }
            
            DebugLog($"[{name}] RPC_RequestRemovalFromSlot - From: {info.Source}, CurrentSlot: {PlacedSlotIndex}");
            
            if (_puzzleController != null && PlacedSlotIndex >= 0)
            {
                int previousSlot = PlacedSlotIndex;
                
                // Remover del puzzle controller
                _puzzleController.RemoveItemFromSlot(previousSlot);
                
                // El Master actualiza su estado local
                IsPlacedInSlot = false;
                IsCorrectlyPlaced = false;
                PlacedSlotIndex = -1;
                
                DebugLog($"[{name}] Master removed item from slot {previousSlot}");
                DebugLog($"[{name}] Master state cleared - IsPlaced: {IsPlacedInSlot}, Slot: {PlacedSlotIndex}");
                
                // Notificar a TODOS los clientes para que actualicen su estado
                RPC_NotifyRemoval(previousSlot);
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_RequestRemovalFromCachedSlot(int cachedSlotId, RpcInfo info = default)
        {
            DebugLog($"[{name}] RPC_RequestRemovalFromCachedSlot CALLED - SlotId: {cachedSlotId}, IsMaster: {Runner.IsSharedModeMasterClient}, From: {info.Source}");
            
            // In Shared Mode, only Master Client processes the actual removal
            if (!Runner.IsSharedModeMasterClient)
            {
                DebugLog($"[{name}] Not master client, skipping processing but RPC was received");
                return;
            }
            
            DebugLog($"[{name}] Master Client processing removal for cached slot {cachedSlotId}");
            
            // Buscar el controller si no lo tenemos
            if (_puzzleController == null)
            {
                _puzzleController = FindObjectOfType<NetworkedSlotPuzzleController>();
                if (_puzzleController != null)
                {
                    DebugLog($"[{name}] Found puzzle controller: {_puzzleController.name}");
                }
                else
                {
                    DebugLogWarning($"[{name}] No puzzle controller found, cannot remove from slot!");
                    return;
                }
            }
            
            if (cachedSlotId >= 0)
            {
                DebugLog($"[{name}] Calling RemoveItemFromSlot on controller for slot {cachedSlotId}");
                
                // Limpiar el slot específico
                _puzzleController.RemoveItemFromSlot(cachedSlotId);
                
                // El Master actualiza su estado si coincide con el slot actual
                if (PlacedSlotIndex == cachedSlotId)
                {
                    IsPlacedInSlot = false;
                    IsCorrectlyPlaced = false;
                    PlacedSlotIndex = -1;
                    DebugLog($"[{name}] Master state updated after cached slot removal");
                }
                
                DebugLog($"[{name}] Successfully removed from cached slot {cachedSlotId}");
                
                // IMPORTANTE: Notificar a TODOS los clientes para que actualicen su estado
                RPC_NotifyRemoval(cachedSlotId);
            }
            else
            {
                DebugLogWarning($"[{name}] Invalid cached slot id: {cachedSlotId}");
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_NotifyPlacement(int slotId, NetworkBool isCorrect)
        {
            DebugLog($"[{name}] RPC_NotifyPlacement received - Slot: {slotId}, Correct: {isCorrect}, IsMaster: {Runner.IsSharedModeMasterClient}");
            
            // IMPORTANTE: En Shared Mode, TODOS los clientes deben actualizar su estado local
            // cuando reciben esta notificación
            IsPlacedInSlot = true;
            PlacedSlotIndex = slotId;
            IsCorrectlyPlaced = isCorrect;
            
            DebugLog($"[{name}] Local state updated - IsPlaced: {IsPlacedInSlot}, SlotIndex: {PlacedSlotIndex}");
            
            OnPlacedInSlot(slotId, isCorrect);
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_NotifyRemoval(int slotId)
        {
            DebugLog($"[{name}] RPC_NotifyRemoval received - Slot: {slotId}, IsMaster: {Runner.IsSharedModeMasterClient}");
            
            // IMPORTANTE: En Shared Mode, TODOS los clientes deben actualizar su estado local
            // cuando reciben esta notificación de remoción
            if (PlacedSlotIndex == slotId)
            {
                IsPlacedInSlot = false;
                PlacedSlotIndex = -1;
                IsCorrectlyPlaced = false;
                DebugLog($"[{name}] Local state cleared after removal from slot {slotId}");
            }
            
            OnRemovedFromSlot(slotId);
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_PlacementFailed()
        {
            // Only Master Client should send this RPC in Shared Mode
            if (!Runner.IsSharedModeMasterClient && Runner.LocalPlayer != PlayerRef.None)
            {
                return;
            }
            
            DebugLog($"[{name}] RPC_PlacementFailed");
            
            if (_returnToOriginOnFailedPlacement)
            {
                ResetToOrigin();
            }
        }
        
        public virtual void OnPlacedInSlot(int slotIndex, bool isCorrect)
        {
            DebugLog($"[{name}] OnPlacedInSlot - Slot: {slotIndex}, Correct: {isCorrect}");
            
            // Mantener el objeto kinematic cuando está en un slot
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                DebugLog($"[{name}] Rigidbody set to kinematic - object snapped to slot");
            }
            
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
            
            // Si se remueve de un slot y no está siendo agarrado, activar físicas
            if (!_isBeingGrabbed && _rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                DebugLog($"[{name}] Rigidbody physics enabled after removal from slot");
            }
            
            OnItemRemoved?.Invoke();
            HideAllEffects();
            PlaySound(_removalSound);
            OnRemovedCustom(slotIndex);
        }
        
        public void SetItemId(int id)
        {
            _itemId = id;
            NetworkedItemId = id;
            DebugLog($"[{name}] ItemId set to {id}");
        }
        
        public void SetPuzzleController(NetworkedSlotPuzzleController controller)
        {
            _puzzleController = controller;
            DebugLog($"[{name}] Puzzle controller set: {controller?.name ?? "null"}");
            
            // In Shared Mode, ensure the reference is properly set
            if (controller != null && Runner != null && Runner.IsSharedModeMasterClient)
            {
                DebugLog($"[{name}] Master Client confirmed puzzle controller reference");
            }
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
                _rigidbody.isKinematic = false; // Permitir físicas cuando vuelve al origen
                DebugLog($"[{name}] Rigidbody physics enabled at origin");
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