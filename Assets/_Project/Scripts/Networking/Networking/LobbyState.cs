using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace HackMonkeys.Core
{
    /// <summary>
    /// LobbyState - SOLO manejo de datos de jugadores y eventos
    /// Patrón Repository limpio - NO hereda de NetworkBehaviour
    /// </summary>
    public class LobbyState : MonoBehaviour
    {
        [Header("Events - Solo para UI")]
        public UnityEvent<LobbyPlayer> OnPlayerJoined;
        public UnityEvent<LobbyPlayer> OnPlayerLeft;
        public UnityEvent<LobbyPlayer> OnPlayerUpdated;
        public UnityEvent<int, int> OnPlayerCountChanged; // current, max
        public UnityEvent<bool> OnAllPlayersReady;
        
        // ✅ ESTADO DEL LOBBY - Solo datos
        private Dictionary<PlayerRef, LobbyPlayer> _players = new Dictionary<PlayerRef, LobbyPlayer>();
        
        public static LobbyState Instance { get; private set; }
        
        // ✅ PROPERTIES READ-ONLY - Acceso controlado a los datos
        public IReadOnlyDictionary<PlayerRef, LobbyPlayer> Players => _players;
        public int PlayerCount => _players.Count;
        public bool AllPlayersReady => _players.Count > 0 && _players.Values.All(p => p.IsReady);
        
        // ✅ COMPUTED PROPERTIES - Calculadas en tiempo real
        public LobbyPlayer HostPlayer => _players.Values.FirstOrDefault(p => p.IsHost);
        public LobbyPlayer LocalPlayer => _players.Values.FirstOrDefault(p => p.IsLocalPlayer);
        
        private void Awake()
        {
            // ✅ SINGLETON PATTERN SIMPLE
            if (Instance != null)
            {
                Debug.LogWarning("[LobbyState] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            Debug.Log("[LobbyState] ✅ Initialized successfully");
        }
        
        /// <summary>
        /// ✅ REGISTRO DE JUGADOR - Llamado desde LobbyPlayer.Spawned()
        /// </summary>
        public void RegisterPlayer(LobbyPlayer player)
        {
            if (player == null)
            {
                Debug.LogWarning("[LobbyState] ❌ Attempted to register null player");
                return;
            }
            
            PlayerRef playerRef = player.PlayerRef;
            
            if (_players.ContainsKey(playerRef))
            {
                Debug.LogWarning($"[LobbyState] Player {playerRef} already registered, updating...");
                _players[playerRef] = player;
                return;
            }
            
            // ✅ AÑADIR JUGADOR
            _players[playerRef] = player;
            
            Debug.Log($"[LobbyState] ✅ Player registered: {player.GetDisplayName()} (Total: {PlayerCount})");
            
            // ✅ DISPARAR EVENTOS
            OnPlayerJoined?.Invoke(player);
            OnPlayerCountChanged?.Invoke(PlayerCount, GetMaxPlayers());
            CheckAllPlayersReady();
            
            // 🧪 DEBUG LOG
            Debug.Log($"🧪 [LOBBYSTATE] Events fired for player join: {player.GetDisplayName()}");
        }
        
        /// <summary>
        /// ✅ DESREGISTRO DE JUGADOR - Llamado desde LobbyPlayer.Despawned()
        /// </summary>
        public void UnregisterPlayer(LobbyPlayer player)
        {
            if (player == null)
            {
                Debug.LogWarning("[LobbyState] ❌ Attempted to unregister null player");
                return;
            }
            
            PlayerRef playerRef = player.PlayerRef;
            
            if (!_players.ContainsKey(playerRef))
            {
                Debug.LogWarning($"[LobbyState] Player {playerRef} not found in registry");
                return;
            }
            
            // ✅ REMOVER JUGADOR
            _players.Remove(playerRef);
            
            Debug.Log($"[LobbyState] 👋 Player unregistered: {player.GetDisplayName()} (Remaining: {PlayerCount})");
            
            // ✅ DISPARAR EVENTOS
            OnPlayerLeft?.Invoke(player);
            OnPlayerCountChanged?.Invoke(PlayerCount, GetMaxPlayers());
            CheckAllPlayersReady();
        }
        
        /// <summary>
        /// ✅ ACTUALIZACIÓN DE JUGADOR - Llamado desde LobbyPlayer change detection
        /// </summary>
        public void UpdatePlayerDisplay(LobbyPlayer player)
        {
            if (player == null)
            {
                Debug.LogWarning("[LobbyState] ❌ Attempted to update null player");
                return;
            }
            
            // Verificar que el jugador está registrado
            if (!_players.ContainsKey(player.PlayerRef))
            {
                Debug.LogWarning($"[LobbyState] Player {player.PlayerRef} not registered, cannot update");
                return;
            }
            
            Debug.Log($"[LobbyState] 🔄 Player updated: {player.GetDisplayName()} - Ready: {player.IsReady}");
            
            // ✅ DISPARAR EVENTOS
            OnPlayerUpdated?.Invoke(player);
            CheckAllPlayersReady();
        }
        
        /// <summary>
        /// ✅ OBTENER JUGADOR ESPECÍFICO
        /// </summary>
        public LobbyPlayer GetPlayer(PlayerRef playerRef)
        {
            return _players.TryGetValue(playerRef, out LobbyPlayer player) ? player : null;
        }
        
        /// <summary>
        /// ✅ TOGGLE READY STATE - Solo para jugador local
        /// </summary>
        public void ToggleLocalPlayerReady()
        {
            if (LocalPlayer == null)
            {
                Debug.LogWarning("[LobbyState] ❌ No local player found to toggle ready state");
                return;
            }
            
            Debug.Log($"[LobbyState] 🔄 Toggling ready state for local player: {LocalPlayer.GetDisplayName()}");
            LocalPlayer.ToggleReady();
        }
        
        /// <summary>
        /// ✅ OBTENER LISTA DE JUGADORES ORDENADA
        /// </summary>
        public List<LobbyPlayer> GetPlayersList(bool hostFirst = true)
        {
            var playersList = _players.Values.ToList();
            
            if (hostFirst)
            {
                // Host primero, luego jugador local, luego otros
                return playersList.OrderByDescending(p => p.IsHost)
                                 .ThenByDescending(p => p.IsLocalPlayer)
                                 .ThenBy(p => p.PlayerName.ToString())
                                 .ToList();
            }
            
            return playersList.OrderBy(p => p.PlayerName.ToString()).ToList();
        }
        
        /// <summary>
        /// ✅ LIMPIAR TODOS LOS JUGADORES
        /// </summary>
        public void ClearAllPlayers()
        {
            Debug.Log("[LobbyState] 🧹 Clearing all players");
            
            var playersToRemove = _players.Values.ToList();
            _players.Clear();
            
            // Notificar que cada jugador se fue
            foreach (var player in playersToRemove)
            {
                OnPlayerLeft?.Invoke(player);
            }
            
            OnPlayerCountChanged?.Invoke(0, GetMaxPlayers());
            CheckAllPlayersReady();
        }
        
        // ✅ MÉTODOS AUXILIARES PRIVADOS
        private void CheckAllPlayersReady()
        {
            bool allReady = AllPlayersReady;
            Debug.Log($"[LobbyState] 🎯 All players ready check: {allReady} ({PlayerCount} players)");
            OnAllPlayersReady?.Invoke(allReady);
        }
        
        private int GetMaxPlayers()
        {
            return NetworkBootstrapper.Instance?.CurrentMaxPlayers ?? 4;
        }
        
        // ✅ DEBUG & VALIDATION
        [ContextMenu("Debug: List All Players")]
        private void DebugListPlayers()
        {
            Debug.Log($"=== LobbyState Players ({PlayerCount}/{GetMaxPlayers()}) ===");
            
            if (PlayerCount == 0)
            {
                Debug.Log("No players in lobby");
                return;
            }
            
            foreach (var kvp in _players)
            {
                var player = kvp.Value;
                string status = $"- {player.GetDisplayName()} | Ready: {player.IsReady} | Local: {player.IsLocalPlayer}";
                
                if (player.IsHost) status += " | HOST";
                
                Debug.Log(status);
            }
            
            Debug.Log($"All Ready: {AllPlayersReady}");
            Debug.Log($"Host Player: {HostPlayer?.GetDisplayName() ?? "None"}");
            Debug.Log($"Local Player: {LocalPlayer?.GetDisplayName() ?? "None"}");
            Debug.Log("================================");
        }
        
        [ContextMenu("Debug: Validate State")]
        private void DebugValidateState()
        {
            Debug.Log("=== LobbyState Validation ===");
            
            // Verificar integridad de datos
            int nullPlayers = _players.Values.Count(p => p == null);
            if (nullPlayers > 0)
            {
                Debug.LogError($"❌ Found {nullPlayers} null players in registry!");
            }
            else
            {
                Debug.Log("✅ No null players found");
            }
            
            // Verificar host único
            var hosts = _players.Values.Where(p => p.IsHost).ToList();
            if (hosts.Count > 1)
            {
                Debug.LogError($"❌ Multiple hosts detected: {hosts.Count}");
            }
            else if (hosts.Count == 1)
            {
                Debug.Log($"✅ Single host: {hosts[0].GetDisplayName()}");
            }
            else
            {
                Debug.LogWarning("⚠️ No host found");
            }
            
            // Verificar local player único
            var localPlayers = _players.Values.Where(p => p.IsLocalPlayer).ToList();
            if (localPlayers.Count > 1)
            {
                Debug.LogError($"❌ Multiple local players detected: {localPlayers.Count}");
            }
            else if (localPlayers.Count == 1)
            {
                Debug.Log($"✅ Single local player: {localPlayers[0].GetDisplayName()}");
            }
            else
            {
                Debug.LogWarning("⚠️ No local player found");
            }
            
            Debug.Log("================================");
        }
        
        // ✅ CLEANUP
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Debug.Log("[LobbyState] 🧹 Instance destroyed, clearing singleton reference");
                Instance = null;
            }
        }
        
        // ✅ ESTADÍSTICAS PARA UI
        public LobbyStats GetLobbyStats()
        {
            return new LobbyStats
            {
                TotalPlayers = PlayerCount,
                MaxPlayers = GetMaxPlayers(),
                ReadyPlayers = _players.Values.Count(p => p.IsReady),
                AllReady = AllPlayersReady,
                HasHost = HostPlayer != null,
                HasLocalPlayer = LocalPlayer != null,
                HostName = HostPlayer?.GetDisplayName() ?? "None",
                LocalPlayerName = LocalPlayer?.GetDisplayName() ?? "None"
            };
        }
    }
    
    /// <summary>
    /// ✅ DTO: Estadísticas del lobby para debugging y UI
    /// </summary>
    [System.Serializable]
    public struct LobbyStats
    {
        public int TotalPlayers;
        public int MaxPlayers;
        public int ReadyPlayers;
        public bool AllReady;
        public bool HasHost;
        public bool HasLocalPlayer;
        public string HostName;
        public string LocalPlayerName;
        
        public float ReadyPercentage => TotalPlayers > 0 ? (float)ReadyPlayers / TotalPlayers : 0f;
        public bool IsFull => TotalPlayers >= MaxPlayers;
        public int SlotsRemaining => MaxPlayers - TotalPlayers;
    }
}