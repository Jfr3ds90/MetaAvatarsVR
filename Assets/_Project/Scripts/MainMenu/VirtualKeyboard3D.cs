using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Oculus.Interaction;
using UnityEngine.UI;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Componente helper para mantener referencia al icono de una tecla
    /// </summary>
    public class KeyIconReference : MonoBehaviour
    {
        public Image iconImage;
    }
    
    /// <summary>
    /// Teclado virtual 3D interactivo para VR
    /// Soporta múltiples layouts y feedback háptico
    /// </summary>
    public class VirtualKeyboard3D : MonoBehaviour
    {
        [System.Serializable]
        public class KeyboardKey
        {
            public string character;
            public string shiftCharacter;
            public KeyCode keyCode;
            public KeyType keyType;
            public float width = 1f; // Multiplicador de ancho para teclas especiales
            public float height = 1f; // Multiplicador de alto para teclas especiales
            public Sprite iconSprite; // Icono para teclas especiales
            public InteractableButton3D button;
        }
        
        public enum KeyType
        {
            Character,
            Backspace,
            Enter,
            Space,
            Shift,
            Numbers,
            Symbols,
            Close
        }
        
        public enum KeyboardLayout
        {
            Alphabetic,
            Numeric,
            Symbols
        }
        
        [Header("Keyboard Configuration")]
        [SerializeField] private Transform keysContainer;
        [SerializeField] private GameObject keyPrefab; // Prefab con InteractableButton3D y RectTransform
        [SerializeField] private float baseKeyWidth = 80f; // Ancho base en unidades de RectTransform
        [SerializeField] private float baseKeyHeight = 80f; // Alto base en unidades de RectTransform
        [SerializeField] private float keySpacing = 10f;
        [SerializeField] private KeyboardLayout currentLayout = KeyboardLayout.Alphabetic;
        
        [Header("Special Key Icons")]
        [SerializeField] private Sprite backspaceIcon;
        [SerializeField] private Sprite enterIcon;
        [SerializeField] private Sprite spaceIcon;
        [SerializeField] private Sprite shiftIcon;
        [SerializeField] private Sprite numbersIcon;
        [SerializeField] private Sprite symbolsIcon;
        [SerializeField] private Sprite closeIcon;
        
        [Header("Visual Settings")]
        [SerializeField] private Material normalKeyMaterial;
        [SerializeField] private Material specialKeyMaterial;
        [SerializeField] private Material pressedKeyMaterial;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color specialKeyTextColor = Color.cyan;
        [SerializeField] private Color iconColor = Color.white;
        
        [Header("Icon Settings")]
        [SerializeField] private float defaultIconMargin = 0.2f; // 20% margen por defecto
        [SerializeField] private float spaceKeyIconMargin = 0.3f; // 30% para barra espaciadora
        [SerializeField] private float largeKeyIconMargin = 0.25f; // 25% para teclas grandes
        
        [Header("Layout Panels")]
        [SerializeField] private GameObject alphabeticPanel;
        [SerializeField] private GameObject numericPanel;
        [SerializeField] private GameObject symbolsPanel;
        
        [Header("Special Keys")]
        [SerializeField] private TextMeshProUGUI shiftIndicator;
        [SerializeField] private TextMeshProUGUI capsLockIndicator;
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip keyPressSound;
        [SerializeField] private AudioClip specialKeySound;
        [SerializeField] private float soundVolume = 0.5f;
        
        [Header("Haptics")]
        [SerializeField] private float hapticIntensity = 0.1f;
        [SerializeField] private float hapticDuration = 0.05f;
        
        // Events
        public event System.Action<char> OnKeyPressed;
        public event System.Action OnBackspace;
        public event System.Action OnEnter;
        public event System.Action OnSpace;
        public event System.Action OnClose;
        
        // Private state
        private Dictionary<KeyboardLayout, List<KeyboardKey>> _keyboardLayouts;
        private List<KeyboardKey> _currentKeys;
        private bool _isShiftActive = false;
        private bool _isCapsLockActive = false;
        private Transform _currentLayoutPanel;
        
        // Keyboard layouts definition
        private readonly string[] QWERTY_LAYOUT = new string[]
        {
            "1234567890",
            "qwertyuiop",
            "asdfghjkl",
            "^zxcvbnm<"
        };
        
        private readonly string[] NUMBERS_LAYOUT = new string[]
        {
            "123",
            "456",
            "789",
            "*0#"
        };
        
        private readonly string[] SYMBOLS_LAYOUT = new string[]
        {
            "!@#$%^&*()",
            "-=[]\\;',./",
            "_+{}|:\"<>?",
            "~`"
        };
        
        private void Awake()
        {
            InitializeKeyboardLayouts();
            
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 5f;
            }
        }
        
        private void Start()
        {
            // Create initial layout
            SwitchLayout(currentLayout);
        }
        
        private void InitializeKeyboardLayouts()
        {
            _keyboardLayouts = new Dictionary<KeyboardLayout, List<KeyboardKey>>();
            
            // Initialize alphabetic layout
            _keyboardLayouts[KeyboardLayout.Alphabetic] = CreateAlphabeticLayout();
            
            // Initialize numeric layout
            _keyboardLayouts[KeyboardLayout.Numeric] = CreateNumericLayout();
            
            // Initialize symbols layout
            _keyboardLayouts[KeyboardLayout.Symbols] = CreateSymbolsLayout();
        }
        
        private List<KeyboardKey> CreateAlphabeticLayout()
        {
            List<KeyboardKey> keys = new List<KeyboardKey>();
            
            // Create keys from QWERTY layout
            for (int row = 0; row < QWERTY_LAYOUT.Length; row++)
            {
                string rowChars = QWERTY_LAYOUT[row];
                
                for (int col = 0; col < rowChars.Length; col++)
                {
                    char c = rowChars[col];
                    
                    if (c == '^') // Shift key
                    {
                        keys.Add(new KeyboardKey
                        {
                            character = "⇧",
                            shiftCharacter = "⇧",
                            keyType = KeyType.Shift,
                            width = 1.5f,
                            height = 1f,
                            iconSprite = shiftIcon
                        });
                    }
                    else if (c == '<') // Backspace key
                    {
                        keys.Add(new KeyboardKey
                        {
                            character = "⌫",
                            shiftCharacter = "⌫",
                            keyType = KeyType.Backspace,
                            width = 1.5f,
                            height = 1f,
                            iconSprite = backspaceIcon
                        });
                    }
                    else
                    {
                        // Regular character key
                        string upper = c.ToString().ToUpper();
                        keys.Add(new KeyboardKey
                        {
                            character = c.ToString(),
                            shiftCharacter = upper,
                            keyType = KeyType.Character,
                            width = 1f,
                            height = 1f
                        });
                    }
                }
            }
            
            // Add bottom row special keys
            keys.Add(new KeyboardKey
            {
                character = "123",
                shiftCharacter = "123",
                keyType = KeyType.Numbers,
                width = 1.5f,
                height = 1f,
                iconSprite = numbersIcon
            });
            
            keys.Add(new KeyboardKey
            {
                character = "Space",
                shiftCharacter = "Space",
                keyType = KeyType.Space,
                width = 5f,
                height = 1f,
                iconSprite = spaceIcon
            });
            
            keys.Add(new KeyboardKey
            {
                character = "Enter",
                shiftCharacter = "Enter",
                keyType = KeyType.Enter,
                width = 2f,
                height = 1f,
                iconSprite = enterIcon
            });
            
            return keys;
        }
        
        private List<KeyboardKey> CreateNumericLayout()
        {
            List<KeyboardKey> keys = new List<KeyboardKey>();
            
            // Create number keys
            foreach (char c in "1234567890")
            {
                keys.Add(new KeyboardKey
                {
                    character = c.ToString(),
                    shiftCharacter = c.ToString(),
                    keyType = KeyType.Character,
                    width = 1f,
                    height = 1f
                });
            }
            
            // Add special keys
            keys.Add(new KeyboardKey
            {
                character = ".",
                shiftCharacter = ",",
                keyType = KeyType.Character,
                width = 1f,
                height = 1f
            });
            
            keys.Add(new KeyboardKey
            {
                character = "⌫",
                shiftCharacter = "⌫",
                keyType = KeyType.Backspace,
                width = 1.5f,
                height = 1f,
                iconSprite = backspaceIcon
            });
            
            keys.Add(new KeyboardKey
            {
                character = "ABC",
                shiftCharacter = "ABC",
                keyType = KeyType.Symbols, // Reuse for returning to alpha
                width = 1.5f,
                height = 1f,
                iconSprite = symbolsIcon
            });
            
            return keys;
        }
        
        private List<KeyboardKey> CreateSymbolsLayout()
        {
            List<KeyboardKey> keys = new List<KeyboardKey>();
            
            // Create symbol keys
            string symbols = "!@#$%^&*()-=+[]{}\\|;:'\",.<>?/~`_";
            foreach (char c in symbols)
            {
                keys.Add(new KeyboardKey
                {
                    character = c.ToString(),
                    shiftCharacter = c.ToString(),
                    keyType = KeyType.Character,
                    width = 1f,
                    height = 1f
                });
            }
            
            return keys;
        }
        
        public void SwitchLayout(KeyboardLayout layout)
        {
            // Hide current layout
            if (_currentLayoutPanel != null)
            {
                _currentLayoutPanel.gameObject.SetActive(false);
            }
            
            // Clear existing keys
            ClearCurrentKeys();
            
            currentLayout = layout;
            _currentKeys = _keyboardLayouts[layout];
            
            // Show appropriate panel
            switch (layout)
            {
                case KeyboardLayout.Alphabetic:
                    _currentLayoutPanel = alphabeticPanel?.transform ?? keysContainer;
                    break;
                case KeyboardLayout.Numeric:
                    _currentLayoutPanel = numericPanel?.transform ?? keysContainer;
                    break;
                case KeyboardLayout.Symbols:
                    _currentLayoutPanel = symbolsPanel?.transform ?? keysContainer;
                    break;
            }
            
            if (_currentLayoutPanel != null)
            {
                _currentLayoutPanel.gameObject.SetActive(true);
            }
            
            // Generate keyboard layout
            GenerateKeyboard();
        }
        
        private void GenerateKeyboard()
        {
            if (_currentKeys == null || _currentKeys.Count == 0) return;
            
            // Clear existing visual keys
            foreach (Transform child in keysContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Generate keys based on layout
            if (currentLayout == KeyboardLayout.Alphabetic)
            {
                GenerateQWERTYLayout();
            }
            else if (currentLayout == KeyboardLayout.Numeric)
            {
                GenerateNumericPad();
            }
            else
            {
                GenerateSymbolsGrid();
            }
        }
        
        private void GenerateQWERTYLayout()
        {
            float currentX = 0;
            float currentY = 0;
            int keyIndex = 0;
            
            float[] rowOffsets = { 0f, 0.5f, 0.75f, 0f }; // Indentation for each row
            
            for (int row = 0; row < QWERTY_LAYOUT.Length; row++)
            {
                currentX = rowOffsets[row] * (baseKeyWidth + keySpacing);
                currentY = -row * (baseKeyHeight + keySpacing);
                
                int keysInRow = QWERTY_LAYOUT[row].Length;
                
                for (int col = 0; col < keysInRow; col++)
                {
                    if (keyIndex >= _currentKeys.Count) break;
                    
                    KeyboardKey keyData = _currentKeys[keyIndex];
                    
                    // Calcular posición centrada considerando el ancho de la tecla
                    float keyWidth = baseKeyWidth * keyData.width;
                    float keyHeight = baseKeyHeight * keyData.height;
                    
                    // La posición debe ser el centro de la tecla
                    Vector3 centerPosition = new Vector3(
                        currentX + (keyWidth / 2f),
                        currentY - (keyHeight / 2f),
                        0
                    );
                    
                    GameObject keyObj = CreateKey(keyData, centerPosition);
                    
                    // Avanzar X por el ancho total de la tecla
                    currentX += keyWidth + keySpacing;
                    keyIndex++;
                }
            }
            
            // Add bottom row (space bar, etc.)
            currentY -= (baseKeyHeight + keySpacing);
            currentX = 0;
            
            // Numbers/Symbols key
            if (keyIndex < _currentKeys.Count)
            {
                KeyboardKey numbersKey = _currentKeys[keyIndex++];
                float keyWidth = baseKeyWidth * numbersKey.width;
                float keyHeight = baseKeyHeight * numbersKey.height;
                
                Vector3 centerPosition = new Vector3(
                    currentX + (keyWidth / 2f),
                    currentY - (keyHeight / 2f),
                    0
                );
                
                CreateKey(numbersKey, centerPosition);
                currentX += keyWidth + keySpacing;
            }
            
            // Space bar
            if (keyIndex < _currentKeys.Count)
            {
                KeyboardKey spaceKey = _currentKeys[keyIndex++];
                float keyWidth = baseKeyWidth * spaceKey.width;
                float keyHeight = baseKeyHeight * spaceKey.height;
                
                Vector3 centerPosition = new Vector3(
                    currentX + (keyWidth / 2f),
                    currentY - (keyHeight / 2f),
                    0
                );
                
                CreateKey(spaceKey, centerPosition);
                currentX += keyWidth + keySpacing;
            }
            
            // Enter key
            if (keyIndex < _currentKeys.Count)
            {
                KeyboardKey enterKey = _currentKeys[keyIndex++];
                float keyWidth = baseKeyWidth * enterKey.width;
                float keyHeight = baseKeyHeight * enterKey.height;
                
                Vector3 centerPosition = new Vector3(
                    currentX + (keyWidth / 2f),
                    currentY - (keyHeight / 2f),
                    0
                );
                
                CreateKey(enterKey, centerPosition);
            }
        }
        
        private void GenerateNumericPad()
        {
            int cols = 3;
            int keyIndex = 0;
            
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (keyIndex >= _currentKeys.Count) break;
                    
                    KeyboardKey keyData = _currentKeys[keyIndex];
                    
                    float keyWidth = baseKeyWidth * keyData.width;
                    float keyHeight = baseKeyHeight * keyData.height;
                    
                    // Calcular posición centrada
                    float x = col * (baseKeyWidth + keySpacing) + (keyWidth / 2f);
                    float y = -row * (baseKeyHeight + keySpacing) - (keyHeight / 2f);
                    
                    CreateKey(keyData, new Vector3(x, y, 0));
                    
                    keyIndex++;
                }
            }
        }
        
        private void GenerateSymbolsGrid()
        {
            int cols = 10;
            int keyIndex = 0;
            int row = 0;
            int col = 0;
            
            foreach (var keyData in _currentKeys)
            {
                float keyWidth = baseKeyWidth * keyData.width;
                float keyHeight = baseKeyHeight * keyData.height;
                
                // Calcular posición centrada
                float x = col * (baseKeyWidth + keySpacing) + (keyWidth / 2f);
                float y = -row * (baseKeyHeight + keySpacing) - (keyHeight / 2f);
                
                CreateKey(keyData, new Vector3(x, y, 0));
                
                col++;
                if (col >= cols)
                {
                    col = 0;
                    row++;
                }
            }
        }
        
        private GameObject CreateKey(KeyboardKey keyData, Vector3 centerPosition)
        {
            GameObject keyObj = Instantiate(keyPrefab, keysContainer);
            
            // Configurar RectTransform
            RectTransform rectTransform = keyObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Establecer tamaño usando width y height
                Vector2 keySize = new Vector2(
                    baseKeyWidth * keyData.width,
                    baseKeyHeight * keyData.height
                );
                rectTransform.sizeDelta = keySize;
                
                // Posicionar en el centro calculado
                rectTransform.anchoredPosition = new Vector2(centerPosition.x, centerPosition.y);
                
                // Asegurar que el pivot esté en el centro (0.5, 0.5)
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }
            else
            {
                // Fallback si no tiene RectTransform
                keyObj.transform.localPosition = centerPosition;
                Debug.LogWarning($"Key prefab doesn't have RectTransform! Using transform position.");
            }
            
            // Actualizar BoxCollider para que coincida con el tamaño de la tecla
            BoxCollider boxCollider = keyObj.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                // Calcular el tamaño del collider basado en el tamaño de la tecla
                float colliderWidth = (baseKeyWidth * keyData.width); // Convertir de UI units a world units
                float colliderHeight = (baseKeyHeight * keyData.height); 
                float colliderDepth = 0.1f; // Profundidad fija
                
                boxCollider.size = new Vector3(colliderWidth, colliderHeight, colliderDepth);
                
                // Asegurar que el centro del collider esté correcto
                boxCollider.center = Vector3.zero;
                
                Debug.Log($"Key '{keyData.character}' - Collider size: {boxCollider.size}");
            }
            else
            {
                Debug.LogWarning($"Key '{keyData.character}' doesn't have BoxCollider!");
            }
            
            // Get button component
            InteractableButton3D button = keyObj.GetComponent<InteractableButton3D>();
            if (button != null)
            {
                keyData.button = button;
                
                // Configurar contenido visual
                ConfigureKeyVisual(keyObj, keyData);
                
                // Subscribe to button events
                button.OnButtonPressed.AddListener(() => OnKeyButtonPressed(keyData));
                
                // Asegurar que el botón mantiene escala base de 1
                button.SetBaseScale(Vector3.one);
            }
            
            return keyObj;
        }
        
        private void ConfigureKeyVisual(GameObject keyObj, KeyboardKey keyData)
        {
            // Buscar componente de texto
            TextMeshProUGUI textComponent = keyObj.GetComponentInChildren<TextMeshProUGUI>();
            
            bool isSpecialKey = keyData.keyType != KeyType.Character;
            
            // Si la tecla tiene un icono, crear componente Image
            if (keyData.iconSprite != null)
            {
                // Buscar o crear contenedor para el icono
                Transform contentTransform = keyObj.transform.Find("Content");
                if (contentTransform == null)
                {
                    // Si no existe Content, usar el primer hijo o el mismo keyObj
                    contentTransform = textComponent != null ? textComponent.transform.parent : keyObj.transform;
                }
                
                // Crear nuevo GameObject para el icono
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(contentTransform, false);
                
                // Añadir y configurar componente Image
                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.sprite = keyData.iconSprite;
                iconImage.color = isSpecialKey ? specialKeyTextColor : iconColor;
                iconImage.preserveAspect = true;
                
                // Configurar RectTransform del icono
                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                
                // Calcular el tamaño óptimo del icono
                CalculateOptimalIconSize(iconRect, keyData, keyObj);
                
                // Desactivar texto si existe
                if (textComponent != null)
                {
                    textComponent.gameObject.SetActive(false);
                }
                
                // Guardar referencia al icono para poder actualizarlo después si es necesario
                keyObj.AddComponent<KeyIconReference>().iconImage = iconImage;
            }
            else if (textComponent != null)
            {
                // Usar texto si no hay icono
                string label = _isShiftActive ? keyData.shiftCharacter : keyData.character;
                textComponent.text = label;
                textComponent.color = isSpecialKey ? specialKeyTextColor : textColor;
                textComponent.gameObject.SetActive(true);
            }
            
            // Configurar material del botón si es tecla especial
            InteractableButton3D button = keyObj.GetComponent<InteractableButton3D>();
            if (button != null && isSpecialKey)
            {
                // Aquí podrías configurar diferentes materiales para teclas especiales
                // Por ejemplo, cambiar el color del background
                Renderer backgroundRenderer = keyObj.GetComponentInChildren<Renderer>();
                if (backgroundRenderer != null && specialKeyMaterial != null)
                {
                    backgroundRenderer.material = specialKeyMaterial;
                }
            }
        }
        
        private void CalculateOptimalIconSize(RectTransform iconRect, KeyboardKey keyData, GameObject keyObj)
        {
            // Obtener el tamaño del botón
            RectTransform buttonRect = keyObj.GetComponent<RectTransform>();
            if (buttonRect == null) return;
            
            Vector2 buttonSize = buttonRect.sizeDelta;
            
            // Definir márgenes según el tipo de tecla usando valores serializables
            float marginPercentage = defaultIconMargin;
            
            switch (keyData.keyType)
            {
                case KeyType.Space:
                    marginPercentage = spaceKeyIconMargin;
                    break;
                case KeyType.Enter:
                case KeyType.Backspace:
                    marginPercentage = largeKeyIconMargin;
                    break;
                case KeyType.Shift:
                case KeyType.Numbers:
                case KeyType.Symbols:
                    marginPercentage = defaultIconMargin;
                    break;
            }
            
            // Calcular el espacio disponible con márgenes
            float availableWidth = buttonSize.x * (1f - marginPercentage);
            float availableHeight = buttonSize.y * (1f - marginPercentage);
            
            // Obtener el aspect ratio del sprite
            if (keyData.iconSprite != null)
            {
                float spriteWidth = keyData.iconSprite.rect.width;
                float spriteHeight = keyData.iconSprite.rect.height;
                float spriteAspectRatio = spriteWidth / spriteHeight;
                
                // Calcular el tamaño óptimo manteniendo el aspect ratio
                float iconWidth, iconHeight;
                
                // Verificar qué dimensión es más restrictiva
                if (availableWidth / availableHeight > spriteAspectRatio)
                {
                    // La altura es más restrictiva
                    iconHeight = availableHeight;
                    iconWidth = iconHeight * spriteAspectRatio;
                }
                else
                {
                    // El ancho es más restrictivo
                    iconWidth = availableWidth;
                    iconHeight = iconWidth / spriteAspectRatio;
                }
                
                // Aplicar el tamaño calculado
                iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);
                
                // Debug para verificar tamaños
                if (Application.isEditor)
                {
                    Debug.Log($"Key '{keyData.character}' - Button: {buttonSize}, Icon: {iconRect.sizeDelta}, Margin: {marginPercentage * 100}%");
                }
            }
            else
            {
                // Fallback si no hay sprite
                iconRect.sizeDelta = new Vector2(availableWidth, availableHeight);
            }
        }
        
        private void OnKeyButtonPressed(KeyboardKey keyData)
        {
            Debug.Log($"[VirtualKeyboard3D] Key pressed: {keyData.character}");
            
            // Play sound
            PlayKeySound(keyData.keyType == KeyType.Character);
            
            // Trigger haptics
            TriggerHapticFeedback();
            
            // Handle key press based on type
            switch (keyData.keyType)
            {
                case KeyType.Character:
                    char charToSend = _isShiftActive ? keyData.shiftCharacter[0] : keyData.character[0];
                    
                    if (_isCapsLockActive && char.IsLetter(charToSend))
                    {
                        charToSend = char.ToUpper(charToSend);
                    }
                    
                    OnKeyPressed?.Invoke(charToSend);
                    
                    // Auto-disable shift after character (unless caps lock)
                    if (_isShiftActive && !_isCapsLockActive)
                    {
                        SetShiftState(false);
                    }
                    break;
                    
                case KeyType.Backspace:
                    OnBackspace?.Invoke();
                    break;
                    
                case KeyType.Enter:
                    OnEnter?.Invoke();
                    break;
                    
                case KeyType.Space:
                    OnSpace?.Invoke();
                    break;
                    
                case KeyType.Shift:
                    ToggleShift();
                    break;
                    
                case KeyType.Numbers:
                    SwitchLayout(KeyboardLayout.Numeric);
                    break;
                    
                case KeyType.Symbols:
                    if (currentLayout == KeyboardLayout.Numeric)
                    {
                        SwitchLayout(KeyboardLayout.Alphabetic);
                    }
                    else
                    {
                        SwitchLayout(KeyboardLayout.Symbols);
                    }
                    break;
                    
                case KeyType.Close:
                    OnClose?.Invoke();
                    break;
            }
        }
        
        private void ToggleShift()
        {
            // Check for double-tap for caps lock
            if (_isShiftActive)
            {
                _isCapsLockActive = !_isCapsLockActive;
                UpdateCapsLockIndicator();
            }
            
            SetShiftState(!_isShiftActive);
        }
        
        private void SetShiftState(bool active)
        {
            _isShiftActive = active;
            UpdateShiftIndicator();
            
            // Update all character keys
            foreach (var key in _currentKeys)
            {
                if (key.keyType == KeyType.Character && key.button != null)
                {
                    GameObject keyObj = key.button.gameObject;
                    
                    // Solo actualizar si la tecla usa texto (no tiene icono)
                    KeyIconReference iconRef = keyObj.GetComponent<KeyIconReference>();
                    if (iconRef == null || iconRef.iconImage == null)
                    {
                        TextMeshProUGUI textComponent = keyObj.GetComponentInChildren<TextMeshProUGUI>();
                        
                        if (textComponent != null && textComponent.gameObject.activeSelf)
                        {
                            string label = active ? key.shiftCharacter : key.character;
                            if (_isCapsLockActive && label.Length == 1 && char.IsLetter(label[0]))
                            {
                                label = label.ToUpper();
                            }
                            textComponent.text = label;
                        }
                    }
                }
                else if (key.keyType == KeyType.Shift && key.button != null)
                {
                    // Actualizar visual del botón Shift si es necesario
                    GameObject keyObj = key.button.gameObject;
                    KeyIconReference iconRef = keyObj.GetComponent<KeyIconReference>();
                    
                    if (iconRef != null && iconRef.iconImage != null)
                    {
                        // Cambiar color del icono shift cuando está activo
                        iconRef.iconImage.color = active ? Color.yellow : specialKeyTextColor;
                        
                        if (_isCapsLockActive)
                        {
                            iconRef.iconImage.color = Color.green;
                        }
                    }
                }
            }
        }
        
        private void UpdateShiftIndicator()
        {
            if (shiftIndicator != null)
            {
                shiftIndicator.gameObject.SetActive(_isShiftActive);
                shiftIndicator.color = _isCapsLockActive ? Color.green : Color.yellow;
            }
        }
        
        private void UpdateCapsLockIndicator()
        {
            if (capsLockIndicator != null)
            {
                capsLockIndicator.gameObject.SetActive(_isCapsLockActive);
            }
        }
        
        private void PlayKeySound(bool isCharacterKey)
        {
            if (audioSource == null) return;
            
            AudioClip clipToPlay = isCharacterKey ? keyPressSound : specialKeySound;
            if (clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay, soundVolume);
            }
        }
        
        private void TriggerHapticFeedback()
        {
            // Get active controller from interaction
            // This is simplified - in practice you'd determine which controller is pointing at the keyboard
            OVRInput.SetControllerVibration(1, hapticIntensity, OVRInput.Controller.RTouch);
            
            DOVirtual.DelayedCall(hapticDuration, () => OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch));
        }
        
        private void ClearCurrentKeys()
        {
            if (_currentKeys != null)
            {
                foreach (var key in _currentKeys)
                {
                    if (key.button != null)
                    {
                        key.button.OnButtonPressed.RemoveAllListeners();
                    }
                }
                _currentKeys.Clear();
            }
        }
        
        public void Show()
        {
            gameObject.SetActive(true);
            transform.DOScale(Vector3.one, 0.3f).From(Vector3.zero).SetEase(Ease.OutBack);
        }
        
        public void Hide()
        {
            transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() => gameObject.SetActive(false));
        }
        
        private void OnDestroy()
        {
            ClearCurrentKeys();
        }
        
        #region Public Methods for External Control
        
        public void TypeCharacter(char c)
        {
            OnKeyPressed?.Invoke(c);
        }
        
        public void TypeBackspace()
        {
            OnBackspace?.Invoke();
        }
        
        public void TypeEnter()
        {
            OnEnter?.Invoke();
        }
        
        public void SetCapsLock(bool enabled)
        {
            _isCapsLockActive = enabled;
            UpdateCapsLockIndicator();
            SetShiftState(_isShiftActive); // Refresh keys
        }
        
        #endregion
    }
}