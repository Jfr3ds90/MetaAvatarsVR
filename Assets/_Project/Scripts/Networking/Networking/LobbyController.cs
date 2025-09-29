using UnityEngine;
using UnityEngine.Events;
using System.Threading.Tasks;

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
                Debug.LogWarning("[LobbyController] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            Debug.Log("[LobbyController] ✅ Initialized successfully");
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
                    Debug.LogWarning("[LobbyController] ⏳ Waiting for LobbyState.Instance...");
                    
                if (_networkBootstrapper == null)
                    Debug.LogWarning("[LobbyController] ⏳ Waiting for NetworkBootstrapper.Instance...");
                
                yield return new WaitForSeconds(0.1f);
            }
            
            Debug.Log("[LobbyController] ✅ All references initialized successfully");
            
            // 🧪 DEBUG LOG
            Debug.Log($"🧪 [LOBBYCONTROLLER] LobbyState: {_lobbyState != null}");
            Debug.Log($"🧪 [LOBBYCONTROLLER] NetworkBootstrapper: {_networkBootstrapper != null}");
        }
        
        // ========================================
        // ✅ ACCIONES PRINCIPALES DEL LOBBY
        // ========================================
        
        /// <summary>
        /// ✅ Iniciar partida (cualquier jugador puede iniciar en Shared Mode)
        /// </summary>
        public async void StartGame()
        {
            Debug.Log("[LobbyController] 🚀 === STARTING GAME SEQUENCE ===");
            
            // Debug de estado actual
            Debug.Log($"[LobbyController] IsRoomCreator: {IsRoomCreator}, IsInRoom: {IsInLobby}");
            Debug.Log($"[LobbyController] AllPlayersReady: {_lobbyState?.AllPlayersReady}");
            Debug.Log($"[LobbyController] PlayerCount: {_lobbyState?.PlayerCount}");
            
            PlayerDataManager.Instance.UpdateSessionPlayers(_lobbyState);
                
            string selectedMap = _lobbyState.GetSelectedMap();
            PlayerDataManager.Instance.SetSelectedMap(selectedMap);
            Debug.Log($"[LobbyController] Selected map: {selectedMap}");
            
            // VALIDACIÓN Fail Fast
            if (!ValidateCanStartGame())
            {
                string reason = GetStartGameValidationError();
                Debug.LogError($"[LobbyController] ❌ Cannot start game: {reason}");
                OnActionFailed?.Invoke($"Cannot start game: {reason}");
                OnGameStartFailed?.Invoke();
                return;
            }
            
            try
            {
                Debug.Log("[LobbyController] ✅ Validation passed, starting game...");
                OnGameStarting?.Invoke();

                string mapName = PlayerDataManager.Instance.SelectedMap;
                int playerCount = _lobbyState.PlayerCount;
                
                Debug.Log($"[LobbyController] Starting match with map: {mapName}, players: {playerCount}");

                bool coreReady = await _gameCore.StartMatch(mapName, playerCount);
                Debug.Log($"[LobbyController] GameCore.StartMatch result: {coreReady}");

                if (coreReady)
                {
                    Debug.Log($"[LobbyController] Calling NetworkBootstrapper.StartGame...");
                    bool success = await _networkBootstrapper.StartGame();
                    
                    if (success)
                    {
                        Debug.Log("[LobbyController] ✅ Game started successfully!");
                    }
                    else
                    {
                        Debug.LogError("[LobbyController] ❌ Failed to start game - NetworkBootstrapper error");
                        OnActionFailed?.Invoke("Failed to start game - network error");
                        OnGameStartFailed?.Invoke();
                    }
                }
                else
                {
                    Debug.LogError("[LobbyController] ❌ GameCore.StartMatch failed");
                    OnActionFailed?.Invoke("Failed to initialize game core");
                    OnGameStartFailed?.Invoke();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyController] ❌ Exception starting game: {e.Message}");
                Debug.LogError($"[LobbyController] Stack trace: {e.StackTrace}");
                OnActionFailed?.Invoke($"Error starting game: {e.Message}");
                OnGameStartFailed?.Invoke();
            }
        }
        
        /// <summary>
        /// Abandonar lobby
        /// </summary>
        public async void LeaveLobby()
        {
            Debug.Log("[LobbyController] 👋 Attempting to leave lobby...");
    
            if (!CanLeaveLobby)
            {
                Debug.LogWarning("[LobbyController] ❌ Not in a lobby to leave");
                OnActionFailed?.Invoke("Not in a lobby");
                return;
            }
    
            try
            {
                OnLeavingLobby?.Invoke();
        
                // IMPORTANTE: Limpiar LobbyState ANTES de desconectar
                if (_lobbyState != null)
                {
                    Debug.Log("[LobbyController] Clearing LobbyState before disconnect");
            
                    // Obtener referencia al jugador local antes de limpiar
                    var localPlayer = _lobbyState.LocalPlayer;
            
                    // Limpiar todos los jugadores del estado
                    _lobbyState.ClearAllPlayers();
            
                    // Si tenemos un jugador local, asegurar que se destruya
                    if (localPlayer != null)
                    {
                        Debug.Log("[LobbyController] Forcing cleanup of local player");
                        localPlayer.ForceCleanup();
                    }
                }
        
                // Esperar un momento para que se procesen las limpiezas
                await Task.Delay(100);
        
                // Ahora sí, desconectar de la red
                await _networkBootstrapper.LeaveRoom();
        
                Debug.Log("[LobbyController] ✅ Left lobby successfully");
        
                // Limpiar referencias locales
                CleanupLocalReferences();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyController] ❌ Exception leaving lobby: {e.Message}");
                OnActionFailed?.Invoke($"Error leaving lobby: {e.Message}");
            }
        }
        
        private void CleanupLocalReferences()
        {
            Debug.Log("[LobbyController] Cleaning up local references");
    
            // Verificar si hay LobbyPlayers huérfanos
            var orphanedPlayers = FindObjectsOfType<LobbyPlayer>();
            if (orphanedPlayers.Length > 0)
            {
                Debug.LogWarning($"[LobbyController] Found {orphanedPlayers.Length} orphaned LobbyPlayers, destroying them");
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
            Debug.Log("[LobbyController] Handling unexpected disconnection");
    
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
                Debug.LogError("[LobbyController] ❌ LobbyState not available");
                OnActionFailed?.Invoke("Lobby state not available");
                return;
            }
            
            var localPlayer = _lobbyState.LocalPlayer;
            if (localPlayer == null)
            {
                Debug.LogWarning("[LobbyController] ❌ No local player found");
                OnActionFailed?.Invoke("Local player not found");
                return;
            }
            
            Debug.Log($"[LobbyController] 🔄 Toggling ready state for: {localPlayer.GetDisplayName()}");
            
            _lobbyState.ToggleLocalPlayerReady();
        }
        
        /// <summary>
        /// Kick player (solo el creador de la sala puede kickear en Shared Mode)
        /// </summary>
        public void KickPlayer(LobbyPlayer playerToKick)
        {
            if (!IsRoomCreator)
            {
                Debug.LogError("[LobbyController] ❌ Only room creator can kick players");
                OnActionFailed?.Invoke("Only room creator can kick players");
                return;
            }
            
            if (playerToKick == null)
            {
                Debug.LogWarning("[LobbyController] ❌ Cannot kick null player");
                OnActionFailed?.Invoke("Invalid player to kick");
                return;
            }
            
            if (playerToKick.IsLocalPlayer)
            {
                Debug.LogWarning("[LobbyController] ❌ Cannot kick local player");
                OnActionFailed?.Invoke("Cannot kick yourself");
                return;
            }
            
            Debug.Log($"[LobbyController] 🥾 Kicking player: {playerToKick.GetDisplayName()}");
            
            // TODO: Implementar kick functionality en NetworkBootstrapper/Fusion
            // Por ahora, solo log
            Debug.LogWarning("[LobbyController] ⚠️ Kick functionality not implemented yet");
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
            Debug.Log("=== LobbyController Status ===");
            Debug.Log($"Instance: {Instance != null}");
            Debug.Log($"LobbyState: {_lobbyState != null}");
            Debug.Log($"NetworkBootstrapper: {_networkBootstrapper != null}");
            Debug.Log($"Is Room Creator: {IsRoomCreator}");
            Debug.Log($"Is In Lobby: {IsInLobby}");
            Debug.Log($"Can Start Game: {CanStartGame}");
            Debug.Log($"Can Leave Lobby: {CanLeaveLobby}");
            
            if (!CanStartGame)
            {
                Debug.Log($"Start Game Error: {GetStartGameValidationError()}");
            }
            
            var info = GetLobbyInfo();
            if (info != null)
            {
                Debug.Log($"Room: {info.RoomName} ({info.CurrentPlayers}/{info.MaxPlayers})");
                Debug.Log($"Ready: {info.ReadyPlayers}/{info.CurrentPlayers} ({info.ReadyPercentage:P})");
            }
            
            Debug.Log("================================");
        }
        
        [ContextMenu("Debug: Test Start Game")]
        private void DebugTestStartGame()
        {
            Debug.Log("🧪 [DEBUG] Testing start game...");
            StartGame();
        }
        
        [ContextMenu("Debug: Test Toggle Ready")]
        private void DebugTestToggleReady()
        {
            Debug.Log("🧪 [DEBUG] Testing toggle ready...");
            ToggleReady();
        }
        
        // ✅ CLEANUP
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Debug.Log("[LobbyController] 🧹 Instance destroyed, clearing singleton reference");
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