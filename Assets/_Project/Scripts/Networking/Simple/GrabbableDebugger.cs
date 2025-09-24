using UnityEngine;
using Fusion;
using MetaAvatarsVR.Networking.Pragmatic;
using Oculus.Interaction;

namespace MetaAvatarsVR.Networking
{
    /// <summary>
    /// Script de diagnóstico para identificar por qué el objeto no se mueve
    /// Añadir este script JUNTO con PragmaticNetworkedGrabbable
    /// </summary>
    [RequireComponent(typeof(PragmaticNetworkedGrabbable))]
    public class GrabbableDebugger : MonoBehaviour
    {
        private PragmaticNetworkedGrabbable _grabbable;
        private Grabbable _metaGrabbable;
        private NetworkTransform _networkTransform;
        private Transform _lastTransform;
        private Vector3 _lastPosition;
        private bool _isMonitoring = false;
        
        [Header("Debug Info - Runtime")]
        [SerializeField] private bool _isGrabbed = false;
        [SerializeField] private bool _networkTransformEnabled = true;
        [SerializeField] private string _grabberName = "None";
        [SerializeField] private float _distanceToGrabber = 0f;
        [SerializeField] private Vector3 _currentPosition;
        [SerializeField] private Vector3 _targetPosition;
        [SerializeField] private bool _isMoving = false;
        
        void Start()
        {
            _grabbable = GetComponent<PragmaticNetworkedGrabbable>();
            _metaGrabbable = GetComponent<Grabbable>();
            _networkTransform = GetComponent<NetworkTransform>();
            
            // Suscribirse a eventos de Meta Grabbable
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised += OnMetaGrabbableEvent;
            }
        }
        
        private void OnMetaGrabbableEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select)
            {
                Debug.Log("===== GRAB DIAGNOSTIC START =====");
                Debug.Log($"[GrabbableDebugger] Meta Grabbable SELECT event fired");
                
                var interactor = evt.Data as IInteractorView;
                if (interactor != null)
                {
                    Debug.Log($"[GrabbableDebugger] Interactor type: {interactor.GetType()}");
                    
                    if (interactor is MonoBehaviour mb)
                    {
                        _lastTransform = mb.transform;
                        Debug.Log($"[GrabbableDebugger] Grabber transform acquired: {mb.transform.name}");
                        Debug.Log($"[GrabbableDebugger] Grabber position: {mb.transform.position}");
                        Debug.Log($"[GrabbableDebugger] Object position: {transform.position}");
                        
                        // Verificar jerarquía
                        Transform parent = mb.transform.parent;
                        string hierarchy = mb.transform.name;
                        while (parent != null)
                        {
                            hierarchy = parent.name + "/" + hierarchy;
                            parent = parent.parent;
                        }
                        Debug.Log($"[GrabbableDebugger] Grabber hierarchy: {hierarchy}");
                    }
                    else
                    {
                        Debug.LogWarning($"[GrabbableDebugger] Could not cast interactor to MonoBehaviour");
                        
                        // Intentar obtener Transform de otra manera
                        var transformProp = interactor.GetType().GetProperty("Transform");
                        if (transformProp != null)
                        {
                            var t = transformProp.GetValue(interactor) as Transform;
                            if (t != null)
                            {
                                _lastTransform = t;
                                Debug.Log($"[GrabbableDebugger] Got transform via reflection: {t.name}");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[GrabbableDebugger] Interactor is NULL!");
                }
                
                _isMonitoring = true;
                _lastPosition = transform.position;
                Debug.Log("===== GRAB DIAGNOSTIC END =====");
            }
            else if (evt.Type == PointerEventType.Unselect)
            {
                Debug.Log($"[GrabbableDebugger] Meta Grabbable UNSELECT event fired");
                _isMonitoring = false;
                _lastTransform = null;
            }
        }
        
        void Update()
        {
            // Actualizar información de debug
            _isGrabbed = _grabbable != null && _grabbable.IsGrabbed;
            _networkTransformEnabled = _networkTransform != null && _networkTransform.enabled;
            _currentPosition = transform.position;
            
            if (_lastTransform != null)
            {
                _grabberName = _lastTransform.name;
                _distanceToGrabber = Vector3.Distance(transform.position, _lastTransform.position);
                _targetPosition = _lastTransform.position; // Simplificado para debug
            }
            else
            {
                _grabberName = "NULL";
                _distanceToGrabber = 0f;
            }
            
            // Detectar si el objeto se está moviendo
            _isMoving = Vector3.Distance(_currentPosition, _lastPosition) > 0.001f;
            
            // Monitorear durante el grab
            if (_isMonitoring)
            {
                if (Time.frameCount % 30 == 0) // Log cada 30 frames
                {
                    Debug.Log($"[GrabbableDebugger] MONITOR: " +
                        $"Pos={_currentPosition}, " +
                        $"Moving={_isMoving}, " +
                        $"NT_Enabled={_networkTransformEnabled}, " +
                        $"Grabber={_grabberName}, " +
                        $"Distance={_distanceToGrabber:F2}");
                    
                    if (_lastTransform != null && !_isMoving)
                    {
                        Debug.LogWarning($"[GrabbableDebugger] NOT MOVING! Grabber at {_lastTransform.position}, Object at {transform.position}");
                        
                        // Verificar componentes
                        var rb = GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            Debug.Log($"[GrabbableDebugger] Rigidbody: isKinematic={rb.isKinematic}, constraints={rb.constraints}");
                        }
                        
                        // Verificar si algo más está moviendo el objeto
                        var allComponents = GetComponents<Component>();
                        foreach (var comp in allComponents)
                        {
                            if (comp is MonoBehaviour mb && mb.enabled && mb != this && mb != _grabbable)
                            {
                                Debug.Log($"[GrabbableDebugger] Active component: {comp.GetType().Name}");
                            }
                        }
                    }
                }
            }
            
            _lastPosition = _currentPosition;
        }
        
        void OnDestroy()
        {
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised -= OnMetaGrabbableEvent;
            }
        }
        
        // Método para forzar el movimiento (para testing)
        [ContextMenu("Force Move to Grabber")]
        public void ForceMoveToGrabber()
        {
            if (_lastTransform != null)
            {
                transform.position = _lastTransform.position;
                Debug.Log($"[GrabbableDebugger] FORCED move to {_lastTransform.position}");
            }
            else
            {
                Debug.LogError($"[GrabbableDebugger] Cannot force move - no grabber transform!");
            }
        }
    }
}