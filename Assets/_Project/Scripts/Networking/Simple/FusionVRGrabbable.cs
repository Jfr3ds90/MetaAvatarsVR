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
    public class FusionVRGrabbable : NetworkBehaviour, IStateAuthorityChanged
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
            // SOLUCIÓN: Inicializar y sincronizar posición para TODOS los clientes
            if (_networkRigidbody != null)
            {
                // Teleport inicial para sincronizar la posición en todos los clientes
                _networkRigidbody.Teleport(transform.position, transform.rotation);
                
                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] Initial teleport to sync position: {transform.position}");
            }
            
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
        
        // Implementación de IStateAuthorityChanged
        public void StateAuthorityChanged()
        {
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] StateAuthority changed to: {Object.StateAuthority}. IsGrabbed: {IsGrabbed}");
            
            // SOLUCIÓN: Cuando perdemos autoridad mientras el objeto está siendo agarrado
            if (!HasStateAuthority && IsGrabbed)
            {
                // Hacer el rigidbody kinematic para evitar conflictos de física
                _rigidbody.isKinematic = true;
                
                // Desactivar colliders para evitar interferencias
                foreach (var col in _colliders)
                {
                    if (col != null)
                        col.enabled = false;
                }
                
                // IMPORTANTE: Teleport para sincronizar con el nuevo estado
                if (_networkRigidbody != null)
                {
                    _networkRigidbody.Teleport(transform.position, transform.rotation);
                    
                    if (_debugMode)
                        Debug.Log($"[FusionVRGrabbable] Teleported on authority loss to sync position");
                }
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            // Actualizar estado de física solo si tenemos autoridad
            if (IsGrabbed && _localGrabber != null)
            {
                // Aplicar movimiento siguiendo al grabber
                ApplyGrabMovement();
                
                // Rastrear velocidad para el lanzamiento
                TrackVelocity();
                
                // Verificar distancia de ruptura
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
            // SOLUCIÓN SIMPLE: NO interpolar si NetworkRigidbody3D ya está manejando la interpolación
            // NetworkRigidbody3D ya maneja su propia interpolación, no debemos interferir
            // Solo dejar que NetworkRigidbody3D haga su trabajo
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
                
                // Calcular offsets basados en la posición actual
                GrabOffset = _localGrabber.InverseTransformPoint(transform.position);
                GrabRotationOffset = Quaternion.Inverse(_localGrabber.rotation) * transform.rotation;
                
                _isLocallyGrabbed = true;
                
                if (!HasStateAuthority)
                {
                    // Solicitar autoridad sin hacer cambios prematuros
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
            
            // Guardar posición actual antes de solicitar autoridad
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            
            Object.RequestStateAuthority();
            
            float timeout = 1f;
            float elapsed = 0f;
            
            // Esperar autoridad sin mover el objeto
            while (!HasStateAuthority && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (HasStateAuthority)
            {
                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] State authority acquired in {elapsed:F2}s!");
                
                // SOLUCIÓN CRÍTICA: Teleport después de obtener autoridad
                // Esto sincroniza la posición correctamente con todos los clientes
                if (_networkRigidbody != null)
                {
                    _networkRigidbody.Teleport(currentPos, currentRot);
                    
                    if (_debugMode)
                        Debug.Log($"[FusionVRGrabbable] Teleported to sync position after authority transfer: {currentPos}");
                }
                
                // Pequeña espera para que el teleport se propague
                yield return null;
                
                // Ahora aplicar el grab
                ApplyGrab();
            }
            else
            {
                if (_debugMode)
                    Debug.LogWarning($"[FusionVRGrabbable] Failed to acquire authority after {timeout}s");
                
                // No pudimos obtener autoridad, cancelar el grab
                _isLocallyGrabbed = false;
                _localGrabber = null;
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
            
            IsGrabbed = true;
            CurrentGrabber = Runner.LocalPlayer;
            IsKinematic = true;
            
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Grab applied - Player {Runner.LocalPlayer}");
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
            
            // SOLUCIÓN: Usar asignación directa para movimiento continuo
            // NetworkRigidbody3D sincronizará esto automáticamente
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            
            // Si la distancia es muy grande, puede ser un salto no deseado
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > 1f && _networkRigidbody != null)
            {
                // Para saltos grandes, usar Teleport para evitar interpolación incorrecta
                _networkRigidbody.Teleport(targetPosition, targetRotation);
                
                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] Large movement detected ({distance:F2}m), using Teleport");
            }
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
            
            // Solo actualizar física para clientes remotos
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