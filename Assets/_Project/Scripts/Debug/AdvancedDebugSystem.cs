using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using System.Runtime.CompilerServices;
using Fusion;

// IMPORTANTE: Este archivo usa UnityEngine.LogType para manejar logs de Unity
// No confundir con Fusion.LogType que es específico de Photon Fusion
namespace HackMonkeys.Debugging
{
    /// <summary>
    /// Categorías de logging usando bitmask para filtrado múltiple eficiente
    /// </summary>
    [Flags]
    public enum LogCategory
    {
        None = 0,
        General = 1 << 0,           // 1
        UI = 1 << 1,                // 2
        Networking = 1 << 2,        // 4
        MusicalNote = 1 << 3,       // 8
        FusionVrGrabbable = 1 << 4, // 16
        Physics = 1 << 5,           // 32
        Audio = 1 << 6,             // 64
        Performance = 1 << 7,       // 128
        VRInput = 1 << 8,           // 256
        StateManagement = 1 << 9,   // 512
        Photon = 1 << 10,           // 1024
        Animation = 1 << 11,        // 2048
        Avatar = 1 << 12,           // 4096
        Lobby = 1 << 13,            // 8192
        Gameplay = 1 << 14,         // 16384
        Puzzle = 1 << 15,           // 32768
        SceneManagement = 1 << 16,  // 65536
        Flashlight = 1 << 17,       // 131072


        // Combinaciones útiles predefinidas
        VRSystems = VRInput | FusionVrGrabbable | Physics,
        NetworkSystems = Networking | Photon | StateManagement,
        GameplaySystems = Gameplay | Puzzle | SceneManagement,
        All = ~0
    }

    /// <summary>
    /// Nivel de severidad del log
    /// </summary>
    public enum LogLevel
    {
        Verbose = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// Estructura para almacenar información completa del log
    /// </summary>
    [Serializable]
    public struct LogEntry
    {
        public float timestamp;
        public string message;
        public LogCategory category;
        public LogLevel level;
        public string className;
        public string methodName;
        public int lineNumber;
        public int frameCount;
        public float fps;
        
        // Información específica de Photon Fusion
        public int? tick;
        public byte? playerId;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("[{0:F3}] ", timestamp);
            sb.AppendFormat("[{0}] ", level);
            sb.AppendFormat("[{0}] ", category);
            
            if (tick.HasValue)
                sb.AppendFormat("[Tick:{0}] ", tick.Value);
            if (playerId.HasValue)
                sb.AppendFormat("[P{0}] ", playerId.Value);
            
            sb.AppendFormat("[{0}.{1}:{2}] ", className, methodName, lineNumber);
            sb.AppendFormat("FPS:{0:F1} ", fps);
            sb.Append(message);
            
            return sb.ToString();
        }

        public string ToCSV()
        {
            return $"{timestamp:F3},{level},{category},{className},{methodName}," +
                   $"{lineNumber},{frameCount},{fps:F1}," +
                   $"{tick?.ToString() ?? ""}," +
                   $"{playerId?.ToString() ?? ""}," +
                   $"\"{message.Replace("\"", "\"\"")}\"";
        }
    }

    /// <summary>
    /// Sistema avanzado de debugging con filtrado por bitmask y persistencia
    /// </summary>
    public class AdvancedDebugSystem : MonoBehaviour
    {
        private static AdvancedDebugSystem _instance;
        public static AdvancedDebugSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Primero buscar si existe una instancia en la escena
                    _instance = FindAnyObjectByType<AdvancedDebugSystem>();

                    // Si no existe, crear una nueva con configuración por defecto
                    if (_instance == null)
                    {
                        Debug.LogWarning("[AdvancedDebugSystem] No instance found in scene. Creating default instance. " +
                                       "Consider adding AdvancedDebugSystem to your scene for custom configuration.");
                        GameObject go = new GameObject("[AdvancedDebugSystem]");
                        _instance = go.AddComponent<AdvancedDebugSystem>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Configuration")]
        [SerializeField] private bool _enableLogging = true;
        [SerializeField] private bool _enableConsoleOutput = true;
        [SerializeField] private bool _enableFileOutput = true;
        [SerializeField] private bool _enableInVRDisplay = false;
        
        [Header("Filtering")]
        [SerializeField] private LogCategory _activeCategories = LogCategory.All;
        [SerializeField] private LogLevel _minimumLogLevel = LogLevel.Debug;
        
        [Header("Performance")]
        [SerializeField] private int _maxLogsInMemory = 1000;
        [SerializeField] private int _maxLogsPerFrame = 10;
        [SerializeField] private float _fileWriteInterval = 5f;
        
        [Header("File Output")]
        [SerializeField] private string _logFileName = "debug_log";
        [SerializeField] private bool _useTimestampInFileName = true;
        
        // Runtime data
        private Queue<LogEntry> _logQueue = new Queue<LogEntry>();
        private List<LogEntry> _pendingFileWrites = new List<LogEntry>();
        private StringBuilder _stringBuilder = new StringBuilder();
        private string _sessionId;
        private string _logFilePath;
        private float _lastFileWrite;
        private int _logsThisFrame;
        private float _currentFPS;
        
        // Performance tracking
        private float _deltaTime;
        
        // Photon Fusion integration
        private NetworkRunner _networkRunner;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[AdvancedDebugSystem] Duplicate instance found on '{gameObject.name}'. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log($"[AdvancedDebugSystem] Initialized from scene on '{gameObject.name}' with categories: {_activeCategories}");
            InitializeSystem();
        }

        private void InitializeSystem()
        {
            _sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            CreateLogFile();
            
            // Suscribirse a eventos de Unity
            Application.logMessageReceived += HandleUnityLog;
            
            // Suscribirse a logs de Fusion si está disponible
            #if FUSION_WEAVER
          
            #endif
        }

        private void CreateLogFile()
        {
            if (!_enableFileOutput) return;

            string fileName = _useTimestampInFileName 
                ? $"{_logFileName}_{_sessionId}.csv" 
                : $"{_logFileName}.csv";
                
            _logFilePath = Path.Combine(Application.persistentDataPath, "Logs", fileName);
            
            // Crear directorio si no existe
            string directory = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Escribir header del CSV
            string header = "Timestamp,Level,Category,Class,Method,Line,Frame,FPS,Tick,PlayerID,Message\n";
            File.WriteAllText(_logFilePath, header);
        }

        private void Update()
        {
            // Calcular FPS
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _currentFPS = 1.0f / _deltaTime;
            
            // Reset contador de logs por frame
            _logsThisFrame = 0;
            
            // Escribir logs pendientes a archivo
            if (_enableFileOutput && Time.time - _lastFileWrite > _fileWriteInterval)
            {
                WriteLogsToFile();
            }
        }

        private void OnDestroy()
        {
            WriteLogsToFile();
            Application.logMessageReceived -= HandleUnityLog;
            
            #if FUSION_WEAVER
            // Limpiar callbacks de Fusion
          
            
            #endif
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                WriteLogsToFile();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                WriteLogsToFile();
        }

        #region Public Logging Methods

        /// <summary>
        /// Log principal con información completa del caller
        /// </summary>
        public static void Log(
            string message, 
            LogCategory category = LogCategory.General,
            LogLevel level = LogLevel.Debug,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!Instance._enableLogging) return;
            if (!Instance.ShouldLog(category, level)) return;
            if (Instance._logsThisFrame >= Instance._maxLogsPerFrame) return;
            
            Instance._logsThisFrame++;
            
            // Extraer nombre de clase del path
            string className = Path.GetFileNameWithoutExtension(sourceFilePath);
            
            LogEntry entry = new LogEntry
            {
                timestamp = Time.time,
                message = message,
                category = category,
                level = level,
                className = className,
                methodName = memberName,
                lineNumber = sourceLineNumber,
                frameCount = Time.frameCount,
                fps = Instance._currentFPS
            };
            
            // Añadir información de Photon Fusion si está disponible
            if (Instance._networkRunner != null)
            {
                entry.tick = Instance._networkRunner.Tick;
                entry.playerId = (byte?)Instance._networkRunner.LocalPlayer.RawEncoded;
            }
            
            Instance.ProcessLog(entry);
        }

        /// <summary>
        /// Log rápido para mensajes simples
        /// </summary>
        public static void QuickLog(string message, LogCategory category = LogCategory.General)
        {
            Log(message, category, LogLevel.Debug);
        }

        /// <summary>
        /// Log de información
        /// </summary>
        public static void LogInfo(string message, LogCategory category = LogCategory.General)
        {
            Log(message, category, LogLevel.Info);
        }

        /// <summary>
        /// Log de advertencia
        /// </summary>
        public static void LogWarning(string message, LogCategory category = LogCategory.General)
        {
            Log(message, category, LogLevel.Warning);
        }

        /// <summary>
        /// Log de error
        /// </summary>
        public static void LogError(string message, LogCategory category = LogCategory.General)
        {
            Log(message, category, LogLevel.Error);
        }

        /// <summary>
        /// Log crítico
        /// </summary>
        public static void LogCritical(string message, LogCategory category = LogCategory.General)
        {
            Log(message, category, LogLevel.Critical);
        }

        /// <summary>
        /// Log formateado con parámetros
        /// </summary>
        public static void LogFormat(string format, LogCategory category, params object[] args)
        {
            Log(string.Format(format, args), category, LogLevel.Debug);
        }

        #endregion

        #region Configuration Methods

        public static void SetCategories(LogCategory categories)
        {
            Instance._activeCategories = categories;
        }

        public static void EnableCategory(LogCategory category)
        {
            Instance._activeCategories |= category;
        }

        public static void DisableCategory(LogCategory category)
        {
            Instance._activeCategories &= ~category;
        }

        public static void SetMinimumLevel(LogLevel level)
        {
            Instance._minimumLogLevel = level;
        }

        public static void SetNetworkRunner(NetworkRunner runner)
        {
            Instance._networkRunner = runner;
        }

        /// <summary>
        /// Obtiene las categorías activas actuales
        /// </summary>
        public static LogCategory GetActiveCategories()
        {
            return Instance._activeCategories;
        }

        /// <summary>
        /// Obtiene el nivel mínimo de log actual
        /// </summary>
        public static LogLevel GetMinimumLevel()
        {
            return Instance._minimumLogLevel;
        }

        /// <summary>
        /// Verifica si el logging está habilitado
        /// </summary>
        public static bool IsLoggingEnabled()
        {
            return Instance._enableLogging;
        }

        /// <summary>
        /// Habilita o deshabilita el logging completamente
        /// </summary>
        public static void SetLoggingEnabled(bool enabled)
        {
            Instance._enableLogging = enabled;
        }

        #endregion

        #region Private Methods

        private bool ShouldLog(LogCategory category, LogLevel level)
        {
            return (_activeCategories & category) != 0 && level >= _minimumLogLevel;
        }

        private void ProcessLog(LogEntry entry)
        {
            // Añadir a cola
            _logQueue.Enqueue(entry);
            if (_logQueue.Count > _maxLogsInMemory)
            {
                _logQueue.Dequeue();
            }
            
            // Añadir a lista de escritura pendiente
            if (_enableFileOutput)
            {
                _pendingFileWrites.Add(entry);
            }
            
            // Output a consola
            if (_enableConsoleOutput)
            {
                OutputToConsole(entry);
            }
        }

        private void OutputToConsole(LogEntry entry)
        {
            string logMessage = entry.ToString();
            
            switch (entry.level)
            {
                case LogLevel.Error:
                case LogLevel.Critical:
                    Debug.LogError(logMessage);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(logMessage);
                    break;
                default:
                    Debug.Log(logMessage);
                    break;
            }
        }

        private void WriteLogsToFile()
        {
            if (!_enableFileOutput || _pendingFileWrites.Count == 0) return;
            
            try
            {
                _stringBuilder.Clear();
                foreach (var entry in _pendingFileWrites)
                {
                    _stringBuilder.AppendLine(entry.ToCSV());
                }
                
                File.AppendAllText(_logFilePath, _stringBuilder.ToString());
                _pendingFileWrites.Clear();
                _lastFileWrite = Time.time;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write logs to file: {e.Message}");
            }
        }

        private void HandleUnityLog(string logString, string stackTrace, UnityEngine.LogType type)
        {
            // Capturar logs de Unity no procesados por nuestro sistema
            if (!logString.Contains("[") || !logString.Contains("]"))
            {
                LogLevel level = LogLevel.Debug;
                switch (type)
                {
                    case UnityEngine.LogType.Error:
                    case UnityEngine.LogType.Exception:
                        level = LogLevel.Error;
                        break;
                    case UnityEngine.LogType.Warning:
                        level = LogLevel.Warning;
                        break;
                }
                
                Log($"[Unity] {logString}", LogCategory.General, level);
            }
        }

        #if FUSION_WEAVER
        private void HandleFusionInfo(string message)
        {
            Log($"[Fusion] {message}", LogCategory.Photon | LogCategory.Networking, LogLevel.Info);
        }

        private void HandleFusionWarn(string message)
        {
            Log($"[Fusion] {message}", LogCategory.Photon | LogCategory.Networking, LogLevel.Warning);
        }

        private void HandleFusionError(string message)
        {
            Log($"[Fusion] {message}", LogCategory.Photon | LogCategory.Networking, LogLevel.Error);
        }
        #endif

        #endregion

        #region Public Query Methods

        public static List<LogEntry> GetLogs(LogCategory categoryFilter = LogCategory.All)
        {
            List<LogEntry> filtered = new List<LogEntry>();
            foreach (var entry in Instance._logQueue)
            {
                if ((entry.category & categoryFilter) != 0)
                {
                    filtered.Add(entry);
                }
            }
            return filtered;
        }

        public static string GetLogFilePath()
        {
            return Instance._logFilePath;
        }

        public static void ClearLogs()
        {
            Instance._logQueue.Clear();
            Instance._pendingFileWrites.Clear();
        }

        #endregion
    }
}