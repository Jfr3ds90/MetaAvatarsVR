using UnityEngine;
using Oculus.Avatar2;
using System.Collections;

[RequireComponent(typeof(OvrAvatarEntity))]
public class AvatarTrackingInitializer : MonoBehaviour
{
    [Tooltip("Espera adicional después de la inicialización antes de reiniciar el tracking (segundos)")]
    [SerializeField] private float _initializationDelay = 1.0f;
    
    [Tooltip("Habilitar logs detallados para depuración")]
    [SerializeField] private bool _debugLogs = false;
    
    [SerializeField] private OvrAvatarEntity _avatarEntity;
    public SampleInputManager _inputManager;
    
    private void Awake()
    {
        //_avatarEntity = GetComponent<OvrAvatarEntity>();
    }
    
    private void Start()
    {
        StartCoroutine(InitializeBodyTrackingAfterDelay());
    }
    
    private IEnumerator InitializeBodyTrackingAfterDelay()
    {
        // Esperar a que el avatar termine de inicializarse
        if (_debugLogs) Debug.Log($"<color=green>[AvatarTrackingInitializer] Esperando inicialización del avatar...</Color>");
        
        while (!_avatarEntity.IsCreated || _avatarEntity.IsPendingAvatar)
        {
            yield return null;
        }
        
        // Esperar un poco más para que todo se estabilice
        if (_debugLogs) Debug.Log($"<color=green>[AvatarTrackingInitializer] Avatar inicializado. Esperando {_initializationDelay} segundos adicionales...</Color>");
        yield return new WaitForSeconds(_initializationDelay);
        
        // Buscar el SampleInputManager
        _inputManager = FindSampleInputManager();
        
        if (_inputManager != null)
        {
            if (_debugLogs) Debug.Log("<Color=green>[AvatarTrackingInitializer] Reiniciando el sistema de body tracking...</Color>");
            
            // Guardar el modo actual
            var currentMode = _inputManager.BodyTrackingMode;
            
            // Cambiar a None para forzar un reinicio, luego volver al modo original
            _inputManager.BodyTrackingMode = OvrAvatarBodyTrackingMode.None;
            
            // Esperar un frame para procesar el cambio
            yield return null;
            
            // Restaurar el modo original
            _inputManager.BodyTrackingMode = currentMode;
            
            if (_debugLogs) Debug.Log("<Color=green>[AvatarTrackingInitializer] Reinicio completado.</Color>");
            
            // Esperar y verificar el estado del tracking
            yield return new WaitForSeconds(0.5f);
            if (_debugLogs) Debug.Log($"<Color=green>[AvatarTrackingInitializer] Estado del tracking después del reinicio: {_avatarEntity.TrackingPoseValid}</color>");
        }
        else
        {
            Debug.LogWarning("<color=green>[AvatarTrackingInitializer] No se pudo encontrar el SampleInputManager.</color>");
        }
    }
    
    private SampleInputManager FindSampleInputManager()
    {
        // Intentar obtener desde el InputManager del avatar
        if (_avatarEntity.InputManager is SampleInputManager sampleManager)
        {
            return sampleManager;
        }
        
        // Buscar en la escena
        return FindObjectOfType<SampleInputManager>();
    }
}