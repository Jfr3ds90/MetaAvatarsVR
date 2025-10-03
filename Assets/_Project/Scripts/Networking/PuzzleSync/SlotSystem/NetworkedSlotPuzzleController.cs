using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.SlotSystem
{
    public enum SlotPuzzleState
    {
        NotStarted,
        WaitingForItems,
        InProgress,
        ValidationPhase,
        Completed,
        Failed
    }
    
    [System.Serializable]
    public class SlotPuzzleConfig
    {
        public int puzzleId = 0;
        public bool requireAllSlotsFilled = true;
        public bool requireCorrectOrder = true;
        public bool allowPartialCompletion = false;
        public float validationDelay = 1f;
        public int maxAttempts = -1;
        public bool autoReset = false;
        public float resetDelay = 3f;
    }
    
    public abstract class NetworkedSlotPuzzleController : NetworkBehaviour
    {
        [Header("Puzzle Configuration")]
        [SerializeField] protected SlotPuzzleConfig _config = new SlotPuzzleConfig();
        
        [Header("Components")]
        [SerializeField] protected NetworkedSlot[] _slots;
        [SerializeField] protected NetworkedSlottableItem[] _items;
        
        [Header("Completion Actions")]
        [SerializeField] protected NetworkedDoor _puzzleDoor;
        [SerializeField] protected GameObject[] _objectsToActivate;
        [SerializeField] protected GameObject[] _objectsToDeactivate;
        
        [Header("Audio")]
        [SerializeField] protected AudioClip _puzzleCompleteSound;
        [SerializeField] protected AudioClip _puzzleFailSound;
        [SerializeField] protected AudioClip _progressSound;
        
        [Header("Network State")]
        [Networked] public SlotPuzzleState CurrentState { get; set; }
        [Networked] public NetworkBool IsSolved { get; set; }
        [Networked] public int CorrectItemsPlaced { get; set; }
        [Networked] public int TotalItemsPlaced { get; set; }
        [Networked] public int CurrentAttempts { get; set; }
        [Networked] public float CompletionTime { get; set; }
        [Networked] public TickTimer ValidationTimer { get; set; }
        
        [Header("Events")]
        public UnityEvent OnPuzzleStarted = new UnityEvent();
        public UnityEvent<SlotPuzzleState> OnStateChanged = new UnityEvent<SlotPuzzleState>();
        public UnityEvent<int, int> OnProgressUpdated = new UnityEvent<int, int>();
        public UnityEvent OnPuzzleCompleted = new UnityEvent();
        public UnityEvent OnPuzzleFailed = new UnityEvent();
        public UnityEvent OnPuzzleReset = new UnityEvent();
        
        protected AudioSource _audioSource;
        protected Dictionary<int, int> _slotItemMapping = new Dictionary<int, int>();
        protected List<int> _expectedPattern;
        
        protected virtual void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            ValidateConfiguration();
            SetupComponents();
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                CurrentState = SlotPuzzleState.NotStarted;
                IsSolved = false;
                CorrectItemsPlaced = 0;
                TotalItemsPlaced = 0;
                CurrentAttempts = 0;
                CompletionTime = 0f;
                
                InitializePuzzle();
            }
            
            RegisterComponents();
        }
        
        protected virtual void InitializePuzzle()
        {
            GenerateExpectedPattern();
            ConfigureSlots();
            ConfigureItems();
            
            TransitionToState(SlotPuzzleState.WaitingForItems);
            
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RPC_RequestStartPuzzle(_config.puzzleId);
            }
        }
        
        protected abstract void GenerateExpectedPattern();
        
        protected virtual void ConfigureSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null)
                {
                    _slots[i].SetPuzzleController(this);
                    
                    if (_expectedPattern != null && i < _expectedPattern.Count)
                    {
                        _slots[i].SetExpectedItem(_expectedPattern[i]);
                    }
                }
            }
        }
        
        protected virtual void ConfigureItems()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] != null)
                {
                    _items[i].SetPuzzleController(this);
                    _items[i].SetItemId(i);
                }
            }
        }
        
        protected virtual void RegisterComponents()
        {
            foreach (var slot in _slots)
            {
                if (slot != null)
                {
                    slot.OnItemPlaced.AddListener(OnSlotItemPlaced);
                    slot.OnItemRemoved.AddListener(OnSlotItemRemoved);
                }
            }
        }
        
        public virtual bool TryPlaceItemInSlot(ISlottable item, int slotId)
        {
            if (!HasStateAuthority) return false;
            
            var slot = GetSlot(slotId);
            if (slot == null || !slot.CanAcceptItem) return false;
            
            bool isCorrect = ValidatePlacement(item, slot);
            bool placed = slot.TryPlaceItem(item, isCorrect, Runner.LocalPlayer);
            
            if (placed)
            {
                TotalItemsPlaced++;
                if (isCorrect)
                {
                    CorrectItemsPlaced++;
                }
                
                _slotItemMapping[slotId] = item.ItemId;
                
                UpdateProgress();
                CheckCompletionCondition();
            }
            
            return placed;
        }
        
        public virtual void RemoveItemFromSlot(int slotId)
        {
            if (!HasStateAuthority) return;
            
            var slot = GetSlot(slotId);
            if (slot == null || !slot.IsOccupied) return;
            
            if (slot.IsCorrect)
            {
                CorrectItemsPlaced--;
            }
            TotalItemsPlaced--;
            
            _slotItemMapping.Remove(slotId);
            slot.RemoveItem();
            
            UpdateProgress();
        }
        
        public virtual bool ValidatePlacement(ISlottable item, NetworkedSlot slot)
        {
            return slot.ValidateItem(item);
        }
        
        protected virtual void CheckCompletionCondition()
        {
            if (CurrentState != SlotPuzzleState.WaitingForItems && 
                CurrentState != SlotPuzzleState.InProgress) return;
            
            bool isComplete = false;
            
            if (_config.requireAllSlotsFilled)
            {
                if (_config.requireCorrectOrder)
                {
                    isComplete = (CorrectItemsPlaced == _slots.Length);
                }
                else
                {
                    isComplete = (TotalItemsPlaced == _slots.Length);
                }
            }
            else if (_config.allowPartialCompletion)
            {
                isComplete = (CorrectItemsPlaced >= GetRequiredCorrectItems());
            }
            
            if (isComplete)
            {
                TransitionToState(SlotPuzzleState.ValidationPhase);
                ValidationTimer = TickTimer.CreateFromSeconds(Runner, _config.validationDelay);
            }
        }
        
        protected virtual int GetRequiredCorrectItems()
        {
            return Mathf.CeilToInt(_slots.Length * 0.75f);
        }
        
        protected virtual void UpdateProgress()
        {
            RPC_NotifyProgress(CorrectItemsPlaced, _slots.Length);
        }
        
        protected virtual void TransitionToState(SlotPuzzleState newState)
        {
            if (!HasStateAuthority) return;
            
            var previousState = CurrentState;
            CurrentState = newState;
            
            RPC_NotifyStateChange(newState);
            
            switch (newState)
            {
                case SlotPuzzleState.WaitingForItems:
                    OnPuzzleStarted?.Invoke();
                    break;
                    
                case SlotPuzzleState.InProgress:
                    break;
                    
                case SlotPuzzleState.ValidationPhase:
                    break;
                    
                case SlotPuzzleState.Completed:
                    CompletePuzzle();
                    break;
                    
                case SlotPuzzleState.Failed:
                    FailPuzzle();
                    break;
            }
        }
        
        protected virtual void CompletePuzzle()
        {
            IsSolved = true;
            CompletionTime = Runner.SimulationTime;
            
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RPC_CompletePuzzle(_config.puzzleId);
            }
            
            if (_puzzleDoor != null)
            {
                _puzzleDoor.RPC_UnlockDoor();
                _puzzleDoor.RPC_RequestOpen();
            }
            
            RPC_NotifyCompletion();
        }
        
        protected virtual void FailPuzzle()
        {
            CurrentAttempts++;
            
            if (_config.maxAttempts > 0 && CurrentAttempts >= _config.maxAttempts)
            {
                RPC_NotifyFailure(true);
            }
            else
            {
                RPC_NotifyFailure(false);
                
                if (_config.autoReset)
                {
                    StartCoroutine(ResetAfterDelay());
                }
            }
        }
        
        private System.Collections.IEnumerator ResetAfterDelay()
        {
            yield return new WaitForSeconds(_config.resetDelay);
            ResetPuzzle();
        }
        
        public virtual void ResetPuzzle()
        {
            if (!HasStateAuthority) return;
            
            CurrentState = SlotPuzzleState.NotStarted;
            IsSolved = false;
            CorrectItemsPlaced = 0;
            TotalItemsPlaced = 0;
            _slotItemMapping.Clear();
            
            foreach (var slot in _slots)
            {
                if (slot != null)
                {
                    slot.ResetSlot();
                }
            }
            
            foreach (var item in _items)
            {
                if (item != null)
                {
                    item.ResetToOrigin();
                }
            }
            
            RPC_NotifyReset();
            
            InitializePuzzle();
        }
        
        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (CurrentState == SlotPuzzleState.ValidationPhase)
                {
                    if (ValidationTimer.Expired(Runner))
                    {
                        if (ValidateFinalConfiguration())
                        {
                            TransitionToState(SlotPuzzleState.Completed);
                        }
                        else
                        {
                            TransitionToState(SlotPuzzleState.Failed);
                        }
                    }
                }
                
                OnFixedUpdateCustom();
            }
        }
        
        protected virtual bool ValidateFinalConfiguration()
        {
            if (_config.requireCorrectOrder)
            {
                return CorrectItemsPlaced == _slots.Length;
            }
            return true;
        }
        
        protected virtual void OnFixedUpdateCustom()
        {
        }
        
        public NetworkedSlot GetSlot(int slotId)
        {
            return _slots.FirstOrDefault(s => s != null && s.SlotId == slotId);
        }
        
        public NetworkedSlottableItem GetItem(int itemId)
        {
            return _items.FirstOrDefault(i => i != null && i.ItemId == itemId);
        }
        
        protected virtual void OnSlotItemPlaced(int itemId)
        {
            if (CurrentState == SlotPuzzleState.WaitingForItems)
            {
                TransitionToState(SlotPuzzleState.InProgress);
            }
        }
        
        protected virtual void OnSlotItemRemoved(int itemId)
        {
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyStateChange(SlotPuzzleState state)
        {
            OnStateChanged?.Invoke(state);
            Debug.Log($"[{GetType().Name}] State changed to: {state}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyProgress(int correct, int total)
        {
            OnProgressUpdated?.Invoke(correct, total);
            PlaySound(_progressSound);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyCompletion()
        {
            OnPuzzleCompleted?.Invoke();
            PlaySound(_puzzleCompleteSound);
            
            foreach (var obj in _objectsToActivate)
            {
                if (obj != null) obj.SetActive(true);
            }
            
            foreach (var obj in _objectsToDeactivate)
            {
                if (obj != null) obj.SetActive(false);
            }
            
            Debug.Log($"[{GetType().Name}] PUZZLE COMPLETED!");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyFailure(NetworkBool isFinal)
        {
            if (isFinal)
            {
                OnPuzzleFailed?.Invoke();
            }
            PlaySound(_puzzleFailSound);
            Debug.Log($"[{GetType().Name}] Puzzle failed. Final: {isFinal}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyReset()
        {
            OnPuzzleReset?.Invoke();
            Debug.Log($"[{GetType().Name}] Puzzle reset");
        }
        
        protected void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
        
        protected virtual void ValidateConfiguration()
        {
            if (_slots == null || _slots.Length == 0)
            {
                Debug.LogError($"[{GetType().Name}] No slots configured!");
            }
            
            if (_items == null || _items.Length == 0)
            {
                Debug.LogError($"[{GetType().Name}] No items configured!");
            }
        }
        
        protected virtual void SetupComponents()
        {
            if (_slots == null || _slots.Length == 0)
            {
                _slots = GetComponentsInChildren<NetworkedSlot>();
            }
            
            if (_items == null || _items.Length == 0)
            {
                _items = FindObjectsOfType<NetworkedSlottableItem>();
            }
        }
        
        #if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] protected bool _debugMode = false;
        
        protected virtual void OnGUI()
        {
            if (!_debugMode || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 250));
            GUILayout.Label($"{GetType().Name} Debug");
            GUILayout.Label($"State: {CurrentState}");
            GUILayout.Label($"Progress: {CorrectItemsPlaced}/{_slots.Length}");
            GUILayout.Label($"Attempts: {CurrentAttempts}");
            GUILayout.Label($"Is Solved: {IsSolved}");
            
            if (HasStateAuthority)
            {
                if (GUILayout.Button("Complete Puzzle"))
                {
                    TransitionToState(SlotPuzzleState.Completed);
                }
                
                if (GUILayout.Button("Fail Puzzle"))
                {
                    TransitionToState(SlotPuzzleState.Failed);
                }
                
                if (GUILayout.Button("Reset Puzzle"))
                {
                    ResetPuzzle();
                }
            }
            GUILayout.EndArea();
        }
        #endif
    }
}