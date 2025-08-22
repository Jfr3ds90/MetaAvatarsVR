using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedPiano : NetworkBehaviour
    {
        [Header("Piano Configuration")]
        [SerializeField] private NetworkedPianoKey[] _pianoKeys;
        [SerializeField] private int[] _correctSequence = { 0, 1, 2, 3 };
        [SerializeField] private NetworkedDoor _puzzleDoor;
        [SerializeField] private int _puzzleId = 1;
        [SerializeField] private float _sequenceResetDelay = 3f;
        
        [Header("Audio")]
        [SerializeField] private AudioClip _successMelody;
        [SerializeField] private AudioClip _failureSound;
        [SerializeField] private bool _playbackCorrectSequence = true;
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject _successIndicator;
        [SerializeField] private GameObject _failureIndicator;
        [SerializeField] private GameObject[] _progressIndicators;
        
        [Header("Network State")]
        [Networked, Capacity(20)]
        public NetworkArray<int> PlayedNotes { get; }
        
        [Networked]
        public int CurrentNoteIndex { get; set; }
        
        [Networked]
        public NetworkBool IsSolved { get; set; }
        
        [Networked]
        public int AttemptCount { get; set; }
        
        [Networked]
        public float LastNoteTime { get; set; }
        
        [Networked]
        public TickTimer DoorOpenTimer { get; set; }
        
        [Networked]
        public TickTimer ResetTimer { get; set; }
        
        [Header("Events")]
        public UnityEvent<int> OnNotePressed = new UnityEvent<int>();
        public UnityEvent<int> OnCorrectNote = new UnityEvent<int>();
        public UnityEvent<int> OnWrongNote = new UnityEvent<int>();
        public UnityEvent OnSequenceComplete = new UnityEvent();
        public UnityEvent OnSequenceFailed = new UnityEvent();
        public UnityEvent OnPianoReset = new UnityEvent();
        
        private AudioSource _audioSource;
        private List<int> _currentSequence = new List<int>();
        private bool _isProcessing = false;
        private float _sequenceStartTime = 0f;
        
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            SetupPianoKeys();
        }
        
        private void SetupPianoKeys()
        {
            if (_pianoKeys == null || _pianoKeys.Length == 0)
            {
                _pianoKeys = GetComponentsInChildren<NetworkedPianoKey>();
            }
            
            for (int i = 0; i < _pianoKeys.Length; i++)
            {
                if (_pianoKeys[i] != null)
                {
                    _pianoKeys[i].SetKeyData(i, this);
                    
                    int keyIndex = i;
                    _pianoKeys[i].OnKeyPressed.AddListener(() => OnKeyPressed(keyIndex));
                }
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                CurrentNoteIndex = 0;
                IsSolved = false;
                AttemptCount = 0;
                LastNoteTime = 0f;
                
                for (int i = 0; i < PlayedNotes.Length; i++)
                {
                    PlayedNotes.Set(i, -1);
                }
                
                RegisterWithPuzzleManager();
            }
            
            UpdateProgressIndicators();
        }
        
        private void RegisterWithPuzzleManager()
        {
            if (NetworkedPuzzleManager.Instance != null)
            {
                if (NetworkedPuzzleManager.Instance.CanStartPuzzle(_puzzleId))
                {
                    NetworkedPuzzleManager.Instance.RPC_RequestStartPuzzle(_puzzleId);
                }
            }
        }
        
        private void OnKeyPressed(int keyIndex)
        {
            if (!HasStateAuthority || _isProcessing || IsSolved)
                return;
                
            ProcessKeyPress(keyIndex);
        }
        
        public void RegisterKeyPress(int keyIndex, PlayerRef player)
        {
            if (!HasStateAuthority || IsSolved)
                return;
                
            ProcessKeyPress(keyIndex);
        }
        
        private void ProcessKeyPress(int keyIndex)
        {
            if (CurrentNoteIndex == 0)
            {
                _sequenceStartTime = Runner.SimulationTime;
            }
            
            PlayedNotes.Set(CurrentNoteIndex, keyIndex);
            LastNoteTime = Runner.SimulationTime;
            
            bool isCorrect = CheckNote(keyIndex, CurrentNoteIndex);
            
            if (isCorrect)
            {
                CurrentNoteIndex++;
                RPC_NotifyCorrectNote(keyIndex, CurrentNoteIndex - 1);
                
                if (NetworkedPuzzleManager.Instance != null)
                {
                    NetworkedPuzzleManager.Instance.RPC_UpdatePuzzleProgress(_puzzleId, CurrentNoteIndex);
                }
                
                if (CurrentNoteIndex >= _correctSequence.Length)
                {
                    CompletePuzzle();
                }
            }
            else
            {
                RPC_NotifyWrongNote(keyIndex);
                FailSequence();
            }
        }
        
        private bool CheckNote(int keyIndex, int sequenceIndex)
        {
            if (sequenceIndex < 0 || sequenceIndex >= _correctSequence.Length)
                return false;
                
            return _correctSequence[sequenceIndex] == keyIndex;
        }
        
        private void CompletePuzzle()
        {
            IsSolved = true;
            _isProcessing = true;
            
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RPC_CompletePuzzle(_puzzleId);
            }
            
            if (_puzzleDoor != null)
            {
                _puzzleDoor.RPC_UnlockDoor();
                DoorOpenTimer = TickTimer.CreateFromSeconds(Runner, 1f);
            }
            
            RPC_NotifySequenceComplete();
        }
        
        private void FailSequence()
        {
            AttemptCount++;
            _isProcessing = true;
            
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RPC_UpdatePuzzleProgress(_puzzleId, 0);
            }
            
            RPC_NotifySequenceFailed();
            
            ResetTimer = TickTimer.CreateFromSeconds(Runner, _sequenceResetDelay);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyCorrectNote(int keyIndex, int sequenceIndex)
        {
            OnCorrectNote?.Invoke(keyIndex);
            
            if (_pianoKeys[keyIndex] != null)
            {
                _pianoKeys[keyIndex].PlaySuccessFeedback();
            }
            
            UpdateProgressIndicators();
            
            Debug.Log($"[NetworkedPiano] Correct note! Key {keyIndex} at position {sequenceIndex}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyWrongNote(int keyIndex)
        {
            OnWrongNote?.Invoke(keyIndex);
            
            if (_pianoKeys[keyIndex] != null)
            {
                _pianoKeys[keyIndex].PlayFailureFeedback();
            }
            
            PlaySound(_failureSound);
            
            if (_failureIndicator != null)
            {
                _failureIndicator.SetActive(true);
                StartCoroutine(HideIndicatorAfterDelay(_failureIndicator, 2f));
            }
            
            Debug.Log($"[NetworkedPiano] Wrong note! Key {keyIndex}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifySequenceComplete()
        {
            OnSequenceComplete?.Invoke();
            
            if (_playbackCorrectSequence)
            {
                PlaybackSequence();
            }
            
            PlaySound(_successMelody);
            
            if (_successIndicator != null)
                _successIndicator.SetActive(true);
                
            foreach (var key in _pianoKeys)
            {
                if (key != null)
                    key.PlaySuccessFeedback();
            }
            
            Debug.Log("[NetworkedPiano] Sequence completed successfully!");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifySequenceFailed()
        {
            OnSequenceFailed?.Invoke();
            
            foreach (var key in _pianoKeys)
            {
                if (key != null)
                    key.PlayFailureFeedback();
            }
            
            Debug.Log("[NetworkedPiano] Sequence failed! Resetting...");
        }
        
        private void ResetPiano()
        {
            if (!HasStateAuthority)
                return;
                
            CurrentNoteIndex = 0;
            _isProcessing = false;
            _currentSequence.Clear();
            
            for (int i = 0; i < PlayedNotes.Length; i++)
            {
                PlayedNotes.Set(i, -1);
            }
            
            foreach (var key in _pianoKeys)
            {
                if (key != null)
                    key.ResetKey();
            }
            
            RPC_NotifyPianoReset();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPianoReset()
        {
            OnPianoReset?.Invoke();
            
            if (_failureIndicator != null)
                _failureIndicator.SetActive(false);
                
            UpdateProgressIndicators();
            
            Debug.Log("[NetworkedPiano] Piano reset");
        }
        
        private void PlaybackSequence()
        {
            StartCoroutine(PlaybackSequenceCoroutine());
        }
        
        private System.Collections.IEnumerator PlaybackSequenceCoroutine()
        {
            yield return new WaitForSeconds(0.5f);
            
            foreach (int keyIndex in _correctSequence)
            {
                if (keyIndex >= 0 && keyIndex < _pianoKeys.Length && _pianoKeys[keyIndex] != null)
                {
                    _pianoKeys[keyIndex].PlayNote();
                    _pianoKeys[keyIndex].AnimateKeyPress();
                    yield return new WaitForSeconds(0.4f);
                }
            }
        }
        
        private void UpdateProgressIndicators()
        {
            if (_progressIndicators != null)
            {
                for (int i = 0; i < _progressIndicators.Length; i++)
                {
                    if (_progressIndicators[i] != null)
                    {
                        _progressIndicators[i].SetActive(i < CurrentNoteIndex);
                    }
                }
            }
        }
        
        private System.Collections.IEnumerator HideIndicatorAfterDelay(GameObject indicator, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (indicator != null)
                indicator.SetActive(false);
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
        
        public void SetCorrectSequence(int[] sequence)
        {
            _correctSequence = sequence;
        }
        
        public int[] GetCorrectSequence()
        {
            return _correctSequence;
        }
        
        public void ShowHint()
        {
            if (_playbackCorrectSequence && !IsSolved)
            {
                PlaybackSequence();
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                // Check door open timer
                if (DoorOpenTimer.ExpiredOrNotRunning(Runner) == false && DoorOpenTimer.Expired(Runner) && _puzzleDoor != null)
                {
                    _puzzleDoor.RPC_RequestOpen();
                    DoorOpenTimer = TickTimer.None;
                }
                
                // Check reset timer
                if (ResetTimer.ExpiredOrNotRunning(Runner) == false && ResetTimer.Expired(Runner))
                {
                    ResetPiano();
                    ResetTimer = TickTimer.None;
                }
                
                // Auto-reset if no input for too long
                if (!IsSolved && CurrentNoteIndex > 0 && Runner.SimulationTime - LastNoteTime > 10f)
                {
                    ResetPiano();
                }
            }
        }
        
        private void OnDestroy()
        {
            foreach (var key in _pianoKeys)
            {
                if (key != null)
                {
                    key.OnKeyPressed.RemoveAllListeners();
                }
            }
        }
    }
}