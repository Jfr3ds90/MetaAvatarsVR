using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion.Photon.Realtime;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace HackMonkeys.Core
{
    public class NetworkBootstrapper : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Configuration")] [SerializeField]
        private NetworkRunner runnerPrefab;

        [SerializeField] private NetworkSceneManagerDefault sceneManagerPrefab;
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private string gameSceneName = "GameScene";

        [Header("Scene Management")] [SerializeField]
        private List<SceneInfo> availableScenes = new List<SceneInfo>();

        private string _selectedSceneName = "";

        [Header("Player Spawning")] [SerializeField]
        private LobbyPlayer lobbyPlayerPrefab;

        [Header("Room Settings")] [SerializeField]
        private int defaultMaxPlayers = 4;

        [SerializeField] private string defaultRegion = "us";

        [Header("Events - SIMPLIFICADOS")] public UnityEvent OnConnectedToServerEvent;
        public UnityEvent<string> OnConnectionFailed;
        public UnityEvent<List<SessionInfo>> OnSessionListUpdatedEvent;

        private NetworkRunner _runner;
        private NetworkSceneManagerDefault _sceneManager;
        private bool _isInRoom = false;
        private GameCore _gameCore;

        private TaskCompletionSource<List<SessionInfo>> _sessionListTcs;
        private List<SessionInfo> _receivedSessions;

        public static NetworkBootstrapper Instance { get; private set; }
        public NetworkRunner Runner => _runner;
        public bool IsConnected => _runner != null && _runner.IsRunning;
        public bool IsInRoom => _isInRoom;
        public bool IsHost => _runner != null && _runner.IsServer;

        public string CurrentRoomName { get; private set; }
        public int CurrentMaxPlayers { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _gameCore = GameCore.Instance;
        }

        /// <summary>
        /// Crear sala + configurar callbacks automáticamente
        /// </summary>
        public async Task<bool> CreateRoom(string roomName, int maxPlayers = 0, string sceneName = null)
        {
            if (_runner != null)
            {
                Debug.LogWarning("[NetworkBootstrapper] Runner already exists. Shutting down...");
                await ShutdownRunner();
            }

            try
            {
                Debug.Log($"[NetworkBootstrapper] Creating room: {roomName}");

                if (maxPlayers <= 0) maxPlayers = defaultMaxPlayers;

                if (!string.IsNullOrEmpty(sceneName) && IsValidScene(sceneName))
                {
                    _selectedSceneName = sceneName;
                    Debug.Log($"[NetworkBootstrapper] Selected scene: {sceneName}");
                }
                else
                {
                    _selectedSceneName = gameSceneName; 
                    Debug.Log($"[NetworkBootstrapper] Using default scene: {gameSceneName}");
                }

                CurrentRoomName = roomName;
                CurrentMaxPlayers = maxPlayers;

                _runner = Instantiate(runnerPrefab);
                _runner.name = "NetworkRunner_Host";

                _runner.AddCallbacks(this);

                _sceneManager = Instantiate(sceneManagerPrefab);

                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Host,
                    SessionName = roomName,
                    PlayerCount = maxPlayers,
                    SceneManager = _sceneManager,
                    CustomLobbyName = "HackMonkeys_Lobby",
                    IsVisible = true,
                    IsOpen = true,
                    SessionProperties = CreateSessionProperties(sceneName)
                };

                var result = await _runner.StartGame(startGameArgs);

                if (result.Ok)
                {
                    Debug.Log("[NetworkBootstrapper] ✅ Room created successfully!");
                    
                    PlayerDataManager.Instance.SetSessionData(Runner.LocalPlayer, true, roomName);
                    
                    if (PlayerDataManager.Instance != null && Runner.LocalPlayer.IsRealPlayer)
                    {
                        PlayerDataManager.Instance.UpdateLocalPlayerRef(Runner.LocalPlayer);
                        Debug.Log($"[NetworkBootstrapper] ✅ Host LocalPlayerRef updated: {Runner.LocalPlayer}");
                    }
                    
                    _isInRoom = true;
                    OnConnectedToServerEvent?.Invoke();
                    return true;
                }
                else
                {
                    Debug.LogError($"[NetworkBootstrapper] ❌ Failed to create room: {result.ShutdownReason}");
                    OnConnectionFailed?.Invoke(result.ShutdownReason.ToString());
                    await CleanupRunner();
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] ❌ Exception creating room: {e.Message}");
                OnConnectionFailed?.Invoke(e.Message);
                await CleanupRunner();
                return false;
            }
        }
        
        private Dictionary<string, SessionProperty> CreateSessionProperties(string sceneName)
        {
            var properties = new Dictionary<string, SessionProperty>();
    
            if (!string.IsNullOrEmpty(sceneName))
            {
                properties["scene"] = sceneName;
            }
    
            properties["version"] = Application.version;
            properties["gamemode"] = "default";
    
            return properties;
        }
        
       
        
        public bool ChangeSelectedScene(string newSceneName)
        {
            if (!IsHost)
            {
                Debug.LogWarning("[NetworkBootstrapper] Only host can change scene");
                return false;
            }
    
            if (!IsValidScene(newSceneName))
            {
                Debug.LogError($"[NetworkBootstrapper] Invalid scene: {newSceneName}");
                return false;
            }
    
            _selectedSceneName = newSceneName;
            Debug.Log($"[NetworkBootstrapper] Scene changed to: {newSceneName}");
    
            // TODO: Enviar un RPC para notificar a los clientes del cambio
    
            return true;
        }

        /// <summary>
        /// Une a una sala existente + configurar callbacks
        /// </summary>
        public async Task<bool> JoinRoom(SessionInfo session)
        {
            if (_runner != null)
            {
                Debug.LogWarning("[NetworkBootstrapper] Runner already exists. Shutting down...");
                await ShutdownRunner();
            }

            try
            {
                Debug.Log($"[NetworkBootstrapper] Joining room: {session.Name}");

                CurrentRoomName = session.Name;
                CurrentMaxPlayers = session.MaxPlayers;

                _runner = Instantiate(runnerPrefab);
                _runner.name = "NetworkRunner_Client";

                _runner.AddCallbacks(this);

                _sceneManager = Instantiate(sceneManagerPrefab);

                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Client,
                    SessionName = session.Name,
                    SceneManager = _sceneManager,
                    CustomLobbyName = "HackMonkeys_Lobby"
                };

                var result = await _runner.StartGame(startGameArgs);

                if (result.Ok)
                {
                    Debug.Log("[NetworkBootstrapper] ✅ Joined room successfully!");
                    Debug.Log("[NetworkBootstrapper] ✅ Callbacks configured - ready for player spawning");
                    _isInRoom = true;
                    OnConnectedToServerEvent?.Invoke();
                    
                    PlayerDataManager.Instance.SetSessionData(PlayerRef.None, false, session.Name);
                    
                    return true;
                }
                else
                {
                    Debug.LogError($"[NetworkBootstrapper] ❌ Failed to join room: {result.ShutdownReason}");
                    OnConnectionFailed?.Invoke(result.ShutdownReason.ToString());
                    await CleanupRunner();
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] ❌ Exception joining room: {e.Message}");
                OnConnectionFailed?.Invoke(e.Message);
                await CleanupRunner();
                return false;
            }
        }

        // ========================================
        // AQUÍ SE MUEVE LA LÓGICA DE PlayerSpawner
        // ========================================
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkBootstrapper] 🎯 Player {player} joined the room");

            if (runner.IsServer)
            {
                if (lobbyPlayerPrefab == null)
                {
                    Debug.LogError("[NetworkBootstrapper] ❌ LobbyPlayer prefab not assigned!");
                    return;
                }

                Vector3 spawnPosition = Vector3.zero;
                NetworkObject networkPlayerObject = runner.Spawn(
                    lobbyPlayerPrefab.gameObject,
                    spawnPosition,
                    Quaternion.identity,
                    player
                );

                if (networkPlayerObject != null)
                {
                    Debug.Log($"[NetworkBootstrapper] ✅ Spawned LobbyPlayer for player {player}");
                }
                else
                {
                    Debug.LogError($"[NetworkBootstrapper] ❌ Failed to spawn LobbyPlayer for player {player}");
                }
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkBootstrapper] 👋 Player {player} left the room");
        }

        /// <summary>
        /// Obtiene la lista de sesiones disponibles
        /// </summary>
        public async Task<List<SessionInfo>> GetAvailableSessions()
        {
            try
            {
                Debug.Log("[NetworkBootstrapper] 🔍 Searching for available sessions...");

                var tempRunner = Instantiate(runnerPrefab);
                tempRunner.name = "NetworkRunner_SessionFinder";

                var sessionListCallback = new SessionListCallback();
                tempRunner.AddCallbacks(sessionListCallback);

                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Client,
                    SessionName = null, 
                    CustomLobbyName = "HackMonkeys_Lobby",
                };

                await tempRunner.JoinSessionLobby(SessionLobby.Custom, "HackMonkeys_Lobby");

                await Task.Delay(2000);

                var sessions = sessionListCallback.GetSessions();

                await tempRunner.Shutdown();
                Destroy(tempRunner.gameObject);

                Debug.Log($"[NetworkBootstrapper] ✅ Found {sessions.Count} available sessions");
                return sessions;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] ❌ Failed to get sessions: {e.Message}");
                return new List<SessionInfo>();
            }
        }

        /// <summary>
        /// Abandona la sala actual
        /// </summary>
        public async Task LeaveRoom()
        {
            if (_runner == null || !_isInRoom) return;

            Debug.Log("[NetworkBootstrapper] 👋 Leaving room...");

            await ShutdownRunner();
            _isInRoom = false;
            CurrentRoomName = "";
            CurrentMaxPlayers = 0;

            Debug.Log("[NetworkBootstrapper] ✅ Left room successfully");
        }

        /// <summary>
        /// Iniciar partida con escena seleccionada
        /// </summary>
        public async Task<bool> StartGame(string overrideSceneName = null)
        {
            if (!IsHost || !_isInRoom)
            {
                Debug.LogError("[NetworkBootstrapper] ❌ Only host can start the game!");
                return false;
            }
    
            try
            {
                string sceneToLoad = !string.IsNullOrEmpty(overrideSceneName) ? overrideSceneName : SelectedSceneName;
        
                Debug.Log($"[NetworkBootstrapper] 🚀 Starting game with scene: {sceneToLoad}");
        
                var sceneIndex = GetSceneIndex(sceneToLoad);
                if (sceneIndex.IsValid == false)
                {
                    Debug.LogError($"[NetworkBootstrapper] ❌ Scene '{sceneToLoad}' not found!");
                    return false;
                }
        
                await _runner.LoadScene(sceneIndex);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] ❌ Failed to start game: {e.Message}");
                return false;
            }
        }

        #region SceneManagement

        public string SelectedSceneName
        {
            get => string.IsNullOrEmpty(_selectedSceneName) ? gameSceneName : _selectedSceneName;
            set => _selectedSceneName = value;
        }

        public List<SceneInfo> GetAvailableScenes()
        {
            return availableScenes;
        }

        public bool IsValidScene(string sceneName)
        {
            return availableScenes.Any(s => s.sceneName == sceneName);
        }

        public SceneInfo GetSceneInfo(string sceneName)
        {
            return availableScenes.FirstOrDefault(s => s.sceneName == sceneName);
        }

        private SceneRef GetSceneIndex(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                if (name == sceneName)
                {
                    return SceneRef.FromIndex(i);
                }
            }

            Debug.LogError($"[NetworkBootstrapper] ❌ Scene '{sceneName}' not found in build settings!");
            return SceneRef.FromIndex(0);
        }

        #endregion

        // ========================================
        // MÉTODOS DE LIMPIEZA
        // ========================================

        private async Task ShutdownRunner()
        {
            if (_runner != null)
            {
                Debug.Log("[NetworkBootstrapper] 🔄 Shutting down runner...");
                await _runner.Shutdown();
                await CleanupRunner();
            }
        }

        private async Task CleanupRunner()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
                Destroy(_runner.gameObject);
                _runner = null;
                Debug.Log("[NetworkBootstrapper] ✅ Runner cleaned up");
            }

            if (_sceneManager != null)
            {
                Destroy(_sceneManager.gameObject);
                _sceneManager = null;
                Debug.Log("[NetworkBootstrapper] ✅ SceneManager cleaned up");
            }

            await Task.Delay(100);
        }

        // ========================================
        // CALLBACKS DE FUSION
        // ========================================

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[NetworkBootstrapper] 🌐 Connected to Photon Cloud");
    
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.UpdateLocalPlayerRef(runner.LocalPlayer);
                Debug.Log($"[NetworkBootstrapper] ✅ LocalPlayerRef updated: {runner.LocalPlayer}");
            }
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[NetworkBootstrapper] 📡 Disconnected from server: {reason}");
            _isInRoom = false;
            
            if (_gameCore != null)
            {
                _gameCore.OnNetworkDisconnected();
            }
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[NetworkBootstrapper] ❌ Connect failed: {reason}");
            OnConnectionFailed?.Invoke(reason.ToString());
            
            
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner.name == "NetworkRunner_SessionFinder")
            {
                Debug.Log($"[NetworkBootstrapper] 📋 Session list updated: {sessionList.Count} sessions");
                _receivedSessions = new List<SessionInfo>(sessionList);
                _sessionListTcs?.TrySetResult(_receivedSessions);
            }

            OnSessionListUpdatedEvent?.Invoke(sessionList);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[NetworkBootstrapper] 🔄 Runner shutdown: {shutdownReason}");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token)
        {
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
            ArraySegment<byte> data)
        {
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            Debug.Log("[NetworkBootstrapper] 🎬 Scene load done");
    
            // Notificar a GameCore que la escena está lista
            if (_gameCore != null)
            {
                _gameCore.OnGameSceneLoaded();
            }
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            Debug.Log("[NetworkBootstrapper] 🎬 Scene load starting...");
    
            PlayerDataManager.Instance.UpdateSelectedMapFromLobbyPlayer();
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                Debug.Log("[NetworkBootstrapper] 🧹 Destroying - final cleanup");
                _runner.RemoveCallbacks(this);
                _runner.Shutdown();
            }
        }

        // ========================================
        // ✅ VALIDACIÓN PARA DEBUGGING
        // ========================================

        [ContextMenu("Debug: Validate Configuration")]
        private void ValidateConfiguration()
        {
            Debug.Log("=== NetworkBootstrapper Validation ===");

            if (runnerPrefab == null)
                Debug.LogError("❌ Runner Prefab not assigned!");
            else
                Debug.Log("✅ Runner Prefab assigned");

            if (sceneManagerPrefab == null)
                Debug.LogError("❌ Scene Manager Prefab not assigned!");
            else
                Debug.Log("✅ Scene Manager Prefab assigned");

            if (lobbyPlayerPrefab == null)
                Debug.LogError("❌ LobbyPlayer Prefab not assigned!");
            else
                Debug.Log("✅ LobbyPlayer Prefab assigned");

            Debug.Log($"Default Max Players: {defaultMaxPlayers}");
            Debug.Log($"Current Room: {CurrentRoomName ?? "None"}");
            Debug.Log($"Is In Room: {IsInRoom}");
            Debug.Log($"Is Host: {IsHost}");
            Debug.Log("================================");
        }
        
        private class SessionListCallback : INetworkRunnerCallbacks
        {
            private List<SessionInfo> _sessions = new List<SessionInfo>();

            public List<SessionInfo> GetSessions() => new List<SessionInfo>(_sessions);

            public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
            {
                _sessions = new List<SessionInfo>(sessionList);
                Debug.Log($"[SessionListCallback] Received {sessionList.Count} sessions");
            }

            // Implementación vacía de otros callbacks
            public void OnConnectedToServer(NetworkRunner runner)
            {
            }

            public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
            {
            }

            public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
            {
            }

            public void OnInput(NetworkRunner runner, NetworkInput input)
            {
            }

            public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
            {
            }

            public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
            {
            }

            public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
            {
            }

            public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
            {
            }

            public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request,
                byte[] token)
            {
            }

            public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
            {
            }

            public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
                ArraySegment<byte> data)
            {
            }

            public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
            {
            }

            public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
            {
            }

            public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
            {
            }

            public void OnSceneLoadDone(NetworkRunner runner)
            {
            }

            public void OnSceneLoadStart(NetworkRunner runner)
            {
               
            }

            public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
            {
            }

            public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
            {
            }
        }
    }
    
    [System.Serializable]
    public class SceneInfo
    {
        public string sceneName;
        public string displayName;
        public Sprite previewImage;
        public string description;
        public int minPlayers = 2;
        public int maxPlayers = 4;
    }
}