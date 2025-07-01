using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Gestiona el estado del lobby y los jugadores conectados
    /// </summary>
    public class LobbyManager : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private LobbyPlayer lobbyPlayerPrefab;
        
        [Header("Events")]
        public UnityEvent<LobbyPlayer> OnPlayerJoined;
        public UnityEvent<LobbyPlayer> OnPlayerLeft;
        public UnityEvent<LobbyPlayer> OnPlayerUpdated;
        public UnityEvent<int, int> OnPlayerCountChanged; // current, max
        public UnityEvent<bool> OnAllPlayersReady; // true if all ready
        
        // Estado del lobby
        private Dictionary<PlayerRef, LobbyPlayer> _players = new Dictionary<PlayerRef, LobbyPlayer>();
        private NetworkRunner _runner;
        
        public static LobbyManager Instance { get; private set; }
        
        public IReadOnlyDictionary<PlayerRef, LobbyPlayer> Players => _players;
        public int PlayerCount => _players.Count;
        public int MaxPlayers => NetworkBootstrapper.Instance?.CurrentMaxPlayers ?? 4;
        public bool AllPlayersReady => _players.Count > 0 && _players.Values.All(p => p.IsReady);
        
        // Host info
        public LobbyPlayer HostPlayer => _players.Values.FirstOrDefault(p => p.IsHost);
        public bool IsHost => Runner != null && Runner.IsServer;
        
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
        }
        
        public override void Spawned()
        {
            _runner = Runner;
            
            // Si somos el host, esperamos a que los jugadores se conecten
            if (Runner.IsServer)
            {
                Debug.Log("[LobbyManager] Host ready, waiting for players...");
            }
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        public void RegisterPlayer(LobbyPlayer player)
        {
            if (player == null) return;
            
            PlayerRef playerRef = player.PlayerRef;
            
            if (!_players.ContainsKey(playerRef))
            {
                _players[playerRef] = player;
                Debug.Log($"[LobbyManager] Player registered: {player.GetDisplayName()}");
                
                // Notificar eventos
                OnPlayerJoined?.Invoke(player);
                OnPlayerCountChanged?.Invoke(PlayerCount, MaxPlayers);
                CheckAllPlayersReady();
            }
        }
        
        public void UnregisterPlayer(LobbyPlayer player)
        {
            if (player == null) return;
            
            PlayerRef playerRef = player.PlayerRef;
            
            if (_players.ContainsKey(playerRef))
            {
                _players.Remove(playerRef);
                Debug.Log($"[LobbyManager] Player unregistered: {player.GetDisplayName()}");
                
                // Notificar eventos
                OnPlayerLeft?.Invoke(player);
                OnPlayerCountChanged?.Invoke(PlayerCount, MaxPlayers);
                CheckAllPlayersReady();
            }
        }
        
        public void UpdatePlayerDisplay(LobbyPlayer player)
        {
            if (player == null) return;
            
            OnPlayerUpdated?.Invoke(player);
            CheckAllPlayersReady();
        }
        
        private void CheckAllPlayersReady()
        {
            OnAllPlayersReady?.Invoke(AllPlayersReady);
        }
        
        /// <summary>
        /// Obtiene un jugador específico
        /// </summary>
        public LobbyPlayer GetPlayer(PlayerRef playerRef)
        {
            return _players.TryGetValue(playerRef, out LobbyPlayer player) ? player : null;
        }
        
        /// <summary>
        /// Obtiene el jugador local
        /// </summary>
        public LobbyPlayer GetLocalPlayer()
        {
            return _players.Values.FirstOrDefault(p => p.IsLocalPlayer);
        }
        
        /// <summary>
        /// Inicia la partida (solo el host)
        /// </summary>
        public async void StartGame()
        {
            if (!IsHost)
            {
                Debug.LogError("[LobbyManager] Only host can start the game!");
                return;
            }
            
            if (!AllPlayersReady)
            {
                Debug.LogWarning("[LobbyManager] Not all players are ready!");
                return;
            }
            
            Debug.Log("[LobbyManager] Starting game...");
            
            // Usar NetworkBootstrapper para iniciar
            bool success = await NetworkBootstrapper.Instance.StartGame();
            
            if (!success)
            {
                Debug.LogError("[LobbyManager] Failed to start game!");
            }
        }
        
        /// <summary>
        /// El jugador local cambia su estado de listo
        /// </summary>
        public void ToggleLocalPlayerReady()
        {
            var localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                localPlayer.ToggleReady();
            }
        }
        
        #region Debug
        
        [ContextMenu("Debug: List All Players")]
        private void DebugListPlayers()
        {
            Debug.Log($"=== Lobby Players ({PlayerCount}/{MaxPlayers}) ===");
            foreach (var kvp in _players)
            {
                var player = kvp.Value;
                Debug.Log($"- {player.GetDisplayName()} | Ready: {player.IsReady} | Local: {player.IsLocalPlayer}");
            }
            Debug.Log($"All Ready: {AllPlayersReady}");
        }
        
        #endregion
    }
}