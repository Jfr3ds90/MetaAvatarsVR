using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace TLD.Tools
{
    public class TLDLODGeneratorWindow : EditorWindow
    {
        [MenuItem("Tools/TLD Tools/LOD Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<TLDLODGeneratorWindow>("TLD LOD Generator");
            window.minSize = new Vector2(450, 600);
        }

        [MenuItem("Tools/TLD Tools/Documentation")]
        public static void OpenDocumentation()
        {
            string[] searchPaths = {
                Application.dataPath + "/TLD Tools/LOD Generator/Documentation/TLD_LOD_Generator_Documentation.pdf",
                Application.dataPath + "/Editor/TLD Tools/TLD_LOD_Generator_Documentation.pdf"
            };

            string foundPath = "";
            foreach (string path in searchPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    foundPath = path;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(foundPath))
            {
                Application.OpenURL("file://" + foundPath);
            }
            else
            {
                EditorUtility.DisplayDialog("TLD Documentation",
                    "Documentation not found locally.\n\nVisit our YouTube channel for video tutorials and guides!",
                    "Open YouTube", "Close");
                Application.OpenURL("https://www.youtube.com/@TheLastDreamProductions");
            }
        }

        private Texture2D tldLogo;
        private GUIStyle headerStyle;
        private GUIStyle brandStyle;
        private Color tldPrimaryColor = new Color(0.2f, 0.6f, 1.0f, 1.0f);
        private Color tldSecondaryColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);

        private GameObject selectedObject;
        private LODGroup lodGroup;
        private MeshFilter[] meshFilters;
        private LODSettings[] lodSettings = new LODSettings[4];
        private Vector2 scrollPosition;
        private bool showAdvanced = false;
        private bool showPreview = true;
        private int previewLOD = 0;

        private bool preserveUVSeams = true;
        private bool preserveBoundaries = true;
        private bool preserveNormals = true;
        private bool generateImpostors = false;
        private float qualityBias = 1.0f;
        private AnimationCurve reductionCurve = AnimationCurve.Linear(0, 1, 1, 0);

        private string lastError = "";
        private bool hasValidSelection = false;
        private Dictionary<Mesh, MeshValidationResult> meshValidationCache = new Dictionary<Mesh, MeshValidationResult>();

        // Prefab support fields
        private bool isPrefabMode = false;
        private string meshAssetFolder = "Assets/Generated LOD Meshes";
        private List<GameObject> lodObjectsToCleanup = new List<GameObject>();
        private Dictionary<GameObject, GameObject> originalObjectBackups = new Dictionary<GameObject, GameObject>();

        private void OnEnable()
        {
            InitializeLODSettings();
            InitializeTLDBranding();
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void InitializeTLDBranding()
        {
            if (tldLogo == null)
            {
                tldLogo = CreateTLDLogo();
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 18;
                headerStyle.normal.textColor = tldPrimaryColor;
                headerStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (brandStyle == null)
            {
                brandStyle = new GUIStyle(EditorStyles.miniLabel);
                brandStyle.fontSize = 10;
                brandStyle.normal.textColor = Color.gray;
                brandStyle.alignment = TextAnchor.MiddleCenter;
                brandStyle.fontStyle = FontStyle.Italic;
            }
        }

        private Texture2D CreateTLDLogo()
        {
            int width = 64;
            int height = 64;
            Texture2D logo = new Texture2D(width, height);

            float centerX = width / 2f;
            float centerY = height / 2f;
            float circleRadius = 28f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    Color pixelColor = Color.clear;

                    if (distance <= circleRadius)
                    {
                        pixelColor = tldPrimaryColor;
                    }

                    if (distance <= circleRadius - 3)
                    {
                        pixelColor = new Color(tldPrimaryColor.r * 0.8f, tldPrimaryColor.g * 0.8f, tldPrimaryColor.b * 0.8f, 1f);
                    }

                    if (distance <= circleRadius - 3)
                    {
                        if (y >= centerY + 8 && y <= centerY + 14 && x >= centerX - 12 && x <= centerX + 12)
                        {
                            pixelColor = Color.white;
                        }
                        else if (x >= centerX - 3 && x <= centerX + 3 && y >= centerY - 12 && y <= centerY + 14)
                        {
                            pixelColor = Color.white;
                        }
                    }

                    logo.SetPixel(x, y, pixelColor);
                }
            }

            logo.Apply();
            return logo;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            meshValidationCache.Clear();

            if (tldLogo != null)
            {
                DestroyImmediate(tldLogo);
            }

            // Only clean up backup objects
            CleanupBackupObjects();
        }

        private void CleanupBackupObjects()
        {
            foreach (var backup in originalObjectBackups.Values)
            {
                if (backup != null)
                {
                    DestroyImmediate(backup);
                }
            }
            originalObjectBackups.Clear();
        }

        private void OnHierarchyChanged()
        {
            // Detect prefab mode changes
            bool currentPrefabMode = IsEditingPrefab();
            if (currentPrefabMode != isPrefabMode)
            {
                isPrefabMode = currentPrefabMode;
                OnObjectSelectionChanged(); // Refresh validation
            }
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != selectedObject)
            {
                selectedObject = Selection.activeGameObject;
                OnObjectSelectionChanged();
            }
        }

        // PREFAB SUPPORT METHODS
        private bool IsEditingPrefab()
        {
            return PrefabStageUtility.GetCurrentPrefabStage() != null;
        }

        private bool IsPrefabAsset(GameObject obj)
        {
            if (obj == null) return false;
            return PrefabUtility.IsPartOfPrefabAsset(obj);
        }

        private bool IsPrefabInstance(GameObject obj)
        {
            if (obj == null) return false;
            return PrefabUtility.IsPartOfPrefabInstance(obj);
        }

        private string GetPrefabAssetPath(GameObject obj)
        {
            if (obj == null) return "";
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
        }

        private void EnsureMeshAssetFolder()
        {
            try
            {
                if (string.IsNullOrEmpty(meshAssetFolder))
                {
                    meshAssetFolder = "Assets/Generated LOD Meshes";
                }

                if (!AssetDatabase.IsValidFolder(meshAssetFolder))
                {
                    string[] folders = meshAssetFolder.Split('/');
                    string currentPath = folders[0]; // "Assets"

                    for (int i = 1; i < folders.Length; i++)
                    {
                        string newPath = currentPath + "/" + folders[i];
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            string result = AssetDatabase.CreateFolder(currentPath, folders[i]);
                            if (string.IsNullOrEmpty(result))
                            {
                                throw new Exception($"Failed to create folder: {newPath}");
                            }
                        }
                        currentPath = newPath;
                    }

                    AssetDatabase.Refresh();
                    Debug.Log($"Created mesh asset folder: {meshAssetFolder}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to ensure mesh asset folder: {e.Message}");
                // Fallback to Assets root
                meshAssetFolder = "Assets";
            }
        }

        private void InitializeLODSettings()
        {
            for (int i = 0; i < lodSettings.Length; i++)
            {
                lodSettings[i] = new LODSettings
                {
                    enabled = i < 3,
                    screenRelativeHeight = Mathf.Lerp(0.6f, 0.01f, (float)i / 3),
                    qualityReduction = Mathf.Lerp(0.1f, 0.9f, (float)i / 3),
                    name = $"LOD {i}"
                };
            }
        }

        private void OnGUI()
        {
            var originalBGColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);

            EditorGUILayout.BeginVertical("box");

            GUI.backgroundColor = originalBGColor;

            DrawHeader();
            DrawPrefabModeInfo();
            DrawErrorDisplay();
            DrawObjectSelection();

            if (hasValidSelection)
            {
                DrawMeshValidation();
                DrawLODSettings();
                DrawAdvancedSettings();
                DrawPreview();
                DrawGenerationButtons();
            }

            DrawTLDFooter();

            EditorGUILayout.EndVertical();
        }

        private void DrawPrefabModeInfo()
        {
            if (IsEditingPrefab())
            {
                EditorGUILayout.HelpBox("Prefab Mode: LOD meshes will be saved as assets and properly integrated with the prefab system.", MessageType.Info);
            }
            else if (selectedObject != null && IsPrefabAsset(selectedObject))
            {
                EditorGUILayout.HelpBox("Cannot modify prefab assets directly. Please edit the prefab or work with an instance in the scene.", MessageType.Warning);
            }
            else if (selectedObject != null && IsPrefabInstance(selectedObject))
            {
                EditorGUILayout.HelpBox("Prefab Instance: Changes will create an override. Use 'Apply to Prefab' to save to the prefab asset.", MessageType.Info);
            }
        }

        private void DrawTLDFooter()
        {
            EditorGUILayout.Space(10);

            var originalColor = GUI.color;
            GUI.color = tldPrimaryColor;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.Height(1));
            GUI.color = originalColor;

            EditorGUILayout.BeginHorizontal();
            if (brandStyle != null)
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("The Last Dream Productions", brandStyle))
                {
                    Application.OpenURL("https://www.youtube.com/@TheLastDreamProductions");
                }
                EditorGUILayout.LabelField(" | ", brandStyle, GUILayout.Width(15));
                if (GUILayout.Button("Support", brandStyle))
                {
                    Application.OpenURL("mailto:tldproductionbusiness@gmail.com");
                }
                EditorGUILayout.LabelField(" | ", brandStyle, GUILayout.Width(15));
                if (GUILayout.Button("Documentation", brandStyle))
                {
                    OpenDocumentation();
                }
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (tldLogo != null)
            {
                GUILayout.Label(tldLogo, GUILayout.Width(48), GUILayout.Height(48));
            }

            EditorGUILayout.BeginVertical();
            GUILayout.Space(8);
            if (headerStyle != null)
            {
                EditorGUILayout.LabelField("TLD LOD Generator", headerStyle);
            }
            else
            {
                EditorGUILayout.LabelField("TLD LOD Generator", EditorStyles.boldLabel);
            }

            if (brandStyle != null)
            {
                EditorGUILayout.LabelField("Professional Mesh Optimization Suite", brandStyle);
            }
            else
            {
                EditorGUILayout.LabelField("Professional Mesh Optimization Suite", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            var originalColor = GUI.color;
            GUI.color = tldPrimaryColor;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.Height(2));
            GUI.color = originalColor;

            EditorGUILayout.BeginHorizontal();
            if (brandStyle != null)
            {
                EditorGUILayout.LabelField("v1.1.0 - Prefab Edition", brandStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("© The Last Dream Productions", brandStyle);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawErrorDisplay()
        {
            if (!string.IsNullOrEmpty(lastError))
            {
                EditorGUILayout.HelpBox(lastError, MessageType.Error);
                if (GUILayout.Button("Clear Error"))
                {
                    lastError = "";
                }
                EditorGUILayout.Space();
            }
        }

        private void DrawObjectSelection()
        {
            EditorGUILayout.LabelField("Target Object", EditorStyles.boldLabel);

            GameObject newSelection = EditorGUILayout.ObjectField("GameObject", selectedObject, typeof(GameObject), true) as GameObject;

            if (newSelection != selectedObject)
            {
                selectedObject = newSelection;
                OnObjectSelectionChanged();
            }

            if (!hasValidSelection)
            {
                if (selectedObject == null)
                {
                    EditorGUILayout.HelpBox("Select a GameObject with MeshRenderer components to generate LODs.", MessageType.Info);
                }
                else if (IsPrefabAsset(selectedObject) && !IsEditingPrefab())
                {
                    EditorGUILayout.HelpBox("Cannot modify prefab assets directly. Please edit the prefab or work with an instance in the scene.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Selected object has no valid meshes or contains errors. Check the validation section below.", MessageType.Warning);
                }
                return;
            }

            DrawMeshInfo();
        }

        private void DrawMeshInfo()
        {
            if (meshFilters != null && meshFilters.Length > 0)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Mesh Information", EditorStyles.boldLabel);

                int validMeshes = 0;
                int totalVerts = 0;
                int totalTris = 0;

                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh != null)
                    {
                        validMeshes++;
                        totalVerts += mf.sharedMesh.vertexCount;
                        totalTris += mf.sharedMesh.triangles.Length / 3;
                    }
                }

                EditorGUILayout.LabelField($"Valid Meshes: {validMeshes}/{meshFilters.Length}");
                EditorGUILayout.LabelField($"Total Vertices: {totalVerts:N0}");
                EditorGUILayout.LabelField($"Total Triangles: {totalTris:N0}");

                float estimatedMemoryMB = (totalVerts * 32 + totalTris * 12) / (1024f * 1024f);
                EditorGUILayout.LabelField($"Est. Memory: {estimatedMemoryMB:F2} MB");

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawMeshValidation()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mesh Validation", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            bool hasErrors = false;
            bool hasWarnings = false;

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                var validation = ValidateMesh(mf.sharedMesh);

                if (validation.hasErrors || validation.hasWarnings)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(mf.name, GUILayout.Width(150));

                    if (validation.hasErrors)
                    {
                        EditorGUILayout.LabelField("❌ Errors", GUILayout.Width(80));
                        hasErrors = true;
                    }
                    else if (validation.hasWarnings)
                    {
                        EditorGUILayout.LabelField("⚠️ Warnings", GUILayout.Width(80));
                        hasWarnings = true;
                    }

                    EditorGUILayout.LabelField(validation.message);
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (!hasErrors && !hasWarnings)
            {
                EditorGUILayout.LabelField("✅ All meshes passed validation", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private MeshValidationResult ValidateMesh(Mesh mesh)
        {
            if (meshValidationCache.ContainsKey(mesh))
                return meshValidationCache[mesh];

            var result = new MeshValidationResult();

            try
            {
                if (mesh == null)
                {
                    result.hasErrors = true;
                    result.message = "Mesh is null";
                    return result;
                }

                if (mesh.vertexCount == 0)
                {
                    result.hasErrors = true;
                    result.message = "Mesh has no vertices";
                    return result;
                }

                if (mesh.triangles.Length == 0)
                {
                    result.hasErrors = true;
                    result.message = "Mesh has no triangles";
                    return result;
                }

                var triangles = mesh.triangles;
                var vertices = mesh.vertices;
                int degenerateCount = 0;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (triangles[i] == triangles[i + 1] ||
                        triangles[i + 1] == triangles[i + 2] ||
                        triangles[i] == triangles[i + 2])
                    {
                        degenerateCount++;
                    }
                    else
                    {
                        Vector3 v1 = vertices[triangles[i]];
                        Vector3 v2 = vertices[triangles[i + 1]];
                        Vector3 v3 = vertices[triangles[i + 2]];

                        Vector3 cross = Vector3.Cross(v2 - v1, v3 - v1);
                        if (cross.magnitude < 0.0001f)
                        {
                            degenerateCount++;
                        }
                    }
                }

                if (degenerateCount > 0)
                {
                    result.hasWarnings = true;
                    result.message = $"{degenerateCount} degenerate triangles found";
                }

                if (triangles.Length / 3 > 100000)
                {
                    result.hasWarnings = true;
                    result.message = "Very high triangle count - LOD generation may be slow";
                }

                if (mesh.normals.Length == 0)
                {
                    result.hasWarnings = true;
                    result.message = "Mesh has no normals - will be calculated";
                }

                if (mesh.uv.Length == 0)
                {
                    result.hasWarnings = true;
                    result.message = "Mesh has no UV coordinates";
                }

                if (!result.hasErrors && !result.hasWarnings)
                {
                    result.message = "Valid";
                }
            }
            catch (Exception e)
            {
                result.hasErrors = true;
                result.message = $"Validation failed: {e.Message}";
            }

            meshValidationCache[mesh] = result;
            return result;
        }

        private void OnObjectSelectionChanged()
        {
            lastError = "";
            hasValidSelection = false;
            meshValidationCache.Clear();

            try
            {
                if (selectedObject != null)
                {
                    // Check prefab constraints
                    if (IsPrefabAsset(selectedObject) && !IsEditingPrefab())
                    {
                        lastError = "Cannot modify prefab assets directly. Please edit the prefab or work with an instance in the scene.";
                        hasValidSelection = false;
                        return;
                    }

                    meshFilters = selectedObject.GetComponentsInChildren<MeshFilter>();
                    lodGroup = selectedObject.GetComponent<LODGroup>();

                    if (meshFilters != null && meshFilters.Length > 0)
                    {
                        bool hasValidMesh = false;
                        foreach (var mf in meshFilters)
                        {
                            if (mf.sharedMesh != null)
                            {
                                var validation = ValidateMesh(mf.sharedMesh);
                                if (!validation.hasErrors)
                                {
                                    hasValidMesh = true;
                                    break;
                                }
                            }
                        }
                        hasValidSelection = hasValidMesh;
                    }
                }
                else
                {
                    meshFilters = null;
                    lodGroup = null;
                }
            }
            catch (Exception e)
            {
                lastError = $"Error analyzing selected object: {e.Message}";
                hasValidSelection = false;
            }
        }

        private void DrawLODSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LOD Levels", EditorStyles.boldLabel);

            ValidateLODSettings();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            for (int i = 0; i < lodSettings.Length; i++)
            {
                DrawLODLevel(i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ValidateLODSettings()
        {
            for (int i = 1; i < lodSettings.Length; i++)
            {
                if (lodSettings[i].enabled && lodSettings[i - 1].enabled)
                {
                    if (lodSettings[i].screenRelativeHeight >= lodSettings[i - 1].screenRelativeHeight)
                    {
                        lodSettings[i].screenRelativeHeight = lodSettings[i - 1].screenRelativeHeight * 0.9f;
                    }
                }
            }
        }

        private void DrawLODLevel(int index)
        {
            var settings = lodSettings[index];

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            settings.enabled = EditorGUILayout.Toggle(settings.enabled, GUILayout.Width(20));
            EditorGUILayout.LabelField($"LOD {index}", EditorStyles.boldLabel);

            if (index == 0)
            {
                EditorGUILayout.LabelField("(Original)", EditorStyles.miniLabel);
                settings.qualityReduction = 0f;
            }
            else
            {
                EditorGUILayout.LabelField($"({(1f - settings.qualityReduction) * 100:F0}% quality)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();

            if (settings.enabled)
            {
                EditorGUI.indentLevel++;

                float oldScreenHeight = settings.screenRelativeHeight;
                settings.screenRelativeHeight = EditorGUILayout.Slider("Screen Height", settings.screenRelativeHeight, 0.001f, 1f);

                if (settings.screenRelativeHeight != oldScreenHeight)
                {
                    ValidateLODSettings();
                }

                if (index > 0)
                {
                    float oldReduction = settings.qualityReduction;
                    settings.qualityReduction = EditorGUILayout.Slider("Quality Reduction", settings.qualityReduction, 0.05f, 0.98f);

                    if (settings.qualityReduction > 0.95f)
                    {
                        EditorGUILayout.HelpBox("Very high reduction may cause mesh errors", MessageType.Warning);
                    }

                    if (meshFilters != null && meshFilters.Length > 0)
                    {
                        int originalTris = 0;
                        foreach (var mf in meshFilters)
                        {
                            if (mf.sharedMesh != null)
                                originalTris += mf.sharedMesh.triangles.Length / 3;
                        }

                        int reducedTris = Mathf.Max(1, Mathf.RoundToInt(originalTris * (1f - settings.qualityReduction)));
                        EditorGUILayout.LabelField($"Estimated Triangles: {reducedTris:N0}", EditorStyles.miniLabel);

                        if (reducedTris < 3)
                        {
                            EditorGUILayout.HelpBox("Too few triangles - mesh may become invalid", MessageType.Error);
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedSettings()
        {
            EditorGUILayout.Space();
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Settings", true);

            if (showAdvanced)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField("Mesh Simplification", EditorStyles.boldLabel);
                preserveUVSeams = EditorGUILayout.Toggle("Preserve UV Seams", preserveUVSeams);
                preserveBoundaries = EditorGUILayout.Toggle("Preserve Boundaries", preserveBoundaries);
                preserveNormals = EditorGUILayout.Toggle("Preserve Normals", preserveNormals);

                EditorGUILayout.Space();
                qualityBias = EditorGUILayout.Slider("Quality Bias", qualityBias, 0.1f, 3.0f);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Reduction Curve", EditorStyles.boldLabel);
                reductionCurve = EditorGUILayout.CurveField("Custom Curve", reductionCurve);

                EditorGUILayout.Space();
                generateImpostors = EditorGUILayout.Toggle("Generate Impostor LODs", generateImpostors);

                if (generateImpostors)
                {
                    EditorGUILayout.HelpBox("Impostor generation is experimental", MessageType.Info);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Asset Management", EditorStyles.boldLabel);
                string newFolder = EditorGUILayout.TextField("Mesh Asset Folder", meshAssetFolder);
                if (newFolder != meshAssetFolder)
                {
                    meshAssetFolder = newFolder;
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.Space();
            showPreview = EditorGUILayout.Foldout(showPreview, "Preview", true);

            if (showPreview && lodGroup != null)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField("LOD Preview", EditorStyles.boldLabel);

                var enabledLODs = lodSettings.Where(s => s.enabled).ToArray();
                string[] lodNames = enabledLODs.Select((s, i) => $"LOD {i}").ToArray();

                if (lodNames.Length > 0)
                {
                    previewLOD = EditorGUILayout.Popup("Preview LOD", Mathf.Clamp(previewLOD, 0, lodNames.Length - 1), lodNames);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Force LOD Level"))
                    {
                        ForceLODLevel(previewLOD);
                    }

                    if (GUILayout.Button("Reset LOD"))
                    {
                        ResetLODLevel();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawGenerationButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");

            bool canGenerate = ValidateForGeneration();

            GUI.enabled = canGenerate;
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate LODs", GUILayout.Height(30)))
            {
                GenerateLODs();
            }

            GUI.enabled = lodGroup != null;
            if (GUILayout.Button("Clear LODs", GUILayout.Height(30)))
            {
                ClearLODs();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (!canGenerate)
            {
                EditorGUILayout.HelpBox("Fix validation errors before generating LODs", MessageType.Warning);
            }

            if (GUILayout.Button("Batch Process Selected Objects"))
            {
                BatchProcessSelection();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Safety", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Backup"))
            {
                CreateBackup();
            }
            if (GUILayout.Button("Restore Backup"))
            {
                RestoreBackup();
            }
            if (GUILayout.Button("Clean Generated Assets"))
            {
                CleanGeneratedAssets();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private bool ValidateForGeneration()
        {
            if (!hasValidSelection) return false;

            if (!lodSettings.Any(s => s.enabled)) return false;

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    var validation = ValidateMesh(mf.sharedMesh);
                    if (validation.hasErrors) return false;
                }
            }

            return true;
        }

        private Mesh SimplifyMesh(Mesh originalMesh, float reductionAmount)
        {
            if (originalMesh == null)
                throw new ArgumentNullException(nameof(originalMesh));

            if (reductionAmount < 0 || reductionAmount > 1)
                throw new ArgumentOutOfRangeException(nameof(reductionAmount), "Must be between 0 and 1");

            try
            {
                var validation = ValidateMesh(originalMesh);
                if (validation.hasErrors)
                {
                    throw new Exception($"Cannot simplify invalid mesh: {validation.message}");
                }

                var meshSimplifier = new MeshSimplifier(originalMesh);
                meshSimplifier.PreserveBoundaryEdges = preserveBoundaries;
                meshSimplifier.PreserveUVSeamEdges = preserveUVSeams;
                meshSimplifier.PreserveNormals = preserveNormals;
                meshSimplifier.QualityBias = qualityBias;

                int targetVertCount = Mathf.Max(3, Mathf.RoundToInt(originalMesh.vertexCount * (1f - reductionAmount)));

                Mesh result = meshSimplifier.SimplifyMesh(targetVertCount);

                if (result == null)
                {
                    throw new Exception("Mesh simplification returned null");
                }

                var resultValidation = ValidateMesh(result);
                if (resultValidation.hasErrors)
                {
                    DestroyImmediate(result);
                    throw new Exception($"Simplified mesh is invalid: {resultValidation.message}");
                }

                // CRITICAL FIX: ALWAYS save mesh as asset, regardless of context
                result = CreatePersistentMeshAsset(result, originalMesh.name, reductionAmount);

                return result;
            }
            catch (Exception e)
            {
                throw new Exception($"Mesh simplification failed: {e.Message}");
            }
        }

        private void GenerateLODs()
        {
            if (!ValidateForGeneration())
            {
                lastError = "Cannot generate LODs due to validation errors";
                return;
            }

            EditorUtility.DisplayProgressBar("Generating LODs", "Starting...", 0f);

            try
            {
                CreateBackup();

                // Ensure mesh folder exists before starting
                EnsureMeshAssetFolder();

                // Handle prefab context properly
                GameObject targetObject = selectedObject;
                bool isInPrefabStage = IsEditingPrefab();
                bool isPrefabInstance = IsPrefabInstance(targetObject);

                if (isInPrefabStage)
                {
                    Debug.Log("Generating LODs in prefab editing mode");
                }
                else if (IsPrefabAsset(selectedObject))
                {
                    EditorUtility.DisplayDialog("TLD LOD Generator - Error",
                        "Cannot modify prefab assets directly. Please edit the prefab or work with an instance in the scene.", "OK");
                    return;
                }

                if (lodGroup == null)
                {
                    lodGroup = targetObject.AddComponent<LODGroup>();
                }

                List<LOD> lods = new List<LOD>();
                var enabledSettings = lodSettings.Where(s => s.enabled).ToArray();

                for (int i = 0; i < enabledSettings.Length; i++)
                {
                    var settings = enabledSettings[i];
                    EditorUtility.DisplayProgressBar("Generating LODs", $"Processing LOD {i}...", (float)i / enabledSettings.Length);

                    try
                    {
                        LOD lod = GenerateLODLevel(i, settings);
                        lods.Add(lod);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Failed to generate LOD {i}: {e.Message}");
                    }
                }

                lodGroup.SetLODs(lods.ToArray());
                lodGroup.RecalculateBounds();

                // CRITICAL: Force save assets immediately
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Proper prefab handling
                if (isInPrefabStage)
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (prefabStage != null)
                    {
                        EditorUtility.SetDirty(prefabStage.prefabContentsRoot);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(prefabStage.prefabContentsRoot);
                    }
                }
                else
                {
                    EditorUtility.SetDirty(targetObject);

                    if (isPrefabInstance)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
                    }
                }

                // Force another save
                AssetDatabase.SaveAssets();

                lastError = "";
                string contextMessage = isInPrefabStage ?
                    "Prefab changes will be saved when you exit prefab mode. Mesh assets have been created." :
                    isPrefabInstance ? "Scene changes saved. Mesh assets created. Use 'Apply to Prefab' to save to prefab asset." :
                    "Scene changes and mesh assets saved.";

                EditorUtility.DisplayDialog("TLD LOD Generator - Success",
                    $"Generated {lods.Count} LOD levels successfully!\n\n{contextMessage}\n\nMesh assets saved to: {meshAssetFolder}\n\nThe Last Dream Productions - Unity Tools", "Awesome!");
            }
            catch (Exception e)
            {
                lastError = $"Failed to generate LODs: {e.Message}";
                EditorUtility.DisplayDialog("TLD LOD Generator - Error",
                    $"Failed to generate LODs: {e.Message}\n\nContact TLD Support if this persists.", "OK");

                try
                {
                    RestoreBackup();
                }
                catch
                {
                    Debug.LogWarning("Failed to restore backup after LOD generation error");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private LOD GenerateLODLevel(int lodIndex, LODSettings settings)
        {
            List<Renderer> renderers = new List<Renderer>();

            if (lodIndex == 0)
            {
                var originalRenderers = selectedObject.GetComponentsInChildren<Renderer>();
                renderers.AddRange(originalRenderers.Where(r => r != null));
            }
            else
            {
                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.sharedMesh == null) continue;

                    try
                    {
                        GameObject lodGameObject = CreateLODGameObject(meshFilter.gameObject, lodIndex);
                        MeshFilter newMeshFilter = lodGameObject.GetComponent<MeshFilter>();
                        MeshRenderer newRenderer = lodGameObject.GetComponent<MeshRenderer>();

                        Mesh simplifiedMesh = SimplifyMesh(meshFilter.sharedMesh, settings.qualityReduction);

                        if (simplifiedMesh == null)
                        {
                            throw new Exception($"Failed to simplify mesh {meshFilter.sharedMesh.name}");
                        }

                        newMeshFilter.sharedMesh = simplifiedMesh;

                        var originalRenderer = meshFilter.GetComponent<MeshRenderer>();
                        if (originalRenderer != null)
                        {
                            newRenderer.sharedMaterials = originalRenderer.sharedMaterials;
                        }

                        renderers.Add(newRenderer);
                        lodObjectsToCleanup.Add(lodGameObject); // Track for cleanup
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Error processing mesh {meshFilter.name}: {e.Message}");
                    }
                }
            }

            if (renderers.Count == 0)
            {
                throw new Exception($"No valid renderers created for LOD {lodIndex}");
            }

            return new LOD(settings.screenRelativeHeight, renderers.ToArray());
        }

        private GameObject CreateLODGameObject(GameObject original, int lodIndex)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            GameObject lodObject = new GameObject($"{original.name}_LOD{lodIndex}");
            lodObject.transform.SetParent(selectedObject.transform);
            lodObject.transform.localPosition = original.transform.localPosition;
            lodObject.transform.localRotation = original.transform.localRotation;
            lodObject.transform.localScale = original.transform.localScale;

            lodObject.AddComponent<MeshFilter>();
            lodObject.AddComponent<MeshRenderer>();

            return lodObject;
        }

        private Mesh CreatePersistentMeshAsset(Mesh mesh, string originalName, float reduction)
        {
            try
            {
                EnsureMeshAssetFolder();

                // Create unique filename with timestamp to avoid conflicts
                string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string reductionStr = $"{(1f - reduction):P0}".Replace("%", "pct").Replace(" ", "");
                string assetName = $"{originalName}_LOD_{reductionStr}_{timestamp}.asset";

                // Clean filename of invalid characters
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                {
                    assetName = assetName.Replace(c, '_');
                }

                string assetPath = $"{meshAssetFolder}/{assetName}";

                // Ensure path is unique
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                // Create a copy of the mesh for the asset (important!)
                Mesh assetMesh = new Mesh();
                assetMesh.name = System.IO.Path.GetFileNameWithoutExtension(assetName);

                // Copy all mesh data
                assetMesh.vertices = mesh.vertices;
                assetMesh.triangles = mesh.triangles;
                assetMesh.normals = mesh.normals;
                assetMesh.uv = mesh.uv;
                assetMesh.uv2 = mesh.uv2;
                assetMesh.uv3 = mesh.uv3;
                assetMesh.uv4 = mesh.uv4;
                assetMesh.tangents = mesh.tangents;
                assetMesh.colors = mesh.colors;
                assetMesh.colors32 = mesh.colors32;

                // Copy submeshes
                assetMesh.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    assetMesh.SetSubMesh(i, mesh.GetSubMesh(i));
                }

                assetMesh.RecalculateBounds();

                // Create and save the mesh asset
                AssetDatabase.CreateAsset(assetMesh, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Destroy the temporary mesh
                if (mesh != assetMesh)
                {
                    DestroyImmediate(mesh);
                }

                // Load and return the persistent asset
                Mesh persistentMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

                if (persistentMesh == null)
                {
                    throw new Exception($"Failed to load created mesh asset at {assetPath}");
                }

                Debug.Log($"Created persistent mesh asset: {assetPath}");
                return persistentMesh;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create persistent mesh asset: {e.Message}");
                // Return original mesh as fallback, but it may still disappear
                return mesh;
            }
        }

        private void ClearLODs()
        {
            try
            {
                if (lodGroup != null)
                {
                    // Clear LOD objects
                    for (int i = selectedObject.transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = selectedObject.transform.GetChild(i);
                        if (child.name.Contains("_LOD"))
                        {
                            DestroyImmediate(child.gameObject);
                        }
                    }

                    DestroyImmediate(lodGroup);
                    lodGroup = null;

                    // Mark as dirty for prefab handling
                    if (IsEditingPrefab())
                    {
                        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                        if (prefabStage != null)
                        {
                            EditorUtility.SetDirty(prefabStage.prefabContentsRoot);
                        }
                    }
                    else
                    {
                        EditorUtility.SetDirty(selectedObject);
                        if (IsPrefabInstance(selectedObject))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(selectedObject);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                lastError = $"Error clearing LODs: {e.Message}";
            }
        }

        private void BatchProcessSelection()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("TLD LOD Generator", "Please select one or more GameObjects to process.", "OK");
                return;
            }

            int processedCount = 0;
            int errorCount = 0;
            List<string> errors = new List<string>();

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                EditorUtility.DisplayProgressBar("TLD Batch Processing", $"Processing {selectedObjects[i].name}...", (float)i / selectedObjects.Length);

                try
                {
                    GameObject previousSelection = selectedObject;
                    selectedObject = selectedObjects[i];
                    OnObjectSelectionChanged();

                    if (hasValidSelection && ValidateForGeneration())
                    {
                        GenerateLODs();
                        processedCount++;
                    }
                    else
                    {
                        errors.Add($"{selectedObjects[i].name}: Invalid or no meshes found");
                        errorCount++;
                    }

                    selectedObject = previousSelection;
                    OnObjectSelectionChanged();
                }
                catch (Exception e)
                {
                    errors.Add($"{selectedObjects[i].name}: {e.Message}");
                    errorCount++;
                }
            }

            EditorUtility.ClearProgressBar();

            string message = $"TLD Batch Processing Complete!\n\nProcessed: {processedCount}\nErrors: {errorCount}";
            if (errors.Count > 0)
            {
                message += "\n\nErrors:\n" + string.Join("\n", errors.Take(5));
                if (errors.Count > 5)
                    message += $"\n...and {errors.Count - 5} more";
            }
            message += "\n\n© The Last Dream Productions";

            EditorUtility.DisplayDialog("TLD Batch Complete", message, "Great!");
        }

        private void CreateBackup()
        {
            if (selectedObject != null)
            {
                try
                {
                    GameObject backup = Instantiate(selectedObject);
                    backup.name = selectedObject.name + "_TLD_Backup";
                    backup.SetActive(false);
                    originalObjectBackups[selectedObject] = backup;

                    if (IsEditingPrefab())
                    {
                        backup.transform.SetParent(selectedObject.transform.parent);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to create backup: {e.Message}");
                }
            }
        }

        private void RestoreBackup()
        {
            if (selectedObject != null && originalObjectBackups.ContainsKey(selectedObject))
            {
                try
                {
                    GameObject backup = originalObjectBackups[selectedObject];
                    if (backup != null)
                    {
                        EditorUtility.CopySerializedManagedFieldsOnly(backup, selectedObject);
                        DestroyImmediate(backup);
                        originalObjectBackups.Remove(selectedObject);

                        OnObjectSelectionChanged(); // Refresh state
                        EditorUtility.DisplayDialog("TLD LOD Generator", "Backup restored successfully!", "OK");
                    }
                }
                catch (Exception e)
                {
                    lastError = $"Failed to restore backup: {e.Message}";
                }
            }
            else
            {
                EditorUtility.DisplayDialog("TLD LOD Generator", "No backup found for the selected object.", "OK");
            }
        }

        private void CleanGeneratedAssets()
        {
            if (EditorUtility.DisplayDialog("Clean Generated Assets",
                $"This will delete all mesh assets in:\n{meshAssetFolder}\n\nThis action cannot be undone!",
                "Delete Assets", "Cancel"))
            {
                try
                {
                    if (AssetDatabase.IsValidFolder(meshAssetFolder))
                    {
                        string[] meshAssets = AssetDatabase.FindAssets("t:Mesh", new[] { meshAssetFolder });
                        int deletedCount = 0;

                        foreach (string guid in meshAssets)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guid);
                            if (AssetDatabase.DeleteAsset(path))
                            {
                                deletedCount++;
                            }
                        }

                        AssetDatabase.Refresh();
                        EditorUtility.DisplayDialog("Clean Complete", $"Deleted {deletedCount} mesh assets.", "OK");
                    }
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Clean Failed", $"Error cleaning assets: {e.Message}", "OK");
                }
            }
        }

        private void CleanupTemporaryObjects()
        {
            foreach (var obj in lodObjectsToCleanup)
            {
                if (obj != null)
                {
                    DestroyImmediate(obj);
                }
            }
            lodObjectsToCleanup.Clear();

            foreach (var backup in originalObjectBackups.Values)
            {
                if (backup != null)
                {
                    DestroyImmediate(backup);
                }
            }
            originalObjectBackups.Clear();
        }

        private void ForceLODLevel(int level)
        {
            if (lodGroup != null)
            {
                try
                {
                    lodGroup.ForceLOD(level);
                }
                catch (Exception e)
                {
                    lastError = $"Error forcing LOD level: {e.Message}";
                }
            }
        }

        private void ResetLODLevel()
        {
            if (lodGroup != null)
            {
                try
                {
                    lodGroup.ForceLOD(-1);
                }
                catch (Exception e)
                {
                    lastError = $"Error resetting LOD level: {e.Message}";
                }
            }
        }
    }

    [System.Serializable]
    public class LODSettings
    {
        public bool enabled = true;
        public string name = "LOD";
        public float screenRelativeHeight = 0.5f;
        public float qualityReduction = 0.5f;
    }

    public struct MeshValidationResult
    {
        public bool hasErrors;
        public bool hasWarnings;
        public string message;
    }

    public class MeshSimplifier
    {
        private Mesh originalMesh;

        public bool PreserveBoundaryEdges { get; set; } = true;
        public bool PreserveUVSeamEdges { get; set; } = true;
        public bool PreserveNormals { get; set; } = true;
        public float QualityBias { get; set; } = 1.0f;

        public MeshSimplifier(Mesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            originalMesh = mesh;
        }

        public Mesh SimplifyMesh(int targetVertexCount)
        {
            if (targetVertexCount <= 0)
                throw new ArgumentException("Target vertex count must be positive", nameof(targetVertexCount));

            if (targetVertexCount >= originalMesh.vertexCount)
            {
                return CreateMeshCopy(originalMesh);
            }

            try
            {
                Vector3[] vertices = originalMesh.vertices;
                int[] triangles = originalMesh.triangles;
                Vector3[] normals = originalMesh.normals;
                Vector2[] uvs = originalMesh.uv;
                Vector4[] tangents = originalMesh.tangents;

                if (vertices == null || vertices.Length == 0)
                    throw new InvalidOperationException("Mesh has no vertices");

                if (triangles == null || triangles.Length == 0)
                    throw new InvalidOperationException("Mesh has no triangles");

                if (triangles.Length % 3 != 0)
                    throw new InvalidOperationException("Triangle array length is not divisible by 3");

                if (normals == null || normals.Length != vertices.Length)
                {
                    normals = CalculateNormals(vertices, triangles);
                }

                var simplifiedData = PerformDecimation(vertices, triangles, normals, uvs, tangents, targetVertexCount);

                Mesh simplifiedMesh = new Mesh();
                simplifiedMesh.name = originalMesh.name + "_Simplified";

                simplifiedMesh.vertices = simplifiedData.vertices;
                simplifiedMesh.triangles = simplifiedData.triangles;

                if (simplifiedData.normals != null && simplifiedData.normals.Length == simplifiedData.vertices.Length)
                {
                    simplifiedMesh.normals = simplifiedData.normals;
                }
                else
                {
                    simplifiedMesh.RecalculateNormals();
                }

                if (simplifiedData.uvs != null && simplifiedData.uvs.Length == simplifiedData.vertices.Length)
                {
                    simplifiedMesh.uv = simplifiedData.uvs;
                }

                if (simplifiedData.tangents != null && simplifiedData.tangents.Length == simplifiedData.vertices.Length)
                {
                    simplifiedMesh.tangents = simplifiedData.tangents;
                }
                else if (simplifiedData.uvs != null)
                {
                    simplifiedMesh.RecalculateTangents();
                }

                simplifiedMesh.RecalculateBounds();

                if (simplifiedMesh.vertexCount == 0 || simplifiedMesh.triangles.Length == 0)
                {
                    UnityEngine.Object.DestroyImmediate(simplifiedMesh);
                    throw new InvalidOperationException("Simplification resulted in empty mesh");
                }

                return simplifiedMesh;
            }
            catch (Exception e)
            {
                throw new Exception($"Mesh simplification failed: {e.Message}", e);
            }
        }

        private MeshData PerformDecimation(Vector3[] vertices, int[] triangles, Vector3[] normals, Vector2[] uvs, Vector4[] tangents, int targetVertexCount)
        {
            try
            {
                float reductionRatio = (float)targetVertexCount / vertices.Length;
                reductionRatio = Mathf.Clamp(reductionRatio, 0.01f, 1.0f);

                var clusteredData = ClusterVertices(vertices, triangles, normals, uvs, tangents, reductionRatio);

                return clusteredData;
            }
            catch (Exception e)
            {
                throw new Exception($"Decimation failed: {e.Message}", e);
            }
        }

        private MeshData ClusterVertices(Vector3[] vertices, int[] triangles, Vector3[] normals, Vector2[] uvs, Vector4[] tangents, float reductionRatio)
        {
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices)
            {
                bounds.Encapsulate(vertex);
            }

            float clusterSize = bounds.size.magnitude * (1f - reductionRatio) * 0.1f;
            clusterSize = Mathf.Max(clusterSize, 0.001f);

            Dictionary<Vector3Int, List<int>> clusters = new Dictionary<Vector3Int, List<int>>();

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int clusterKey = new Vector3Int(
                    Mathf.FloorToInt(vertices[i].x / clusterSize),
                    Mathf.FloorToInt(vertices[i].y / clusterSize),
                    Mathf.FloorToInt(vertices[i].z / clusterSize)
                );

                if (!clusters.ContainsKey(clusterKey))
                {
                    clusters[clusterKey] = new List<int>();
                }
                clusters[clusterKey].Add(i);
            }

            List<Vector3> newVertices = new List<Vector3>();
            List<Vector3> newNormals = new List<Vector3>();
            List<Vector2> newUVs = new List<Vector2>();
            List<Vector4> newTangents = new List<Vector4>();
            Dictionary<int, int> vertexMapping = new Dictionary<int, int>();

            int newVertexIndex = 0;
            foreach (var cluster in clusters.Values)
            {
                Vector3 centerPos = Vector3.zero;
                Vector3 centerNormal = Vector3.zero;
                Vector2 centerUV = Vector2.zero;
                Vector4 centerTangent = Vector4.zero;

                foreach (int oldIndex in cluster)
                {
                    centerPos += vertices[oldIndex];
                    if (normals != null && oldIndex < normals.Length)
                        centerNormal += normals[oldIndex];
                    if (uvs != null && oldIndex < uvs.Length)
                        centerUV += uvs[oldIndex];
                    if (tangents != null && oldIndex < tangents.Length)
                        centerTangent += tangents[oldIndex];
                }

                centerPos /= cluster.Count;
                centerNormal = (centerNormal / cluster.Count).normalized;
                centerUV /= cluster.Count;
                centerTangent /= cluster.Count;

                newVertices.Add(centerPos);
                newNormals.Add(centerNormal);
                newUVs.Add(centerUV);
                newTangents.Add(centerTangent);

                foreach (int oldIndex in cluster)
                {
                    vertexMapping[oldIndex] = newVertexIndex;
                }

                newVertexIndex++;
            }

            List<int> newTriangles = new List<int>();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int v1 = vertexMapping[triangles[i]];
                int v2 = vertexMapping[triangles[i + 1]];
                int v3 = vertexMapping[triangles[i + 2]];

                if (v1 != v2 && v2 != v3 && v1 != v3)
                {
                    newTriangles.Add(v1);
                    newTriangles.Add(v2);
                    newTriangles.Add(v3);
                }
            }

            return new MeshData
            {
                vertices = newVertices.ToArray(),
                triangles = newTriangles.ToArray(),
                normals = newNormals.Count > 0 ? newNormals.ToArray() : null,
                uvs = newUVs.Count > 0 ? newUVs.ToArray() : null,
                tangents = newTangents.Count > 0 ? newTangents.ToArray() : null
            };
        }

        private Vector3[] CalculateNormals(Vector3[] vertices, int[] triangles)
        {
            Vector3[] normals = new Vector3[vertices.Length];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];

                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 v3 = vertices[i3];

                Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;

                normals[i1] += normal;
                normals[i2] += normal;
                normals[i3] += normal;
            }

            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normals[i].normalized;
            }

            return normals;
        }

        private Mesh CreateMeshCopy(Mesh original)
        {
            Mesh copy = new Mesh();
            copy.name = original.name + "_Copy";
            copy.vertices = original.vertices;
            copy.triangles = original.triangles;
            copy.normals = original.normals;
            copy.uv = original.uv;
            copy.tangents = original.tangents;
            copy.colors = original.colors;
            copy.RecalculateBounds();
            return copy;
        }

        private struct MeshData
        {
            public Vector3[] vertices;
            public int[] triangles;
            public Vector3[] normals;
            public Vector2[] uvs;
            public Vector4[] tangents;
        }
    }

    [CustomPropertyDrawer(typeof(LODSettings))]
    public class LODSettingsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            SerializedProperty enabled = property.FindPropertyRelative("enabled");
            SerializedProperty screenHeight = property.FindPropertyRelative("screenRelativeHeight");
            SerializedProperty quality = property.FindPropertyRelative("qualityReduction");

            if (enabled != null)
            {
                enabled.boolValue = EditorGUI.Toggle(new Rect(rect.x, rect.y, 20, rect.height), enabled.boolValue);
            }

            EditorGUI.LabelField(new Rect(rect.x + 25, rect.y, 50, rect.height), label);

            if (enabled != null && enabled.boolValue && screenHeight != null)
            {
                screenHeight.floatValue = EditorGUI.Slider(new Rect(rect.x + 80, rect.y, rect.width - 80, rect.height), screenHeight.floatValue, 0.001f, 1f);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}