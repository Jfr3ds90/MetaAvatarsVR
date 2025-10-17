using UnityEngine;
using Fusion;
using Cysharp.Threading.Tasks;
using System.Threading;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;


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
        [Networked] public NetworkBool IsRoomCreator { get; set; } // En Shared Mode, quien creó la sala
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
        // DEPRECATED: Mantener IsHost para compatibilidad  
        public bool IsHost => IsRoomCreator;
        #endregion

        #region Network Lifecycle
        public override void Spawned()
        {
            AdvancedDebugSystem.Log($"[LOBBYPLAYER] 🎮 Spawned - PlayerRef: {Object.InputAuthority}, IsLocal: {HasInputAuthority}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);

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
            AdvancedDebugSystem.Log($"[LOBBYPLAYER] 👋 Despawning - Name: {_cachedPlayerName}, PlayerRef: {Object.InputAuthority}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
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
                AdvancedDebugSystem.Log("[LOBBYPLAYER] 🔄 Starting local player initialization...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                
                // Obtener PlayerDataManager
                _dataManager = PlayerDataManager.Instance;
                
                if (_dataManager == null)
                {
                    AdvancedDebugSystem.LogError("[LOBBYPLAYER] ❌ PlayerDataManager not found!", LogCategory.Networking | LogCategory.Photon);
                    return;
                }
                
                // Esperar a que los datos estén listos
                bool dataReady = await _dataManager.WaitForDataReady();
                
                if (!dataReady)
                {
                    AdvancedDebugSystem.LogError("[LOBBYPLAYER] ❌ Player data not ready after timeout!", LogCategory.Networking | LogCategory.Photon);
                    return;
                }
                
                // Obtener datos validados
                string playerName = _dataManager.GetPlayerName();
                Color playerColor = _dataManager.GetPlayerColor();
                // En Shared Mode, verificar si somos el creador de la sala
                bool isRoomCreator = NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsRoomCreator;
                
                // Validación adicional
                if (string.IsNullOrEmpty(playerName))
                {
                    playerName = $"Player_{Object.InputAuthority.PlayerId}";
                    AdvancedDebugSystem.LogWarning($"[LOBBYPLAYER] Name was empty, using fallback: {playerName}", LogCategory.Networking | LogCategory.Photon);
                }
                
                _cachedPlayerName = playerName;
                
                AdvancedDebugSystem.Log($"[LOBBYPLAYER] 📤 Sending player data - Name: {playerName}, IsRoomCreator: {isRoomCreator}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                
                // Enviar datos via RPC
                RPC_SetPlayerData(playerName, playerColor, isRoomCreator);
                
                // Esperar confirmación de sincronización
                await WaitForDataSyncAsync(cancellationToken);
                
                // Registrar en LobbyState
                RegisterInLobbyState();
                
                // Si no somos el creador, sincronizar con el mapa del creador
                if (!isRoomCreator)
                {
                    await SyncWithCreatorMapAsync(cancellationToken);
                }
                
                AdvancedDebugSystem.Log($"[LOBBYPLAYER] ✅ Local player initialization complete: {playerName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
            catch (System.OperationCanceledException)
            {
                AdvancedDebugSystem.Log("[LOBBYPLAYER] Initialization cancelled", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
            catch (System.Exception e)
            {
                AdvancedDebugSystem.LogError($"[LOBBYPLAYER] ❌ Initialization error: {e.Message}", LogCategory.Networking | LogCategory.Photon);
            }
        }

        /// <summary>
        /// Espera a que los datos estén sincronizados en la red
        /// </summary>
        private async UniTask WaitForDataSyncAsync(CancellationToken cancellationToken)
        {
            float timeout = 3f;
            float elapsed = 0f;
            
            AdvancedDebugSystem.Log("[LOBBYPLAYER] ⏳ Waiting for data sync...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            while (!DataInitialized && elapsed < timeout)
            {
                await UniTask.Delay(100, cancellationToken: cancellationToken);
                elapsed += 0.1f;
                
                // Verificar si los datos están disponibles
                if (!string.IsNullOrEmpty(PlayerName.ToString()))
                {
                    AdvancedDebugSystem.Log($"[LOBBYPLAYER] ✅ Data synced: {PlayerName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                    break;
                }
            }
            
            if (elapsed >= timeout)
            {
                AdvancedDebugSystem.LogWarning("[LOBBYPLAYER] ⚠️ Data sync timeout!", LogCategory.Networking | LogCategory.Photon);
            }
        }

        /// <summary>
        /// Espera datos para jugador remoto
        /// </summary>
        private async UniTaskVoid WaitForRemotePlayerDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                AdvancedDebugSystem.Log($"[LOBBYPLAYER] ⏳ Waiting for remote player data - PlayerRef: {Object.InputAuthority}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                
                float timeout = 5f;
                float elapsed = 0f;
                
                // Esperar hasta que los datos estén disponibles o timeout
                while (elapsed < timeout)
                {
                    // Verificar si los datos están listos
                    if (DataInitialized || !string.IsNullOrEmpty(PlayerName.ToString()))
                    {
                        _cachedPlayerName = PlayerName.ToString();
                        AdvancedDebugSystem.Log($"[LOBBYPLAYER] ✅ Remote player data received: {_cachedPlayerName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                        break;
                    }
                    
                    await UniTask.Delay(100, cancellationToken: cancellationToken);
                    elapsed += 0.1f;
                }
                
                if (elapsed >= timeout)
                {
                    AdvancedDebugSystem.LogWarning($"[LOBBYPLAYER] ⚠️ Timeout waiting for remote player data - PlayerRef: {Object.InputAuthority}", LogCategory.Networking | LogCategory.Photon);
                    _cachedPlayerName = $"Player_{Object.InputAuthority.PlayerId}";
                }
                
                // Registrar cuando tengamos datos
                RegisterInLobbyState();
                
            }
            catch (System.OperationCanceledException)
            {
                AdvancedDebugSystem.Log("[LOBBYPLAYER] Remote player wait cancelled", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
            catch (System.Exception e)
            {
                AdvancedDebugSystem.LogError($"[LOBBYPLAYER] ❌ Error waiting for remote data: {e.Message}", LogCategory.Networking | LogCategory.Photon);
            }
        }

        /// <summary>
        /// Sincroniza con el mapa seleccionado por el creador de la sala
        /// </summary>
        private async UniTask SyncWithCreatorMapAsync(CancellationToken cancellationToken)
        {
            // Esperar un momento para que LobbyState esté listo
            await UniTask.Delay(500, cancellationToken: cancellationToken);
            
            if (LobbyState.Instance != null)
            {
                var hostPlayer = LobbyState.Instance.RoomCreatorPlayer; // Usar RoomCreatorPlayer en lugar de HostPlayer
                if (hostPlayer != null && !string.IsNullOrEmpty(hostPlayer.SelectedMap.ToString()))
                {
                    AdvancedDebugSystem.Log($"[LOBBYPLAYER] 🗺️ Player syncing with room creator's map: {hostPlayer.SelectedMap}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                    
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
                AdvancedDebugSystem.LogWarning("[LOBBYPLAYER] ⚠️ LobbyState not available for registration", LogCategory.Networking | LogCategory.Photon);
                WaitForLobbyStateAsync(_cancellationTokenSource.Token).Forget();
                return;
            }
            
            AdvancedDebugSystem.Log($"[LOBBYPLAYER] 📝 Registering player - Name: {GetDisplayName()}, PlayerRef: {PlayerRef}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
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
                    AdvancedDebugSystem.LogError("[LOBBYPLAYER] ❌ LobbyState not found after timeout!", LogCategory.Networking | LogCategory.Photon);
                }
            }
            catch (System.OperationCanceledException)
            {
                AdvancedDebugSystem.Log("[LOBBYPLAYER] LobbyState wait cancelled", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
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
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)] // En Shared Mode, enviamos a todos
        private void RPC_SetPlayerData(NetworkString<_32> name, Color color, NetworkBool isRoomCreator)
        {
            AdvancedDebugSystem.Log($"[LOBBYPLAYER-RPC] 📥 Setting player data - Name: {name}, IsRoomCreator: {isRoomCreator}, PlayerRef: {Object.InputAuthority}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            // En Shared Mode, cada jugador actualiza sus propios datos
            if (!HasInputAuthority)
            {
                AdvancedDebugSystem.Log("[LOBBYPLAYER-RPC] Received remote player data", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                return;
            }
            
            // Validar y establecer datos
            string validName = !string.IsNullOrEmpty(name.ToString()) 
                ? name.ToString() 
                : $"Player_{Object.InputAuthority.PlayerId}";
            
            PlayerName = validName;
            PlayerColor = color;
            IsRoomCreator = isRoomCreator;
            DataInitialized = true;
            
            _cachedPlayerName = validName;
            
            // Si es el creador, inicializar con el mapa por defecto
            if (isRoomCreator && string.IsNullOrEmpty(SelectedMap.ToString()))
            {
                var networkBootstrapper = NetworkBootstrapper.Instance;
                if (networkBootstrapper != null)
                {
                    string defaultMap = networkBootstrapper.GetDefaultSceneName();
                    SelectedMap = defaultMap;
                    AdvancedDebugSystem.Log($"[LOBBYPLAYER-RPC] Room creator initialized with default map: {defaultMap}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                    
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
        [Rpc(RpcSources.All, RpcTargets.All)] // En Shared Mode, cualquiera puede notificar
        private void RPC_NotifyDataReady(PlayerRef playerRef, NetworkString<_32> playerName)
        {
            AdvancedDebugSystem.Log($"[LOBBYPLAYER-RPC] 📢 Data ready notification - Player: {playerName} ({playerRef})", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
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
                AdvancedDebugSystem.LogWarning("[LOBBYPLAYER] Cannot toggle ready - not local player", LogCategory.Networking | LogCategory.Photon);
                return;
            }

            bool newReadyState = !IsReady;
            AdvancedDebugSystem.Log($"[LOBBYPLAYER] 🔄 Toggling ready to: {newReadyState}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            RPC_SetReady(newReadyState);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_SetReady(NetworkBool ready)
        {
            // En Shared Mode, cada jugador actualiza su propio estado
            if (HasInputAuthority)
            {
                IsReady = ready;
                AdvancedDebugSystem.Log($"[LOBBYPLAYER] Ready state set to: {ready}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                // Notificar a todos del cambio
                RPC_NotifyReadyStateChanged(Object.InputAuthority, ready);
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)] // En Shared Mode
        private void RPC_NotifyReadyStateChanged(PlayerRef playerRef, NetworkBool ready)
        {
            // Actualizar display solo cuando el servidor confirma el cambio
            if (LobbyState.Instance != null && _isRegistered)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
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
        /// Cambiar mapa (solo el creador de la sala puede cambiar el mapa)
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)] // En Shared Mode
        public void RPC_ChangeMap(NetworkString<_64> mapName)
        {
            AdvancedDebugSystem.Log($"[LOBBYPLAYER-RPC] 📤 RPC_ChangeMap received: {mapName}, IsRoomCreator: {IsRoomCreator}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            // Solo el creador de la sala puede cambiar el mapa
            if (!IsRoomCreator)
            {
                AdvancedDebugSystem.LogWarning("[LOBBYPLAYER] Non-room-creator tried to change map!", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            // En Shared Mode, el creador actualiza su propio estado y notifica a todos
            if (HasInputAuthority && IsRoomCreator)
            {
                SelectedMap = mapName;
                AdvancedDebugSystem.Log($"[LOBBYPLAYER-RPC] ✅ Room creator updated SelectedMap to: {mapName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                
                // Notificar a todos los jugadores
                RPC_NotifyMapChange(mapName);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)] // En Shared Mode
        private void RPC_NotifyMapChange(NetworkString<_64> mapName)
        {
            AdvancedDebugSystem.Log($"[LOBBYPLAYER-RPC] 📥 Map change notification received: {mapName}, IsRoomCreator: {IsRoomCreator}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            // En Shared Mode, todos actualizan la selección del mapa localmente
            SelectedMap = mapName;
            AdvancedDebugSystem.Log($"[LOBBYPLAYER-RPC] ✅ Updated SelectedMap to: {mapName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            if (LobbyState.Instance != null)
            {
                // Todos los jugadores actualizan su estado
                LobbyState.Instance.UpdateMapSelection(mapName.ToString());
                
                // Todos actualizan su NetworkBootstrapper
                if (NetworkBootstrapper.Instance != null)
                {
                    AdvancedDebugSystem.Log($"[LOBBYPLAYER-RPC] 🗺️ Updating NetworkBootstrapper map to: {mapName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                    NetworkBootstrapper.Instance.SelectedSceneName = mapName.ToString();
                }
            }
        }
        #endregion

        #region Change Detection
        public override void FixedUpdateNetwork()
        {
            // En Shared Mode, solo verificamos InputAuthority
            if (HasInputAuthority)
            {
                foreach (var change in _changeDetector.DetectChanges(this))
                {
                    switch (change)
                    {
                        case nameof(IsReady):
                            AdvancedDebugSystem.Log($"[LOBBYPLAYER] Ready state changed: {IsReady}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                            if (LobbyState.Instance != null && _isRegistered)
                            {
                                LobbyState.Instance.UpdatePlayerDisplay(this);
                            }
                            break;
                            
                        case nameof(SelectedMap):
                            AdvancedDebugSystem.Log($"[LOBBYPLAYER] Map changed: {SelectedMap}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                            if (IsRoomCreator && LobbyState.Instance != null)
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
            AdvancedDebugSystem.Log($"[LOBBYPLAYER] Force cleanup - Name: {_cachedPlayerName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
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
            AdvancedDebugSystem.Log($"[LOBBYPLAYER] OnDestroy - Name: {_cachedPlayerName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
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
            AdvancedDebugSystem.Log($"=== LobbyPlayer Debug ===", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Name: {GetDisplayName()}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Cached Name: {_cachedPlayerName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Ready: {IsReady}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Host: {IsHost}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"IsLocal: {IsLocalPlayer}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"DataInitialized: {DataInitialized}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"IsRegistered: {_isRegistered}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"PlayerRef: {PlayerRef}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"=========================", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        #endregion
    }
}