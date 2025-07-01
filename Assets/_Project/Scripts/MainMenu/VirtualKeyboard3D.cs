using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Oculus.Interaction;

namespace HackMonkeys.UI.Spatial
{
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
        [SerializeField] private GameObject keyPrefab; // Prefab con InteractableButton3D
        [SerializeField] private float keySize = 0.08f;
        [SerializeField] private float keySpacing = 0.01f;
        [SerializeField] private KeyboardLayout currentLayout = KeyboardLayout.Alphabetic;
        
        [Header("Visual Settings")]
        [SerializeField] private Material normalKeyMaterial;
        [SerializeField] private Material specialKeyMaterial;
        [SerializeField] private Material pressedKeyMaterial;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color specialKeyTextColor = Color.cyan;
        
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
                            width = 1.5f
                        });
                    }
                    else if (c == '<') // Backspace key
                    {
                        keys.Add(new KeyboardKey
                        {
                            character = "⌫",
                            shiftCharacter = "⌫",
                            keyType = KeyType.Backspace,
                            width = 1.5f
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
                            keyType = KeyType.Character
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
                width = 1.5f
            });
            
            keys.Add(new KeyboardKey
            {
                character = "Space",
                shiftCharacter = "Space",
                keyType = KeyType.Space,
                width = 5f
            });
            
            keys.Add(new KeyboardKey
            {
                character = "Enter",
                shiftCharacter = "Enter",
                keyType = KeyType.Enter,
                width = 2f
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
                    keyType = KeyType.Character
                });
            }
            
            // Add special keys
            keys.Add(new KeyboardKey
            {
                character = ".",
                shiftCharacter = ",",
                keyType = KeyType.Character
            });
            
            keys.Add(new KeyboardKey
            {
                character = "⌫",
                shiftCharacter = "⌫",
                keyType = KeyType.Backspace,
                width = 1.5f
            });
            
            keys.Add(new KeyboardKey
            {
                character = "ABC",
                shiftCharacter = "ABC",
                keyType = KeyType.Symbols, // Reuse for returning to alpha
                width = 1.5f
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
                    keyType = KeyType.Character
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
            int currentRow = 0;
            
            float[] rowOffsets = { 0f, 0.5f, 0.75f, 0f }; // Indentation for each row
            
            for (int row = 0; row < QWERTY_LAYOUT.Length; row++)
            {
                currentX = rowOffsets[row] * (keySize + keySpacing);
                currentY = -row * (keySize + keySpacing);
                
                int keysInRow = QWERTY_LAYOUT[row].Length;
                
                for (int col = 0; col < keysInRow; col++)
                {
                    if (keyIndex >= _currentKeys.Count) break;
                    
                    KeyboardKey keyData = _currentKeys[keyIndex];
                    GameObject keyObj = CreateKey(keyData, new Vector3(currentX, currentY, 0));
                    
                    currentX += (keySize + keySpacing) * keyData.width;
                    keyIndex++;
                }
            }
            
            // Add bottom row (space bar, etc.)
            currentY -= (keySize + keySpacing);
            currentX = 0;
            
            // Numbers/Symbols key
            if (keyIndex < _currentKeys.Count)
            {
                KeyboardKey numbersKey = _currentKeys[keyIndex++];
                CreateKey(numbersKey, new Vector3(currentX, currentY, 0));
                currentX += (keySize + keySpacing) * numbersKey.width;
            }
            
            // Space bar
            if (keyIndex < _currentKeys.Count)
            {
                KeyboardKey spaceKey = _currentKeys[keyIndex++];
                CreateKey(spaceKey, new Vector3(currentX, currentY, 0));
                currentX += (keySize + keySpacing) * spaceKey.width;
            }
            
            // Enter key
            if (keyIndex < _currentKeys.Count)
            {
                KeyboardKey enterKey = _currentKeys[keyIndex++];
                CreateKey(enterKey, new Vector3(currentX, currentY, 0));
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
                    
                    float x = col * (keySize + keySpacing);
                    float y = -row * (keySize + keySpacing);
                    
                    KeyboardKey keyData = _currentKeys[keyIndex];
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
                float x = col * (keySize + keySpacing);
                float y = -row * (keySize + keySpacing);
                
                CreateKey(keyData, new Vector3(x, y, 0));
                
                col++;
                if (col >= cols)
                {
                    col = 0;
                    row++;
                }
            }
        }
        
        private GameObject CreateKey(KeyboardKey keyData, Vector3 localPosition)
        {
            GameObject keyObj = Instantiate(keyPrefab, keysContainer);
            keyObj.transform.localPosition = localPosition;
            
            // Scale key based on width
            Vector3 scale = keyObj.transform.localScale;
            scale.x *= keyData.width;
            keyObj.transform.localScale = scale;
            
            // Get button component
            InteractableButton3D button = keyObj.GetComponent<InteractableButton3D>();
            if (button != null)
            {
                keyData.button = button;
                
                // Set button label
                string label = _isShiftActive ? keyData.shiftCharacter : keyData.character;
                button.SetButtonLabel(label);
                
                // Configure button appearance
                bool isSpecialKey = keyData.keyType != KeyType.Character;
                
                // Subscribe to button events
                button.OnButtonPressed.AddListener(() => OnKeyButtonPressed(keyData));
            }
            
            return keyObj;
        }
        
        private void OnKeyButtonPressed(KeyboardKey keyData)
        {
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
                    string label = active ? key.shiftCharacter : key.character;
                    if (_isCapsLockActive && label.Length == 1 && char.IsLetter(label[0]))
                    {
                        label = label.ToUpper();
                    }
                    key.button.SetButtonLabel(label);
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