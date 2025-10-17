using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;


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
                AdvancedDebugSystem.LogWarning("[LobbyState] Multiple instances detected. Destroying duplicate.", LogCategory.Networking | LogCategory.Photon);
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            AdvancedDebugSystem.Log("[LobbyState] ✅ Initialized successfully", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        /// <summary>
        /// REGISTRO DE JUGADOR - Llamado desde LobbyPlayer.Spawned()
        /// </summary>
        public void RegisterPlayer(LobbyPlayer player)
        {
            if (player == null)
            {
                AdvancedDebugSystem.LogWarning("[LobbyState] ❌ Attempted to register null player", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            PlayerRef playerRef = player.PlayerRef;
            
            if (_players.ContainsKey(playerRef))
            {
                AdvancedDebugSystem.LogWarning($"[LobbyState] Player {playerRef} already registered, updating...", LogCategory.Networking | LogCategory.Photon);
                _players[playerRef] = player;
                return;
            }
            
            _players[playerRef] = player;
            
            AdvancedDebugSystem.Log($"[LobbyState] ✅ Player registered: {player.GetDisplayName()} (Total: {PlayerCount})", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            OnPlayerJoined?.Invoke(player);
            OnPlayerCountChanged?.Invoke(PlayerCount, GetMaxPlayers());
            CheckAllPlayersReady();
            
            AdvancedDebugSystem.Log($"🧪 [LOBBYSTATE] Events fired for player join: {player.GetDisplayName()}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        /// <summary>
        /// DESREGISTRO DE JUGADOR - Llamado desde LobbyPlayer.Despawned()
        /// </summary>
        public void UnregisterPlayer(LobbyPlayer player)
        {
            if (player == null)
            {
                AdvancedDebugSystem.LogWarning("[LobbyState] ❌ Attempted to unregister null player", LogCategory.Networking | LogCategory.Photon);
                return;
            }
    
            PlayerRef playerRef = player.PlayerRef;
    
            if (!_players.ContainsKey(playerRef))
            {
                AdvancedDebugSystem.LogWarning($"[LobbyState] Player {playerRef} not found in registry", LogCategory.Networking | LogCategory.Photon);
                return;
            }
    
            // Verificar que sea el mismo objeto
            if (_players[playerRef] != player)
            {
                AdvancedDebugSystem.LogWarning($"[LobbyState] Player reference mismatch for {playerRef}", LogCategory.Networking | LogCategory.Photon);
                return;
            }
    
            _players.Remove(playerRef);
    
            AdvancedDebugSystem.Log($"[LobbyState] 👋 Player unregistered: {player.GetDisplayName()} (Remaining: {PlayerCount})", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
    
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
                AdvancedDebugSystem.LogWarning("[LobbyState] ❌ Attempted to update null player", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            if (!_players.ContainsKey(player.PlayerRef))
            {
                AdvancedDebugSystem.LogWarning($"[LobbyState] Player {player.PlayerRef} not registered, cannot update", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            AdvancedDebugSystem.Log($"[LobbyState] 🔄 Player updated: {player.GetDisplayName()} - Ready: {player.IsReady}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            OnPlayerUpdated?.Invoke(player);
            CheckAllPlayersReady();
            
            if (player.IsRoomCreator)
            {
                CheckRoomCreatorMapChange();
            }
        }
        
        public void UpdateMapSelection(string mapName)
        {
            AdvancedDebugSystem.Log($"[LobbyState] 🗺️ Map selection updated: {mapName}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
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
                AdvancedDebugSystem.LogWarning("[LobbyState] ❌ No local player found to toggle ready state", LogCategory.Networking | LogCategory.Photon);
                return;
            }
            
            AdvancedDebugSystem.Log($"[LobbyState] 🔄 Toggling ready state for local player: {LocalPlayer.GetDisplayName()}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
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
            AdvancedDebugSystem.Log("[LobbyState] 🧹 Clearing all players", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
    
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
            AdvancedDebugSystem.Log($"[LobbyState] 🎯 All players ready check: {allReady} ({PlayerCount} players)", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
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
                    AdvancedDebugSystem.Log($"[LobbyState] 🗺️ Detected host map change to: {currentMap}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
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
                    AdvancedDebugSystem.LogError($"[LobbyState] ❌ Found {kvp.Value} instances of player {kvp.Key}!", LogCategory.Networking | LogCategory.Photon);
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
            AdvancedDebugSystem.Log("[LobbyState] Checking for duplicate players...", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
    
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            var processedRefs = new HashSet<PlayerRef>();
    
            foreach (var player in allLobbyPlayers)
            {
                if (processedRefs.Contains(player.PlayerRef))
                {
                    AdvancedDebugSystem.LogWarning($"[LobbyState] Found duplicate player {player.PlayerRef}, destroying...", LogCategory.Networking | LogCategory.Photon);
            
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
    
            AdvancedDebugSystem.Log("[LobbyState] Duplicate cleanup complete", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }

        
        // DEBUG & VALIDATION
        [ContextMenu("Debug: List All Players")]
        private void DebugListPlayers()
        {
            AdvancedDebugSystem.Log($"=== LobbyState Players ({PlayerCount}/{GetMaxPlayers()}) ===", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            
            if (PlayerCount == 0)
            {
                AdvancedDebugSystem.Log("No players in lobby", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
                return;
            }
            
            foreach (var kvp in _players)
            {
                var player = kvp.Value;
                string status = $"- {player.GetDisplayName()} | Ready: {player.IsReady} | Local: {player.IsLocalPlayer}";
                
                if (player.IsRoomCreator) status += " | ROOM CREATOR";
                
                AdvancedDebugSystem.Log(status, LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
            
            AdvancedDebugSystem.Log($"All Ready: {AllPlayersReady}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Room Creator: {RoomCreatorPlayer?.GetDisplayName() ?? "None"}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Local Player: {LocalPlayer?.GetDisplayName() ?? "None"}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log("================================", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        [ContextMenu("Debug: Validate State")]
        private void DebugValidateState()
        {
            AdvancedDebugSystem.Log("=== LobbyState Validation ===", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
    
            // Verificar duplicados
            if (!ValidateNoDuplicates())
            {
                AdvancedDebugSystem.LogError("❌ Duplicate players detected!", LogCategory.Networking | LogCategory.Photon);
            }
            else
            {
                AdvancedDebugSystem.Log("✅ No duplicate players", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
    
            // Verificar objetos huérfanos
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            var orphanedCount = 0;
    
            foreach (var player in allLobbyPlayers)
            {
                if (!_players.ContainsValue(player))
                {
                    AdvancedDebugSystem.LogWarning($"⚠️ Orphaned player found: {player.GetDisplayName()}", LogCategory.Networking | LogCategory.Photon);
                    orphanedCount++;
                }
            }
    
            if (orphanedCount > 0)
            {
                AdvancedDebugSystem.LogError($"❌ Found {orphanedCount} orphaned players!", LogCategory.Networking | LogCategory.Photon);
            }
            else
            {
                AdvancedDebugSystem.Log("✅ No orphaned players", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            }
    
            AdvancedDebugSystem.Log($"Total registered: {_players.Count}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"Total in scene: {allLobbyPlayers.Length}", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log("================================", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                AdvancedDebugSystem.Log("[LobbyState] 🧹 Instance destroyed, clearing singleton reference", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        
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