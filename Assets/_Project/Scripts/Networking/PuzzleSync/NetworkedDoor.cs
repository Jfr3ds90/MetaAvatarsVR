using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
    public enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing,
        Locked
    }
    
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedDoor : NetworkBehaviour
    {
        [Header("Door Configuration")]
        [SerializeField] private Transform _doorTransform;
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _openSpeed = 2f;
        [SerializeField] private bool _autoClose = false;
        [SerializeField] private float _autoCloseDelay = 5f;
        [SerializeField] private bool _requiresPuzzleCompletion = true;
        [SerializeField] private int _requiredPuzzleId = 0;
        
        [Header("Door Movement")]
        [SerializeField] private bool _useRotation = true;
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;
        [SerializeField] private bool _usePosition = false;
        [SerializeField] private Vector3 _openPosition;
        
        [Header("Audio & Visual")]
        [SerializeField] private AudioClip _openSound;
        [SerializeField] private AudioClip _closeSound;
        [SerializeField] private AudioClip _lockedSound;
        [SerializeField] private GameObject _lockedIndicator;
        [SerializeField] private GameObject _unlockedIndicator;
        [SerializeField] private ParticleSystem _openParticles;
        
        [Header("Network State")]
        [Networked] public DoorState CurrentState { get; set; }
        [Networked] public NetworkBool IsUnlocked { get; set; }
        [Networked] public float StateChangeTime { get; set; }
        [Networked] public int OpenCount { get; set; }
        
        [Header("Events")]
        public UnityEvent OnDoorOpening = new UnityEvent();
        public UnityEvent OnDoorOpened = new UnityEvent();
        public UnityEvent OnDoorClosing = new UnityEvent();
        public UnityEvent OnDoorClosed = new UnityEvent();
        public UnityEvent OnDoorUnlocked = new UnityEvent();
        public UnityEvent OnDoorLocked = new UnityEvent();
        
        private AudioSource _audioSource;
        private Vector3 _closedRotation;
        private Vector3 _closedPosition;
        private Vector3 _targetRotation;
        private Vector3 _targetPosition;
        private Coroutine _autoCloseCoroutine;
        private bool _isInitialized = false;
        
        private void Awake()
        {
            if (_doorTransform == null)
                _doorTransform = transform;
                
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            _closedRotation = _doorTransform.localEulerAngles;
            _closedPosition = _doorTransform.localPosition;
            
            if (_usePosition && _openPosition == Vector3.zero)
            {
                _openPosition = _closedPosition + Vector3.up * 3f;
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                CurrentState = DoorState.Locked;
                IsUnlocked = false;
                StateChangeTime = 0f;
                OpenCount = 0;
                
                if (_requiresPuzzleCompletion)
                {
                    RegisterPuzzleCallback();
                }
                else
                {
                    UnlockDoor();
                }
            }
            
            UpdateDoorVisuals();
            _isInitialized = true;
        }
        
        private void RegisterPuzzleCallback()
        {
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RegisterPuzzleCallback(_requiredPuzzleId, OnPuzzleCompleted);
                
                if (NetworkedPuzzleManager.Instance.IsPuzzleCompleted(_requiredPuzzleId))
                {
                    UnlockDoor();
                }
            }
        }
        
        private void OnPuzzleCompleted(PuzzleProgress progress)
        {
            if (progress.State == PuzzleState.Completed)
            {
                RPC_UnlockDoor();
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestOpen(RpcInfo info = default)
        {
            if (CanOpen())
            {
                OpenDoor();
            }
            else if (CurrentState == DoorState.Locked)
            {
                RPC_PlayLockedSound();
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestClose(RpcInfo info = default)
        {
            if (CanClose())
            {
                CloseDoor();
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestToggle(RpcInfo info = default)
        {
            if (CurrentState == DoorState.Open || CurrentState == DoorState.Opening)
            {
                CloseDoor();
            }
            else if (CanOpen())
            {
                OpenDoor();
            }
            else if (CurrentState == DoorState.Locked)
            {
                RPC_PlayLockedSound();
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_UnlockDoor(RpcInfo info = default)
        {
            if (HasStateAuthority)
            {
                UnlockDoor();
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_LockDoor(RpcInfo info = default)
        {
            if (HasStateAuthority)
            {
                LockDoor();
            }
        }
        
        private void UnlockDoor()
        {
            IsUnlocked = true;
            CurrentState = DoorState.Closed;
            StateChangeTime = Runner.SimulationTime;
            
            RPC_NotifyUnlocked();
        }
        
        private void LockDoor()
        {
            IsUnlocked = false;
            CurrentState = DoorState.Locked;
            StateChangeTime = Runner.SimulationTime;
            
            if (CurrentState == DoorState.Open || CurrentState == DoorState.Opening)
            {
                CloseDoor();
            }
            
            RPC_NotifyLocked();
        }
        
        private void OpenDoor()
        {
            CurrentState = DoorState.Opening;
            StateChangeTime = Runner.SimulationTime;
            OpenCount++;
            
            RPC_NotifyOpening();
            
            if (_autoClose && _autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
            }
        }
        
        private void CloseDoor()
        {
            CurrentState = DoorState.Closing;
            StateChangeTime = Runner.SimulationTime;
            
            RPC_NotifyClosing();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyUnlocked()
        {
            OnDoorUnlocked?.Invoke();
            UpdateDoorVisuals();
            PlaySound(_openSound);
            
            Debug.Log($"[NetworkedDoor] {gameObject.name} unlocked!");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyLocked()
        {
            OnDoorLocked?.Invoke();
            UpdateDoorVisuals();
            
            Debug.Log($"[NetworkedDoor] {gameObject.name} locked!");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyOpening()
        {
            OnDoorOpening?.Invoke();
            PlaySound(_openSound);
            
            if (_openParticles != null)
                _openParticles.Play();
                
            StartCoroutine(AnimateDoorOpen());
            
            Debug.Log($"[NetworkedDoor] {gameObject.name} opening...");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyClosing()
        {
            OnDoorClosing?.Invoke();
            PlaySound(_closeSound);
            
            StartCoroutine(AnimateDoorClose());
            
            Debug.Log($"[NetworkedDoor] {gameObject.name} closing...");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayLockedSound()
        {
            PlaySound(_lockedSound);
            
            if (_lockedIndicator != null)
            {
                StartCoroutine(FlashIndicator(_lockedIndicator));
            }
        }
        
        private IEnumerator AnimateDoorOpen()
        {
            CalculateOpenTargets();
            
            float elapsed = 0f;
            float duration = 1f / _openSpeed;
            
            Vector3 startRotation = _doorTransform.localEulerAngles;
            Vector3 startPosition = _doorTransform.localPosition;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = Mathf.SmoothStep(0, 1, t);
                
                if (_useRotation)
                {
                    _doorTransform.localEulerAngles = Vector3.Lerp(startRotation, _targetRotation, t);
                }
                
                if (_usePosition)
                {
                    _doorTransform.localPosition = Vector3.Lerp(startPosition, _targetPosition, t);
                }
                
                yield return null;
            }
            
            if (_useRotation)
                _doorTransform.localEulerAngles = _targetRotation;
            if (_usePosition)
                _doorTransform.localPosition = _targetPosition;
                
            if (HasStateAuthority)
            {
                CurrentState = DoorState.Open;
                RPC_NotifyOpened();
                
                if (_autoClose)
                {
                    _autoCloseCoroutine = StartCoroutine(AutoCloseTimer());
                }
            }
        }
        
        private IEnumerator AnimateDoorClose()
        {
            float elapsed = 0f;
            float duration = 1f / _openSpeed;
            
            Vector3 startRotation = _doorTransform.localEulerAngles;
            Vector3 startPosition = _doorTransform.localPosition;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = Mathf.SmoothStep(0, 1, t);
                
                if (_useRotation)
                {
                    _doorTransform.localEulerAngles = Vector3.Lerp(startRotation, _closedRotation, t);
                }
                
                if (_usePosition)
                {
                    _doorTransform.localPosition = Vector3.Lerp(startPosition, _closedPosition, t);
                }
                
                yield return null;
            }
            
            if (_useRotation)
                _doorTransform.localEulerAngles = _closedRotation;
            if (_usePosition)
                _doorTransform.localPosition = _closedPosition;
                
            if (HasStateAuthority)
            {
                CurrentState = DoorState.Closed;
                RPC_NotifyClosed();
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyOpened()
        {
            OnDoorOpened?.Invoke();
            Debug.Log($"[NetworkedDoor] {gameObject.name} fully opened");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyClosed()
        {
            OnDoorClosed?.Invoke();
            Debug.Log($"[NetworkedDoor] {gameObject.name} fully closed");
        }
        
        private IEnumerator AutoCloseTimer()
        {
            yield return new WaitForSeconds(_autoCloseDelay);
            
            if (CurrentState == DoorState.Open)
            {
                CloseDoor();
            }
        }
        
        private IEnumerator FlashIndicator(GameObject indicator)
        {
            if (indicator == null)
                yield break;
                
            for (int i = 0; i < 3; i++)
            {
                indicator.SetActive(true);
                yield return new WaitForSeconds(0.2f);
                indicator.SetActive(false);
                yield return new WaitForSeconds(0.2f);
            }
        }
        
        private void CalculateOpenTargets()
        {
            if (_useRotation)
            {
                _targetRotation = _closedRotation + (_rotationAxis * _openAngle);
            }
            
            if (_usePosition)
            {
                _targetPosition = _openPosition;
            }
        }
        
        private void UpdateDoorVisuals()
        {
            if (_lockedIndicator != null)
                _lockedIndicator.SetActive(CurrentState == DoorState.Locked);
                
            if (_unlockedIndicator != null)
                _unlockedIndicator.SetActive(IsUnlocked);
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
        
        private bool CanOpen()
        {
            return IsUnlocked && (CurrentState == DoorState.Closed || CurrentState == DoorState.Closing);
        }
        
        private bool CanClose()
        {
            return CurrentState == DoorState.Open || CurrentState == DoorState.Opening;
        }
        
        public void ForceOpen()
        {
            if (HasStateAuthority)
            {
                UnlockDoor();
                OpenDoor();
            }
        }
        
        public void ForceClose()
        {
            if (HasStateAuthority)
            {
                CloseDoor();
            }
        }
        
        private void OnDestroy()
        {
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.UnregisterPuzzleCallback(_requiredPuzzleId, OnPuzzleCompleted);
            }
            
            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (_doorTransform == null)
                _doorTransform = transform;
                
            Gizmos.color = Color.green;
            
            if (_useRotation)
            {
                Vector3 openDir = Quaternion.Euler(_rotationAxis * _openAngle) * _doorTransform.forward;
                Gizmos.DrawRay(_doorTransform.position, openDir * 2f);
            }
            
            if (_usePosition)
            {
                Gizmos.DrawWireCube(_doorTransform.TransformPoint(_openPosition), Vector3.one * 0.5f);
            }
        }
    }
}