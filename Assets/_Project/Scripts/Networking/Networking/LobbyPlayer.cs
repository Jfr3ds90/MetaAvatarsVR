using UnityEngine;
using Fusion;
using HackMonkeys.UI.Panels;
using TMPro;

namespace HackMonkeys.Core
{
    /// <summary>
    /// LobbyPlayer ACTUALIZADO - Usa LobbyState en lugar de LobbyManager
    /// Representa a un jugador en el lobby con datos sincronizados en red
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        [Networked] public NetworkString<_32> PlayerName { get; set; }

        [Networked] public NetworkBool IsReady { get; set; }

        [Networked] public NetworkBool IsHost { get; set; }

        [Networked] public Color PlayerColor { get; set; }

        // Referencias locales (no sincronizadas)
        private PlayerPrefsManager _prefsManager;
        private ChangeDetector _changeDetector;

        // Cache de valores anteriores para detectar cambios
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

            // Inicializar el ChangeDetector
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

            // Si es nuestro jugador local
            if (HasInputAuthority)
            {
                Debug.Log("🧪 [LOBBYPLAYER] Configuring local player data...");

                _prefsManager = PlayerPrefsManager.Instance;

                if (_prefsManager == null)
                {
                    Debug.LogError("🧪 [LOBBYPLAYER] ❌ PlayerPrefsManager.Instance is NULL!");
                    // Usar valores por defecto
                    RPC_SetPlayerData(
                        $"Player {Object.InputAuthority.PlayerId}",
                        Color.HSVToRGB(Random.Range(0f, 1f), 0.8f, 1f),
                        Runner.IsServer
                    );
                }
                else
                {
                    // Configurar datos iniciales
                    RPC_SetPlayerData(
                        _prefsManager.GetPlayerName(),
                        _prefsManager.GetPlayerColor(),
                        Runner.IsServer // Es host si es el servidor
                    );

                    Debug.Log($"🧪 [LOBBYPLAYER] Player name: {_prefsManager.GetPlayerName()}");
                    Debug.Log($"🧪 [LOBBYPLAYER] Is host: {Runner.IsServer}");
                    Debug.Log($"🧪 [LOBBYPLAYER] Player color: {_prefsManager.GetPlayerColor()}");
                }
            }

            // CORRECCIÓN: Intentar registrar inmediatamente Y con fallback
            TryRegisterInLobbyState();

            // Guardar valores iniciales
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

                // Buscar en la escena también
                if (attempts % 5 == 0) // Cada 5 intentos
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

                // NUEVO: Forzar un refresh en LobbyRoom si está activo
                var lobbyRoom = FindObjectOfType<LobbyRoom>();
                if (lobbyRoom != null && lobbyRoom.gameObject.activeInHierarchy)
                {
                    Debug.Log("🧪 [LOBBYPLAYER] Triggering LobbyRoom refresh...");
                    // Usar reflection para llamar RefreshPlayersList si es privado
                    var method = lobbyRoom.GetType().GetMethod("RefreshPlayersList",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(lobbyRoom, null);
                }
            }
            else
            {
                Debug.LogError($"🧪 [LOBBYPLAYER] ❌ CRITICAL: LobbyState never initialized after {timeout}s!");
                Debug.LogError("🧪 [LOBBYPLAYER] ❌ Player will not appear in lobby UI!");

                // Último intento desesperado
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

            // ✅ CAMBIO: Usar LobbyState en lugar de LobbyManager
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
    
            // Validar nombre
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

        public override void FixedUpdateNetwork()
        {
            // Detectar cambios usando ChangeDetector
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
                }
            }
        }

        private void OnNameChangedCallback()
        {
            Debug.Log($"🧪 [LOBBYPLAYER] 🔄 Name changed to: {PlayerName.ToString()}");

            // ✅ CAMBIO: Usar LobbyState en lugar de LobbyManager
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

            // ✅ CAMBIO: Usar LobbyState en lugar de LobbyManager
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

        // Métodos públicos para el UI
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

        // ✅ NUEVOS MÉTODOS ÚTILES PARA UI
        public bool CanBeKicked()
        {
            // No se puede kickear al host ni al jugador local
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

            // Solo mostrar para el jugador local
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