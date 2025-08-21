using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

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
            }
            
            for (int i = 0; i < _levers.Length; i++)
            {
                if (_levers[i] != null)
                {
                    string letter = _useCustomMapping && i < _leverLetters.Length 
                        ? _leverLetters[i] 
                        : ((char)('A' + i)).ToString();
                        
                    _levers[i].SetLeverData(i, letter);
                    _levers[i].SetPuzzleController(this);
                    _leverIndexToLetter[i] = letter;
                    
                    int index = i;
                    _levers[i].OnLeverStateChanged.AddListener((idx, state) => OnLeverChanged(idx, state));
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
        
        private void OnLeverChanged(int leverIndex, bool activated)
        {
            if (!HasStateAuthority)
                return;
                
            if (IsSolved)
                return;
                
            if (activated)
            {
                ProcessLeverActivation(leverIndex);
            }
            else
            {
                ProcessLeverDeactivation(leverIndex);
            }
        }
        
        public int RegisterLeverActivation(int leverIndex)
        {
            if (!HasStateAuthority)
                return -1;
                
            if (_activatedLevers.Contains(leverIndex))
                return _activatedLevers.IndexOf(leverIndex);
                
            _activatedLevers.Add(leverIndex);
            UpdateCurrentSequence();
            
            return _activatedLevers.Count - 1;
        }
        
        public void RegisterLeverDeactivation(int leverIndex)
        {
            if (!HasStateAuthority)
                return;
                
            if (_activatedLevers.Contains(leverIndex))
            {
                _activatedLevers.Remove(leverIndex);
                UpdateCurrentSequence();
            }
        }
        
        private void ProcessLeverActivation(int leverIndex)
        {
            if (!_leverIndexToLetter.ContainsKey(leverIndex))
                return;
                
            string letter = _leverIndexToLetter[leverIndex];
            int sequencePosition = _activatedLevers.IndexOf(leverIndex);
            
            if (sequencePosition >= 0 && sequencePosition < _correctSequence.Length)
            {
                if (_correctSequence[sequencePosition].ToString() == letter)
                {
                    CorrectCount++;
                    RPC_NotifyCorrectLever(leverIndex);
                    
                    if (CorrectCount >= _correctSequence.Length)
                    {
                        SolvePuzzle();
                    }
                }
                else
                {
                    FailPuzzle();
                }
            }
        }
        
        private void ProcessLeverDeactivation(int leverIndex)
        {
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
            RPC_NotifySequenceUpdated(sequence);
            
            Debug.Log($"[NetworkedLeverPuzzle] Current sequence: {sequence}");
        }
        
        private void RecalculateCorrectCount()
        {
            int correct = 0;
            for (int i = 0; i < _activatedLevers.Count && i < _correctSequence.Length; i++)
            {
                int leverIndex = _activatedLevers[i];
                if (_leverIndexToLetter.ContainsKey(leverIndex))
                {
                    string letter = _leverIndexToLetter[leverIndex];
                    if (_correctSequence[i].ToString() == letter)
                    {
                        correct++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            
            CorrectCount = correct;
        }
        
        private void SolvePuzzle()
        {
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
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyCorrectLever(int leverIndex)
        {
            OnCorrectLever?.Invoke(leverIndex);
            
            if (_levers[leverIndex] != null)
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
                
            Debug.Log("[NetworkedLeverPuzzle] Puzzle solved!");
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
            
            Debug.Log("[NetworkedLeverPuzzle] Puzzle failed! Resetting...");
        }
        
        private void ResetPuzzle()
        {
            if (!HasStateAuthority)
                return;
                
            _activatedLevers.Clear();
            CurrentSequence = "";
            CorrectCount = 0;
            
            foreach (var lever in _levers)
            {
                if (lever != null)
                {
                    lever.RPC_ResetLever();
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
                
            Debug.Log("[NetworkedLeverPuzzle] Puzzle reset");
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
                // Check reset timer
                if (ResetTimer.ExpiredOrNotRunning(Runner) == false && ResetTimer.Expired(Runner))
                {
                    ResetPuzzle();
                    ResetTimer = TickTimer.None;
                }
            }
        }
        
        private void OnDestroy()
        {
            foreach (var lever in _levers)
            {
                if (lever != null)
                {
                    lever.OnLeverStateChanged.RemoveAllListeners();
                }
            }
        }
    }
}