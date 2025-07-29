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
    /// Se coloca en cada escena de juego y se encarga de la inicialización correcta
    /// </summary>
    public class GameplaySceneInitializer : MonoBehaviour
    {
        [Header("Configuración")] [SerializeField]
        private NetworkObject gameplayManagerPrefab;

        [SerializeField] private float initializationTimeout = 10f;
        [SerializeField] private bool debugMode = true;

        [Header("Spawn de Jugadores")] [SerializeField]
        private bool autoSpawnExistingPlayers = true;

        [SerializeField] private float playerSpawnDelay = 0.5f;

        private CancellationTokenSource _cancellationTokenSource;

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

            // 2. Solo el servidor continúa
            if (!runner.IsServer)
            {
                if (debugMode) Debug.Log("[GameplaySceneInitializer] 👤 Cliente detectado - terminando inicialización");
                return;
            }

            // 3. Esperar a que la simulación esté activa
            if (debugMode) Debug.Log("[GameplaySceneInitializer] ⏳ Esperando simulación activa...");

            await WaitForSimulationReady(runner, cancellationToken);

            if (debugMode) Debug.Log($"[GameplaySceneInitializer] ✅ Simulación lista - Tick: {runner.Tick}");

            // 4. Verificar si GameplayManager ya existe
            var existingManager = FindObjectOfType<GameplayManager>();
            if (existingManager != null)
            {
                Debug.LogWarning("[GameplaySceneInitializer] ⚠️ GameplayManager ya existe en la escena");
                return;
            }

            // 5. Spawn del GameplayManager
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🎯 Spawneando GameplayManager...");

            if (!runner.CanSpawn)
            {
                Debug.LogError("[GameplaySceneInitializer] ❌ Runner no puede spawnear objetos!");
                return;
            }
            NetworkObject spawnedManager = await SpawnGameplayManager(runner);

            if (spawnedManager != null)
            {
                if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ GameplayManager spawneado exitosamente");

                // 6. Notificar a GameCore si es necesario
                NotifyGameCore();

                // 7. Manejar jugadores existentes
                if (autoSpawnExistingPlayers)
                {
                    await HandleExistingPlayers(runner, cancellationToken);
                }
            }
            else
            {
                Debug.LogError("[GameplaySceneInitializer] ❌ Fallo al spawnear GameplayManager");
            }
        }

        private async Task<NetworkRunner> WaitForNetworkRunner(CancellationToken cancellationToken)
        {
            float elapsedTime = 0f;
            
            Debug.Log($"[GameplaySceneInitializer] Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            Debug.Log($"[GameplaySceneInitializer] NetworkRunner instances: {NetworkRunner.Instances.Count}");
    
            foreach (var runner in NetworkRunner.Instances)
            {
                Debug.Log($"[GameplaySceneInitializer] Found runner: {runner.name}, IsRunning: {runner.IsRunning}, GameMode: {runner.GameMode}");
            }

            while (elapsedTime < initializationTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Intentar obtener el runner de varias formas
                NetworkRunner runner = null;

                // Método 1: Por la escena actual
                runner = NetworkRunner.GetRunnerForScene(gameObject.scene);
                if (runner != null && runner.IsRunning)
                {
                    return runner;
                }

                // Método 2: Primera instancia activa
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

                await Task.Delay(100); // Esperar 100ms
                elapsedTime += 0.1f;

                if (debugMode && (int)(elapsedTime * 10) % 10 == 0) // Log cada segundo
                {
                    Debug.Log($"[GameplaySceneInitializer] ⏳ Esperando runner... ({elapsedTime:F1}s)");
                }
            }

            return null;
        }

        private async Task WaitForSimulationReady(NetworkRunner runner, CancellationToken cancellationToken)
        {
            // Esperar a que el tick sea mayor que 0 (simulación activa)
            while (runner.Tick <= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            // Esperar un par de ticks más para asegurar estabilidad
            int targetTick = runner.Tick + 2;
            while (runner.Tick < targetTick)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private async Task<NetworkObject> SpawnGameplayManager(NetworkRunner runner)
        {
            try
            {
                // Verificaciones previas
                Debug.Log("[GameplaySceneInitializer] 🔍 Verificando antes de spawn:");
                Debug.Log($"  - Runner válido: {runner != null}");
                Debug.Log($"  - Runner.IsRunning: {runner?.IsRunning}");
                Debug.Log($"  - Runner.IsServer: {runner?.IsServer}");
                Debug.Log($"  - Prefab válido: {gameplayManagerPrefab != null}");

                if (gameplayManagerPrefab != null)
                {
                    Debug.Log($"  - Prefab name: {gameplayManagerPrefab.name}");
                    Debug.Log($"  - Has NetworkObject: {gameplayManagerPrefab.GetComponent<NetworkObject>() != null}");
                    Debug.Log($"  - Has GameplayManager: {gameplayManagerPrefab.GetComponent<GameplayManager>() != null}");
                }
                
                if (!runner.CanSpawn)
                {
                    Debug.LogError("[GameplaySceneInitializer] ❌ Runner no puede spawnear objetos!");
                    return null;
                }
                
                // Intentar spawn con más información
                Debug.Log("[GameplaySceneInitializer] 📡 Llamando a SpawnAsync...");

                var spawnTask = runner.SpawnAsync(
                    gameplayManagerPrefab,
                    Vector3.zero,
                    Quaternion.identity,
                    onBeforeSpawned: (runner, obj) =>
                    {
                        Debug.Log("[GameplaySceneInitializer] 📦 Pre-spawn callback ejecutado");
                        Debug.Log($"  - Object: {obj}");
                        Debug.Log($"  - Object name: {obj?.name}");
                    }
                );

                Debug.Log("[GameplaySceneInitializer] ⏳ Esperando resultado de SpawnAsync...");
                NetworkObject spawnedObject = await spawnTask;

                Debug.Log($"[GameplaySceneInitializer] 📦 SpawnAsync completado:");
                Debug.Log($"  - Resultado: {spawnedObject}");
                Debug.Log($"  - Es null: {spawnedObject == null}");

                if (spawnedObject != null)
                {
                    Debug.Log($"  - Spawned object name: {spawnedObject.name}");
                    Debug.Log($"  - Has GameplayManager: {spawnedObject.GetComponent<GameplayManager>() != null}");
                    Debug.Log($"  - IsValid: {spawnedObject.IsValid}");
                    Debug.Log($"  - Id: {spawnedObject.Id}");

                    // Verificar que realmente se spawneó
                    var gm = spawnedObject.GetComponent<GameplayManager>();
                    if (gm != null)
                    {
                        Debug.Log("[GameplaySceneInitializer] ✅ GameplayManager component encontrado!");
                        return spawnedObject;
                    }
                    else
                    {
                        Debug.LogError(
                            "[GameplaySceneInitializer] ❌ NetworkObject spawneado pero sin GameplayManager component!");
                    }
                }
                else
                {
                    Debug.LogError("[GameplaySceneInitializer] ❌ SpawnAsync retornó null!");
                }

                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameplaySceneInitializer] ❌ Excepción en SpawnGameplayManager:");
                Debug.LogError($"  - Tipo: {e.GetType().Name}");
                Debug.LogError($"  - Mensaje: {e.Message}");
                Debug.LogError($"  - StackTrace: {e.StackTrace}");
                return null;
            }
        }

        private NetworkObject SpawnGameplayManagerSync(NetworkRunner runner)
        {
            Debug.Log("[GameplaySceneInitializer] 🔄 Intentando spawn síncrono...");

            try
            {
                // Método 1: Spawn directo con prefab
                NetworkObject spawnedObject = runner.Spawn(
                    gameplayManagerPrefab,
                    Vector3.zero,
                    Quaternion.identity,
                    null, // Sin input authority específico
                    (runner, obj) =>
                    {
                        Debug.Log($"[GameplaySceneInitializer] 📦 OnBeforeSpawned - Object: {obj?.name}");
                    }
                );

                if (spawnedObject != null)
                {
                    Debug.Log($"[GameplaySceneInitializer] ✅ Spawn síncrono exitoso: {spawnedObject.name}");
                    return spawnedObject;
                }
                else
                {
                    Debug.LogError("[GameplaySceneInitializer] ❌ Spawn síncrono retornó null");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameplaySceneInitializer] ❌ Error en spawn síncrono: {e.Message}");
            }

            // Método 2: Instanciar localmente primero (NO RECOMENDADO pero para debug)
            Debug.LogWarning("[GameplaySceneInitializer] ⚠️ Intentando método de respaldo...");

            try
            {
                // Verificar si podemos al menos instanciar el prefab
                GameObject tempInstance = Instantiate(gameplayManagerPrefab.gameObject);
                Debug.Log($"[GameplaySceneInitializer] 📦 Instancia local creada: {tempInstance.name}");

                // Verificar componentes
                var netObj = tempInstance.GetComponent<NetworkObject>();
                var gmComp = tempInstance.GetComponent<GameplayManager>();

                Debug.Log($"  - NetworkObject: {netObj != null}");
                Debug.Log($"  - GameplayManager: {gmComp != null}");

                // Destruir la instancia temporal
                DestroyImmediate(tempInstance);

                // Si llegamos aquí, el prefab está bien configurado
                Debug.Log("[GameplaySceneInitializer] ✅ Prefab validado correctamente");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameplaySceneInitializer] ❌ No se puede ni instanciar el prefab: {e.Message}");
            }

            return null;
        }


        private void NotifyGameCore()
        {
            var gameCore = GameCore.Instance;
            if (gameCore != null)
            {
                if (debugMode) Debug.Log("[GameplaySceneInitializer] 📢 Notificando a GameCore...");
                // GameCore ya debería estar en estado InMatch, pero podemos confirmar
                if (gameCore.CurrentState == GameCore.GameState.LoadingMatch)
                {
                    gameCore.OnGameSceneLoaded();
                }
            }
        }

        private async Task HandleExistingPlayers(NetworkRunner runner, CancellationToken cancellationToken)
        {
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 👥 Manejando jugadores existentes...");

            // Esperar un poco para que GameplayManager se inicialice
            await Task.Delay((int)(playerSpawnDelay * 1000), cancellationToken);

            var gameplayManager = GameplayManager.Instance;
            if (gameplayManager == null)
            {
                Debug.LogWarning("[GameplaySceneInitializer] ⚠️ No se pudo obtener GameplayManager.Instance");
                return;
            }

            // Obtener todos los jugadores conectados
            foreach (var player in runner.ActivePlayers)
            {
                if (player.IsRealPlayer)
                {
                    if (debugMode) Debug.Log($"[GameplaySceneInitializer] 👤 Notificando jugador existente: {player}");

                    // Llamar a PlayerJoined manualmente
                    gameplayManager.PlayerJoined(player);

                    // Pequeño delay entre jugadores
                    await Task.Delay(100, cancellationToken);
                }
            }

            if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ Jugadores existentes procesados");
        }

        private void OnDestroy()
        {
            // Cancelar cualquier operación en progreso
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🧹 Initializer destruido");
        }

        #region Debug Helpers

        [ContextMenu("Debug: Print Scene State")]
        private void DebugPrintSceneState()
        {
            Debug.Log("=== GAMEPLAY SCENE STATE ===");

            // NetworkRunner
            var runner = NetworkRunner.GetRunnerForScene(gameObject.scene);
            Debug.Log($"NetworkRunner: {(runner != null ? "Found" : "Not Found")}");
            if (runner != null)
            {
                Debug.Log($"  - IsRunning: {runner.IsRunning}");
                Debug.Log($"  - IsServer: {runner.IsServer}");
                Debug.Log($"  - Tick: {runner.Tick}");
                Debug.Log($"  - ActivePlayers: {runner.ActivePlayers.Count()}");
            }

            // GameplayManager
            var gameplayManager = FindObjectOfType<GameplayManager>();
            Debug.Log($"GameplayManager: {(gameplayManager != null ? "Exists" : "Not Found")}");

            // GameCore
            var gameCore = GameCore.Instance;
            Debug.Log($"GameCore State: {(gameCore != null ? gameCore.CurrentState.ToString() : "null")}");

            Debug.Log("===========================");
        }

        [ContextMenu("Test: Force Spawn GameplayManager")]
        private async void TestForceSpawn()
        {
            var runner = NetworkRunner.GetRunnerForScene(gameObject.scene);
            if (runner != null && runner.IsServer)
            {
                await SpawnGameplayManager(runner);
            }
            else
            {
                Debug.LogError("No se puede forzar spawn - no hay runner o no es servidor");
            }
        }

        #endregion
    }
}