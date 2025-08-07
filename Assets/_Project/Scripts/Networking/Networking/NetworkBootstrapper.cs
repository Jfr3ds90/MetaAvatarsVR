using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion.Photon.Realtime;
using HackMonkeys.Gameplay;
using HackMonkeys.UI.Panels;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

namespace HackMonkeys.Core
{
    /// <summary>
    /// NetworkBootstrapper - Sistema central de networking con Photon Fusion
    /// Versión refactorizada con optimizaciones de rendimiento y gestión de memoria
    /// </summary>
    public class NetworkBootstrapper : MonoBehaviour, INetworkRunnerCallbacks
    {
        #region Configuration
        
        [Header("Runner Configuration")]
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private NetworkSceneManagerDefault sceneManagerPrefab;
        
        [Header("Scene Management")]
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private List<SceneInfo> availableScenes = new List<SceneInfo>();
        
        [Header("Player Spawning")]
        [SerializeField] private LobbyPlayer lobbyPlayerPrefab;
        
        [Header("Room Settings")]
        [SerializeField] private int defaultMaxPlayers = 4;
        [SerializeField] private string defaultRegion = "us";
        
        [Header("Session Discovery Settings")]
        [SerializeField] private float sessionCacheDuration = 2f;
        [SerializeField] private float sessionDiscoveryTimeout = 5f;
        [SerializeField] private int maxSessionRetries = 3;
        
        [Header("Performance Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool autoCleanupSessionFinder = true;
        [SerializeField] private float sessionFinderIdleTimeout = 60f;
        
        #endregion
        
        #region Events
        
        [Header("Events")]
        public UnityEvent OnConnectedToServerEvent;
        public UnityEvent<string> OnConnectionFailed;
        public UnityEvent<List<SessionInfo>> OnSessionListUpdatedEvent;
        public UnityEvent OnRoomCreated;
        public UnityEvent OnRoomJoined;
        public UnityEvent OnRoomLeft;
        public UnityEvent<PlayerRef> OnPlayerSpawned;
        public UnityEvent<PlayerRef> OnPlayerDespawned;
        
        #endregion
        
        #region Private Fields
        
        // Core networking
        private NetworkRunner _runner;
        private NetworkSceneManagerDefault _sceneManager;
        private bool _isInRoom = false;
        private GameCore _gameCore;
        
        // Player tracking
        private Dictionary<PlayerRef, NetworkObject> _playerObjects = new Dictionary<PlayerRef, NetworkObject>();
        private readonly object _playerObjectsLock = new object();
        
        // Session discovery optimization
        private NetworkRunner _persistentSessionFinderRunner;
        private DateTime _lastSessionListTime;
        private List<SessionInfo> _cachedSessions;
        private readonly object _sessionCacheLock = new object();
        private bool _isSessionFinderActive = false;
        private Coroutine _sessionFinderCleanupCoroutine;
        private int _consecutiveSessionFailures = 0;
        
        // Room state
        private string _selectedSceneName = "";
        private string _currentRoomName = "";
        private int _currentMaxPlayers = 0;
        private SessionInfo _currentSessionInfo;
        
        // Singleton
        private static NetworkBootstrapper _instance;
        
        #endregion
        
        #region Properties
        
        public static NetworkBootstrapper Instance => _instance;
        public NetworkRunner Runner => _runner;
        public bool IsConnected => _runner != null && _runner.IsRunning;
        public bool IsInRoom => _isInRoom;
        public bool IsHost => _runner != null && _runner.IsServer;
        public string CurrentRoomName => _currentRoomName;
        public int CurrentMaxPlayers => _currentMaxPlayers;
        public string SelectedSceneName
        {
            get => string.IsNullOrEmpty(_selectedSceneName) ? gameSceneName : _selectedSceneName;
            set => _selectedSceneName = value;
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            LogDebug("NetworkBootstrapper initialized");
        }
        
        private void Start()
        {
            _gameCore = GameCore.Instance;
            ValidateConfiguration();
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                LogDebug("NetworkBootstrapper destroying - final cleanup");
                
                // Cleanup session finder
                CleanupSessionFinderImmediate();
                
                // Cleanup main runner
                if (_runner != null)
                {
                    CleanupAllNetworkObjects();
                    _runner.RemoveCallbacks(this);
                    _runner.Shutdown();
                }
                
                // Clear references
                lock (_playerObjectsLock)
                {
                    _playerObjects.Clear();
                }
                
                _instance = null;
            }
        }
        
        #endregion
        
        #region Room Management
        
        /// <summary>
        /// Creates a new room and becomes the host
        /// </summary>
        public async Task<bool> CreateRoom(string roomName, int maxPlayers = 0, string sceneName = null)
        {
            // VALIDACIÓN 1: Asegurar que roomName no está vacío
            if (string.IsNullOrWhiteSpace(roomName))
            {
                Debug.LogError("Room name cannot be empty!");
                return false;
            }
    
            // VALIDACIÓN 2: Limpiar caracteres problemáticos
            roomName = SanitizeRoomName(roomName);
    
            Debug.Log($"Creating room with sanitized name: '{roomName}'");
    
            _currentRoomName = roomName;
            _currentMaxPlayers = maxPlayers <= 0 ? defaultMaxPlayers : maxPlayers;
    
            // Crear runner
            _runner = Instantiate(runnerPrefab);
            _runner.name = "NetworkRunner_Host";
            _runner.AddCallbacks(this);
    
            // IMPORTANTE: Configurar StartGameArgs correctamente
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = roomName,  // CRÍTICO: Este es el nombre que verán los clientes
                PlayerCount = _currentMaxPlayers,
                SceneManager = _sceneManager,
                CustomLobbyName = "HackMonkeys_Lobby",
                IsVisible = true,
                IsOpen = true,
                SessionProperties = CreateEnhancedSessionProperties(roomName, _selectedSceneName)
            };
    
            // LOG para debug
            Debug.Log($"StartGameArgs configured:");
            Debug.Log($"  - SessionName: '{startGameArgs.SessionName}'");
            Debug.Log($"  - CustomLobbyName: '{startGameArgs.CustomLobbyName}'");
            Debug.Log($"  - IsVisible: {startGameArgs.IsVisible}");
    
            var result = await _runner.StartGame(startGameArgs);
    
            if (result.Ok)
            {
                Debug.Log($"✅ Room created successfully with name: '{roomName}'");
        
                // Verificar que el runner tiene el nombre correcto
                VerifySessionName();
            }
    
            return result.Ok;
        }
        
        private string SanitizeRoomName(string name)
        {
            // Remover caracteres que pueden causar problemas
            name = name.Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\s-.]", "");
    
            // Limitar longitud
            if (name.Length > 32)
                name = name.Substring(0, 32);
    
            return name;
        }
        
        private Dictionary<string, SessionProperty> CreateEnhancedSessionProperties(string roomName, string sceneName)
        {
            var properties = new Dictionary<string, SessionProperty>();
    
            // AGREGAR: Backup del nombre como property
            properties["displayName"] = roomName;  // Backup en caso de que Name falle
            properties["hostName"] = PlayerDataManager.Instance?.GetPlayerName() ?? "Host";
    
            if (!string.IsNullOrEmpty(sceneName))
            {
                properties["scene"] = sceneName;
            }
    
            properties["version"] = Application.version;
            properties["timestamp"] = System.DateTime.Now.Ticks.ToString();
    
            return properties;
        }
        
        private void VerifySessionName()
        {
            // Verificación de debug
            if (_runner != null && _runner.SessionInfo.IsValid)
            {
                Debug.Log($"[VERIFY] Session Name in Runner: '{_runner.SessionInfo.Name}'");
                Debug.Log($"[VERIFY] Is Session Valid: {_runner.SessionInfo.IsValid}");
            }
        }
        
        /// <summary>
        /// Joins an existing room
        /// </summary>
        public async Task<bool> JoinRoom(SessionInfo session)
        {
            if (_runner != null)
            {
                LogWarning("Runner already exists. Shutting down...");
                await ShutdownRunner();
            }
            
            try
            {
                LogDebug($"Joining room: {session.Name}");
                
                _currentRoomName = session.Name;
                _currentMaxPlayers = session.MaxPlayers;
                _currentSessionInfo = session;
                
                // Create runner
                _runner = Instantiate(runnerPrefab);
                _runner.name = "NetworkRunner_Client";
                _runner.AddCallbacks(this);
                
                // Create scene manager
                _sceneManager = Instantiate(sceneManagerPrefab);
                _sceneManager.name = "NetworkSceneManager_Client";
                DontDestroyOnLoad(_sceneManager.gameObject);
                
                LogDebug("CLIENT SceneManager created and set to DontDestroyOnLoad");
                
                // Configure start arguments
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Client,
                    SessionName = session.Name,
                    SceneManager = _sceneManager,
                    CustomLobbyName = "HackMonkeys_Lobby"
                };
                
                // Join the game
                var result = await _runner.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    LogDebug("✅ Joined room successfully!");
                    _isInRoom = true;
                    
                    OnConnectedToServerEvent?.Invoke();
                    OnRoomJoined?.Invoke();
                    
                    PlayerDataManager.Instance?.SetSessionData(PlayerRef.None, false, session.Name);
                    
                    return true;
                }
                else
                {
                    LogError($"Failed to join room: {result.ShutdownReason}");
                    OnConnectionFailed?.Invoke(result.ShutdownReason.ToString());
                    await CleanupRunner();
                    return false;
                }
            }
            catch (Exception e)
            {
                LogError($"Exception joining room: {e.Message}");
                OnConnectionFailed?.Invoke(e.Message);
                await CleanupRunner();
                return false;
            }
        }
        
        /// <summary>
        /// Leaves the current room
        /// </summary>
        public async Task LeaveRoom()
        {
            if (_runner == null || !_isInRoom) return;
            
            LogDebug("Leaving room...");
            
            try
            {
                // Cleanup network objects first
                if (_runner.IsRunning)
                {
                    if (_runner.IsServer)
                    {
                        CleanupAllNetworkObjects();
                    }
                    else
                    {
                        CleanupLocalPlayerObjects();
                    }
                    
                    // Give time for despawn messages
                    await Task.Delay(100);
                }
                
                await ShutdownRunner();
                
                _isInRoom = false;
                _currentRoomName = "";
                _currentMaxPlayers = 0;
                _currentSessionInfo = null;
                
                // Clear player objects
                lock (_playerObjectsLock)
                {
                    _playerObjects.Clear();
                }
                
                OnRoomLeft?.Invoke();
                
                LogDebug("✅ Left room successfully");
            }
            catch (Exception e)
            {
                LogError($"Error leaving room: {e.Message}");
            }
        }
        
        /// <summary>
        /// Starts the game (Host only)
        /// </summary>
        public async Task<bool> StartGame(string overrideSceneName = null)
        {
            LogDebug("=== START GAME CALLED ===");
            LogDebug($"IsHost: {IsHost}, IsInRoom: {IsInRoom}");
            LogDebug($"Runner exists: {_runner != null}");
            LogDebug($"SceneManager exists: {_sceneManager != null}");
            
            if (!IsHost || !_isInRoom)
            {
                LogError("Only host can start the game!");
                return false;
            }
            
            try
            {
                string sceneToLoad = !string.IsNullOrEmpty(overrideSceneName) ? overrideSceneName : SelectedSceneName;
                
                LogDebug($"🚀 Starting game with scene: {sceneToLoad}");
                
                if (_runner == null)
                {
                    LogError("Runner is null!");
                    return false;
                }
                
                if (_sceneManager == null)
                {
                    LogError("NetworkSceneManagerDefault is null!");
                    return false;
                }
                
                var sceneIndex = GetSceneIndex(sceneToLoad);
                if (sceneIndex.IsValid == false)
                {
                    LogError($"Scene '{sceneToLoad}' not found!");
                    return false;
                }
                
                LogDebug($"Loading scene index: {sceneIndex}");
                
                await _runner.LoadScene(sceneIndex);
                
                LogDebug("✅ LoadScene completed");
                
                return true;
            }
            catch (Exception e)
            {
                LogError($"Failed to start game: {e.Message}");
                LogError($"Stack trace: {e.StackTrace}");
                return false;
            }
        }
        
        #endregion
        
        #region Optimized Session Discovery
        
        /// <summary>
        /// Gets available sessions with caching and optimization
        /// </summary>
        public async Task<List<SessionInfo>> GetAvailableSessions()
        {
            // Check cache first
            lock (_sessionCacheLock)
            {
                if (_cachedSessions != null && 
                    (DateTime.Now - _lastSessionListTime).TotalSeconds < sessionCacheDuration)
                {
                    LogDebug($"Returning cached sessions ({_cachedSessions.Count} rooms)");
                    return new List<SessionInfo>(_cachedSessions);
                }
            }
            
            // Prevent multiple simultaneous discoveries
            if (_isSessionFinderActive)
            {
                LogDebug("Session finder already active, waiting...");
                
                int waitAttempts = 0;
                while (_isSessionFinderActive && waitAttempts < 20)
                {
                    await Task.Delay(100);
                    waitAttempts++;
                }
                
                lock (_sessionCacheLock)
                {
                    if (_cachedSessions != null)
                    {
                        return new List<SessionInfo>(_cachedSessions);
                    }
                }
            }
            
            _isSessionFinderActive = true;
            
            try
            {
                LogDebug("🔍 Starting optimized session discovery...");
                
                // Create or reuse persistent runner
                if (_persistentSessionFinderRunner == null || !_persistentSessionFinderRunner.IsRunning)
                {
                    await CreatePersistentSessionFinder();
                }
                
                // Reset idle cleanup timer
                ResetSessionFinderCleanupTimer();
                
                // Use persistent runner to get sessions
                var sessionListCallback = new OptimizedSessionListCallback();
                
                _persistentSessionFinderRunner.AddCallbacks(sessionListCallback);
                
                // Wait for session list
                await Task.Delay(1500);
                
                var sessions = sessionListCallback.GetSessions();
                
                _persistentSessionFinderRunner.RemoveCallbacks(sessionListCallback);
                
                // Update cache
                lock (_sessionCacheLock)
                {
                    _cachedSessions = sessions;
                    _lastSessionListTime = DateTime.Now;
                }
                
                _consecutiveSessionFailures = 0;
                
                LogDebug($"✅ Found {sessions.Count} available sessions");
                
                OnSessionListUpdatedEvent?.Invoke(sessions);
                
                return sessions;
            }
            catch (Exception e)
            {
                _consecutiveSessionFailures++;
                LogError($"Failed to get sessions: {e.Message}");
                
                lock (_sessionCacheLock)
                {
                    return _cachedSessions ?? new List<SessionInfo>();
                }
            }
            finally
            {
                _isSessionFinderActive = false;
            }
        }
        
        /// <summary>
        /// Creates the persistent session finder runner
        /// </summary>
        private async Task CreatePersistentSessionFinder()
        {
            LogDebug("Creating persistent session finder runner...");
            
            // Cleanup existing runner if any
            if (_persistentSessionFinderRunner != null)
            {
                if (_persistentSessionFinderRunner.IsRunning)
                {
                    await _persistentSessionFinderRunner.Shutdown();
                }
                Destroy(_persistentSessionFinderRunner.gameObject);
                _persistentSessionFinderRunner = null;
            }
            
            // Create new persistent runner
            _persistentSessionFinderRunner = Instantiate(runnerPrefab);
            _persistentSessionFinderRunner.name = "NetworkRunner_SessionFinder_Persistent";
            DontDestroyOnLoad(_persistentSessionFinderRunner.gameObject);
            
            // Join session lobby
            await _persistentSessionFinderRunner.JoinSessionLobby(SessionLobby.Custom, "HackMonkeys_Lobby");
            
            LogDebug("✅ Persistent session finder created");
        }
        
        /// <summary>
        /// Resets the idle cleanup timer for session finder
        /// </summary>
        private void ResetSessionFinderCleanupTimer()
        {
            if (!autoCleanupSessionFinder) return;
            
            if (_sessionFinderCleanupCoroutine != null)
            {
                StopCoroutine(_sessionFinderCleanupCoroutine);
            }
            
            _sessionFinderCleanupCoroutine = StartCoroutine(SessionFinderIdleCleanup());
        }
        
        /// <summary>
        /// Coroutine to cleanup idle session finder
        /// </summary>
        private IEnumerator SessionFinderIdleCleanup()
        {
            yield return new WaitForSeconds(sessionFinderIdleTimeout);
            
            if (!_isSessionFinderActive && _persistentSessionFinderRunner != null)
            {
                LogDebug("Session finder idle timeout - cleaning up");
                CleanupSessionFinderImmediate();
            }
        }
        
        /// <summary>
        /// Immediately cleans up the session finder
        /// </summary>
        private void CleanupSessionFinderImmediate()
        {
            if (_sessionFinderCleanupCoroutine != null)
            {
                StopCoroutine(_sessionFinderCleanupCoroutine);
                _sessionFinderCleanupCoroutine = null;
            }
            
            if (_persistentSessionFinderRunner != null)
            {
                LogDebug("Cleaning up session finder...");
                
                if (_persistentSessionFinderRunner.IsRunning)
                {
                    _persistentSessionFinderRunner.Shutdown();
                }
                
                Destroy(_persistentSessionFinderRunner.gameObject);
                _persistentSessionFinderRunner = null;
            }
            
            lock (_sessionCacheLock)
            {
                _cachedSessions = null;
            }
        }
        
        /// <summary>
        /// Invalidates the session cache
        /// </summary>
        public void InvalidateSessionCache()
        {
            lock (_sessionCacheLock)
            {
                _cachedSessions = null;
                _lastSessionListTime = DateTime.MinValue;
            }
        }
        
        #endregion
        
        #region Player Management
        
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            LogDebug($"🎯 Player {player} joined the room");
            
            if (runner.IsServer)
            {
                SpawnLobbyPlayer(player);
            }
        }
        
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            LogDebug($"👋 Player {player} left the room");
            
            if (runner.IsServer)
            {
                CleanupPlayerObjects(runner, player);
            }
        }
        
        private void SpawnLobbyPlayer(PlayerRef player)
        {
            if (lobbyPlayerPrefab == null)
            {
                LogError("LobbyPlayer prefab not assigned!");
                return;
            }
            
            Vector3 spawnPosition = Vector3.zero;
            NetworkObject networkPlayerObject = _runner.Spawn(
                lobbyPlayerPrefab.gameObject,
                spawnPosition,
                Quaternion.identity,
                player
            );
            
            if (networkPlayerObject != null)
            {
                LogDebug($"✅ Spawned LobbyPlayer for player {player}");
                
                lock (_playerObjectsLock)
                {
                    _playerObjects[player] = networkPlayerObject;
                }
                
                OnPlayerSpawned?.Invoke(player);
            }
            else
            {
                LogError($"Failed to spawn LobbyPlayer for player {player}");
            }
        }
        
        private void CleanupPlayerObjects(NetworkRunner runner, PlayerRef player)
        {
            LogDebug($"🧹 Cleaning up objects for player {player}");
            
            // Use tracked reference
            lock (_playerObjectsLock)
            {
                if (_playerObjects.TryGetValue(player, out NetworkObject playerObject))
                {
                    if (playerObject != null && playerObject.IsValid)
                    {
                        LogDebug($"Despawning tracked object for player {player}");
                        runner.Despawn(playerObject);
                    }
                    _playerObjects.Remove(player);
                }
            }
            
            // Fallback: search for orphaned LobbyPlayers
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            foreach (var lobbyPlayer in allLobbyPlayers)
            {
                if (lobbyPlayer.PlayerRef == player)
                {
                    var netObj = lobbyPlayer.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsValid)
                    {
                        LogDebug($"Despawning found LobbyPlayer for player {player}");
                        runner.Despawn(netObj);
                    }
                }
            }
            
            OnPlayerDespawned?.Invoke(player);
        }
        
        private void CleanupAllNetworkObjects()
        {
            LogDebug("🧹 Cleaning up all network objects");
            
            if (_runner == null || !_runner.IsRunning) return;
            
            // Despawn all LobbyPlayers
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            foreach (var lobbyPlayer in allLobbyPlayers)
            {
                var netObj = lobbyPlayer.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsValid)
                {
                    LogDebug($"Despawning LobbyPlayer: {lobbyPlayer.PlayerName}");
                    _runner.Despawn(netObj);
                }
            }
            
            lock (_playerObjectsLock)
            {
                _playerObjects.Clear();
            }
        }
        
        private void CleanupLocalPlayerObjects()
        {
            LogDebug("🧹 Cleaning up local player objects");
            
            if (_runner == null || !_runner.IsRunning) return;
            
            var localPlayer = _runner.LocalPlayer;
            if (localPlayer.IsRealPlayer)
            {
                CleanupPlayerObjects(_runner, localPlayer);
            }
        }
        
        #endregion
        
        #region Scene Management
        
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
        
        public string GetDefaultSceneName()
        {
            if (!string.IsNullOrEmpty(_selectedSceneName))
                return _selectedSceneName;
            
            if (availableScenes != null && availableScenes.Count > 0)
            {
                _selectedSceneName = availableScenes[0].sceneName;
                LogDebug($"Using first available scene: {_selectedSceneName}");
                return _selectedSceneName;
            }
            
            _selectedSceneName = gameSceneName;
            LogDebug($"Using default scene: {_selectedSceneName}");
            return _selectedSceneName;
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
            
            LogError($"Scene '{sceneName}' not found in build settings!");
            return SceneRef.FromIndex(0);
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
        
        #endregion
        
        #region INetworkRunnerCallbacks
        
        public void OnConnectedToServer(NetworkRunner runner)
        {
            LogDebug("🌐 Connected to Photon Cloud");
            
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.UpdateLocalPlayerRef(runner.LocalPlayer);
                LogDebug($"✅ LocalPlayerRef updated: {runner.LocalPlayer}");
            }
            
            LogDebug($"🌐 OnConnectedToServer - Callbacks registered: {runner.name}");
        }
        
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            LogDebug($"📡 Disconnected from server: {reason}");
            
            if (runner.IsRunning)
            {
                CleanupAllNetworkObjects();
            }
            
            _isInRoom = false;
            lock (_playerObjectsLock)
            {
                _playerObjects.Clear();
            }
            
            if (_gameCore != null)
            {
                _gameCore.OnNetworkDisconnected();
            }
        }
        
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            LogError($"Connect failed: {reason}");
            OnConnectionFailed?.Invoke(reason.ToString());
        }
        
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner.name.Contains("SessionFinder"))
            {
                LogDebug($"📋 Session list updated: {sessionList.Count} sessions");
                OnSessionListUpdatedEvent?.Invoke(sessionList);
            }
        }
        
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            LogDebug($"🔄 Runner shutdown: {shutdownReason}");
            
            lock (_playerObjectsLock)
            {
                _playerObjects.Clear();
            }
            
            // Cleanup orphaned LobbyPlayers
            var orphanedPlayers = FindObjectsOfType<LobbyPlayer>();
            foreach (var player in orphanedPlayers)
            {
                LogDebug($"Destroying orphaned LobbyPlayer: {player.name}");
                Destroy(player.gameObject);
            }
        }
        
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            LogDebug($"🎬 OnSceneLoadDone - {runner.GameMode}");
            LogDebug($"- New Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            LogDebug($"- Is Server: {runner.IsServer}");
            LogDebug($"- Is Client: {runner.IsClient}");
            
            if (_gameCore != null)
            {
                _gameCore.OnGameSceneLoaded();
            }
        }
        
        public void OnSceneLoadStart(NetworkRunner runner)
        {
            _gameCore?.TransitionToState(GameCore.GameState.LoadingMatch);
            LogDebug($"🎬 OnSceneLoadStart - {runner.GameMode}");
            
            if (!runner.IsServer && _gameCore != null)
            {
                LogDebug("📱 CLIENT: Scene change detected");
                _gameCore.OnClientSceneChangeStarted();
            }
            
            PlayerDataManager.Instance?.UpdateSelectedMapFromLobbyPlayer();
        }
        
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        
        #endregion
        
        #region Cleanup & Utilities
        
        private async Task ShutdownRunner()
        {
            if (_runner != null)
            {
                LogDebug("🔄 Shutting down runner...");
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
                LogDebug("✅ Runner cleaned up");
            }
            
            if (_sceneManager != null)
            {
                Destroy(_sceneManager.gameObject);
                _sceneManager = null;
                LogDebug("✅ SceneManager cleaned up");
            }
            
            await Task.Delay(100);
        }
        
        private void ValidateConfiguration()
        {
            if (runnerPrefab == null)
                LogError("Runner Prefab not assigned!");
            
            if (sceneManagerPrefab == null)
                LogError("Scene Manager Prefab not assigned!");
            
            if (lobbyPlayerPrefab == null)
                LogError("LobbyPlayer Prefab not assigned!");
        }
        
        #endregion
        
        #region Logging Utilities
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[NetworkBootstrapper] {message}");
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[NetworkBootstrapper] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[NetworkBootstrapper] {message}");
        }
        
        #endregion
        
        #region Debug Menu
        
        [ContextMenu("Debug: Validate Configuration")]
        private void DebugValidateConfiguration()
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
            Debug.Log($"Available Scenes: {availableScenes.Count}");
            Debug.Log($"Session Cache Duration: {sessionCacheDuration}s");
            Debug.Log($"Auto Cleanup Enabled: {autoCleanupSessionFinder}");
            Debug.Log("================================");
        }
        
        [ContextMenu("Debug: Print Session Cache")]
        private void DebugPrintSessionCache()
        {
            Debug.Log("=== Session Cache Status ===");
            lock (_sessionCacheLock)
            {
                if (_cachedSessions != null)
                {
                    Debug.Log($"Cached Sessions: {_cachedSessions.Count}");
                    Debug.Log($"Cache Age: {(DateTime.Now - _lastSessionListTime).TotalSeconds}s");
                    foreach (var session in _cachedSessions)
                    {
                        Debug.Log($"  - {session.Name}: {session.PlayerCount}/{session.MaxPlayers}");
                    }
                }
                else
                {
                    Debug.Log("No cached sessions");
                }
            }
            Debug.Log($"Session Finder Active: {_isSessionFinderActive}");
            Debug.Log($"Persistent Runner Exists: {_persistentSessionFinderRunner != null}");
            Debug.Log("==========================");
        }
        
        [ContextMenu("Debug: Force Clear Cache")]
        private void DebugForceClearCache()
        {
            InvalidateSessionCache();
            Debug.Log("Session cache cleared");
        }
        
        [ContextMenu("Debug: Force Cleanup Session Finder")]
        private void DebugForceCleanupSessionFinder()
        {
            CleanupSessionFinderImmediate();
            Debug.Log("Session finder cleaned up");
        }
        
        #endregion
        
        #region Helper Classes
        
        /// <summary>
        /// Optimized callback for session list updates
        /// </summary>
        private class OptimizedSessionListCallback : INetworkRunnerCallbacks
        {
            private List<SessionInfo> _sessions = new List<SessionInfo>();
            private readonly object _lock = new object();
            
            public List<SessionInfo> GetSessions()
            {
                lock (_lock)
                {
                    return new List<SessionInfo>(_sessions);
                }
            }
            
            public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
            {
                lock (_lock)
                {
                    _sessions = new List<SessionInfo>(sessionList);
                    Debug.Log($"[OptimizedSessionListCallback] Received {sessionList.Count} sessions");
                }
            }
            
            // Empty implementations for other callbacks
            public void OnConnectedToServer(NetworkRunner runner) { }
            public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
            public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
            public void OnInput(NetworkRunner runner, NetworkInput input) { }
            public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
            public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
            public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
            public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
            public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
            public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
            public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
            public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
            public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
            public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
            public void OnSceneLoadDone(NetworkRunner runner) { }
            public void OnSceneLoadStart(NetworkRunner runner) { }
            public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Scene information for available maps/levels
    /// </summary>
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