using UnityEngine;
using System;
using System.Collections.Generic;
using Fusion;
using Cysharp.Threading.Tasks;

namespace HackMonkeys.Core
{
    /// <summary>
    /// PlayerDataManager mejorado con validación de datos
    /// Garantiza que siempre haya un nombre disponible
    /// </summary>
    public class PlayerDataManager : MonoBehaviour
    {
        #region Constants
        private static class Keys
        {
            public const string PLAYER_NAME = "HM_PlayerName";
            public const string PLAYER_COLOR_R = "HM_PlayerColorR";
            public const string PLAYER_COLOR_G = "HM_PlayerColorG";
            public const string PLAYER_COLOR_B = "HM_PlayerColorB";
        }
        #endregion

        #region Session Data
        [Header("Session Data")]
        [SerializeField] private bool _isHost = false;
        [SerializeField] private PlayerRef _localPlayerRef;
        [SerializeField] private string _currentRoomName;
        [SerializeField] private string _selectedMap;

        private Dictionary<PlayerRef, SessionPlayerData> _sessionPlayers = new Dictionary<PlayerRef, SessionPlayerData>();

        [Serializable]
        public class SessionPlayerData
        {
            public string Name;
            public Color Color;
            public bool IsHost;
            public bool IsReady;
        }
        #endregion

        #region Singleton
        public static PlayerDataManager Instance { get; private set; }
        #endregion

        #region Inspector Fields
        [Header("Debug Override - Leave empty to use saved values")] 
        [SerializeField] private string overridePlayerName = "";
        [SerializeField] private bool useRandomNameIfEmpty = true;
        [SerializeField] private Color overridePlayerColor = Color.clear;
        #endregion

        #region Private Fields
        [SerializeField] private string _playerName;
        private Color _playerColor;
        private bool _hasLoadedData = false;
        private bool _isInitializing = false;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Inicializar datos inmediatamente
            InitializeDataAsync().Forget();
        }
        #endregion

        #region Async Initialization
        /// <summary>
        /// Inicialización asíncrona de datos del jugador
        /// </summary>
        private async UniTaskVoid InitializeDataAsync()
        {
            if (_isInitializing) return;
            
            _isInitializing = true;
            
            // Pequeño delay para asegurar que todo esté listo
            await UniTask.Delay(100);
            
            LoadOrCreateData();
            
            _isInitializing = false;
            
            Debug.Log($"[PlayerDataManager] ✅ Initialized - Name: {_playerName}, Color: {ColorUtility.ToHtmlStringRGB(_playerColor)}");
        }

        private void LoadOrCreateData()
        {
            // Cargar o generar nombre
            if (!string.IsNullOrEmpty(overridePlayerName))
            {
                _playerName = overridePlayerName;
                Debug.Log($"[PlayerDataManager] Using override name: {_playerName}");
            }
            else if (PlayerPrefs.HasKey(Keys.PLAYER_NAME))
            {
                _playerName = PlayerPrefs.GetString(Keys.PLAYER_NAME);
                
                // Validar que no esté vacío
                if (string.IsNullOrEmpty(_playerName))
                {
                    _playerName = GenerateRandomName();
                    SavePlayerName();
                }
                
                Debug.Log($"[PlayerDataManager] Loaded saved name: {_playerName}");
            }
            else
            {
                _playerName = GenerateRandomName();
                SavePlayerName();
                Debug.Log($"[PlayerDataManager] Generated new name: {_playerName}");
            }
            
            // Cargar o generar color
            if (overridePlayerColor != Color.clear && overridePlayerColor.a > 0)
            {
                _playerColor = overridePlayerColor;
                Debug.Log($"[PlayerDataManager] Using override color");
            }
            else if (PlayerPrefs.HasKey(Keys.PLAYER_COLOR_R))
            {
                float r = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_R);
                float g = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_G);
                float b = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_B);
                _playerColor = new Color(r, g, b);
                Debug.Log($"[PlayerDataManager] Loaded saved color");
            }
            else
            {
                _playerColor = GenerateRandomColor();
                SavePlayerColor();
                Debug.Log($"[PlayerDataManager] Generated new color");
            }

            _hasLoadedData = true;
        }

        private string GenerateRandomName()
        {
            if (useRandomNameIfEmpty)
            {
                int timestamp = DateTime.Now.Second * 1000 + DateTime.Now.Millisecond;
                return $"Player_{timestamp}_{UnityEngine.Random.Range(10, 99)}";
            }
            else
            {
                return $"Player_{UnityEngine.Random.Range(1000, 9999)}";
            }
        }

        private Color GenerateRandomColor()
        {
            float hue = UnityEngine.Random.Range(0f, 1f);
            float saturation = UnityEngine.Random.Range(0.6f, 1f);
            float value = UnityEngine.Random.Range(0.7f, 1f);
            return Color.HSVToRGB(hue, saturation, value);
        }
        #endregion

        #region Public API - Getters
        /// <summary>
        /// Obtiene el nombre del jugador, garantizando que nunca sea null o vacío
        /// </summary>
        public string GetPlayerName()
        {
            // Si aún no se han cargado los datos, cargarlos síncronamente
            if (!_hasLoadedData)
            {
                LoadOrCreateData();
            }
            
            // Validación adicional - NUNCA retornar vacío
            if (string.IsNullOrEmpty(_playerName))
            {
                _playerName = GenerateRandomName();
                SavePlayerName();
                Debug.LogWarning($"[PlayerDataManager] Name was empty, generated: {_playerName}");
            }
            
            return _playerName;
        }

        public Color GetPlayerColor()
        {
            if (!_hasLoadedData)
            {
                LoadOrCreateData();
            }
            
            // Si el color es transparente o negro, generar uno nuevo
            if (_playerColor.a <= 0 || (_playerColor.r <= 0 && _playerColor.g <= 0 && _playerColor.b <= 0))
            {
                _playerColor = GenerateRandomColor();
                SavePlayerColor();
                Debug.LogWarning("[PlayerDataManager] Color was invalid, generated new one");
            }
            
            return _playerColor;
        }

        /// <summary>
        /// Espera hasta que los datos estén listos
        /// </summary>
        public async UniTask<bool> WaitForDataReady()
        {
            int attempts = 0;
            while (!_hasLoadedData && attempts < 50) // Max 5 segundos
            {
                await UniTask.Delay(100);
                attempts++;
            }
            
            if (!_hasLoadedData)
            {
                Debug.LogError("[PlayerDataManager] Data not ready after timeout!");
                LoadOrCreateData(); // Forzar carga
            }
            
            return _hasLoadedData;
        }
        #endregion

        #region Public API - Setters
        public void SetPlayerName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            _playerName = name;
            SavePlayerName();
            Debug.Log($"[PlayerDataManager] Name updated to: {_playerName}");
        }

        public void SetPlayerColor(Color color)
        {
            _playerColor = color;
            SavePlayerColor();
            Debug.Log($"[PlayerDataManager] Color updated");
        }
        #endregion

        #region Session API
        public void SetSessionData(PlayerRef localRef, bool isHost, string roomName)
        {
            _localPlayerRef = localRef;
            _isHost = isHost;
            _currentRoomName = roomName;
            _sessionPlayers = new Dictionary<PlayerRef, SessionPlayerData>();

            Debug.Log($"[PlayerDataManager] Session started - Host: {isHost}, Room: {roomName}, PlayerRef: {localRef}");
        }

        public void UpdateSessionPlayers(LobbyState lobbyState)
        {
            if (lobbyState == null) return;
            
            _sessionPlayers.Clear();

            foreach (var kvp in lobbyState.Players)
            {
                _sessionPlayers[kvp.Key] = new SessionPlayerData
                {
                    Name = kvp.Value.PlayerName.ToString(),
                    Color = kvp.Value.PlayerColor,
                    IsHost = kvp.Value.IsHost,
                    IsReady = kvp.Value.IsReady
                };
            }

            _selectedMap = lobbyState.GetSelectedMap();

            Debug.Log($"[PlayerDataManager] Updated {_sessionPlayers.Count} players in session");
        }

        public void ClearSessionData()
        {
            _isHost = false;
            _localPlayerRef = default;
            _currentRoomName = null;
            _selectedMap = null;
            _sessionPlayers?.Clear();

            Debug.Log("[PlayerDataManager] Session data cleared");
        }

        public bool IsHost => _isHost;
        public PlayerRef LocalPlayerRef => _localPlayerRef;
        public string CurrentRoomName => _currentRoomName;
        public string SelectedMap => _selectedMap;

        public SessionPlayerData GetSessionPlayer(PlayerRef playerRef)
        {
            return _sessionPlayers?.TryGetValue(playerRef, out var data) == true ? data : null;
        }

        public Dictionary<PlayerRef, SessionPlayerData> GetAllSessionPlayers()
        {
            return new Dictionary<PlayerRef, SessionPlayerData>(_sessionPlayers ?? new Dictionary<PlayerRef, SessionPlayerData>());
        }

        public void UpdateLocalPlayerRef(PlayerRef playerRef)
        {
            _localPlayerRef = playerRef;
            Debug.Log($"[PlayerDataManager] Updated LocalPlayerRef: {playerRef}");
        }

        public void UpdateSessionInfo(string selectedMap, string roomName = null)
        {
            _selectedMap = selectedMap;
            if (!string.IsNullOrEmpty(roomName))
                _currentRoomName = roomName;
            
            Debug.Log($"[PlayerDataManager] Updated session info - Map: {selectedMap}");
        }

        public void SetSelectedMap(string mapName)
        {
            _selectedMap = mapName;
            Debug.Log($"[PlayerDataManager] Selected map: {mapName}");
        }

        public void UpdateSelectedMapFromLobbyPlayer()
        {
            if (LobbyState.Instance != null)
            {
                var hostPlayer = LobbyState.Instance.HostPlayer;
                if (hostPlayer != null)
                {
                    _selectedMap = hostPlayer.SelectedMap.ToString();
                    Debug.Log($"[PlayerDataManager] Updated map from host player: {_selectedMap}");
                }
            }
        }
        #endregion

        #region Save Methods
        private void SavePlayerName()
        {
            PlayerPrefs.SetString(Keys.PLAYER_NAME, _playerName);
            PlayerPrefs.Save();
        }

        private void SavePlayerColor()
        {
            PlayerPrefs.SetFloat(Keys.PLAYER_COLOR_R, _playerColor.r);
            PlayerPrefs.SetFloat(Keys.PLAYER_COLOR_G, _playerColor.g);
            PlayerPrefs.SetFloat(Keys.PLAYER_COLOR_B, _playerColor.b);
            PlayerPrefs.Save();
        }
        #endregion

        #region Utility Methods
        public void ForceRandomName()
        {
            _playerName = GenerateRandomName();
            SavePlayerName();
            Debug.Log($"[PlayerDataManager] Forced new random name: {_playerName}");
        }

        public void ForceRandomColor()
        {
            _playerColor = GenerateRandomColor();
            SavePlayerColor();
            Debug.Log($"[PlayerDataManager] Forced new random color");
        }

        public void ClearAllData()
        {
            PlayerPrefs.DeleteKey(Keys.PLAYER_NAME);
            PlayerPrefs.DeleteKey(Keys.PLAYER_COLOR_R);
            PlayerPrefs.DeleteKey(Keys.PLAYER_COLOR_G);
            PlayerPrefs.DeleteKey(Keys.PLAYER_COLOR_B);
            PlayerPrefs.Save();

            LoadOrCreateData();
            Debug.Log("[PlayerDataManager] All data cleared and regenerated");
        }
        #endregion

        #region Debug Methods
        [ContextMenu("Debug: Print Current Settings")]
        private void DebugPrintSettings()
        {
            Debug.Log("=== PlayerDataManager ===");
            Debug.Log($"Player Name: {_playerName}");
            Debug.Log($"Player Color: #{ColorUtility.ToHtmlStringRGB(_playerColor)}");
            Debug.Log($"Has Loaded: {_hasLoadedData}");
            Debug.Log($"Is Host: {_isHost}");
            Debug.Log($"Local PlayerRef: {_localPlayerRef}");
            Debug.Log($"Current Room: {_currentRoomName}");
            Debug.Log("=========================");
        }
        #endregion
    }
}