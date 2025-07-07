using System;
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
        [SerializeField] private float keyboardDistance = 0.5f;
        [SerializeField] private float keyboardYOffset = -0.3f;
        [SerializeField] private float animationDuration = 0.3f;

        [Header("Keyboard Position")] 
        [SerializeField] private Vector3 keyboardOffset;
        [SerializeField] private Vector3 KeyboardSize;
        [SerializeField] private float keyboaardPos = 0.2f;
        
        private VirtualKeyboard3D _keyboardInstance;
        [SerializeField] private InteractableInputField3D _currentActiveField;
        private Transform _playerTransform;
        
        // Callbacks para el input activo
        private System.Action<char> _onKeyPressed;
        private System.Action _onBackspace;
        private System.Action _onEnter;
        private System.Action _onSpace;
        
        public static VirtualKeyboardManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Obtener referencia del jugador
            _playerTransform = Camera.main?.transform.parent;
            
            // Crear instancia única del teclado
            CreateKeyboardInstance();
        }

        private void Update()
        {
            if (_currentActiveField != null)
            {
                PositionKeyboard(_currentActiveField.transform);
            }
        }

        private void CreateKeyboardInstance()
        {
            if (keyboardPrefab == null)
            {
                UnityEngine.Debug.LogError("VirtualKeyboardManager: No keyboard prefab assigned!");
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
            if (_keyboardInstance == null) return;
            
            // Si ya hay un campo activo, notificarle que perdió el foco
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
            
            // Posicionar el teclado
            PositionKeyboard(inputField.transform);
            
            // Mostrar el teclado con animación
            ShowKeyboard();
        }
        
        /// <summary>
        /// Oculta el teclado actual
        /// </summary>
        public void HideKeyboard()
        {
            if (_keyboardInstance == null || !_keyboardInstance.gameObject.activeSelf) return;
            
            // Animar la desaparición
            _keyboardInstance.transform.DOScale(Vector3.zero, animationDuration * 0.7f).SetEase(Ease.InBack).OnComplete(() => 
                {
                    _keyboardInstance.gameObject.SetActive(false);
                    
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
                });
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
            
            // Calcular posición relativa al campo
            /*Vector3 keyboardPos = fieldTransform.position;
            keyboardPos += fieldTransform.forward * keyboardDistance;
            keyboardPos.y += keyboardYOffset;*/

            Vector3 keyboardPos = Vector3.Lerp(_playerTransform.position, fieldTransform.position, keyboaardPos);
            _keyboardInstance.transform.position = (keyboardPos+keyboardOffset);
            
            // Hacer que el teclado mire hacia el jugador
            if (_playerTransform != null)
            {
                Vector3 lookDirection = _keyboardInstance.transform.position - _playerTransform.position;
                lookDirection.y = 0;
                _keyboardInstance.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
        
        private void ShowKeyboard()
        {
            if (_keyboardInstance == null) return;
            
            _keyboardInstance.gameObject.SetActive(true);
            
            // Animar la aparición
            _keyboardInstance.transform.localScale = Vector3.zero;
            _keyboardInstance.transform.DOScale(KeyboardSize, animationDuration).SetEase(Ease.OutBack);
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