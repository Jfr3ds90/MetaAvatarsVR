using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Realtime;
using UnityEngine.Events;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Maneja la inicialización y configuración de Photon Fusion para el menú principal VR
    /// </summary>
    public class NetworkBootstrapper : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Configuration")]
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private string preferredRegion = "sa";
        [SerializeField] private int maxReconnectAttempts = 3;
        
        [Header("Events")]
        public UnityEvent OnConnectedToServer_event;
        public UnityEvent<string> OnConnectionFailed;
        public UnityEvent<List<SessionInfo>> OnSessionListUpdated_event;
        
        private NetworkRunner _runner;
        private int _reconnectAttempts = 0;
        private bool _isConnecting = false;
        
        public static NetworkBootstrapper Instance { get; private set; }
        public bool IsConnected => _runner != null && _runner.IsConnectedToServer;
        public NetworkRunner Runner => _runner;
        
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
        
        private async void Start()
        {
            // Esperar a que VR esté completamente inicializado
            await Task.Delay(1000);
            await InitializeNetwork();
        }
        
        public async Task InitializeNetwork()
        {
            if (_isConnecting || IsConnected) return;
            
            _isConnecting = true;
            Debug.Log("[NetworkBootstrapper] Initializing Photon Fusion...");
            
            try
            {
                // Crear el NetworkRunner si no existe
                if (_runner == null)
                {
                    _runner = Instantiate(runnerPrefab);
                    _runner.name = "NetworkRunner";
                }
                
                // Configurar los argumentos de inicio
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Shared, // Modo compartido para lobby
                    SessionName = "MainMenu_Lobby",
                    CustomLobbyName = "HackMonkeys_MainLobby",
                    SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
                };
                
                // Configurar región preferida
                var appSettings = PhotonAppSettings.Instance.AppSettings;
                appSettings.FixedRegion = preferredRegion;
                appSettings.UseNameServer = true;
                
                // Iniciar la conexión
                var result = await _runner.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    Debug.Log("[NetworkBootstrapper] Connected successfully!");
                    _reconnectAttempts = 0;
                    OnConnectedToServer_event?.Invoke();
                    
                    // Iniciar actualización de lista de salas
                    InvokeRepeating(nameof(UpdateSessionList), 0f, 2f);
                }
                else
                {
                    throw new Exception($"Failed to start: {result.ShutdownReason}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] Connection failed: {e.Message}");
                await HandleConnectionFailure(e.Message);
            }
            finally
            {
                _isConnecting = false;
            }
        }
        
        private async Task HandleConnectionFailure(string error)
        {
            OnConnectionFailed?.Invoke(error);
            
            if (_reconnectAttempts < maxReconnectAttempts)
            {
                _reconnectAttempts++;
                Debug.Log($"[NetworkBootstrapper] Reconnect attempt {_reconnectAttempts}/{maxReconnectAttempts}");
                
                await Task.Delay(2000); // Esperar 2 segundos
                await InitializeNetwork();
            }
            else
            {
                Debug.LogError("[NetworkBootstrapper] Max reconnection attempts reached");
            }
        }
        
        private async void UpdateSessionList()
        {
            if (!IsConnected) return;
            
            try
            {
                //var sessions = await _runner.GetSessionsAsync();
                //OnSessionListUpdated_event?.Invoke(sessions);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] Failed to update session list: {e.Message}");
            }
        }
        
        public async Task<bool> CreateRoom(string roomName, int maxPlayers = 6)
        {
            if (!IsConnected) return false;
            
            try
            {
                await _runner.Shutdown();
                
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Host,
                    SessionName = roomName,
                    PlayerCount = maxPlayers,
                    SceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>(),
                    Scene = SceneRef.FromIndex(1) // Índice de la escena del juego
                };
                
                var result = await _runner.StartGame(startGameArgs);
                return result.Ok;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] Failed to create room: {e.Message}");
                return false;
            }
        }
        
        public async Task<bool> JoinRoom(SessionInfo session)
        {
            if (!IsConnected) return false;
            
            try
            {
                await _runner.Shutdown();
                
                var startGameArgs = new StartGameArgs()
                {
                    GameMode = GameMode.Client,
                    SessionName = session.Name,
                    SceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>()
                };
                
                var result = await _runner.StartGame(startGameArgs);
                return result.Ok;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkBootstrapper] Failed to join room: {e.Message}");
                return false;
            }
        }
        
        #region INetworkRunnerCallbacks

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            throw new NotImplementedException();
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkBootstrapper] Player {player} joined");
        }
        
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkBootstrapper] Player {player} left");
        }
        
        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[NetworkBootstrapper] Connected to Photon Cloud");
        }
        
        public void OnDisconnectedFromServer(NetworkRunner runner)
        {
            Debug.Log("[NetworkBootstrapper] Disconnected from server");
            CancelInvoke(nameof(UpdateSessionList));
            
            // Intentar reconectar automáticamente
            _ = HandleConnectionFailure("Disconnected from server");
        }
        
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[NetworkBootstrapper] Connect failed: {reason}");
            _ = HandleConnectionFailure(reason.ToString());
        }
        
        // Implementación vacía de otros callbacks requeridos
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            throw new NotImplementedException();
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            throw new NotImplementedException();
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        
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