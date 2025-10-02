using System.Linq;
using UnityEngine;
using Fusion;
using System.Threading.Tasks;
using HackMonkeys.Gameplay;
using System.Threading;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Inicializador de escena de gameplay que maneja el spawn del GameplayManager
    /// y prepara el VR Rig local para auto-detección
    /// </summary>
    public class GameplaySceneInitializer : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private NetworkObject gameplayManagerPrefab;
        [SerializeField] private float initializationTimeout = 10f;
        [SerializeField] private bool debugMode = true;

        [Header("Local VR Reference")]
        [SerializeField] private GameObject localVRRig; // OVRCameraRig existente en la escena
        
        [Header("Spawn de Jugadores")]
        [SerializeField] private bool autoSpawnExistingPlayers = true;
        [SerializeField] private float playerSpawnDelay = 0.5f;

        private CancellationTokenSource _cancellationTokenSource;

        private void Awake()
        {
            // Validar y configurar VR Rig
            if (localVRRig == null)
            {
                Debug.LogWarning("[GameplaySceneInitializer] ⚠️ Local VR Rig no asignado, buscando en la escena...");
                localVRRig = FindObjectOfType<OVRCameraRig>()?.gameObject;
                
                if (localVRRig != null)
                {
                    Debug.Log("[GameplaySceneInitializer] ✅ OVRCameraRig encontrado en la escena");
                }
            }
            
            // Asignar tag para auto-detección
            if (localVRRig != null && !localVRRig.CompareTag("LocalVRRig"))
            {
                localVRRig.tag = "LocalVRRig";
                Debug.Log("[GameplaySceneInitializer] 🏷️ VR Rig etiquetado como 'LocalVRRig'");
            }
        }

        private async void Start()
        {
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🎮 Iniciando inicialización de escena de gameplay...");

            // Validación inicial
            if (gameplayManagerPrefab == null)
            {
                Debug.LogError("[GameplaySceneInitializer] ❌ GameplayManager prefab no asignado!");
                Destroy(gameObject);
                return;
            }

            // Crear token de cancelación
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await InitializeGameplayScene(_cancellationTokenSource.Token);
            }
            catch (System.OperationCanceledException)
            {
                Debug.LogWarning("[GameplaySceneInitializer] ⚠️ Inicialización cancelada");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameplaySceneInitializer] ❌ Error durante inicialización: {e.Message}");
            }
            finally
            {
                // Limpiar
                Destroy(gameObject);
            }
        }

        private async Task InitializeGameplayScene(CancellationToken cancellationToken)
        {
            // 1. Esperar por NetworkRunner
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 📡 Esperando NetworkRunner...");

            NetworkRunner runner = await WaitForNetworkRunner(cancellationToken);
            if (runner == null)
            {
                Debug.LogError("[GameplaySceneInitializer] ❌ No se encontró NetworkRunner después del timeout");
                return;
            }

            if (debugMode)
                Debug.Log($"[GameplaySceneInitializer] ✅ NetworkRunner encontrado - IsServer: {runner.IsServer}");

            // 2. Bifurcación según modo de red
            if (runner.GameMode == GameMode.Shared)
            {
                await HandleSharedModeInitialization(runner, cancellationToken);
            }
            else if (runner.IsServer)
            {
                await HandleHostInitialization(runner, cancellationToken);
            }
            else
            {
                PrepareClientVRRig();
            }
        }

        #region Shared Mode Logic
        private async Task HandleSharedModeInitialization(NetworkRunner runner, CancellationToken cancellationToken)
        {
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🤝 SHARED MODE: Inicializando...");
            
            await WaitForSimulationReady(runner, cancellationToken);
            
            // En Shared Mode, verificar si ya existe un GameplayManager
            var existingManager = FindObjectOfType<GameplayManager>();
            
            if (existingManager == null)
            {
                // En Shared Mode, usar una estrategia más robusta para determinar quién spawna
                // Opción 1: El jugador con el PlayerId más bajo que esté activo spawna
                // Opción 2: Si somos el único jugador o el primero, spawneamos
                bool shouldSpawn = false;
                
                // Verificar si somos el jugador con el PlayerId más bajo activo
                var activePlayersList = runner.ActivePlayers.ToList();
                if (activePlayersList.Count > 0)
                {
                    activePlayersList.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
                    shouldSpawn = activePlayersList[0] == runner.LocalPlayer;
                    
                    if (debugMode) 
                    {
                        Debug.Log($"[GameplaySceneInitializer] 📊 SHARED: Active players: {activePlayersList.Count}");
                        Debug.Log($"[GameplaySceneInitializer] 📊 SHARED: Local PlayerId: {runner.LocalPlayer.PlayerId}");
                        Debug.Log($"[GameplaySceneInitializer] 📊 SHARED: Lowest PlayerId: {activePlayersList[0].PlayerId}");
                        Debug.Log($"[GameplaySceneInitializer] 📊 SHARED: Should spawn: {shouldSpawn}");
                    }
                }
                else
                {
                    // Si no hay jugadores activos (raro), intentamos spawnear
                    shouldSpawn = true;
                    if (debugMode) Debug.Log("[GameplaySceneInitializer] 📊 SHARED: No active players detected, attempting spawn");
                }
                
                if (shouldSpawn)
                {
                    if (debugMode) Debug.Log("[GameplaySceneInitializer] 🎯 SHARED: Spawneando GameplayManager...");
                    
                    // Pequeña espera para evitar condiciones de carrera
                    await Task.Delay(100, cancellationToken);
                    
                    // Doble verificación antes de spawnear
                    existingManager = FindObjectOfType<GameplayManager>();
                    if (existingManager == null)
                    {
                        NetworkObject spawnedManager = await SpawnGameplayManager(runner);
                        
                        if (spawnedManager != null)
                        {
                            NotifyGameCore();
                            if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ SHARED: GameplayManager spawneado y listo");
                        }
                        else
                        {
                            Debug.LogError("[GameplaySceneInitializer] ❌ SHARED: Fallo al spawnear GameplayManager");
                        }
                    }
                    else
                    {
                        if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ SHARED: GameplayManager ya fue spawneado por otro jugador");
                        NotifyGameCore();
                    }
                }
                else
                {
                    // Esperar a que otro jugador lo spawne
                    if (debugMode) Debug.Log("[GameplaySceneInitializer] 👥 SHARED: Esperando que otro jugador spawne el GameplayManager...");
                    await WaitForGameplayManager(cancellationToken);
                    NotifyGameCore();
                }
            }
            else
            {
                if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ SHARED: GameplayManager ya existe");
                NotifyGameCore();
            }
            
            PrepareClientVRRig();
            
            // En modo Shared, manejar el spawn de jugadores existentes
            var gameplayManager = GameplayManager.Instance;
            if (gameplayManager != null)
            {
                if (debugMode) Debug.Log("[GameplaySceneInitializer] 🎮 SHARED: Procesando jugadores para spawn...");
                
                // Esperar un poco para asegurar que todo esté listo
                await Task.Delay(500, cancellationToken);
                
                // Procesar todos los jugadores activos (incluyéndonos)
                foreach (var player in runner.ActivePlayers)
                {
                    if (player.IsRealPlayer)
                    {
                        if (debugMode) Debug.Log($"[GameplaySceneInitializer] 👤 SHARED: Notificando GameplayManager sobre jugador {player}");
                        gameplayManager.PlayerJoined(player);
                        await Task.Delay(100, cancellationToken);
                    }
                }
                
                if (debugMode) Debug.Log($"[GameplaySceneInitializer] ✅ SHARED: {runner.ActivePlayers.Count()} jugadores procesados");
            }
            else
            {
                Debug.LogError("[GameplaySceneInitializer] ❌ SHARED: GameplayManager.Instance es null!");
            }
            
            if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ SHARED: Inicialización completa");
        }
        #endregion
        
        #region Host Logic
        private async Task HandleHostInitialization(NetworkRunner runner, CancellationToken cancellationToken)
        {
            // Esperar a que la simulación esté activa
            if (debugMode) Debug.Log("[GameplaySceneInitializer] ⏳ HOST: Esperando simulación activa...");
            await WaitForSimulationReady(runner, cancellationToken);

            // Verificar si GameplayManager ya existe
            var existingManager = FindObjectOfType<GameplayManager>();
            if (existingManager != null)
            {
                Debug.LogWarning("[GameplaySceneInitializer] ⚠️ GameplayManager ya existe en la escena");
                return;
            }

            // Spawn del GameplayManager
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🎯 Spawneando GameplayManager...");
            NetworkObject spawnedManager = await SpawnGameplayManager(runner);

            if (spawnedManager != null)
            {
                if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ GameplayManager spawneado exitosamente");

                // Notificar a GameCore
                NotifyGameCore();

                // Manejar jugadores existentes (esto creará los NetworkPlayers)
                if (autoSpawnExistingPlayers)
                {
                    await HandleExistingPlayers(runner, cancellationToken);
                }
                
                // El NetworkPlayer del host se auto-configurará cuando sea spawneado
                if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ HOST: Inicialización completa");
            }
        }
        #endregion

        #region Client Logic
        private void PrepareClientVRRig()
        {
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 👤 CLIENTE: Preparando VR Rig para auto-detección...");
            
            // Asegurar que el VR Rig tenga el tag correcto
            if (localVRRig != null && !localVRRig.CompareTag("LocalVRRig"))
            {
                localVRRig.tag = "LocalVRRig";
                Debug.Log("[GameplaySceneInitializer] ✅ CLIENTE: VR Rig etiquetado correctamente");
            }
            
            // El NetworkPlayer se auto-configurará cuando sea spawneado
            Debug.Log("[GameplaySceneInitializer] ✅ CLIENTE: Esperando NetworkPlayer para auto-configuración");
        }
        #endregion

        #region Wait Methods
        private async Task WaitForGameplayManager(CancellationToken cancellationToken)
        {
            float elapsedTime = 0f;
            float timeout = 10f; // Aumentar timeout a 10 segundos
            int checkCount = 0;
            
            while (elapsedTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var manager = FindObjectOfType<GameplayManager>();
                if (manager != null && GameplayManager.Instance != null)
                {
                    if (debugMode) Debug.Log($"[GameplaySceneInitializer] ✅ GameplayManager encontrado y listo después de {checkCount} verificaciones ({elapsedTime:F1}s)");
                    return;
                }
                
                checkCount++;
                if (debugMode && checkCount % 10 == 0) // Log cada 10 verificaciones (1 segundo)
                {
                    Debug.Log($"[GameplaySceneInitializer] ⏳ Esperando GameplayManager... ({elapsedTime:F1}s / {timeout:F1}s)");
                }
                
                await Task.Delay(100);
                elapsedTime += 0.1f;
            }
            
            // Si llegamos aquí, intentar spawnearlo nosotros como fallback
            Debug.LogWarning($"[GameplaySceneInitializer] ⚠️ Timeout esperando GameplayManager después de {timeout}s");
            Debug.Log("[GameplaySceneInitializer] 🔄 Intentando spawnear GameplayManager como fallback...");
            
            var runner = NetworkRunner.GetRunnerForScene(gameObject.scene);
            if (runner != null && runner.IsRunning)
            {
                var spawnedManager = await SpawnGameplayManager(runner);
                if (spawnedManager != null)
                {
                    Debug.Log("[GameplaySceneInitializer] ✅ GameplayManager spawneado exitosamente como fallback");
                }
                else
                {
                    Debug.LogError("[GameplaySceneInitializer] ❌ Fallo crítico: No se pudo spawnear GameplayManager incluso como fallback");
                }
            }
        }
        
        private async Task WaitForGameplayManagerReady(GameplayManager manager, CancellationToken cancellationToken)
        {
            float elapsedTime = 0f;
            float timeout = 2f;
            
            while (elapsedTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Verificar que el Instance esté asignado (indica que Awake se ejecutó)
                if (GameplayManager.Instance == manager)
                {
                    // Pequeña espera adicional para asegurar que Spawned() se complete
                    await Task.Delay(100);
                    if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ GameplayManager completamente inicializado");
                    return;
                }
                
                await Task.Delay(50);
                elapsedTime += 0.05f;
            }
            
            Debug.LogWarning("[GameplaySceneInitializer] ⚠️ Timeout esperando que GameplayManager esté listo");
        }
        
        private async Task<NetworkRunner> WaitForNetworkRunner(CancellationToken cancellationToken)
        {
            float elapsedTime = 0f;

            while (elapsedTime < initializationTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Por la escena actual
                NetworkRunner runner = NetworkRunner.GetRunnerForScene(gameObject.scene);
                if (runner != null && runner.IsRunning)
                {
                    return runner;
                }

                // Primera instancia activa
                if (NetworkRunner.Instances.Count > 0)
                {
                    foreach (var instance in NetworkRunner.Instances)
                    {
                        if (instance != null && instance.IsRunning)
                        {
                            return instance;
                        }
                    }
                }

                await Task.Delay(100);
                elapsedTime += 0.1f;
            }

            return null;
        }

        private async Task WaitForSimulationReady(NetworkRunner runner, CancellationToken cancellationToken)
        {
            while (runner.Tick <= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            int targetTick = runner.Tick + 2;
            while (runner.Tick < targetTick)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
        #endregion

        #region Spawn Methods
        private async Task<NetworkObject> SpawnGameplayManager(NetworkRunner runner)
        {
            try
            {
                // En modo Shared, todos pueden spawnear pero verificamos primero
                if (runner.GameMode == GameMode.Shared)
                {
                    if (debugMode) Debug.Log($"[GameplaySceneInitializer] 🔍 SHARED MODE - Verificando capacidad de spawn");
                    if (debugMode) Debug.Log($"[GameplaySceneInitializer] 🔍 Runner.IsRunning: {runner.IsRunning}");
                    if (debugMode) Debug.Log($"[GameplaySceneInitializer] 🔍 Runner.Tick: {runner.Tick}");
                    if (debugMode) Debug.Log($"[GameplaySceneInitializer] 🔍 LocalPlayer: {runner.LocalPlayer}");
                }
                else if (!runner.CanSpawn)
                {
                    Debug.LogError("[GameplaySceneInitializer] ❌ Runner no puede spawnear objetos!");
                    return null;
                }

                // En modo Shared, usar Spawn sin InputAuthority específico para objetos de scene
                NetworkObject spawnedObject = null;
                
                if (runner.GameMode == GameMode.Shared)
                {
                    // Para objetos de scene en Shared Mode, no especificar InputAuthority
                    spawnedObject = runner.Spawn(
                        gameplayManagerPrefab,
                        Vector3.zero,
                        Quaternion.identity,
                        PlayerRef.None  // Sin autoridad específica para el GameplayManager
                    );
                }
                else
                {
                    // Para otros modos, usar SpawnAsync
                    spawnedObject = await runner.SpawnAsync(
                        gameplayManagerPrefab,
                        Vector3.zero,
                        Quaternion.identity
                    );
                }

                if (spawnedObject != null)
                {
                    if (debugMode) Debug.Log($"[GameplaySceneInitializer] ✅ NetworkObject spawneado: {spawnedObject.name}");
                    
                    var gm = spawnedObject.GetComponent<GameplayManager>();
                    if (gm != null)
                    {
                        if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ GameplayManager component encontrado");
                        
                        // Esperar a que el GameplayManager complete su inicialización
                        await WaitForGameplayManagerReady(gm, _cancellationTokenSource.Token);
                        return spawnedObject;
                    }
                    else
                    {
                        Debug.LogError("[GameplaySceneInitializer] ❌ GameplayManager component no encontrado en el prefab!");
                    }
                }
                else
                {
                    Debug.LogError("[GameplaySceneInitializer] ❌ Fallo al spawnear NetworkObject!");
                }

                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameplaySceneInitializer] ❌ Excepción en SpawnGameplayManager: {e.Message}");
                Debug.LogError($"[GameplaySceneInitializer] ❌ StackTrace: {e.StackTrace}");
                return null;
            }
        }
        #endregion

        #region Helper Methods
        private void NotifyGameCore()
        {
            var gameCore = GameCore.Instance;
            if (gameCore != null && gameCore.CurrentState == GameCore.GameState.LoadingMatch)
            {
                gameCore.OnGameSceneLoaded();
            }
        }

        private async Task HandleExistingPlayers(NetworkRunner runner, CancellationToken cancellationToken)
        {
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 👥 Manejando jugadores existentes...");

            await Task.Delay((int)(playerSpawnDelay * 1000), cancellationToken);

            var gameplayManager = GameplayManager.Instance;
            if (gameplayManager == null) return;

            foreach (var player in runner.ActivePlayers)
            {
                if (player.IsRealPlayer)
                {
                    if (debugMode) Debug.Log($"[GameplaySceneInitializer] 👤 Procesando jugador: {player}");
                    gameplayManager.PlayerJoined(player);
                    await Task.Delay(100, cancellationToken);
                }
            }
            
            if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ Jugadores existentes procesados");
        }
        #endregion

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🧹 Initializer destruido");
        }

        #region Debug
        [ContextMenu("Debug: Print VR Rig Status")]
        private void DebugVRRigStatus()
        {
            Debug.Log("=== VR RIG STATUS ===");
            Debug.Log($"Local VR Rig: {localVRRig}");
            if (localVRRig != null)
            {
                Debug.Log($"  - Name: {localVRRig.name}");
                Debug.Log($"  - Tag: {localVRRig.tag}");
                Debug.Log($"  - Active: {localVRRig.activeInHierarchy}");
                Debug.Log($"  - Has OVRCameraRig: {localVRRig.GetComponent<OVRCameraRig>() != null}");
            }
            Debug.Log("====================");
        }
        #endregion
    }
}