using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Cysharp.Threading.Tasks;
using System.Threading;
using Fusion.Addons.Physics;

namespace MetaAvatarsVR.Networking
{
    /// <summary>
    /// Implementación basada en los samples oficiales de Photon VR Host/Shared
    /// Modernizada con UniTask para mejor performance en Quest
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class NetworkGrabbableObject : NetworkBehaviour
    {
        [Header("Grab Configuration")]
        [SerializeField] private bool _smoothGrabbing = true;
        [SerializeField] private float _smoothSpeed = 15f;
        [SerializeField] private float _authorityTimeout = 500f; // milliseconds
        
        [Header("Components")]
        private Grabbable _grabbable;
        private NetworkTransform _networkTransform;
        private NetworkRigidbody3D _networkRigidbody;
        private Rigidbody _rigidbody;
        
        // Transform visual (hijo) que se mueve localmente
        private Transform _visualTransform;
        
        [Header("Network State")]
        [Networked] public NetworkBool IsGrabbed { get; set; }
        [Networked] public PlayerRef GrabbingPlayer { get; set; }
        [Networked] public NetworkBehaviourId GrabberId { get; set; }
        [Networked] public Vector3 LocalPositionOffset { get; set; }
        [Networked] public Quaternion LocalRotationOffset { get; set; }
        
        // Estado local
        private bool _isBeingGrabbedLocally = false;
        private Transform _localGrabberTransform;
        private float _ungrabTime = -1f;
        private Vector3 _ungrabPosition;
        private Quaternion _ungrabRotation;
        
        // Para el sistema de autoridad con UniTask
        private bool _waitingForAuthority = false;
        private CancellationTokenSource _authorityCts;
        
        void Awake()
        {
            // Obtener componentes
            _grabbable = GetComponent<Grabbable>();
            _networkTransform = GetComponent<NetworkTransform>();
            _networkRigidbody = GetComponent<NetworkRigidbody3D>();
            _rigidbody = GetComponent<Rigidbody>();
            
            // IMPORTANTE: Configurar visual transform correctamente
            SetupVisualTransform();
            
            // Configurar NetworkObject
            var networkObject = GetComponent<NetworkObject>();
         
            // Configurar el Transformer para Meta XR
            EnsureTransformer();
        }
        
        private void SetupVisualTransform()
        {
            // NetworkRigidbody3D SÍ tiene InterpolationTarget
            if (_networkRigidbody != null && _networkRigidbody.InterpolationTarget != null)
            {
                _visualTransform = _networkRigidbody.InterpolationTarget;
                Debug.Log($"[NetworkGrabbable] Using NetworkRigidbody3D InterpolationTarget: {_visualTransform.name}");
            }
            // NetworkTransform NO tiene InterpolationTarget en Fusion 2
            else if (_networkTransform != null)
            {
                // Para NetworkTransform, crear un hijo visual si no existe
                Transform visualChild = transform.Find("Visual");
                if (visualChild == null)
                {
                    GameObject visualGO = new GameObject("Visual");
                    visualGO.transform.SetParent(transform);
                    visualGO.transform.localPosition = Vector3.zero;
                    visualGO.transform.localRotation = Quaternion.identity;
                    
                    // Mover todos los MeshRenderers al visual
                    MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
                    foreach (var renderer in renderers)
                    {
                        if (renderer.transform != transform)
                        {
                            renderer.transform.SetParent(visualGO.transform, true);
                        }
                    }
                    
                    visualChild = visualGO.transform;
                }
                
                _visualTransform = visualChild;
                Debug.Log($"[NetworkGrabbable] Using visual child for NetworkTransform: {_visualTransform.name}");
            }
            else
            {
                // Fallback: usar el transform principal
                _visualTransform = transform;
                Debug.LogWarning("[NetworkGrabbable] No network component found, using main transform");
            }
        }
        
        private void EnsureTransformer()
        {
            var transformer = GetComponent<OneGrabFreeTransformer>();
            if (transformer == null)
            {
                transformer = gameObject.AddComponent<OneGrabFreeTransformer>();
            }
        }
        
        void Start()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised += OnGrabbableEvent;
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                IsGrabbed = false;
                GrabbingPlayer = PlayerRef.None;
            }
        }
        
        private void OnGrabbableEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select)
            {
                // Llamar versión async sin await (fire-and-forget)
                OnLocalGrabAsync(evt).Forget();
            }
            else if (evt.Type == PointerEventType.Unselect)
            {
                OnLocalRelease();
            }
        }
        
        private async UniTaskVoid OnLocalGrabAsync(PointerEvent evt)
        {
            if (!Runner.IsRunning) return;
            
            // Obtener el transform del grabber
            IInteractorView interactor = evt.Data as IInteractorView;
            if (interactor == null)
            {
                Debug.LogWarning("[NetworkGrabbable] Could not cast evt.Data to IInteractorView");
                return;
            }
            
            // Obtener el transform del interactor
            if (interactor is MonoBehaviour mb)
            {
                _localGrabberTransform = mb.transform;
            }
            else
            {
                Debug.LogWarning("[NetworkGrabbable] Could not get transform from interactor");
                return;
            }
            
            _isBeingGrabbedLocally = true;
            
            // Calcular offsets
            Vector3 offset = _localGrabberTransform.InverseTransformPoint(transform.position);
            Quaternion rotOffset = Quaternion.Inverse(_localGrabberTransform.rotation) * transform.rotation;
            
            Debug.Log($"[NetworkGrabbable] Local grab started - Requesting authority");
            
            // Solicitar autoridad y esperar
            if (!HasStateAuthority)
            {
                _waitingForAuthority = true;
                Object.RequestStateAuthority();
                
                // Cancelar task anterior si existe
                _authorityCts?.Cancel();
                _authorityCts = new CancellationTokenSource();
                
                // Esperar autoridad con timeout
                bool authorityAcquired = await WaitForAuthorityAsync(offset, rotOffset, _authorityCts.Token);
                
                if (!authorityAcquired && _isBeingGrabbedLocally)
                {
                    // Falló la adquisición de autoridad
                    Debug.LogWarning($"[NetworkGrabbable] Failed to acquire authority - releasing grab");
                    _isBeingGrabbedLocally = false;
                    _localGrabberTransform = null;
                }
            }
            else
            {
                SetGrabbedState(offset, rotOffset);
            }
        }
        
        private async UniTask<bool> WaitForAuthorityAsync(Vector3 offset, Quaternion rotOffset, CancellationToken cancellationToken)
        {
            try
            {
                // Esperar hasta que tengamos autoridad o timeout
                float startTime = Time.time;
                
                while (!HasStateAuthority && (Time.time - startTime) < (_authorityTimeout / 1000f))
                {
                    // Esperar un frame
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    
                    // Verificar si todavía estamos esperando
                    if (!_waitingForAuthority || !_isBeingGrabbedLocally)
                    {
                        Debug.Log("[NetworkGrabbable] Authority wait cancelled - grab released");
                        return false;
                    }
                }
                
                _waitingForAuthority = false;
                
                if (HasStateAuthority)
                {
                    Debug.Log($"[NetworkGrabbable] Authority acquired in {(Time.time - startTime) * 1000f:F0}ms");
                    SetGrabbedState(offset, rotOffset);
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[NetworkGrabbable] Authority timeout after {_authorityTimeout}ms");
                    return false;
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[NetworkGrabbable] Authority wait cancelled");
                _waitingForAuthority = false;
                return false;
            }
        }
        
        private void SetGrabbedState(Vector3 offset, Quaternion rotOffset)
        {
            IsGrabbed = true;
            GrabbingPlayer = Runner.LocalPlayer;
            LocalPositionOffset = offset;
            LocalRotationOffset = rotOffset;
            
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
            
            Debug.Log($"[NetworkGrabbable] Grab state set for player {Runner.LocalPlayer}");
        }
        
        private void OnLocalRelease()
        {
            if (!_isBeingGrabbedLocally) return;
            
            Debug.Log($"[NetworkGrabbable] Local release");
            
            // Cancelar cualquier task de autoridad pendiente
            _authorityCts?.Cancel();
            _authorityCts = null;
            
            // Guardar posición para extrapolación
            _ungrabTime = Time.time;
            _ungrabPosition = transform.position;
            _ungrabRotation = transform.rotation;
            
            _isBeingGrabbedLocally = false;
            _localGrabberTransform = null;
            _waitingForAuthority = false;
            
            if (HasStateAuthority)
            {
                IsGrabbed = false;
                GrabbingPlayer = PlayerRef.None;
                
                if (_rigidbody != null)
                {
                    _rigidbody.isKinematic = false;
                }
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            // Solo actualizar si tenemos autoridad y está agarrado
            if (!HasStateAuthority || !IsGrabbed) return;
            
            // Para el jugador local, actualizar la posición networkeada
            if (_isBeingGrabbedLocally && _localGrabberTransform != null)
            {
                Vector3 targetPos = _localGrabberTransform.TransformPoint(LocalPositionOffset);
                Quaternion targetRot = _localGrabberTransform.rotation * LocalRotationOffset;
                
                // Actualizar el transform principal (networkeado)
                transform.position = targetPos;
                transform.rotation = targetRot;
            }
        }
        
        public override void Render()
        {
            // EXTRAPOLACIÓN: Override la interpolación de NetworkTransform
            
            if (IsGrabbed)
            {
                if (_isBeingGrabbedLocally && _localGrabberTransform != null)
                {
                    // JUGADOR LOCAL: Extrapolación directa
                    Vector3 targetPos = _localGrabberTransform.TransformPoint(LocalPositionOffset);
                    Quaternion targetRot = _localGrabberTransform.rotation * LocalRotationOffset;
                    
                    // Mover el visual directamente (sin afectar el transform principal)
                    if (_visualTransform != null && _visualTransform != transform)
                    {
                        if (_smoothGrabbing)
                        {
                            _visualTransform.position = Vector3.Lerp(_visualTransform.position, targetPos, Time.deltaTime * _smoothSpeed);
                            _visualTransform.rotation = Quaternion.Slerp(_visualTransform.rotation, targetRot, Time.deltaTime * _smoothSpeed);
                        }
                        else
                        {
                            _visualTransform.position = targetPos;
                            _visualTransform.rotation = targetRot;
                        }
                    }
                    
                    // También actualizar el transform principal para que NetworkTransform lo sincronice
                    transform.position = targetPos;
                    transform.rotation = targetRot;
                }
                else if (_waitingForAuthority && _localGrabberTransform != null)
                {
                    // ESPERANDO AUTORIDAD: Mover visualmente mientras esperamos
                    Vector3 targetPos = _localGrabberTransform.TransformPoint(LocalPositionOffset);
                    Quaternion targetRot = _localGrabberTransform.rotation * LocalRotationOffset;
                    
                    if (_visualTransform != null)
                    {
                        _visualTransform.position = targetPos;
                        _visualTransform.rotation = targetRot;
                    }
                }
            }
            else if (_ungrabTime > 0 && (Time.time - _ungrabTime) < 0.3f)
            {
                // POST-UNGRAB: Mantener posición por 300ms para evitar snap-back
                if (_visualTransform != null && _visualTransform != transform)
                {
                    _visualTransform.position = _ungrabPosition;
                    _visualTransform.rotation = _ungrabRotation;
                }
            }
            else
            {
                _ungrabTime = -1f;
            }
        }
        
        void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= OnGrabbableEvent;
            }
            
            // Limpiar cancellation token
            _authorityCts?.Cancel();
            _authorityCts?.Dispose();
        }
        
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            if (IsGrabbed)
            {
                Gizmos.color = _isBeingGrabbedLocally ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.15f);
                
                if (_localGrabberTransform != null)
                {
                    Gizmos.DrawLine(transform.position, _localGrabberTransform.position);
                }
            }
        }
    }
}