using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;

namespace HackMonkeys.UI.Spatial.DebugRay
{
    /// <summary>
    /// Sistema de debug mejorado que simula un RayInteractor usando el mouse
    /// Permite probar la UI 3D sin necesidad de usar las gafas VR
    /// </summary>
    [RequireComponent(typeof(RayInteractor))]
    public class MouseRayInteractorDebug : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableMouseControl = true;
        [SerializeField] private Camera debugCamera;
        [SerializeField] private float rayDistance = 10f;
        [SerializeField] internal KeyCode toggleKey = KeyCode.F1;
        
        [Header("Visual Debug")]
        [SerializeField] private bool showDebugRay = true;
        [SerializeField] private LineRenderer debugLineRenderer;
        [SerializeField] private GameObject debugReticle;
        [SerializeField] private Color rayColor = Color.green;
        [SerializeField] private Color rayHoverColor = Color.yellow;
        [SerializeField] private Color rayClickColor = Color.red;
        
        [Header("Mouse Settings")]
        [SerializeField] private float mouseSensitivity = 1f;
        [SerializeField] private bool lockCursor = false;
        [SerializeField] private bool simulateHaptics = true;
        
        private RayInteractor _rayInteractor;
        private Transform _rayOrigin;
        private bool _isActive = true;
        private Ray _currentRay;
        
        // Estado del mouse
        private bool _isMousePressed = false;
        private bool _wasMousePressed = false;
        
        // Interactables tracking
        private RayInteractable _currentHoveredInteractable;
        private InteractableButton3D _currentHoveredButton;
        private InteractableButton3D _pressedButton;
        private InteractableInputField3D _currentHoveredInputField;
        
        // Para tracking del estado de interacción
        private Dictionary<RayInteractable, InteractorState> _interactableStates = new Dictionary<RayInteractable, InteractorState>();
        
        private void Awake()
        {
            SetupComponents();
            CreateDebugVisuals();
        }
        
        private void SetupComponents()
        {
            _rayInteractor = GetComponent<RayInteractor>();
            if (_rayInteractor == null)
            {
                _rayInteractor = gameObject.AddComponent<RayInteractor>();
            }
            
            _rayOrigin = transform;
            
            if (debugCamera == null)
            {
                debugCamera = Camera.main;
                if (debugCamera == null)
                {
                    GameObject camObj = new GameObject("Debug Camera");
                    debugCamera = camObj.AddComponent<Camera>();
                    debugCamera.transform.position = Vector3.up * 1.6f - Vector3.forward * 2f;
                    debugCamera.transform.rotation = Quaternion.identity;
                }
            }
            
            _rayInteractor.MaxRayLength = rayDistance;
        }
        
        private void CreateDebugVisuals()
        {
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
            
            // Limpiar estados
            ClearAllStates();
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                enableMouseControl = !enableMouseControl;
                Debug.Log($"[MouseRayDebug] Mouse control: {(enableMouseControl ? "ACTIVADO" : "DESACTIVADO")}");
                
                if (!enableMouseControl)
                {
                    ClearAllStates();
                }
            }
            
            if (!enableMouseControl || !_isActive) return;
            
            UpdateMouseRay();
            UpdateInteraction();
            UpdateDebugVisuals();
        }
        
        private void UpdateMouseRay()
        {
            _currentRay = debugCamera.ScreenPointToRay(Input.mousePosition);
            transform.position = _currentRay.origin;
            transform.rotation = Quaternion.LookRotation(_currentRay.direction);
        }
        
        private void UpdateInteraction()
        {
            _wasMousePressed = _isMousePressed;
            _isMousePressed = Input.GetMouseButton(0);
            
            // Raycast para detectar interactables
            RayInteractable hoveredInteractable = null;
            RaycastHit hitInfo = default;
            
            if (Physics.Raycast(_currentRay, out RaycastHit hit, rayDistance))
            {
                hoveredInteractable = hit.collider.GetComponentInParent<RayInteractable>();
                hitInfo = hit;
            }
            
            // Manejar cambios de hover
            if (hoveredInteractable != _currentHoveredInteractable)
            {
                HandleHoverChange(hoveredInteractable);
            }
            
            // Actualizar estado del interactable actual
            if (_currentHoveredInteractable != null)
            {
                InteractorState previousState = _interactableStates.ContainsKey(_currentHoveredInteractable) 
                    ? _interactableStates[_currentHoveredInteractable] 
                    : InteractorState.Normal;
                
                InteractorState currentState = _isMousePressed ? InteractorState.Select : InteractorState.Normal;
                
                // Detectar transiciones de estado
                if (currentState != previousState)
                {
                    if (currentState == InteractorState.Select)
                    {
                        HandleSelectStart();
                    }
                    else
                    {
                        HandleSelectEnd();
                    }
                }
                
                _interactableStates[_currentHoveredInteractable] = currentState;
            }
            
            // Manejar clics fuera de interactables (para cerrar teclado)
            if (_isMousePressed && !_wasMousePressed && hoveredInteractable == null)
            {
                HandleClickOutside();
            }
        }
        
        private void HandleHoverChange(RayInteractable newHoveredInteractable)
        {
            // Exit del anterior
            if (_currentHoveredInteractable != null)
            {
                if (_currentHoveredButton != null)
                {
                    _currentHoveredButton.OnHoverExit();
                    _currentHoveredButton = null;
                }
                
                _currentHoveredInputField = null;
                _interactableStates.Remove(_currentHoveredInteractable);
            }
            
            // Enter al nuevo
            _currentHoveredInteractable = newHoveredInteractable;
            
            if (_currentHoveredInteractable != null)
            {
                // Buscar componentes específicos
                _currentHoveredButton = _currentHoveredInteractable.GetComponentInParent<InteractableButton3D>();
                _currentHoveredInputField = _currentHoveredInteractable.GetComponentInParent<InteractableInputField3D>();
                
                if (_currentHoveredButton != null)
                {
                    _currentHoveredButton.OnHoverEnter();
                    
                    if (simulateHaptics)
                    {
                        Debug.Log("[MouseRayDebug] 🎮 Hover haptic (0.1f)");
                    }
                }
                
                // Inicializar estado
                _interactableStates[_currentHoveredInteractable] = InteractorState.Normal;
                
                Debug.Log($"[MouseRayDebug] Hovering: {_currentHoveredInteractable.name}");
            }
        }
        
        private void HandleSelectStart()
        {
            Debug.Log($"[MouseRayDebug] Select start on: {_currentHoveredInteractable.name}");
            
            // Manejar input field
            if (_currentHoveredInputField != null)
            {
                // Buscar input fields activos
                var allInputFields = FindObjectsOfType<InteractableInputField3D>();
                InteractableInputField3D activeField = null;
                
                foreach (var field in allInputFields)
                {
                    if (field != _currentHoveredInputField && field.IsFocused())
                    {
                        activeField = field;
                        break;
                    }
                }
                
                // Si hay un campo activo diferente, desfocarlo primero
                if (activeField != null)
                {
                    activeField.Unfocus();
                    // Delay para permitir que se complete el unfocus
                    DOVirtual.DelayedCall(0.1f, () => {
                        if (_currentHoveredInputField != null)
                            _currentHoveredInputField.Focus();
                    });
                }
                else
                {
                    _currentHoveredInputField.Focus();
                }
                
                return;
            }
            
            // Manejar botón
            if (_currentHoveredButton != null)
            {
                _pressedButton = _currentHoveredButton;
                _pressedButton.OnSelectStart();
                
                if (simulateHaptics)
                {
                    Debug.Log("[MouseRayDebug] 🎮 Click haptic (0.3f)");
                }
            }
        }
        
        private void HandleSelectEnd()
        {
            Debug.Log($"[MouseRayDebug] Select end on: {_currentHoveredInteractable.name}");
            
            // Importante: Llamar OnSelectEnd en el botón presionado
            if (_pressedButton != null)
            {
                // Usar reflection para llamar al método privado OnSelectEnd
                var buttonType = typeof(InteractableButton3D);
                var onSelectEndMethod = buttonType.GetMethod("OnSelectEnd", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onSelectEndMethod != null)
                {
                    onSelectEndMethod.Invoke(_pressedButton, null);
                    Debug.Log($"[MouseRayDebug] Called OnSelectEnd on button: {_pressedButton.name}");
                }
                else
                {
                    Debug.LogWarning("[MouseRayDebug] Could not find OnSelectEnd method!");
                }
                
                _pressedButton = null;
            }
        }
        
        private void HandleClickOutside()
        {
            Debug.Log("[MouseRayDebug] Click outside any interactable");
            
            // Buscar si hay un input field activo
            var activeInputField = FindActiveInputField();
            
            if (activeInputField != null)
            {
                Debug.Log($"[MouseRayDebug] Unfocusing active input field: {activeInputField.name}");
                activeInputField.Unfocus();
            }
        }
        
        private InteractableInputField3D FindActiveInputField()
        {
            var allInputFields = FindObjectsOfType<InteractableInputField3D>();
            foreach (var field in allInputFields)
            {
                if (field.IsFocused())
                {
                    return field;
                }
            }
            return null;
        }
        
        private void ClearAllStates()
        {
            if (_currentHoveredButton != null)
            {
                _currentHoveredButton.OnHoverExit();
                _currentHoveredButton = null;
            }
            
            if (_pressedButton != null)
            {
                // Asegurar que se llame OnSelectEnd
                var buttonType = typeof(InteractableButton3D);
                var onSelectEndMethod = buttonType.GetMethod("OnSelectEnd", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onSelectEndMethod != null)
                {
                    onSelectEndMethod.Invoke(_pressedButton, null);
                }
                _pressedButton = null;
            }
            
            _currentHoveredInteractable = null;
            _currentHoveredInputField = null;
            _interactableStates.Clear();
            
            if (debugReticle != null)
            {
                debugReticle.SetActive(false);
            }
        }
        
        private void UpdateDebugVisuals()
        {
            if (!showDebugRay || debugLineRenderer == null) return;
            
            Vector3 startPos = _currentRay.origin;
            Vector3 endPos = _currentRay.origin + _currentRay.direction * rayDistance;
            Color currentColor = rayColor;
            
            if (Physics.Raycast(_currentRay, out RaycastHit hit, rayDistance))
            {
                endPos = hit.point;
                
                var interactable = hit.collider.GetComponentInParent<RayInteractable>();
                if (interactable != null)
                {
                    currentColor = _isMousePressed ? rayClickColor : rayHoverColor;
                    
                    if (debugReticle != null)
                    {
                        debugReticle.SetActive(true);
                        debugReticle.transform.position = hit.point;
                        debugReticle.GetComponent<Renderer>().material.color = currentColor;
                    }
                }
                else
                {
                    if (debugReticle != null)
                    {
                        debugReticle.SetActive(false);
                    }
                }
            }
            else
            {
                if (debugReticle != null)
                {
                    debugReticle.SetActive(false);
                }
            }
            
            debugLineRenderer.startColor = currentColor;
            debugLineRenderer.endColor = currentColor;
            debugLineRenderer.SetPosition(0, startPos);
            debugLineRenderer.SetPosition(1, endPos);
        }
        
        public void SetActive(bool active)
        {
            _isActive = active;
            enableMouseControl = active;
            
            if (!active)
            {
                ClearAllStates();
            }
        }
        
        public void SimulateClick()
        {
            if (_currentHoveredInteractable != null)
            {
                HandleSelectStart();
                DOVirtual.DelayedCall(0.1f, () => HandleSelectEnd());
            }
        }
        
        #region Gizmos
        
        private void OnDrawGizmos()
        {
            if (!enableMouseControl || !Application.isPlaying) return;
            
            Gizmos.color = _isMousePressed ? Color.red : Color.green;
            Gizmos.DrawRay(_currentRay.origin, _currentRay.direction * rayDistance);
            Gizmos.DrawWireSphere(_currentRay.origin, 0.1f);
        }
        
        #endregion
    }
}