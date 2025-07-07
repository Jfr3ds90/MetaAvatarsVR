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
        
        public static LobbyController Instance { get; private set; }
        
        // ✅ PROPERTIES - Estado derivado para UI
        public bool IsHost => _networkBootstrapper?.IsHost ?? false;
        public bool IsInLobby => _networkBootstrapper?.IsInRoom ?? false;
        public bool CanStartGame => ValidateCanStartGame();
        public bool CanLeaveLobby => IsInLobby;
        
        private void Awake()
        {
            // ✅ SINGLETON PATTERN
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
            // ✅ OBTENER REFERENCIAS DESPUÉS DE QUE TODOS LOS SINGLETONS ESTÉN LISTOS
            StartCoroutine(InitializeReferences());
        }
        
        private System.Collections.IEnumerator InitializeReferences()
        {
            // Esperar hasta que las referencias estén disponibles
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
        /// ✅ ACCIÓN: Iniciar partida (solo host)
        /// </summary>
        public async void StartGame()
        {
            Debug.Log("[LobbyController] 🚀 Attempting to start game...");
            
            // ✅ VALIDACIÓN PREVIA
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
                
                // ✅ DELEGAR A NETWORKBOOTSTRAPPER
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
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyController] ❌ Exception starting game: {e.Message}");
                OnActionFailed?.Invoke($"Error starting game: {e.Message}");
                OnGameStartFailed?.Invoke();
            }
        }
        
        /// <summary>
        /// ✅ ACCIÓN: Abandonar lobby
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
                
                // ✅ DELEGAR A NETWORKBOOTSTRAPPER
                await _networkBootstrapper.LeaveRoom();
                
                Debug.Log("[LobbyController] ✅ Left lobby successfully");
                
                // ✅ LIMPIAR ESTADO LOCAL
                _lobbyState?.ClearAllPlayers();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyController] ❌ Exception leaving lobby: {e.Message}");
                OnActionFailed?.Invoke($"Error leaving lobby: {e.Message}");
            }
        }
        
        /// <summary>
        /// ✅ ACCIÓN: Toggle ready del jugador local
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
            
            // ✅ DELEGAR A LOBBYSTATE
            _lobbyState.ToggleLocalPlayerReady();
        }
        
        /// <summary>
        /// ✅ ACCIÓN: Kick player (solo host)
        /// </summary>
        public void KickPlayer(LobbyPlayer playerToKick)
        {
            if (!IsHost)
            {
                Debug.LogError("[LobbyController] ❌ Only host can kick players");
                OnActionFailed?.Invoke("Only host can kick players");
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
        /// ✅ QUERY: ¿Puede iniciar el juego?
        /// </summary>
        private bool ValidateCanStartGame()
        {
            if (_networkBootstrapper == null || _lobbyState == null) return false;
            
            return _networkBootstrapper.IsHost &&
                   _networkBootstrapper.IsInRoom &&
                   _lobbyState.AllPlayersReady &&
                   _lobbyState.PlayerCount >= 2;
        }
        
        /// <summary>
        /// ✅ QUERY: Razón por la que no puede iniciar
        /// </summary>
        private string GetStartGameValidationError()
        {
            if (_networkBootstrapper == null) return "Network not available";
            if (_lobbyState == null) return "Lobby state not available";
            
            if (!_networkBootstrapper.IsHost) return "Only host can start game";
            if (!_networkBootstrapper.IsInRoom) return "Not in a room";
            if (_lobbyState.PlayerCount < 2) return "Need at least 2 players";
            if (!_lobbyState.AllPlayersReady) return "Not all players are ready";
            
            return "Unknown error";
        }
        
        /// <summary>
        /// ✅ QUERY: Obtener información completa del lobby para UI
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
                IsHost = IsHost,
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
        /// ✅ QUERY: ¿Es el jugador local el host?
        /// </summary>
        public bool IsLocalPlayerHost()
        {
            return IsHost && _lobbyState?.LocalPlayer?.IsHost == true;
        }
        
        /// <summary>
        /// ✅ QUERY: Obtener jugador local
        /// </summary>
        public LobbyPlayer GetLocalPlayer()
        {
            return _lobbyState?.LocalPlayer;
        }
        
        /// <summary>
        /// ✅ QUERY: Obtener lista de jugadores
        /// </summary>
        public System.Collections.Generic.List<LobbyPlayer> GetPlayersList(bool hostFirst = true)
        {
            return _lobbyState?.GetPlayersList(hostFirst) ?? new System.Collections.Generic.List<LobbyPlayer>();
        }
        
        // ========================================
        // ✅ DEBUG & VALIDATION
        // ========================================
        
        [ContextMenu("Debug: Controller Status")]
        private void DebugControllerStatus()
        {
            Debug.Log("=== LobbyController Status ===");
            Debug.Log($"Instance: {Instance != null}");
            Debug.Log($"LobbyState: {_lobbyState != null}");
            Debug.Log($"NetworkBootstrapper: {_networkBootstrapper != null}");
            Debug.Log($"Is Host: {IsHost}");
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
    /// ✅ DTO: Información completa del lobby para UI
    /// </summary>
    [System.Serializable]
    public class LobbyInfo
    {
        public string RoomName;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int ReadyPlayers;
        public bool IsHost;
        public bool IsInLobby;
        public bool AllReady;
        public bool CanStart;
        public bool CanLeave;
        public string HostName;
        public string LocalPlayerName;
        public float ReadyPercentage;
        public int SlotsRemaining;
        
        // ✅ COMPUTED PROPERTIES
        public bool IsFull => CurrentPlayers >= MaxPlayers;
        public string StatusText => AllReady ? "All Ready!" : $"{ReadyPlayers}/{CurrentPlayers} Ready";
        public string RoomCode => RoomName?.GetHashCode().ToString("X6") ?? "------";
    }
}