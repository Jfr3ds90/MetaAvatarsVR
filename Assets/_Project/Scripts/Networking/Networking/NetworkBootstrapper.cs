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
    /// NetworkBootstrapper CONSOLIDADO - Ahora maneja spawning de jugadores
    /// ELIMINA la dependencia de PlayerSpawner
    /// </summary>
    public class NetworkBootstrapper : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Configuration")]
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private NetworkSceneManagerDefault sceneManagerPrefab;
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private string gameSceneName = "GameScene";
        
        [Header("✅ NUEVO: Player Spawning")]
        [SerializeField] private LobbyPlayer lobbyPlayerPrefab; // ✅ AÑADIDO
        
        [Header("Room Settings")]
        [SerializeField] private int defaultMaxPlayers = 4;
        [SerializeField] private string defaultRegion = "us";
        
        [Header("Events - SIMPLIFICADOS")]
        public UnityEvent OnConnectedToServerEvent;
        public UnityEvent<string> OnConnectionFailed;
        public UnityEvent<List<SessionInfo>> OnSessionListUpdatedEvent;
        // ❌ ELIMINADO: OnPlayerJoinedRoom y OnPlayerLeftRoom (redundantes)
        
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
        /// ✅ MÉTODO MEJORADO: Crear sala + configurar callbacks automáticamente
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
                
                // ✅ CRÍTICO: Configurar callbacks ANTES de StartGame
                _runner.AddCallbacks(this);
                
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
                    Debug.Log("[NetworkBootstrapper] ✅ Room created successfully!");
                    Debug.Log("[NetworkBootstrapper] ✅ Callbacks configured - ready for player spawning");
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
        
        /// <summary>
        /// ✅ MÉTODO MEJORADO: Une a una sala existente + configurar callbacks
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
                
                // ✅ CRÍTICO: Configurar callbacks ANTES de StartGame
                _runner.AddCallbacks(this);
                
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
                    Debug.Log("[NetworkBootstrapper] ✅ Joined room successfully!");
                    Debug.Log("[NetworkBootstrapper] ✅ Callbacks configured - ready for player spawning");
                    _isInRoom = true;
                    OnConnectedToServerEvent?.Invoke();
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
        // ✅ CALLBACK CONSOLIDADO: AQUÍ SE MUEVE LA LÓGICA DE PlayerSpawner
        // ========================================
        
        /// <summary>
        /// ✅ CONSOLIDADO: Player spawning movido desde PlayerSpawner.cs
        /// </summary>
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkBootstrapper] 🎯 Player {player} joined the room");
            
            // ✅ SPAWN LOGIC CONSOLIDADO (antes en PlayerSpawner.cs)
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
            
            // ❌ ELIMINADO: OnPlayerJoinedRoom?.Invoke(player); (redundante)
            // El LobbyPlayer se auto-registrará en LobbyManager cuando haga Spawned()
        }
        
        /// <summary>
        /// ✅ CALLBACK: Player left
        /// </summary>
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkBootstrapper] 👋 Player {player} left the room");
            
            // ❌ ELIMINADO: OnPlayerLeftRoom?.Invoke(player); (redundante)
            // El NetworkObject se destruye automáticamente
            // LobbyManager se enterará via LobbyPlayer.Despawned()
        }
        
        /// <summary>
        /// Obtiene la lista de sesiones disponibles
        /// </summary>
        public async Task<List<SessionInfo>> GetAvailableSessions()
        {
            try
            {
                Debug.Log("[NetworkBootstrapper] 🔍 Searching for available sessions...");
        
                // CORRECCIÓN: Usar un método diferente para solo listar sesiones
                // Opción 1: Usar JoinSessionLobby para obtener lista sin unirse
        
                var tempRunner = Instantiate(runnerPrefab);
                tempRunner.name = "NetworkRunner_SessionFinder";
        
                // Crear un callback temporal solo para capturar la lista
                var sessionListCallback = new SessionListCallback();
                tempRunner.AddCallbacks(sessionListCallback);
        
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Client, // CAMBIO: Client mode no auto-join
                    SessionName = null, // IMPORTANTE: No especificar nombre
                    CustomLobbyName = "HackMonkeys_Lobby",
                    //DisableClientSessionCreation = true // No crear sesión nueva
                };
        
                // Intentar conectar al lobby (no a una sesión específica)
                await tempRunner.JoinSessionLobby(SessionLobby.Custom, "HackMonkeys_Lobby");
        
                // Esperar un momento para recibir la lista
                await Task.Delay(2000);
        
                var sessions = sessionListCallback.GetSessions();
        
                // Limpiar
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
        /// Inicia la partida (solo el Host puede hacerlo)
        /// </summary>
        public async Task<bool> StartGame()
        {
            if (!IsHost || !_isInRoom)
            {
                Debug.LogError("[NetworkBootstrapper] ❌ Only host can start the game!");
                return false;
            }
            
            try
            {
                Debug.Log("[NetworkBootstrapper] 🚀 Starting game...");
                
                // Cambiar a la escena del juego
                await _runner.LoadScene(GetSceneIndex(gameSceneName));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] ❌ Failed to start game: {e.Message}");
                return false;
            }
        }
        
        // ========================================
        // MÉTODOS DE LIMPIEZA MEJORADOS
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
                // ✅ CRÍTICO: Remover callbacks antes de destruir
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
            
            Debug.LogError($"[NetworkBootstrapper] ❌ Scene '{sceneName}' not found in build settings!");
            return SceneRef.FromIndex(0);
        }
        
        // ========================================
        // CALLBACKS DE FUSION - IMPLEMENTACIÓN COMPLETA
        // ========================================
        
        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[NetworkBootstrapper] 🌐 Connected to Photon Cloud");
        }
        
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[NetworkBootstrapper] 📡 Disconnected from server: {reason}");
            _isInRoom = false;
        }
        
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[NetworkBootstrapper] ❌ Connect failed: {reason}");
            OnConnectionFailed?.Invoke(reason.ToString());
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
        
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            // Solo procesar si es el runner temporal de búsqueda
            if (runner.name == "NetworkRunner_SessionFinder")
            {
                Debug.Log($"[NetworkBootstrapper] 📋 Session list updated: {sessionList.Count} sessions");
                _receivedSessions = new List<SessionInfo>(sessionList);
                _sessionListTcs?.TrySetResult(_receivedSessions);
            }
            
            // Notificar a LobbyBrowser
            OnSessionListUpdatedEvent?.Invoke(sessionList);
        }
        
        // Implementación vacía de otros callbacks
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) 
        {
            Debug.Log($"[NetworkBootstrapper] 🔄 Runner shutdown: {shutdownReason}");
        }
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
    }
}