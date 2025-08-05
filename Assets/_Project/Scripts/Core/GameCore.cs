using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;
using Fusion;
using DG.Tweening;

namespace HackMonkeys.Core
{
    /// <summary>
    /// GameCore - Coordinador central del flujo del juego VR Multijugador
    /// Maneja transiciones de estado, pantallas de carga VR, y coordina todos los sistemas
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameCore : MonoBehaviour
    {
        [Header("Start Panel")] 
        [SerializeField] private GameState gameState; 
        
        #region Singleton
        private static GameCore _instance;

        public static GameCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameCore>();
                    if (_instance == null)
                    {
                        GameObject coreObject = new GameObject("[GameCore]");
                        _instance = coreObject.AddComponent<GameCore>();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Game States
        public enum GameState
        {
            Initializing,   // Inicializando sistemas
            MainMenu,       // En menú principal
            InLobby,        // En el lobby esperando
            LoadingMatch,   // Cargando partida
            InMatch,        // Jugando
            Results,        // Pantalla de resultados
            Disconnected,   // Desconectado/Error
            NameTag
        }

        [Header("State Management")]
        [SerializeField] private GameState _currentState = GameState.Initializing;
        [SerializeField] private GameState _previousState;
        
        public GameState CurrentState => _currentState;
        public GameState PreviousState => _previousState;
        public bool IsInGame => _currentState == GameState.InMatch;
        public bool IsLoading => _currentState == GameState.LoadingMatch || _currentState == GameState.Initializing;
        #endregion

        #region VR Loading Environment
        [Header("VR Loading Configuration")]
        [SerializeField] private GameObject vrLoadingEnvironmentPrefab;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private Color fadeColor = Color.black;
        
        private GameObject _currentLoadingEnvironment;
        private CanvasGroup _vrFadeCanvas;
        private VRLoadingEnvironment _loadingEnvironment;
        #endregion

        #region Scene Configuration
        [Header("Scene Configuration")]
        [SerializeField] private string menuSceneName = "MenuScene";
        [SerializeField] private string resultsSceneName = "ResultsScene";
        // El gameplay scene viene del NetworkBootstrapper según el mapa seleccionado
        #endregion

        #region Core References
        private NetworkBootstrapper _networkBootstrapper;
        private SpatialUIManager _spatialUIManager;
        private PlayerDataManager _playerDataManager;
        private LobbyController _lobbyController;
        
        // Match Data
        private string _currentMapName;
        private int _currentPlayerCount;
        private float _matchStartTime;
        #endregion

        #region Events
        public static event Action<GameState, GameState> OnStateChanged;
        public static event Action<float> OnLoadingProgress;
        public static event Action OnMatchStarted;
        public static event Action OnMatchEnded;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("[GameCore] 🎮 GameCore initialized");
        }

        private void Start()
        {
            StartCoroutine(InitializeGame());
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Initialization
        private IEnumerator InitializeGame()
        {
            Debug.Log("[GameCore] 🚀 Starting game initialization...");
            
            // Obtener referencias a sistemas existentes
            yield return new WaitUntil(() => NetworkBootstrapper.Instance != null);
            _networkBootstrapper = NetworkBootstrapper.Instance;
            
            yield return new WaitUntil(() => SpatialUIManager.Instance != null);
            _spatialUIManager = SpatialUIManager.Instance;
            
            yield return new WaitUntil(() => PlayerDataManager.Instance != null);
            _playerDataManager = PlayerDataManager.Instance;
            
            // Crear canvas para fade VR si no existe
            CreateVRFadeCanvas();
            
            // Transicionar a menú principal
            yield return TransitionToStateCoroutine(GameState.NameTag);
            
            Debug.Log("[GameCore] ✅ Game initialization complete");
        }

        private void CreateVRFadeCanvas()
        {
            GameObject fadeObject = new GameObject("VR Fade Canvas");
            fadeObject.transform.SetParent(transform);
            
            Canvas canvas = fadeObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            
            _vrFadeCanvas = fadeObject.AddComponent<CanvasGroup>();
            _vrFadeCanvas.alpha = 0;
            _vrFadeCanvas.blocksRaycasts = false;
            
            UnityEngine.UI.Image fadeImage = fadeObject.AddComponent<UnityEngine.UI.Image>();
            fadeImage.color = fadeColor;
            fadeImage.rectTransform.anchorMin = Vector2.zero;
            fadeImage.rectTransform.anchorMax = Vector2.one;
            fadeImage.rectTransform.sizeDelta = Vector2.zero;
        }
        #endregion

        #region State Management
        /// <summary>
        /// Transiciona a un nuevo estado del juego
        /// </summary>
        public void TransitionToState(GameState newState)
        {
            if (_currentState == newState)
            {
                Debug.LogWarning($"[GameCore] Already in state {newState}");
                return;
            }

            StartCoroutine(TransitionToStateCoroutine(newState));
        }

        private IEnumerator TransitionToStateCoroutine(GameState newState)
        {
            Debug.Log($"[GameCore] 🔄 Transitioning from {_currentState} to {newState}");
            
            _previousState = _currentState;
            
            // Exit current state
            yield return ExitState(_currentState);
            
            // Update state
            _currentState = newState;
            
            // Enter new state
            yield return EnterState(newState);
            
            // Notify listeners
            OnStateChanged?.Invoke(_previousState, _currentState);
            
            Debug.Log($"[GameCore] ✅ Transitioned to {newState}");
        }

        private IEnumerator ExitState(GameState state)
        {
            switch (state)
            {
                case GameState.InMatch:
                    // Limpiar sistemas de gameplay
                    CleanupGameplaySystems();
                    yield return FadeOut();
                    break;
                    
                case GameState.InLobby:
                    // Guardar datos del lobby si es necesario
                    SaveLobbyData();
                    break;
            }
        }

        private IEnumerator EnterState(GameState state)
        {
            switch (state)
            {
                case GameState.NameTag:
                    yield return EnterName();
                    break;
                case GameState.MainMenu:
                    yield return EnterMainMenu();
                    break;
                    
                case GameState.InLobby:
                    yield return EnterLobby();
                    break;
                    
                case GameState.LoadingMatch:
                    yield return EnterLoadingMatch();
                    break;
                    
                case GameState.InMatch:
                    yield return EnterMatch();
                    break;
                    
                case GameState.Results:
                    yield return EnterResults();
                    break;
                    
                case GameState.Disconnected:
                    yield return EnterDisconnected();
                    break;
            }
        }
        #endregion

        #region State Enter Methods
        private IEnumerator EnterMainMenu()
        {
            // Asegurar que estamos en la escena del menú
            if (SceneManager.GetActiveScene().name != menuSceneName)
            {
                yield return LoadSceneAsync(menuSceneName);
            }
            
            // Mostrar UI principal
            _spatialUIManager?.ShowPanel(PanelID.MainPanel);
            
            yield return FadeIn();
        }
        
        private IEnumerator EnterName()
        {
            // Asegurar que estamos en la escena del menú
            if (SceneManager.GetActiveScene().name != menuSceneName)
            {
                yield return LoadSceneAsync(menuSceneName);
            }
            
            // Mostrar UI principal
            _spatialUIManager?.ShowPanel(PanelID.NameTag);
            
            yield return FadeIn();
        }
        

        private IEnumerator EnterLobby()
        {
            // UI ya debería estar mostrando el lobby panel
            // Solo actualizamos estado
            yield return null;
        }

        private IEnumerator EnterLoadingMatch()
        {
            Debug.Log($"[GameCore] Entering LoadingMatch state - IsHost: {NetworkBootstrapper.Instance?.IsHost}");
    
            // Fade out
            yield return FadeOut();
    
            // Mostrar ambiente de carga VR
            yield return ShowVRLoadingEnvironment();
    
            // Ocultar UI del lobby
            _spatialUIManager?.gameObject.SetActive(false);
    
            // Los clientes solo esperan, el host inicia el cambio de escena
            if (NetworkBootstrapper.Instance?.IsHost == true)
            {
                Debug.Log("[GameCore] HOST: Will trigger scene change");
            }
            else
            {
                Debug.Log("[GameCore] CLIENT: Waiting for scene sync from host");
            }
        }

        private IEnumerator EnterMatch()
        {
            // Registrar inicio de partida
            _matchStartTime = Time.time;
            OnMatchStarted?.Invoke();
            
            // Ocultar ambiente de carga
            yield return HideVRLoadingEnvironment();
            
            // Fade in al juego
            yield return FadeIn();
            
            Debug.Log("[GameCore] 🎮 Match started!");
        }

        private IEnumerator EnterResults()
        {
            // Fade out del juego
            yield return FadeOut();
            
            // Cargar escena de resultados
            yield return LoadSceneAsync(resultsSceneName);
            
            // Mostrar UI de resultados
            _spatialUIManager?.gameObject.SetActive(true);
            _spatialUIManager?.ShowPanel(PanelID.Results);
            
            yield return FadeIn();
        }

        private IEnumerator EnterDisconnected()
        {
            // Mostrar mensaje de desconexión
            ShowVRNotification("Connection Lost", 3f);
            
            // Esperar un poco
            yield return new WaitForSeconds(2f);
            
            // Volver al menú
            TransitionToState(GameState.MainMenu);
        }
        #endregion

        #region Public API - Game Flow
        /// <summary>
        /// Llamado cuando el jugador crea o se une a un lobby
        /// </summary>
        public void OnJoinedLobby(string roomName, bool isHost)
        {
            Debug.Log($"[GameCore] Joined lobby: {roomName} (Host: {isHost})");
            TransitionToState(GameState.InLobby);
        }

        /// <summary>
        /// Llamado cuando el host inicia la partida
        /// </summary>
        public async Task<bool> StartMatch(string mapName, int playerCount)
        {
            if (_currentState != GameState.InLobby)
            {
                Debug.LogError("[GameCore] Can only start match from lobby");
                return false;
            }
    
            // SOLO el host ejecuta esta lógica
            if (NetworkBootstrapper.Instance?.IsHost != true)
            {
                Debug.LogWarning("[GameCore] Only host can start match - clients wait for scene sync");
                return false;
            }
    
            _currentMapName = mapName;
            _currentPlayerCount = playerCount;
    
            // Transicionar a loading
            TransitionToState(GameState.LoadingMatch);
    
            await Task.Yield();
    
            return true;
        }


        /// <summary>
        /// Llamado por NetworkBootstrapper cuando la escena del juego está lista
        /// </summary>
        public void OnGameSceneLoaded()
        {
            if (_currentState == GameState.LoadingMatch)
            {
                TransitionToState(GameState.InMatch);
            }
        }

        public void OnFinishEnterName()
        {
            if (_currentState == GameState.NameTag)
            {
                TransitionToState(GameState.MainMenu);
            }
        }

        /// <summary>
        /// Finalizar partida actual
        /// </summary>
        public void EndMatch(MatchResult result = null)
        {
            if (_currentState != GameState.InMatch)
            {
                Debug.LogWarning("[GameCore] Not in match, cannot end");
                return;
            }
            
            OnMatchEnded?.Invoke();
            
            // Guardar resultados si los hay
            if (result != null)
            {
                SaveMatchResults(result);
            }
            
            TransitionToState(GameState.Results);
        }

        /// <summary>
        /// Volver al menú principal
        /// </summary>
        public async void ReturnToMainMenu()
        {
            // Desconectar si estamos conectados
            if (_networkBootstrapper != null && _networkBootstrapper.IsConnected)
            {
                await _networkBootstrapper.LeaveRoom();
            }
            
            TransitionToState(GameState.MainMenu);
        }

        /// <summary>
        /// Manejar desconexión de red
        /// </summary>
        public void OnNetworkDisconnected()
        {
            Debug.LogWarning("[GameCore] Network disconnected!");
            
            if (_currentState == GameState.InMatch || _currentState == GameState.InLobby)
            {
                TransitionToState(GameState.Disconnected);
            }
        }
        #endregion

        #region VR Loading Environment
        private IEnumerator ShowVRLoadingEnvironment()
        {
            if (vrLoadingEnvironmentPrefab == null)
            {
                Debug.LogWarning("[GameCore] No VR loading environment prefab assigned");
                yield break;
            }
            
            // Instanciar ambiente de carga
            _currentLoadingEnvironment = Instantiate(vrLoadingEnvironmentPrefab);
            _loadingEnvironment = _currentLoadingEnvironment.GetComponent<VRLoadingEnvironment>();
            
            if (_loadingEnvironment != null)
            {
                _loadingEnvironment.Show();
                
                // Actualizar progreso
                StartCoroutine(UpdateLoadingProgress());
            }
            
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator HideVRLoadingEnvironment()
        {
            if (_loadingEnvironment != null)
            {
                _loadingEnvironment.Hide();
                yield return new WaitForSeconds(0.5f);
            }
            
            if (_currentLoadingEnvironment != null)
            {
                Destroy(_currentLoadingEnvironment);
                _currentLoadingEnvironment = null;
            }
        }

        private IEnumerator UpdateLoadingProgress()
        {
            float progress = 0;
            
            while (_currentState == GameState.LoadingMatch && _loadingEnvironment != null)
            {
                // Simular progreso (en realidad vendría del NetworkBootstrapper)
                progress = Mathf.MoveTowards(progress, 0.9f, Time.deltaTime * 0.3f);
                
                _loadingEnvironment.SetProgress(progress);
                OnLoadingProgress?.Invoke(progress);
                
                yield return null;
            }
            
            // Completar al 100%
            if (_loadingEnvironment != null)
            {
                _loadingEnvironment.SetProgress(1f);
                OnLoadingProgress?.Invoke(1f);
            }
        }
        #endregion

        #region VR Utilities
        private IEnumerator FadeOut()
        {
            if (_vrFadeCanvas != null)
            {
                _vrFadeCanvas.blocksRaycasts = true;
                _vrFadeCanvas.DOFade(1f, fadeOutDuration);
                yield return new WaitForSeconds(fadeOutDuration);
            }
        }

        private IEnumerator FadeIn()
        {
            if (_vrFadeCanvas != null)
            {
                _vrFadeCanvas.DOFade(0f, fadeInDuration);
                yield return new WaitForSeconds(fadeInDuration);
                _vrFadeCanvas.blocksRaycasts = false;
            }
        }

        private void ShowVRNotification(string message, float duration = 2f)
        {
            // TODO: Implementar notificaciones 3D en VR
            Debug.Log($"[VR Notification] {message}");
        }
        #endregion

        #region Scene Management
        private IEnumerator LoadSceneAsync(string sceneName)
        {
            Debug.Log($"[GameCore] Loading scene: {sceneName}");
            
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
            
            while (!loadOperation.isDone)
            {
                float progress = Mathf.Clamp01(loadOperation.progress / 0.9f);
                OnLoadingProgress?.Invoke(progress);
                yield return null;
            }
            
            Debug.Log($"[GameCore] Scene loaded: {sceneName}");
        }
        
        /// <summary>
        /// Llamado cuando un CLIENTE recibe notificación de cambio de escena
        /// </summary>
        public void OnClientSceneChangeStarted()
        {
            Debug.Log("[GameCore] 📱 CLIENT: Scene change detected");
    
            if (_currentState == GameState.InLobby)
            {
                // El cliente también debe transicionar a LoadingMatch
                TransitionToState(GameState.LoadingMatch);
            }
        }
        #endregion

        #region Helper Methods
        private void SaveLobbyData()
        {
            // Guardar datos relevantes del lobby antes de salir
            if (_playerDataManager != null && LobbyState.Instance != null)
            {
                _playerDataManager.UpdateSessionPlayers(LobbyState.Instance);
            }
        }

        private void SaveMatchResults(MatchResult result)
        {
            // TODO: Implementar guardado de resultados
            Debug.Log($"[GameCore] Match results saved");
        }

        private void CleanupGameplaySystems()
        {
            // TODO: Limpiar sistemas de gameplay
            Debug.Log("[GameCore] Gameplay systems cleaned up");
        }
        #endregion

        #region Debug
        [ContextMenu("Debug: Print Current State")]
        private void DebugPrintState()
        {
            Debug.Log($"=== GameCore State ===");
            Debug.Log($"Current: {_currentState}");
            Debug.Log($"Previous: {_previousState}");
            Debug.Log($"Is In Game: {IsInGame}");
            Debug.Log($"Is Loading: {IsLoading}");
            Debug.Log($"====================");
        }

        [ContextMenu("Test: Start Match")]
        private void DebugStartMatch()
        {
            _ = StartMatch("TestMap", 4);
        }

        [ContextMenu("Test: End Match")]
        private void DebugEndMatch()
        {
            EndMatch();
        }

        [ContextMenu("Test: Disconnect")]
        private void DebugDisconnect()
        {
            OnNetworkDisconnected();
        }
        #endregion
    }

    /// <summary>
    /// Clase para resultados de partida
    /// </summary>
    [Serializable]
    public class MatchResult
    {
        public string winnerName;
        public float matchDuration;
        public Dictionary<PlayerRef, int> playerScores;
        // Añadir más datos según tu juego
    }
}