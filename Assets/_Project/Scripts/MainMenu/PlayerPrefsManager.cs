using UnityEngine;
using System;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Versión SIMPLIFICADA de PlayerPrefsManager
    /// Solo mantiene lo esencial para el lobby multijugador
    /// </summary>
    public class PlayerPrefsManager : MonoBehaviour
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
        
        #region Singleton
        
        public static PlayerPrefsManager Instance { get; private set; }
        
        #endregion
        
        #region Inspector Fields - EXPUESTOS PARA DEBUG
        
        [Header("Debug Override - Leave empty to use saved values")]
        [SerializeField] private string overridePlayerName = "";
        [SerializeField] private bool useRandomNameIfEmpty = true;
        [SerializeField] private Color overridePlayerColor = Color.clear;
        
        #endregion
        
        #region Private Fields
        
        private string _playerName;
        private Color _playerColor;
        private bool _hasLoadedData = false;
        
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
            
            LoadOrCreateData();
        }
        
        #endregion
        
        #region Data Management
        
        private void LoadOrCreateData()
        {
            // NOMBRE DEL JUGADOR
            // Prioridad: 1) Override del inspector, 2) PlayerPrefs, 3) Aleatorio
            if (!string.IsNullOrEmpty(overridePlayerName))
            {
                _playerName = overridePlayerName;
                Debug.Log($"[PlayerPrefsManager] Using override name: {_playerName}");
            }
            else if (PlayerPrefs.HasKey(Keys.PLAYER_NAME))
            {
                _playerName = PlayerPrefs.GetString(Keys.PLAYER_NAME);
                Debug.Log($"[PlayerPrefsManager] Loaded saved name: {_playerName}");
            }
            else
            {
                _playerName = GenerateRandomName();
                Debug.Log($"[PlayerPrefsManager] Generated new name: {_playerName}");
                SavePlayerName();
            }
            
            // COLOR DEL JUGADOR
            // Prioridad: 1) Override del inspector, 2) PlayerPrefs, 3) Aleatorio
            if (overridePlayerColor != Color.clear && overridePlayerColor.a > 0)
            {
                _playerColor = overridePlayerColor;
                Debug.Log($"[PlayerPrefsManager] Using override color");
            }
            else if (PlayerPrefs.HasKey(Keys.PLAYER_COLOR_R))
            {
                float r = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_R);
                float g = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_G);
                float b = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_B);
                _playerColor = new Color(r, g, b);
                Debug.Log($"[PlayerPrefsManager] Loaded saved color");
            }
            else
            {
                _playerColor = GenerateRandomColor();
                Debug.Log($"[PlayerPrefsManager] Generated new color");
                SavePlayerColor();
            }
            
            _hasLoadedData = true;
        }
        
        private string GenerateRandomName()
        {
            // Para desarrollo: generar nombres únicos basados en tiempo + random
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
            // Generar colores vibrantes con buena saturación
            float hue = UnityEngine.Random.Range(0f, 1f);
            float saturation = UnityEngine.Random.Range(0.6f, 1f);
            float value = UnityEngine.Random.Range(0.7f, 1f);
            return Color.HSVToRGB(hue, saturation, value);
        }
        
        #endregion
        
        #region Public API - Getters
        
        public string GetPlayerName()
        {
            if (!_hasLoadedData) LoadOrCreateData();
            return _playerName;
        }
        
        public Color GetPlayerColor()
        {
            if (!_hasLoadedData) LoadOrCreateData();
            return _playerColor;
        }
        
        #endregion
        
        #region Public API - Setters
        
        public void SetPlayerName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            
            _playerName = name;
            SavePlayerName();
            Debug.Log($"[PlayerPrefsManager] Name updated to: {_playerName}");
        }
        
        public void SetPlayerColor(Color color)
        {
            _playerColor = color;
            SavePlayerColor();
            Debug.Log($"[PlayerPrefsManager] Color updated");
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
        
        /// <summary>
        /// Fuerza un nuevo nombre aleatorio (útil para testing)
        /// </summary>
        public void ForceRandomName()
        {
            _playerName = GenerateRandomName();
            SavePlayerName();
            Debug.Log($"[PlayerPrefsManager] Forced new random name: {_playerName}");
        }
        
        /// <summary>
        /// Fuerza un nuevo color aleatorio
        /// </summary>
        public void ForceRandomColor()
        {
            _playerColor = GenerateRandomColor();
            SavePlayerColor();
            Debug.Log($"[PlayerPrefsManager] Forced new random color");
        }
        
        /// <summary>
        /// Limpia todos los datos guardados
        /// </summary>
        public void ClearAllData()
        {
            PlayerPrefs.DeleteKey(Keys.PLAYER_NAME);
            PlayerPrefs.DeleteKey(Keys.PLAYER_COLOR_R);
            PlayerPrefs.DeleteKey(Keys.PLAYER_COLOR_G);
            PlayerPrefs.DeleteKey(Keys.PLAYER_COLOR_B);
            PlayerPrefs.Save();
            
            LoadOrCreateData();
            Debug.Log("[PlayerPrefsManager] All data cleared and regenerated");
        }
        
        #endregion
        
        #region Debug Methods
        
        [ContextMenu("Debug: Print Current Settings")]
        private void DebugPrintSettings()
        {
            Debug.Log("=== PlayerPrefsManager (Simplified) ===");
            Debug.Log($"Player Name: {_playerName}");
            Debug.Log($"Player Color: #{ColorUtility.ToHtmlStringRGB(_playerColor)}");
            Debug.Log($"Has Override Name: {!string.IsNullOrEmpty(overridePlayerName)}");
            Debug.Log($"Has Override Color: {overridePlayerColor != Color.clear}");
            Debug.Log("=====================================");
        }
        
        [ContextMenu("Debug: Force Random Name")]
        private void DebugForceRandomName()
        {
            ForceRandomName();
        }
        
        [ContextMenu("Debug: Force Random Color")]
        private void DebugForceRandomColor()
        {
            ForceRandomColor();
        }
        
        [ContextMenu("Debug: Clear All Data")]
        private void DebugClearData()
        {
            ClearAllData();
        }
        
        #endregion
    }
}