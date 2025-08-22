using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Gestor singleton del teclado virtual para optimizar recursos
    /// Solo existe una instancia del teclado que se comparte entre todos los inputs
    /// </summary>
    public class VirtualKeyboardManager : MonoBehaviour
    {
        [Header("Keyboard Settings")]
        [SerializeField] private VirtualKeyboard3D keyboardPrefab;
        [SerializeField] private float animationDuration = 0.3f;

        [Header("Smart Positioning")] 
        [SerializeField] private float optimalDistance = 0.6f; // Distancia óptima del usuario
        [SerializeField] private float minDistance = 0.3f; // Distancia mínima
        [SerializeField] private float maxDistance = 1.2f; // Distancia máxima
        [SerializeField] private float heightOffset = -0.3f; // Offset desde el nivel de los ojos
        [SerializeField] public float angleTilt = 25f; // Inclinación del teclado en grados
        [SerializeField] private bool smoothMovement = true; // Movimiento suave del teclado
        [SerializeField] private float smoothSpeed = 5f; // Velocidad de movimiento suave
        
        [Header("Keyboard Size & Offset")]
        [SerializeField] private Vector3 keyboardOffset = new Vector3(0f, -0.2f, 0f);
        [SerializeField] private Vector3 KeyboardSize = Vector3.one;
        
        [Header("Advanced Positioning")]
        [SerializeField] private bool avoidOcclusion = true; // Evitar oclusión con objetos
        [SerializeField] private LayerMask occlusionLayers = -1; // Capas para detección de oclusión
        [SerializeField] private float lateralOffset = 0f; // Desplazamiento lateral si es necesario
        
        private VirtualKeyboard3D _keyboardInstance;
        [SerializeField] private InteractableInputField3D _currentActiveField;
        private Transform _playerTransform;
        
        // Callbacks para el input activo
        private System.Action<char> _onKeyPressed;
        private System.Action _onBackspace;
        private System.Action _onEnter;
        private System.Action _onSpace;
        
        // Estado de transición
        private bool _isTransitioning = false;
        private InteractableInputField3D _pendingField = null;
        private Coroutine _transitionCoroutine = null;
        
        public static VirtualKeyboardManager Instance { get; private set; }
        
        /// <summary>
        /// Verifica si el teclado está correctamente configurado
        /// </summary>
        public bool IsKeyboardConfigured => keyboardPrefab != null && _keyboardInstance != null;
        
        /// <summary>
        /// Intenta asignar el prefab del teclado si no está asignado
        /// </summary>
        public void SetKeyboardPrefab(VirtualKeyboard3D prefab)
        {
            if (keyboardPrefab == null && prefab != null)
            {
                keyboardPrefab = prefab;
                ValidateKeyboardSettings();
                CreateKeyboardInstance();
            }
        }
        
        /// <summary>
        /// Valida y corrige los settings del teclado si están en valores inválidos
        /// </summary>
        private void ValidateKeyboardSettings()
        {
            // Si KeyboardSize es cero o muy pequeño, usar tamaño por defecto
            if (KeyboardSize.magnitude < 0.01f)
            {
                KeyboardSize = Vector3.one;
                Debug.LogWarning("VirtualKeyboardManager: KeyboardSize was zero or too small, setting to Vector3.one");
            }
            
            // Si keyboardOffset no tiene valores razonables, usar offset por defecto
            if (keyboardOffset.magnitude < 0.01f && keyboardOffset == Vector3.zero)
            {
                keyboardOffset = new Vector3(0f, -0.2f, 0f);
                Debug.Log("VirtualKeyboardManager: keyboardOffset was zero, setting default offset");
            }
        }
        
        /// <summary>
        /// Configura los parámetros de posición y tamaño del teclado
        /// </summary>
        public void ConfigureKeyboardSettings(Vector3 offset, Vector3 size, float position)
        {
            keyboardOffset = offset;
            KeyboardSize = size;
            optimalDistance = position > 0 ? position : optimalDistance;
            
            // Validar los nuevos valores
            ValidateKeyboardSettings();
            
            Debug.Log($"VirtualKeyboardManager: Settings configured - Offset: {keyboardOffset}, Size: {KeyboardSize}, Distance: {optimalDistance}");
        }
        
        /// <summary>
        /// Configura los parámetros avanzados de posicionamiento
        /// </summary>
        public void ConfigureAdvancedPositioning(float optimal, float min, float max, float height, float tilt, bool smooth)
        {
            optimalDistance = Mathf.Clamp(optimal, 0.3f, 2f);
            minDistance = Mathf.Clamp(min, 0.2f, optimal);
            maxDistance = Mathf.Clamp(max, optimal, 3f);
            heightOffset = Mathf.Clamp(height, -1f, 1f);
            angleTilt = Mathf.Clamp(tilt, -45f, 45f);
            smoothMovement = smooth;
            
            Debug.Log($"VirtualKeyboardManager: Advanced positioning configured - Optimal: {optimalDistance}, Height: {heightOffset}, Tilt: {angleTilt}°");
        }
        
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Validar y corregir valores si están en cero
            ValidateKeyboardSettings();
            
            // Obtener referencia del jugador
            _playerTransform = Camera.main?.transform.parent;
            
            // Crear instancia única del teclado
            CreateKeyboardInstance();
        }

        private void Update()
        {
            if (_currentActiveField != null && _keyboardInstance != null && _keyboardInstance.gameObject.activeSelf)
            {
                UpdateKeyboardPosition(_currentActiveField.transform);
                CheckForExternalInteraction();
            }
        }
        
        /// <summary>
        /// Verifica si el usuario está interactuando con algo que no sea el InputField activo o el teclado
        /// </summary>
        private void CheckForExternalInteraction()
        {
            if (_currentActiveField == null || _isTransitioning) return;
            
            // Buscar todos los ray interactors
            var rayInteractors = FindObjectsOfType<Oculus.Interaction.RayInteractor>();
            
            foreach (var interactor in rayInteractors)
            {
                // Verificar si el interactor está seleccionando algo
                if (interactor.State == Oculus.Interaction.InteractorState.Select)
                {
                    if (interactor.HasCandidate)
                    {
                        var props = interactor.CandidateProperties as Oculus.Interaction.RayInteractor.RayCandidateProperties;
                        if (props != null && props.ClosestInteractable != null)
                        {
                            var interactable = props.ClosestInteractable;
                            
                            // Verificar si es el InputField actual
                            var inputField = interactable.GetComponent<InteractableInputField3D>();
                            if (inputField == _currentActiveField)
                            {
                                continue; // Es el campo actual, no hacer nada
                            }
                            
                            // Verificar si es parte del teclado virtual
                            var keyboardButton = interactable.GetComponentInParent<VirtualKeyboard3D>();
                            if (keyboardButton != null)
                            {
                                continue; // Es parte del teclado, no hacer nada
                            }
                            
                            // Verificar si es otro InputField
                            if (inputField != null)
                            {
                                // Se hará la transición automáticamente cuando el nuevo campo llame a Focus()
                                continue;
                            }
                            
                            // Es un objeto diferente (botón, etc.), ocultar el teclado
                            Debug.Log($"VirtualKeyboardManager: External interaction detected with {interactable.name}, hiding keyboard");
                            HideKeyboard();
                            return;
                        }
                    }
                    else
                    {
                        // Clic en el vacío, ocultar el teclado
                        Debug.Log("VirtualKeyboardManager: Click on empty space detected, hiding keyboard");
                        HideKeyboard();
                        return;
                    }
                }
            }
        }

        private void CreateKeyboardInstance()
        {
            if (keyboardPrefab == null)
            {
                Debug.LogError("VirtualKeyboardManager: No keyboard prefab assigned! Please assign a VirtualKeyboard3D prefab in the inspector.");
                return;
            }
            
            // Instanciar el teclado una sola vez
            _keyboardInstance = Instantiate(keyboardPrefab, transform);
            _keyboardInstance.gameObject.SetActive(false);
            
            // Suscribirse a los eventos del teclado
            _keyboardInstance.OnKeyPressed += HandleKeyPressed;
            _keyboardInstance.OnBackspace += HandleBackspace;
            _keyboardInstance.OnEnter += HandleEnter;
            _keyboardInstance.OnSpace += HandleSpace;
            _keyboardInstance.OnClose += HideKeyboard;
        }
        
        /// <summary>
        /// Muestra el teclado para un campo de entrada específico
        /// </summary>
        public void ShowKeyboardFor(InteractableInputField3D inputField, 
            System.Action<char> onKeyPressed,
            System.Action onBackspace,
            System.Action onEnter,
            System.Action onSpace)
        {
            // Si ya estamos en transición, cancelar
            if (_isTransitioning)
            {
                Debug.Log("VirtualKeyboardManager: Transition already in progress, queueing request.");
                _pendingField = inputField;
                return;
            }
            
            // Intentar crear el teclado si no existe
            if (_keyboardInstance == null)
            {
                CreateKeyboardInstance();
                if (_keyboardInstance == null)
                {
                    Debug.LogError("VirtualKeyboardManager: Cannot show keyboard - no instance available and unable to create one.");
                    return;
                }
            }
            
            // Actualizar referencia del jugador si es necesario
            if (_playerTransform == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    _playerTransform = mainCamera.transform.parent ?? mainCamera.transform;
                }
            }
            
            // Si es el mismo campo, no hacer nada
            if (_currentActiveField == inputField && IsKeyboardVisible)
            {
                return;
            }
            
            // Si hay un campo activo diferente, hacer transición
            if (_currentActiveField != null && _currentActiveField != inputField && IsKeyboardVisible)
            {
                // Iniciar transición entre campos
                if (_transitionCoroutine != null)
                {
                    StopCoroutine(_transitionCoroutine);
                }
                _transitionCoroutine = StartCoroutine(TransitionBetweenFields(inputField, onKeyPressed, onBackspace, onEnter, onSpace));
            }
            else
            {
                // No hay campo activo o el teclado está oculto, mostrar directamente
                
                // Notificar al campo anterior si existe
                if (_currentActiveField != null && _currentActiveField != inputField)
                {
                    _currentActiveField.OnKeyboardHidden();
                }
                
                // Guardar referencias
                _currentActiveField = inputField;
                _onKeyPressed = onKeyPressed;
                _onBackspace = onBackspace;
                _onEnter = onEnter;
                _onSpace = onSpace;
                
                // Posicionar el teclado en la posición óptima
                PositionKeyboard(inputField.transform);
                
                // Mostrar el teclado con animación
                ShowKeyboard();
            }
        }
        
        /// <summary>
        /// Oculta el teclado actual sin parámetros (para compatibilidad con delegados)
        /// </summary>
        public void HideKeyboard()
        {
            HideKeyboard(false);
        }
        
        /// <summary>
        /// Oculta el teclado actual con opción de ocultación inmediata
        /// </summary>
        public void HideKeyboard(bool immediate)
        {
            if (_keyboardInstance == null || !_keyboardInstance.gameObject.activeSelf) return;
            
            // Cancelar cualquier transición en progreso
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
                _isTransitioning = false;
            }
            
            if (immediate)
            {
                // Ocultar inmediatamente
                _keyboardInstance.gameObject.SetActive(false);
                _keyboardInstance.transform.localScale = Vector3.zero;
                CleanupAfterHide();
            }
            else
            {
                // Animar la desaparición
                _keyboardInstance.transform.DOScale(Vector3.zero, animationDuration * 0.7f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => 
                    {
                        _keyboardInstance.gameObject.SetActive(false);
                        CleanupAfterHide();
                    });
            }
        }
        
        private void CleanupAfterHide()
        {
            // Notificar al campo actual
            if (_currentActiveField != null)
            {
                _currentActiveField.OnKeyboardHidden();
                _currentActiveField = null;
            }
            
            // Limpiar callbacks
            _onKeyPressed = null;
            _onBackspace = null;
            _onEnter = null;
            _onSpace = null;
            _isTransitioning = false;
        }
        
        /// <summary>
        /// Verifica si el teclado está visible
        /// </summary>
        public bool IsKeyboardVisible => _keyboardInstance != null && _keyboardInstance.gameObject.activeSelf;
        
        /// <summary>
        /// Obtiene el campo de entrada activo actual
        /// </summary>
        public InteractableInputField3D GetActiveField() => _currentActiveField;
        
        private void PositionKeyboard(Transform fieldTransform)
        {
            if (_keyboardInstance == null || fieldTransform == null) return;
            
            Vector3 targetPosition = CalculateOptimalKeyboardPosition(fieldTransform);
            Quaternion targetRotation = CalculateOptimalKeyboardRotation(targetPosition);
            
            // Aplicar posición y rotación inicial
            _keyboardInstance.transform.position = targetPosition;
            _keyboardInstance.transform.rotation = targetRotation;
        }
        
        private void UpdateKeyboardPosition(Transform fieldTransform)
        {
            if (_keyboardInstance == null || fieldTransform == null || _playerTransform == null) return;
            
            Vector3 targetPosition = CalculateOptimalKeyboardPosition(fieldTransform);
            Quaternion targetRotation = CalculateOptimalKeyboardRotation(targetPosition);
            
            if (smoothMovement)
            {
                // Usar una distancia de umbral para evitar micro-movimientos
                float positionDiff = Vector3.Distance(_keyboardInstance.transform.position, targetPosition);
                
                // Solo mover si la diferencia es significativa
                if (positionDiff > 0.01f)
                {
                    _keyboardInstance.transform.position = Vector3.Lerp(
                        _keyboardInstance.transform.position, 
                        targetPosition, 
                        Time.deltaTime * smoothSpeed
                    );
                }
                
                // Solo rotar si la diferencia es significativa
                float rotationDiff = Quaternion.Angle(_keyboardInstance.transform.rotation, targetRotation);
                if (rotationDiff > 0.5f)
                {
                    _keyboardInstance.transform.rotation = Quaternion.Slerp(
                        _keyboardInstance.transform.rotation,
                        targetRotation,
                        Time.deltaTime * smoothSpeed
                    );
                }
            }
            else
            {
                _keyboardInstance.transform.position = targetPosition;
                _keyboardInstance.transform.rotation = targetRotation;
            }
        }
        
        private Vector3 CalculateOptimalKeyboardPosition(Transform fieldTransform)
        {
            if (_playerTransform == null) return fieldTransform.position + Vector3.forward * optimalDistance;
            
            // Obtener la cámara principal (cabeza del usuario en VR)
            Camera playerCamera = Camera.main;
            if (playerCamera == null) playerCamera = _playerTransform.GetComponentInChildren<Camera>();
            
            Vector3 playerPos = playerCamera != null ? playerCamera.transform.position : _playerTransform.position;
            Vector3 fieldPos = fieldTransform.position;
            
            // Calcular dirección desde el jugador hacia el campo
            Vector3 directionToField = (fieldPos - playerPos);
            directionToField.y = 0; // Ignorar diferencias de altura para el cálculo de dirección
            directionToField = directionToField.normalized;
            
            // Si no hay dirección clara, usar la dirección forward del jugador
            if (directionToField.magnitude < 0.001f)
            {
                directionToField = _playerTransform.forward;
                directionToField.y = 0;
                directionToField = directionToField.normalized;
            }
            
            // Calcular distancia horizontal entre jugador y campo
            Vector3 horizontalDiff = fieldPos - playerPos;
            horizontalDiff.y = 0;
            float distanceToField = horizontalDiff.magnitude;
            
            // Determinar la distancia del teclado desde el jugador
            float keyboardDistance = optimalDistance;
            
            // Si el campo está más cerca que la distancia óptima, ajustar
            if (distanceToField < optimalDistance)
            {
                // Colocar el teclado entre el jugador y el campo, pero nunca más cerca que minDistance
                keyboardDistance = Mathf.Max(distanceToField * 0.7f, minDistance);
            }
            else if (distanceToField > maxDistance * 2)
            {
                // Si el campo está muy lejos, limitar la distancia del teclado
                keyboardDistance = Mathf.Min(optimalDistance, maxDistance);
            }
            
            // Calcular posición base del teclado
            Vector3 keyboardPosition = playerPos + directionToField * keyboardDistance;
            
            // Ajustar altura del teclado
            float targetHeight = playerPos.y + heightOffset;
            
            // Si el campo está a una altura muy diferente, ajustar ligeramente
            float heightDiff = fieldPos.y - playerPos.y;
            if (Mathf.Abs(heightDiff) > 0.3f)
            {
                // Ajustar solo un poco hacia la altura del campo
                targetHeight += Mathf.Clamp(heightDiff * 0.2f, -0.2f, 0.2f);
            }
            
            keyboardPosition.y = targetHeight;
            
            // Aplicar offset lateral si es necesario
            if (Mathf.Abs(lateralOffset) > 0.01f)
            {
                Vector3 rightDirection = Vector3.Cross(Vector3.up, directionToField).normalized;
                keyboardPosition += rightDirection * lateralOffset;
            }
            
            // Verificar oclusión y ajustar si es necesario
            if (avoidOcclusion)
            {
                keyboardPosition = CheckAndAdjustForOcclusion(playerPos, keyboardPosition);
            }
            
            // Aplicar offset adicional en el espacio local del teclado
            // Por ahora aplicamos el offset en coordenadas del mundo
            keyboardPosition += keyboardOffset;
            
            return keyboardPosition;
        }
        
        private Quaternion CalculateOptimalKeyboardRotation(Vector3 keyboardPosition)
        {
            if (_playerTransform == null) return Quaternion.identity;
            
            Camera playerCamera = Camera.main;
            if (playerCamera == null) playerCamera = _playerTransform.GetComponentInChildren<Camera>();
            
            Vector3 playerPos = playerCamera != null ? playerCamera.transform.position : _playerTransform.position;
            
            // El teclado debe mirar AWAY del jugador (para que el jugador vea el frente)
            Vector3 lookDirection = keyboardPosition - playerPos;
            lookDirection.y = 0; // Mantener el teclado nivelado horizontalmente
            
            // Si no hay dirección válida, usar dirección por defecto
            if (lookDirection.magnitude < 0.001f)
            {
                lookDirection = _playerTransform.forward;
                lookDirection.y = 0;
            }
            
            // Crear rotación base - el teclado mira en la misma dirección que el vector desde el jugador
            Quaternion baseRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            
            // Aplicar rotación de 180 grados en Y para que el frente del teclado mire hacia el jugador
            Quaternion flipRotation = Quaternion.Euler(0, 180, 0);
            
            // Aplicar inclinación hacia el usuario para mejor visibilidad
            // Positivo inclina el teclado hacia el usuario (parte superior hacia atrás)
            Quaternion tiltRotation = Quaternion.Euler(angleTilt, 180, 0);
            
            return baseRotation * flipRotation * tiltRotation;
        }
        
        private Vector3 CheckAndAdjustForOcclusion(Vector3 playerPos, Vector3 targetPos)
        {
            // Verificar si hay objetos entre el jugador y la posición objetivo
            Vector3 direction = targetPos - playerPos;
            float distance = direction.magnitude;
            
            if (Physics.Raycast(playerPos, direction.normalized, out RaycastHit hit, distance, occlusionLayers))
            {
                // Si hay oclusión, ajustar la posición
                // Opción 1: Mover el teclado más cerca del jugador
                float adjustedDistance = hit.distance * 0.8f;
                
                if (adjustedDistance >= minDistance)
                {
                    return playerPos + direction.normalized * adjustedDistance;
                }
                
                // Opción 2: Intentar desplazar lateralmente
                Vector3[] lateralOffsets = new Vector3[]
                {
                    Vector3.Cross(Vector3.up, direction.normalized) * 0.3f,
                    Vector3.Cross(Vector3.up, direction.normalized) * -0.3f,
                    Vector3.up * 0.2f,
                    Vector3.down * 0.2f
                };
                
                foreach (var offset in lateralOffsets)
                {
                    Vector3 alternativePos = targetPos + offset;
                    if (!Physics.Linecast(playerPos, alternativePos, occlusionLayers))
                    {
                        return alternativePos;
                    }
                }
            }
            
            return targetPos;
        }
        
        private void ShowKeyboard()
        {
            if (_keyboardInstance == null) return;
            
            // Validar KeyboardSize antes de usarlo
            Vector3 targetScale = KeyboardSize;
            if (targetScale.magnitude < 0.01f)
            {
                targetScale = Vector3.one;
                Debug.LogWarning("VirtualKeyboardManager: KeyboardSize is zero, using Vector3.one for scale animation");
            }
            
            _keyboardInstance.gameObject.SetActive(true);
            
            // Animar la aparición
            _keyboardInstance.transform.localScale = Vector3.zero;
            _keyboardInstance.transform.DOScale(targetScale, animationDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => 
                {
                    _isTransitioning = false;
                    
                    // Si hay un campo pendiente, procesarlo
                    if (_pendingField != null)
                    {
                        var field = _pendingField;
                        _pendingField = null;
                        field.Focus();
                    }
                });
        }
        
        /// <summary>
        /// Realiza una transición suave entre campos de entrada
        /// </summary>
        private System.Collections.IEnumerator TransitionBetweenFields(
            InteractableInputField3D newField,
            System.Action<char> onKeyPressed,
            System.Action onBackspace,
            System.Action onEnter,
            System.Action onSpace)
        {
            _isTransitioning = true;
            
            // Animar salida del teclado
            yield return _keyboardInstance.transform
                .DOScale(Vector3.zero, animationDuration * 0.5f)
                .SetEase(Ease.InBack)
                .WaitForCompletion();
            
            // Notificar al campo anterior
            if (_currentActiveField != null)
            {
                _currentActiveField.OnKeyboardHidden();
            }
            
            // Actualizar referencias
            _currentActiveField = newField;
            _onKeyPressed = onKeyPressed;
            _onBackspace = onBackspace;
            _onEnter = onEnter;
            _onSpace = onSpace;
            
            // Reposicionar para el nuevo campo
            PositionKeyboard(newField.transform);
            
            // Validar tamaño
            Vector3 targetScale = KeyboardSize;
            if (targetScale.magnitude < 0.01f)
            {
                targetScale = Vector3.one;
            }
            
            // Animar entrada del teclado
            yield return _keyboardInstance.transform
                .DOScale(targetScale, animationDuration * 0.5f)
                .SetEase(Ease.OutBack)
                .WaitForCompletion();
            
            _isTransitioning = false;
            _transitionCoroutine = null;
            
            // Procesar campo pendiente si existe
            if (_pendingField != null)
            {
                var field = _pendingField;
                _pendingField = null;
                field.Focus();
            }
        }
        
        // Handlers para los eventos del teclado
        private void HandleKeyPressed(char key)
        {
            _onKeyPressed?.Invoke(key);
        }
        
        private void HandleBackspace()
        {
            _onBackspace?.Invoke();
        }
        
        private void HandleEnter()
        {
            _onEnter?.Invoke();
        }
        
        private void HandleSpace()
        {
            _onSpace?.Invoke();
        }
        
        /// <summary>
        /// Cambia el layout del teclado
        /// </summary>
        public void SwitchKeyboardLayout(VirtualKeyboard3D.KeyboardLayout layout)
        {
            _keyboardInstance?.SwitchLayout(layout);
        }
        
        /// <summary>
        /// Establece el estado de CapsLock
        /// </summary>
        public void SetCapsLock(bool enabled)
        {
            _keyboardInstance?.SetCapsLock(enabled);
        }
        
        private void OnDestroy()
        {
            if (_keyboardInstance != null)
            {
                // Desuscribirse de eventos
                _keyboardInstance.OnKeyPressed -= HandleKeyPressed;
                _keyboardInstance.OnBackspace -= HandleBackspace;
                _keyboardInstance.OnEnter -= HandleEnter;
                _keyboardInstance.OnSpace -= HandleSpace;
                _keyboardInstance.OnClose -= HideKeyboard;
                
                Destroy(_keyboardInstance.gameObject);
            }
        }
    }
}