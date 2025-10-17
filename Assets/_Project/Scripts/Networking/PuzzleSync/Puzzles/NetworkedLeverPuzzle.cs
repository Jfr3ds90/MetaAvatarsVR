using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;


namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedLeverPuzzle : NetworkBehaviour
    {
        [Header("Puzzle Configuration")]
        [SerializeField] private NetworkedLever[] _levers;
        [SerializeField] private string _correctSequence = "DATOS";
        [SerializeField] private NetworkedDoor _puzzleDoor;
        [SerializeField] private int _puzzleId = 0;
        
        [Header("Letter Mapping")]
        [SerializeField] private bool _useCustomMapping = true;
        [SerializeField] private string[] _leverLetters = { "D", "A", "T", "O", "S" };
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject _successIndicator;
        [SerializeField] private GameObject _failureIndicator;
        [SerializeField] private AudioClip _successSound;
        [SerializeField] private AudioClip _failureSound;
        [SerializeField] private AudioClip _progressSound;
        
        [Header("Network State")]
        [Networked, Capacity(10)]
        public NetworkString<_16> CurrentSequence { get; set; }
        
        [Networked]
        public int CorrectCount { get; set; }
        
        [Networked]
        public NetworkBool IsSolved { get; set; }
        
        [Networked]
        public int AttemptCount { get; set; }
        
        [Networked]
        public TickTimer ResetTimer { get; set; }
        
        [Header("Events")]
        public UnityEvent<string> OnSequenceUpdated = new UnityEvent<string>();
        public UnityEvent<int> OnCorrectLever = new UnityEvent<int>();
        public UnityEvent OnPuzzleSolved = new UnityEvent();
        public UnityEvent OnPuzzleFailed = new UnityEvent();
        public UnityEvent OnPuzzleReset = new UnityEvent();
        
        private AudioSource _audioSource;
        private List<int> _activatedLevers = new List<int>();
        private Dictionary<int, string> _leverIndexToLetter = new Dictionary<int, string>();
        
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            SetupLevers();
        }
        
        private void SetupLevers()
        {
            if (_levers == null || _levers.Length == 0)
            {
                _levers = GetComponentsInChildren<NetworkedLever>();
                AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Found {_levers.Length} levers", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
    
            for (int i = 0; i < _levers.Length; i++)
            {
                if (_levers[i] != null)
                {
                    string letter = _useCustomMapping && i < _leverLetters.Length 
                        ? _leverLetters[i] 
                        : ((char)('A' + i)).ToString();
                    
                    _leverIndexToLetter[i] = letter;
                    
                    AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Lever {i} mapped to letter '{letter}'", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                }
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                CurrentSequence = "";
                CorrectCount = 0;
                IsSolved = false;
                AttemptCount = 0;
                
                RegisterWithPuzzleManager();
            }
        }
        
        private void RegisterWithPuzzleManager()
        {
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RPC_RequestStartPuzzle(_puzzleId);
            }
        }
        
        
        public void RegisterLever(NetworkedLever lever, int index)
        {
            if (index >= 0 && index < _levers.Length)
            {
                _levers[index] = lever;
                AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Lever {index} registered", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
        }

        
        public void OnLeverStateChanged(int leverIndex, bool activated)
        {
            if (!HasStateAuthority)
            {
                AdvancedDebugSystem.LogWarning($"[NetworkedLeverPuzzle] OnLeverStateChanged called without authority", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            if (IsSolved)
            {
                AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Puzzle already solved, ignoring lever change", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                return;
            }
            
            AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Lever {leverIndex} state changed to {activated}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            if (activated)
            {
                ProcessLeverActivation(leverIndex);
            }
            else
            {
                ProcessLeverDeactivation(leverIndex);
            }
        }
        
        
        private void ProcessLeverActivation(int leverIndex)
        {
            if (!_leverIndexToLetter.ContainsKey(leverIndex))
            {
                AdvancedDebugSystem.LogError($"[NetworkedLeverPuzzle] No letter mapping for lever index {leverIndex}!", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            if (_activatedLevers.Contains(leverIndex))
            {
                AdvancedDebugSystem.LogWarning($"[NetworkedLeverPuzzle] Lever {leverIndex} already in activated list", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            _activatedLevers.Add(leverIndex);
            string letter = _leverIndexToLetter[leverIndex];
            
            AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Added lever {leverIndex} (letter '{letter}') to sequence", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            UpdateCurrentSequence();
            
            ValidateCurrentSequence();
        }
        
      
        private void ProcessLeverDeactivation(int leverIndex)
        {
            if (!_activatedLevers.Contains(leverIndex))
            {
                AdvancedDebugSystem.LogWarning($"[NetworkedLeverPuzzle] Lever {leverIndex} not in activated list", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            _activatedLevers.Remove(leverIndex);
            
            string letter = _leverIndexToLetter.ContainsKey(leverIndex) ? _leverIndexToLetter[leverIndex] : "?";
            AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Removed lever {leverIndex} (letter '{letter}') from sequence", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            UpdateCurrentSequence();
            
            RecalculateCorrectCount();
        }
        
        
        private void UpdateCurrentSequence()
        {
            string sequence = "";
            
            foreach (int index in _activatedLevers)
            {
                if (_leverIndexToLetter.ContainsKey(index))
                {
                    sequence += _leverIndexToLetter[index];
                }
            }
            
            CurrentSequence = sequence;
            
            AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Current sequence updated: '{sequence}'", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            RPC_NotifySequenceUpdated(sequence);
        }
        
     
        private void ValidateCurrentSequence()
        {
            string currentSeq = CurrentSequence.ToString();
            
            if (string.IsNullOrEmpty(currentSeq))
            {
                CorrectCount = 0;
                return;
            }
            
            AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Validating sequence: '{currentSeq}' vs correct: '{_correctSequence}'", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            bool isValid = true;
            int correctCount = 0;
            
            for (int i = 0; i < currentSeq.Length && i < _correctSequence.Length; i++)
            {
                if (currentSeq[i] == _correctSequence[i])
                {
                    correctCount++;
                }
                else
                {
                    isValid = false;
                    break;
                }
            }
            
            CorrectCount = correctCount;
            
            if (!isValid)
            {
                AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Incorrect sequence at position {correctCount}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                FailPuzzle();
            }
            else if (currentSeq.Length == _correctSequence.Length && correctCount == _correctSequence.Length)
            {
                AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] PUZZLE SOLVED! Sequence complete and correct", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                SolvePuzzle();
            }
            else
            {
                AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Partial sequence correct: {correctCount}/{_correctSequence.Length}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                
                if (correctCount > 0)
                {
                    RPC_NotifyCorrectLever(_activatedLevers[correctCount - 1]);
                }
            }
        }
        
       
        private void RecalculateCorrectCount()
        {
            int correct = 0;
            string currentSeq = CurrentSequence.ToString();
            
            for (int i = 0; i < currentSeq.Length && i < _correctSequence.Length; i++)
            {
                if (currentSeq[i] == _correctSequence[i])
                {
                    correct++;
                }
                else
                {
                    break;
                }
            }
            
            CorrectCount = correct;
            AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Recalculated correct count: {correct}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        private void SolvePuzzle()
        {
            if (IsSolved)
                return;
                
            IsSolved = true;
            
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RPC_CompletePuzzle(_puzzleId);
            }
            
            if (_puzzleDoor != null)
            {
                _puzzleDoor.RPC_UnlockDoor();
                _puzzleDoor.RPC_RequestOpen();
            }
            
            RPC_NotifyPuzzleSolved();
        }
        
        private void FailPuzzle()
        {
            AttemptCount++;
            
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RPC_UpdatePuzzleProgress(_puzzleId, 0);
            }
            
            RPC_NotifyPuzzleFailed();
            
            ResetTimer = TickTimer.CreateFromSeconds(Runner, 2f);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifySequenceUpdated(string sequence)
        {
            OnSequenceUpdated?.Invoke(sequence);
            PlaySound(_progressSound);
            
            AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Broadcasting sequence update: '{sequence}'", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyCorrectLever(int leverIndex)
        {
            OnCorrectLever?.Invoke(leverIndex);
            
            if (leverIndex >= 0 && leverIndex < _levers.Length && _levers[leverIndex] != null)
            {
                ShowFeedback(_levers[leverIndex].transform.position, true);
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPuzzleSolved()
        {
            OnPuzzleSolved?.Invoke();
            PlaySound(_successSound);
            
            if (_successIndicator != null)
                _successIndicator.SetActive(true);
                
            AdvancedDebugSystem.Log("[NetworkedLeverPuzzle] PUZZLE SOLVED! Broadcasting to all clients", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPuzzleFailed()
        {
            OnPuzzleFailed?.Invoke();
            PlaySound(_failureSound);
            
            if (_failureIndicator != null)
            {
                _failureIndicator.SetActive(true);
                StartCoroutine(HideIndicatorAfterDelay(_failureIndicator, 2f));
            }
            
            AdvancedDebugSystem.Log("[NetworkedLeverPuzzle] Puzzle failed! Resetting in 2 seconds...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        private void ResetPuzzle()
        {
            if (!HasStateAuthority)
                return;
                
            AdvancedDebugSystem.Log("[NetworkedLeverPuzzle] Resetting puzzle...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            _activatedLevers.Clear();
            CurrentSequence = "";
            CorrectCount = 0;
            
            foreach (var lever in _levers)
            {
                if (lever != null)
                {
                    lever.ResetLever();
                }
            }
            
            RPC_NotifyPuzzleReset();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPuzzleReset()
        {
            OnPuzzleReset?.Invoke();
            
            if (_failureIndicator != null)
                _failureIndicator.SetActive(false);
                
            AdvancedDebugSystem.Log("[NetworkedLeverPuzzle] Puzzle reset complete", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        private void ShowFeedback(Vector3 position, bool success)
        {
            // Could instantiate particle effects or UI feedback here
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
        
        public void SetCorrectSequence(string sequence)
        {
            _correctSequence = sequence;
            AdvancedDebugSystem.Log($"[NetworkedLeverPuzzle] Correct sequence set to: '{sequence}'", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        public string GetCurrentSequence()
        {
            return CurrentSequence.ToString();
        }
        
        public bool CheckSequence()
        {
            return CurrentSequence.ToString() == _correctSequence;
        }
        
        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (ResetTimer.ExpiredOrNotRunning(Runner) == false && ResetTimer.Expired(Runner))
                {
                    ResetPuzzle();
                    ResetTimer = TickTimer.None;
                }
            }
        }
        
        private void OnDestroy()
        {
            _activatedLevers.Clear();
            _leverIndexToLetter.Clear();
        }
        
        public int RegisterLeverActivation(int leverIndex)
        {
            AdvancedDebugSystem.LogWarning($"[NetworkedLeverPuzzle] RegisterLeverActivation is deprecated. Use OnLeverStateChanged instead", LogCategory.Networking | LogCategory.Photon);
            OnLeverStateChanged(leverIndex, true);
            return _activatedLevers.IndexOf(leverIndex);
        }
        
        public void RegisterLeverDeactivation(int leverIndex)
        {
            AdvancedDebugSystem.LogWarning($"[NetworkedLeverPuzzle] RegisterLeverDeactivation is deprecated. Use OnLeverStateChanged instead", LogCategory.Networking | LogCategory.Photon);
            OnLeverStateChanged(leverIndex, false);
        }
    }
}