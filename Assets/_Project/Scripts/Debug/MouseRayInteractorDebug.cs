using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using System.Collections.Generic;

namespace HackMonkeys.UI.Spatial.DebugRay
{
    /// <summary>
    /// Sistema de debug que simula un RayInteractor usando el mouse
    /// Permite probar la UI 3D sin necesidad de usar las gafas VR
    /// </summary>
    [RequireComponent(typeof(RayInteractor))]
    public class MouseRayInteractorDebug : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableMouseControl = true;
        [SerializeField] private Camera debugCamera;
        [SerializeField] private float rayDistance = 10f;
        [SerializeField] internal KeyCode toggleKey = KeyCode.F1; // Tecla para activar/desactivar
        
        [Header("Visual Debug")]
        [SerializeField] private bool showDebugRay = true;
        [SerializeField] private LineRenderer debugLineRenderer;
        [SerializeField] private GameObject debugReticle;
        [SerializeField] private Color rayColor = Color.green;
        [SerializeField] private Color rayHoverColor = Color.yellow;
        
        [Header("Mouse Settings")]
        [SerializeField] private float mouseSensitivity = 1f;
        [SerializeField] private bool lockCursor = false;
        [SerializeField] private bool simulateHaptics = true; // Mostrar logs cuando habría haptics
        
        private RayInteractor _rayInteractor;
        private Transform _rayOrigin;
        private bool _isActive = true;
        private Ray _currentRay;
        
        // Estado del mouse
        private bool _isMousePressed = false;
        private bool _wasMousePressed = false;
        
        // Para simular el estado del interactor
        private InteractorState _simulatedState = InteractorState.Normal;
        
        private void Awake()
        {
            SetupComponents();
            CreateDebugVisuals();
        }
        
        private void SetupComponents()
        {
            // Obtener o crear RayInteractor
            _rayInteractor = GetComponent<RayInteractor>();
            if (_rayInteractor == null)
            {
                _rayInteractor = gameObject.AddComponent<RayInteractor>();
            }
            
            // Configurar el origen del rayo
            _rayOrigin = transform;
            
            // Obtener cámara de debug si no se especificó
            if (debugCamera == null)
            {
                debugCamera = Camera.main;
                if (debugCamera == null)
                {
                    // Crear una cámara de debug
                    GameObject camObj = new GameObject("Debug Camera");
                    debugCamera = camObj.AddComponent<Camera>();
                    debugCamera.transform.position = Vector3.up * 1.6f - Vector3.forward * 2f;
                    debugCamera.transform.rotation = Quaternion.identity;
                }
            }
            
            // Asegurarse de que el RayInteractor esté configurado correctamente
            _rayInteractor.MaxRayLength = rayDistance;
        }
        
        private void CreateDebugVisuals()
        {
            // Crear LineRenderer para visualizar el rayo
            if (debugLineRenderer == null && showDebugRay)
            {
                GameObject lineObj = new GameObject("Debug Ray Visual");
                lineObj.transform.SetParent(transform);
                debugLineRenderer = lineObj.AddComponent<LineRenderer>();
                debugLineRenderer.startWidth = 0.01f;
                debugLineRenderer.endWidth = 0.005f;
                debugLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                debugLineRenderer.startColor = rayColor;
                debugLineRenderer.endColor = rayColor;
                debugLineRenderer.positionCount = 2;
            }
            
            // Crear reticle de debug
            if (debugReticle == null && showDebugRay)
            {
                debugReticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                debugReticle.name = "Debug Reticle";
                debugReticle.transform.localScale = Vector3.one * 0.03f;
                debugReticle.GetComponent<Renderer>().material.color = rayColor;
                Destroy(debugReticle.GetComponent<Collider>());
                debugReticle.SetActive(false);
            }
        }
        
        private void OnEnable()
        {
            Debug.Log($"[MouseRayDebug] Activado - Presiona {toggleKey} para toggle");
            
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
        
        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            if (debugReticle != null)
            {
                debugReticle.SetActive(false);
            }
        }
        
        private void Update()
        {
            // Toggle con tecla
            if (Input.GetKeyDown(toggleKey))
            {
                enableMouseControl = !enableMouseControl;
                Debug.Log($"[MouseRayDebug] Mouse control: {(enableMouseControl ? "ACTIVADO" : "DESACTIVADO")}");
                
                if (!enableMouseControl && debugReticle != null)
                {
                    debugReticle.SetActive(false);
                }
            }
            
            if (!enableMouseControl || !_isActive) return;
            
            UpdateMouseRay();
            UpdateInteractorState();
            UpdateDebugVisuals();
        }
        
        private void UpdateMouseRay()
        {
            // Crear rayo desde la posición del mouse
            _currentRay = debugCamera.ScreenPointToRay(Input.mousePosition);
            
            // Actualizar la posición y dirección del transform para que el RayInteractor funcione
            transform.position = _currentRay.origin;
            transform.rotation = Quaternion.LookRotation(_currentRay.direction);
        }
        
        private void UpdateInteractorState()
        {
            // Guardar estado anterior
            _wasMousePressed = _isMousePressed;
            _isMousePressed = Input.GetMouseButton(0);
            
            // Simular el estado del interactor basado en el mouse
            if (_isMousePressed)
            {
                _simulatedState = InteractorState.Select;
            }
            else
            {
                _simulatedState = InteractorState.Normal;
            }
            
            // Detectar click para debug
            if (_isMousePressed && !_wasMousePressed)
            {
                OnMouseClickStart();
            }
            else if (!_isMousePressed && _wasMousePressed)
            {
                OnMouseClickEnd();
            }
            
            // Forzar actualización del RayInteractor
            // Nota: Esta parte puede requerir reflection o una extensión del RayInteractor
            // ya que el State es readonly. Por ahora, usaremos logs para debug.
        }
        
        private void OnMouseClickStart()
        {
            Debug.Log("[MouseRayDebug] Click iniciado");
            
            // Verificar si hay un interactable bajo el cursor
            if (Physics.Raycast(_currentRay, out RaycastHit hit, rayDistance))
            {
                var interactable = hit.collider.GetComponent<RayInteractable>();
                if (interactable != null)
                {
                    Debug.Log($"[MouseRayDebug] Interactable encontrado: {interactable.name}");
                    
                    // Buscar componentes específicos
                    var button = interactable.GetComponentInParent<InteractableButton3D>();
                    if (button != null)
                    {
                        Debug.Log($"[MouseRayDebug] Botón clickeado: {button.name}");
                        button.OnSelectStart();
                        
                        if (simulateHaptics)
                        {
                            Debug.Log("[MouseRayDebug] 🎮 Simulando haptic feedback (0.3f)");
                        }
                    }
                    
                    var slider = interactable.GetComponentInParent<InteractableSlider3D>();
                    if (slider != null)
                    {
                        Debug.Log($"[MouseRayDebug] Slider clickeado: {slider.name}");
                    }
                    
                    var inputField = interactable.GetComponentInParent<InteractableInputField3D>();
                    if (inputField != null)
                    {
                        Debug.Log($"[MouseRayDebug] Input field clickeado: {inputField.name}");
                        inputField.Focus();
                    }
                }
            }
        }
        
        private void OnMouseClickEnd()
        {
            Debug.Log("[MouseRayDebug] Click terminado");
            
            // Notificar a los botones que se soltó el click
            var allButtons = FindObjectsOfType<InteractableButton3D>();
            foreach (var button in allButtons)
            {
                // Aquí podrías implementar lógica más específica si es necesario
            }
        }
        
        private void UpdateDebugVisuals()
        {
            if (!showDebugRay) return;
            
            // Actualizar LineRenderer
            if (debugLineRenderer != null)
            {
                Vector3 startPos = _currentRay.origin;
                Vector3 endPos = _currentRay.origin + _currentRay.direction * rayDistance;
                
                // Verificar si hay hit
                bool hasHit = false;
                if (Physics.Raycast(_currentRay, out RaycastHit hit, rayDistance))
                {
                    endPos = hit.point;
                    hasHit = true;
                    
                    // Verificar si es un interactable
                    var interactable = hit.collider.GetComponent<RayInteractable>();
                    if (interactable != null)
                    {
                        debugLineRenderer.startColor = rayHoverColor;
                        debugLineRenderer.endColor = rayHoverColor;
                        
                        // Mostrar reticle
                        if (debugReticle != null)
                        {
                            debugReticle.SetActive(true);
                            debugReticle.transform.position = hit.point;
                            debugReticle.GetComponent<Renderer>().material.color = rayHoverColor;
                        }
                    }
                    else
                    {
                        debugLineRenderer.startColor = rayColor;
                        debugLineRenderer.endColor = rayColor;
                        
                        if (debugReticle != null)
                        {
                            debugReticle.SetActive(false);
                        }
                    }
                }
                else
                {
                    debugLineRenderer.startColor = rayColor;
                    debugLineRenderer.endColor = rayColor;
                    
                    if (debugReticle != null)
                    {
                        debugReticle.SetActive(false);
                    }
                }
                
                debugLineRenderer.SetPosition(0, startPos);
                debugLineRenderer.SetPosition(1, endPos);
            }
        }
        
        /// <summary>
        /// Permite activar/desactivar el sistema de debug desde código
        /// </summary>
        public void SetActive(bool active)
        {
            _isActive = active;
            enableMouseControl = active;
            
            if (!active && debugReticle != null)
            {
                debugReticle.SetActive(false);
            }
        }
        
        /// <summary>
        /// Simula un click en la posición actual del mouse
        /// </summary>
        public void SimulateClick()
        {
            OnMouseClickStart();
            Invoke(nameof(OnMouseClickEnd), 0.1f);
        }
        
        #region Gizmos
        
        private void OnDrawGizmos()
        {
            if (!enableMouseControl || !Application.isPlaying) return;
            
            // Dibujar el rayo en la vista de escena
            Gizmos.color = _isMousePressed ? Color.red : Color.green;
            Gizmos.DrawRay(_currentRay.origin, _currentRay.direction * rayDistance);
            
            // Dibujar esfera en el origen
            Gizmos.DrawWireSphere(_currentRay.origin, 0.1f);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Editor helper para configurar rápidamente el debug
    /// </summary>
    #if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(MouseRayInteractorDebug))]
    public class MouseRayInteractorDebugEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            MouseRayInteractorDebug debug = (MouseRayInteractorDebug)target;
            
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.HelpBox(
                "Este sistema permite probar la UI 3D usando el mouse.\n" +
                $"• Presiona {debug.toggleKey} para activar/desactivar\n" +
                "• Click izquierdo para interactuar\n" +
                "• El rayo se origina desde la cámara de debug",
                UnityEditor.MessageType.Info
            );
            
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Simular Click"))
                {
                    debug.SimulateClick();
                }
            }
        }
    }
    #endif
}