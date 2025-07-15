using UnityEngine;
using Fusion;
using HackMonkeys.UI.Panels;
using TMPro;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Representa a un jugador en el lobby con datos sincronizados en red
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        [Networked] public NetworkString<_32> PlayerName { get; set; }

        [Networked] public NetworkBool IsReady { get; set; }

        [Networked] public NetworkBool IsHost { get; set; }

        [Networked] public Color PlayerColor { get; set; }
        
        [property: Networked] public NetworkString<_64> SelectedMap { get; set; }

        private PlayerDataManager _dataManager;
        private ChangeDetector _changeDetector;

        private NetworkString<_32> _previousName;
        private NetworkBool _previousReady;

        public PlayerRef PlayerRef => Object.InputAuthority;
        public bool IsLocalPlayer => HasInputAuthority;

        public override void Spawned()
        {
            Debug.Log($"🧪 [LOBBYPLAYER] ========== SPAWNED ==========");
            Debug.Log($"🧪 [LOBBYPLAYER] Player spawned: {Object.InputAuthority}");
            Debug.Log($"🧪 [LOBBYPLAYER] Is local player: {HasInputAuthority}");
            Debug.Log($"🧪 [LOBBYPLAYER] Frame: {Time.frameCount}");

            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

            if (HasInputAuthority)
            {
                Debug.Log("🧪 [LOBBYPLAYER] Configuring local player data...");

                _dataManager = PlayerDataManager.Instance;

                if (_dataManager == null)
                {
                    Debug.LogError("🧪 [LOBBYPLAYER] ❌ PlayerPrefsManager.Instance is NULL!");
                    RPC_SetPlayerData(
                        $"Player {Object.InputAuthority.PlayerId}",
                        Color.HSVToRGB(Random.Range(0f, 1f), 0.8f, 1f),
                        Runner.IsServer
                    );
                }
                else
                {
                    RPC_SetPlayerData(
                        _dataManager.GetPlayerName(),
                        _dataManager.GetPlayerColor(),
                        Runner.IsServer 
                    );

                    Debug.Log($"🧪 [LOBBYPLAYER] Player name: {_dataManager.GetPlayerName()}");
                    Debug.Log($"🧪 [LOBBYPLAYER] Is host: {Runner.IsServer}");
                    Debug.Log($"🧪 [LOBBYPLAYER] Player color: {_dataManager.GetPlayerColor()}");
                }
            }

            TryRegisterInLobbyState();

            _previousName = PlayerName;
            _previousReady = IsReady;

            Debug.Log($"🧪 [LOBBYPLAYER] ========== END SPAWNED ==========");
        }

        private void TryRegisterInLobbyState()
        {
            Debug.Log("🧪 [LOBBYPLAYER] Attempting to register in LobbyState...");

            if (LobbyState.Instance != null)
            {
                Debug.Log("🧪 [LOBBYPLAYER] ✅ LobbyState found immediately, registering...");
                LobbyState.Instance.RegisterPlayer(this);
                Debug.Log("🧪 [LOBBYPLAYER] ✅ Player registered successfully in LobbyState");
            }
            else
            {
                Debug.LogWarning("🧪 [LOBBYPLAYER] ⚠️ LobbyState.Instance is NULL! Starting coroutine fallback...");
                StartCoroutine(WaitForLobbyStateAndRegister());
            }
        }

        private System.Collections.IEnumerator WaitForLobbyStateAndRegister()
        {
            Debug.Log("🧪 [LOBBYPLAYER] === Starting WaitForLobbyState Coroutine ===");

            float timeout = 5f;
            float elapsed = 0f;
            int attempts = 0;

            while (LobbyState.Instance == null && elapsed < timeout)
            {
                attempts++;
                Debug.Log($"🧪 [LOBBYPLAYER] Attempt {attempts} - Waiting for LobbyState... (elapsed: {elapsed:F1}s)");

                if (attempts % 5 == 0) 
                {
                    var lobbyStateInScene = FindObjectOfType<LobbyState>();
                    if (lobbyStateInScene != null)
                    {
                        Debug.LogWarning("🧪 [LOBBYPLAYER] Found LobbyState in scene but Instance is still null!");
                    }
                }

                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (LobbyState.Instance != null)
            {
                Debug.Log($"🧪 [LOBBYPLAYER] ✅ LobbyState found after {elapsed:F1}s and {attempts} attempts!");
                LobbyState.Instance.RegisterPlayer(this);
                Debug.Log("🧪 [LOBBYPLAYER] ✅ Player registered successfully via coroutine");

                var lobbyRoom = FindObjectOfType<LobbyRoom>();
                if (lobbyRoom != null && lobbyRoom.gameObject.activeInHierarchy)
                {
                    Debug.Log("🧪 [LOBBYPLAYER] Triggering LobbyRoom refresh...");
                    var method = lobbyRoom.GetType().GetMethod("RefreshPlayersList",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(lobbyRoom, null);
                }
            }
            else
            {
                Debug.LogError($"🧪 [LOBBYPLAYER] ❌ CRITICAL: LobbyState never initialized after {timeout}s!");
                Debug.LogError("🧪 [LOBBYPLAYER] ❌ Player will not appear in lobby UI!");

                var lobbyState = FindObjectOfType<LobbyState>();
                if (lobbyState != null)
                {
                    Debug.LogWarning(
                        "🧪 [LOBBYPLAYER] Last resort: Found LobbyState via FindObjectOfType, forcing registration");
                    lobbyState.RegisterPlayer(this);
                }
            }

            Debug.Log("🧪 [LOBBYPLAYER] === End WaitForLobbyState Coroutine ===");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Debug.Log($"🧪 [LOBBYPLAYER] Player despawned: {Object.InputAuthority}");

            if (LobbyState.Instance != null)
            {
                Debug.Log("🧪 [LOBBYPLAYER] 👋 Unregistering player from LobbyState...");
                LobbyState.Instance.UnregisterPlayer(this);
                Debug.Log("🧪 [LOBBYPLAYER] ✅ Player unregistered successfully from LobbyState");
            }
            else
            {
                Debug.LogWarning("🧪 [LOBBYPLAYER] ⚠️ LobbyState.Instance is null during despawn");
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetPlayerData(NetworkString<_32> name, Color color, NetworkBool isHost)
        {
            Debug.Log($"🧪 [LOBBYPLAYER] RPC_SetPlayerData called:");
            Debug.Log($"  - Name: '{name}'");
            Debug.Log($"  - IsHost: {isHost}");
            Debug.Log($"  - Color: {color}");
    
            if (string.IsNullOrEmpty(name.ToString()))
            {
                Debug.LogWarning("🧪 [LOBBYPLAYER] Empty name received, using default");
                PlayerName = $"Player {Object.InputAuthority.PlayerId}";
            }
            else
            {
                PlayerName = name;
            }
    
            PlayerColor = color;
            IsHost = isHost;
    
            Debug.Log($"🧪 [LOBBYPLAYER] ✅ Player data set via RPC");
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            Debug.Log($"🧪 [LOBBYPLAYER] RPC_SetReady called - Ready: {ready} for player: {PlayerName}");

            IsReady = ready;

            Debug.Log($"🧪 [LOBBYPLAYER] ✅ Ready state set via RPC");
        }
        
        // RPC para cambiar el mapa (solo el host puede llamarlo)
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_ChangeMap(NetworkString<_64> mapName)
        {
            Debug.Log($"🧪 [LOBBYPLAYER] 📡 RPC_ChangeMap called");
            Debug.Log($"🧪 [LOBBYPLAYER] 📡 Map parameter: {mapName}");
            Debug.Log($"🧪 [LOBBYPLAYER] 📡 Called by player: {Object.InputAuthority}");
            Debug.Log($"🧪 [LOBBYPLAYER] 📡 This player IsHost: {IsHost}");
    
            if (!IsHost)
            {
                Debug.LogWarning($"🧪 [LOBBYPLAYER] ❌ Non-host tried to change map!");
                return;
            }
    
            Debug.Log($"🧪 [LOBBYPLAYER] 📡 Setting SelectedMap from '{SelectedMap}' to '{mapName}'");
            SelectedMap = mapName;
            Debug.Log($"🧪 [LOBBYPLAYER] ✅ Map changed successfully to: {SelectedMap}");
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority || HasInputAuthority)
            {
                foreach (var change in _changeDetector.DetectChanges(this))
                {
                    switch (change)
                    {
                        case nameof(PlayerName):
                            OnNameChangedCallback();
                            break;
                        case nameof(IsReady):
                            OnReadyChangedCallback();
                            break;
                        case nameof(PlayerColor):
                            OnColorChangedCallback();
                            break;
                        case nameof(IsHost):
                            OnHostChangedCallback();
                            break;
                        case nameof(SelectedMap):
                            OnMapChangedCallback();
                            break;
                    }
                }
            }
    
            if (!IsHost && LobbyState.Instance != null)
            {
                LobbyState.Instance.CheckHostMapChange();
            }
        }

        private void OnNameChangedCallback()
        {
            Debug.Log($"🧪 [LOBBYPLAYER] 🔄 Name changed to: {PlayerName.ToString()}");

            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
            else
            {
                Debug.LogWarning("🧪 [LOBBYPLAYER] ⚠️ Cannot update display - LobbyState.Instance is null");
            }
        }

        private void OnReadyChangedCallback()
        {
            Debug.Log($"🧪 [LOBBYPLAYER] 🔄 Ready state changed to: {IsReady} for player: {PlayerName}");

            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
            else
            {
                Debug.LogWarning("🧪 [LOBBYPLAYER] ⚠️ Cannot update display - LobbyState.Instance is null");
            }
        }

        private void OnColorChangedCallback()
        {
            Debug.Log($"🧪 [LOBBYPLAYER] 🔄 Color changed for player: {PlayerName}");

            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }

        private void OnHostChangedCallback()
        {
            Debug.Log($"🧪 [LOBBYPLAYER] 🔄 Host status changed to: {IsHost} for player: {PlayerName}");

            if (LobbyState.Instance != null)
            {
                LobbyState.Instance.UpdatePlayerDisplay(this);
            }
        }
        
        private void OnMapChangedCallback()
        {
            Debug.Log($"🧪 [LOBBYPLAYER] 🗺️ Map changed to: {SelectedMap.ToString()}");
            Debug.Log($"🧪 [LOBBYPLAYER] 🗺️ This player IsHost: {IsHost}, IsLocal: {IsLocalPlayer}");
            
            if (IsHost && LobbyState.Instance != null)
            {
                Debug.Log($"🧪 [LOBBYPLAYER] 🗺️ Host player map changed, notifying all clients!");
                // Notificar a la UI que el mapa cambió
                LobbyState.Instance.UpdateMapSelection(SelectedMap.ToString());
            }
        }

        public void ToggleReady()
        {
            if (!HasInputAuthority)
            {
                Debug.LogWarning($"🧪 [LOBBYPLAYER] ❌ Cannot toggle ready - not input authority for {PlayerName}");
                return;
            }

            bool newReadyState = !IsReady;
            Debug.Log($"🧪 [LOBBYPLAYER] 🔄 Toggling ready state to: {newReadyState} for player: {PlayerName}");

            RPC_SetReady(newReadyState);
        }

        public string GetDisplayName()
        {
            string name = PlayerName.ToString();
            if (string.IsNullOrEmpty(name))
                name = "Player";

            if (IsHost)
                name += " (Host)";

            return name;
        }

        public string GetColorHex()
        {
            return $"#{ColorUtility.ToHtmlStringRGB(PlayerColor)}";
        }

        public bool CanBeKicked()
        {
            return !IsHost && !IsLocalPlayer;
        }

        public string GetStatusText()
        {
            if (IsHost)
                return IsReady ? "Ready (Host)" : "Not Ready (Host)";
            else
                return IsReady ? "Ready" : "Not Ready";
        }

        public float GetReadyProgress()
        {
            return IsReady ? 1.0f : 0.0f;
        }

        // ✅ DEBUG METHODS
        [ContextMenu("Debug: Player Info")]
        private void DebugPlayerInfo()
        {
            Debug.Log("=== LobbyPlayer Debug Info ===");
            Debug.Log($"Player Ref: {PlayerRef}");
            Debug.Log($"Name: {PlayerName}");
            Debug.Log($"Display Name: {GetDisplayName()}");
            Debug.Log($"Is Ready: {IsReady}");
            Debug.Log($"Is Host: {IsHost}");
            Debug.Log($"Is Local: {IsLocalPlayer}");
            Debug.Log($"Color: {GetColorHex()}");
            Debug.Log($"Status: {GetStatusText()}");
            Debug.Log($"Can Be Kicked: {CanBeKicked()}");
            Debug.Log($"Has Input Authority: {HasInputAuthority}");
            Debug.Log($"Object State: {Object?.IsValid}");
            Debug.Log("================================");
        }

        [ContextMenu("Debug: Toggle Ready (Test)")]
        private void DebugToggleReady()
        {
            if (HasInputAuthority)
            {
                Debug.Log("🧪 [DEBUG] Testing toggle ready...");
                ToggleReady();
            }
            else
            {
                Debug.LogWarning("🧪 [DEBUG] Cannot test toggle ready - not input authority");
            }
        }

        [ContextMenu("Debug: Validate Registration")]
        private void DebugValidateRegistration()
        {
            Debug.Log("=== Registration Validation ===");

            if (LobbyState.Instance == null)
            {
                Debug.LogError("❌ LobbyState.Instance is NULL");
                return;
            }

            var foundPlayer = LobbyState.Instance.GetPlayer(PlayerRef);
            if (foundPlayer == null)
            {
                Debug.LogError($"❌ Player {PlayerRef} not found in LobbyState");
            }
            else if (foundPlayer == this)
            {
                Debug.Log($"✅ Player {PlayerRef} correctly registered in LobbyState");
            }
            else
            {
                Debug.LogError($"❌ Player {PlayerRef} registration mismatch - different instance");
            }

            Debug.Log("================================");
        }

        #region OnGUI Debug (Editor Only)

        private void OnGUI()
        {
            if (!Application.isEditor) return;
            if (!HasInputAuthority) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Label($"Local Player: {PlayerName} - Ready: {IsReady}");
            GUILayout.Label($"Host: {IsHost} | Authority: {HasInputAuthority}");
            GUILayout.Label($"LobbyState: {LobbyState.Instance != null}");

            if (GUILayout.Button("Toggle Ready"))
            {
                ToggleReady();
            }

            GUILayout.EndArea();
        }

        #endregion
    }
}