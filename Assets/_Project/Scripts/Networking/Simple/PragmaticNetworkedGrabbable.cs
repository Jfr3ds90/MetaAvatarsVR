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
            if (HasStateAuthority)
            {
                IsGrabbed = false;
                GrabbingPlayer = PlayerRef.None;
                SyncedPosition = transform.position;
                SyncedRotation = transform.rotation;
                HasAuthority = true;
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
            
            // Solicitar autoridad
            if (!HasStateAuthority)
            {
                RequestAuthorityAsync().Forget();
            }
            else
            {
                SetGrabbedState();
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
        
        private async UniTaskVoid RequestAuthorityAsync()
        {
            if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] Requesting authority...");
            
            Object.RequestStateAuthority();
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                float timeout = 0.5f;
                float elapsed = 0f;
                
                while (!HasStateAuthority && elapsed < timeout && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _cancellationTokenSource.Token);
                    elapsed += Time.deltaTime;
                }
                
                if (HasStateAuthority)
                {
                    if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] Authority acquired!");
                    SetGrabbedState();
                }
                else
                {
                    Debug.LogWarning($"[PragmaticNetworkedGrabbable] Authority timeout - forcing release");
                    ForceRelease();
                }
            }
            catch (System.OperationCanceledException)
            {
                if (_debugMode) Debug.Log($"[PragmaticNetworkedGrabbable] Authority request cancelled");
            }
        }
        
        private void SetGrabbedState()
        {
            IsGrabbed = true;
            GrabbingPlayer = Runner.LocalPlayer;
            HasAuthority = true;
            
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
            
            if (HasStateAuthority)
            {
                // Sincronizar posición final
                SyncedPosition = transform.position;
                SyncedRotation = transform.rotation;
                
                IsGrabbed = false;
                GrabbingPlayer = PlayerRef.None;
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
            // Sincronizar posición para otros jugadores
            if (HasStateAuthority && IsGrabbed)
            {
                SyncedPosition = transform.position;
                SyncedRotation = transform.rotation;
            }
        }
        
        public override void Render()
        {
            // Interpolar para otros jugadores
            if (!_isLocallyGrabbed && IsGrabbed && GrabbingPlayer != Runner.LocalPlayer)
            {
                float lerpSpeed = Time.deltaTime * 10f;
                transform.position = Vector3.Lerp(transform.position, SyncedPosition, lerpSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, lerpSpeed);
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