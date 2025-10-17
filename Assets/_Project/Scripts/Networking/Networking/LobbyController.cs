using UnityEngine;
using UnityEngine.Events;
using System.Threading.Tasks;
using HackMonkeys.Debugging;

namespace HackMonkeys.Core
{
    /// <summary>
    /// LobbyController - SOLO acciones y validaciones del lobby
    /// Patrón Controller limpio - Coordina entre LobbyState y NetworkBootstrapper
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        [Header("Events - Acciones del Controller")]
        public UnityEvent OnGameStarting;
        public UnityEvent OnGameStartFailed;
        public UnityEvent OnLeavingLobby;
        public UnityEvent<string> OnActionFailed;
        
        private LobbyState _lobbyState;
        private NetworkBootstrapper _networkBootstrapper;
        private GameCore _gameCore;
        
        public static LobbyController Instance { get; private set; }
        
        // En Shared Mode, usamos IsRoomCreator en lugar de IsHost
        public bool IsRoomCreator => _networkBootstrapper?.IsRoomCreator ?? false;
        // DEPRECATED: Mantener IsHost para compatibilidad
        public bool IsHost => IsRoomCreator;
        public bool IsInLobby => _networkBootstrapper?.IsInRoom ?? false;
        public bool CanStartGame => ValidateCanStartGame();
        public bool CanLeaveLobby => IsInLobby;
        
        private void Awake()
        {
            if (Instance != null)
            {
                AdvancedDebugSystem.LogWarning("Multiple instances detected. Destroying duplicate.", LogCategory.Lobby | LogCategory.StateManagement);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            AdvancedDebugSystem.LogInfo("✅ Initialized successfully", LogCategory.Lobby | LogCategory.StateManagement);
        }
        
        private void Start()
        {
            _gameCore = GameCore.Instance;
            
            StartCoroutine(InitializeReferences());
        }
        
        private System.Collections.IEnumerator InitializeReferences()
        {
            while (_lobbyState == null || _networkBootstrapper == null)
            {
                _lobbyState = LobbyState.Instance;
                _networkBootstrapper = NetworkBootstrapper.Instance;
                
                if (_lobbyState == null)
                    AdvancedDebugSystem.LogWarning("⏳ Waiting for LobbyState.Instance...", LogCategory.Lobby | LogCategory.StateManagement);

                if (_networkBootstrapper == null)
                    AdvancedDebugSystem.LogWarning("⏳ Waiting for NetworkBootstrapper.Instance...", LogCategory.Lobby | LogCategory.Networking);
                
                yield return new WaitForSeconds(0.1f);
            }
            
            AdvancedDebugSystem.Log("[LobbyController] ✅ All references initialized successfully", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            // 🧪 DEBUG LOG
            AdvancedDebugSystem.Log($"🧪 [LOBBYCONTROLLER] LobbyState: {_lobbyState != null}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"🧪 [LOBBYCONTROLLER] NetworkBootstrapper: {_networkBootstrapper != null}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        // ========================================
        // ✅ ACCIONES PRINCIPALES DEL LOBBY
        // ========================================
        
        /// <summary>
        /// ✅ Iniciar partida (cualquier jugador puede iniciar en Shared Mode)
        /// </summary>
        public async void StartGame()
        {
            AdvancedDebugSystem.Log("[LobbyController] 🚀 === STARTING GAME SEQUENCE ===", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            // Debug de estado actual
            AdvancedDebugSystem.Log($"[LobbyController] IsRoomCreator: {IsRoomCreator}, IsInRoom: {IsInLobby}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"[LobbyController] AllPlayersReady: {_lobbyState?.AllPlayersReady}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"[LobbyController] PlayerCount: {_lobbyState?.PlayerCount}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            PlayerDataManager.Instance.UpdateSessionPlayers(_lobbyState);
                
            string selectedMap = _lobbyState.GetSelectedMap();
            PlayerDataManager.Instance.SetSelectedMap(selectedMap);
            AdvancedDebugSystem.Log($"[LobbyController] Selected map: {selectedMap}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            // VALIDACIÓN Fail Fast
            if (!ValidateCanStartGame())
            {
                string reason = GetStartGameValidationError();
                AdvancedDebugSystem.LogError($"[LobbyController] ❌ Cannot start game: {reason}", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke($"Cannot start game: {reason}");
                OnGameStartFailed?.Invoke();
                return;
            }
            
            try
            {
                AdvancedDebugSystem.Log("[LobbyController] ✅ Validation passed, starting game...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                OnGameStarting?.Invoke();

                string mapName = PlayerDataManager.Instance.SelectedMap;
                int playerCount = _lobbyState.PlayerCount;
                
                AdvancedDebugSystem.Log($"[LobbyController] Starting match with map: {mapName}, players: {playerCount}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);

                bool coreReady = await _gameCore.StartMatch(mapName, playerCount);
                AdvancedDebugSystem.Log($"[LobbyController] GameCore.StartMatch result: {coreReady}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);

                if (coreReady)
                {
                    AdvancedDebugSystem.Log($"[LobbyController] Calling NetworkBootstrapper.StartGame...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                    bool success = await _networkBootstrapper.StartGame();
                    
                    if (success)
                    {
                        AdvancedDebugSystem.Log("[LobbyController] ✅ Game started successfully!", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                    }
                    else
                    {
                        AdvancedDebugSystem.LogError("[LobbyController] ❌ Failed to start game - NetworkBootstrapper error", LogCategory.Networking | LogCategory.Photon);
                        OnActionFailed?.Invoke("Failed to start game - network error");
                        OnGameStartFailed?.Invoke();
                    }
                }
                else
                {
                    AdvancedDebugSystem.LogError("[LobbyController] ❌ GameCore.StartMatch failed", LogCategory.Networking | LogCategory.Photon);
                    OnActionFailed?.Invoke("Failed to initialize game core");
                    OnGameStartFailed?.Invoke();
                }
            }
            catch (System.Exception e)
            {
                AdvancedDebugSystem.LogError($"[LobbyController] ❌ Exception starting game: {e.Message}", LogCategory.Networking | LogCategory.Photon);
                AdvancedDebugSystem.LogError($"[LobbyController] Stack trace: {e.StackTrace}", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke($"Error starting game: {e.Message}");
                OnGameStartFailed?.Invoke();
            }
        }
        
        /// <summary>
        /// Abandonar lobby
        /// </summary>
        public async void LeaveLobby()
        {
            AdvancedDebugSystem.Log("[LobbyController] 👋 Attempting to leave lobby...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
    
            if (!CanLeaveLobby)
            {
                AdvancedDebugSystem.LogWarning("[LobbyController] ❌ Not in a lobby to leave", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke("Not in a lobby");
                return;
            }
    
            try
            {
                OnLeavingLobby?.Invoke();
        
                // IMPORTANTE: Limpiar LobbyState ANTES de desconectar
                if (_lobbyState != null)
                {
                    AdvancedDebugSystem.Log("[LobbyController] Clearing LobbyState before disconnect", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
                    // Obtener referencia al jugador local antes de limpiar
                    var localPlayer = _lobbyState.LocalPlayer;
            
                    // Limpiar todos los jugadores del estado
                    _lobbyState.ClearAllPlayers();
            
                    // Si tenemos un jugador local, asegurar que se destruya
                    if (localPlayer != null)
                    {
                        AdvancedDebugSystem.Log("[LobbyController] Forcing cleanup of local player", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                        localPlayer.ForceCleanup();
                    }
                }
        
                // Esperar un momento para que se procesen las limpiezas
                await Task.Delay(100);
        
                // Ahora sí, desconectar de la red
                await _networkBootstrapper.LeaveRoom();
        
                AdvancedDebugSystem.Log("[LobbyController] ✅ Left lobby successfully", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        
                // Limpiar referencias locales
                CleanupLocalReferences();
            }
            catch (System.Exception e)
            {
                AdvancedDebugSystem.LogError($"[LobbyController] ❌ Exception leaving lobby: {e.Message}", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke($"Error leaving lobby: {e.Message}");
            }
        }
        
        private void CleanupLocalReferences()
        {
            AdvancedDebugSystem.Log("[LobbyController] Cleaning up local references", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
    
            // Verificar si hay LobbyPlayers huérfanos
            var orphanedPlayers = FindObjectsOfType<LobbyPlayer>();
            if (orphanedPlayers.Length > 0)
            {
                AdvancedDebugSystem.LogWarning($"[LobbyController] Found {orphanedPlayers.Length} orphaned LobbyPlayers, destroying them", LogCategory.Networking | LogCategory.Photon);
                foreach (var player in orphanedPlayers)
                {
                    if (player != null && player.gameObject != null)
                    {
                        Destroy(player.gameObject);
                    }
                }
            }
        }
        
        public void OnUnexpectedDisconnection()
        {
            AdvancedDebugSystem.Log("[LobbyController] Handling unexpected disconnection", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
    
            // Limpiar estado local
            if (_lobbyState != null)
            {
                _lobbyState.ClearAllPlayers();
            }
    
            // Limpiar referencias
            CleanupLocalReferences();
    
            // Notificar UI
            OnActionFailed?.Invoke("Connection lost");
        }
        
        /// <summary>
        /// Toggle ready del jugador local
        /// </summary>
        public void ToggleReady()
        {
            if (_lobbyState == null)
            {
                AdvancedDebugSystem.LogError("[LobbyController] ❌ LobbyState not available", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke("Lobby state not available");
                return;
            }
            
            var localPlayer = _lobbyState.LocalPlayer;
            if (localPlayer == null)
            {
                AdvancedDebugSystem.LogWarning("[LobbyController] ❌ No local player found", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke("Local player not found");
                return;
            }
            
            AdvancedDebugSystem.Log($"[LobbyController] 🔄 Toggling ready state for: {localPlayer.GetDisplayName()}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            _lobbyState.ToggleLocalPlayerReady();
        }
        
        /// <summary>
        /// Kick player (solo el creador de la sala puede kickear en Shared Mode)
        /// </summary>
        public void KickPlayer(LobbyPlayer playerToKick)
        {
            if (!IsRoomCreator)
            {
                AdvancedDebugSystem.LogError("[LobbyController] ❌ Only room creator can kick players", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke("Only room creator can kick players");
                return;
            }
            
            if (playerToKick == null)
            {
                AdvancedDebugSystem.LogWarning("[LobbyController] ❌ Cannot kick null player", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke("Invalid player to kick");
                return;
            }
            
            if (playerToKick.IsLocalPlayer)
            {
                AdvancedDebugSystem.LogWarning("[LobbyController] ❌ Cannot kick local player", LogCategory.Networking | LogCategory.Photon);
                OnActionFailed?.Invoke("Cannot kick yourself");
                return;
            }
            
            AdvancedDebugSystem.Log($"[LobbyController] 🥾 Kicking player: {playerToKick.GetDisplayName()}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            // TODO: Implementar kick functionality en NetworkBootstrapper/Fusion
            // Por ahora, solo log
            AdvancedDebugSystem.LogWarning("[LobbyController] ⚠️ Kick functionality not implemented yet", LogCategory.Networking | LogCategory.Photon);
            OnActionFailed?.Invoke("Kick functionality not implemented");
        }
        
        // ========================================
        // ✅ QUERIES Y VALIDACIONES
        // ========================================
        
        /// <summary>
        /// ¿Puede iniciar el juego?
        /// </summary>
        private bool ValidateCanStartGame()
        {
            if (_networkBootstrapper == null || _lobbyState == null) return false;
            
            // En Shared Mode, cualquier jugador puede iniciar si todos están listos
            return _networkBootstrapper.IsInRoom &&
                   _lobbyState.AllPlayersReady &&
                   _lobbyState.PlayerCount >= 1;
        }
        
        /// <summary>
        /// Razón por la que no puede iniciar
        /// </summary>
        private string GetStartGameValidationError()
        {
            if (_networkBootstrapper == null) return "Network not available";
            if (_lobbyState == null) return "Lobby state not available";
            
            // En Shared Mode, no hay restricción de host
            // if (!_networkBootstrapper.IsRoomCreator) return "Only room creator can start game";
            if (!_networkBootstrapper.IsInRoom) return "Not in a room";
            if (_lobbyState.PlayerCount < 2) return "Need at least 2 players";
            if (!_lobbyState.AllPlayersReady) return "Not all players are ready";
            
            return "Unknown error";
        }
        
        /// <summary>
        /// Obtener información completa del lobby para UI
        /// </summary>
        public LobbyInfo GetLobbyInfo()
        {
            if (_networkBootstrapper == null || _lobbyState == null)
                return null;
                
            var stats = _lobbyState.GetLobbyStats();
            
            return new LobbyInfo
            {
                RoomName = _networkBootstrapper.CurrentRoomName,
                CurrentPlayers = stats.TotalPlayers,
                MaxPlayers = stats.MaxPlayers,
                ReadyPlayers = stats.ReadyPlayers,
                IsHost = IsRoomCreator, // Usar IsRoomCreator para UI
                IsInLobby = IsInLobby,
                AllReady = stats.AllReady,
                CanStart = CanStartGame,
                CanLeave = CanLeaveLobby,
                HostName = stats.HostName,
                LocalPlayerName = stats.LocalPlayerName,
                ReadyPercentage = stats.ReadyPercentage,
                SlotsRemaining = stats.SlotsRemaining
            };
        }
        
        /// <summary>
        /// ¿Es el jugador local el creador de la sala?
        /// </summary>
        public bool IsLocalPlayerHost()
        {
            return IsRoomCreator && _lobbyState?.LocalPlayer?.IsRoomCreator == true;
        }
        
        /// <summary>
        /// Obtener jugador local
        /// </summary>
        public LobbyPlayer GetLocalPlayer()
        {
            return _lobbyState?.LocalPlayer;
        }
        
        /// <summary>
        /// Obtener lista de jugadores
        /// </summary>
        public System.Collections.Generic.List<LobbyPlayer> GetPlayersList(bool hostFirst = true)
        {
            return _lobbyState?.GetPlayersList(hostFirst) ?? new System.Collections.Generic.List<LobbyPlayer>();
        }
        
        // ========================================
        // DEBUG & VALIDATION
        // ========================================
        
        [ContextMenu("Debug: Controller Status")]
        private void DebugControllerStatus()
        {
            AdvancedDebugSystem.Log("=== LobbyController Status ===", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Instance: {Instance != null}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"LobbyState: {_lobbyState != null}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"NetworkBootstrapper: {_networkBootstrapper != null}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Is Room Creator: {IsRoomCreator}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Is In Lobby: {IsInLobby}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Can Start Game: {CanStartGame}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Can Leave Lobby: {CanLeaveLobby}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            if (!CanStartGame)
            {
                AdvancedDebugSystem.Log($"Start Game Error: {GetStartGameValidationError()}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
            
            var info = GetLobbyInfo();
            if (info != null)
            {
                AdvancedDebugSystem.Log($"Room: {info.RoomName} ({info.CurrentPlayers}/{info.MaxPlayers})", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                AdvancedDebugSystem.Log($"Ready: {info.ReadyPlayers}/{info.CurrentPlayers} ({info.ReadyPercentage:P})", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
            
            AdvancedDebugSystem.Log("================================", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        [ContextMenu("Debug: Test Start Game")]
        private void DebugTestStartGame()
        {
            AdvancedDebugSystem.Log("🧪 [DEBUG] Testing start game...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            StartGame();
        }
        
        [ContextMenu("Debug: Test Toggle Ready")]
        private void DebugTestToggleReady()
        {
            AdvancedDebugSystem.Log("🧪 [DEBUG] Testing toggle ready...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            ToggleReady();
        }
        
        // ✅ CLEANUP
        private void OnDestroy()
        {
            if (Instance == this)
            {
                AdvancedDebugSystem.Log("[LobbyController] 🧹 Instance destroyed, clearing singleton reference", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                Instance = null;
            }
        }
    }
    
    /// <summary>
    /// Información completa del lobby para UI
    /// </summary>
    [System.Serializable]
    public class LobbyInfo
    {
        public string RoomName;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int ReadyPlayers;
        public bool IsHost; // En Shared Mode, representa IsRoomCreator
        public bool IsInLobby;
        public bool AllReady;
        public bool CanStart;
        public bool CanLeave;
        public string HostName;
        public string LocalPlayerName;
        public float ReadyPercentage;
        public int SlotsRemaining;
        
        //COMPUTED PROPERTIES
        public bool IsFull => CurrentPlayers >= MaxPlayers;
        public string StatusText => AllReady ? "All Ready!" : $"{ReadyPlayers}/{CurrentPlayers} Ready";
        public string RoomCode => RoomName?.GetHashCode().ToString("X6") ?? "------";
    }
}