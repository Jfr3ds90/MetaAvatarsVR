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
            if (HasStateAuthority)
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
            if (!HasStateAuthority) return false;
            if (IsOccupied) return false;
            if (item == null) return false;
            
            _currentItem = item;
            IsOccupied = true;
            PlacedItemId = item.ItemId;
            IsCorrect = isCorrect;
            LastInteractedPlayer = player != default ? player : Runner.LocalPlayer;
            
            if (_snapToCenter)
            {
                SnapItemToSlot(item);
            }
            
            RPC_NotifyItemPlaced(item.ItemId, isCorrect);
            
            return true;
        }
        
        public virtual void RemoveItem()
        {
            if (!HasStateAuthority) return;
            if (!IsOccupied) return;
            
            int removedItemId = PlacedItemId;
            
            _currentItem = null;
            IsOccupied = false;
            PlacedItemId = -1;
            IsCorrect = false;
            
            RPC_NotifyItemRemoved(removedItemId);
        }
        
        protected virtual void SnapItemToSlot(ISlottable item)
        {
            if (item?.Transform == null) return;
            
            item.Transform.position = _itemAnchor.position;
            
            if (_lockRotation)
            {
                item.Transform.rotation = _itemAnchor.rotation;
            }
            
            var rb = item.Transform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyItemPlaced(int itemId, NetworkBool isCorrect)
        {
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
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyItemRemoved(int itemId)
        {
            OnItemRemoved?.Invoke(itemId);
            UpdateVisualState();
            OnItemRemovedCustom(itemId);
        }
        
        public virtual void SetExpectedItem(int itemId)
        {
            _expectedItemId = itemId;
            if (HasStateAuthority)
            {
                ExpectedItemId = itemId;
            }
        }
        
        public void SetPuzzleController(NetworkedSlotPuzzleController controller)
        {
            _puzzleController = controller;
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
            if (HasStateAuthority)
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