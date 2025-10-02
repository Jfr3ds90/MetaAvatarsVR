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
        public UnityEvent<string> OnMapChanged;
        public UnityEvent<int, int> OnPlayerCountChanged; 
        public UnityEvent<bool> OnAllPlayersReady;
        
        private Dictionary<PlayerRef, LobbyPlayer> _players = new Dictionary<PlayerRef, LobbyPlayer>();
        
        public static LobbyState Instance { get; private set; }
        
        public IReadOnlyDictionary<PlayerRef, LobbyPlayer> Players => _players;
        public int PlayerCount => _players.Count;
        public bool AllPlayersReady => _players.Count > 0 && _players.Values.All(p => p.IsReady);
        
        // En Shared Mode, usamos RoomCreatorPlayer
        public LobbyPlayer RoomCreatorPlayer => _players.Values.FirstOrDefault(p => p.IsRoomCreator);
        // DEPRECATED: Mantener HostPlayer para compatibilidad
        public LobbyPlayer HostPlayer => RoomCreatorPlayer;
        public LobbyPlayer LocalPlayer => _players.Values.FirstOrDefault(p => p.IsLocalPlayer);
        private string _lastKnownMap = "";
        private void Awake()
        {
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
        /// REGISTRO DE JUGADOR - Llamado desde LobbyPlayer.Spawned()
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
            
            _players[playerRef] = player;
            
            Debug.Log($"[LobbyState] ✅ Player registered: {player.GetDisplayName()} (Total: {PlayerCount})");
            
            OnPlayerJoined?.Invoke(player);
            OnPlayerCountChanged?.Invoke(PlayerCount, GetMaxPlayers());
            CheckAllPlayersReady();
            
            Debug.Log($"🧪 [LOBBYSTATE] Events fired for player join: {player.GetDisplayName()}");
        }
        
        /// <summary>
        /// DESREGISTRO DE JUGADOR - Llamado desde LobbyPlayer.Despawned()
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
    
            // Verificar que sea el mismo objeto
            if (_players[playerRef] != player)
            {
                Debug.LogWarning($"[LobbyState] Player reference mismatch for {playerRef}");
                return;
            }
    
            _players.Remove(playerRef);
    
            Debug.Log($"[LobbyState] 👋 Player unregistered: {player.GetDisplayName()} (Remaining: {PlayerCount})");
    
            OnPlayerLeft?.Invoke(player);
            OnPlayerCountChanged?.Invoke(PlayerCount, GetMaxPlayers());
            CheckAllPlayersReady();
        }
        
        /// <summary>
        /// ACTUALIZACIÓN DE JUGADOR - Llamado desde LobbyPlayer change detection
        /// </summary>
        public void UpdatePlayerDisplay(LobbyPlayer player)
        {
            if (player == null)
            {
                Debug.LogWarning("[LobbyState] ❌ Attempted to update null player");
                return;
            }
            
            if (!_players.ContainsKey(player.PlayerRef))
            {
                Debug.LogWarning($"[LobbyState] Player {player.PlayerRef} not registered, cannot update");
                return;
            }
            
            Debug.Log($"[LobbyState] 🔄 Player updated: {player.GetDisplayName()} - Ready: {player.IsReady}");
            
            OnPlayerUpdated?.Invoke(player);
            CheckAllPlayersReady();
            
            if (player.IsRoomCreator)
            {
                CheckRoomCreatorMapChange();
            }
        }
        
        public void UpdateMapSelection(string mapName)
        {
            Debug.Log($"[LobbyState] 🗺️ Map selection updated: {mapName}");
            OnMapChanged?.Invoke(mapName);
        }
        
        public string GetSelectedMap()
        {
            var host = RoomCreatorPlayer;
            if (host != null)
            {
                return host.SelectedMap.ToString();
            }
            return "";
        }
        
        /// <summary>
        /// OBTENER JUGADOR ESPECÍFICO
        /// </summary>
        public LobbyPlayer GetPlayer(PlayerRef playerRef)
        {
            return _players.TryGetValue(playerRef, out LobbyPlayer player) ? player : null;
        }
        
        /// <summary>
        /// TOGGLE READY STATE - Solo para jugador local
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
        /// OBTENER LISTA DE JUGADORES ORDENADA
        /// </summary>
        public List<LobbyPlayer> GetPlayersList(bool hostFirst = true)
        {
            var playersList = _players.Values.Where(p => p != null).ToList();
    
            if (playersList.Count == 0)
                return new List<LobbyPlayer>();
    
            if (hostFirst)
            {
                return playersList.OrderByDescending(p => p.IsRoomCreator ? 1 : 0)
                    .ThenByDescending(p => p.IsLocalPlayer ? 1 : 0)
                    .ThenBy(p => {
                        string name = p.PlayerName.ToString();
                        return string.IsNullOrEmpty(name) ? "Unknown" : name;
                    })
                    .ToList();
            }
    
            return playersList.OrderBy(p => {
                string name = p.PlayerName.ToString();
                return string.IsNullOrEmpty(name) ? "Unknown" : name;
            }).ToList();
        }
        
        /// <summary>
        /// LIMPIAR TODOS LOS JUGADORES
        /// </summary>
        public void ClearAllPlayers()
        {
            Debug.Log("[LobbyState] 🧹 Clearing all players");
    
            // Crear copia de la lista para evitar modificación durante iteración
            var playersToRemove = _players.Values.ToList();
    
            // Limpiar el diccionario primero
            _players.Clear();
    
            // Notificar la salida de cada jugador
            foreach (var player in playersToRemove)
            {
                if (player != null)
                {
                    OnPlayerLeft?.Invoke(player);
                }
            }
    
            OnPlayerCountChanged?.Invoke(0, GetMaxPlayers());
            CheckAllPlayersReady();
        }
        
        // MÉTODOS AUXILIARES PRIVADOS
        private void CheckAllPlayersReady()
        {
            bool allReady = AllPlayersReady;
            Debug.Log($"[LobbyState] 🎯 All players ready check: {allReady} ({PlayerCount} players)");
            OnAllPlayersReady?.Invoke(allReady);
        }
        
        public void CheckRoomCreatorMapChange()
        {
            var host = RoomCreatorPlayer;
            if (host != null)
            {
                string currentMap = host.SelectedMap.ToString();
                if (!string.IsNullOrEmpty(currentMap) && currentMap != _lastKnownMap)
                {
                    _lastKnownMap = currentMap;
                    Debug.Log($"[LobbyState] 🗺️ Detected host map change to: {currentMap}");
                    OnMapChanged?.Invoke(currentMap);
                }
            }
        }
        
        public bool ValidateNoDuplicates()
        {
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            var playerRefCounts = new Dictionary<PlayerRef, int>();
    
            foreach (var player in allLobbyPlayers)
            {
                if (!playerRefCounts.ContainsKey(player.PlayerRef))
                    playerRefCounts[player.PlayerRef] = 0;
                playerRefCounts[player.PlayerRef]++;
            }
    
            bool hasDuplicates = false;
            foreach (var kvp in playerRefCounts)
            {
                if (kvp.Value > 1)
                {
                    Debug.LogError($"[LobbyState] ❌ Found {kvp.Value} instances of player {kvp.Key}!");
                    hasDuplicates = true;
                }
            }
    
            return !hasDuplicates;
        }
        
        private int GetMaxPlayers()
        {
            return NetworkBootstrapper.Instance?.CurrentMaxPlayers ?? 4;
        }
        
        [ContextMenu("Clean Duplicate Players")]
        public void CleanDuplicatePlayers()
        {
            Debug.Log("[LobbyState] Checking for duplicate players...");
    
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            var processedRefs = new HashSet<PlayerRef>();
    
            foreach (var player in allLobbyPlayers)
            {
                if (processedRefs.Contains(player.PlayerRef))
                {
                    Debug.LogWarning($"[LobbyState] Found duplicate player {player.PlayerRef}, destroying...");
            
                    // Desregistrar si está registrado
                    if (_players.ContainsKey(player.PlayerRef) && _players[player.PlayerRef] == player)
                    {
                        _players.Remove(player.PlayerRef);
                    }
            
                    // Destruir el duplicado
                    Destroy(player.gameObject);
                }
                else
                {
                    processedRefs.Add(player.PlayerRef);
                }
            }
    
            Debug.Log("[LobbyState] Duplicate cleanup complete");
        }

        
        // DEBUG & VALIDATION
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
                
                if (player.IsRoomCreator) status += " | ROOM CREATOR";
                
                Debug.Log(status);
            }
            
            Debug.Log($"All Ready: {AllPlayersReady}");
            Debug.Log($"Room Creator: {RoomCreatorPlayer?.GetDisplayName() ?? "None"}");
            Debug.Log($"Local Player: {LocalPlayer?.GetDisplayName() ?? "None"}");
            Debug.Log("================================");
        }
        
        [ContextMenu("Debug: Validate State")]
        private void DebugValidateState()
        {
            Debug.Log("=== LobbyState Validation ===");
    
            // Verificar duplicados
            if (!ValidateNoDuplicates())
            {
                Debug.LogError("❌ Duplicate players detected!");
            }
            else
            {
                Debug.Log("✅ No duplicate players");
            }
    
            // Verificar objetos huérfanos
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            var orphanedCount = 0;
    
            foreach (var player in allLobbyPlayers)
            {
                if (!_players.ContainsValue(player))
                {
                    Debug.LogWarning($"⚠️ Orphaned player found: {player.GetDisplayName()}");
                    orphanedCount++;
                }
            }
    
            if (orphanedCount > 0)
            {
                Debug.LogError($"❌ Found {orphanedCount} orphaned players!");
            }
            else
            {
                Debug.Log("✅ No orphaned players");
            }
    
            Debug.Log($"Total registered: {_players.Count}");
            Debug.Log($"Total in scene: {allLobbyPlayers.Length}");
            Debug.Log("================================");
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Debug.Log("[LobbyState] 🧹 Instance destroyed, clearing singleton reference");
        
                // Limpiar todos los jugadores antes de destruir
                ClearAllPlayers();
        
                Instance = null;
            }
        }
        
        public LobbyStats GetLobbyStats()
        {
            return new LobbyStats
            {
                TotalPlayers = PlayerCount,
                MaxPlayers = GetMaxPlayers(),
                ReadyPlayers = _players.Values.Count(p => p.IsReady),
                AllReady = AllPlayersReady,
                HasHost = RoomCreatorPlayer != null, // Represents room creator in Shared Mode
                HasLocalPlayer = LocalPlayer != null,
                HostName = RoomCreatorPlayer?.GetDisplayName() ?? "None", // Room creator name
                LocalPlayerName = LocalPlayer?.GetDisplayName() ?? "None"
            };
        }
    }
    
 
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