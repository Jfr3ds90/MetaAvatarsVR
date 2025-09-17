using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
 
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedDoor : NetworkBehaviour
    {
        #region Configuration
        
        [Header("Door Configuration")]
        [SerializeField] private Transform _doorTransform;
        [SerializeField] private bool _requiresPuzzleCompletion = true;
        [SerializeField] private int _requiredPuzzleId = 0;
        
        [Header("Movement Type")]
        [SerializeField] private bool _useRotation = true;
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;
        [SerializeField] private float _openAngle = 90f;
        
        [SerializeField] private bool _usePosition = false;
        [SerializeField] private Vector3 _openOffset = new Vector3(0, 3, 0);
        
        [Header("Animation Settings")]
        [SerializeField] private float _animationDuration = 1.5f;
        [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool _autoClose = false;
        [SerializeField] private float _autoCloseDelay = 5f;
        
        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _openSound;
        [SerializeField] private AudioClip _closeSound;
        [SerializeField] private AudioClip _lockedSound;
        [SerializeField] private AudioClip _unlockSound;
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject _lockedIndicator;
        [SerializeField] private GameObject _unlockedIndicator;
        [SerializeField] private ParticleSystem _openParticles;
        [SerializeField] private ParticleSystem _closeParticles;
        
        [Header("Debug")]
        [SerializeField] private bool _debugMode = false;
        [SerializeField] private bool _startUnlocked = false;
        
        #endregion
        
        #region Network State
        
        [Networked] public float DoorProgress { get; set; }  
        [Networked] public NetworkBool IsMoving { get; set; }
        [Networked] public NetworkBool TargetOpen { get; set; }
        [Networked] public NetworkBool IsUnlocked { get; set; }
        [Networked] public int OpenCount { get; set; }
        [Networked] public TickTimer AutoCloseTimer { get; set; }
        
        #endregion
        
        #region Events
        
        [Header("Events")]
        public UnityEvent OnDoorOpening = new UnityEvent();
        public UnityEvent OnDoorOpened = new UnityEvent();
        public UnityEvent OnDoorClosing = new UnityEvent();
        public UnityEvent OnDoorClosed = new UnityEvent();
        public UnityEvent OnDoorUnlocked = new UnityEvent();
        public UnityEvent OnDoorLocked = new UnityEvent();
        public UnityEvent<float> OnDoorProgressChanged = new UnityEvent<float>();
        
        #endregion
        
        #region Private Variables
        
        private Vector3 _closedRotation;
        private Vector3 _openRotation;
        private Vector3 _closedPosition;
        private Vector3 _openPosition;
        
        private float _animationSpeed;
        private float _localProgress;
        private bool _isInitialized = false;
        
        private Quaternion _cachedClosedQuat;
        private Quaternion _cachedOpenQuat;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_doorTransform == null)
                _doorTransform = transform;
            
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
            
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            
            _closedRotation = _doorTransform.localEulerAngles;
            _closedPosition = _doorTransform.localPosition;
            
            _openRotation = _closedRotation + (_rotationAxis * _openAngle);
            _openPosition = _closedPosition + _openOffset;
            
            _cachedClosedQuat = Quaternion.Euler(_closedRotation);
            _cachedOpenQuat = Quaternion.Euler(_openRotation);
            
            _animationSpeed = 1f / _animationDuration;
            
            var animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
                if (_debugMode)
                    Debug.LogWarning($"[NetworkedDoor] Animator disabled on {name} - using procedural animation");
            }
        }
        
        #endregion
        
        #region Network Lifecycle
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                DoorProgress = 0f;
                IsMoving = false;
                TargetOpen = false;
                IsUnlocked = _startUnlocked;
                OpenCount = 0;
                AutoCloseTimer = TickTimer.None;
                
                if (_requiresPuzzleCompletion && !_startUnlocked)
                {
                    RegisterPuzzleCallback();
                }
            }
            
            _localProgress = DoorProgress;
            UpdateDoorTransform(_localProgress);
            UpdateVisualIndicators();
            
            _isInitialized = true;
            
            if (_debugMode)
                Debug.Log($"[NetworkedDoor] {name} spawned - Unlocked: {IsUnlocked}");
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            if (IsMoving)
            {
                float target = TargetOpen ? 1f : 0f;
                float newProgress = Mathf.MoveTowards(DoorProgress, target, Runner.DeltaTime * _animationSpeed);
                
                if (!Mathf.Approximately(newProgress, DoorProgress))
                {
                    DoorProgress = newProgress;
                    
                    if (_debugMode)
                        Debug.Log($"[NetworkedDoor] Progress: {DoorProgress:F2}");
                }
                
                if (Mathf.Approximately(DoorProgress, target))
                {
                    DoorProgress = target;
                    IsMoving = false;
                    
                    if (TargetOpen)
                    {
                        OpenCount++;
                        RPC_NotifyOpened();
                        
                        if (_autoClose)
                        {
                            AutoCloseTimer = TickTimer.CreateFromSeconds(Runner, _autoCloseDelay);
                        }
                    }
                    else
                    {
                        RPC_NotifyClosed();
                    }
                }
            }
            
            if (_autoClose && AutoCloseTimer.Expired(Runner))
            {
                AutoCloseTimer = TickTimer.None;
                CloseDoor();
            }
        }
        
        public override void Render()
        {
            if (_isInitialized)
            {
                _localProgress = Mathf.Lerp(_localProgress, DoorProgress, Time.deltaTime * 10f);
                
                float curvedProgress = _animationCurve.Evaluate(_localProgress);
                
                UpdateDoorTransform(curvedProgress);
                
                OnDoorProgressChanged?.Invoke(_localProgress);
            }
        }
        
        #endregion
        
        #region Door Control
        
       
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestOpen()
        {
            if (!HasStateAuthority) return;
            
            if (IsUnlocked && !IsMoving && !TargetOpen)
            {
                OpenDoor();
            }
            else if (!IsUnlocked)
            {
                RPC_PlayLockedFeedback();
            }
        }
        
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestClose()
        {
            if (!HasStateAuthority) return;
            
            if (!IsMoving && TargetOpen)
            {
                CloseDoor();
            }
        }
        
       
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestToggle()
        {
            if (!HasStateAuthority) return;
            
            if (TargetOpen)
                CloseDoor();
            else if (IsUnlocked)
                OpenDoor();
            else
                RPC_PlayLockedFeedback();
        }
        
        private void OpenDoor()
        {
            if (!HasStateAuthority) return;
            
            TargetOpen = true;
            IsMoving = true;
            
            if (AutoCloseTimer.IsRunning)
                AutoCloseTimer = TickTimer.None;
            
            RPC_NotifyOpening();
            
            if (_debugMode)
                Debug.Log($"[NetworkedDoor] {name} opening...");
        }
        
        private void CloseDoor()
        {
            if (!HasStateAuthority) return;
            
            TargetOpen = false;
            IsMoving = true;
            
            RPC_NotifyClosing();
            
            if (_debugMode)
                Debug.Log($"[NetworkedDoor] {name} closing...");
        }
        
        #endregion
        
        #region Lock/Unlock System
        
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_UnlockDoor()
        {
            if (!HasStateAuthority) return;
            
            if (!IsUnlocked)
            {
                IsUnlocked = true;
                RPC_NotifyUnlocked();
                
                if (_debugMode)
                    Debug.Log($"[NetworkedDoor] {name} unlocked!");
            }
        }
        
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_LockDoor()
        {
            if (!HasStateAuthority) return;
            
            if (IsUnlocked)
            {
                IsUnlocked = false;
                
                if (TargetOpen || IsMoving)
                {
                    CloseDoor();
                }
                
                RPC_NotifyLocked();
                
                if (_debugMode)
                    Debug.Log($"[NetworkedDoor] {name} locked!");
            }
        }
        
        #endregion
        
        #region Puzzle Integration
        
        private void RegisterPuzzleCallback()
        {
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RegisterPuzzleCallback(_requiredPuzzleId, OnPuzzleCompleted);
                
                if (NetworkedPuzzleManager.Instance.IsPuzzleCompleted(_requiredPuzzleId))
                {
                    RPC_UnlockDoor();
                }
                
                if (_debugMode)
                    Debug.Log($"[NetworkedDoor] {name} registered for puzzle {_requiredPuzzleId}");
            }
            else
            {
                Debug.LogWarning($"[NetworkedDoor] No NetworkedPuzzleManager found for {name}!");
            }
        }
        
       
        private void OnPuzzleCompleted(PuzzleProgress progress)
        {
            if (progress.PuzzleId == _requiredPuzzleId && progress.State == PuzzleState.Completed)
            {
                RPC_UnlockDoor();
                
                if (HasStateAuthority)
                {
                    StartCoroutine(DelayedOpen());
                }
                
                if (_debugMode)
                    Debug.Log($"[NetworkedDoor] {name} unlocked by puzzle {progress.PuzzleId} completion!");
            }
        }
        
        private IEnumerator DelayedOpen()
        {
            yield return new WaitForSeconds(0.5f);
            OpenDoor();
        }
        
        #endregion
        
        #region Visual Updates
        
        private void UpdateDoorTransform(float progress)
        {
            if (_useRotation)
            {
                _doorTransform.localRotation = Quaternion.Lerp(_cachedClosedQuat, _cachedOpenQuat, progress);
            }
            
            if (_usePosition)
            {
                _doorTransform.localPosition = Vector3.Lerp(_closedPosition, _openPosition, progress);
            }
        }
        
        private void UpdateVisualIndicators()
        {
            if (_lockedIndicator != null)
                _lockedIndicator.SetActive(!IsUnlocked);
            
            if (_unlockedIndicator != null)
                _unlockedIndicator.SetActive(IsUnlocked);
        }
        
        #endregion
        
        #region RPC Notifications
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyOpening()
        {
            OnDoorOpening?.Invoke();
            PlaySound(_openSound);
            
            if (_openParticles != null)
                _openParticles.Play();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyOpened()
        {
            OnDoorOpened?.Invoke();
            
            if (_debugMode)
                Debug.Log($"[NetworkedDoor] {name} opened (Count: {OpenCount})");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyClosing()
        {
            OnDoorClosing?.Invoke();
            PlaySound(_closeSound);
            
            if (_closeParticles != null)
                _closeParticles.Play();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyClosed()
        {
            OnDoorClosed?.Invoke();
            
            if (_debugMode)
                Debug.Log($"[NetworkedDoor] {name} closed");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyUnlocked()
        {
            OnDoorUnlocked?.Invoke();
            PlaySound(_unlockSound);
            UpdateVisualIndicators();
            
            if (_unlockedIndicator != null)
            {
                StartCoroutine(FlashIndicator(_unlockedIndicator, Color.green));
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyLocked()
        {
            OnDoorLocked?.Invoke();
            UpdateVisualIndicators();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayLockedFeedback()
        {
            PlaySound(_lockedSound);
            
            if (_lockedIndicator != null)
            {
                StartCoroutine(FlashIndicator(_lockedIndicator, Color.red));
            }
            
            if (_debugMode)
                Debug.Log($"[NetworkedDoor] {name} is locked!");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
        
        private IEnumerator FlashIndicator(GameObject indicator, Color flashColor)
        {
            if (indicator == null) yield break;
            
            var renderer = indicator.GetComponent<Renderer>();
            if (renderer == null) yield break;
            
            Color originalColor = renderer.material.color;
            float flashDuration = 0.2f;
            int flashCount = 3;
            
            for (int i = 0; i < flashCount; i++)
            {
                renderer.material.color = flashColor;
                yield return new WaitForSeconds(flashDuration);
                renderer.material.color = originalColor;
                yield return new WaitForSeconds(flashDuration);
            }
        }
        
        #endregion
        
        #region Public API
        
       
        public void ForceOpen()
        {
            if (HasStateAuthority)
            {
                IsUnlocked = true;
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
        
       
        public bool IsOpen => DoorProgress >= 0.99f;
        public bool IsClosed => DoorProgress <= 0.01f;
        public bool IsTransitioning => IsMoving;
        public float GetProgress => DoorProgress;
        
        #endregion
        
        #region Cleanup
        
        private void OnDestroy()
        {
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.UnregisterPuzzleCallback(_requiredPuzzleId, OnPuzzleCompleted);
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            if (_doorTransform == null)
                _doorTransform = transform;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_doorTransform.position, Vector3.one * 0.1f);
            
            Gizmos.color = Color.green;
            
            if (_useRotation)
            {
                Vector3 closedDir = _doorTransform.forward;
                Vector3 openDir = Quaternion.Euler(_rotationAxis * _openAngle) * closedDir;
                
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    Vector3 dir = Vector3.Slerp(closedDir, openDir, t);
                    Gizmos.DrawRay(_doorTransform.position, dir * 2f);
                }
                
                Gizmos.DrawLine(_doorTransform.position, _doorTransform.position + openDir * 2f);
            }
            
            if (_usePosition)
            {
                Vector3 openWorldPos = _doorTransform.TransformPoint(_openOffset);
                Gizmos.DrawWireCube(openWorldPos, Vector3.one * 0.1f);
                Gizmos.DrawLine(_doorTransform.position, openWorldPos);
            }
            
            Gizmos.color = IsUnlocked ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_doorTransform.position + Vector3.up * 2f, 0.2f);
        }
        
        private void OnGUI()
        {
            if (!_debugMode || !Application.isPlaying) return;
            
            Vector3 screenPos = Camera.main.WorldToScreenPoint(_doorTransform.position + Vector3.up * 2f);
            if (screenPos.z > 0)
            {
                string debugText = $"Door: {name}\n" +
                                 $"Progress: {DoorProgress:F2}\n" +
                                 $"State: {(IsMoving ? "Moving" : (IsOpen ? "Open" : "Closed"))}\n" +
                                 $"Locked: {!IsUnlocked}\n" +
                                 $"Opens: {OpenCount}";
                
                GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y - 50, 100, 100), debugText);
            }
        }
        
        #endregion
    }
}