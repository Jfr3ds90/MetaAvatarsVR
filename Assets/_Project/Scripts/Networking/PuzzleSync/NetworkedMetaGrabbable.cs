using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

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
                Debug.LogError($"[NetworkedMetaGrabbable] Grabbable is required on {gameObject.name}");
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
            
            if (HasStateAuthority)
            {
                IsGrabbed = false;
                IsHovered = false;
                NetworkedPosition = transform.position;
                NetworkedRotation = transform.rotation;
            }
            else
            {
                if (_rotateTransformer != null)
                {
                    _rotateTransformer.enabled = false;
                    Debug.Log($"[NetworkedMetaGrabbable] Disabled OneGrabRotateTransformer on client for {gameObject.name}");
                }
                
                if (_handGrabInteractable != null)
                {
                    _handGrabInteractable.enabled = false;
                }
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
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
            if (!HasStateAuthority && !_isLocallyGrabbed)
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
            Debug.Log($"[NetworkedMetaGrabbable] Setting up events for {gameObject.name}");
            
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised += OnPointerEvent;
                Debug.Log("[NetworkedMetaGrabbable] Connected to Grabbable events");
            }
            
            if (_pointable != null)
            {
                _pointable.WhenSelect.AddListener(OnSelect);
                _pointable.WhenUnselect.AddListener(OnUnselect);
                _pointable.WhenHover.AddListener(OnHover);
                _pointable.WhenUnhover.AddListener(OnUnhover);
                Debug.Log("[NetworkedMetaGrabbable] Connected to PointableUnityEventWrapper events");
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
            Debug.Log($"[NetworkedMetaGrabbable] {gameObject.name} - Event: {evt.Type}");
            
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    ProcessHover(true);
                    break;
                    
                case PointerEventType.Unhover:
                    ProcessHover(false);
                    break;
                    
                case PointerEventType.Select:
                    Debug.Log($"[NetworkedMetaGrabbable] Select event - starting grab");
                    ProcessGrab(true);
                    break;
                    
                case PointerEventType.Unselect:
                    Debug.Log($"[NetworkedMetaGrabbable] Unselect event - releasing");
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
            Debug.Log($"[NetworkedMetaGrabbable] OnSelect from PointableUnityEventWrapper - Type: {pointerEvent.Type}");
            ProcessGrab(true);
        }

        private void OnUnselect(PointerEvent pointerEvent)
        {
            Debug.Log($"[NetworkedMetaGrabbable] OnUnselect from PointableUnityEventWrapper - Type: {pointerEvent.Type}");
            ProcessGrab(false);
        }

        private void OnHover(PointerEvent pointerEvent)
        {
            Debug.Log($"[NetworkedMetaGrabbable] OnHover from PointableUnityEventWrapper");
            ProcessHover(true);
        }

        private void OnUnhover(PointerEvent pointerEvent)
        {
            Debug.Log($"[NetworkedMetaGrabbable] OnUnhover from PointableUnityEventWrapper");
            ProcessHover(false);
        }
        
        #endregion
        
        #region Processing
        
        private void ProcessGrab(bool grabbing)
        {
            Debug.Log($"[NetworkedMetaGrabbable] ProcessGrab: {grabbing} on {gameObject.name}");
            
            if (Runner == null || !Runner.IsRunning)
            {
                Debug.LogWarning("[NetworkedMetaGrabbable] Runner not ready");
                return;
            }
            
            PlayerRef localPlayer = Runner.LocalPlayer;
            _isLocallyGrabbed = grabbing;
            _isLocalPlayer = true;
            
            if (!HasStateAuthority && _rotateTransformer != null)
            {
                if (grabbing)
                {
                    _rotateTransformer.enabled = true;
                    if (_handGrabInteractable != null)
                        _handGrabInteractable.enabled = true;
                    
                    _lastValidRotation = transform.rotation;
                    Debug.Log($"[NetworkedMetaGrabbable] Enabled local control for {gameObject.name}");
                }
                else
                {
                    _rotateTransformer.enabled = false;
                    if (_handGrabInteractable != null)
                        _handGrabInteractable.enabled = false;
                    
                    Debug.Log($"[NetworkedMetaGrabbable] Disabled local control for {gameObject.name}");
                }
            }
            
            if (grabbing)
            {
                Debug.Log($"[NetworkedMetaGrabbable] Sending RPC_OnGrabbed for player {localPlayer}");
                RPC_OnGrabbed(localPlayer);
                OnMetaGrabbed?.Invoke(localPlayer);
            }
            else
            {
                Debug.Log($"[NetworkedMetaGrabbable] Sending RPC_OnReleased for player {localPlayer}");
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
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_OnGrabbed(PlayerRef player, RpcInfo info = default)
        {
            Debug.Log($"[NetworkedMetaGrabbable RPC] {gameObject.name} grabbed by player {player}");
            
            if (IsGrabbed && GrabbingPlayer != player)
            {
                Debug.LogWarning($"Already grabbed by {GrabbingPlayer}");
                return;
            }
            
            IsGrabbed = true;
            GrabbingPlayer = player;
            
            RPC_BroadcastControlChange(player, true);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_OnReleased(PlayerRef player, RpcInfo info = default)
        {
            Debug.Log($"[NetworkedMetaGrabbable RPC] {gameObject.name} released by player {player}");
            
            if (!IsGrabbed || GrabbingPlayer != player) return;
            
            IsGrabbed = false;
            GrabbingPlayer = PlayerRef.None;
            
            RPC_BroadcastControlChange(player, false);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastControlChange(PlayerRef player, bool isGrabbing)
        {
            if (!HasStateAuthority)
            {
                if (isGrabbing && player != Runner.LocalPlayer)
                {
                    if (_rotateTransformer != null)
                        _rotateTransformer.enabled = false;
                    if (_handGrabInteractable != null)
                        _handGrabInteractable.enabled = false;
                }
                else if (!isGrabbing && player != Runner.LocalPlayer)
                {
                    if (_rotateTransformer != null)
                        _rotateTransformer.enabled = false;
                    if (_handGrabInteractable != null)
                        _handGrabInteractable.enabled = false;
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_OnHovered(PlayerRef player, RpcInfo info = default)
        {
            IsHovered = true;
            HoveringPlayer = player;
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_OnUnhovered(PlayerRef player, RpcInfo info = default)
        {
            if (HoveringPlayer != player) return;
            
            IsHovered = false;
            HoveringPlayer = PlayerRef.None;
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
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