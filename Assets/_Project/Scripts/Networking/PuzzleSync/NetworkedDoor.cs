using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
    /// <summary>
    /// Puerta networked con animación procedural optimizada para VR multijugador
    /// Usa interpolación suave sin Animator para mejor performance y sincronización
    /// Compatible con el sistema de PuzzleProgress para callbacks de puzzles completados
    /// </summary>
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
        
        // Minimal network state for efficiency
        [Networked] public float DoorProgress { get; set; }  // 0 = closed, 1 = open
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
        
        // Cache para evitar allocations
        private Quaternion _cachedClosedQuat;
        private Quaternion _cachedOpenQuat;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Setup references
            if (_doorTransform == null)
                _doorTransform = transform;
            
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
            
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            
            // Cache initial positions
            _closedRotation = _doorTransform.localEulerAngles;
            _closedPosition = _doorTransform.localPosition;
            
            // Calculate open positions
            _openRotation = _closedRotation + (_rotationAxis * _openAngle);
            _openPosition = _closedPosition + _openOffset;
            
            // Cache quaternions for performance
            _cachedClosedQuat = Quaternion.Euler(_closedRotation);
            _cachedOpenQuat = Quaternion.Euler(_openRotation);
            
            // Calculate animation speed
            _animationSpeed = 1f / _animationDuration;
            
            // Disable any Animator if present (legacy support)
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
                // Initialize network state
                DoorProgress = 0f;
                IsMoving = false;
                TargetOpen = false;
                IsUnlocked = _startUnlocked;
                OpenCount = 0;
                AutoCloseTimer = TickTimer.None;
                
                // Register with puzzle system if needed
                if (_requiresPuzzleCompletion && !_startUnlocked)
                {
                    RegisterPuzzleCallback();
                }
            }
            
            // Set initial visual state
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
            
            // Update door animation
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
                
                // Check if animation complete
                if (Mathf.Approximately(DoorProgress, target))
                {
                    DoorProgress = target;
                    IsMoving = false;
                    
                    if (TargetOpen)
                    {
                        OpenCount++;
                        RPC_NotifyOpened();
                        
                        // Start auto-close timer if enabled
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
            
            // Check auto-close timer
            if (_autoClose && AutoCloseTimer.Expired(Runner))
            {
                AutoCloseTimer = TickTimer.None;
                CloseDoor();
            }
        }
        
        public override void Render()
        {
            // Smooth interpolation for all clients
            if (_isInitialized)
            {
                // Interpolate local progress for smooth visuals
                _localProgress = Mathf.Lerp(_localProgress, DoorProgress, Time.deltaTime * 10f);
                
                // Apply animation curve for more natural motion
                float curvedProgress = _animationCurve.Evaluate(_localProgress);
                
                // Update transform
                UpdateDoorTransform(curvedProgress);
                
                // Notify progress listeners
                OnDoorProgressChanged?.Invoke(_localProgress);
            }
        }
        
        #endregion
        
        #region Door Control
        
        /// <summary>
        /// Request to open the door (checks if unlocked)
        /// </summary>
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
        
        /// <summary>
        /// Request to close the door
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestClose()
        {
            if (!HasStateAuthority) return;
            
            if (!IsMoving && TargetOpen)
            {
                CloseDoor();
            }
        }
        
        /// <summary>
        /// Request to toggle door state
        /// </summary>
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
            
            // Cancel auto-close if reopening
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
        
        /// <summary>
        /// Unlock the door (usually called when puzzle is completed)
        /// </summary>
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
        
        /// <summary>
        /// Lock the door
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_LockDoor()
        {
            if (!HasStateAuthority) return;
            
            if (IsUnlocked)
            {
                IsUnlocked = false;
                
                // Close if open
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
                
                // Check if puzzle is already completed
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
        
        /// <summary>
        /// Callback cuando un puzzle se completa - usa PuzzleProgress struct
        /// </summary>
        private void OnPuzzleCompleted(PuzzleProgress progress)
        {
            // Check if this is the puzzle we're waiting for and if it's completed
            if (progress.PuzzleId == _requiredPuzzleId && progress.State == PuzzleState.Completed)
            {
                RPC_UnlockDoor();
                
                // Optional: Auto-open when puzzle is solved
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
                // Use quaternion lerp for smooth rotation
                _doorTransform.localRotation = Quaternion.Lerp(_cachedClosedQuat, _cachedOpenQuat, progress);
            }
            
            if (_usePosition)
            {
                // Use vector lerp for smooth position
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
            
            // Flash indicator
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
        
        /// <summary>
        /// Force open the door (bypasses lock check)
        /// </summary>
        public void ForceOpen()
        {
            if (HasStateAuthority)
            {
                IsUnlocked = true;
                OpenDoor();
            }
        }
        
        /// <summary>
        /// Force close the door
        /// </summary>
        public void ForceClose()
        {
            if (HasStateAuthority)
            {
                CloseDoor();
            }
        }
        
        /// <summary>
        /// Get current door state
        /// </summary>
        public bool IsOpen => DoorProgress >= 0.99f;
        public bool IsClosed => DoorProgress <= 0.01f;
        public bool IsTransitioning => IsMoving;
        public float GetProgress => DoorProgress;
        
        #endregion
        
        #region Cleanup
        
        private void OnDestroy()
        {
            // Unregister from puzzle manager
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
            
            // Draw closed position
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_doorTransform.position, Vector3.one * 0.1f);
            
            // Draw open position/rotation
            Gizmos.color = Color.green;
            
            if (_useRotation)
            {
                // Draw arc showing rotation path
                Vector3 closedDir = _doorTransform.forward;
                Vector3 openDir = Quaternion.Euler(_rotationAxis * _openAngle) * closedDir;
                
                // Draw rotation arc
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    Vector3 dir = Vector3.Slerp(closedDir, openDir, t);
                    Gizmos.DrawRay(_doorTransform.position, dir * 2f);
                }
                
                // Draw final position
                Gizmos.DrawLine(_doorTransform.position, _doorTransform.position + openDir * 2f);
            }
            
            if (_usePosition)
            {
                Vector3 openWorldPos = _doorTransform.TransformPoint(_openOffset);
                Gizmos.DrawWireCube(openWorldPos, Vector3.one * 0.1f);
                Gizmos.DrawLine(_doorTransform.position, openWorldPos);
            }
            
            // Draw lock state
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