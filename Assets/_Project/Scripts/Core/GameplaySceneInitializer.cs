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
    /// y la configuración del VR Rig local para cada cliente
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
            // Validar que el VR Rig esté asignado
            if (localVRRig == null)
            {
                Debug.LogWarning("[GameplaySceneInitializer] ⚠️ Local VR Rig no asignado, buscando en la escena...");
                localVRRig = FindObjectOfType<OVRCameraRig>()?.gameObject;
                
                if (localVRRig != null)
                {
                    Debug.Log("[GameplaySceneInitializer] ✅ OVRCameraRig encontrado en la escena");
                }
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
                // CLIENTE: Esperar su NetworkPlayer y configurar VR
                await HandleClientInitialization(runner, cancellationToken);
            }
        }

        #region Host Logic
        private async Task HandleHostInitialization(NetworkRunner runner, CancellationToken cancellationToken)
        {
            // 1. Esperar simulación
            if (debugMode) Debug.Log("[GameplaySceneInitializer] ⏳ HOST: Esperando simulación activa...");
            await WaitForSimulationReady(runner, cancellationToken);

            // 2. Verificar GameplayManager existente
            var existingManager = FindObjectOfType<GameplayManager>();
            if (existingManager != null)
            {
                Debug.LogWarning("[GameplaySceneInitializer] ⚠️ GameplayManager ya existe");
                // NO configurar VR aquí todavía
                return;
            }

            // 3. Spawn GameplayManager
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🎯 Spawneando GameplayManager...");
            NetworkObject spawnedManager = await SpawnGameplayManager(runner);

            if (spawnedManager != null)
            {
                if (debugMode) Debug.Log("[GameplaySceneInitializer] ✅ GameplayManager spawneado");

                // 4. Notificar a GameCore
                NotifyGameCore();

                // 5. Manejar jugadores existentes (esto crea los NetworkPlayers)
                if (autoSpawnExistingPlayers)
                {
                    await HandleExistingPlayers(runner, cancellationToken);
                }

                // 6. AHORA sí configurar VR del host
                await SetupHostVR(runner, cancellationToken);
            }
        }

        private async Task SetupHostVR(NetworkRunner runner, CancellationToken cancellationToken)
        {
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🥽 HOST: Configurando VR local...");

            // Esperar por el NetworkPlayer del host
            NetworkPlayer hostPlayer = await WaitForLocalNetworkPlayer(runner, cancellationToken);

            if (hostPlayer != null && localVRRig != null)
            {
                hostPlayer.SetVRRig(localVRRig);
                Debug.Log("[GameplaySceneInitializer] ✅ HOST: VR Rig conectado al NetworkPlayer");
            }
        }
        #endregion

        #region Client Logic
        private async Task HandleClientInitialization(NetworkRunner runner, CancellationToken cancellationToken)
        {
            if (debugMode) Debug.Log("[GameplaySceneInitializer] 👤 CLIENTE: Iniciando configuración...");

            // 1. Esperar a que GameplayManager sea replicado
            GameplayManager gameplayManager = await WaitForGameplayManager(cancellationToken);
            if (gameplayManager == null)
            {
                Debug.LogError("[GameplaySceneInitializer] ❌ CLIENTE: Timeout esperando GameplayManager");
                return;
            }

            // 2. Esperar por el NetworkPlayer local
            NetworkPlayer localPlayer = await WaitForLocalNetworkPlayer(runner, cancellationToken);
            if (localPlayer == null)
            {
                Debug.LogError("[GameplaySceneInitializer] ❌ CLIENTE: Timeout esperando NetworkPlayer local");
                return;
            }

            // 3. Configurar el VR Rig local
            if (localVRRig != null)
            {
                if (debugMode) Debug.Log("[GameplaySceneInitializer] 🥽 CLIENTE: Conectando VR Rig local...");
                
                localPlayer.SetVRRig(localVRRig);
                
                // Notificar a GameplayManager (sin crear nuevo VR Rig)
                gameplayManager.RegisterLocalPlayer(localPlayer);
                
                Debug.Log("[GameplaySceneInitializer] ✅ CLIENTE: Configuración completa");
            }
            else
            {
                Debug.LogError("[GameplaySceneInitializer] ❌ CLIENTE: No se encontró VR Rig local en la escena");
            }
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

        private async Task<GameplayManager> WaitForGameplayManager(CancellationToken cancellationToken)
        {
            float elapsedTime = 0f;

            while (elapsedTime < initializationTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                GameplayManager manager = GameplayManager.Instance;
                if (manager != null)
                {
                    return manager;
                }

                await Task.Delay(100);
                elapsedTime += 0.1f;

                if (debugMode && (int)(elapsedTime * 10) % 10 == 0)
                {
                    Debug.Log($"[GameplaySceneInitializer] ⏳ Esperando GameplayManager... ({elapsedTime:F1}s)");
                }
            }

            return null;
        }

        private async Task<NetworkPlayer> WaitForLocalNetworkPlayer(NetworkRunner runner, CancellationToken cancellationToken)
        {
            float elapsedTime = 0f;

            while (elapsedTime < initializationTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var networkPlayers = FindObjectsOfType<NetworkPlayer>();
                foreach (var player in networkPlayers)
                {
                    if (player.HasInputAuthority)
                    {
                        return player;
                    }
                }

                await Task.Delay(100);
                elapsedTime += 0.1f;

                if (debugMode && (int)(elapsedTime * 10) % 10 == 0)
                {
                    Debug.Log($"[GameplaySceneInitializer] ⏳ Esperando NetworkPlayer local... ({elapsedTime:F1}s)");
                }
            }

            return null;
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
                    gameplayManager.PlayerJoined(player);
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        #endregion

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            if (debugMode) Debug.Log("[GameplaySceneInitializer] 🧹 Initializer destruido");
        }
    }
}