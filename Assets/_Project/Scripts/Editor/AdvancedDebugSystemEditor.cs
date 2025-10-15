#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using HackMonkeys.Debugging;

[CustomEditor(typeof(AdvancedDebugSystem))]
public class AdvancedDebugSystemEditor : Editor
{
    private bool _showCategories = true;
    private bool _showStats = false;
    private bool _showRecentLogs = false;
    private Vector2 _logScrollPosition;
    
    private SerializedProperty _enableLogging;
    private SerializedProperty _enableConsoleOutput;
    private SerializedProperty _enableFileOutput;
    private SerializedProperty _enableInVRDisplay;
    private SerializedProperty _activeCategories;
    private SerializedProperty _minimumLogLevel;
    private SerializedProperty _maxLogsInMemory;
    private SerializedProperty _maxLogsPerFrame;
    private SerializedProperty _fileWriteInterval;
    private SerializedProperty _logFileName;
    private SerializedProperty _useTimestampInFileName;

    private void OnEnable()
    {
        _enableLogging = serializedObject.FindProperty("_enableLogging");
        _enableConsoleOutput = serializedObject.FindProperty("_enableConsoleOutput");
        _enableFileOutput = serializedObject.FindProperty("_enableFileOutput");
        _enableInVRDisplay = serializedObject.FindProperty("_enableInVRDisplay");
        _activeCategories = serializedObject.FindProperty("_activeCategories");
        _minimumLogLevel = serializedObject.FindProperty("_minimumLogLevel");
        _maxLogsInMemory = serializedObject.FindProperty("_maxLogsInMemory");
        _maxLogsPerFrame = serializedObject.FindProperty("_maxLogsPerFrame");
        _fileWriteInterval = serializedObject.FindProperty("_fileWriteInterval");
        _logFileName = serializedObject.FindProperty("_logFileName");
        _useTimestampInFileName = serializedObject.FindProperty("_useTimestampInFileName");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // Header
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("Advanced Debug System", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        
        // Main Configuration
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Main Configuration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_enableLogging);
        
        if (_enableLogging.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enableConsoleOutput);
            EditorGUILayout.PropertyField(_enableFileOutput);
            EditorGUILayout.PropertyField(_enableInVRDisplay);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
        
        // Categories
        EditorGUILayout.BeginVertical("box");
        _showCategories = EditorGUILayout.Foldout(_showCategories, "Log Categories", true);
        
        if (_showCategories)
        {
            EditorGUI.BeginChangeCheck();
            
            // Quick actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", GUILayout.Width(50)))
            {
                _activeCategories.intValue = -1; // All flags
            }
            if (GUILayout.Button("None", GUILayout.Width(50)))
            {
                _activeCategories.intValue = 0;
            }
            if (GUILayout.Button("VR Only", GUILayout.Width(70)))
            {
                _activeCategories.intValue = (int)(LogCategory.VRSystems);
            }
            if (GUILayout.Button("Network", GUILayout.Width(70)))
            {
                _activeCategories.intValue = (int)(LogCategory.NetworkSystems);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Individual category toggles in columns
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            
            int currentCategories = _activeCategories.intValue;
            var categories = System.Enum.GetValues(typeof(LogCategory)).Cast<LogCategory>()
                .Where(c => c != LogCategory.None && c != LogCategory.All && 
                           c != LogCategory.VRSystems && c != LogCategory.NetworkSystems);
            
            int column = 0;
            foreach (var category in categories)
            {
                if (column >= 6) // 6 categorías por columna
                {
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.BeginVertical();
                    column = 0;
                }
                
                bool isActive = (currentCategories & (int)category) != 0;
                bool newValue = EditorGUILayout.Toggle(category.ToString(), isActive);
                
                if (newValue != isActive)
                {
                    if (newValue)
                        currentCategories |= (int)category;
                    else
                        currentCategories &= ~(int)category;
                }
                
                column++;
            }
            
            _activeCategories.intValue = currentCategories;
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
        
        // Filtering
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Filtering", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_minimumLogLevel);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
        
        // Performance
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_maxLogsInMemory);
        EditorGUILayout.PropertyField(_maxLogsPerFrame);
        EditorGUILayout.PropertyField(_fileWriteInterval);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
        
        // File Output
        if (_enableFileOutput.boolValue)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("File Output", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_logFileName);
            EditorGUILayout.PropertyField(_useTimestampInFileName);
            
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Current Log File:");
                string path = AdvancedDebugSystem.GetLogFilePath();
                if (!string.IsNullOrEmpty(path))
                {
                    if (GUILayout.Button("Open", GUILayout.Width(50)))
                    {
                        EditorUtility.RevealInFinder(path);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space();
        
        // Runtime Stats (solo en Play Mode)
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginVertical("box");
            _showStats = EditorGUILayout.Foldout(_showStats, "Runtime Statistics", true);
            
            if (_showStats)
            {
                var logs = AdvancedDebugSystem.GetLogs();
                EditorGUILayout.LabelField($"Total Logs: {logs.Count}");
                
                // Count by category
                var categoryCounts = new Dictionary<LogCategory, int>();
                foreach (var log in logs)
                {
                    if (!categoryCounts.ContainsKey(log.category))
                        categoryCounts[log.category] = 0;
                    categoryCounts[log.category]++;
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("By Category:");
                EditorGUI.indentLevel++;
                foreach (var kvp in categoryCounts.OrderByDescending(x => x.Value))
                {
                    EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value}");
                }
                EditorGUI.indentLevel--;
                
                EditorGUILayout.Space();
                if (GUILayout.Button("Clear All Logs"))
                {
                    AdvancedDebugSystem.ClearLogs();
                }
            }
            EditorGUILayout.EndVertical();
            
            // Recent Logs
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            _showRecentLogs = EditorGUILayout.Foldout(_showRecentLogs, "Recent Logs", true);
            
            if (_showRecentLogs)
            {
                var recentLogs = AdvancedDebugSystem.GetLogs()
                    .OrderByDescending(l => l.timestamp)
                    .Take(10)
                    .ToList();
                
                _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition, 
                    GUILayout.Height(200));
                
                foreach (var log in recentLogs)
                {
                    Color oldColor = GUI.color;
                    switch (log.level)
                    {
                        case LogLevel.Error:
                        case LogLevel.Critical:
                            GUI.color = Color.red;
                            break;
                        case LogLevel.Warning:
                            GUI.color = Color.yellow;
                            break;
                    }
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"[{log.timestamp:F2}]", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"[{log.category}]", GUILayout.Width(100));
                    EditorGUILayout.LabelField(log.message);
                    EditorGUILayout.EndHorizontal();
                    
                    GUI.color = oldColor;
                }
                
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}

// Property Drawer para mostrar las categorías como flags
[CustomPropertyDrawer(typeof(LogCategory))]
public class LogCategoryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        property.intValue = EditorGUI.MaskField(position, label, property.intValue, 
            property.enumNames);
        EditorGUI.EndProperty();
    }
}
#endif