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
                Debug.Log($"[NetworkedLeverPuzzle] Found {_levers.Length} levers");
            }
    
            for (int i = 0; i < _levers.Length; i++)
            {
                if (_levers[i] != null)
                {
                    string letter = _useCustomMapping && i < _leverLetters.Length 
                        ? _leverLetters[i] 
                        : ((char)('A' + i)).ToString();
                    
                    // Mapear índice a letra para validación
                    _leverIndexToLetter[i] = letter;
                    
                    Debug.Log($"[NetworkedLeverPuzzle] Lever {i} mapped to letter '{letter}'");
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
        
        /// <summary>
        /// Registra una palanca específica en el sistema (llamado desde NetworkedLever.Spawned)
        /// </summary>
        public void RegisterLever(NetworkedLever lever, int index)
        {
            if (index >= 0 && index < _levers.Length)
            {
                _levers[index] = lever;
                Debug.Log($"[NetworkedLeverPuzzle] Lever {index} registered");
            }
        }

        /// <summary>
        /// MÉTODO PRINCIPAL: Recibe cambios de estado de las palancas
        /// Este método es llamado desde NetworkedLever cuando cambia su estado
        /// </summary>
        public void OnLeverStateChanged(int leverIndex, bool activated)
        {
            // Solo el host procesa cambios de estado
            if (!HasStateAuthority)
            {
                Debug.LogWarning($"[NetworkedLeverPuzzle] OnLeverStateChanged called without authority");
                return;
            }
            
            // No procesar si el puzzle ya está resuelto
            if (IsSolved)
            {
                Debug.Log($"[NetworkedLeverPuzzle] Puzzle already solved, ignoring lever change");
                return;
            }
            
            Debug.Log($"[NetworkedLeverPuzzle] Lever {leverIndex} state changed to {activated}");
            
            // Procesar activación o desactivación
            if (activated)
            {
                // La palanca fue activada - agregar a la secuencia
                ProcessLeverActivation(leverIndex);
            }
            else
            {
                // La palanca fue desactivada - remover de la secuencia
                ProcessLeverDeactivation(leverIndex);
            }
        }
        
        /// <summary>
        /// Procesa la activación de una palanca
        /// </summary>
        private void ProcessLeverActivation(int leverIndex)
        {
            // Verificar que tengamos el mapeo de letra para este índice
            if (!_leverIndexToLetter.ContainsKey(leverIndex))
            {
                Debug.LogError($"[NetworkedLeverPuzzle] No letter mapping for lever index {leverIndex}!");
                return;
            }
            
            // Si la palanca ya está en la lista, no hacer nada
            if (_activatedLevers.Contains(leverIndex))
            {
                Debug.LogWarning($"[NetworkedLeverPuzzle] Lever {leverIndex} already in activated list");
                return;
            }
            
            // Agregar la palanca a la lista de activadas
            _activatedLevers.Add(leverIndex);
            string letter = _leverIndexToLetter[leverIndex];
            
            Debug.Log($"[NetworkedLeverPuzzle] Added lever {leverIndex} (letter '{letter}') to sequence");
            
            // Actualizar la secuencia actual
            UpdateCurrentSequence();
            
            // Validar la secuencia
            ValidateCurrentSequence();
        }
        
        /// <summary>
        /// Procesa la desactivación de una palanca
        /// </summary>
        private void ProcessLeverDeactivation(int leverIndex)
        {
            // Si la palanca no está en la lista, no hacer nada
            if (!_activatedLevers.Contains(leverIndex))
            {
                Debug.LogWarning($"[NetworkedLeverPuzzle] Lever {leverIndex} not in activated list");
                return;
            }
            
            // Remover la palanca de la lista
            _activatedLevers.Remove(leverIndex);
            
            string letter = _leverIndexToLetter.ContainsKey(leverIndex) ? _leverIndexToLetter[leverIndex] : "?";
            Debug.Log($"[NetworkedLeverPuzzle] Removed lever {leverIndex} (letter '{letter}') from sequence");
            
            // Actualizar la secuencia actual
            UpdateCurrentSequence();
            
            // Recalcular el conteo correcto
            RecalculateCorrectCount();
        }
        
        /// <summary>
        /// Actualiza la secuencia actual basándose en las palancas activadas
        /// </summary>
        private void UpdateCurrentSequence()
        {
            string sequence = "";
            
            // Construir la secuencia con las letras de las palancas activadas en orden
            foreach (int index in _activatedLevers)
            {
                if (_leverIndexToLetter.ContainsKey(index))
                {
                    sequence += _leverIndexToLetter[index];
                }
            }
            
            CurrentSequence = sequence;
            
            Debug.Log($"[NetworkedLeverPuzzle] Current sequence updated: '{sequence}'");
            
            // Notificar a todos los clientes
            RPC_NotifySequenceUpdated(sequence);
        }
        
        /// <summary>
        /// Valida si la secuencia actual es correcta
        /// </summary>
        private void ValidateCurrentSequence()
        {
            string currentSeq = CurrentSequence.ToString();
            
            // Si no hay secuencia, no hay nada que validar
            if (string.IsNullOrEmpty(currentSeq))
            {
                CorrectCount = 0;
                return;
            }
            
            Debug.Log($"[NetworkedLeverPuzzle] Validating sequence: '{currentSeq}' vs correct: '{_correctSequence}'");
            
            // Verificar si la secuencia actual coincide parcialmente con la correcta
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
                    // La secuencia es incorrecta desde este punto
                    isValid = false;
                    break;
                }
            }
            
            CorrectCount = correctCount;
            
            if (!isValid)
            {
                // Secuencia incorrecta - fallo del puzzle
                Debug.Log($"[NetworkedLeverPuzzle] Incorrect sequence at position {correctCount}");
                FailPuzzle();
            }
            else if (currentSeq.Length == _correctSequence.Length && correctCount == _correctSequence.Length)
            {
                // Secuencia completa y correcta - puzzle resuelto
                Debug.Log($"[NetworkedLeverPuzzle] PUZZLE SOLVED! Sequence complete and correct");
                SolvePuzzle();
            }
            else
            {
                // Secuencia parcialmente correcta
                Debug.Log($"[NetworkedLeverPuzzle] Partial sequence correct: {correctCount}/{_correctSequence.Length}");
                
                if (correctCount > 0)
                {
                    // Notificar progreso
                    RPC_NotifyCorrectLever(_activatedLevers[correctCount - 1]);
                }
            }
        }
        
        /// <summary>
        /// Recalcula el conteo de letras correctas después de una desactivación
        /// </summary>
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
            Debug.Log($"[NetworkedLeverPuzzle] Recalculated correct count: {correct}");
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
            
            // Establecer timer para reset automático
            ResetTimer = TickTimer.CreateFromSeconds(Runner, 2f);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifySequenceUpdated(string sequence)
        {
            OnSequenceUpdated?.Invoke(sequence);
            PlaySound(_progressSound);
            
            Debug.Log($"[NetworkedLeverPuzzle] Broadcasting sequence update: '{sequence}'");
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
                
            Debug.Log("[NetworkedLeverPuzzle] PUZZLE SOLVED! Broadcasting to all clients");
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
            
            Debug.Log("[NetworkedLeverPuzzle] Puzzle failed! Resetting in 2 seconds...");
        }
        
        private void ResetPuzzle()
        {
            if (!HasStateAuthority)
                return;
                
            Debug.Log("[NetworkedLeverPuzzle] Resetting puzzle...");
            
            _activatedLevers.Clear();
            CurrentSequence = "";
            CorrectCount = 0;
            
            // Resetear todas las palancas
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
                
            Debug.Log("[NetworkedLeverPuzzle] Puzzle reset complete");
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
            Debug.Log($"[NetworkedLeverPuzzle] Correct sequence set to: '{sequence}'");
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
            // Limpiar cualquier referencia si es necesario
            _activatedLevers.Clear();
            _leverIndexToLetter.Clear();
        }
        
        // Métodos públicos legacy para compatibilidad (si otras partes del código los usan)
        public int RegisterLeverActivation(int leverIndex)
        {
            Debug.LogWarning($"[NetworkedLeverPuzzle] RegisterLeverActivation is deprecated. Use OnLeverStateChanged instead");
            OnLeverStateChanged(leverIndex, true);
            return _activatedLevers.IndexOf(leverIndex);
        }
        
        public void RegisterLeverDeactivation(int leverIndex)
        {
            Debug.LogWarning($"[NetworkedLeverPuzzle] RegisterLeverDeactivation is deprecated. Use OnLeverStateChanged instead");
            OnLeverStateChanged(leverIndex, false);
        }
    }
}