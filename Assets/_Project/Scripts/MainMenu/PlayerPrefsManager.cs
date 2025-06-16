using UnityEngine;
using System;
using System.Collections.Generic;
using Fusion;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Sistema centralizado de gestión de datos persistentes del jugador
    /// Optimizado para VR con sincronización de red opcional
    /// </summary>
    public class PlayerPrefsManager : MonoBehaviour
    {
        #region Constants
        
        // Claves de preferencias organizadas por categoría
        private static class Keys
        {
            // Perfil del jugador
            public const string PLAYER_ID = "HM_PlayerId";
            public const string PLAYER_NAME = "HM_PlayerName";
            public const string AVATAR_CONFIG = "HM_AvatarConfig";
            public const string PLAYER_COLOR_R = "HM_PlayerColorR";
            public const string PLAYER_COLOR_G = "HM_PlayerColorG";
            public const string PLAYER_COLOR_B = "HM_PlayerColorB";
            
            // Progresión
            public const string TOTAL_PLAYTIME = "HM_TotalPlaytime";
            public const string MATCHES_PLAYED = "HM_MatchesPlayed";
            public const string MATCHES_WON = "HM_MatchesWon";
            public const string BEST_SCORE = "HM_BestScore";
            
            // Audio
            public const string VOLUME_MASTER = "HM_VolumeMaster";
            public const string VOLUME_MUSIC = "HM_VolumeMusic";
            public const string VOLUME_SFX = "HM_VolumeSFX";
            public const string VOLUME_VOICE = "HM_VolumeVoice";
            public const string VOICE_CHAT_ENABLED = "HM_VoiceChatEnabled";
            public const string PUSH_TO_TALK = "HM_PushToTalk";
            
            // VR Settings
            public const string HAPTIC_INTENSITY = "HM_HapticIntensity";
            public const string MOVEMENT_TYPE = "HM_MovementType";
            public const string TURN_TYPE = "HM_TurnType";
            public const string TURN_SPEED = "HM_TurnSpeed";
            public const string COMFORT_MODE = "HM_ComfortMode";
            public const string DOMINANT_HAND = "HM_DominantHand";
            public const string PLAYER_HEIGHT = "HM_PlayerHeight";
            public const string IPD_OFFSET = "HM_IPDOffset";
            
            // Accessibility
            public const string SUBTITLES_ENABLED = "HM_SubtitlesEnabled";
            public const string COLORBLIND_MODE = "HM_ColorblindMode";
            public const string UI_SCALE = "HM_UIScale";
            public const string CONTRAST_MODE = "HM_ContrastMode";
            
            // Network
            public const string PREFERRED_REGION = "HM_PreferredRegion";
            public const string AUTO_RECONNECT = "HM_AutoReconnect";
            public const string SHOW_PING = "HM_ShowPing";
            
            // Tutorial & Onboarding
            public const string FIRST_TIME_SETUP = "HM_FirstTimeSetup";
            public const string TUTORIAL_COMPLETED = "HM_TutorialCompleted";
            public const string TOOLTIPS_ENABLED = "HM_TooltipsEnabled";
            
            // Privacy
            public const string ANALYTICS_ENABLED = "HM_AnalyticsEnabled";
            public const string SHOW_ONLINE_STATUS = "HM_ShowOnlineStatus";
        }
        
        // Valores por defecto
        private static class Defaults
        {
            public const float VOLUME = 0.8f;
            public const float HAPTIC = 0.5f;
            public const float TURN_SPEED = 90f;
            public const float UI_SCALE = 1f;
            public const float PLAYER_HEIGHT = 1.75f;
            public const string REGION = "sa";
        }
        
        #endregion
        
        #region Singleton & Properties
        
        public static PlayerPrefsManager Instance { get; private set; }
        
        // Cache de datos para evitar lecturas frecuentes de PlayerPrefs
        private PlayerData _cachedData;
        private bool _isDirty = false;
        private float _saveTimer = 0f;
        private const float SAVE_INTERVAL = 5f; // Guardar cada 5 segundos si hay cambios
        
        #endregion
        
        #region Events
        
        public static event Action<PlayerData> OnPlayerDataChanged;
        public static event Action<string> OnPlayerNameChanged;
        public static event Action<Color> OnPlayerColorChanged;
        public static event Action<AudioSettings> OnAudioSettingsChanged;
        public static event Action<VRSettings> OnVRSettingsChanged;
        public static event Action<AccessibilitySettings> OnAccessibilityChanged;
        
        #endregion
        
        #region Data Structures
        
        [Serializable]
        public class PlayerData
        {
            public string playerId;
            public string playerName;
            public Color playerColor;
            public string avatarConfig;
            public PlayerStats stats;
            public AudioSettings audio;
            public VRSettings vr;
            public AccessibilitySettings accessibility;
            public NetworkSettings network;
            public PrivacySettings privacy;
            
            public PlayerData()
            {
                playerId = Guid.NewGuid().ToString();
                playerName = $"HackMonkey_{UnityEngine.Random.Range(1000, 9999)}";
                playerColor = new Color(
                    UnityEngine.Random.Range(0.3f, 0.9f),
                    UnityEngine.Random.Range(0.3f, 0.9f),
                    UnityEngine.Random.Range(0.3f, 0.9f)
                );
                avatarConfig = "";
                stats = new PlayerStats();
                audio = new AudioSettings();
                vr = new VRSettings();
                accessibility = new AccessibilitySettings();
                network = new NetworkSettings();
                privacy = new PrivacySettings();
            }
        }
        
        [Serializable]
        public class PlayerStats
        {
            public float totalPlaytime;
            public int matchesPlayed;
            public int matchesWon;
            public int bestScore;
            
            public float WinRate => matchesPlayed > 0 ? (float)matchesWon / matchesPlayed : 0f;
            public string FormattedPlaytime => FormatPlaytime(totalPlaytime);
            
            private string FormatPlaytime(float seconds)
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds);
                return $"{(int)time.TotalHours}h {time.Minutes}m";
            }
        }
        
        [Serializable]
        public class AudioSettings
        {
            public float masterVolume = Defaults.VOLUME;
            public float musicVolume = Defaults.VOLUME;
            public float sfxVolume = Defaults.VOLUME;
            public float voiceVolume = Defaults.VOLUME;
            public bool voiceChatEnabled = true;
            public bool pushToTalk = false;
            
            public float GetEffectiveVolume(VolumeType type)
            {
                float typeVolume = type switch
                {
                    VolumeType.Music => musicVolume,
                    VolumeType.SFX => sfxVolume,
                    VolumeType.Voice => voiceVolume,
                    _ => 1f
                };
                return masterVolume * typeVolume;
            }
        }
        
        [Serializable]
        public class VRSettings
        {
            public float hapticIntensity = Defaults.HAPTIC;
            public MovementType movementType = MovementType.Teleport;
            public TurnType turnType = TurnType.Snap;
            public float turnSpeed = Defaults.TURN_SPEED;
            public bool comfortMode = true;
            public HandDominance dominantHand = HandDominance.Right;
            public float playerHeight = Defaults.PLAYER_HEIGHT;
            public float ipdOffset = 0f;
        }
        
        [Serializable]
        public class AccessibilitySettings
        {
            public bool subtitlesEnabled = false;
            public ColorblindMode colorblindMode = ColorblindMode.None;
            public float uiScale = Defaults.UI_SCALE;
            public bool highContrast = false;
            public bool tooltipsEnabled = true;
        }
        
        [Serializable]
        public class NetworkSettings
        {
            public string preferredRegion = Defaults.REGION;
            public bool autoReconnect = true;
            public bool showPing = true;
        }
        
        [Serializable]
        public class PrivacySettings
        {
            public bool analyticsEnabled = true;
            public bool showOnlineStatus = true;
        }
        
        #endregion
        
        #region Enums
        
        public enum VolumeType
        {
            Master,
            Music,
            SFX,
            Voice
        }
        
        public enum MovementType
        {
            Teleport = 0,
            Smooth = 1,
            Hybrid = 2
        }
        
        public enum TurnType
        {
            Snap = 0,
            Smooth = 1
        }
        
        public enum HandDominance
        {
            Right = 0,
            Left = 1
        }
        
        public enum ColorblindMode
        {
            None = 0,
            Protanopia = 1,
            Deuteranopia = 2,
            Tritanopia = 3
        }
        
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
            
            LoadOrCreatePlayerData();
        }
        
        private void Update()
        {
            // Auto-save sistema
            if (_isDirty)
            {
                _saveTimer += Time.deltaTime;
                if (_saveTimer >= SAVE_INTERVAL)
                {
                    SaveAllData();
                    _saveTimer = 0f;
                }
            }
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _isDirty)
            {
                SaveAllData();
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _isDirty)
            {
                SaveAllData();
            }
        }
        
        private void OnDestroy()
        {
            if (_isDirty)
            {
                SaveAllData();
            }
        }
        
        #endregion
        
        #region Data Management
        
        private void LoadOrCreatePlayerData()
        {
            _cachedData = new PlayerData();
            
            // Verificar si es primera vez
            if (!PlayerPrefs.HasKey(Keys.FIRST_TIME_SETUP))
            {
                CreateDefaultData();
                PlayerPrefs.SetInt(Keys.FIRST_TIME_SETUP, 1);
                SaveAllData();
            }
            else
            {
                LoadAllData();
            }
        }
        
        private void CreateDefaultData()
        {
            // Los valores por defecto ya están en el constructor de PlayerData
            _cachedData = new PlayerData();
            
            // Generar ID único si no existe
            if (!PlayerPrefs.HasKey(Keys.PLAYER_ID))
            {
                _cachedData.playerId = Guid.NewGuid().ToString();
            }
        }
        
        private void LoadAllData()
        {
            // Cargar perfil
            _cachedData.playerId = PlayerPrefs.GetString(Keys.PLAYER_ID, _cachedData.playerId);
            _cachedData.playerName = PlayerPrefs.GetString(Keys.PLAYER_NAME, _cachedData.playerName);
            _cachedData.avatarConfig = PlayerPrefs.GetString(Keys.AVATAR_CONFIG, "");
            
            // Cargar color
            float r = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_R, _cachedData.playerColor.r);
            float g = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_G, _cachedData.playerColor.g);
            float b = PlayerPrefs.GetFloat(Keys.PLAYER_COLOR_B, _cachedData.playerColor.b);
            _cachedData.playerColor = new Color(r, g, b);
            
            // Cargar estadísticas
            _cachedData.stats.totalPlaytime = PlayerPrefs.GetFloat(Keys.TOTAL_PLAYTIME, 0f);
            _cachedData.stats.matchesPlayed = PlayerPrefs.GetInt(Keys.MATCHES_PLAYED, 0);
            _cachedData.stats.matchesWon = PlayerPrefs.GetInt(Keys.MATCHES_WON, 0);
            _cachedData.stats.bestScore = PlayerPrefs.GetInt(Keys.BEST_SCORE, 0);
            
            // Cargar audio
            _cachedData.audio.masterVolume = PlayerPrefs.GetFloat(Keys.VOLUME_MASTER, Defaults.VOLUME);
            _cachedData.audio.musicVolume = PlayerPrefs.GetFloat(Keys.VOLUME_MUSIC, Defaults.VOLUME);
            _cachedData.audio.sfxVolume = PlayerPrefs.GetFloat(Keys.VOLUME_SFX, Defaults.VOLUME);
            _cachedData.audio.voiceVolume = PlayerPrefs.GetFloat(Keys.VOLUME_VOICE, Defaults.VOLUME);
            _cachedData.audio.voiceChatEnabled = PlayerPrefs.GetInt(Keys.VOICE_CHAT_ENABLED, 1) == 1;
            _cachedData.audio.pushToTalk = PlayerPrefs.GetInt(Keys.PUSH_TO_TALK, 0) == 1;
            
            // Cargar VR settings
            _cachedData.vr.hapticIntensity = PlayerPrefs.GetFloat(Keys.HAPTIC_INTENSITY, Defaults.HAPTIC);
            _cachedData.vr.movementType = (MovementType)PlayerPrefs.GetInt(Keys.MOVEMENT_TYPE, 0);
            _cachedData.vr.turnType = (TurnType)PlayerPrefs.GetInt(Keys.TURN_TYPE, 0);
            _cachedData.vr.turnSpeed = PlayerPrefs.GetFloat(Keys.TURN_SPEED, Defaults.TURN_SPEED);
            _cachedData.vr.comfortMode = PlayerPrefs.GetInt(Keys.COMFORT_MODE, 1) == 1;
            _cachedData.vr.dominantHand = (HandDominance)PlayerPrefs.GetInt(Keys.DOMINANT_HAND, 0);
            _cachedData.vr.playerHeight = PlayerPrefs.GetFloat(Keys.PLAYER_HEIGHT, Defaults.PLAYER_HEIGHT);
            _cachedData.vr.ipdOffset = PlayerPrefs.GetFloat(Keys.IPD_OFFSET, 0f);
            
            // Cargar accesibilidad
            _cachedData.accessibility.subtitlesEnabled = PlayerPrefs.GetInt(Keys.SUBTITLES_ENABLED, 0) == 1;
            _cachedData.accessibility.colorblindMode = (ColorblindMode)PlayerPrefs.GetInt(Keys.COLORBLIND_MODE, 0);
            _cachedData.accessibility.uiScale = PlayerPrefs.GetFloat(Keys.UI_SCALE, Defaults.UI_SCALE);
            _cachedData.accessibility.highContrast = PlayerPrefs.GetInt(Keys.CONTRAST_MODE, 0) == 1;
            _cachedData.accessibility.tooltipsEnabled = PlayerPrefs.GetInt(Keys.TOOLTIPS_ENABLED, 1) == 1;
            
            // Cargar network
            _cachedData.network.preferredRegion = PlayerPrefs.GetString(Keys.PREFERRED_REGION, Defaults.REGION);
            _cachedData.network.autoReconnect = PlayerPrefs.GetInt(Keys.AUTO_RECONNECT, 1) == 1;
            _cachedData.network.showPing = PlayerPrefs.GetInt(Keys.SHOW_PING, 1) == 1;
            
            // Cargar privacidad
            _cachedData.privacy.analyticsEnabled = PlayerPrefs.GetInt(Keys.ANALYTICS_ENABLED, 1) == 1;
            _cachedData.privacy.showOnlineStatus = PlayerPrefs.GetInt(Keys.SHOW_ONLINE_STATUS, 1) == 1;
        }
        
        private void SaveAllData()
        {
            // Guardar perfil
            PlayerPrefs.SetString(Keys.PLAYER_ID, _cachedData.playerId);
            PlayerPrefs.SetString(Keys.PLAYER_NAME, _cachedData.playerName);
            PlayerPrefs.SetString(Keys.AVATAR_CONFIG, _cachedData.avatarConfig);
            
            // Guardar color
            PlayerPrefs.SetFloat(Keys.PLAYER_COLOR_R, _cachedData.playerColor.r);
            PlayerPrefs.SetFloat(Keys.PLAYER_COLOR_G, _cachedData.playerColor.g);
            PlayerPrefs.SetFloat(Keys.PLAYER_COLOR_B, _cachedData.playerColor.b);
            
            // Guardar estadísticas
            PlayerPrefs.SetFloat(Keys.TOTAL_PLAYTIME, _cachedData.stats.totalPlaytime);
            PlayerPrefs.SetInt(Keys.MATCHES_PLAYED, _cachedData.stats.matchesPlayed);
            PlayerPrefs.SetInt(Keys.MATCHES_WON, _cachedData.stats.matchesWon);
            PlayerPrefs.SetInt(Keys.BEST_SCORE, _cachedData.stats.bestScore);
            
            // Guardar audio
            PlayerPrefs.SetFloat(Keys.VOLUME_MASTER, _cachedData.audio.masterVolume);
            PlayerPrefs.SetFloat(Keys.VOLUME_MUSIC, _cachedData.audio.musicVolume);
            PlayerPrefs.SetFloat(Keys.VOLUME_SFX, _cachedData.audio.sfxVolume);
            PlayerPrefs.SetFloat(Keys.VOLUME_VOICE, _cachedData.audio.voiceVolume);
            PlayerPrefs.SetInt(Keys.VOICE_CHAT_ENABLED, _cachedData.audio.voiceChatEnabled ? 1 : 0);
            PlayerPrefs.SetInt(Keys.PUSH_TO_TALK, _cachedData.audio.pushToTalk ? 1 : 0);
            
            // Guardar VR settings
            PlayerPrefs.SetFloat(Keys.HAPTIC_INTENSITY, _cachedData.vr.hapticIntensity);
            PlayerPrefs.SetInt(Keys.MOVEMENT_TYPE, (int)_cachedData.vr.movementType);
            PlayerPrefs.SetInt(Keys.TURN_TYPE, (int)_cachedData.vr.turnType);
            PlayerPrefs.SetFloat(Keys.TURN_SPEED, _cachedData.vr.turnSpeed);
            PlayerPrefs.SetInt(Keys.COMFORT_MODE, _cachedData.vr.comfortMode ? 1 : 0);
            PlayerPrefs.SetInt(Keys.DOMINANT_HAND, (int)_cachedData.vr.dominantHand);
            PlayerPrefs.SetFloat(Keys.PLAYER_HEIGHT, _cachedData.vr.playerHeight);
            PlayerPrefs.SetFloat(Keys.IPD_OFFSET, _cachedData.vr.ipdOffset);
            
            // Guardar accesibilidad
            PlayerPrefs.SetInt(Keys.SUBTITLES_ENABLED, _cachedData.accessibility.subtitlesEnabled ? 1 : 0);
            PlayerPrefs.SetInt(Keys.COLORBLIND_MODE, (int)_cachedData.accessibility.colorblindMode);
            PlayerPrefs.SetFloat(Keys.UI_SCALE, _cachedData.accessibility.uiScale);
            PlayerPrefs.SetInt(Keys.CONTRAST_MODE, _cachedData.accessibility.highContrast ? 1 : 0);
            PlayerPrefs.SetInt(Keys.TOOLTIPS_ENABLED, _cachedData.accessibility.tooltipsEnabled ? 1 : 0);
            
            // Guardar network
            PlayerPrefs.SetString(Keys.PREFERRED_REGION, _cachedData.network.preferredRegion);
            PlayerPrefs.SetInt(Keys.AUTO_RECONNECT, _cachedData.network.autoReconnect ? 1 : 0);
            PlayerPrefs.SetInt(Keys.SHOW_PING, _cachedData.network.showPing ? 1 : 0);
            
            // Guardar privacidad
            PlayerPrefs.SetInt(Keys.ANALYTICS_ENABLED, _cachedData.privacy.analyticsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(Keys.SHOW_ONLINE_STATUS, _cachedData.privacy.showOnlineStatus ? 1 : 0);
            
            PlayerPrefs.Save();
            _isDirty = false;
            
            Debug.Log("[PlayerPrefsManager] Data saved successfully");
        }
        
        #endregion
        
        #region Public API - Getters
        
        public PlayerData GetPlayerData() => _cachedData;
        public string GetPlayerId() => _cachedData.playerId;
        public string GetPlayerName() => _cachedData.playerName;
        public Color GetPlayerColor() => _cachedData.playerColor;
        public string GetAvatarConfig() => _cachedData.avatarConfig;
        public PlayerStats GetStats() => _cachedData.stats;
        public AudioSettings GetAudioSettings() => _cachedData.audio;
        public VRSettings GetVRSettings() => _cachedData.vr;
        public AccessibilitySettings GetAccessibilitySettings() => _cachedData.accessibility;
        public NetworkSettings GetNetworkSettings() => _cachedData.network;
        public PrivacySettings GetPrivacySettings() => _cachedData.privacy;
        
        public bool IsFirstTimeUser() => !PlayerPrefs.HasKey(Keys.TUTORIAL_COMPLETED);
        public bool HasCompletedTutorial() => PlayerPrefs.GetInt(Keys.TUTORIAL_COMPLETED, 0) == 1;
        
        #endregion
        
        #region Public API - Setters
        
        public void SetPlayerName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == _cachedData.playerName) return;
            
            _cachedData.playerName = name;
            _isDirty = true;
            
            OnPlayerNameChanged?.Invoke(name);
            OnPlayerDataChanged?.Invoke(_cachedData);
        }
        
        public void SetPlayerColor(Color color)
        {
            if (color == _cachedData.playerColor) return;
            
            _cachedData.playerColor = color;
            _isDirty = true;
            
            OnPlayerColorChanged?.Invoke(color);
            OnPlayerDataChanged?.Invoke(_cachedData);
        }
        
        public void SetAvatarConfig(string config)
        {
            if (config == _cachedData.avatarConfig) return;
            
            _cachedData.avatarConfig = config;
            _isDirty = true;
            
            OnPlayerDataChanged?.Invoke(_cachedData);
        }
        
        public void UpdateVolume(VolumeType type, float value)
        {
            value = Mathf.Clamp01(value);
            
            switch (type)
            {
                case VolumeType.Master:
                    _cachedData.audio.masterVolume = value;
                    break;
                case VolumeType.Music:
                    _cachedData.audio.musicVolume = value;
                    break;
                case VolumeType.SFX:
                    _cachedData.audio.sfxVolume = value;
                    break;
                case VolumeType.Voice:
                    _cachedData.audio.voiceVolume = value;
                    break;
            }
            
            _isDirty = true;
            OnAudioSettingsChanged?.Invoke(_cachedData.audio);
        }
        
        public void SetVoiceChatEnabled(bool enabled)
        {
            if (_cachedData.audio.voiceChatEnabled == enabled) return;
            
            _cachedData.audio.voiceChatEnabled = enabled;
            _isDirty = true;
            OnAudioSettingsChanged?.Invoke(_cachedData.audio);
        }
        
        public void SetPushToTalk(bool enabled)
        {
            if (_cachedData.audio.pushToTalk == enabled) return;
            
            _cachedData.audio.pushToTalk = enabled;
            _isDirty = true;
            OnAudioSettingsChanged?.Invoke(_cachedData.audio);
        }
        
        public void SetHapticIntensity(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (Mathf.Approximately(_cachedData.vr.hapticIntensity, intensity)) return;
            
            _cachedData.vr.hapticIntensity = intensity;
            _isDirty = true;
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
        }
        
        public void SetMovementType(MovementType type)
        {
            if (_cachedData.vr.movementType == type) return;
            
            _cachedData.vr.movementType = type;
            _isDirty = true;
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
        }
        
        public void SetTurnType(TurnType type)
        {
            if (_cachedData.vr.turnType == type) return;
            
            _cachedData.vr.turnType = type;
            _isDirty = true;
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
        }
        
        public void SetTurnSpeed(float speed)
        {
            speed = Mathf.Clamp(speed, 30f, 180f);
            if (Mathf.Approximately(_cachedData.vr.turnSpeed, speed)) return;
            
            _cachedData.vr.turnSpeed = speed;
            _isDirty = true;
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
        }
        
        public void SetComfortMode(bool enabled)
        {
            if (_cachedData.vr.comfortMode == enabled) return;
            
            _cachedData.vr.comfortMode = enabled;
            _isDirty = true;
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
        }
        
        public void SetDominantHand(HandDominance hand)
        {
            if (_cachedData.vr.dominantHand == hand) return;
            
            _cachedData.vr.dominantHand = hand;
            _isDirty = true;
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
        }
        
        public void SetPlayerHeight(float height)
        {
            height = Mathf.Clamp(height, 1f, 2.5f);
            if (Mathf.Approximately(_cachedData.vr.playerHeight, height)) return;
            
            _cachedData.vr.playerHeight = height;
            _isDirty = true;
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
        }
        
        public void SetIPDOffset(float offset)
        {
            offset = Mathf.Clamp(offset, -10f, 10f);
            if (Mathf.Approximately(_cachedData.vr.ipdOffset, offset)) return;
            
            _cachedData.vr.ipdOffset = offset;
            _isDirty = true;
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
        }
        
        public void SetSubtitlesEnabled(bool enabled)
        {
            if (_cachedData.accessibility.subtitlesEnabled == enabled) return;
            
            _cachedData.accessibility.subtitlesEnabled = enabled;
            _isDirty = true;
            OnAccessibilityChanged?.Invoke(_cachedData.accessibility);
        }
        
        public void SetColorblindMode(ColorblindMode mode)
        {
            if (_cachedData.accessibility.colorblindMode == mode) return;
            
            _cachedData.accessibility.colorblindMode = mode;
            _isDirty = true;
            OnAccessibilityChanged?.Invoke(_cachedData.accessibility);
        }
        
        public void SetUIScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.8f, 1.5f);
            if (Mathf.Approximately(_cachedData.accessibility.uiScale, scale)) return;
            
            _cachedData.accessibility.uiScale = scale;
            _isDirty = true;
            OnAccessibilityChanged?.Invoke(_cachedData.accessibility);
        }
        
        public void SetHighContrast(bool enabled)
        {
            if (_cachedData.accessibility.highContrast == enabled) return;
            
            _cachedData.accessibility.highContrast = enabled;
            _isDirty = true;
            OnAccessibilityChanged?.Invoke(_cachedData.accessibility);
        }
        
        public void SetTooltipsEnabled(bool enabled)
        {
            if (_cachedData.accessibility.tooltipsEnabled == enabled) return;
            
            _cachedData.accessibility.tooltipsEnabled = enabled;
            _isDirty = true;
            OnAccessibilityChanged?.Invoke(_cachedData.accessibility);
        }
        
        public void SetPreferredRegion(string region)
        {
            if (string.IsNullOrEmpty(region) || _cachedData.network.preferredRegion == region) return;
            
            _cachedData.network.preferredRegion = region;
            _isDirty = true;
        }
        
        public void SetAutoReconnect(bool enabled)
        {
            if (_cachedData.network.autoReconnect == enabled) return;
            
            _cachedData.network.autoReconnect = enabled;
            _isDirty = true;
        }
        
        public void SetShowPing(bool show)
        {
            if (_cachedData.network.showPing == show) return;
            
            _cachedData.network.showPing = show;
            _isDirty = true;
        }
        
        public void SetAnalyticsEnabled(bool enabled)
        {
            if (_cachedData.privacy.analyticsEnabled == enabled) return;
            
            _cachedData.privacy.analyticsEnabled = enabled;
            _isDirty = true;
        }
        
        public void SetShowOnlineStatus(bool show)
        {
            if (_cachedData.privacy.showOnlineStatus == show) return;
            
            _cachedData.privacy.showOnlineStatus = show;
            _isDirty = true;
        }
        
        public void SetTutorialCompleted(bool completed)
        {
            PlayerPrefs.SetInt(Keys.TUTORIAL_COMPLETED, completed ? 1 : 0);
            PlayerPrefs.Save();
        }
        
        #endregion
        
        #region Statistics Management
        
        public void AddPlaytime(float seconds)
        {
            _cachedData.stats.totalPlaytime += seconds;
            _isDirty = true;
        }
        
        public void RecordMatchResult(bool won, int score = 0)
        {
            _cachedData.stats.matchesPlayed++;
            
            if (won)
            {
                _cachedData.stats.matchesWon++;
            }
            
            if (score > _cachedData.stats.bestScore)
            {
                _cachedData.stats.bestScore = score;
            }
            
            _isDirty = true;
        }
        
        public void ResetStatistics()
        {
            _cachedData.stats = new PlayerStats();
            _isDirty = true;
        }
        
        #endregion
        
        #region Utility Methods
        
        public void SaveImmediately()
        {
            if (_isDirty)
            {
                SaveAllData();
                _saveTimer = 0f;
            }
        }
        
        public void ResetToDefaults()
        {
            // Preservar ID del jugador
            string playerId = _cachedData.playerId;
            
            // Crear datos por defecto
            _cachedData = new PlayerData();
            _cachedData.playerId = playerId;
            
            // Guardar inmediatamente
            SaveAllData();
            
            // Notificar cambios
            OnPlayerDataChanged?.Invoke(_cachedData);
            OnAudioSettingsChanged?.Invoke(_cachedData.audio);
            OnVRSettingsChanged?.Invoke(_cachedData.vr);
            OnAccessibilityChanged?.Invoke(_cachedData.accessibility);
            
            Debug.Log("[PlayerPrefsManager] Settings reset to defaults");
        }
        
        public void DeleteAllData()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            
            // Recrear datos por defecto
            CreateDefaultData();
            SaveAllData();
            
            Debug.Log("[PlayerPrefsManager] All data deleted and reset");
        }
        
        public Dictionary<string, object> ExportDataAsJson()
        {
            var data = new Dictionary<string, object>
            {
                ["playerId"] = _cachedData.playerId,
                ["playerName"] = _cachedData.playerName,
                ["playerColor"] = ColorToHex(_cachedData.playerColor),
                ["avatarConfig"] = _cachedData.avatarConfig,
                ["stats"] = new Dictionary<string, object>
                {
                    ["totalPlaytime"] = _cachedData.stats.totalPlaytime,
                    ["matchesPlayed"] = _cachedData.stats.matchesPlayed,
                    ["matchesWon"] = _cachedData.stats.matchesWon,
                    ["bestScore"] = _cachedData.stats.bestScore,
                    ["winRate"] = _cachedData.stats.WinRate
                },
                ["audio"] = new Dictionary<string, object>
                {
                    ["masterVolume"] = _cachedData.audio.masterVolume,
                    ["musicVolume"] = _cachedData.audio.musicVolume,
                    ["sfxVolume"] = _cachedData.audio.sfxVolume,
                    ["voiceVolume"] = _cachedData.audio.voiceVolume,
                    ["voiceChatEnabled"] = _cachedData.audio.voiceChatEnabled,
                    ["pushToTalk"] = _cachedData.audio.pushToTalk
                },
                ["vr"] = new Dictionary<string, object>
                {
                    ["hapticIntensity"] = _cachedData.vr.hapticIntensity,
                    ["movementType"] = _cachedData.vr.movementType.ToString(),
                    ["turnType"] = _cachedData.vr.turnType.ToString(),
                    ["turnSpeed"] = _cachedData.vr.turnSpeed,
                    ["comfortMode"] = _cachedData.vr.comfortMode,
                    ["dominantHand"] = _cachedData.vr.dominantHand.ToString(),
                    ["playerHeight"] = _cachedData.vr.playerHeight,
                    ["ipdOffset"] = _cachedData.vr.ipdOffset
                },
                ["accessibility"] = new Dictionary<string, object>
                {
                    ["subtitlesEnabled"] = _cachedData.accessibility.subtitlesEnabled,
                    ["colorblindMode"] = _cachedData.accessibility.colorblindMode.ToString(),
                    ["uiScale"] = _cachedData.accessibility.uiScale,
                    ["highContrast"] = _cachedData.accessibility.highContrast,
                    ["tooltipsEnabled"] = _cachedData.accessibility.tooltipsEnabled
                },
                ["network"] = new Dictionary<string, object>
                {
                    ["preferredRegion"] = _cachedData.network.preferredRegion,
                    ["autoReconnect"] = _cachedData.network.autoReconnect,
                    ["showPing"] = _cachedData.network.showPing
                },
                ["privacy"] = new Dictionary<string, object>
                {
                    ["analyticsEnabled"] = _cachedData.privacy.analyticsEnabled,
                    ["showOnlineStatus"] = _cachedData.privacy.showOnlineStatus
                }
            };
            
            return data;
        }
        
        private string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }
        
        #endregion
        
        #region Network Sync (Optional)
        
        /// <summary>
        /// Estructura para sincronizar datos básicos del jugador en red
        /// </summary>
        public struct NetworkPlayerData : INetworkStruct
        {
            public NetworkString<_32> playerName;
            public byte colorR;
            public byte colorG;
            public byte colorB;
            public int matchesPlayed;
            public int matchesWon;
            public NetworkBool voiceChatEnabled;
            
            public static NetworkPlayerData FromPlayerData(PlayerData data)
            {
                return new NetworkPlayerData
                {
                    playerName = data.playerName,
                    colorR = (byte)(data.playerColor.r * 255),
                    colorG = (byte)(data.playerColor.g * 255),
                    colorB = (byte)(data.playerColor.b * 255),
                    matchesPlayed = data.stats.matchesPlayed,
                    matchesWon = data.stats.matchesWon,
                    voiceChatEnabled = data.audio.voiceChatEnabled
                };
            }
            
            public Color GetColor()
            {
                return new Color(colorR / 255f, colorG / 255f, colorB / 255f);
            }
            
            public float GetWinRate()
            {
                return matchesPlayed > 0 ? (float)matchesWon / matchesPlayed : 0f;
            }
        }
        
        public NetworkPlayerData GetNetworkData()
        {
            return NetworkPlayerData.FromPlayerData(_cachedData);
        }
        
        #endregion
        
        #region Debug & Editor Tools
        
        [ContextMenu("Print Current Settings")]
        private void DebugPrintSettings()
        {
            Debug.Log("=== HackMonkeys Player Data ===");
            Debug.Log($"Player ID: {_cachedData.playerId}");
            Debug.Log($"Player Name: {_cachedData.playerName}");
            Debug.Log($"Player Color: {ColorToHex(_cachedData.playerColor)}");
            Debug.Log($"\n--- Statistics ---");
            Debug.Log($"Total Playtime: {_cachedData.stats.FormattedPlaytime}");
            Debug.Log($"Matches: {_cachedData.stats.matchesPlayed} (Won: {_cachedData.stats.matchesWon})");
            Debug.Log($"Win Rate: {_cachedData.stats.WinRate:P}");
            Debug.Log($"Best Score: {_cachedData.stats.bestScore}");
            Debug.Log($"\n--- Audio Settings ---");
            Debug.Log($"Master Volume: {_cachedData.audio.masterVolume:P}");
            Debug.Log($"Music Volume: {_cachedData.audio.musicVolume:P}");
            Debug.Log($"SFX Volume: {_cachedData.audio.sfxVolume:P}");
            Debug.Log($"Voice Volume: {_cachedData.audio.voiceVolume:P}");
            Debug.Log($"Voice Chat: {(_cachedData.audio.voiceChatEnabled ? "Enabled" : "Disabled")}");
            Debug.Log($"Push to Talk: {(_cachedData.audio.pushToTalk ? "Yes" : "No")}");
            Debug.Log($"\n--- VR Settings ---");
            Debug.Log($"Haptic Intensity: {_cachedData.vr.hapticIntensity:P}");
            Debug.Log($"Movement: {_cachedData.vr.movementType}");
            Debug.Log($"Turn: {_cachedData.vr.turnType} ({_cachedData.vr.turnSpeed}°/s)");
            Debug.Log($"Comfort Mode: {(_cachedData.vr.comfortMode ? "On" : "Off")}");
            Debug.Log($"Dominant Hand: {_cachedData.vr.dominantHand}");
            Debug.Log($"Player Height: {_cachedData.vr.playerHeight}m");
            Debug.Log($"IPD Offset: {_cachedData.vr.ipdOffset}mm");
            Debug.Log($"\n--- Accessibility ---");
            Debug.Log($"Subtitles: {(_cachedData.accessibility.subtitlesEnabled ? "On" : "Off")}");
            Debug.Log($"Colorblind Mode: {_cachedData.accessibility.colorblindMode}");
            Debug.Log($"UI Scale: {_cachedData.accessibility.uiScale:P}");
            Debug.Log($"High Contrast: {(_cachedData.accessibility.highContrast ? "On" : "Off")}");
            Debug.Log($"Tooltips: {(_cachedData.accessibility.tooltipsEnabled ? "On" : "Off")}");
            Debug.Log($"\n--- Network ---");
            Debug.Log($"Preferred Region: {_cachedData.network.preferredRegion}");
            Debug.Log($"Auto Reconnect: {(_cachedData.network.autoReconnect ? "Yes" : "No")}");
            Debug.Log($"Show Ping: {(_cachedData.network.showPing ? "Yes" : "No")}");
            Debug.Log($"\n--- Privacy ---");
            Debug.Log($"Analytics: {(_cachedData.privacy.analyticsEnabled ? "Enabled" : "Disabled")}");
            Debug.Log($"Online Status: {(_cachedData.privacy.showOnlineStatus ? "Visible" : "Hidden")}");
            Debug.Log("==============================");
        }
        
        [ContextMenu("Add Test Playtime (1 hour)")]
        private void DebugAddPlaytime()
        {
            AddPlaytime(3600f);
            Debug.Log($"Added 1 hour. Total: {_cachedData.stats.FormattedPlaytime}");
        }
        
        [ContextMenu("Simulate Match Win")]
        private void DebugSimulateWin()
        {
            RecordMatchResult(true, UnityEngine.Random.Range(100, 1000));
            Debug.Log($"Match recorded. Stats: {_cachedData.stats.matchesWon}/{_cachedData.stats.matchesPlayed} (Win Rate: {_cachedData.stats.WinRate:P})");
        }
        
        [ContextMenu("Force Save")]
        private void DebugForceSave()
        {
            SaveImmediately();
            Debug.Log("Data saved to disk");
        }
        
        #endregion
    }
}