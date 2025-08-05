using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using TMPro;
using DG.Tweening;
using System.Text;
using System.Collections;
using Oculus.Interaction.Surfaces;
using System.Collections.Generic;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Campo de texto 3D interactuable para VR con teclado virtual compartido
    /// </summary>
    [System.Serializable]
    public class InputFieldValueChangedEvent : UnityEvent<string> { }
    
    public class InteractableInputField3D : MonoBehaviour
    {
        [Header("Input Field Components")]
        [SerializeField] private RayInteractable fieldInteractable;
        [SerializeField] private TMP_InputField inputText;
        [SerializeField] private TextMeshProUGUI placeholderText;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private Transform inputFieldBackground;
        [SerializeField] private Transform caretObject;
        
        [Header("Visual Elements")]
        [SerializeField] private GameObject focusOutline;
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material focusedMaterial;
        [SerializeField] private Material hoverMaterial;
        [SerializeField] private Material errorMaterial;
        
        [Header("Input Settings")]
        [SerializeField] private string fieldTitle = "Input Field";
        [SerializeField] private string placeholderString = "Enter text...";
        [SerializeField] private int characterLimit = 50;
        [SerializeField] private TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard;
        [SerializeField] private bool multiLine = false;
        [SerializeField] private int maxLines = 1;
        
        [Header("Validation")]
        [SerializeField] private bool validateInput = false;
        [SerializeField] private string validationRegex = "";
        [SerializeField] private string validationErrorMessage = "Invalid input";
        
        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.2f;
        [SerializeField] private float caretBlinkRate = 0.5f;
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip typingSound;
        [SerializeField] private AudioClip errorSound;
        [SerializeField] private AudioClip submitSound;
        
        [Header("Events")]
        public InputFieldValueChangedEvent OnValueChanged;
        public InputFieldValueChangedEvent OnEndEdit;
        public UnityEvent OnSelect;
        public UnityEvent OnDeselect;
        
        // Private state
        private string _currentText = "";
        private bool _isFocused = false;
        private bool _isHovered = false;
        private VirtualKeyboardManager _keyboardManager;
        private Renderer _backgroundRenderer;
        private Coroutine _caretBlinkCoroutine;
        private int _caretPosition = 0;
        private bool _isValid = true;
        
        // Tracking de interactores
        private Dictionary<RayInteractor, bool> _hoveredInteractors = new Dictionary<RayInteractor, bool>();
        private RayInteractor _activeInteractor;
        
        private void Awake()
        {
            InitializeComponents();
            SetupInteraction();
            
            // Obtener referencia al KeyboardManager
            _keyboardManager = VirtualKeyboardManager.Instance;
            if (_keyboardManager == null)
            {
                Debug.LogWarning("VirtualKeyboardManager not found! Creating one...");
                GameObject keyboardManagerObj = new GameObject("VirtualKeyboardManager");
                _keyboardManager = keyboardManagerObj.AddComponent<VirtualKeyboardManager>();
            }
        }
        
        private void InitializeComponents()
        {
            // Get background renderer
            if (inputFieldBackground != null)
            {
                _backgroundRenderer = inputFieldBackground.GetComponent<Renderer>();
            }
            
            // Set initial texts
            if (titleLabel != null)
            {
                titleLabel.text = fieldTitle;
            }
            
            if (placeholderText != null)
            {
                placeholderText.text = placeholderString;
                placeholderText.gameObject.SetActive(true);
            }
            
            // Hide caret initially
            if (caretObject != null)
            {
                caretObject.gameObject.SetActive(false);
            }
            
            // Hide focus outline
            if (focusOutline != null)
            {
                focusOutline.SetActive(false);
            }
            
            // Create audio source if needed
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 5f;
            }
        }
        
        private void SetupInteraction()
        {
            // Setup field interactable
            if (fieldInteractable == null)
            {
                fieldInteractable = GetComponent<RayInteractable>();
                if (fieldInteractable == null)
                {
                    fieldInteractable = gameObject.AddComponent<RayInteractable>();
                }
            }
            
            // Setup collision surface
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(2f, 0.5f, 0.1f);
                collider.isTrigger = true;
            }
            
            ColliderSurface surface = GetComponent<ColliderSurface>();
            if (surface == null)
            {
                surface = gameObject.AddComponent<ColliderSurface>();
            }
            
            fieldInteractable.InjectSurface(surface);
        }
        
        private void Update()
        {
            CheckInteraction();
        }
        
        public bool IsFocused()
        {
            return _isFocused;
        }
        
       private void CheckInteraction()
{
    if (fieldInteractable == null) return;
    
    var rayInteractors = FindObjectsOfType<RayInteractor>();
    
    foreach (var interactor in rayInteractors)
    {
        bool isCurrentlyHovering = false;
        
        if (interactor.HasCandidate && 
            interactor.CandidateProperties is RayInteractor.RayCandidateProperties props &&
            props.ClosestInteractable == fieldInteractable)
        {
            isCurrentlyHovering = true;
            
            bool wasHovering = _hoveredInteractors.ContainsKey(interactor) && _hoveredInteractors[interactor];
            
            if (!wasHovering)
            {
                if (!_isHovered)
                {
                    _activeInteractor = interactor;
                    OnHoverEnter();
                }
            }
            
            // Detectar clic para focus
            if (!_isFocused && interactor.State == InteractorState.Select)
            {
                // Verificar que es un nuevo clic
                InteractorState previousState = _hoveredInteractors.ContainsKey(interactor) ? 
                    InteractorState.Normal : InteractorState.Normal;
                    
                if (wasHovering) // Solo si ya estaba hovering
                {
                    Focus();
                }
            }
        }
        else if (_hoveredInteractors.ContainsKey(interactor) && _hoveredInteractors[interactor])
        {
            if (_activeInteractor == interactor)
            {
                OnHoverExit();
                _activeInteractor = null;
            }
        }
        
        _hoveredInteractors[interactor] = isCurrentlyHovering;
    }
    
    // Mejorar detección de clics fuera
    if (_isFocused && _keyboardManager != null)
    {
        bool shouldUnfocus = false;
        
        foreach (var interactor in rayInteractors)
        {
            if (interactor.State == InteractorState.Select)
            {
                if (interactor.HasCandidate)
                {
                    var props = interactor.CandidateProperties as RayInteractor.RayCandidateProperties;
                    if (props != null && props.ClosestInteractable != fieldInteractable)
                    {
                        // Verificar si el clic es en el teclado virtual
                        var clickedInteractable = props.ClosestInteractable;
                        var button = clickedInteractable.GetComponentInParent<InteractableButton3D>();
                        var keyboard = button?.GetComponentInParent<VirtualKeyboard3D>();
                        
                        if (keyboard == null)
                        {
                            // No es el teclado, deberíamos desfocar
                            shouldUnfocus = true;
                            break;
                        }
                    }
                }
                else
                {
                    // Clic en el vacío
                    shouldUnfocus = true;
                    break;
                }
            }
        }
        
        if (shouldUnfocus)
        {
            Unfocus();
        }
    }
}
        
        private void OnHoverEnter()
        {
            if (_isFocused) return;
            
            _isHovered = true;
            UpdateMaterial(hoverMaterial);
            
            // Scale animation
            inputFieldBackground.DOScale(inputFieldBackground.localScale * 1.02f, animationDuration)
                .SetEase(Ease.OutQuad);
        }
        
        private void OnHoverExit()
        {
            if (_isFocused) return;
            
            _isHovered = false;
            UpdateMaterial(normalMaterial);
            
            // Reset scale
            inputFieldBackground.DOScale(Vector3.one, animationDuration)
                .SetEase(Ease.OutQuad);
        }
        
        public void Focus()
        {
            if (_isFocused) return;
            
            _isFocused = true;
            
            // Visual updates
            UpdateMaterial(focusedMaterial);
            
            if (focusOutline != null)
            {
                focusOutline.SetActive(true);
                focusOutline.transform.DOScale(Vector3.one, animationDuration)
                    .From(Vector3.one * 0.9f)
                    .SetEase(Ease.OutBack);
            }
            
            // Show input text and hide placeholder
            if (string.IsNullOrEmpty(_currentText))
            {
                if (placeholderText != null) placeholderText.gameObject.SetActive(false);
                if (inputText != null) inputText.gameObject.SetActive(true);
            }
            
            // Start caret blinking
            StartCaretBlink();
            
            // Request keyboard from manager
            if (_keyboardManager != null)
            {
                _keyboardManager.ShowKeyboardFor(
                    this,
                    OnVirtualKeyPressed,
                    OnVirtualBackspace,
                    OnVirtualEnter,
                    OnVirtualSpace
                );
            }
            
            // Haptic feedback
            TriggerHapticFeedback(0.2f);
            
            OnSelect?.Invoke();
        }
        
        public void Unfocus()
        {
            if (!_isFocused) return;
            
            _isFocused = false;
            
            // Visual updates
            UpdateMaterial(_isValid ? normalMaterial : errorMaterial);
            
            if (focusOutline != null)
            {
                focusOutline.transform.DOScale(Vector3.one * 0.9f, animationDuration)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => focusOutline.SetActive(false));
            }
            
            // Show placeholder if empty
            if (string.IsNullOrEmpty(_currentText))
            {
                if (placeholderText != null) placeholderText.gameObject.SetActive(true);
                //if (inputText != null) inputText.gameObject.SetActive(false);
            }
            
            // Stop caret
            StopCaretBlink();
            
            // Hide keyboard through manager
            if (_keyboardManager != null && _keyboardManager.GetActiveField() == this)
            {
                _keyboardManager.HideKeyboard();
            }
            
            OnDeselect?.Invoke();
            OnEndEdit?.Invoke(_currentText);
        }
        
        /// <summary>
        /// Called by KeyboardManager when keyboard is hidden
        /// </summary>
        public void OnKeyboardHidden()
        {
            if (_isFocused)
            {
                _isFocused = false;
                
                // Visual updates without triggering events again
                UpdateMaterial(_isValid ? normalMaterial : errorMaterial);
                
                if (focusOutline != null)
                {
                    focusOutline.SetActive(false);
                }
                
                // Show placeholder if empty
                if (string.IsNullOrEmpty(_currentText))
                {
                    if (placeholderText != null) placeholderText.gameObject.SetActive(true);
                    //if (inputText != null) inputText.gameObject.SetActive(false);
                }
                
                StopCaretBlink();
            }
        }
        
        private void OnVirtualKeyPressed(char key)
        {
            if (_currentText.Length >= characterLimit) 
            {
                PlayErrorSound();
                return;
            }
            
            // Insert character at caret position
            _currentText = _currentText.Insert(_caretPosition, key.ToString());
            _caretPosition++;
            
            UpdateText();
            PlayTypingSound();
        }
        
        private void OnVirtualBackspace()
        {
            if (_caretPosition > 0)
            {
                _currentText = _currentText.Remove(_caretPosition - 1, 1);
                _caretPosition--;
                UpdateText();
                PlayTypingSound();
            }
        }
        
        private void OnVirtualEnter()
        {
            if (multiLine && _currentText.Split('\n').Length < maxLines)
            {
                _currentText = _currentText.Insert(_caretPosition, "\n");
                _caretPosition++;
                UpdateText();
            }
            else
            {
                // Submit and unfocus
                Unfocus();
                PlaySubmitSound();
            }
        }
        
        private void OnVirtualSpace()
        {
            if (_currentText.Length < characterLimit)
            {
                _currentText = _currentText.Insert(_caretPosition, " ");
                _caretPosition++;
                UpdateText();
                PlayTypingSound();
            }
        }
        
        private void UpdateText()
        {
            if (inputText != null)
            {
                inputText.text = _currentText;
            }
            
            // Validate if needed
            if (validateInput)
            {
                ValidateInput();
            }
            
            // Update caret position
            UpdateCaretPosition();
            
            // Fire value changed event
            OnValueChanged?.Invoke(_currentText);
        }
        
        private void ValidateInput()
        {
            if (string.IsNullOrEmpty(validationRegex)) 
            {
                _isValid = true;
                return;
            }
            
            try
            {
                System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(validationRegex);
                _isValid = regex.IsMatch(_currentText);
            }
            catch
            {
                _isValid = true; // Default to valid if regex is invalid
            }
            
            // Update visual state based on validation
            if (!_isValid && !_isFocused)
            {
                UpdateMaterial(errorMaterial);
                ShowValidationError();
            }
        }
        
        private void ShowValidationError()
        {
            // Could show a tooltip or error message
            if (!string.IsNullOrEmpty(validationErrorMessage))
            {
                Debug.Log($"Validation Error: {validationErrorMessage}");
                PlayErrorSound();
            }
        }
        
        private void StartCaretBlink()
        {
            if (caretObject == null) return;
            
            StopCaretBlink();
            caretObject.gameObject.SetActive(true);
            _caretBlinkCoroutine = StartCoroutine(CaretBlinkRoutine());
        }
        
        private void StopCaretBlink()
        {
            if (_caretBlinkCoroutine != null)
            {
                StopCoroutine(_caretBlinkCoroutine);
                _caretBlinkCoroutine = null;
            }
            
            if (caretObject != null)
            {
                caretObject.gameObject.SetActive(false);
            }
        }
        
        private IEnumerator CaretBlinkRoutine()
        {
            while (true)
            {
                caretObject.gameObject.SetActive(!caretObject.gameObject.activeSelf);
                yield return new WaitForSeconds(caretBlinkRate);
            }
        }
        
        private void UpdateCaretPosition()
        {
            if (caretObject == null || inputText == null) return;
            
            // Simple caret positioning - you might want to improve this
            // for proper text measurement
            float charWidth = 0.02f; // Approximate character width
            float xOffset = _caretPosition * charWidth;
            
            Vector3 caretPos = caretObject.localPosition;
            caretPos.x = -0.9f + xOffset; // Start from left edge
            caretObject.localPosition = caretPos;
        }
        
        private void UpdateMaterial(Material material)
        {
            if (_backgroundRenderer != null && material != null)
            {
                _backgroundRenderer.material = material;
            }
        }
        
        private void TriggerHapticFeedback(float intensity)
        {
            // Use the active interactor to determine which hand
            if (_activeInteractor != null)
            {
                bool isLeftHand = _activeInteractor.name.ToLower().Contains("left");
                OVRInput.Controller controller = isLeftHand ? 
                    OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
                    
                OVRInput.SetControllerVibration(1, intensity, controller);
                DOVirtual.DelayedCall(0.1f, () => 
                    OVRInput.SetControllerVibration(0, 0, controller));
            }
        }
        
        private void PlayTypingSound()
        {
            if (audioSource != null && typingSound != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f); // Slight variation
                audioSource.PlayOneShot(typingSound, 0.5f);
            }
        }
        
        private void PlayErrorSound()
        {
            if (audioSource != null && errorSound != null)
            {
                audioSource.PlayOneShot(errorSound, 0.7f);
            }
        }
        
        private void PlaySubmitSound()
        {
            if (audioSource != null && submitSound != null)
            {
                audioSource.PlayOneShot(submitSound, 0.8f);
            }
        }
        
        #region Public Methods
        
        public void SetText(string text)
        {
            _currentText = text ?? "";
            _caretPosition = _currentText.Length;
    
            if (inputText != null)
            {
                inputText.text = _currentText;
            }
    
            // Modificación: mantener inputText visible siempre
            bool hasText = !string.IsNullOrEmpty(_currentText);
            if (placeholderText != null) 
                placeholderText.gameObject.SetActive(!hasText && !_isFocused);
    
            if (inputText != null) 
                inputText.gameObject.SetActive(true); // Siempre visible
    
            OnValueChanged?.Invoke(_currentText);
        }
        
        public string GetText()
        {
            return _currentText;
        }
        
        public void SetInteractable(bool interactable)
        {
            enabled = interactable;
            if (fieldInteractable != null)
            {
                fieldInteractable.enabled = interactable;
            }
            
            // Visual feedback
            if (!interactable)
            {
                UpdateMaterial(normalMaterial);
                if (_backgroundRenderer != null)
                {
                    Color color = _backgroundRenderer.material.color;
                    color.a = 0.5f;
                    _backgroundRenderer.material.color = color;
                }
            }
        }
        
        public void SetPlaceholder(string placeholder)
        {
            placeholderString = placeholder;
            if (placeholderText != null)
            {
                placeholderText.text = placeholder;
            }
        }
        
        public void SetCharacterLimit(int limit)
        {
            characterLimit = Mathf.Max(1, limit);
            if (_currentText.Length > characterLimit)
            {
                _currentText = _currentText.Substring(0, characterLimit);
                UpdateText();
            }
        }
        
        public void SetContentType(TMP_InputField.ContentType type)
        {
            contentType = type;
            
            // Update validation based on content type
            switch (contentType)
            {
                case TMP_InputField.ContentType.IntegerNumber:
                    validationRegex = @"^-?\d+$";
                    if (_keyboardManager != null)
                    {
                        _keyboardManager.SwitchKeyboardLayout(VirtualKeyboard3D.KeyboardLayout.Numeric);
                    }
                    break;
                case TMP_InputField.ContentType.DecimalNumber:
                    validationRegex = @"^-?\d*\.?\d*$";
                    if (_keyboardManager != null)
                    {
                        _keyboardManager.SwitchKeyboardLayout(VirtualKeyboard3D.KeyboardLayout.Numeric);
                    }
                    break;
                case TMP_InputField.ContentType.EmailAddress:
                    validationRegex = @"^[\w\.-]+@[\w\.-]+\.\w+$";
                    break;
                case TMP_InputField.ContentType.Password:
                    // Password fields might want to hide text
                    break;
            }
        }
        
        public void Clear()
        {
            SetText("");
        }
        
        #endregion
        
        #region Gizmos
        
        private void OnDrawGizmosSelected()
        {
            // Draw input field bounds
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(transform.position + collider.center, collider.size);
            }
        }
        
        #endregion
        
        private void OnDestroy()
        {
            // Clean up
            _hoveredInteractors.Clear();
            
            if (_isFocused && _keyboardManager != null)
            {
                _keyboardManager.HideKeyboard();
            }
        }
    }
}