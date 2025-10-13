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
            }
        }
        
        public void SetExpectedSequence(string sequence)
        {
            // Solo el Master Client puede establecer la secuencia esperada
            if (Runner.IsSharedModeMasterClient)
            {
                ExpectedSequence = sequence;
                Debug.Log($"[NetworkedPiano] Expected sequence set: {sequence}");
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
                
                RPC_UpdatePianoState(true, 0, 0);
            }
        }
        
        private void OnKeyPressed(string noteName, int keyIndex)
        {
            if (!IsActive) return;
            
            // En Shared Mode, cualquier jugador puede interactuar
            RPC_ProcessKeyPress(noteName, keyIndex, Runner.LocalPlayer);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ProcessKeyPress(string noteName, int keyIndex, PlayerRef player, RpcInfo info = default)
        {
            if (!IsActive) return;
            
            // Validar si es input duplicado
            if (!_allowMultiplePlayers && LastPlayerInput != player && _currentNotes.Count > 0)
            {
                Debug.Log($"[NetworkedPiano] Different player attempted input. Last: {LastPlayerInput}, Current: {player}");
                return;
            }
            
            LastPlayerInput = player;
            _currentNotes.Add(noteName);
            CurrentSequence = string.Join("", _currentNotes);
            SequenceProgress = _currentNotes.Count;
            
            string expected = ExpectedSequence.ToString();
            int currentIndex = _currentNotes.Count - 1;
            
            // Validar nota por nota
            if (currentIndex < expected.Length && 
                noteName == expected.Substring(currentIndex * 2, 2)) // Asumiendo formato "DoReMiFa"
            {
                // Nota correcta
                RPC_NotifyCorrectNote(keyIndex, SequenceProgress);
                
                // Verificar si completó la secuencia
                if (_currentNotes.Count * 2 >= expected.Length)
                {
                    IsActive = false;
                    RPC_SequenceComplete();
                }
            }
            else
            {
                // Nota incorrecta
                CurrentAttempt++;
                RPC_NotifyWrongNote(keyIndex);
                
                if (CurrentAttempt >= _maxAttempts)
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
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdatePianoState(NetworkBool active, int progress, int attempts)
        {
            IsActive = active;
            SequenceProgress = progress;
            CurrentAttempt = attempts;
            UpdateProgressIndicators(progress);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyCorrectNote(int keyIndex, int progress)
        {
            if (keyIndex < _pianoKeys.Length)
                _pianoKeys[keyIndex].ShowCorrectFeedback();
                
            UpdateProgressIndicators(progress);
            OnProgressUpdate?.Invoke(progress);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyWrongNote(int keyIndex)
        {
            if (keyIndex < _pianoKeys.Length)
                _pianoKeys[keyIndex].ShowErrorFeedback();
                
            PlaySound(_wrongSound);
            ShowErrorFeedback();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SequenceComplete()
        {
            OnSequenceCompleted?.Invoke();
            PlaySound(_successMelody ?? _correctSound);
            ShowSuccessFeedback();
            Debug.Log("[NetworkedPiano] Sequence completed successfully!");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SequenceFailed()
        {
            OnSequenceFailed?.Invoke();
            PlaySound(_wrongSound);
            ResetSequence();
            Debug.Log("[NetworkedPiano] Sequence failed after max attempts");
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
            CurrentSequence = "";
            SequenceProgress = 0;
            UpdateProgressIndicators(0);
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