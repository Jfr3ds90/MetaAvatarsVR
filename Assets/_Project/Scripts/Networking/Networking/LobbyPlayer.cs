using UnityEngine;
using Fusion;
using HackMonkeys.UI.Panels;
using System.Collections;
using System.Linq;

namespace HackMonkeys.Core
{
    /// <summary>
    /// LobbyPlayer con sincronización corregida para actualizar también la instancia local
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }
        [Networked] public NetworkBool IsHost { get; set; }
        [Networked] public Color PlayerColor { get; set; }
        [Networked] public NetworkString<_64> SelectedMap { get; set; }

        private PlayerDataManager _dataManager;
        private ChangeDetector _changeDetector;
        
        public PlayerRef PlayerRef => Object.InputAuthority;
        public bool IsLocalPlayer => HasInputAuthority;

        public override void Spawned()
        {
            Debug.Log($"[LOBBYPLAYER] Player spawned: {Object.InputAuthority} - IsLocal: {HasInputAuthority}");

            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

            if (HasInputAuthority)
            {
                _dataManager = PlayerDataManager.Instance;

                if (_dataManager != null)
                {
                    RPC_SetPlayerData(
                        _dataManager.GetPlayerName(),
                        _dataManager.GetPlayerColor(),
                        Runner.IsServer
                    );
                }
            }

            TryRegisterInLobbyState();
            
            // Si es un cliente que se une después, sincronizar con el mapa del host
            if (!HasInputAuthority && !Runner.IsServer)
            {
                StartCoroutine(SyncWithHostMap());
            }
        }

        private void TryRegisterInLobbyState()
        {
            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.RegisterPlayer(this);
                // Actualización inicial
                StartCoroutine(DelayedInitialUpdate());
            }
            else
            {
                StartCoroutine(WaitForLobbyStateAndRegister());
            }
        }

        private IEnumerator DelayedInitialUpdate()
        {
            yield return new WaitForSeconds(0.1f);
            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }

        private System.Collections.IEnumerator WaitForLobbyStateAndRegister()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (LobbyState.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.RegisterPlayer(this);
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Debug.Log($"[LOBBYPLAYER] Despawning player: {PlayerName} (PlayerRef: {Object.InputAuthority})");
    
            // Desregistrar de LobbyState
            if (LobbyState.Instance != null)
            {
                Debug.Log("[LOBBYPLAYER] 👋 Unregistering from LobbyState...");
                LobbyState.Instance.UnregisterPlayer(this);
            }
    
            // Cancelar todas las coroutines
            StopAllCoroutines();
    
            // Limpiar referencias
            _dataManager = null;
            _changeDetector = null;
    
            // Si somos el jugador local, limpiar referencias adicionales
            if (IsLocalPlayer)
            {
                Debug.Log("[LOBBYPLAYER] Local player despawned, cleaning up local references");
        
                // Notificar a PlayerDataManager
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.ClearSessionData();
                }
            }
    
            // Marcar para destrucción
            if (hasState)
            {
                Debug.Log("[LOBBYPLAYER] Scheduling destruction of GameObject");
                // Destruir el GameObject después de un pequeño delay para asegurar la sincronización
                Destroy(gameObject, 0.1f);
            }
            else
            {
                // Si no hay estado, destruir inmediatamente
                Debug.Log("[LOBBYPLAYER] Destroying GameObject immediately");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Detectar cambios usando ChangeDetector para el jugador local
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            // Solo el jugador local necesita detectar sus propios cambios
            if (HasInputAuthority || HasStateAuthority)
            {
                foreach (var change in _changeDetector.DetectChanges(this))
                {
                    switch (change)
                    {
                        case nameof(IsReady):
                            Debug.Log($"[LOBBYPLAYER] Local change detected - Ready: {IsReady}");
                            if (LobbyState.Instance != null)
                            {
                                LobbyState.Instance.UpdatePlayerDisplay(this);
                            }
                            break;
                        case nameof(SelectedMap):
                            Debug.Log($"[LOBBYPLAYER] Map change detected - Map: {SelectedMap}");
                            if (IsHost && LobbyState.Instance != null)
                            {
                                LobbyState.Instance.UpdateMapSelection(SelectedMap.ToString());
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Configurar datos iniciales del jugador
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetPlayerData(NetworkString<_32> name, Color color, NetworkBool isHost)
        {
            Debug.Log($"[LOBBYPLAYER] Setting player data - Name: {name}, IsHost: {isHost}");
            
            PlayerName = string.IsNullOrEmpty(name.ToString()) ? 
                $"Player {Object.InputAuthority.PlayerId}" : name;
            PlayerColor = color;
            IsHost = isHost;
            
            // Si es el host, inicializar con el mapa por defecto
            if (isHost && string.IsNullOrEmpty(SelectedMap.ToString()))
            {
                var networkBootstrapper = NetworkBootstrapper.Instance;
                if (networkBootstrapper != null)
                {
                    string defaultMap = networkBootstrapper.GetDefaultSceneName();
                    SelectedMap = defaultMap;
                    Debug.Log($"[LOBBYPLAYER] Host initialized with default map: {defaultMap}");
                    
                    // Notificar a todos del mapa inicial
                    StartCoroutine(NotifyInitialMap());
                }
            }
        }
        
        /// <summary>
        /// Notifica el mapa inicial después de un pequeño delay
        /// </summary>
        private IEnumerator NotifyInitialMap()
        {
            yield return new WaitForSeconds(0.2f);
            
            if (HasStateAuthority && IsHost && LobbyState.Instance != null)
            {
                Debug.Log($"[LOBBYPLAYER] Notifying initial map: {SelectedMap}");
                LobbyState.Instance.UpdateMapSelection(SelectedMap.ToString());
            }
        }

        /// <summary>
        /// Toggle Ready mejorado con actualización local inmediata
        /// </summary>
        public void ToggleReady()
        {
            if (!HasInputAuthority)
            {
                Debug.LogWarning($"[LOBBYPLAYER] Cannot toggle ready - not local player");
                return;
            }

            bool newReadyState = !IsReady;
            Debug.Log($"[LOBBYPLAYER] Toggling ready to: {newReadyState}");
            
            // Actualizar UI local inmediatamente (optimistic update)
            if (LobbyState.Instance != null)
            {
                // Crear una copia temporal con el nuevo estado para actualizar la UI
                StartCoroutine(OptimisticReadyUpdate(newReadyState));
            }
            
            // Enviar RPC para actualizar en el servidor y otros clientes
            RPC_SetReadyAndNotify(newReadyState);
        }

        /// <summary>
        /// Actualización optimista de la UI mientras esperamos la sincronización
        /// </summary>
        private IEnumerator OptimisticReadyUpdate(bool newReadyState)
        {
            // Actualizar UI local inmediatamente con el estado esperado
            // Esto da feedback instantáneo al usuario
            
            var playerItem = FindObjectsOfType<LobbyPlayerItem>()
                .FirstOrDefault(item => item.GetPlayerRef() == PlayerRef);
                
            if (playerItem != null)
            {
                // Actualizar visualmente el botón ready (texto)
                var readyButton = GameObject.Find("ReadyButton")?.GetComponent<TMPro.TextMeshProUGUI>();
                if (readyButton != null && IsLocalPlayer)
                {
                    readyButton.text = newReadyState ? "Not Ready" : "Ready";
                }
            }
            
            yield return null;
        }

        /// <summary>
        /// RPC híbrido: StateAuthority actualiza, All notifica
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_SetReadyAndNotify(NetworkBool ready)
        {
            Debug.Log($"[LOBBYPLAYER] RPC_SetReadyAndNotify - Ready: {ready}, HasStateAuth: {HasStateAuthority}, IsLocal: {IsLocalPlayer}");
            
            // Solo el StateAuthority puede modificar propiedades Networked
            if (HasStateAuthority)
            {
                IsReady = ready;
                Debug.Log($"[LOBBYPLAYER] StateAuthority set IsReady to: {IsReady}");
            }
            
            // Todos actualizan su UI, incluyendo el jugador local
            StartCoroutine(DelayedUIUpdateForAll());
        }

        /// <summary>
        /// Actualización de UI con delay para asegurar sincronización
        /// </summary>
        private IEnumerator DelayedUIUpdateForAll()
        {
            // Esperar 2 frames para asegurar que Fusion sincronizó el valor
            yield return null;
            yield return null;
            
            Debug.Log($"[LOBBYPLAYER] Updating UI after delay - Name: {PlayerName}, Ready: {IsReady}, IsLocal: {IsLocalPlayer}");
            
            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }

        /// <summary>
        /// RPC alternativo: Notificar cambio a todos directamente
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastReadyChange(PlayerRef playerRef, NetworkBool isReady)
        {
            Debug.Log($"[LOBBYPLAYER] Broadcast ready change - Player: {playerRef}, Ready: {isReady}");
            
            // Buscar el jugador y actualizar su display
            var player = FindObjectsOfType<LobbyPlayer>()
                .FirstOrDefault(p => p.PlayerRef == playerRef);
                
            if (player != null && LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(player);
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
            
            StartCoroutine(DelayedMapUpdate());
        }

        private IEnumerator DelayedMapUpdate()
        {
            yield return null;
            yield return null;
            
            if (LobbyState.Instance != null && IsHost)
            {
                LobbyState.Instance.UpdateMapSelection(SelectedMap.ToString());
            }
        }

        /// <summary>
        /// Sincronizar con el mapa del host cuando un cliente se une
        /// </summary>
        private IEnumerator SyncWithHostMap()
        {
            // Esperar a que LobbyState esté listo
            yield return new WaitForSeconds(0.5f);
            
            if (LobbyState.Instance != null)
            {
                var hostPlayer = LobbyState.Instance.HostPlayer;
                if (hostPlayer != null && !string.IsNullOrEmpty(hostPlayer.SelectedMap.ToString()))
                {
                    Debug.Log($"[LOBBYPLAYER] Client syncing with host map: {hostPlayer.SelectedMap}");
                    
                    // Actualizar NetworkBootstrapper local
                    var networkBootstrapper = NetworkBootstrapper.Instance;
                    if (networkBootstrapper != null)
                    {
                        networkBootstrapper.SelectedSceneName = hostPlayer.SelectedMap.ToString();
                    }
                    
                    // Notificar a LobbyState para actualizar UI
                    LobbyState.Instance.UpdateMapSelection(hostPlayer.SelectedMap.ToString());
                }
            }
        }
        
        public void ForceCleanup()
        {
            Debug.Log($"[LOBBYPLAYER] Force cleanup called for: {PlayerName}");
    
            // Desregistrar de LobbyState
            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.UnregisterPlayer(this);
            }
    
            // Detener todas las coroutines
            StopAllCoroutines();
    
            // Destruir el GameObject
            Destroy(gameObject);
        }

        // Métodos auxiliares
        public string GetDisplayName()
        {
            string name = PlayerName.ToString();
            if (string.IsNullOrEmpty(name))
                name = "Player";

            if (IsHost)
                name += " (Host)";

            return name;
        }
        
        private void OnDestroy()
        {
            Debug.Log($"[LOBBYPLAYER] OnDestroy called for: {PlayerName}");
    
            // Última oportunidad para limpiar si no se hizo antes
            if (LobbyState.Instance != null)
            {
                var registered = LobbyState.Instance.GetPlayer(PlayerRef);
                if (registered == this)
                {
                    Debug.Log("[LOBBYPLAYER] Still registered in OnDestroy, unregistering now");
                    LobbyState.Instance.UnregisterPlayer(this);
                }
            }
        }

        public string GetStatusText()
        {
            if (IsHost)
                return IsReady ? "Ready (Host)" : "Not Ready (Host)";
            else
                return IsReady ? "Ready" : "Not Ready";
        }

        // Debug mejorado
        [ContextMenu("Debug: Toggle Ready Local")]
        private void DebugToggleReadyLocal()
        {
            if (HasInputAuthority)
            {
                Debug.Log("[DEBUG] Testing toggle ready...");
                ToggleReady();
            }
            else
            {
                Debug.LogWarning("[DEBUG] Not local player!");
            }
        }
        
        [ContextMenu("Debug: Force UI Update")]
        private void DebugForceUIUpdate()
        {
            Debug.Log($"[DEBUG] Forcing UI update - Ready: {IsReady}");
            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }
        
        [ContextMenu("Debug: Player State")]
        private void DebugPlayerState()
        {
            Debug.Log($"=== LobbyPlayer Debug ===");
            Debug.Log($"Name: {PlayerName}");
            Debug.Log($"Ready: {IsReady}");
            Debug.Log($"Host: {IsHost}");
            Debug.Log($"IsLocal: {IsLocalPlayer}");
            Debug.Log($"HasInputAuth: {HasInputAuthority}");
            Debug.Log($"HasStateAuth: {HasStateAuthority}");
            Debug.Log($"PlayerRef: {PlayerRef}");
            Debug.Log($"=========================");
        }
    }
}