using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedPianoKey : NetworkedInteractable
    {
        [Header("Piano Key Configuration")]
        [SerializeField] private int _keyIndex;
        [SerializeField] private string _noteName = "C";
        [SerializeField] private AudioClip _noteSound;
        [SerializeField] private float _keyPressDepth = 0.02f;
        [SerializeField] private float _keyAnimationSpeed = 10f;
        
        [Header("Visual Feedback")]
        [SerializeField] private Material _defaultMaterial;
        [SerializeField] private Material _pressedMaterial;
        [SerializeField] private Material _successMaterial;
        [SerializeField] private Material _failureMaterial;
        [SerializeField] private GameObject _noteIndicator;
        [SerializeField] private ParticleSystem _keyPressParticles;
        
        [Header("Key Events")]
        public UnityEvent OnKeyPressed = new UnityEvent();
        public UnityEvent OnKeyReleased = new UnityEvent();
        
        private NetworkedPiano _pianoController;
        private MeshRenderer _meshRenderer;
        private Vector3 _defaultPosition;
        private Vector3 _pressedPosition;
        private bool _isAnimating = false;
        private float _currentPressAmount = 0f;
        
        protected override void Awake()
        {
            base.Awake();
            
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = GetComponentInChildren<MeshRenderer>();
                
            _defaultPosition = transform.localPosition;
            _pressedPosition = _defaultPosition - Vector3.up * _keyPressDepth;
            
            if (_pianoController == null)
                _pianoController = GetComponentInParent<NetworkedPiano>();
        }
        
        protected override void PerformActivation(PlayerRef player)
        {
            RPC_PressKey(player);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_PressKey(PlayerRef player, RpcInfo info = default)
        {
            if (_pianoController != null)
            {
                _pianoController.RegisterKeyPress(_keyIndex, player);
            }
            
            RPC_NotifyKeyPressed(player);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyKeyPressed(PlayerRef player)
        {
            OnKeyPressed?.Invoke();
            PlayNote();
            AnimateKeyPress();
            
            Debug.Log($"[NetworkedPianoKey] Key {_noteName} ({_keyIndex}) pressed by player {player}");
        }
        
        public void PlayNote()
        {
            if (_noteSound != null && _audioSource != null)
            {
                _audioSource.pitch = 1f + (_keyIndex * 0.1f);
                _audioSource.PlayOneShot(_noteSound);
            }
        }
        
        public void AnimateKeyPress()
        {
            if (_isAnimating)
                return;
                
            StartCoroutine(AnimateKey());
        }
        
        private System.Collections.IEnumerator AnimateKey()
        {
            _isAnimating = true;
            
            if (_meshRenderer != null && _pressedMaterial != null)
            {
                _meshRenderer.material = _pressedMaterial;
            }
            
            if (_keyPressParticles != null)
            {
                _keyPressParticles.Play();
            }
            
            // Animate down
            while (_currentPressAmount < 1f)
            {
                _currentPressAmount += Time.deltaTime * _keyAnimationSpeed;
                _currentPressAmount = Mathf.Clamp01(_currentPressAmount);
                
                transform.localPosition = Vector3.Lerp(_defaultPosition, _pressedPosition, _currentPressAmount);
                yield return null;
            }
            
            // Hold for a moment
            yield return new WaitForSeconds(0.1f);
            
            // Animate up
            while (_currentPressAmount > 0f)
            {
                _currentPressAmount -= Time.deltaTime * _keyAnimationSpeed;
                _currentPressAmount = Mathf.Clamp01(_currentPressAmount);
                
                transform.localPosition = Vector3.Lerp(_defaultPosition, _pressedPosition, _currentPressAmount);
                yield return null;
            }
            
            transform.localPosition = _defaultPosition;
            
            if (_meshRenderer != null && _defaultMaterial != null)
            {
                _meshRenderer.material = _defaultMaterial;
            }
            
            _isAnimating = false;
            OnKeyReleased?.Invoke();
        }
        
        public void PlaySuccessFeedback()
        {
            if (_meshRenderer != null && _successMaterial != null)
            {
                StartCoroutine(FlashMaterial(_successMaterial, 0.5f));
            }
            
            if (_noteIndicator != null)
            {
                _noteIndicator.SetActive(true);
                StartCoroutine(HideIndicatorAfterDelay(1f));
            }
        }
        
        public void PlayFailureFeedback()
        {
            if (_meshRenderer != null && _failureMaterial != null)
            {
                StartCoroutine(FlashMaterial(_failureMaterial, 0.3f));
            }
        }
        
        private System.Collections.IEnumerator FlashMaterial(Material material, float duration)
        {
            if (_meshRenderer == null || material == null)
                yield break;
                
            Material originalMaterial = _meshRenderer.material;
            _meshRenderer.material = material;
            
            yield return new WaitForSeconds(duration);
            
            if (_meshRenderer != null && !_isAnimating)
            {
                _meshRenderer.material = _defaultMaterial ?? originalMaterial;
            }
        }
        
        private System.Collections.IEnumerator HideIndicatorAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_noteIndicator != null)
                _noteIndicator.SetActive(false);
        }
        
        public void SetKeyData(int index, NetworkedPiano piano)
        {
            _keyIndex = index;
            _pianoController = piano;
            
            // Set note name based on index
            string[] noteNames = { "C", "D", "E", "F", "G", "A", "B" };
            _noteName = noteNames[index % noteNames.Length];
        }
        
        public void ResetKey()
        {
            if (_meshRenderer != null && _defaultMaterial != null)
            {
                _meshRenderer.material = _defaultMaterial;
            }
            
            if (_noteIndicator != null)
            {
                _noteIndicator.SetActive(false);
            }
            
            transform.localPosition = _defaultPosition;
            _currentPressAmount = 0f;
            _isAnimating = false;
            
            CurrentState = InteractableState.Idle;
        }
        
        public int GetKeyIndex()
        {
            return _keyIndex;
        }
        
        public string GetNoteName()
        {
            return _noteName;
        }
        
        protected override void UpdateVisualState(InteractableState state)
        {
            base.UpdateVisualState(state);
            
            if (state == InteractableState.Hovering && _meshRenderer != null)
            {
                // Could add hover glow effect here
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
                return;
                
            Gizmos.color = Color.cyan;
            Vector3 currentPos = transform.position;
            Vector3 pressedPos = currentPos - Vector3.up * _keyPressDepth;
            
            Gizmos.DrawWireCube(currentPos, Vector3.one * 0.1f);
            Gizmos.DrawLine(currentPos, pressedPos);
            Gizmos.DrawWireCube(pressedPos, Vector3.one * 0.08f);
        }
    }
}