using UnityEngine;
using Fusion;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace HackMonkeys.Core
{
    /// <summary>
    /// LobbyPlayer refactorizado con sincronización mejorada usando UniTask
    /// Garantiza que el nombre esté disponible antes del registro
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        #region Networked Properties
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }
        [Networked] public NetworkBool IsHost { get; set; }
        [Networked] public Color PlayerColor { get; set; }
        [Networked] public NetworkString<_64> SelectedMap { get; set; }
        [Networked] public NetworkBool DataInitialized { get; set; }
        #endregion

        #region Private Fields
        private PlayerDataManager _dataManager;
        private ChangeDetector _changeDetector;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRegistered = false;
        private string _cachedPlayerName = "Unknown"; // Para debug sin acceder a Networked
        #endregion

        #region Properties
        public PlayerRef PlayerRef => Object.InputAuthority;
        public bool IsLocalPlayer => HasInputAuthority;
        #endregion

        #region Network Lifecycle
        public override void Spawned()
        {
            Debug.Log($"[LOBBYPLAYER] 🎮 Spawned - PlayerRef: {Object.InputAuthority}, IsLocal: {HasInputAuthority}");

            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _cancellationTokenSource = new CancellationTokenSource();

            if (HasInputAuthority)
            {
                // Cliente local: inicializar y enviar datos
                InitializeLocalPlayerAsync(_cancellationTokenSource.Token).Forget();
            }
            else
            {
                // Jugador remoto: esperar datos y registrar
                WaitForRemotePlayerDataAsync(_cancellationTokenSource.Token).Forget();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Debug.Log($"[LOBBYPLAYER] 👋 Despawning - Name: {_cachedPlayerName}, PlayerRef: {Object.InputAuthority}");
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            if (LobbyState.Instance != null && _isRegistered)
            {
                LobbyState.Instance.UnregisterPlayer(this);
            }
            
            _dataManager = null;
            _changeDetector = null;
            
            if (IsLocalPlayer && PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.ClearSessionData();
            }
            
            if (hasState)
            {
                DestroyAfterDelay().Forget();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private async UniTaskVoid DestroyAfterDelay()
        {
            await UniTask.Delay(100);
            if (gameObject != null)
                Destroy(gameObject);
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Inicialización asíncrona para jugador local
        /// </summary>
        private async UniTaskVoid InitializeLocalPlayerAsync(CancellationToken cancellationToken)
        {
            try
            {
                Debug.Log("[LOBBYPLAYER] 🔄 Starting local player initialization...");
                
                // Obtener PlayerDataManager
                _dataManager = PlayerDataManager.Instance;
                
                if (_dataManager == null)
                {
                    Debug.LogError("[LOBBYPLAYER] ❌ PlayerDataManager not found!");
                    return;
                }
                
                // Esperar a que los datos estén listos
                bool dataReady = await _dataManager.WaitForDataReady();
                
                if (!dataReady)
                {
                    Debug.LogError("[LOBBYPLAYER] ❌ Player data not ready after timeout!");
                    return;
                }
                
                // Obtener datos validados
                string playerName = _dataManager.GetPlayerName();
                Color playerColor = _dataManager.GetPlayerColor();
                bool isHost = Runner.IsServer;
                
                // Validación adicional
                if (string.IsNullOrEmpty(playerName))
                {
                    playerName = $"Player_{Object.InputAuthority.PlayerId}";
                    Debug.LogWarning($"[LOBBYPLAYER] Name was empty, using fallback: {playerName}");
                }
                
                _cachedPlayerName = playerName;
                
                Debug.Log($"[LOBBYPLAYER] 📤 Sending player data - Name: {playerName}, IsHost: {isHost}");
                
                // Enviar datos via RPC
                RPC_SetPlayerData(playerName, playerColor, isHost);
                
                // Esperar confirmación de sincronización
                await WaitForDataSyncAsync(cancellationToken);
                
                // Registrar en LobbyState
                RegisterInLobbyState();
                
                // Si es cliente, sincronizar con el mapa del host
                if (!Runner.IsServer)
                {
                    await SyncWithHostMapAsync(cancellationToken);
                }
                
                Debug.Log($"[LOBBYPLAYER] ✅ Local player initialization complete: {playerName}");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[LOBBYPLAYER] Initialization cancelled");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LOBBYPLAYER] ❌ Initialization error: {e.Message}");
            }
        }

        /// <summary>
        /// Espera a que los datos estén sincronizados en la red
        /// </summary>
        private async UniTask WaitForDataSyncAsync(CancellationToken cancellationToken)
        {
            float timeout = 3f;
            float elapsed = 0f;
            
            Debug.Log("[LOBBYPLAYER] ⏳ Waiting for data sync...");
            
            while (!DataInitialized && elapsed < timeout)
            {
                await UniTask.Delay(100, cancellationToken: cancellationToken);
                elapsed += 0.1f;
                
                // Verificar si los datos están disponibles
                if (!string.IsNullOrEmpty(PlayerName.ToString()))
                {
                    Debug.Log($"[LOBBYPLAYER] ✅ Data synced: {PlayerName}");
                    break;
                }
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogWarning("[LOBBYPLAYER] ⚠️ Data sync timeout!");
            }
        }

        /// <summary>
        /// Espera datos para jugador remoto
        /// </summary>
        private async UniTaskVoid WaitForRemotePlayerDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                Debug.Log($"[LOBBYPLAYER] ⏳ Waiting for remote player data - PlayerRef: {Object.InputAuthority}");
                
                float timeout = 5f;
                float elapsed = 0f;
                
                // Esperar hasta que los datos estén disponibles o timeout
                while (elapsed < timeout)
                {
                    // Verificar si los datos están listos
                    if (DataInitialized || !string.IsNullOrEmpty(PlayerName.ToString()))
                    {
                        _cachedPlayerName = PlayerName.ToString();
                        Debug.Log($"[LOBBYPLAYER] ✅ Remote player data received: {_cachedPlayerName}");
                        break;
                    }
                    
                    await UniTask.Delay(100, cancellationToken: cancellationToken);
                    elapsed += 0.1f;
                }
                
                if (elapsed >= timeout)
                {
                    Debug.LogWarning($"[LOBBYPLAYER] ⚠️ Timeout waiting for remote player data - PlayerRef: {Object.InputAuthority}");
                    _cachedPlayerName = $"Player_{Object.InputAuthority.PlayerId}";
                }
                
                // Registrar cuando tengamos datos
                RegisterInLobbyState();
                
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[LOBBYPLAYER] Remote player wait cancelled");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LOBBYPLAYER] ❌ Error waiting for remote data: {e.Message}");
            }
        }

        /// <summary>
        /// Sincroniza con el mapa seleccionado por el host
        /// </summary>
        private async UniTask SyncWithHostMapAsync(CancellationToken cancellationToken)
        {
            // Esperar un momento para que LobbyState esté listo
            await UniTask.Delay(500, cancellationToken: cancellationToken);
            
            if (LobbyState.Instance != null)
            {
                var hostPlayer = LobbyState.Instance.HostPlayer;
                if (hostPlayer != null && !string.IsNullOrEmpty(hostPlayer.SelectedMap.ToString()))
                {
                    Debug.Log($"[LOBBYPLAYER] 🗺️ Client syncing with host map: {hostPlayer.SelectedMap}");
                    
                    var networkBootstrapper = NetworkBootstrapper.Instance;
                    if (networkBootstrapper != null)
                    {
                        networkBootstrapper.SelectedSceneName = hostPlayer.SelectedMap.ToString();
                    }
                    
                    LobbyState.Instance.UpdateMapSelection(hostPlayer.SelectedMap.ToString());
                }
            }
        }
        #endregion

        #region Registration
        /// <summary>
        /// Registra el jugador en LobbyState
        /// </summary>
        private void RegisterInLobbyState()
        {
            if (_isRegistered) return;
            
            if (LobbyState.Instance == null)
            {
                Debug.LogWarning("[LOBBYPLAYER] ⚠️ LobbyState not available for registration");
                WaitForLobbyStateAsync(_cancellationTokenSource.Token).Forget();
                return;
            }
            
            Debug.Log($"[LOBBYPLAYER] 📝 Registering player - Name: {GetDisplayName()}, PlayerRef: {PlayerRef}");
            
            LobbyState.Instance.RegisterPlayer(this);
            _isRegistered = true;
            
            // Actualizar display después del registro
            DelayedUpdateDisplayAsync().Forget();
        }

        /// <summary>
        /// Espera a que LobbyState esté disponible
        /// </summary>
        private async UniTaskVoid WaitForLobbyStateAsync(CancellationToken cancellationToken)
        {
            try
            {
                float timeout = 5f;
                float elapsed = 0f;
                
                while (LobbyState.Instance == null && elapsed < timeout)
                {
                    await UniTask.Delay(100, cancellationToken: cancellationToken);
                    elapsed += 0.1f;
                }
                
                if (LobbyState.Instance != null)
                {
                    RegisterInLobbyState();
                }
                else
                {
                    Debug.LogError("[LOBBYPLAYER] ❌ LobbyState not found after timeout!");
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[LOBBYPLAYER] LobbyState wait cancelled");
            }
        }

        /// <summary>
        /// Actualiza el display después de un delay
        /// </summary>
        private async UniTaskVoid DelayedUpdateDisplayAsync()
        {
            await UniTask.Delay(100);
            
            if (LobbyState.Instance != null && _isRegistered)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }
        #endregion

        #region RPCs
        /// <summary>
        /// RPC para establecer datos del jugador con confirmación
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetPlayerData(NetworkString<_32> name, Color color, NetworkBool isHost)
        {
            Debug.Log($"[LOBBYPLAYER-RPC] 📥 Setting player data - Name: {name}, IsHost: {isHost}, PlayerRef: {Object.InputAuthority}");
            
            // Validar y establecer datos
            string validName = !string.IsNullOrEmpty(name.ToString()) 
                ? name.ToString() 
                : $"Player_{Object.InputAuthority.PlayerId}";
            
            PlayerName = validName;
            PlayerColor = color;
            IsHost = isHost;
            DataInitialized = true;
            
            _cachedPlayerName = validName;
            
            // Si es el host, inicializar con el mapa por defecto
            if (isHost && string.IsNullOrEmpty(SelectedMap.ToString()))
            {
                var networkBootstrapper = NetworkBootstrapper.Instance;
                if (networkBootstrapper != null)
                {
                    string defaultMap = networkBootstrapper.GetDefaultSceneName();
                    SelectedMap = defaultMap;
                    Debug.Log($"[LOBBYPLAYER-RPC] Host initialized with default map: {defaultMap}");
                    
                    // Notificar a todos del mapa
                    RPC_NotifyMapChange(defaultMap);
                }
            }
            
            // Notificar a todos que los datos están listos
            RPC_NotifyDataReady(Object.InputAuthority, validName);
        }

        /// <summary>
        /// Notifica a todos los clientes que los datos están listos
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyDataReady(PlayerRef playerRef, NetworkString<_32> playerName)
        {
            Debug.Log($"[LOBBYPLAYER-RPC] 📢 Data ready notification - Player: {playerName} ({playerRef})");
            
            // Si es nuestro jugador local, confirmar sincronización
            if (HasInputAuthority)
            {
                DataInitialized = true;
                _cachedPlayerName = playerName.ToString();
            }
            
            // Actualizar display si ya estamos registrados
            if (_isRegistered && LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }

        /// <summary>
        /// Toggle ready state con notificación
        /// </summary>
        public void ToggleReady()
        {
            if (!HasInputAuthority)
            {
                Debug.LogWarning("[LOBBYPLAYER] Cannot toggle ready - not local player");
                return;
            }

            bool newReadyState = !IsReady;
            Debug.Log($"[LOBBYPLAYER] 🔄 Toggling ready to: {newReadyState}");
            
            RPC_SetReady(newReadyState);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_SetReady(NetworkBool ready)
        {
            if (HasStateAuthority)
            {
                IsReady = ready;
            }
            
            // Actualizar display con delay
            UpdateDisplayAfterDelayAsync().Forget();
        }

        private async UniTaskVoid UpdateDisplayAfterDelayAsync()
        {
            await UniTask.Delay(50);
            
            if (LobbyState.Instance != null && _isRegistered)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }

        /// <summary>
        /// Cambiar mapa (solo host)
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        public void RPC_ChangeMap(NetworkString<_64> mapName)
        {
            if (!IsHost)
            {
                Debug.LogWarning("[LOBBYPLAYER] Non-host tried to change map!");
                return;
            }
            
            if (HasStateAuthority)
            {
                SelectedMap = mapName;
            }
            
            RPC_NotifyMapChange(mapName);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyMapChange(NetworkString<_64> mapName)
        {
            if (LobbyState.Instance != null && IsHost)
            {
                LobbyState.Instance.UpdateMapSelection(mapName.ToString());
            }
        }
        #endregion

        #region Change Detection
        public override void FixedUpdateNetwork()
        {
            if (HasInputAuthority || HasStateAuthority)
            {
                foreach (var change in _changeDetector.DetectChanges(this))
                {
                    switch (change)
                    {
                        case nameof(IsReady):
                            Debug.Log($"[LOBBYPLAYER] Ready state changed: {IsReady}");
                            if (LobbyState.Instance != null && _isRegistered)
                            {
                                LobbyState.Instance.UpdatePlayerDisplay(this);
                            }
                            break;
                            
                        case nameof(SelectedMap):
                            Debug.Log($"[LOBBYPLAYER] Map changed: {SelectedMap}");
                            if (IsHost && LobbyState.Instance != null)
                            {
                                LobbyState.Instance.UpdateMapSelection(SelectedMap.ToString());
                            }
                            break;
                    }
                }
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// Obtiene el nombre para mostrar, con fallbacks
        /// </summary>
        public string GetDisplayName()
        {
            string name = PlayerName.ToString();
            
            // Usar cache si el networked está vacío
            if (string.IsNullOrEmpty(name))
            {
                name = _cachedPlayerName;
            }
            
            // Fallback final
            if (string.IsNullOrEmpty(name))
            {
                name = $"Player_{PlayerRef.PlayerId}";
            }

            if (IsHost)
                name += " (Host)";

            return name;
        }

        public string GetStatusText()
        {
            if (IsHost)
                return IsReady ? "Ready (Host)" : "Not Ready (Host)";
            else
                return IsReady ? "Ready" : "Not Ready";
        }

        /// <summary>
        /// Limpieza forzada
        /// </summary>
        public void ForceCleanup()
        {
            Debug.Log($"[LOBBYPLAYER] Force cleanup - Name: {_cachedPlayerName}");
            
            _cancellationTokenSource?.Cancel();
            
            if (LobbyState.Instance != null && _isRegistered)
            {
                LobbyState.Instance.UnregisterPlayer(this);
            }
            
            Destroy(gameObject);
        }
        #endregion

        #region Unity Callbacks
        private void OnDestroy()
        {
            Debug.Log($"[LOBBYPLAYER] OnDestroy - Name: {_cachedPlayerName}");
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            if (LobbyState.Instance != null && _isRegistered)
            {
                var registered = LobbyState.Instance.GetPlayer(PlayerRef);
                if (registered == this)
                {
                    LobbyState.Instance.UnregisterPlayer(this);
                }
            }
        }
        #endregion

        #region Debug
        [ContextMenu("Debug: Player State")]
        private void DebugPlayerState()
        {
            Debug.Log($"=== LobbyPlayer Debug ===");
            Debug.Log($"Name: {GetDisplayName()}");
            Debug.Log($"Cached Name: {_cachedPlayerName}");
            Debug.Log($"Ready: {IsReady}");
            Debug.Log($"Host: {IsHost}");
            Debug.Log($"IsLocal: {IsLocalPlayer}");
            Debug.Log($"DataInitialized: {DataInitialized}");
            Debug.Log($"IsRegistered: {_isRegistered}");
            Debug.Log($"PlayerRef: {PlayerRef}");
            Debug.Log($"=========================");
        }
        #endregion
    }
}