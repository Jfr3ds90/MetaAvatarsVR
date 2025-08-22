using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedLever : NetworkedInteractable
    {
        [Header("Lever Configuration")]
        [SerializeField] private int _leverIndex;
        [SerializeField] private string _leverLetter = "A";
        [SerializeField] private float _activationAngle = -55f;
        [SerializeField] private Transform _leverHandle;
        [SerializeField] private bool _returnToCenter = false;
        
        [Header("Puzzle Integration")]
        [SerializeField] private NetworkedLeverPuzzle _puzzleController;
        
        [Header("Network State")]
        [Networked] public NetworkBool IsActivated { get; set; }
        [Networked] public int ActivationOrder { get; set; }
        
        [Header("Lever Events")]
        public UnityEvent<int, bool> OnLeverStateChanged = new UnityEvent<int, bool>();
        public UnityEvent<string> OnLeverActivated = new UnityEvent<string>();
        public UnityEvent<string> OnLeverDeactivated = new UnityEvent<string>();
        
        private Quaternion _startRotation;
        private Quaternion _activatedRotation;
        private bool _isMoving = false;
        
        protected override void Awake()
        {
            base.Awake();
            
            if (_leverHandle == null)
                _leverHandle = transform;
                
            _startRotation = _leverHandle.localRotation;
            _activatedRotation = Quaternion.Euler(_activationAngle, 0, 0);
            
            if (_puzzleController == null)
                _puzzleController = GetComponentInParent<NetworkedLeverPuzzle>();
        }
        
        public override void Spawned()
        {
            base.Spawned();
            
            if (HasStateAuthority)
            {
                IsActivated = false;
                ActivationOrder = -1;
            }
            
            UpdateLeverVisual();
        }
        
        protected override void PerformActivation(PlayerRef player)
        {
            if (_isMoving)
                return;
                
            RPC_ToggleLever(player);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ToggleLever(PlayerRef player, RpcInfo info = default)
        {
            if (!HasStateAuthority)
                return;
                
            bool newState = !IsActivated;
            IsActivated = newState;
            
            if (newState)
            {
                if (_puzzleController != null)
                {
                    ActivationOrder = _puzzleController.RegisterLeverActivation(_leverIndex);
                }
                
                RPC_NotifyLeverActivated();
            }
            else
            {
                if (_puzzleController != null)
                {
                    _puzzleController.RegisterLeverDeactivation(_leverIndex);
                }
                
                ActivationOrder = -1;
                RPC_NotifyLeverDeactivated();
            }
            
            Debug.Log($"[NetworkedLever] Lever {_leverLetter} ({_leverIndex}) toggled to {newState} by player {player}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyLeverActivated()
        {
            OnLeverActivated?.Invoke(_leverLetter);
            OnLeverStateChanged?.Invoke(_leverIndex, true);
            AnimateLever(true);
            PlaySound(_interactSound);
            
            Debug.Log($"[NetworkedLever] Lever {_leverLetter} activated");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyLeverDeactivated()
        {
            OnLeverDeactivated?.Invoke(_leverLetter);
            OnLeverStateChanged?.Invoke(_leverIndex, false);
            AnimateLever(false);
            PlaySound(_releaseSound);
            
            Debug.Log($"[NetworkedLever] Lever {_leverLetter} deactivated");
        }
        
        private void AnimateLever(bool activate)
        {
            if (_leverHandle == null)
                return;
                
            _isMoving = true;
            
            Quaternion targetRotation = activate ? _activatedRotation : _startRotation;
            
            StartCoroutine(RotateLever(targetRotation));
        }
        
        private System.Collections.IEnumerator RotateLever(Quaternion targetRotation)
        {
            float duration = 0.5f;
            float elapsed = 0f;
            Quaternion startRotation = _leverHandle.localRotation;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = Mathf.SmoothStep(0, 1, t);
                
                _leverHandle.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }
            
            _leverHandle.localRotation = targetRotation;
            _isMoving = false;
            
            if (_returnToCenter && IsActivated)
            {
                yield return new WaitForSeconds(0.5f);
                if (IsActivated)
                {
                    RPC_ToggleLever(Runner.LocalPlayer);
                }
            }
        }
        
        private void UpdateLeverVisual()
        {
            if (_leverHandle != null)
            {
                _leverHandle.localRotation = IsActivated ? _activatedRotation : _startRotation;
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ResetLever()
        {
            IsActivated = false;
            ActivationOrder = -1;
            CurrentState = InteractableState.Idle;
            
            UpdateLeverVisual();
        }
        
        public void SetPuzzleController(NetworkedLeverPuzzle controller)
        {
            _puzzleController = controller;
        }
        
        public int GetLeverIndex()
        {
            return _leverIndex;
        }
        
        public string GetLeverLetter()
        {
            return _leverLetter;
        }
        
        public void SetLeverData(int index, string letter)
        {
            _leverIndex = index;
            _leverLetter = letter;
        }
        
        private void OnDrawGizmosSelected()
        {
            if (_leverHandle == null)
                _leverHandle = transform;
                
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_leverHandle.position, Vector3.one * 0.2f);
            
            Vector3 direction = _leverHandle.rotation * Vector3.forward;
            Gizmos.DrawRay(_leverHandle.position, direction * 0.5f);
            
            Gizmos.color = Color.green;
            Vector3 activatedDirection = Quaternion.Euler(_activationAngle, 0, 0) * Vector3.forward;
            Gizmos.DrawRay(_leverHandle.position, activatedDirection * 0.5f);
        }
    }
}