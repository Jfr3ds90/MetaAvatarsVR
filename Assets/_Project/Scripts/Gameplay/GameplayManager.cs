using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using HackMonkeys.Core;
using UnityEngine.XR;
using DG.Tweening;

namespace HackMonkeys.Gameplay
{
    /// <summary>
    /// GameplayManager - Gestiona el gameplay multijugador VR
    /// IMPORTANTE: Cambiado de SimulationBehaviour a NetworkBehaviour para soportar RPCs
    /// </summary>
    public class GameplayManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        #region Configuration
        [Header("Player Spawning")]
        [SerializeField] private NetworkPrefabRef networkPlayerPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float spawnRadius = 2f;
        
        [Header("VR Configuration")]
        [SerializeField] private GameObject localVRRigPrefab; // OVRCameraRig local
        [SerializeField] private float playerHeight = 1.8f;
        [SerializeField] private LayerMask teleportLayers;
        
        [Header("Game Configuration")]
        [SerializeField] private float matchDuration = 300f; // 5 minutos
        [SerializeField] private int minimumPlayers = 2;
        
        [Header("UI References")]
        [SerializeField] private GameObject inGameUICanvas;
        [SerializeField] private TMPro.TextMeshProUGUI matchTimerText;
        [SerializeField] private TMPro.TextMeshProUGUI playerCountText;
        #endregion

        #region Private Fields
        private static GameplayManager _instance;
        public static GameplayManager Instance => _instance;
        
        private GameCore _gameCore;
        private Dictionary<PlayerRef, NetworkPlayer> _players = new Dictionary<PlayerRef, NetworkPlayer>();
        private NetworkPlayer _localPlayer;
        [SerializeField] public GameObject _localVRRig;
        
        // Match State - Ahora con [Networked] para sincronización
        [Networked] public float MatchStartTime { get; set; }
        [Networked] public float MatchTimeRemaining { get; set; }
        [Networked] public NetworkBool IsMatchActiveBool { get; set; }
        [Networked] public MatchState CurrentMatchState { get; set; }
        
        public enum MatchState : byte
        {
            WaitingForPlayers,
            Starting,
            InProgress,
            Ending,
            Ended
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance != null)
            {
                Debug.LogError("Destroy");
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            _gameCore = GameCore.Instance;
            
            Debug.Log("[GameplayManager] 🎮 Initialized");
        }

        private void Start()
        {
            // Verificar que estamos en el estado correcto
            if (_gameCore != null && _gameCore.CurrentState != GameCore.GameState.InMatch)
            {
                Debug.LogError("[GameplayManager] ❌ Not in InMatch state!");
                return;
            }
            
            StartCoroutine(InitializeGameplay());
        }

        private void OnDestroy()
        {
                Debug.LogError("Destroy");
            if (_instance == this)
                _instance = null;
        }
        #endregion

        #region Network Lifecycle
        public override void Spawned()
        {
            Debug.Log($"[GameplayManager] NetworkBehaviour spawned - IsServer: {Runner.IsServer}");
            
            // Si somos el Host, inicializar el estado del juego
            if (Runner.IsServer)
            {
                InitializeMatchState();
            }
        }
        #endregion

        #region Initialization
        private IEnumerator InitializeGameplay()
        {
            Debug.Log("[GameplayManager] 🚀 Initializing gameplay systems...");
            
            // Esperar a que tengamos Runner
            yield return new WaitUntil(() => Runner != null);
            
            // Configurar VR para gameplay
            SetupVRForGameplay();
            
            // Mostrar UI de juego
            if (inGameUICanvas != null)
                inGameUICanvas.SetActive(true);
            
            Debug.Log("[GameplayManager] ✅ Gameplay initialized");
        }

        private void SetupVRForGameplay()
        {
            // Configurar tracking origin para el juego
            if (OVRManager.instance != null)
            {
                OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            }
            
            // Configurar capas de física para VR
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("LocalPlayer"), LayerMask.NameToLayer("RemotePlayer"));
        }

        private void InitializeMatchState()
        {
            MatchStartTime = Time.time;
            MatchTimeRemaining = matchDuration;
            CurrentMatchState = MatchState.WaitingForPlayers;
            IsMatchActiveBool = false;
            
            Debug.Log("[GameplayManager] 📊 Match state initialized");
        }
        #endregion

        #region Player Management - IPlayerJoined & IPlayerLeft
        public void PlayerJoined(PlayerRef player)
        {
            Debug.Log($"[GameplayManager] 👤 Player {player} joined the match");
            
            if (Runner != null && Runner.IsServer)
            {
                SpawnPlayer(player);
            }
        }

        public void PlayerLeft(PlayerRef player)
        {
            Debug.Log($"[GameplayManager] 👋 Player {player} left the match");
            
            if (_players.TryGetValue(player, out NetworkPlayer networkPlayer))
            {
                _players.Remove(player);
                
                // Si era el jugador local, limpiar su rig
                if (player == Runner.LocalPlayer && _localVRRig != null)
                {
                    Destroy(_localVRRig);
                }
            }
            
            UpdatePlayerCount();
        }

        private void SpawnPlayer(PlayerRef player)
        {
            Debug.Log($"[GameplayManager] 🎯 Spawning player {player}");
            
            // Obtener punto de spawn
            Vector3 spawnPosition = GetSpawnPosition();
            Quaternion spawnRotation = GetSpawnRotation();
            
            // Spawn del NetworkPlayer
            NetworkObject networkPlayerObject = Runner.Spawn(
                networkPlayerPrefab, 
                spawnPosition, 
                spawnRotation, 
                player
            );
            
            if (networkPlayerObject != null)
            {
                NetworkPlayer networkPlayer = networkPlayerObject.GetComponent<NetworkPlayer>();
                _players[player] = networkPlayer;
                
                Debug.Log($"[GameplayManager] ✅ Player {player} spawned successfully");
            }
            else
            {
                Debug.LogError($"[GameplayManager] ❌ Failed to spawn player {player}");
            }
        }

        private Vector3 GetSpawnPosition()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                // Usar punto de spawn aleatorio
                int randomIndex = Random.Range(0, spawnPoints.Length);
                Transform spawnPoint = spawnPoints[randomIndex];
                
                // Añadir variación aleatoria
                Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                return spawnPoint.position + new Vector3(randomOffset.x, 0, randomOffset.y);
            }
            
            // Spawn por defecto
            return new Vector3(0, 0, 0);
        }

        private Quaternion GetSpawnRotation()
        {
            // Rotación aleatoria en Y
            return Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }
        #endregion

        #region Local VR Player Setup
        /// <summary>
        /// Llamado por NetworkPlayer cuando el jugador local está listo
        /// </summary>
        public void OnLocalPlayerSpawned(NetworkPlayer localPlayer)
        {
            Debug.Log("[GameplayManager] 🥽 Setting up local VR player");
            
            _localPlayer = localPlayer;
            
            // Crear el OVRCameraRig local
            SetupLocalVRRig(localPlayer);
            
            // Configurar input
            SetupVRInput();
            
            // Notificar que el jugador está listo
            if (Runner.IsServer)
            {
                CheckMatchStart();
            }
        }

        private void SetupLocalVRRig(NetworkPlayer localPlayer)
        {
            if (localVRRigPrefab == null)
            {
                Debug.LogError("[GameplayManager] ❌ Local VR Rig prefab not assigned!");
                return;
            }

            if (_localVRRig == null)
            {
                // Instanciar OVRCameraRig
                _localVRRig = Instantiate(localVRRigPrefab);
                _localVRRig.name = "LocalVRRig";
            }
            
            // Posicionar en la posición del NetworkPlayer
            _localVRRig.transform.position = localPlayer.transform.position;
            _localVRRig.transform.rotation = localPlayer.transform.rotation;
            
            // Configurar el NetworkPlayer para seguir al VR Rig
            localPlayer.SetVRRig(_localVRRig);
            
            // Configurar capas para evitar auto-colisiones
            SetLayerRecursively(_localVRRig, LayerMask.NameToLayer("LocalPlayer"));
            
            Debug.Log("[GameplayManager] ✅ Local VR Rig configured");
        }

        private void SetupVRInput()
        {
            // Aquí configurarías el sistema de input VR
            // Por ejemplo, locomotion, teleport, grab, etc.
            
            var locomotion = _localVRRig.GetComponentInChildren<OVRPlayerController>();
            if (locomotion != null)
            {
                locomotion.EnableLinearMovement = true;
                locomotion.EnableRotation = true;
            }
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        #endregion

        #region Match Flow
        public override void FixedUpdateNetwork()
        {
            if (!Runner.IsServer) return;
            
            // Actualizar timer en el servidor
            if (IsMatchActive())
            {
                UpdateMatchTimer();
            }
        }

        private void Update()
        {
            // Actualizar UI localmente
            UpdateUI();
        }

        private void UpdateMatchTimer()
        {
            MatchTimeRemaining -= Runner.DeltaTime;
            
            if (MatchTimeRemaining <= 0)
            {
                EndMatch();
            }
        }

        private void CheckMatchStart()
        {
            if (CurrentMatchState != MatchState.WaitingForPlayers) return;
            
            int playerCount = _players.Count;
            
            if (playerCount >= minimumPlayers)
            {
                StartCoroutine(StartMatchCountdown());
            }
        }

        private IEnumerator StartMatchCountdown()
        {
            CurrentMatchState = MatchState.Starting;
            
            Debug.Log("[GameplayManager] 🎯 Starting match countdown...");
            
            // Countdown de 3 segundos
            for (int i = 3; i > 0; i--)
            {
                // Notificar countdown via RPC
                RPC_ShowCountdown(i);
                yield return new WaitForSeconds(1f);
            }
            
            StartMatch();
        }

        private void StartMatch()
        {
            CurrentMatchState = MatchState.InProgress;
            IsMatchActiveBool = true;
            MatchStartTime = Time.time;
            
            Debug.Log("[GameplayManager] 🎮 Match started!");
            
            // Notificar a todos los jugadores
            RPC_OnMatchStarted();
            
            // Habilitar controles de jugadores
            EnablePlayerControls(true);
        }

        private void EndMatch()
        {
            if (CurrentMatchState == MatchState.Ended) return;
            
            CurrentMatchState = MatchState.Ending;
            IsMatchActiveBool = false;
            
            Debug.Log("[GameplayManager] 🏁 Match ended!");
            
            // Deshabilitar controles
            EnablePlayerControls(false);
            
            // Calcular resultados
            MatchResult results = CalculateMatchResults();
            
            // Notificar a GameCore
            StartCoroutine(TransitionToResults(results));
        }

        private IEnumerator TransitionToResults(MatchResult results)
        {
            // Notificar fin via RPC
            RPC_OnMatchEnded();
            
            yield return new WaitForSeconds(2f);
            
            // Notificar a GameCore
            if (_gameCore != null)
            {
                _gameCore.EndMatch(results);
            }
        }

        private MatchResult CalculateMatchResults()
        {
            var results = new MatchResult
            {
                matchDuration = Time.time - MatchStartTime,
                playerScores = new Dictionary<PlayerRef, int>()
            };
            
            // Calcular scores (ejemplo básico)
            foreach (var kvp in _players)
            {
                results.playerScores[kvp.Key] = Random.Range(0, 100); // Placeholder
            }
            
            return results;
        }
        #endregion

        #region RPCs - Ahora funcionan correctamente con NetworkBehaviour
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnMatchStarted()
        {
            Debug.Log("[GameplayManager] 📢 RPC: Match started notification");
            ShowVRNotification("Match Started!", 2f);
            
            // Efectos locales
            if (_localPlayer != null)
            {
                // Haptic feedback
                OVRInput.SetControllerVibration(1, 0.5f, OVRInput.Controller.Touch);
                DOVirtual.DelayedCall(0.2f, () => 
                    OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.Touch));
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnMatchEnded()
        {
            Debug.Log("[GameplayManager] 📢 RPC: Match ended notification");
            ShowVRNotification("Match Complete!", 3f);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowCountdown(int seconds)
        {
            ShowVRNotification($"Match starting in {seconds}...", 0.9f);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowNotification(string message, float duration)
        {
            ShowVRNotification(message, duration);
        }
        #endregion

        #region UI & Feedback
        private void UpdateUI()
        {
            // Timer
            if (matchTimerText != null && IsMatchActive())
            {
                int minutes = Mathf.FloorToInt(MatchTimeRemaining / 60);
                int seconds = Mathf.FloorToInt(MatchTimeRemaining % 60);
                matchTimerText.text = $"{minutes:00}:{seconds:00}";
            }
            
            // Player count
            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {_players.Count}";
            }
        }

        private void UpdatePlayerCount()
        {
            int count = _players.Count;
            
            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {count}";
            }
            
            // Si estamos esperando jugadores y bajamos del mínimo
            if (IsMatchActive() && count < minimumPlayers)
            {
                // Pausar o terminar el juego
                ShowVRNotification("Not enough players!", 3f);
            }
        }

        private void ShowVRNotification(string message, float duration = 2f)
        {
            // TODO: Implementar sistema de notificaciones 3D en VR
            Debug.Log($"[VR Notification] {message}");
            
            // Por ahora, usar el canvas de UI
            if (inGameUICanvas != null)
            {
                // Implementar texto flotante 3D
            }
        }

        private void EnablePlayerControls(bool enabled)
        {
            foreach (var player in _players.Values)
            {
                player.EnableControls(enabled);
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Obtener el jugador local
        /// </summary>
        public NetworkPlayer GetLocalPlayer()
        {
            return _localPlayer;
        }

        /// <summary>
        /// Obtener todos los jugadores
        /// </summary>
        public Dictionary<PlayerRef, NetworkPlayer> GetAllPlayers()
        {
            return new Dictionary<PlayerRef, NetworkPlayer>(_players);
        }

        /// <summary>
        /// Verificar si el juego está activo
        /// </summary>
        public bool IsMatchActive()
        {
            return IsMatchActiveBool;
        }

        /// <summary>
        /// Obtener tiempo restante
        /// </summary>
        public float GetTimeRemaining()
        {
            return MatchTimeRemaining;
        }
        #endregion

        #region Debug & Testing
        [Header("Debug")]
        [SerializeField] private bool enableDebugUI = true;
        [SerializeField] private bool autoStartMatch = false;
        
        /*private void OnGUI()
        {
            if (!enableDebugUI || !Application.isEditor) return;
            
            GUILayout.BeginArea(new Rect(10, 100, 300, 200));
            GUILayout.Label($"Match State: {CurrentMatchState}");
            GUILayout.Label($"Players: {_players.Count}");
            GUILayout.Label($"Time: {MatchTimeRemaining:F1}s");
            GUILayout.Label($"Is Server: {Runner?.IsServer}");
            
            if (Runner != null && Runner.IsServer)
            {
                if (GUILayout.Button("Force Start Match"))
                {
                    StartMatch();
                }
                
                if (GUILayout.Button("Force End Match"))
                {
                    EndMatch();
                }
            }
            
            GUILayout.EndArea();
        }*/

        [ContextMenu("Debug: List Players")]
        private void DebugListPlayers()
        {
            Debug.Log("=== GameplayManager Players ===");
            foreach (var kvp in _players)
            {
                Debug.Log($"Player {kvp.Key}: {kvp.Value.name}");
            }
            Debug.Log($"Total: {_players.Count}");
            Debug.Log("==============================");
        }
        #endregion
    }
}