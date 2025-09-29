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
    /// NO maneja la creación de VR Rigs, solo el gameplay y spawn de jugadores
    /// </summary>
    public class GameplayManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        #region Configuration

        [Header("Player Spawning")] [SerializeField]
        private NetworkPrefabRef networkPlayerPrefab;

        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float spawnRadius = 2f;

        [Header("Game Configuration")] [SerializeField]
        private float matchDuration = 300f;

        [SerializeField] private int minimumPlayers = 2;

        [Header("UI References")] [SerializeField]
        private GameObject inGameUICanvas;

        [SerializeField] private TMPro.TextMeshProUGUI matchTimerText;
        [SerializeField] private TMPro.TextMeshProUGUI playerCountText;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = true;

        #endregion

        #region Private Fields

        private static GameplayManager _instance;
        public static GameplayManager Instance => _instance;

        private GameCore _gameCore;
        private Dictionary<PlayerRef, NetworkPlayer> _players = new Dictionary<PlayerRef, NetworkPlayer>();
        private NetworkPlayer _localPlayer;

        // Match State - Sincronizado en red
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
                Debug.LogWarning("[GameplayManager] ⚠️ Instance already exists, destroying duplicate");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            _gameCore = GameCore.Instance;

            Debug.Log("[GameplayManager] 🎮 Instance initialized in Awake");
        }

        private void Start()
        {
            // Retrasar la inicialización para asegurar que Spawned() se complete primero
            StartCoroutine(DelayedStart());
        }

        private IEnumerator DelayedStart()
        {
            // Esperar un frame para asegurar que Spawned() se ejecute primero
            yield return null;

            // Verificar que estamos en el estado correcto
            if (_gameCore != null && _gameCore.CurrentState != GameCore.GameState.InMatch &&
                _gameCore.CurrentState != GameCore.GameState.LoadingMatch)
            {
                Debug.LogWarning(
                    $"[GameplayManager] ⚠️ GameCore in state {_gameCore.CurrentState}, expected InMatch or LoadingMatch");
            }

            yield return StartCoroutine(InitializeGameplay());
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        #endregion

        #region Network Lifecycle

        public override void Spawned()
        {
            Debug.Log($"[GameplayManager] NetworkBehaviour Spawned called");
            Debug.Log($"  - Runner: {Runner}");
            Debug.Log($"  - LocalPlayer: {Runner?.LocalPlayer}");
            Debug.Log($"  - GameMode: {Runner?.GameMode}");
            Debug.Log($"  - IsRunning: {Runner?.IsRunning}");

            if (Runner == null)
            {
                Debug.LogError("[GameplayManager] ❌ Runner is null in Spawned!");
                return;
            }

            // En Shared Mode, inicializar el estado si no está inicializado
            if (Runner.GameMode == GameMode.Shared)
            {
                // Verificar si el estado ya fue inicializado
                if (CurrentMatchState == MatchState.WaitingForPlayers && MatchTimeRemaining == 0)
                {
                    Debug.Log("[GameplayManager] Initializing match state in Shared Mode");
                    InitializeMatchState();
                }
                else
                {
                    Debug.Log("[GameplayManager] Match state already initialized");
                }
            }
            else if (HasStateAuthority)
            {
                // En otros modos, solo el host inicializa
                Debug.Log("[GameplayManager] Initializing match state as Host");
                InitializeMatchState();
            }
        }

        #endregion

        #region Initialization

        private IEnumerator InitializeGameplay()
        {
            Debug.Log("[GameplayManager] 🚀 Initializing gameplay systems...");

            // Esperar a que tengamos Runner
            int waitFrames = 0;
            while (Runner == null && waitFrames < 300) // Max 5 segundos a 60fps
            {
                yield return null;
                waitFrames++;
            }

            if (Runner == null)
            {
                Debug.LogError("[GameplayManager] ❌ Runner never became available!");
                yield break;
            }

            Debug.Log($"[GameplayManager] Runner available after {waitFrames} frames");

            // Configurar VR para gameplay
            SetupVRForGameplay();

            // Mostrar UI de juego
            if (inGameUICanvas != null)
                inGameUICanvas.SetActive(true);

            Debug.Log("[GameplayManager] ✅ Gameplay systems initialized successfully");
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
            Debug.Log($"[GameplayManager] 📊 GameMode: {Runner?.GameMode}");
            Debug.Log($"[GameplayManager] 📊 LocalPlayer: {Runner?.LocalPlayer}");
            Debug.Log($"[GameplayManager] 📊 Is Local Player: {player == Runner?.LocalPlayer}");

            if (Runner == null)
            {
                Debug.LogError("[GameplayManager] ❌ Runner is null in PlayerJoined!");
                return;
            }

            if (Runner.GameMode == GameMode.Shared)
            {
                if (player == Runner.LocalPlayer)
                {
                    Debug.Log($"[GameplayManager] ✅ SHARED: Es nuestro jugador local, procediendo a spawn...");
                    StartCoroutine(DelayedSpawnPlayer(player));
                    Debug.Log(
                        $"[GameplayManager] 🎮 Shared Mode: Local player {player} will spawn their own NetworkPlayer");
                }
                else
                {
                    Debug.Log($"[GameplayManager] ℹ️ SHARED: Player {player} no es local, no spawneamos (ellos spawnean su propio NetworkPlayer)");
                }
            }
            else if (HasStateAuthority)
            {
                StartCoroutine(DelayedSpawnPlayer(player));
                Debug.Log($"[GameplayManager] 🎮 Host/Server Mode: Authority will spawn player {player}");
            }
            else
            {
                Debug.Log($"[GameplayManager] ℹ️ No authority to spawn player {player}");
            }
        }

        private IEnumerator DelayedSpawnPlayer(PlayerRef player)
        {
            // Pequeña espera para asegurar que todo esté inicializado
            yield return new WaitForSeconds(0.1f);

            if (Runner != null && Runner.IsRunning)
            {
                SpawnPlayer(player);
            }
            else
            {
                Debug.LogError($"[GameplayManager] ❌ Cannot spawn player {player} - Runner not ready");
            }
        }

        public void PlayerLeft(PlayerRef player)
        {
            Debug.Log($"[GameplayManager] 👋 Player {player} left the match");

            if (_players.TryGetValue(player, out NetworkPlayer networkPlayer))
            {
                _players.Remove(player);
            }

            UpdatePlayerCount();
        }

        private void SpawnPlayer(PlayerRef player)
        {
            Debug.Log($"[GameplayManager] 🎯 Spawning player {player}");
            Debug.Log($"[GameplayManager] 📊 SpawnPlayer Details:");
            Debug.Log($"  - Runner.GameMode: {Runner.GameMode}");
            Debug.Log($"  - Runner.LocalPlayer: {Runner.LocalPlayer}");
            Debug.Log($"  - Player to spawn: {player}");
            Debug.Log($"  - Is local player: {player == Runner.LocalPlayer}");

            // Verificar que el player es válido
            if (!player.IsRealPlayer)
            {
                Debug.LogError($"[GameplayManager] ❌ Invalid player reference: {player}");
                return;
            }

            // Verificar si el prefab está asignado
            if (!networkPlayerPrefab.IsValid)
            {
                Debug.LogError($"[GameplayManager] ❌ NetworkPlayerPrefab is not valid/assigned!");
                return;
            }

            // Obtener punto de spawn
            Vector3 spawnPosition = GetSpawnPosition();
            Quaternion spawnRotation = GetSpawnRotation();
            
            Debug.Log($"[GameplayManager] 📍 Spawn Position: {spawnPosition}, Rotation: {spawnRotation.eulerAngles}");

            // Spawn con callback para verificar autoridad
            NetworkObject networkPlayerObject = Runner.Spawn(
                networkPlayerPrefab,
                spawnPosition,
                spawnRotation,
                inputAuthority: player,
                onBeforeSpawned: (runner, obj) =>
                {
                    Debug.Log($"[GameplayManager] Pre-spawn callback for player {player}");
                    Debug.Log($"  - Object being spawned: {obj}");
                    Debug.Log($"  - Will have InputAuthority: {player}");
                }
            );

            if (networkPlayerObject != null)
            {
                NetworkPlayer networkPlayer = networkPlayerObject.GetComponent<NetworkPlayer>();
                _players[player] = networkPlayer;

                if (enableDebugLogs)
                {
                    Debug.Log($"[GameplayManager] ✅ Player {player} spawned successfully");
                    Debug.Log($"  - Object.InputAuthority: {networkPlayerObject.InputAuthority}");
                    Debug.Log($"  - HasInputAuthority: {networkPlayerObject.HasInputAuthority}");
                    Debug.Log($"  - StateAuthority: {networkPlayerObject.StateAuthority}");

                    // Verificación adicional
                    if (networkPlayerObject.InputAuthority != player)
                    {
                        Debug.LogError($"[GameplayManager] ⚠️ InputAuthority mismatch!");
                        Debug.LogError($"  - Expected: {player}");
                        Debug.LogError($"  - Got: {networkPlayerObject.InputAuthority}");
                    }
                }
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
                int randomIndex = Random.Range(0, spawnPoints.Length);
                Transform spawnPoint = spawnPoints[randomIndex];

                Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                return spawnPoint.position + new Vector3(randomOffset.x, 0, randomOffset.y);
            }

            return new Vector3(0, 0, 0);
        }

        private Quaternion GetSpawnRotation()
        {
            return Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }

        #endregion

        #region Local Player Registration

        /// <summary>
        /// Registra el jugador local (llamado por NetworkPlayer cuando se auto-configura)
        /// </summary>
        public void RegisterLocalPlayer(NetworkPlayer localPlayer)
        {
            Debug.Log("[GameplayManager] 📝 Registrando jugador local");

            _localPlayer = localPlayer;

            // En Shared Mode, cualquier jugador puede verificar el inicio del match
            CheckMatchStart();

            Debug.Log("[GameplayManager] ✅ Jugador local registrado");
        }

        #endregion

        #region Match Flow

        public override void FixedUpdateNetwork()
        {
            // En Shared Mode, todos calculan el tiempo localmente
            // Solo actualizamos si somos el jugador con menor ID para evitar conflictos
            if (Runner.LocalPlayer.PlayerId != 0) return;

            // Actualizar timer
            if (IsMatchActive())
            {
                UpdateMatchTimer();
            }
        }

        private void Update()
        {
            // Actualizar UI localmente
            UpdateUI();

            // Debug para verificar jugadores
            if (enableDebugLogs && Time.frameCount % 120 == 0) // Cada 2 segundos
            {
                DebugPlayersStatus();
            }
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

            RPC_OnMatchStarted();
            EnablePlayerControls(true);
        }

        private void EndMatch()
        {
            if (CurrentMatchState == MatchState.Ended) return;

            CurrentMatchState = MatchState.Ending;
            IsMatchActiveBool = false;

            Debug.Log("[GameplayManager] 🏁 Match ended!");

            EnablePlayerControls(false);

            MatchResult results = CalculateMatchResults();
            StartCoroutine(TransitionToResults(results));
        }

        private IEnumerator TransitionToResults(MatchResult results)
        {
            RPC_OnMatchEnded();

            yield return new WaitForSeconds(2f);

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

            foreach (var kvp in _players)
            {
                results.playerScores[kvp.Key] = Random.Range(0, 100);
            }

            return results;
        }

        #endregion

        #region RPCs

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnMatchStarted()
        {
            Debug.Log("[GameplayManager] 📢 RPC: Match started notification");
            ShowVRNotification("Match Started!", 2f);

            if (_localPlayer != null)
            {
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
            if (matchTimerText != null && IsMatchActive())
            {
                int minutes = Mathf.FloorToInt(MatchTimeRemaining / 60);
                int seconds = Mathf.FloorToInt(MatchTimeRemaining % 60);
                matchTimerText.text = $"{minutes:00}:{seconds:00}";
            }

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

            if (IsMatchActive() && count < minimumPlayers)
            {
                ShowVRNotification("Not enough players!", 3f);
            }
        }

        private void ShowVRNotification(string message, float duration = 2f)
        {
            Debug.Log($"[VR Notification] {message}");

            // TODO: Implementar sistema de notificaciones 3D en VR
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

        public NetworkPlayer GetLocalPlayer()
        {
            return _localPlayer;
        }

        public Dictionary<PlayerRef, NetworkPlayer> GetAllPlayers()
        {
            return new Dictionary<PlayerRef, NetworkPlayer>(_players);
        }

        public bool IsMatchActive()
        {
            return IsMatchActiveBool;
        }

        public float GetTimeRemaining()
        {
            return MatchTimeRemaining;
        }

        #endregion

        #region Debug

        private void DebugPlayersStatus()
        {
            Debug.Log($"[GameplayManager] === PLAYERS STATUS === (Shared Mode)");
            foreach (var kvp in _players)
            {
                var player = kvp.Value;
                Debug.Log($"  Player {kvp.Key}:");
                Debug.Log($"    - GameObject: {player.name}");
                Debug.Log($"    - HasInputAuthority: {player.HasInputAuthority}");
                Debug.Log($"    - IsVRConnected: {player.IsVRRigConnected()}");
                Debug.Log($"    - Position: {player.transform.position}");
            }

            Debug.Log("======================");
        }

        [ContextMenu("Debug: Force List Players")]
        private void DebugListPlayers()
        {
            Debug.Log("=== GameplayManager Players ===");
            foreach (var kvp in _players)
            {
                Debug.Log($"Player {kvp.Key}: {kvp.Value.name}");
                Debug.Log($"  - InputAuthority: {kvp.Value.Object.InputAuthority}");
                Debug.Log($"  - HasInputAuthority: {kvp.Value.HasInputAuthority}");
            }

            Debug.Log($"Total: {_players.Count}");
            Debug.Log("==============================");
        }

        #endregion
    }
}