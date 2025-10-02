using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Oculus.Interaction.HandGrab;

namespace MetaAvatarsVR.Networking.Pragmatic
{
    /// <summary>
    /// Sistema robusto de grab networking para VR con Photon Fusion
    /// Resuelve conflictos entre múltiples transformers y sistemas de movimiento
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Grabbable))]
    public class PragmaticNetworkedGrabbable : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool _usePhysicsWhileGrabbed = false;
        [SerializeField] private float _positionLerpSpeed = 15f;
        [SerializeField] private float _rotationLerpSpeed = 15f;
        [SerializeField] private LayerMask _grabbedLayer = -1;
        
        [Header("Debug")]
        [SerializeField] private bool _debugMode = true;
        [SerializeField] private float _grabOffsetDistance = 0.1f;
        
        // Components
        private Grabbable _grabbable;
        private NetworkTransform _networkTransform;
        private Rigidbody _rigidbody;
        private HandGrabInteractable _handGrabInteractable;
        
        // Cached Transform Components
        private List<MonoBehaviour> _transformerComponents = new List<MonoBehaviour>();
        private Dictionary<MonoBehaviour, bool> _transformerStates = new Dictionary<MonoBehaviour, bool>();
        
        [Header("Network State")]
        [Networked] public NetworkBool IsGrabbed { get; set; }
        [Networked] public PlayerRef GrabbingPlayer { get; set; }
        [Networked] public Vector3 SyncedPosition { get; set; }
        [Networked] public Quaternion SyncedRotation { get; set; }
        [Networked] public NetworkBool HasAuthority { get; set; }
        
        // Local State
        private bool _isLocallyGrabbed = false;
        private Transform _grabberTransform;
        private Transform _handTransform;
        private Vector3 _localGrabOffset;
        private Quaternion _rotationOffset;
        private int _originalLayer;
        private CancellationTokenSource _cancellationTokenSource;
        
        // Grab Point Management
        private Vector3 _grabPointLocalPosition;
        private Quaternion _grabPointLocalRotation;
        private bool _useHandPose = false;
        
        void Awake()
        {
            CacheComponents();
            DisableConflictingComponents();
        }
        
        private void CacheComponents()
        {
            _grabbable = GetComponent<Grabbable>();
            _networkTransform = GetComponent<NetworkTransform>();
            _rigidbody = GetComponent<Rigidbody>();
            _handGrabInteractable = GetComponent<HandGrabInteractable>();
            
            // Buscar y cachear todos los componentes de transformación
            var transformerTypes = new System.Type[]
            {
                typeof(OneGrabFreeTransformer),
                typeof(GrabFreeTransformer),
                typeof(MoveTowardsTargetProvider)
            };
            
            foreach (var type in transformerTypes)
            {
                var components = GetComponents(type);
                foreach (var comp in components)
                {
                    if (comp is MonoBehaviour mb)
                    {
                        _transformerComponents.Add(mb);
                        _transformerStates[mb] = mb.enabled;
                    }
                }
            }
            
            if (_debugMode)
            {
                Debug.Log($"[PragmaticNetworkedGrabbable] Found {_transformerComponents.Count} transformer components");
            }
        }
        
        private void DisableConflictingComponents()
        {
            // CRÍTICO: Desactivar TODOS los transformers conflictivos
            foreach (var transformer in _transformerComponents)
            {
                transformer.enabled = false;
            }
            
            if (_debugMode)
            {
                Debug.Log($"[PragmaticNetworkedGrabbable] Disabled all conflicting transformers");
            }
        }
        
        void Start()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised += OnGrabbableEvent;
            }
            
            _originalLayer = gameObject.layer;
        }
        
        public override void Spawned()
        {
            // SOLUCIÓN: En modo Shared, TODOS los clientes deben ver la posición inicial
            // No solo el que tiene StateAuthority
            if (Runner.GameMode == GameMode.Shared)
            {
                // En Shared mode, inicializar para todos los clientes
                if (!IsGrabbed) // Solo si no está siendo agarrado
                {
                    SyncedPosition = transform.position;
                    SyncedRotation = transform.rotation;
                }
                
                if (Object.HasStateAuthority)
                {
                    IsGrabbed = false;
                    GrabbingPlayer = PlayerRef.None;
                    HasAuthority = false; // No one has authority initially
                }
            }
            else if (Object.HasStateAuthority)
            {
                // Para otros modos (Host/Server)
                IsGrabbed = false;
                GrabbingPlayer = PlayerRef.None;
                SyncedPosition = transform.position;
                SyncedRotation = transform.rotation;
                HasAuthority = false;
            }
        }
        
        private void OnGrabbableEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    OnLocalGrab(evt);
                    break;
                case PointerEventType.Unselect:
                    OnLocalRelease();
                    break;
                case PointerEventType.Move:
                    UpdateGrabPosition(evt);
                    break;
            }
        }
        
        private void OnLocalGrab(PointerEvent evt)
        {
            if (!Runner.IsRunning) return;
            
            // Obtener el transform del interactor de manera robusta
            if (!ExtractGrabberTransform(evt))
            {
                Debug.LogError($"[PragmaticNetworkedGrabbable] Failed to extract grabber transform");
                return;
            }
            
            // Calcular offset relativo al punto de grab
            CalculateGrabOffset();
            
            // SOLUCIÓN CRÍTICA: NO marcar como agarrado localmente hasta tener autoridad
            // Esto previene que el objeto se mueva antes de tener autoridad
            
            // En modo Shared, verificar StateAuthority, no InputAuthority
            bool hasAuthority = (Runner.GameMode == GameMode.Shared) ? 
                                Object.HasStateAuthority : 
                                HasInputAuthority;
            
            if (!hasAuthority)
            {
                // Guardar la posición actual antes de solicitar autoridad
                Vector3 currentPos = transform.position;
                Quaternion currentRot = transform.rotation;
                
                if (_debugMode) 
                {
                    Debug.Log($"[PragmaticNetworkedGrabbable] No authority, requesting first...");
                    Debug.Log($"  - GameMode: {Runner.GameMode}");
                    Debug.Log($"  - HasStateAuthority: {Object.HasStateAuthority}");
                    Debug.Log($"  - HasInputAuthority: {HasInputAuthority}");
                    Debug.Log($"  - Object at: {currentPos}");
                }
                
                // Solicitar autoridad ANTES de hacer cualquier cambio
                RequestInputAuthorityForGrabAsync(evt).Forget();
            }
            else
            {
                // Ya tenemos autoridad, proceder normalmente
                if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] Already have authority, proceeding with grab");
                CompleteLocalGrab();
                SetGrabbedState();
            }
        }
        
        private void CompleteLocalGrab()
        {
            _isLocallyGrabbed = true;
            
            // SOLUCIÓN CLAVE #1: Desactivar NetworkTransform inmediatamente
            if (_networkTransform != null)
            {
                _networkTransform.enabled = false;
                if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] NetworkTransform DISABLED");
            }
            
            // SOLUCIÓN CLAVE #2: Asegurar que los transformers permanezcan desactivados
            DisableConflictingComponents();
            
            // SOLUCIÓN CLAVE #3: Configurar física apropiadamente
            ConfigurePhysicsForGrab();
            
            // Cambiar layer si está configurado
            if (_grabbedLayer != -1)
            {
                gameObject.layer = (int)Mathf.Log(_grabbedLayer.value, 2);
            }
        }
        
        private bool ExtractGrabberTransform(PointerEvent evt)
        {
            var interactor = evt.Data as IInteractorView;
            
            // Intento 1: Cast directo a MonoBehaviour
            if (interactor is MonoBehaviour mb)
            {
                _grabberTransform = mb.transform;
                
                // Para HandGrabInteractor, buscar la mano real
                if (mb is Oculus.Interaction.HandGrab.HandGrabInteractor handGrabInteractor)
                {
                    // La mano está típicamente en el padre
                    _handTransform = mb.transform.parent?.parent;
                    if (_handTransform == null) _handTransform = mb.transform;
                    
                    // Verificar si hay un punto de grab específico
                    var grabPoint = mb.transform.Find("GrabPoint");
                    if (grabPoint != null)
                    {
                        _grabberTransform = grabPoint;
                        _useHandPose = true;
                    }
                }
                
                if (_debugMode)
                {
                    Debug.Log($"[PragmaticNetworkedGrabbable] Grabber: {_grabberTransform.name}");
                    Debug.Log($"[PragmaticNetworkedGrabbable] Hand: {_handTransform?.name ?? "None"}");
                }
                
                return true;
            }
            
            // Intento 2: Reflexión para obtener Transform
            try
            {
                var transformProp = interactor.GetType().GetProperty("Transform");
                if (transformProp != null)
                {
                    var t = transformProp.GetValue(interactor) as Transform;
                    if (t != null)
                    {
                        _grabberTransform = t;
                        _handTransform = t;
                        return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PragmaticNetworkedGrabbable] Reflection failed: {e.Message}");
            }
            
            return false;
        }
        
        private void CalculateGrabOffset()
        {
            // Calcular offset desde el punto de grab, no desde la mano
            if (_grabberTransform != null)
            {
                // Opción 1: Offset simple basado en distancia configurada
                Vector3 direction = (transform.position - _grabberTransform.position).normalized;
                if (direction.magnitude < 0.1f) direction = _grabberTransform.forward;
                
                _localGrabOffset = _grabberTransform.InverseTransformPoint(
                    _grabberTransform.position + direction * _grabOffsetDistance
                );
                
                // Opción 2: Mantener posición relativa actual
                if (_handGrabInteractable != null && _handGrabInteractable.enabled)
                {
                    _localGrabOffset = _grabberTransform.InverseTransformPoint(transform.position);
                }
                
                _rotationOffset = Quaternion.Inverse(_grabberTransform.rotation) * transform.rotation;
                
                if (_debugMode)
                {
                    Debug.Log($"[PragmaticNetworkedGrabbable] Local offset: {_localGrabOffset}");
                    Debug.Log($"[PragmaticNetworkedGrabbable] Rotation offset: {_rotationOffset.eulerAngles}");
                }
            }
        }
        
        private void ConfigurePhysicsForGrab()
        {
            if (_rigidbody != null)
            {
                if (_usePhysicsWhileGrabbed)
                {
                    // Mantener física pero con constraints
                    _rigidbody.isKinematic = false;
                    _rigidbody.useGravity = false;
                    _rigidbody.linearDamping = 10f;
                    _rigidbody.angularDamping = 10f;
                }
                else
                {
                    // Completamente cinemático
                    _rigidbody.isKinematic = true;
                }
            }
        }
        
        private void UpdateGrabPosition(PointerEvent evt)
        {
            // Actualizar el transform si cambia durante el grab
            if (_isLocallyGrabbed && evt.Data is IInteractorView interactor)
            {
                ExtractGrabberTransform(evt);
            }
        }
        
        private async UniTaskVoid RequestInputAuthorityForGrabAsync(PointerEvent evt)
        {
            if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] Requesting input authority for grab...");
            
            // Guardar la posición actual del objeto ANTES de cualquier cambio
            Vector3 originalPosition = transform.position;
            Quaternion originalRotation = transform.rotation;
            
            // Send RPC to request authority transfer
            RPC_RequestAuthorityTransfer(Runner.LocalPlayer);
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                float timeout = 0.5f;
                float elapsed = 0f;
                
                // Verificar el tipo correcto de autoridad según el modo
                bool waitingForAuthority = (Runner.GameMode == GameMode.Shared) ? 
                                          !Object.HasStateAuthority : 
                                          !HasInputAuthority;
                
                // IMPORTANTE: Mantener el objeto en su posición original mientras esperamos autoridad
                while (waitingForAuthority && elapsed < timeout && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Forzar la posición original para evitar que se mueva
                    transform.position = originalPosition;
                    transform.rotation = originalRotation;
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, _cancellationTokenSource.Token);
                    elapsed += Time.deltaTime;
                    
                    // Actualizar verificación
                    waitingForAuthority = (Runner.GameMode == GameMode.Shared) ? 
                                         !Object.HasStateAuthority : 
                                         !HasInputAuthority;
                }
                
                bool hasAuthority = (Runner.GameMode == GameMode.Shared) ? 
                                   Object.HasStateAuthority : 
                                   HasInputAuthority;
                
                if (hasAuthority)
                {
                    if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] Authority acquired! Completing grab.");
                    
                    // IMPORTANTE: Sincronizar la posición ACTUAL antes de empezar a mover
                    // Esto evita que otros clientes vean el objeto en una posición incorrecta
                    SyncedPosition = transform.position;
                    SyncedRotation = transform.rotation;
                    
                    // Ahora sí completar el grab con autoridad confirmada
                    CompleteLocalGrab();
                    SetGrabbedState();
                    
                    // Actualizar nuevamente después de configurar el estado
                    SyncedPosition = transform.position;
                    SyncedRotation = transform.rotation;
                }
                else
                {
                    Debug.LogWarning($"[PragmaticNetworkedGrabbable] Input authority timeout - cancelling grab");
                    // No hacer nada, el objeto permanece donde estaba
                    _grabberTransform = null;
                    _handTransform = null;
                }
            }
            catch (System.OperationCanceledException)
            {
                if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] Authority request cancelled");
                _grabberTransform = null;
                _handTransform = null;
            }
        }
        
        // Método legacy para compatibilidad
     
        
        private void SetGrabbedState()
        {
            IsGrabbed = true;
            GrabbingPlayer = Runner.LocalPlayer;
            HasAuthority = HasInputAuthority;
            
            if (_debugMode)
            {
                Debug.Log($"[PragmaticNetworkedGrabbable] Grabbed by Player {Runner.LocalPlayer}");
            }
        }
        
        private void OnLocalRelease()
        {
            if (!_isLocallyGrabbed) return;
            
            if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] Local release");
            
            _isLocallyGrabbed = false;
            _grabberTransform = null;
            _handTransform = null;
            _useHandPose = false;
            
            // Reactivar NetworkTransform
            if (_networkTransform != null)
            {
                _networkTransform.enabled = true;
                if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] NetworkTransform ENABLED");
            }
            
            // Restaurar layer
            gameObject.layer = _originalLayer;
            
            // Restaurar física
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.useGravity = true;
                _rigidbody.linearDamping = 1f;
                _rigidbody.angularDamping = 1f;
            }
            
            if (HasInputAuthority)
            {
                // Sync final position
                SyncedPosition = transform.position;
                SyncedRotation = transform.rotation;
                
                IsGrabbed = false;
                GrabbingPlayer = PlayerRef.None;
                HasAuthority = false;
            }
        }
        
        private void ForceRelease()
        {
            _isLocallyGrabbed = false;
            _grabberTransform = null;
            _handTransform = null;
            
            if (_networkTransform != null)
            {
                _networkTransform.enabled = true;
            }
            
            gameObject.layer = _originalLayer;
            
            // Los transformers siguen desactivados
        }
        
        void Update()
        {
            // CRÍTICO: Solo mover si está siendo agarrado localmente Y tenemos el transform
            if (_isLocallyGrabbed && _grabberTransform != null)
            {
                UpdateObjectPosition();
            }
            
            // Verificación de seguridad: asegurar que los transformers sigan desactivados
            if (_isLocallyGrabbed && Time.frameCount % 60 == 0)
            {
                DisableConflictingComponents();
            }
        }
        
        private void UpdateObjectPosition()
        {
            // Calcular posición y rotación objetivo
            Vector3 targetPosition = _grabberTransform.TransformPoint(_localGrabOffset);
            Quaternion targetRotation = _grabberTransform.rotation * _rotationOffset;
            
            // Aplicar movimiento según configuración
            if (_usePhysicsWhileGrabbed && _rigidbody != null && !_rigidbody.isKinematic)
            {
                // Movimiento basado en física
                Vector3 velocity = (targetPosition - transform.position) * _positionLerpSpeed;
                _rigidbody.linearVelocity = velocity;
                
                Quaternion deltaRotation = targetRotation * Quaternion.Inverse(transform.rotation);
                deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
                _rigidbody.angularVelocity = axis * angle * _rotationLerpSpeed;
            }
            else
            {
                // Movimiento directo con lerp suave
                transform.position = Vector3.Lerp(
                    transform.position, 
                    targetPosition, 
                    Time.deltaTime * _positionLerpSpeed
                );
                
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    targetRotation, 
                    Time.deltaTime * _rotationLerpSpeed
                );
            }
            
            // Debug
            if (_debugMode && Time.frameCount % 30 == 0)
            {
                float distance = Vector3.Distance(transform.position, targetPosition);
                if (distance > 0.5f)
                {
                    Debug.LogWarning($"[PragmaticNetworkedGrabbable] Large distance to target: {distance:F2}m");
                }
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            // SOLUCIÓN: En modo Shared usar StateAuthority, no InputAuthority
            bool hasAuthority = (Runner.GameMode == GameMode.Shared) ? 
                               Object.HasStateAuthority : 
                               HasInputAuthority;
            
            // Sync position for other players when we have authority
            if (hasAuthority && IsGrabbed)
            {
                SyncedPosition = transform.position;
                SyncedRotation = transform.rotation;
            }
        }
        
        public override void Render()
        {
            // SOLUCIÓN CRÍTICA: Solo interpolar si tenemos una posición válida sincronizada
            // Evitar mover el objeto a (0,0,0) o posiciones incorrectas
            if (!_isLocallyGrabbed && IsGrabbed && GrabbingPlayer != Runner.LocalPlayer)
            {
                // Verificar que la posición sincronizada es válida (no es el origen por defecto)
                // y que no está demasiado lejos (más de 50 metros es sospechoso)
                float distanceToSynced = Vector3.Distance(transform.position, SyncedPosition);
                bool isValidPosition = SyncedPosition != Vector3.zero || transform.position == Vector3.zero;
                bool isReasonableDistance = distanceToSynced < 50f;
                
                if (isValidPosition && isReasonableDistance)
                {
                    float lerpSpeed = Time.deltaTime * 10f;
                    transform.position = Vector3.Lerp(transform.position, SyncedPosition, lerpSpeed);
                    transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, lerpSpeed);
                }
                else if (_debugMode && Time.frameCount % 60 == 0)
                {
                    Debug.LogWarning($"[PragmaticNetworkedGrabbable] Skipping suspicious sync - Distance: {distanceToSynced:F2}m, SyncedPos: {SyncedPosition}");
                }
            }
        }
        
        void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= OnGrabbableEvent;
            }
            
            // Restaurar estados originales de transformers
            foreach (var kvp in _transformerStates)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.enabled = kvp.Value;
                }
            }
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_RequestAuthorityTransfer(PlayerRef player, RpcInfo info = default)
        {
            // IMPORTANTE: Antes de transferir autoridad, sincronizar la posición actual
            // Esto evita que el objeto salte a una posición incorrecta
            if (Object.HasStateAuthority || (Runner.GameMode != GameMode.Shared && Object.HasInputAuthority))
            {
                SyncedPosition = transform.position;
                SyncedRotation = transform.rotation;
                
                if (_debugMode)
                {
                    Debug.Log($"[PragmaticNetworkedGrabbable] Pre-transfer sync: {SyncedPosition}");
                }
            }
            
            // En modo Shared, necesitamos manejar la transferencia de manera diferente
            if (Runner.GameMode == GameMode.Shared)
            {
                // En Shared mode, cualquier cliente puede tener StateAuthority
                // El primero en agarrar obtiene la autoridad
                if (!IsGrabbed || GrabbingPlayer == PlayerRef.None)
                {
                    // Transferir StateAuthority al jugador que solicita
                    if (Object.HasStateAuthority && Object.StateAuthority != info.Source)
                    {
                        // Sincronizar una última vez antes de liberar autoridad
                        SyncedPosition = transform.position;
                        SyncedRotation = transform.rotation;
                        Object.ReleaseStateAuthority();
                    }
                    
                    // El jugador solicitante tomará StateAuthority
                    if (info.Source == Runner.LocalPlayer)
                    {
                        Object.RequestStateAuthority();
                        HasAuthority = true;
                        
                        // Mantener la posición actual
                        SyncedPosition = transform.position;
                        SyncedRotation = transform.rotation;
                        
                        if (_debugMode)
                        {
                            Debug.Log($"[PragmaticNetworkedGrabbable] State authority requested by player {info.Source}");
                        }
                    }
                }
                else if (_debugMode)
                {
                    Debug.LogWarning($"[PragmaticNetworkedGrabbable] Cannot transfer authority - object already grabbed by {GrabbingPlayer}");
                }
            }
            else
            {
                // Para Host/Server mode, usar InputAuthority
                if (Object.HasStateAuthority)
                {
                    Object.AssignInputAuthority(info.Source);
                    HasAuthority = true;
                    if (_debugMode)
                    {
                        Debug.Log($"[PragmaticNetworkedGrabbable] Input authority transferred to player {info.Source}");
                    }
                }
            }
        }
        
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            if (IsGrabbed)
            {
                // Verde = local grab, Amarillo = remoto
                Gizmos.color = _isLocallyGrabbed ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.1f);
                
                if (_grabberTransform != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.position, _grabberTransform.position);
                    
                    // Mostrar posición objetivo
                    Vector3 targetPos = _grabberTransform.TransformPoint(_localGrabOffset);
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(targetPos, Vector3.one * 0.05f);
                }
            }
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Debug: List All Components")]
        private void DebugListComponents()
        {
            var components = GetComponents<Component>();
            foreach (var comp in components)
            {
                Debug.Log($"Component: {comp.GetType().Name} (Enabled: {(comp is MonoBehaviour b ? b.enabled : true)})");
            }
        }
        
        [ContextMenu("Debug: Disable All Transformers")]
        private void DebugDisableTransformers()
        {
            DisableConflictingComponents();
        }
        #endif
    }
}