using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;


namespace MetaAvatarsVR.Networking.PuzzleSync
{

    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Grabbable))]
    public class NetworkedMetaGrabbable : NetworkBehaviour
    {
        [Header("Meta Components")]
        private Grabbable _grabbable;
        private PointableUnityEventWrapper _pointable;
        private HandGrabInteractable _handGrabInteractable;
        private OneGrabRotateTransformer _rotateTransformer;
        
        [Header("Network State")]
        [Networked] public NetworkBool IsGrabbed { get; set; }
        [Networked] public NetworkBool IsHovered { get; set; }
        [Networked] public PlayerRef GrabbingPlayer { get; set; }
        [Networked] public PlayerRef HoveringPlayer { get; set; }
        [Networked] public Vector3 NetworkedPosition { get; set; }
        [Networked] public QuaternionCompressed NetworkedRotation { get; set; }
        
        [Header("Events")]
        public UnityEvent<PlayerRef> OnMetaGrabbed = new UnityEvent<PlayerRef>();
        public UnityEvent<PlayerRef> OnMetaReleased = new UnityEvent<PlayerRef>();
        public UnityEvent<PlayerRef> OnMetaHovered = new UnityEvent<PlayerRef>();
        public UnityEvent<PlayerRef> OnMetaUnhovered = new UnityEvent<PlayerRef>();
        
        [Header("Configuration")]
        [SerializeField] private bool _syncPosition = false;
        [SerializeField] private bool _syncRotation = true;
        [SerializeField] private float _syncRate = 15f;
        [SerializeField] private float _interpolationSpeed = 10f;
        
        private bool _isLocallyGrabbed = false;
        private bool _isLocalPlayer = false;
        private Quaternion _lastValidRotation;
        private float _lastSyncTime = 0f;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _pointable = GetComponent<PointableUnityEventWrapper>();
            _handGrabInteractable = GetComponent<HandGrabInteractable>();
            _rotateTransformer = GetComponent<OneGrabRotateTransformer>();
            
            if (_grabbable == null)
            {
                AdvancedDebugSystem.LogError($"[NetworkedMetaGrabbable] Grabbable is required on {gameObject.name}", LogCategory.Networking | LogCategory.Photon);
            }
            
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }
        
        private void OnEnable()
        {
            SetupMetaEvents();
        }
        
        private void OnDisable()
        {
            CleanupMetaEvents();
        }
        
        #endregion
        
        #region Network Lifecycle
        
        public override void Spawned()
        {
            Debug.Log($"[NetworkedMetaGrabbable] Spawned {gameObject.name} - " +
                     $"HasStateAuthority: {HasStateAuthority}, " +
                     $"HasInputAuthority: {HasInputAuthority}");
            
            // Initialize state for all clients in Shared mode
            if (Object.HasStateAuthority)
            {
                IsGrabbed = false;
                IsHovered = false;
                NetworkedPosition = transform.position;
                NetworkedRotation = transform.rotation;
            }
            
            // In Shared mode, enable interaction components for local player only
            if (HasInputAuthority)
            {
                if (_rotateTransformer != null)
                {
                    _rotateTransformer.enabled = true;
                    AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Enabled OneGrabRotateTransformer for local player for {gameObject.name}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                }
                
                if (_handGrabInteractable != null)
                {
                    _handGrabInteractable.enabled = true;
                }
            }
            else
            {
                if (_rotateTransformer != null)
                {
                    _rotateTransformer.enabled = false;
                    AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Disabled OneGrabRotateTransformer for remote player for {gameObject.name}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                }
                
                if (_handGrabInteractable != null)
                {
                    _handGrabInteractable.enabled = false;
                }
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            // In Shared mode, only sync position when local player is grabbing
            if (!HasInputAuthority || !_isLocallyGrabbed) return;
            
            if (IsGrabbed || Time.time - _lastSyncTime > (1f / _syncRate))
            {
                if (_syncPosition)
                    NetworkedPosition = transform.position;
                    
                if (_syncRotation)
                    NetworkedRotation = transform.rotation;
                    
                _lastSyncTime = Time.time;
            }
        }
        
        public override void Render()
        {
            // In Shared mode, interpolate for remote objects only
            if (!HasInputAuthority && !_isLocallyGrabbed)
            {
                if (_syncRotation)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, 
                        NetworkedRotation, 
                        Time.deltaTime * _interpolationSpeed
                    );
                }
                
                if (_syncPosition)
                {
                    transform.position = Vector3.Lerp(
                        transform.position,
                        NetworkedPosition,
                        Time.deltaTime * _interpolationSpeed
                    );
                }
            }
        }
        
        #endregion
        
        #region Meta SDK Events
        
        private void SetupMetaEvents()
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Setting up events for {gameObject.name}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised += OnPointerEvent;
                AdvancedDebugSystem.Log("[NetworkedMetaGrabbable] Connected to Grabbable events", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
            
            if (_pointable != null)
            {
                _pointable.WhenSelect.AddListener(OnSelect);
                _pointable.WhenUnselect.AddListener(OnUnselect);
                _pointable.WhenHover.AddListener(OnHover);
                _pointable.WhenUnhover.AddListener(OnUnhover);
                AdvancedDebugSystem.Log("[NetworkedMetaGrabbable] Connected to PointableUnityEventWrapper events", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
        }
        
        private void CleanupMetaEvents()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= OnPointerEvent;
            }
            
            if (_pointable != null)
            {
                _pointable.WhenSelect.RemoveListener(OnSelect);
                _pointable.WhenUnselect.RemoveListener(OnUnselect);
                _pointable.WhenHover.RemoveListener(OnHover);
                _pointable.WhenUnhover.RemoveListener(OnUnhover);
            }
        }
        
        private void OnPointerEvent(PointerEvent evt)
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] {gameObject.name} - Event: {evt.Type}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    ProcessHover(true);
                    break;
                    
                case PointerEventType.Unhover:
                    ProcessHover(false);
                    break;
                    
                case PointerEventType.Select:
                    AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Select event - starting grab", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                    ProcessGrab(true);
                    break;
                    
                case PointerEventType.Unselect:
                    AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Unselect event - releasing", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                    ProcessGrab(false);
                    break;
                    
                case PointerEventType.Move:
                    if (_isLocallyGrabbed)
                    {
                        ProcessMove();
                    }
                    break;
            }
        }
        
        private void OnSelect(PointerEvent pointerEvent)
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] OnSelect from PointableUnityEventWrapper - Type: {pointerEvent.Type}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            ProcessGrab(true);
        }

        private void OnUnselect(PointerEvent pointerEvent)
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] OnUnselect from PointableUnityEventWrapper - Type: {pointerEvent.Type}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            ProcessGrab(false);
        }

        private void OnHover(PointerEvent pointerEvent)
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] OnHover from PointableUnityEventWrapper", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            ProcessHover(true);
        }

        private void OnUnhover(PointerEvent pointerEvent)
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] OnUnhover from PointableUnityEventWrapper", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            ProcessHover(false);
        }
        
        #endregion
        
        #region Processing
        
        private void ProcessGrab(bool grabbing)
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] ProcessGrab: {grabbing} on {gameObject.name}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            if (Runner == null || !Runner.IsRunning)
            {
                AdvancedDebugSystem.LogWarning("[NetworkedMetaGrabbable] Runner not ready", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            PlayerRef localPlayer = Runner.LocalPlayer;
            _isLocallyGrabbed = grabbing;
            _isLocalPlayer = true;
            
            // In Shared mode, control is always enabled for local input authority
            if (HasInputAuthority && _rotateTransformer != null)
            {
                if (grabbing)
                {
                    _lastValidRotation = transform.rotation;
                    AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Starting local grab control for {gameObject.name}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                }
                else
                {
                    AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Ending local grab control for {gameObject.name}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                }
            }
            
            if (grabbing)
            {
                AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Sending RPC_OnGrabbed for player {localPlayer}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                RPC_OnGrabbed(localPlayer);
                OnMetaGrabbed?.Invoke(localPlayer);
            }
            else
            {
                AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable] Sending RPC_OnReleased for player {localPlayer}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                RPC_OnReleased(localPlayer);
                OnMetaReleased?.Invoke(localPlayer);
            }
        }
        
        private void ProcessHover(bool hovering)
        {
            if (Runner == null || !Runner.IsRunning) return;
            
            PlayerRef localPlayer = Runner.LocalPlayer;
            
            if (hovering)
            {
                RPC_OnHovered(localPlayer);
                OnMetaHovered?.Invoke(localPlayer);
            }
            else
            {
                RPC_OnUnhovered(localPlayer);
                OnMetaUnhovered?.Invoke(localPlayer);
            }
        }
        
        private void ProcessMove()
        {
            if (Runner == null || !Runner.IsRunning) return;
            
            if (_isLocallyGrabbed && _isLocalPlayer)
            {
                RPC_UpdateTransform(transform.position, transform.rotation);
            }
        }
        
        #endregion
        
        #region RPCs
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_OnGrabbed(PlayerRef player, RpcInfo info = default)
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable RPC] {gameObject.name} grabbed by player {player}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            if (IsGrabbed && GrabbingPlayer != player)
            {
                AdvancedDebugSystem.LogWarning($"Already grabbed by {GrabbingPlayer}", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            // Request authority transfer to grabbing player
            if (Object.HasStateAuthority && info.Source != Object.InputAuthority)
            {
                Object.AssignInputAuthority(info.Source);
            }
            
            IsGrabbed = true;
            GrabbingPlayer = player;
            
            RPC_BroadcastControlChange(player, true);
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_OnReleased(PlayerRef player, RpcInfo info = default)
        {
            AdvancedDebugSystem.Log($"[NetworkedMetaGrabbable RPC] {gameObject.name} released by player {player}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            if (!IsGrabbed || GrabbingPlayer != player) return;
            
            IsGrabbed = false;
            GrabbingPlayer = PlayerRef.None;
            
            RPC_BroadcastControlChange(player, false);
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_BroadcastControlChange(PlayerRef player, bool isGrabbing)
        {
            // In Shared mode, only the player with InputAuthority controls the object
            if (player != Runner.LocalPlayer && !HasInputAuthority)
            {
                if (_rotateTransformer != null)
                    _rotateTransformer.enabled = false;
                if (_handGrabInteractable != null)
                    _handGrabInteractable.enabled = false;
            }
            else if (HasInputAuthority)
            {
                if (_rotateTransformer != null)
                    _rotateTransformer.enabled = true;
                if (_handGrabInteractable != null)
                    _handGrabInteractable.enabled = true;
            }
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_OnHovered(PlayerRef player, RpcInfo info = default)
        {
            IsHovered = true;
            HoveringPlayer = player;
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_OnUnhovered(PlayerRef player, RpcInfo info = default)
        {
            if (HoveringPlayer != player) return;
            
            IsHovered = false;
            HoveringPlayer = PlayerRef.None;
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_UpdateTransform(Vector3 position, Quaternion rotation, RpcInfo info = default)
        {
            if (IsGrabbed && info.Source == GrabbingPlayer)
            {
                if (_syncPosition)
                    NetworkedPosition = position;
                    
                if (_syncRotation)
                    NetworkedRotation = rotation;
            }
        }
        
        #endregion
    }
}