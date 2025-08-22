using UnityEngine;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Bootstrapper para asegurar que VirtualKeyboardManager está correctamente configurado
    /// Debe colocarse en la escena principal y asignarle el prefab del teclado
    /// </summary>
    public class VirtualKeyboardManagerBootstrapper : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private VirtualKeyboard3D keyboardPrefab;
        [SerializeField] private bool createIfMissing = true;
        
        [Header("Keyboard Settings")]
        [SerializeField] private Vector3 keyboardOffset = new Vector3(0f, -0.2f, 0f);
        [SerializeField] private Vector3 keyboardSize = Vector3.one;
        [SerializeField] private float keyboardPosition = 0.2f;
        
        private void Awake()
        {
            // Verificar si ya existe un VirtualKeyboardManager
            VirtualKeyboardManager manager = VirtualKeyboardManager.Instance;
            
            if (manager == null && createIfMissing)
            {
                // Crear el VirtualKeyboardManager
                GameObject managerObj = new GameObject("VirtualKeyboardManager");
                manager = managerObj.AddComponent<VirtualKeyboardManager>();
                
                // Configurar los settings antes de asignar el prefab
                manager.ConfigureKeyboardSettings(keyboardOffset, keyboardSize, keyboardPosition);
                
                // Importante: Asignar el prefab después de configurar los settings
                if (keyboardPrefab != null)
                {
                    manager.SetKeyboardPrefab(keyboardPrefab);
                }
                else
                {
                    Debug.LogError("VirtualKeyboardManagerBootstrapper: No keyboard prefab assigned! Please assign a VirtualKeyboard3D prefab.");
                }
                
                // Hacer el objeto persistente
                DontDestroyOnLoad(managerObj);
            }
            else if (manager != null)
            {
                // Si el manager ya existe, actualizar sus configuraciones si es necesario
                if (!manager.IsKeyboardConfigured && keyboardPrefab != null)
                {
                    // Configurar settings y prefab
                    manager.ConfigureKeyboardSettings(keyboardOffset, keyboardSize, keyboardPosition);
                    manager.SetKeyboardPrefab(keyboardPrefab);
                }
                else
                {
                    // Solo actualizar los settings si son diferentes
                    manager.ConfigureKeyboardSettings(keyboardOffset, keyboardSize, keyboardPosition);
                }
            }
        }
    }
}