using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedPiano : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private NetworkedPianoKey[] _pianoKeys;
        [SerializeField] private int _maxAttempts = 3;
        
        [Header("Audio")]
        [SerializeField] private AudioClip _correctSound;
        [SerializeField] private AudioClip _wrongSound;
        
        [Header("Network State")]
        [Networked] public NetworkBool IsActive { get; set; }
        [Networked] public int CurrentAttempt { get; set; }
        [Networked, Capacity(20)]
        public NetworkString<_32> ExpectedSequence { get; set; }
        [Networked, Capacity(20)]
        public NetworkString<_32> CurrentSequence { get; set; }
        
        [Header("Events")]
        public UnityEvent OnSequenceCompleted = new UnityEvent();
        public UnityEvent OnSequenceFailed = new UnityEvent();
        
        private AudioSource _audioSource;
        private List<string> _currentNotes = new List<string>();
        private string[] _noteNames = { "Do", "Re", "Mi", "Fa", "Sol", "La", "Si" };
        
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            SetupKeys();
        }
        
        private void SetupKeys()
        {
            if (_pianoKeys == null || _pianoKeys.Length == 0)
                _pianoKeys = GetComponentsInChildren<NetworkedPianoKey>();
                
            for (int i = 0; i < _pianoKeys.Length; i++)
            {
                if (_pianoKeys[i] != null)
                {
                    _pianoKeys[i].SetKeyData(i, i < _noteNames.Length ? _noteNames[i] : $"Key{i}");
                    _pianoKeys[i].OnKeyPressed.AddListener(OnKeyPressed);
                }
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                IsActive = false;
                CurrentAttempt = 0;
                CurrentSequence = "";
            }
        }
        
        public void SetExpectedSequence(string sequence)
        {
            if (HasStateAuthority)
            {
                ExpectedSequence = sequence;
            }
        }
        
        public void ActivatePiano()
        {
            if (HasStateAuthority)
            {
                IsActive = true;
                CurrentAttempt = 0;
                CurrentSequence = "";
                _currentNotes.Clear();
            }
        }
        
        private void OnKeyPressed(string noteName)
        {
            if (!IsActive) return;
            
            RPC_ProcessKey(noteName);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ProcessKey(string noteName, RpcInfo info = default)
        {
            if (!IsActive) return;
            
            _currentNotes.Add(noteName);
            CurrentSequence = string.Join("", _currentNotes);
            
            string expected = ExpectedSequence.ToString();
            
            if (CurrentSequence.ToString() == expected)
            {
                IsActive = false;
                RPC_SequenceComplete();
            }
            else if (_currentNotes.Count >= expected.Length / 2)
            {
                CurrentAttempt++;
                if (CurrentAttempt >= _maxAttempts)
                {
                    RPC_SequenceFailed();
                }
                else
                {
                    ResetSequence();
                }
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SequenceComplete()
        {
            OnSequenceCompleted?.Invoke();
            PlaySound(_correctSound);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SequenceFailed()
        {
            OnSequenceFailed?.Invoke();
            PlaySound(_wrongSound);
            ResetSequence();
        }
        
        public void ResetSequence()
        {
            _currentNotes.Clear();
            CurrentSequence = "";
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
