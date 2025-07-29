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

            // 2. Bifurcación HOST/CLIENTE
            if (runner.IsServer)
            {
                // HOST: Spawnear GameplayManager y manejar jugadores
                await HandleHostInitialization(runner, cancellationToken);
            }
            else
            {
                // CLIENTE: Solo preparar el VR Rig para auto-detección
                PrepareClientVRRig();
            }
        }

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
                if (!runner.CanSpawn)
                {
                    Debug.LogError("[GameplaySceneInitializer] ❌ Runner no puede spawnear objetos!");
                    return null;
                }

                NetworkObject spawnedObject = await runner.SpawnAsync(
                    gameplayManagerPrefab,
                    Vector3.zero,
                    Quaternion.identity
                );

                if (spawnedObject != null)
                {
                    var gm = spawnedObject.GetComponent<GameplayManager>();
                    if (gm != null)
                    {
                        return spawnedObject;
                    }
                }

                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameplaySceneInitializer] ❌ Excepción en SpawnGameplayManager: {e.Message}");
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