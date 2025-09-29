using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using Oculus.Interaction;
using System.Collections;


namespace MetaAvatarsVR.Networking
{
    /// <summary>
    /// Sistema de grab para VR con Fusion 2 usando NetworkRigidbody3D
    /// Soluciona el problema de "lucha entre posiciones" usando la arquitectura correcta
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkRigidbody3D))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Grabbable))]
    public class FusionVRGrabbable : NetworkBehaviour
    {
        [Header("Grab Configuration")]
        [SerializeField] private bool _allowMultipleGrabbers = true;
        [SerializeField] private float _grabBreakDistance = 2f;
        [SerializeField] private LayerMask _grabbedLayer = -1;
        
        [Header("Physics")]
        [SerializeField] private bool _maintainVelocityOnRelease = true;
        [SerializeField] private float _throwForceMultiplier = 1.5f;
        
        [Header("Slot System")]
        [SerializeField] private bool _useSnapSlots = false;
        [SerializeField] private float _snapDistance = 0.3f;
        [SerializeField] private string _slotTag = "SnapSlot";
        
        [Header("Debug")]
        [SerializeField] private bool _debugMode = true;
        
        // Components
        private NetworkRigidbody3D _networkRigidbody;
        private Rigidbody _rigidbody;
        private Grabbable _metaGrabbable;
        private Collider[] _colliders;
        
        // Networked State
        [Networked, OnChangedRender(nameof(OnIsGrabbedChanged))] 
        public NetworkBool IsGrabbed { get; set; }
        [Networked] public PlayerRef CurrentGrabber { get; set; }
        [Networked] public Vector3 GrabOffset { get; set; }
        [Networked] public Quaternion GrabRotationOffset { get; set; }
        [Networked, OnChangedRender(nameof(OnIsKinematicChanged))] 
        public NetworkBool IsKinematic { get; set; }
        [Networked] public Vector3 LastVelocity { get; set; }
        [Networked] public Vector3 LastAngularVelocity { get; set; }
        
        // Local State
        private bool _isLocallyGrabbed = false;
        private Transform _localGrabber;
        private Vector3 _velocityTracker;
        private Vector3 _angularVelocityTracker;
        private int _originalLayer;
        private bool _wasKinematicBeforeGrab;
        
        // Authority Management
        private Coroutine _authorityRequestCoroutine;
        
        #region Unity Lifecycle
        
        void Awake()
        {
            // Cachear componentes
            _networkRigidbody = GetComponent<NetworkRigidbody3D>();
            _rigidbody = GetComponent<Rigidbody>();
            _metaGrabbable = GetComponent<Grabbable>();
            _colliders = GetComponentsInChildren<Collider>();
            
            _originalLayer = gameObject.layer;
            
            // Validar configuración
            ValidateSetup();
        }
        
        void Start()
        {
            // Suscribirse a eventos de Meta Grabbable
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised += OnMetaGrabbableEvent;
            }
        }
        
        #endregion
        
        #region Network Lifecycle
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                IsGrabbed = false;
                CurrentGrabber = PlayerRef.None;
                IsKinematic = _rigidbody.isKinematic;
            }
            
            _rigidbody.isKinematic = IsKinematic;
            
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Spawned. StateAuth: {Object.StateAuthority}, InputAuth: {Object.InputAuthority}, AllowOverride: {Object.Flags.HasFlag(NetworkObjectFlags.AllowStateAuthorityOverride)}");
        }
        
        public void StateAuthorityChanged()
        {
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] StateAuthority changed to: {Object.StateAuthority}. IsGrabbed: {IsGrabbed}");
            
            if (!HasStateAuthority && IsGrabbed)
            {
                _rigidbody.isKinematic = true;
                
                foreach (var col in _colliders)
                {
                    if (col != null)
                        col.enabled = false;
                }
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            // Actualizar estado de física
            if (IsGrabbed && _localGrabber != null)
            {
                // Aplicar movimiento siguiendo al grabber
                ApplyGrabMovement();
                
                // Rastrear velocidad para el lanzamiento
                TrackVelocity();
            }
            
            // Verificar distancia de ruptura
            if (IsGrabbed && _localGrabber != null)
            {
                float distance = Vector3.Distance(transform.position, _localGrabber.position);
                if (distance > _grabBreakDistance)
                {
                    if (_debugMode)
                        Debug.Log($"[FusionVRGrabbable] Breaking grab - distance too far: {distance:F2}");
                    
                    ForceRelease();
                }
            }
        }
        
        public override void Render()
        {
            // No necesitamos hacer nada aquí, la sincronización se maneja en los callbacks
        }
        
        #endregion
        
        #region Grab Logic
        
        private void OnMetaGrabbableEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    OnGrabStart(evt);
                    break;
                case PointerEventType.Unselect:
                    OnGrabEnd();
                    break;
            }
        }
        
        private void OnGrabStart(PointerEvent evt)
        {
            if (!Runner.IsRunning) return;
            
            if (evt.Data is IInteractorView interactor && interactor is MonoBehaviour mb)
            {
                _localGrabber = mb.transform;
                
                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] Local grab started. HasStateAuthority: {HasStateAuthority}");
                
                GrabOffset = _localGrabber.InverseTransformPoint(transform.position);
                GrabRotationOffset = Quaternion.Inverse(_localGrabber.rotation) * transform.rotation;
                
                _isLocallyGrabbed = true;
                
                if (!HasStateAuthority)
                {
                    if (_authorityRequestCoroutine != null)
                        StopCoroutine(_authorityRequestCoroutine);
                    
                    _authorityRequestCoroutine = StartCoroutine(RequestAuthorityCoroutine());
                }
                else
                {
                    ApplyGrab();
                }
            }
        }
        
        private void OnGrabEnd()
        {
            if (!_isLocallyGrabbed) return;
            
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Local release");
            
            _isLocallyGrabbed = false;
            
            if (HasStateAuthority)
            {
                ReleaseObject();
            }
            
            _localGrabber = null;
        }
        
        private IEnumerator RequestAuthorityCoroutine()
        {
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Requesting state authority... (Current: {Object.StateAuthority})");
            
            if (Object.HasStateAuthority)
            {
                Object.ReleaseStateAuthority();
                yield return new WaitForSeconds(0.1f);
            }
            
            Object.RequestStateAuthority();
            
            float timeout = 1f;
            float elapsed = 0f;
            
            while (!HasStateAuthority && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (HasStateAuthority)
            {
                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] State authority acquired in {elapsed:F2}s!");
                
                ApplyGrab();
            }
            else
            {
                if (_debugMode)
                    Debug.LogWarning($"[FusionVRGrabbable] Failed to acquire authority after {timeout}s. Check NetworkObject.AllowStateAuthorityOverride!");
                
                _isLocallyGrabbed = false;
                _localGrabber = null;
                
                if (_metaGrabbable != null)
                {
                    _metaGrabbable.enabled = false;
                    _metaGrabbable.enabled = true;
                }
            }
            
            _authorityRequestCoroutine = null;
        }
        
        private void ApplyGrab()
        {
            _wasKinematicBeforeGrab = _rigidbody.isKinematic;
            
            _rigidbody.isKinematic = true;
            
            foreach (var col in _colliders)
            {
                if (col != null)
                    col.enabled = false;
            }
            
            if (_grabbedLayer != -1)
            {
                gameObject.layer = (int)Mathf.Log(_grabbedLayer.value, 2);
            }
            
            if (_networkRigidbody != null)
            {
                _networkRigidbody.Teleport();
            }
            
            IsGrabbed = true;
            CurrentGrabber = Runner.LocalPlayer;
            IsKinematic = true;
            
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Grab applied - Player {Runner.LocalPlayer}, teleported to current position");
        }
        
        private void ReleaseObject()
        {
            if (_useSnapSlots && TrySnapToSlot())
            {
                IsGrabbed = false;
                CurrentGrabber = PlayerRef.None;
                IsKinematic = true;
                return;
            }
            
            IsGrabbed = false;
            CurrentGrabber = PlayerRef.None;
            IsKinematic = _wasKinematicBeforeGrab;
            
            _rigidbody.isKinematic = _wasKinematicBeforeGrab;
            
            foreach (var col in _colliders)
            {
                if (col != null)
                    col.enabled = true;
            }
            
            if (_maintainVelocityOnRelease && !_wasKinematicBeforeGrab)
            {
                _rigidbody.linearVelocity = LastVelocity * _throwForceMultiplier;
                _rigidbody.angularVelocity = LastAngularVelocity;
                
                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] Applied throw velocity: {LastVelocity.magnitude:F2}");
            }
            
            gameObject.layer = _originalLayer;
            
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Released, colliders re-enabled");
        }
        
        private void ForceRelease()
        {
            _isLocallyGrabbed = false;
            _localGrabber = null;
            
            if (HasStateAuthority)
            {
                ReleaseObject();
            }
        }
        
        #endregion
        
        #region Movement & Physics
        
        private void ApplyGrabMovement()
        {
            if (_localGrabber == null) return;
            
            Vector3 targetPosition = _localGrabber.TransformPoint(GrabOffset);
            Quaternion targetRotation = _localGrabber.rotation * GrabRotationOffset;
            
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        
        private void TrackVelocity()
        {
            // Calcular velocidad basada en cambio de posición
            Vector3 currentVelocity = (transform.position - _velocityTracker) / Runner.DeltaTime;
            LastVelocity = Vector3.Lerp(LastVelocity, currentVelocity, 0.5f);
            
            // Calcular velocidad angular
            Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(Quaternion.Euler(_angularVelocityTracker));
            float angle;
            Vector3 axis;
            deltaRotation.ToAngleAxis(out angle, out axis);
            LastAngularVelocity = axis * (angle * Mathf.Deg2Rad / Runner.DeltaTime);
            
            // Guardar posición actual para el siguiente frame
            _velocityTracker = transform.position;
            _angularVelocityTracker = transform.rotation.eulerAngles;
        }
        
        #endregion
        
        #region Slot System
        
        private bool TrySnapToSlot()
        {
            // Buscar slots cercanos
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _snapDistance);
            
            foreach (var collider in nearbyColliders)
            {
                if (collider.CompareTag(_slotTag))
                {
                    Transform slot = collider.transform;
                    
                    // Snap al slot
                    transform.position = slot.position;
                    transform.rotation = slot.rotation;
                    
                    // Hacer kinematic
                    _rigidbody.isKinematic = true;
                    IsKinematic = true;
                    
                    if (_debugMode)
                        Debug.Log($"[FusionVRGrabbable] Snapped to slot: {slot.name}");
                    
                    return true;
                }
            }
            
            return false;
        }
        
        #endregion
        
        #region Network Callbacks
        
        private void OnIsGrabbedChanged()
        {
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] IsGrabbed changed to: {IsGrabbed} by {CurrentGrabber}");
            
            if (!_isLocallyGrabbed)
            {
                if (IsGrabbed)
                {
                    _rigidbody.isKinematic = true;
                    
                    foreach (var col in _colliders)
                    {
                        if (col != null)
                            col.enabled = false;
                    }
                }
                else
                {
                    _rigidbody.isKinematic = IsKinematic;
                    
                    foreach (var col in _colliders)
                    {
                        if (col != null)
                            col.enabled = true;
                    }
                }
            }
        }
        
        private void OnIsKinematicChanged()
        {
            if (!HasStateAuthority && !_isLocallyGrabbed)
            {
                _rigidbody.isKinematic = IsKinematic;
                
                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] IsKinematic synced to: {IsKinematic}");
            }
        }
        
        #endregion
        
        #region Validation & Debug
        
        private void ValidateSetup()
        {
            if (_networkRigidbody == null)
            {
                Debug.LogError($"[FusionVRGrabbable] {name} - Missing NetworkRigidbody3D!");
            }
            
            if (GetComponent<NetworkTransform>() != null)
            {
                Debug.LogError($"[FusionVRGrabbable] {name} - Has NetworkTransform! Should only use NetworkRigidbody3D");
            }
            
            if (GetComponent<OneGrabFreeTransformer>() != null ||
                GetComponent<GrabFreeTransformer>() != null ||
                GetComponent<MoveTowardsTargetProvider>() != null)
            {
                Debug.LogWarning($"[FusionVRGrabbable] {name} - Has conflicting transformer components!");
            }
            
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && !netObj.Flags.HasFlag(NetworkObjectFlags.AllowStateAuthorityOverride))
            {
                Debug.LogError($"[FusionVRGrabbable] {name} - NetworkObject.AllowStateAuthorityOverride MUST be TRUE for grab to work!");
            }
            
            if (_debugMode)
            {
                Debug.Log($"[FusionVRGrabbable] {name} - Setup validated");
                Debug.Log("Make sure NetworkRunner has RunnerSimulatePhysics3D with ClientPhysicsSimulation set appropriately");
            }
        }
        
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            // Mostrar estado
            Gizmos.color = IsGrabbed ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
            
            // Mostrar conexión al grabber
            if (_localGrabber != null)
            {
                Gizmos.color = HasStateAuthority ? Color.green : Color.yellow;
                Gizmos.DrawLine(transform.position, _localGrabber.position);
            }
            
            // Mostrar slots cercanos
            if (_useSnapSlots)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, _snapDistance);
            }
        }
        
        void OnDestroy()
        {
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised -= OnMetaGrabbableEvent;
            }
            
            if (_authorityRequestCoroutine != null)
            {
                StopCoroutine(_authorityRequestCoroutine);
            }
        }
        
        #endregion
    }
}