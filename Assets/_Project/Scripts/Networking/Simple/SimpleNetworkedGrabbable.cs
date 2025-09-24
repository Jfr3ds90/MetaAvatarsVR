using UnityEngine;
using Fusion;
using Oculus.Interaction;

namespace MetaAvatarsVR.Networking.Simple
{
    /// <summary>
    /// VERSIÓN FINAL: Con override correcto de NetworkTransform
    /// El DefaultExecutionOrder asegura que este script se ejecute DESPUÉS de NetworkTransform
    /// permitiendo sobrescribir la interpolación cuando el objeto está agarrado
    /// </summary>
    [DefaultExecutionOrder(1000)] // CRÍTICO: Ejecutar después de NetworkTransform
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Grabbable))]
    public class SimpleNetworkedGrabbableFinal : NetworkBehaviour
    {
        // Constante para orden de ejecución (después de NetworkTransform)
        public const int EXECUTION_ORDER = 100;
        
        [Header("Configuration")]
        [SerializeField] private bool _makeKinematicOnGrab = true;
        [SerializeField] private bool _disableNetworkTransformOnGrab = false;
        [SerializeField] private float _ungrabExtrapolationTime = 0.3f; // 300ms
        
        [Header("Components - Auto assigned")]
        private Grabbable _grabbable;
        private GrabInteractable _grabInteractable;
        private Rigidbody _rigidbody;
        private NetworkTransform _networkTransform;
        
        [Header("Network State")]
        [Networked] public NetworkBool IsGrabbed { get; set; }
        [Networked] public PlayerRef GrabbingPlayer { get; set; }
        [Networked] public Vector3 LocalGrabOffset { get; set; }
        [Networked] public Quaternion LocalGrabRotation { get; set; }
        
        // Referencias locales para el seguimiento
        private Transform _localGrabberTransform;
        private IInteractorView _currentInteractor;
        private bool _isLocallyGrabbed = false;
        
        // Para extrapolación post-ungrab
        private float _ungrabTime = -1f;
        private Vector3 _ungrabPosition;
        private Quaternion _ungrabRotation;
        
        // Cache del transform del interactor
        private Transform _interpolationTarget;
        
        void Awake()
        {
            // Obtener componentes requeridos
            _grabbable = GetComponent<Grabbable>();
            _grabInteractable = GetComponent<GrabInteractable>();
            _rigidbody = GetComponent<Rigidbody>();
            _networkTransform = GetComponent<NetworkTransform>();
            
            // Configuración del NetworkObject
            var networkObject = GetComponent<NetworkObject>();
           
            
            /*// Obtener el InterpolationTarget si existe, si no usar el transform principal
            if (_networkTransform != null)
            {
                _interpolationTarget = _networkTransform.InterpolationTarget != null ? 
                    _networkTransform.InterpolationTarget : transform;
            }
            else
            {*/
                _interpolationTarget = transform;
            //}
            
            // Verificar y configurar Transformer si no existe
            SetupTransformer();
        }
        
        private void SetupTransformer()
        {
            // El Grabbable necesita un Transformer para funcionar correctamente
            var oneGrabTransformer = GetComponent<OneGrabFreeTransformer>();
            if (oneGrabTransformer == null)
            {
                Debug.Log($"[SimpleNetworkedGrabbableFinal] Adding OneGrabFreeTransformer to {gameObject.name}");
                oneGrabTransformer = gameObject.AddComponent<OneGrabFreeTransformer>();
            }
        }
        
        void Start()
        {
            // Suscribirse a eventos del Grabbable
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised += OnGrabbableEvent;
            }
            
            // Si tenemos GrabInteractable, también podemos usar sus eventos
            if (_grabInteractable != null)
            {
                _grabInteractable.WhenSelectingInteractorAdded.Action += OnGrabInteractableSelected;
                _grabInteractable.WhenSelectingInteractorRemoved.Action += OnGrabInteractableUnselected;
            }
        }
        
        public override void Spawned()
        {
            // Inicializar estado networkeado
            if (HasStateAuthority)
            {
                IsGrabbed = false;
                GrabbingPlayer = PlayerRef.None;
                LocalGrabOffset = Vector3.zero;
                LocalGrabRotation = Quaternion.identity;
            }
        }
        
        #region Grab Detection
        
        private void OnGrabbableEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select)
            {
                _currentInteractor = evt.Data as IInteractorView;
                StartGrab();
            }
            else if (evt.Type == PointerEventType.Unselect)
            {
                EndGrab();
                _currentInteractor = null;
            }
        }
        
        private void OnGrabInteractableSelected(IInteractorView interactor)
        {
            _currentInteractor = interactor;
            StartGrab();
        }
        
        private void OnGrabInteractableUnselected(IInteractorView interactor)
        {
            EndGrab();
            _currentInteractor = null;
        }
        
        #endregion
        
        #region Grab Logic
        
        private void StartGrab()
        {
            if (!Runner.IsRunning) return;
            
            // Obtener el transform del interactor (mano/controlador)
            if (_currentInteractor != null)
            {
                _localGrabberTransform = GetInteractorTransform(_currentInteractor);
                
                if (_localGrabberTransform == null)
                {
                    Debug.LogWarning($"[SimpleNetworkedGrabbableFinal] Could not get transform from interactor");
                    return;
                }
                
                _isLocallyGrabbed = true;
                
                // Calcular offsets locales para mantener la posición relativa
                Vector3 localOffset = _localGrabberTransform.InverseTransformPoint(transform.position);
                Quaternion localRotation = Quaternion.Inverse(_localGrabberTransform.rotation) * transform.rotation;
                
                Debug.Log($"[SimpleNetworkedGrabbableFinal] Local grab started on {gameObject.name} by {_localGrabberTransform.name}");
                
                // Solicitar autoridad para este objeto
                if (!HasStateAuthority)
                {
                    RequestAuthority(localOffset, localRotation);
                }
                else
                {
                    // Ya tenemos autoridad, actualizar estado directamente
                    SetGrabbedState(Runner.LocalPlayer, localOffset, localRotation);
                }
            }
        }
        
        private Transform GetInteractorTransform(IInteractorView view)
        {
            // Múltiples formas de obtener el transform
            if (view is MonoBehaviour mb)
            {
                return mb.transform;
            }
            
            // Si tiene propiedad Transform
            if (view != null)
            {
                try
                {
                    var prop = view.GetType().GetProperty("Transform");
                    if (prop != null)
                    {
                        return prop.GetValue(view) as Transform;
                    }
                }
                catch { }
            }
            
            return null;
        }
        
        private async void RequestAuthority(Vector3 localOffset, Quaternion localRotation)
        {
            Debug.Log($"[SimpleNetworkedGrabbableFinal] Requesting authority for {gameObject.name}");
            
            Object.RequestStateAuthority();
            
            // Esperar un poco para obtener autoridad
            float timeout = Time.time + 0.5f;
            while (!HasStateAuthority && Time.time < timeout)
            {
                await System.Threading.Tasks.Task.Yield();
                if (this == null) return; // Objeto destruido
            }
            
            if (HasStateAuthority)
            {
                Debug.Log($"[SimpleNetworkedGrabbableFinal] Authority acquired for {gameObject.name}");
                SetGrabbedState(Runner.LocalPlayer, localOffset, localRotation);
            }
            else
            {
                Debug.LogWarning($"[SimpleNetworkedGrabbableFinal] Failed to acquire authority for {gameObject.name}");
                _isLocallyGrabbed = false;
                _localGrabberTransform = null;
            }
        }
        
        private void SetGrabbedState(PlayerRef player, Vector3 localOffset, Quaternion localRotation)
        {
            IsGrabbed = true;
            GrabbingPlayer = player;
            LocalGrabOffset = localOffset;
            LocalGrabRotation = localRotation;
            
            if (_makeKinematicOnGrab && _rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
            
            Debug.Log($"[SimpleNetworkedGrabbableFinal] {gameObject.name} grabbed by Player {player}");
        }
        
        private void EndGrab()
        {
            if (!_isLocallyGrabbed) return;
            
            // Guardar posición para extrapolación
            _ungrabTime = Time.time;
            _ungrabPosition = transform.position;
            _ungrabRotation = transform.rotation;
            
            _isLocallyGrabbed = false;
            _localGrabberTransform = null;
            
            if (HasStateAuthority)
            {
                RPC_OnReleased();
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnReleased()
        {
            IsGrabbed = false;
            GrabbingPlayer = PlayerRef.None;
            
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }
            
            Debug.Log($"[SimpleNetworkedGrabbableFinal] {gameObject.name} released");
        }
        
        #endregion
        
        #region Position Update - LA PARTE MÁS IMPORTANTE
        
        public override void FixedUpdateNetwork()
        {
            // Actualizar posición en el tick de red si tenemos autoridad
            if (HasStateAuthority && IsGrabbed && _localGrabberTransform != null)
            {
                Vector3 targetPosition = _localGrabberTransform.TransformPoint(LocalGrabOffset);
                Quaternion targetRotation = _localGrabberTransform.rotation * LocalGrabRotation;
                
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
        }
        
        public override void Render()
        {
            // CRÍTICO: Este método se ejecuta DESPUÉS de NetworkTransform.Render()
            // debido a DefaultExecutionOrder(100), permitiendo sobrescribir la interpolación
            
            if (IsGrabbed)
            {
                // Caso 1: Jugador local agarrando - override completo
                if (_isLocallyGrabbed && _localGrabberTransform != null)
                {
                    Vector3 targetPosition = _localGrabberTransform.TransformPoint(LocalGrabOffset);
                    Quaternion targetRotation = _localGrabberTransform.rotation * LocalGrabRotation;
                    
                    // Override tanto el transform principal como el InterpolationTarget
                    transform.position = targetPosition;
                    transform.rotation = targetRotation;
                    
                    if (_interpolationTarget != null && _interpolationTarget != transform)
                    {
                        _interpolationTarget.position = targetPosition;
                        _interpolationTarget.rotation = targetRotation;
                    }
                }
                // Caso 2: Otro jugador agarrando - confiar en NetworkTransform
                else
                {
                    // NetworkTransform ya manejó esto, pero podemos añadir smoothing adicional si queremos
                }
            }
            // Caso 3: Recién soltado - extrapolación temporal
            else if (_ungrabTime > 0 && (Time.time - _ungrabTime) < _ungrabExtrapolationTime)
            {
                // Mantener la última posición conocida para evitar snap-back
                transform.position = _ungrabPosition;
                transform.rotation = _ungrabRotation;
                
                if (_interpolationTarget != null && _interpolationTarget != transform)
                {
                    _interpolationTarget.position = _ungrabPosition;
                    _interpolationTarget.rotation = _ungrabRotation;
                }
            }
            else
            {
                // Reset ungrab time
                _ungrabTime = -1f;
            }
        }
        
        #endregion
        
        #region Alternative Approach - Disable NetworkTransform
        
        public void SetNetworkTransformEnabled(bool enabled)
        {
            if (_networkTransform != null)
            {
                _networkTransform.enabled = enabled;
            }
        }
        
        // Método alternativo: desactivar NetworkTransform durante el grab
        private void OnGrabStateChanged(bool grabbed)
        {
            if (_disableNetworkTransformOnGrab && _networkTransform != null)
            {
                _networkTransform.enabled = !grabbed;
            }
        }
        
        #endregion
        
        #region Cleanup
        
        void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= OnGrabbableEvent;
            }
            
            if (_grabInteractable != null)
            {
                _grabInteractable.WhenSelectingInteractorAdded.Action -= OnGrabInteractableSelected;
                _grabInteractable.WhenSelectingInteractorRemoved.Action -= OnGrabInteractableUnselected;
            }
        }
        
        #endregion
        
        #region Debug
        
        void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && IsGrabbed)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.1f);
                
                if (_localGrabberTransform != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(transform.position, _localGrabberTransform.position);
                }
            }
        }
        
        #endregion
    }
}