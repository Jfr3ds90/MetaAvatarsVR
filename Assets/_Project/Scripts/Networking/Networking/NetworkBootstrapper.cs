using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Maneja la inicialización y configuración de Photon Fusion para el sistema de lobby
    /// </summary>
    public class NetworkBootstrapper : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Configuration")]
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private NetworkSceneManagerDefault sceneManagerPrefab;
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private string gameSceneName = "GameScene";
        
        [Header("Room Settings")]
        [SerializeField] private int defaultMaxPlayers = 4;
        [SerializeField] private string defaultRegion = "us";
        
        [Header("Events")]
        public UnityEvent OnConnectedToServerEvent;
        public UnityEvent<string> OnConnectionFailed;
        public UnityEvent<List<SessionInfo>> OnSessionListUpdatedEvent;
        public UnityEvent<PlayerRef> OnPlayerJoinedRoom;
        public UnityEvent<PlayerRef> OnPlayerLeftRoom;
        
        private NetworkRunner _runner;
        private NetworkSceneManagerDefault _sceneManager;
        private bool _isInRoom = false;
        
        private TaskCompletionSource<List<SessionInfo>> _sessionListTcs;
        private List<SessionInfo> _receivedSessions;
        
        public static NetworkBootstrapper Instance { get; private set; }
        public NetworkRunner Runner => _runner;
        public bool IsConnected => _runner != null && _runner.IsRunning;
        public bool IsInRoom => _isInRoom;
        public bool IsHost => _runner != null && _runner.IsServer;
        
        // Información de la sala actual
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
        
        /// <summary>
        /// Crea una nueva sala como Host
        /// </summary>
        public async Task<bool> CreateRoom(string roomName, int maxPlayers = 0)
        {
            if (_runner != null)
            {
                Debug.LogWarning("[NetworkBootstrapper] Runner already exists. Shutting down...");
                await ShutdownRunner();
            }
            
            try
            {
                Debug.Log($"[NetworkBootstrapper] Creating room: {roomName}");
                
                // Usar valores por defecto si no se especifican
                if (maxPlayers <= 0) maxPlayers = defaultMaxPlayers;
                
                CurrentRoomName = roomName;
                CurrentMaxPlayers = maxPlayers;
                
                // Crear nuevo NetworkRunner
                _runner = Instantiate(runnerPrefab);
                _runner.name = "NetworkRunner_Host";
                
                // Crear SceneManager
                _sceneManager = Instantiate(sceneManagerPrefab);
                
                // Configurar los argumentos de inicio
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Host,
                    SessionName = roomName,
                    PlayerCount = maxPlayers,
                    SceneManager = _sceneManager,
                    // NO especificar Scene para quedarnos en la escena actual
                    CustomLobbyName = "HackMonkeys_Lobby",
                    IsVisible = true,
                    IsOpen = true
                };
                
                // Iniciar la conexión
                var result = await _runner.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    Debug.Log("[NetworkBootstrapper] Room created successfully!");
                    _isInRoom = true;
                    OnConnectedToServerEvent?.Invoke();
                    return true;
                }
                else
                {
                    Debug.LogError($"[NetworkBootstrapper] Failed to create room: {result.ShutdownReason}");
                    OnConnectionFailed?.Invoke(result.ShutdownReason.ToString());
                    await CleanupRunner();
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] Exception creating room: {e.Message}");
                OnConnectionFailed?.Invoke(e.Message);
                await CleanupRunner();
                return false;
            }
        }
        
        /// <summary>
        /// Une a una sala existente como Client
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
                
                // Crear nuevo NetworkRunner
                _runner = Instantiate(runnerPrefab);
                _runner.name = "NetworkRunner_Client";
                
                // Crear SceneManager
                _sceneManager = Instantiate(sceneManagerPrefab);
                
                // Configurar los argumentos de inicio
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Client,
                    SessionName = session.Name,
                    SceneManager = _sceneManager,
                    // NO especificar Scene para quedarnos en la escena actual
                    CustomLobbyName = "HackMonkeys_Lobby"
                };
                
                // Iniciar la conexión
                var result = await _runner.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    Debug.Log("[NetworkBootstrapper] Joined room successfully!");
                    _isInRoom = true;
                    OnConnectedToServerEvent?.Invoke();
                    return true;
                }
                else
                {
                    Debug.LogError($"[NetworkBootstrapper] Failed to join room: {result.ShutdownReason}");
                    OnConnectionFailed?.Invoke(result.ShutdownReason.ToString());
                    await CleanupRunner();
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] Exception joining room: {e.Message}");
                OnConnectionFailed?.Invoke(e.Message);
                await CleanupRunner();
                return false;
            }
        }
        
        /// <summary>
        /// Obtiene la lista de sesiones disponibles
        /// </summary>
        public async Task<List<SessionInfo>> GetAvailableSessions()
        {
            try
            {
                // Inicializar el TaskCompletionSource
                _sessionListTcs = new TaskCompletionSource<List<SessionInfo>>();
                _receivedSessions = new List<SessionInfo>();
        
                // Crear un runner temporal solo para buscar sesiones
                var tempRunner = Instantiate(runnerPrefab);
                tempRunner.name = "NetworkRunner_SessionFinder";
        
                // IMPORTANTE: Agregar callbacks para recibir la lista
                tempRunner.AddCallbacks(this);
        
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.AutoHostOrClient, // Cambiar a este modo para recibir lista
                    CustomLobbyName = "HackMonkeys_Lobby",
                    DisableNATPunchthrough = true,
                    EnableClientSessionCreation = false // No crear sesión, solo listar
                };
        
                await tempRunner.StartGame(startGameArgs);
        
                // Esperar hasta recibir la lista o timeout de 5 segundos
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(_sessionListTcs.Task, timeoutTask);
        
                List<SessionInfo> sessions;
                if (completedTask == _sessionListTcs.Task)
                {
                    sessions = await _sessionListTcs.Task;
                }
                else
                {
                    Debug.LogWarning("[NetworkBootstrapper] Timeout waiting for session list");
                    sessions = _receivedSessions; // Usar lo que se haya recibido hasta ahora
                }
        
                // Limpiar el runner temporal
                tempRunner.RemoveCallbacks(this);
                await tempRunner.Shutdown();
                Destroy(tempRunner.gameObject);
        
                return sessions;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] Failed to get sessions: {e.Message}");
                return new List<SessionInfo>();
            }
        }
        
        /// <summary>
        /// Abandona la sala actual
        /// </summary>
        public async Task LeaveRoom()
        {
            if (_runner == null || !_isInRoom) return;
            
            Debug.Log("[NetworkBootstrapper] Leaving room...");
            
            await ShutdownRunner();
            _isInRoom = false;
            CurrentRoomName = "";
            CurrentMaxPlayers = 0;
            
            // NO cambiar de escena, solo notificar que salimos
            // El UI Manager debería mostrar el panel principal
        }
        
        /// <summary>
        /// Inicia la partida (solo el Host puede hacerlo)
        /// </summary>
        public async Task<bool> StartGame()
        {
            if (!IsHost || !_isInRoom)
            {
                Debug.LogError("[NetworkBootstrapper] Only host can start the game!");
                return false;
            }
            
            try
            {
                Debug.Log("[NetworkBootstrapper] Starting game...");
                
                // Cambiar a la escena del juego
                await _runner.LoadScene(GetSceneIndex(gameSceneName));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] Failed to start game: {e.Message}");
                return false;
            }
        }
        
        private async Task ShutdownRunner()
        {
            if (_runner != null)
            {
                await _runner.Shutdown();
                await CleanupRunner();
            }
        }
        
        private async Task CleanupRunner()
        {
            if (_runner != null)
            {
                Destroy(_runner.gameObject);
                _runner = null;
            }
            
            if (_sceneManager != null)
            {
                Destroy(_sceneManager.gameObject);
                _sceneManager = null;
            }
            
            // Pequeño delay para asegurar la limpieza
            await Task.Delay(100);
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
            
            Debug.LogError($"[NetworkBootstrapper] Scene '{sceneName}' not found in build settings!");
            return SceneRef.FromIndex(0);
        }
        
        #region INetworkRunnerCallbacks
        
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkBootstrapper] Player {player} joined the room");
            OnPlayerJoinedRoom?.Invoke(player);
        }
        
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkBootstrapper] Player {player} left the room");
            OnPlayerLeftRoom?.Invoke(player);
        }
        
        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[NetworkBootstrapper] Connected to Photon Cloud");
        }
        
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[NetworkBootstrapper] Disconnected from server: {reason}");
            _isInRoom = false;
        }
        
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[NetworkBootstrapper] Connect failed: {reason}");
            OnConnectionFailed?.Invoke(reason.ToString());
        }
        
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner.name == "NetworkRunner_SessionFinder")
            {
                _receivedSessions = new List<SessionInfo>(sessionList);
                _sessionListTcs?.TrySetResult(_receivedSessions);
            }
            OnSessionListUpdatedEvent?.Invoke(sessionList);
        }
        
        // Implementación vacía de otros callbacks
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
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
        
        #endregion
        
        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.Shutdown();
            }
        }
    }
}