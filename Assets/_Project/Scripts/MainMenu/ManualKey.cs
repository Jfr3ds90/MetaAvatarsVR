using HackMonkeys.UI.Spatial;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class ManualKey : MonoBehaviour
{
    [System.Serializable]
    private class KeyboardKey
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
    [Header("Key Configuration")]
    [SerializeField] private string _character;
    [SerializeField] private string _shiftCharacter;
    [SerializeField] private KeyCode _keyCode;
    [SerializeField] private KeyType _keyType;
    [SerializeField] private float _width = 1f; // Multiplicador de ancho para teclas especiales
    [SerializeField] private float _height = 1f; // Multiplicador de alto para teclas especiales
    [SerializeField] private Sprite _iconSprite; // Icono para teclas especiales
    [SerializeField] private InteractableButton3D _button;
    private KeyboardKey Key;
    private List<KeyboardKey> CreateAlphabeticLayout()
    {
        List<KeyboardKey> keys = new List<KeyboardKey>();
        Key.character = _character;
        Key.shiftCharacter = _shiftCharacter;
        Key.keyCode = _keyCode;
        Key.keyType = _keyType;
        Key.width = _width;
        Key.height = _height;
        Key.iconSprite = _iconSprite;
        Key.button = _button;
        return keys;
    }
}
