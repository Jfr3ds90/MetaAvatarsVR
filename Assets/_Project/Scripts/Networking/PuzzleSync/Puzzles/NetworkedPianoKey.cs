using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    
    public class NetworkedPianoKey : NetworkBehaviour
    {
        [Header("Key Configuration")]
        [SerializeField] private int _keyIndex = 0;
        [SerializeField] private string _noteName = "Do";
        [SerializeField] private AudioClip _noteSound;
        
        [Header("Visual")]
        [SerializeField] private MeshRenderer _keyRenderer;
        [SerializeField] private Material _defaultMaterial;
        [SerializeField] private Material _pressedMaterial;
        
        [Header("Network State")]
        [Networked] public int KeyIndex { get; set; }
        [Networked] public NetworkBool IsPressed { get; set; }
        
        [Header("Events")]
        public UnityEvent<string> OnKeyPressed = new UnityEvent<string>();
        
        private PokeInteractable _pokeInteractable;
        private AudioSource _audioSource;
        
        private void Awake()
        {
            _pokeInteractable = GetComponent<PokeInteractable>();
            if (_pokeInteractable == null)
            {
                _pokeInteractable = gameObject.AddComponent<PokeInteractable>();
            }
            
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            if (_keyRenderer == null)
                _keyRenderer = GetComponent<MeshRenderer>();
        }
        
        private void Start()
        {
            if (_pokeInteractable != null)
            {
                _pokeInteractable.WhenPointerEventRaised += OnPokeEvent;
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                KeyIndex = _keyIndex;
                IsPressed = false;
            }
        }
        
        private void OnPokeEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select)
            {
                PressKey();
            }
        }
        
        private void PressKey()
        {
            RPC_OnKeyPress();
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_OnKeyPress(RpcInfo info = default)
        {
            if (IsPressed) return; 
            
            IsPressed = true;
            RPC_NotifyKeyPress();
            
            
            StartCoroutine(ResetKey());
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyKeyPress()
        {
            OnKeyPressed?.Invoke(_noteName);
            PlaySound();
            ShowPressedVisual();
        }
        
        private System.Collections.IEnumerator ResetKey()
        {
            yield return new WaitForSeconds(0.5f);
            IsPressed = false;
            UpdateVisual();
        }
        
        public void SetKeyData(int index, string note)
        {
            _keyIndex = index;
            _noteName = note;
        }
        
        private void PlaySound()
        {
            if (_noteSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_noteSound);
            }
        }
        
        private void ShowPressedVisual()
        {
            if (_keyRenderer != null && _pressedMaterial != null)
            {
                _keyRenderer.material = _pressedMaterial;
                StartCoroutine(ResetVisualAfterDelay());
            }
        }
        
        private System.Collections.IEnumerator ResetVisualAfterDelay()
        {
            yield return new WaitForSeconds(0.2f);
            UpdateVisual();
        }
        
        private void UpdateVisual()
        {
            if (_keyRenderer != null && _defaultMaterial != null)
            {
                _keyRenderer.material = _defaultMaterial;
            }
        }
        
        private void OnDestroy()
        {
            if (_pokeInteractable != null)
            {
                _pokeInteractable.WhenPointerEventRaised -= OnPokeEvent;
            }
        }
    }
}

