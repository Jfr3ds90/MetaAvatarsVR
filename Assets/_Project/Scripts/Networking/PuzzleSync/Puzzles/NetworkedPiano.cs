using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedPiano : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private NetworkedPianoKey[] _pianoKeys;
        [SerializeField] private bool _limitAttempts = false;
        [SerializeField] private int _maxAttempts = 3;
        [SerializeField] private float _resetDelay = 1.5f;
        [SerializeField] private bool _allowMultiplePlayers = true;
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject[] _progressIndicators;
        [SerializeField] private Material _successMaterial;
        [SerializeField] private Material _errorMaterial;
        
        [Header("Audio")]
        [SerializeField] private AudioClip _correctSound;
        [SerializeField] private AudioClip _wrongSound;
        [SerializeField] private AudioClip _successMelody;
        
        [Header("Network State")]
        [Networked] public NetworkBool IsActive { get; set; }
        [Networked] public int CurrentAttempt { get; set; }
        [Networked] public int SequenceProgress { get; set; }
        [Networked, Capacity(20)]
        public NetworkString<_32> ExpectedSequence { get; set; }
        [Networked, Capacity(20)]
        public NetworkString<_32> CurrentSequence { get; set; }
        [Networked] public PlayerRef LastPlayerInput { get; set; }
        
        [Header("Events")]
        public UnityEvent OnSequenceCompleted = new UnityEvent();
        public UnityEvent OnSequenceFailed = new UnityEvent();
        public UnityEvent<int> OnProgressUpdate = new UnityEvent<int>();
        
        private AudioSource _audioSource;
        private List<string> _currentNotes = new List<string>();
        private string[] _noteNames = { "Do", "Re", "Mi", "Fa", "Sol", "La", "Si" };
        private Coroutine _resetCoroutine;
        
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            SetupKeys();
            InitializeProgressIndicators();
        }
        
        private void SetupKeys()
        {
            if (_pianoKeys == null || _pianoKeys.Length == 0)
                _pianoKeys = GetComponentsInChildren<NetworkedPianoKey>();
                
            for (int i = 0; i < _pianoKeys.Length; i++)
            {
                if (_pianoKeys[i] != null)
                {
                    int keyIndex = i;
                    string noteName = i < _noteNames.Length ? _noteNames[i] : $"Key{i}";
                    
                    _pianoKeys[i].SetKeyData(keyIndex, noteName);
                    _pianoKeys[i].OnKeyPressed.RemoveAllListeners();
                    _pianoKeys[i].OnKeyPressed.AddListener((note) => OnKeyPressed(note, keyIndex));
                }
            }
        }
        
        public override void Spawned()
        {
            // En Shared Mode, el Master Client maneja el estado principal
            if (Runner.IsSharedModeMasterClient)
            {
                IsActive = false;
                CurrentAttempt = 0;
                SequenceProgress = 0;
                CurrentSequence = "";
                // Establecer secuencia esperada por defecto
                ExpectedSequence = "Do,Re,Mi,Fa,Sol"; // Cambiar esto según tu puzzle
                Debug.Log($"[NetworkedPiano] Initialized with expected sequence: {ExpectedSequence}");
            }
        }
        
        public void SetExpectedSequence(string sequence)
        {
            // En Shared Mode, tanto el MasterClient como los demás clientes necesitan poder establecer la secuencia localmente
            // cuando se activa el piano a través del RPC
            if (Runner != null && (Runner.IsSharedModeMasterClient || !string.IsNullOrEmpty(sequence)))
            {
                ExpectedSequence = sequence;
                Debug.Log($"[NetworkedPiano] Expected sequence set: {sequence} (IsMaster: {Runner.IsSharedModeMasterClient})");
            }
        }
        
        // Método helper para configurar la secuencia desde el Inspector o scripts externos
        public void SetExpectedNotes(string[] notes)
        {
            if (Runner && Runner.IsSharedModeMasterClient)
            {
                ExpectedSequence = string.Join(",", notes);
                Debug.Log($"[NetworkedPiano] Expected sequence set from array: {ExpectedSequence}");
            }
        }
        
        public void ActivatePiano()
        {
            if (Runner.IsSharedModeMasterClient)
            {
                IsActive = true;
                CurrentAttempt = 0;
                SequenceProgress = 0;
                CurrentSequence = "";
                _currentNotes.Clear();
                
                // Asegurarse de que la secuencia esperada esté establecida
                if (string.IsNullOrEmpty(ExpectedSequence.ToString()))
                {
                    ExpectedSequence = "Do,Re,Mi,Fa,Sol"; // Secuencia por defecto
                    Debug.LogWarning($"[NetworkedPiano] No expected sequence set! Using default: {ExpectedSequence}");
                }
                else
                {
                    Debug.Log($"[NetworkedPiano] Piano activated with sequence: {ExpectedSequence}");
                }
                
                // Informar sobre el modo de intentos
                if (_limitAttempts)
                {
                    Debug.Log($"[NetworkedPiano] Limited attempts mode: {_maxAttempts} attempts allowed");
                }
                else
                {
                    Debug.Log($"[NetworkedPiano] Unlimited attempts mode enabled");
                }
                
                RPC_UpdatePianoState(true, 0, 0);
            }
        }
        
        private void OnKeyPressed(string noteName, int keyIndex)
        {
            if (!IsActive) return;
            
            // En Shared Mode, enviar el input solo al Master Client para procesamiento
            if (Runner.IsSharedModeMasterClient)
            {
                // El Master Client procesa directamente
                ProcessKeyInput(noteName, keyIndex, Runner.LocalPlayer);
            }
            else
            {
                // Los demás clientes envían al Master Client
                RPC_RequestProcessKeyPress(noteName, keyIndex);
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_RequestProcessKeyPress(string noteName, int keyIndex, RpcInfo info = default)
        {
            // Solo el Master Client procesa las solicitudes
            if (!Runner.IsSharedModeMasterClient) return;
            if (!IsActive) return;
            
            ProcessKeyInput(noteName, keyIndex, info.Source);
        }
        
        private void ProcessKeyInput(string noteName, int keyIndex, PlayerRef player)
        {
            if (!Runner.IsSharedModeMasterClient) return;
            if (!IsActive) return;
            
            // Validar si es input duplicado
            if (!_allowMultiplePlayers && LastPlayerInput != player && _currentNotes.Count > 0)
            {
                Debug.Log($"[NetworkedPiano] Different player attempted input. Last: {LastPlayerInput}, Current: {player}");
                return;
            }
            
            LastPlayerInput = player;
            _currentNotes.Add(noteName);
            
            // Actualizar CurrentSequence como propiedad Networked (se sincroniza automáticamente)
            string newSequence = string.Join(",", _currentNotes);
            CurrentSequence = newSequence;
            SequenceProgress = _currentNotes.Count;
            
            Debug.Log($"[NetworkedPiano] Player {player} pressed: {noteName}, Current sequence: {CurrentSequence}, Expected: {ExpectedSequence}");
            
            // Notificar a todos los clientes para actualizar sus indicadores visuales
            RPC_UpdateProgressIndicators(SequenceProgress);
            
            // Parsear la secuencia esperada (formato: "Do,Re,Mi,Fa,Sol")
            string[] expectedNotes = ExpectedSequence.ToString().Split(',');
            int currentIndex = _currentNotes.Count - 1;
            
            // Validar nota por nota
            if (currentIndex < expectedNotes.Length && 
                noteName == expectedNotes[currentIndex])
            {
                // Nota correcta
                RPC_NotifyCorrectNote(keyIndex, SequenceProgress);
                Debug.Log($"[NetworkedPiano] Correct note! Progress: {SequenceProgress}/{expectedNotes.Length}");
                
                // Verificar si completó la secuencia
                if (_currentNotes.Count >= expectedNotes.Length)
                {
                    IsActive = false;
                    RPC_SequenceComplete();
                }
            }
            else
            {
                // Nota incorrecta
                CurrentAttempt++;
                
                if (_limitAttempts)
                {
                    Debug.Log($"[NetworkedPiano] Wrong note! Expected: {(currentIndex < expectedNotes.Length ? expectedNotes[currentIndex] : "N/A")}, Got: {noteName}, Attempt: {CurrentAttempt}/{_maxAttempts}");
                }
                else
                {
                    Debug.Log($"[NetworkedPiano] Wrong note! Expected: {(currentIndex < expectedNotes.Length ? expectedNotes[currentIndex] : "N/A")}, Got: {noteName}. Unlimited attempts mode.");
                }
                
                RPC_NotifyWrongNote(keyIndex);
                
                // Solo verificar el límite de intentos si está habilitado
                if (_limitAttempts && CurrentAttempt >= _maxAttempts)
                {
                    RPC_SequenceFailed();
                }
                else
                {
                    // Resetear después de un delay
                    if (_resetCoroutine != null)
                        StopCoroutine(_resetCoroutine);
                    _resetCoroutine = StartCoroutine(DelayedReset());
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_UpdateProgressIndicators(int progress)
        {
            // Todos los clientes actualizan sus indicadores visuales
            UpdateProgressIndicators(progress);
            Debug.Log($"[NetworkedPiano] Progress indicators updated: {progress}");
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_UpdatePianoState(NetworkBool active, int progress, int attempts)
        {
            IsActive = active;
            SequenceProgress = progress;
            CurrentAttempt = attempts;
            UpdateProgressIndicators(progress);
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_NotifyCorrectNote(int keyIndex, int progress)
        {
            if (keyIndex < _pianoKeys.Length)
                _pianoKeys[keyIndex].ShowCorrectFeedback();
                
            UpdateProgressIndicators(progress);
            OnProgressUpdate?.Invoke(progress);
            
            if (_correctSound != null)
                PlaySound(_correctSound);
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_NotifyWrongNote(int keyIndex)
        {
            if (keyIndex < _pianoKeys.Length)
                _pianoKeys[keyIndex].ShowErrorFeedback();
                
            PlaySound(_wrongSound);
            ShowErrorFeedback();
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_SequenceComplete()
        {
            OnSequenceCompleted?.Invoke();
            PlaySound(_successMelody ?? _correctSound);
            ShowSuccessFeedback();
            Debug.Log("[NetworkedPiano] Sequence completed successfully!");
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_SequenceFailed()
        {
            OnSequenceFailed?.Invoke();
            PlaySound(_wrongSound);
            ResetSequence();
            Debug.Log($"[NetworkedPiano] Sequence failed after {_maxAttempts} attempts");
            
            // Si los intentos son limitados, desactivar el piano
            if (_limitAttempts)
            {
                IsActive = false;
                Debug.Log("[NetworkedPiano] Piano deactivated - Max attempts reached");
            }
        }
        
        private IEnumerator DelayedReset()
        {
            yield return new WaitForSeconds(_resetDelay);
            ResetSequence();
            RPC_UpdatePianoState(true, 0, CurrentAttempt);
        }
        
        public void ResetSequence()
        {
            _currentNotes.Clear();
            
            // Solo el MasterClient puede modificar propiedades Networked
            if (Runner.IsSharedModeMasterClient)
            {
                CurrentSequence = "";
                SequenceProgress = 0;
                RPC_UpdateProgressIndicators(0);
            }
            
            Debug.Log($"[NetworkedPiano] Sequence reset. Waiting for new attempt...");
        }
        
        // Método público para configurar el modo de intentos
        public void SetAttemptsMode(bool limitAttempts, int maxAttempts = 3)
        {
            _limitAttempts = limitAttempts;
            if (limitAttempts && maxAttempts > 0)
            {
                _maxAttempts = maxAttempts;
            }
            
            Debug.Log($"[NetworkedPiano] Attempts mode changed - Limited: {_limitAttempts}, Max: {_maxAttempts}");
        }
        
        private void InitializeProgressIndicators()
        {
            if (_progressIndicators != null)
            {
                foreach (var indicator in _progressIndicators)
                {
                    if (indicator != null)
                        indicator.SetActive(false);
                }
            }
        }
        
        private void UpdateProgressIndicators(int progress)
        {
            if (_progressIndicators != null)
            {
                for (int i = 0; i < _progressIndicators.Length; i++)
                {
                    if (_progressIndicators[i] != null)
                        _progressIndicators[i].SetActive(i < progress);
                }
            }
        }
        
        private void ShowSuccessFeedback()
        {
            // Implementar feedback visual de éxito
            StartCoroutine(FlashMaterial(_successMaterial, 2f));
        }
        
        private void ShowErrorFeedback()
        {
            // Implementar feedback visual de error
            StartCoroutine(FlashMaterial(_errorMaterial, 0.5f));
        }
        
        private IEnumerator FlashMaterial(Material material, float duration)
        {
            // Implementación del flash visual
            yield return new WaitForSeconds(duration);
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
    }
}