using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.SlotSystem
{
    public class NetworkedSlot : NetworkBehaviour
    {
        [Header("Slot Configuration")]
        [SerializeField] protected int _slotId = 0;
        [SerializeField] protected int _expectedItemId = -1;
        [SerializeField] protected Transform _itemAnchor;
        [SerializeField] protected bool _snapToCenter = true;
        [SerializeField] protected bool _lockRotation = true;
        
        [Header("Visual Feedback")]
        [SerializeField] protected MeshRenderer _slotRenderer;
        [SerializeField] protected Material _emptyMaterial;
        [SerializeField] protected Material _correctMaterial;
        [SerializeField] protected Material _incorrectMaterial;
        [SerializeField] protected GameObject _emptyVisual;
        [SerializeField] protected GameObject _occupiedVisual;
        
        [Header("Audio")]
        [SerializeField] protected AudioClip _correctSound;
        [SerializeField] protected AudioClip _incorrectSound;
        
        [Header("Network State")]
        [Networked] public int NetworkedSlotId { get; set; }
        [Networked] public int ExpectedItemId { get; set; }
        [Networked] public NetworkBool IsOccupied { get; set; }
        [Networked] public int PlacedItemId { get; set; }
        [Networked] public NetworkBool IsCorrect { get; set; }
        [Networked] public PlayerRef LastInteractedPlayer { get; set; }
        
        [Header("Events")]
        public UnityEvent<int> OnItemPlaced = new UnityEvent<int>();
        public UnityEvent<int> OnItemRemoved = new UnityEvent<int>();
        public UnityEvent OnCorrectPlacement = new UnityEvent();
        public UnityEvent OnIncorrectPlacement = new UnityEvent();
        
        protected ISlottable _currentItem;
        protected AudioSource _audioSource;
        [SerializeField] protected NetworkedSlotPuzzleController _puzzleController;
        
        public int SlotId => _slotId;
        public bool CanAcceptItem => !IsOccupied;
        
        protected virtual void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            if (_slotRenderer == null)
                _slotRenderer = GetComponent<MeshRenderer>();
                
            if (_itemAnchor == null)
                _itemAnchor = transform;
                
            ValidateComponents();
        }
        
        public override void Spawned()
        {
            // In Shared Mode, Master Client initializes slot state
            if (Runner.IsSharedModeMasterClient)
            {
                NetworkedSlotId = _slotId;
                ExpectedItemId = _expectedItemId;
                IsOccupied = false;
                PlacedItemId = -1;
                IsCorrect = false;
                LastInteractedPlayer = PlayerRef.None;
            }
            
            UpdateVisualState();
            OnSpawnedCustom();
        }
        
        protected virtual void OnSpawnedCustom()
        {
        }
        
        public virtual bool TryPlaceItem(ISlottable item, bool isCorrect, PlayerRef player = default)
        {
            // Only Master Client can modify slot state in Shared Mode
            if (!Runner.IsSharedModeMasterClient) return false;
            if (IsOccupied) return false;
            if (item == null) return false;
            
            _currentItem = item;
            IsOccupied = true;
            PlacedItemId = item.ItemId;
            IsCorrect = isCorrect;
            LastInteractedPlayer = player != default ? player : Runner.LocalPlayer;
            
            if (_snapToCenter)
            {
                RPC_SnapItemToSlot(item.ItemId);
            }
            
            RPC_NotifyItemPlaced(item.ItemId, isCorrect);
            
            return true;
        }
        
        public virtual void RemoveItem()
        {
            // Only Master Client can modify slot state in Shared Mode
            if (!Runner.IsSharedModeMasterClient) return;
            
            Debug.Log($"[NetworkedSlot] RemoveItem called on slot {_slotId}. IsOccupied: {IsOccupied}, PlacedItemId: {PlacedItemId}");
            
            if (!IsOccupied)
            {
                Debug.LogWarning($"[NetworkedSlot] Slot {_slotId} is not occupied, nothing to remove");
                return;
            }
            
            int removedItemId = PlacedItemId;
            
            // Limpiar estado del slot
            _currentItem = null;
            IsOccupied = false;
            PlacedItemId = -1;
            IsCorrect = false;
            LastInteractedPlayer = PlayerRef.None;
            
            Debug.Log($"[NetworkedSlot] Slot {_slotId} cleared. Removed item: {removedItemId}");
            
            RPC_NotifyItemRemoved(removedItemId);
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_SnapItemToSlot(int itemId)
        {
            // Find the item and snap it to position on all clients
            var items = FindObjectsOfType<NetworkedSlottableItem>();
            foreach (var item in items)
            {
                if (item.ItemId == itemId)
                {
                    item.Transform.position = _itemAnchor.position;
                    
                    if (_lockRotation)
                    {
                        item.Transform.rotation = _itemAnchor.rotation;
                    }
                    
                    var rb = item.Transform.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true; // Mantener kinematic cuando está en slot
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        Debug.Log($"[NetworkedSlot] Item {itemId} rigidbody set to kinematic for slot snap");
                    }
                    break;
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_NotifyItemPlaced(int itemId, NetworkBool isCorrect)
        {
            // Only Master Client should broadcast this in Shared Mode
            if (!Runner.IsSharedModeMasterClient && Runner.LocalPlayer != PlayerRef.None)
            {
                return;
            }
            
            OnItemPlaced?.Invoke(itemId);
            
            if (isCorrect)
            {
                OnCorrectPlacement?.Invoke();
                PlaySound(_correctSound);
            }
            else
            {
                OnIncorrectPlacement?.Invoke();
                PlaySound(_incorrectSound);
            }
            
            UpdateVisualState();
            OnItemPlacedCustom(itemId, isCorrect);
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        protected virtual void RPC_NotifyItemRemoved(int itemId)
        {
            Debug.Log($"[NetworkedSlot] RPC_NotifyItemRemoved called for slot {_slotId}, itemId: {itemId}");
            
            // Only Master Client should broadcast this in Shared Mode
            if (!Runner.IsSharedModeMasterClient && Runner.LocalPlayer != PlayerRef.None)
            {
                Debug.Log($"[NetworkedSlot] Not master client, skipping notification broadcast");
                return;
            }
            
            Debug.Log($"[NetworkedSlot] Broadcasting item removal notification for slot {_slotId}");
            OnItemRemoved?.Invoke(itemId);
            UpdateVisualState();
            OnItemRemovedCustom(itemId);
        }
        
        public virtual void SetExpectedItem(int itemId)
        {
            _expectedItemId = itemId;
            if (Runner.IsSharedModeMasterClient)
            {
                ExpectedItemId = itemId;
            }
        }
        
        public void SetSlotId(int id)
        {
            _slotId = id;
            if (Runner != null && Runner.IsSharedModeMasterClient)
            {
                NetworkedSlotId = id;
            }
            Debug.Log($"[NetworkedSlot] Slot {name} ID set to {id}");
        }
        
        public void SetPuzzleController(NetworkedSlotPuzzleController controller)
        {
            _puzzleController = controller;
            Debug.Log($"[NetworkedSlot] Slot {_slotId} controller set to {controller?.name ?? "null"}");
        }
        
        protected virtual void UpdateVisualState()
        {
            if (_slotRenderer != null)
            {
                if (!IsOccupied && _emptyMaterial != null)
                {
                    _slotRenderer.material = _emptyMaterial;
                }
                else if (IsCorrect && _correctMaterial != null)
                {
                    _slotRenderer.material = _correctMaterial;
                }
                else if (!IsCorrect && _incorrectMaterial != null)
                {
                    _slotRenderer.material = _incorrectMaterial;
                }
            }
            
            if (_emptyVisual != null)
                _emptyVisual.SetActive(!IsOccupied);
                
            if (_occupiedVisual != null)
                _occupiedVisual.SetActive(IsOccupied);
        }
        
        protected void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
        
        public virtual void ResetSlot()
        {
            if (Runner.IsSharedModeMasterClient)
            {
                RemoveItem();
                IsOccupied = false;
                PlacedItemId = -1;
                IsCorrect = false;
                LastInteractedPlayer = PlayerRef.None;
            }
            
            _currentItem = null;
            UpdateVisualState();
        }
        
        public bool ValidateItem(ISlottable item)
        {
            if (_expectedItemId == -1) return true;
            return item.ItemId == _expectedItemId;
        }
        
        protected virtual void ValidateComponents()
        {
            if (_itemAnchor == null)
            {
                Debug.LogWarning($"[NetworkedSlot] Item anchor not set on {name}, using transform");
            }
        }
        
        protected virtual void OnItemPlacedCustom(int itemId, bool isCorrect)
        {
        }
        
        protected virtual void OnItemRemovedCustom(int itemId)
        {
        }
        
        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = IsOccupied ? Color.green : Color.red;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
            
            if (_itemAnchor != null && _itemAnchor != transform)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_itemAnchor.position, 0.1f);
                Gizmos.DrawLine(transform.position, _itemAnchor.position);
            }
        }
        #endif
    }
}