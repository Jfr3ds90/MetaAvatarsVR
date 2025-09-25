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
    /// NetworkBootstrapper - Sistema central de networking con Photon Fusion en SHARED MODE
    /// Arquitectura peer-to-peer con autoridad distribuida para VR multijugador
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
        
        // Player tracking - En Shared Mode todos los jugadores son iguales
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
        
        // Room state - No hay distinción Host/Client en Shared Mode
        private string _selectedSceneName = "";
        private string _currentRoomName = "";
        private int _currentMaxPlayers = 0;
        private SessionInfo _currentSessionInfo;
        private bool _isRoomCreator = false; // Quien creó la sala (para UI/UX, no autoridad)
        
        // Singleton
        private static NetworkBootstrapper _instance;
        
        #endregion
        
        #region Properties
        
        public static NetworkBootstrapper Instance => _instance;
        public NetworkRunner Runner => _runner;
        public bool IsConnected => _runner != null && _runner.IsRunning;
        public bool IsInRoom => _isInRoom;
        
        // En Shared Mode no hay Host real, pero mantenemos para compatibilidad UI
        public bool IsRoomCreator => _isRoomCreator;
        
        // DEPRECATED: Mantener para compatibilidad pero redirigir
        public bool IsHost => IsRoomCreator;
        
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
            
            LogDebug("NetworkBootstrapper initialized - SHARED MODE");
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
        
        #region Room Management - Shared Mode
        
        /// <summary>
        /// Creates a new room in SHARED MODE - No host, just the first player
        /// </summary>
        public async Task<bool> CreateRoom(string roomName, int maxPlayers = 0, string sceneName = null)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                Debug.LogError("Room name cannot be empty!");
                return false;
            }
    
            roomName = SanitizeRoomName(roomName);
    
            Debug.Log($"[SHARED MODE] Creating room: '{roomName}'");

            _isInRoom = true;
            _isRoomCreator = true; // Marcamos como creador para UI/UX
            _currentRoomName = roomName;
            _currentMaxPlayers = maxPlayers <= 0 ? defaultMaxPlayers : maxPlayers;
    
            _runner = Instantiate(runnerPrefab);
            _runner.name = "NetworkRunner_Shared";
            _runner.AddCallbacks(this);
            
            _sceneManager = Instantiate(sceneManagerPrefab);
            _sceneManager.name = "NetworkSceneManager_Shared";
            DontDestroyOnLoad(_sceneManager.gameObject);
    
            var startGameArgs = new StartGameArgs()
            {
                // CAMBIO CLAVE: Usar GameMode.Shared en lugar de GameMode.Host
                GameMode = GameMode.Shared,
                SessionName = roomName,
                PlayerCount = _currentMaxPlayers,
                SceneManager = _sceneManager,
                CustomLobbyName = "HackMonkeys_Lobby",
                IsVisible = true,
                IsOpen = true,
                SessionProperties = CreateEnhancedSessionProperties(roomName, _selectedSceneName)
            };
    
            Debug.Log($"[SHARED MODE] StartGameArgs configured:");
            Debug.Log($"  - GameMode: SHARED");
            Debug.Log($"  - SessionName: '{startGameArgs.SessionName}'");
            Debug.Log($"  - CustomLobbyName: '{startGameArgs.CustomLobbyName}'");
    
            var result = await _runner.StartGame(startGameArgs);
    
            if (result.Ok)
            {
                Debug.Log($"✅ [SHARED MODE] Room created: '{roomName}'");
                OnRoomCreated?.Invoke();
                
                // En Shared Mode, esperamos a estar conectados para spawnear
                await Task.Delay(100);
                
                // Spawn del jugador local inmediatamente
                SpawnLocalPlayer();
            }
            else
            {
                LogError($"Failed to create room: {result.ShutdownReason}");
                _isRoomCreator = false;
                OnConnectionFailed?.Invoke(result.ShutdownReason.ToString());
            }
    
            return result.Ok;
        }
        
        /// <summary>
        /// Joins an existing room in SHARED MODE
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
                LogDebug($"[SHARED MODE] Joining room: {session.Name}");
                
                _currentRoomName = session.Name;
                _currentMaxPlayers = session.MaxPlayers;
                _currentSessionInfo = session;
                _isRoomCreator = false; // No somos el creador
                
                _runner = Instantiate(runnerPrefab);
                _runner.name = "NetworkRunner_Shared";
                _runner.AddCallbacks(this);
                
                _sceneManager = Instantiate(sceneManagerPrefab);
                _sceneManager.name = "NetworkSceneManager_Shared";
                DontDestroyOnLoad(_sceneManager.gameObject);
                
                // CAMBIO CLAVE: Usar GameMode.Shared para unirse también
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Shared,
                    SessionName = session.Name,
                    SceneManager = _sceneManager,
                    CustomLobbyName = "HackMonkeys_Lobby"
                };
                
                var result = await _runner.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    LogDebug("✅ [SHARED MODE] Joined room successfully!");
                    _isInRoom = true;
                    
                    OnConnectedToServerEvent?.Invoke();
                    OnRoomJoined?.Invoke();
                    
                    PlayerDataManager.Instance?.SetSessionData(PlayerRef.None, false, session.Name);
                    
                    // En Shared Mode, spawn del jugador local
                    await Task.Delay(100);
                    SpawnLocalPlayer();
                    
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
        /// Spawns the local player in SHARED MODE
        /// </summary>
        private void SpawnLocalPlayer()
        {
            if (!_runner.IsRunning || lobbyPlayerPrefab == null)
            {
                LogError("Cannot spawn player - runner not ready or prefab missing");
                return;
            }
            
            // En Shared Mode, cada jugador spawna su propio objeto con autoridad local
            Vector3 spawnPosition = GetSpawnPosition(_runner.LocalPlayer);
            
            NetworkObject networkPlayerObject = _runner.Spawn(
                lobbyPlayerPrefab.gameObject,
                spawnPosition,
                Quaternion.identity,
                _runner.LocalPlayer // Autoridad local en Shared Mode
            );
            
            if (networkPlayerObject != null)
            {
                LogDebug($"✅ [SHARED MODE] Spawned local player with authority");
                
                lock (_playerObjectsLock)
                {
                    _playerObjects[_runner.LocalPlayer] = networkPlayerObject;
                }
                
                OnPlayerSpawned?.Invoke(_runner.LocalPlayer);
            }
            else
            {
                LogError("Failed to spawn local player");
            }
        }
        
        /// <summary>
        /// Gets spawn position for a player (distributed spawning)
        /// </summary>
        private Vector3 GetSpawnPosition(PlayerRef player)
        {
            // Distribuir spawn positions basado en PlayerRef
            float angle = player.PlayerId * (360f / _currentMaxPlayers);
            float radius = 2f;
            
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            
            return new Vector3(x, 0, z);
        }
        
        /// <summary>
        /// Starts the game scene transition (cualquier jugador puede iniciar en Shared Mode)
        /// </summary>
        public async Task<bool> StartGame(string overrideSceneName = null)
        {
            LogDebug("=== START GAME (SHARED MODE) ===");
            LogDebug($"IsInRoom: {IsInRoom}, Runner exists: {_runner != null}");
            
            if (!_isInRoom)
            {
                LogError("Must be in a room to start the game!");
                return false;
            }
            
            try
            {
                string sceneToLoad = !string.IsNullOrEmpty(overrideSceneName) ? overrideSceneName : SelectedSceneName;
                
                LogDebug($"🚀 [SHARED MODE] Loading scene: {sceneToLoad}");
                
                var sceneIndex = GetSceneIndex(sceneToLoad);
                if (sceneIndex.IsValid == false)
                {
                    LogError($"Scene '{sceneToLoad}' not found!");
                    return false;
                }
                
                // En Shared Mode, cualquier jugador puede iniciar la transición de escena
                // Photon Fusion sincronizará automáticamente la escena para todos
                await _runner.LoadScene(sceneIndex);
                
                LogDebug("✅ Scene load initiated in SHARED MODE");
                
                return true;
            }
            catch (Exception e)
            {
                LogError($"Failed to start game: {e.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region Player Management - Shared Mode
        
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            LogDebug($"🎯 [SHARED MODE] Player {player} joined the room");
            
            // En Shared Mode, cada jugador maneja su propio spawn
            // No hacemos spawn automático aquí, cada cliente spawna su propio objeto
            
            if (player == runner.LocalPlayer)
            {
                LogDebug("Local player joined - spawn handled separately");
            }
            else
            {
                LogDebug($"Remote player {player} joined");
            }
        }
        
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            LogDebug($"👋 [SHARED MODE] Player {player} left the room");
            
            // En Shared Mode, limpiamos objetos del jugador que se fue
            CleanupPlayerObjects(runner, player);
        }
        
        private void CleanupPlayerObjects(NetworkRunner runner, PlayerRef player)
        {
            LogDebug($"🧹 [SHARED MODE] Cleaning up objects for player {player}");
            
            lock (_playerObjectsLock)
            {
                if (_playerObjects.TryGetValue(player, out NetworkObject playerObject))
                {
                    if (playerObject != null && playerObject.IsValid)
                    {
                        // En Shared Mode, solo despawnear si tenemos autoridad o el objeto es huérfano
                        if (playerObject.HasInputAuthority || !playerObject.IsValid)
                        {
                            LogDebug($"Despawning object for player {player}");
                            runner.Despawn(playerObject);
                        }
                    }
                    _playerObjects.Remove(player);
                }
            }
            
            // Buscar objetos huérfanos
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            foreach (var lobbyPlayer in allLobbyPlayers)
            {
                if (lobbyPlayer.PlayerRef == player)
                {
                    var netObj = lobbyPlayer.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsValid && !netObj.HasInputAuthority)
                    {
                        LogDebug($"Found orphaned LobbyPlayer for player {player}");
                        // En Shared Mode, Photon manejará la limpieza automáticamente
                    }
                }
            }
            
            OnPlayerDespawned?.Invoke(player);
        }
        
        private void CleanupAllNetworkObjects()
        {
            LogDebug("🧹 [SHARED MODE] Cleaning up all local network objects");
            
            if (_runner == null || !_runner.IsRunning) return;
            
            // En Shared Mode, solo limpiamos objetos con autoridad local
            var allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
            foreach (var lobbyPlayer in allLobbyPlayers)
            {
                var netObj = lobbyPlayer.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsValid && netObj.HasInputAuthority)
                {
                    LogDebug($"Despawning local LobbyPlayer: {lobbyPlayer.PlayerName}");
                    _runner.Despawn(netObj);
                }
            }
            
            lock (_playerObjectsLock)
            {
                _playerObjects.Clear();
            }
        }
        
        #endregion
        
        #region Session Discovery - Sin cambios para Shared Mode
        
        public async Task<List<SessionInfo>> GetAvailableSessions()
        {
            // La búsqueda de sesiones funciona igual en Shared Mode
            lock (_sessionCacheLock)
            {
                if (_cachedSessions != null && 
                    (DateTime.Now - _lastSessionListTime).TotalSeconds < sessionCacheDuration)
                {
                    LogDebug($"Returning cached sessions ({_cachedSessions.Count} rooms)");
                    return new List<SessionInfo>(_cachedSessions);
                }
            }
            
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
                LogDebug("🔍 [SHARED MODE] Starting session discovery...");
                
                if (_persistentSessionFinderRunner == null || !_persistentSessionFinderRunner.IsRunning)
                {
                    await CreatePersistentSessionFinder();
                }
                
                ResetSessionFinderCleanupTimer();
                
                var sessionListCallback = new OptimizedSessionListCallback();
                
                _persistentSessionFinderRunner.AddCallbacks(sessionListCallback);
                
                await Task.Delay(1500);
                
                var sessions = sessionListCallback.GetSessions();
                
                _persistentSessionFinderRunner.RemoveCallbacks(sessionListCallback);
                
                lock (_sessionCacheLock)
                {
                    _cachedSessions = sessions;
                    _lastSessionListTime = DateTime.Now;
                }
                
                _consecutiveSessionFailures = 0;
                
                LogDebug($"✅ Found {sessions.Count} available SHARED MODE sessions");
                
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
        
        private async Task CreatePersistentSessionFinder()
        {
            LogDebug("Creating persistent session finder for SHARED MODE...");
            
            if (_persistentSessionFinderRunner != null)
            {
                if (_persistentSessionFinderRunner.IsRunning)
                {
                    await _persistentSessionFinderRunner.Shutdown();
                }
                Destroy(_persistentSessionFinderRunner.gameObject);
                _persistentSessionFinderRunner = null;
            }
            
            _persistentSessionFinderRunner = Instantiate(runnerPrefab);
            _persistentSessionFinderRunner.name = "NetworkRunner_SessionFinder_Shared";
            DontDestroyOnLoad(_persistentSessionFinderRunner.gameObject);
            
            await _persistentSessionFinderRunner.JoinSessionLobby(SessionLobby.Custom, "HackMonkeys_Lobby");
            
            LogDebug("✅ Session finder created for SHARED MODE");
        }
        
        #endregion
        
        #region INetworkRunnerCallbacks - Adaptado para Shared Mode
        
        public void OnConnectedToServer(NetworkRunner runner)
        {
            LogDebug("🌐 [SHARED MODE] Connected to Photon Cloud");
            
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.UpdateLocalPlayerRef(runner.LocalPlayer);
                LogDebug($"✅ LocalPlayerRef updated: {runner.LocalPlayer}");
            }
            
            LogDebug($"🌐 OnConnectedToServer - SHARED MODE active");
        }
        
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            LogDebug($"📡 [SHARED MODE] Disconnected from server: {reason}");
            
            if (runner.IsRunning)
            {
                CleanupAllNetworkObjects();
            }
            
            _isInRoom = false;
            _isRoomCreator = false;
            
            lock (_playerObjectsLock)
            {
                _playerObjects.Clear();
            }
            
            if (_gameCore != null)
            {
                _gameCore.OnNetworkDisconnected();
            }
        }
        
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            LogDebug($"🎬 [SHARED MODE] Scene load done");
            LogDebug($"- Current Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            LogDebug($"- Local Player: {runner.LocalPlayer}");
            
            if (_gameCore != null)
            {
                LogDebug("✅ Notifying GameCore that scene loaded");
                _gameCore.OnGameSceneLoaded();
            }
        }
        
        public void OnSceneLoadStart(NetworkRunner runner)
        {
            LogDebug($"🎬 [SHARED MODE] Scene load starting");
            
            _gameCore?.TransitionToState(GameCore.GameState.LoadingMatch);
            
            PlayerDataManager.Instance?.UpdateSelectedMapFromLobbyPlayer();
        }
        
        #endregion
        
        #region Helper Methods - Sin cambios significativos
        
        private string SanitizeRoomName(string name)
        {
            name = name.Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\s-.]", "");
            
            if (name.Length > 32)
                name = name.Substring(0, 32);
            
            return name;
        }
        
        private Dictionary<string, SessionProperty> CreateEnhancedSessionProperties(string roomName, string sceneName)
        {
            var properties = new Dictionary<string, SessionProperty>();
            
            properties["displayName"] = roomName;
            properties["hostName"] = PlayerDataManager.Instance?.GetPlayerName() ?? "Player";
            properties["mode"] = "shared"; // Indicar que es Shared Mode
            
            if (!string.IsNullOrEmpty(sceneName))
            {
                properties["scene"] = sceneName;
            }
            
            properties["version"] = Application.version;
            properties["timestamp"] = System.DateTime.Now.Ticks.ToString();
            
            return properties;
        }
        
        // ... [Resto de métodos helper sin cambios significativos] ...
        
        #endregion
        
        #region Cleanup & Utilities
        
        private async Task ShutdownRunner()
        {
            if (_runner != null)
            {
                LogDebug("🔄 [SHARED MODE] Shutting down runner...");
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
            
            _isRoomCreator = false;
            
            await Task.Delay(100);
        }
        
        // ... [Resto de métodos sin cambios] ...
        
        #endregion
        
        #region Remaining INetworkRunnerCallbacks
        
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            LogError($"[SHARED MODE] Connect failed: {reason}");
            OnConnectionFailed?.Invoke(reason.ToString());
        }
        
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner.name.Contains("SessionFinder"))
            {
                LogDebug($"📋 [SHARED MODE] Session list updated: {sessionList.Count} sessions");
                OnSessionListUpdatedEvent?.Invoke(sessionList);
            }
        }
        
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            LogDebug($"🔄 [SHARED MODE] Runner shutdown: {shutdownReason}");
            
            lock (_playerObjectsLock)
            {
                _playerObjects.Clear();
            }
            
            var orphanedPlayers = FindObjectsOfType<LobbyPlayer>();
            foreach (var player in orphanedPlayers)
            {
                if (player.GetComponent<NetworkObject>()?.HasInputAuthority == true)
                {
                    LogDebug($"Destroying local orphaned LobbyPlayer: {player.name}");
                    Destroy(player.gameObject);
                }
            }
        }
        
        // Empty implementations
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        
        #endregion
        
        #region Logging Utilities
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[NetworkBootstrapper-SHARED] {message}");
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[NetworkBootstrapper-SHARED] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[NetworkBootstrapper-SHARED] {message}");
        }
        
        #endregion
        
        #region Session Finder Cleanup
        
        private void ResetSessionFinderCleanupTimer()
        {
            if (!autoCleanupSessionFinder) return;
            
            if (_sessionFinderCleanupCoroutine != null)
            {
                StopCoroutine(_sessionFinderCleanupCoroutine);
            }
            
            _sessionFinderCleanupCoroutine = StartCoroutine(SessionFinderIdleCleanup());
        }
        
        private IEnumerator SessionFinderIdleCleanup()
        {
            yield return new WaitForSeconds(sessionFinderIdleTimeout);
            
            if (!_isSessionFinderActive && _persistentSessionFinderRunner != null)
            {
                LogDebug("Session finder idle timeout - cleaning up");
                CleanupSessionFinderImmediate();
            }
        }
        
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
        
        public void InvalidateSessionCache()
        {
            lock (_sessionCacheLock)
            {
                _cachedSessions = null;
                _lastSessionListTime = DateTime.MinValue;
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
        
        #endregion
        
        #region Validation
        
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
        
        #region Helper Classes
        
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
                    Debug.Log($"[OptimizedSessionListCallback-SHARED] Received {sessionList.Count} sessions");
                }
            }
            
            // Empty implementations
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