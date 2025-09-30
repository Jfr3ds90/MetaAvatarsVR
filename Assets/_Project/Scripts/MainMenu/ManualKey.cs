using HackMonkeys.UI.Spatial;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using static HackMonkeys.UI.Spatial.VirtualKeyboard3D;

public class ManualKey : MonoBehaviour
{
   /* [System.Serializable]
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
    }*/
    [Header("Key Configuration")]
    public string _character;
    public string _shiftCharacter;
    public KeyCode _keyCode;
    public KeyType _keyType;
    public float _width = 1f; 
    public float _height = 1f; 
    public Sprite _iconSprite; 
    public InteractableButton3D _button;
    public KeyboardKey Key;
    public void SingularKey()
    {
        Key.character = _character;
        Key.shiftCharacter = _shiftCharacter;
        Key.keyCode = _keyCode;
        Key.keyType = _keyType;
        Key.width = _width;
        Key.height = _height;
        Key.iconSprite = _iconSprite;
        Key.button = _button;
    }

    /*private KeyboardKey Key;
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
    }*/
}
