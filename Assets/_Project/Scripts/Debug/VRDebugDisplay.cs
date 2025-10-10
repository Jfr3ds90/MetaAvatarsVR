using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HackMonkeys.Debugging;

/// <summary>
/// Sistema de visualización de logs en VR - Se adjunta a la muñeca del jugador
/// </summary>
public class VRDebugDisplay : MonoBehaviour
{
    [Header("Display Configuration")]
    [SerializeField] private Canvas debugCanvas;
    [SerializeField] private RectTransform logContainer;
    [SerializeField] private GameObject logEntryPrefab;
    [SerializeField] private ScrollRect scrollRect;
    
    [Header("Display Settings")]
    [SerializeField] private int maxDisplayedLogs = 10;
    [SerializeField] private float logLifetime = 10f;
    [SerializeField] private bool autoScroll = true;
    [SerializeField] private bool followWrist = true;
    
    [Header("Wrist Tracking")]
    [SerializeField] private Transform leftHandAnchor;
    [SerializeField] private Transform rightHandAnchor;
    [SerializeField] private bool useLeftHand = true;
    [SerializeField] private Vector3 displayOffset = new Vector3(0, 0.1f, 0);
    [SerializeField] private Vector3 displayRotation = new Vector3(45, 0, 0);
    
    [Header("Filtering")]
    [SerializeField] private LogCategory displayCategories = LogCategory.All;
    [SerializeField] private LogLevel minimumDisplayLevel = LogLevel.Debug;
    
    [Header("Visual Settings")]
    [SerializeField] private Color debugColor = Color.white;
    [SerializeField] private Color infoColor = Color.cyan;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color errorColor = Color.red;
    [SerializeField] private Color criticalColor = new Color(1f, 0.5f, 0f);
    
    [Header("Performance")]
    [SerializeField] private float updateInterval = 0.1f;
    
    // Runtime
    private List<LogDisplayEntry> _displayedLogs = new List<LogDisplayEntry>();
    private float _lastUpdate;
    private Transform _currentHand;
    private int _lastLogCount = 0;
    
    private class LogDisplayEntry
    {
        public GameObject gameObject;
        public TextMeshProUGUI textComponent;
        public float creationTime;
        public LogEntry logData;
    }
    
    private void Awake()
    {
        InitializeDisplay();
        CreateLogEntryPool();
    }
    
    private void InitializeDisplay()
    {
        if (debugCanvas == null)
        {
            CreateDebugCanvas();
        }
        
        // Configurar el canvas para VR
        debugCanvas.renderMode = RenderMode.WorldSpace;
        debugCanvas.transform.localScale = Vector3.one * 0.001f;
        
        // Determinar qué mano seguir
        _currentHand = useLeftHand ? leftHandAnchor : rightHandAnchor;
    }
    
    private void CreateDebugCanvas()
    {
        // Crear Canvas
        GameObject canvasGO = new GameObject("VR Debug Canvas");
        canvasGO.transform.SetParent(transform);
        debugCanvas = canvasGO.AddComponent<Canvas>();
        debugCanvas.renderMode = RenderMode.WorldSpace;
        
        // Añadir Canvas Scaler
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        
        // Añadir Graphic Raycaster
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Crear panel de fondo
        GameObject panel = new GameObject("Debug Panel");
        panel.transform.SetParent(canvasGO.transform);
        Image bgImage = panel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.8f);
        
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(400, 300);
        panelRect.anchoredPosition = Vector2.zero;
        
        // Crear ScrollView
        GameObject scrollViewGO = new GameObject("Scroll View");
        scrollViewGO.transform.SetParent(panel.transform);
        scrollRect = scrollViewGO.AddComponent<ScrollRect>();
        
        RectTransform scrollRectTransform = scrollViewGO.GetComponent<RectTransform>();
        scrollRectTransform.sizeDelta = new Vector2(380, 280);
        scrollRectTransform.anchoredPosition = Vector2.zero;
        
        // Crear viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollViewGO.transform);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0, 0, 0, 0);
        viewport.AddComponent<Mask>();
        
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.sizeDelta = new Vector2(380, 280);
        viewportRect.anchoredPosition = Vector2.zero;
        
        // Crear content container
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform);
        logContainer = content.AddComponent<RectTransform>();
        
        // Configurar layout
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        
        ContentSizeFitter sizeFitter = content.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Configurar ScrollRect
        scrollRect.content = logContainer;
        scrollRect.viewport = viewportRect;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        
        // Crear prefab de entrada de log
        CreateLogEntryPrefab();
    }
    
    private void CreateLogEntryPrefab()
    {
        if (logEntryPrefab == null)
        {
            logEntryPrefab = new GameObject("LogEntryPrefab");
            
            // Añadir texto
            TextMeshProUGUI textComp = logEntryPrefab.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 12;
            textComp.raycastTarget = false;
            
            // Configurar RectTransform
            RectTransform rect = logEntryPrefab.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 20);
            
            // Hacer prefab inactivo
            logEntryPrefab.SetActive(false);
        }
    }
    
    private void CreateLogEntryPool()
    {
        // Pre-crear entradas de log para evitar instanciación en runtime
        for (int i = 0; i < maxDisplayedLogs; i++)
        {
            GameObject entry = Instantiate(logEntryPrefab, logContainer);
            entry.SetActive(false);
        }
    }
    
    private void OnEnable()
    {
        // Comenzar actualizaciones periódicas
        InvokeRepeating(nameof(UpdateLogDisplay), 0f, updateInterval);
    }
    
    private void OnDisable()
    {
        CancelInvoke(nameof(UpdateLogDisplay));
    }
    
    private void Update()
    {
        // Actualizar posición del display si está siguiendo la muñeca
        if (followWrist && _currentHand != null)
        {
            UpdateDisplayPosition();
        }
        
        // Limpiar logs antiguos
        CleanOldLogs();
    }
    
    private void UpdateDisplayPosition()
    {
        // Posicionar el canvas en la muñeca
        debugCanvas.transform.position = _currentHand.position + _currentHand.TransformDirection(displayOffset);
        
        // Rotar para mirar hacia el usuario
        if (Camera.main != null)
        {
            Vector3 lookDirection = Camera.main.transform.position - debugCanvas.transform.position;
            debugCanvas.transform.rotation = Quaternion.LookRotation(-lookDirection);
            debugCanvas.transform.Rotate(displayRotation);
        }
    }
    
    private void UpdateLogDisplay()
    {
        // Obtener nuevos logs
        var allLogs = AdvancedDebugSystem.GetLogs(displayCategories);
        
        if (allLogs.Count > _lastLogCount)
        {
            // Procesar solo los nuevos logs
            for (int i = _lastLogCount; i < allLogs.Count; i++)
            {
                var log = allLogs[i];
                if (log.level >= minimumDisplayLevel)
                {
                    AddLogToDisplay(log);
                }
            }
            
            _lastLogCount = allLogs.Count;
            
            // Auto scroll al final
            if (autoScroll && scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
    
    private void AddLogToDisplay(LogEntry log)
    {
        // Reutilizar o crear nueva entrada
        GameObject logGO = GetOrCreateLogEntry();
        TextMeshProUGUI textComp = logGO.GetComponent<TextMeshProUGUI>();
        
        // Formatear texto
        string formattedLog = FormatLogForVR(log);
        textComp.text = formattedLog;
        
        // Aplicar color según nivel
        textComp.color = GetLogColor(log.level);
        
        // Guardar referencia
        LogDisplayEntry displayEntry = new LogDisplayEntry
        {
            gameObject = logGO,
            textComponent = textComp,
            creationTime = Time.time,
            logData = log
        };
        
        _displayedLogs.Add(displayEntry);
        logGO.SetActive(true);
        
        // Limitar cantidad de logs mostrados
        while (_displayedLogs.Count > maxDisplayedLogs)
        {
            RemoveOldestLog();
        }
    }
    
    private GameObject GetOrCreateLogEntry()
    {
        // Buscar entrada inactiva en el pool
        foreach (Transform child in logContainer)
        {
            if (!child.gameObject.activeSelf)
            {
                return child.gameObject;
            }
        }
        
        // Si no hay disponibles, crear nueva
        return Instantiate(logEntryPrefab, logContainer);
    }
    
    private string FormatLogForVR(LogEntry log)
    {
        // Formato compacto para VR
        string time = $"{log.timestamp:F1}";
        string category = GetShortCategoryName(log.category);
        
        // Truncar mensaje si es muy largo
        string message = log.message;
        if (message.Length > 50)
        {
            message = message.Substring(0, 47) + "...";
        }
        
        return $"[{time}] [{category}] {message}";
    }
    
    private string GetShortCategoryName(LogCategory category)
    {
        // Abreviaciones para ahorrar espacio
        switch (category)
        {
            case LogCategory.UI: return "UI";
            case LogCategory.Networking: return "NET";
            case LogCategory.MusicalNote: return "NOTE";
            case LogCategory.FusionVrGrabbable: return "GRAB";
            case LogCategory.Physics: return "PHYS";
            case LogCategory.Audio: return "AUD";
            case LogCategory.Performance: return "PERF";
            case LogCategory.VRInput: return "VR";
            case LogCategory.StateManagement: return "STATE";
            case LogCategory.Photon: return "PHO";
            case LogCategory.Animation: return "ANIM";
            default: return "GEN";
        }
    }
    
    private Color GetLogColor(LogLevel level)
    {
        switch (level)
        {
            case LogLevel.Verbose:
            case LogLevel.Debug:
                return debugColor;
            case LogLevel.Info:
                return infoColor;
            case LogLevel.Warning:
                return warningColor;
            case LogLevel.Error:
                return errorColor;
            case LogLevel.Critical:
                return criticalColor;
            default:
                return Color.white;
        }
    }
    
    private void CleanOldLogs()
    {
        float currentTime = Time.time;
        
        for (int i = _displayedLogs.Count - 1; i >= 0; i--)
        {
            if (currentTime - _displayedLogs[i].creationTime > logLifetime)
            {
                RemoveLogAt(i);
            }
        }
    }
    
    private void RemoveOldestLog()
    {
        if (_displayedLogs.Count > 0)
        {
            RemoveLogAt(0);
        }
    }
    
    private void RemoveLogAt(int index)
    {
        if (index < 0 || index >= _displayedLogs.Count) return;
        
        var entry = _displayedLogs[index];
        entry.gameObject.SetActive(false);
        _displayedLogs.RemoveAt(index);
    }
    
    // Métodos públicos para control en runtime
    public void ToggleDisplay()
    {
        debugCanvas.gameObject.SetActive(!debugCanvas.gameObject.activeSelf);
    }
    
    public void SwitchHand()
    {
        useLeftHand = !useLeftHand;
        _currentHand = useLeftHand ? leftHandAnchor : rightHandAnchor;
    }
    
    public void ClearDisplay()
    {
        foreach (var entry in _displayedLogs)
        {
            entry.gameObject.SetActive(false);
        }
        _displayedLogs.Clear();
    }
    
    public void SetFilter(LogCategory categories)
    {
        displayCategories = categories;
        ClearDisplay();
        _lastLogCount = 0;
    }
    
    public void SetMinimumLevel(LogLevel level)
    {
        minimumDisplayLevel = level;
        ClearDisplay();
        _lastLogCount = 0;
    }
}