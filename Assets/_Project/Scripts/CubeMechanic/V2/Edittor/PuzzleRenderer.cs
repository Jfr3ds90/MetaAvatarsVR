using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace PuzzleCubes.Core
{
    /// <summary>
    /// Herramienta del editor para crear renders profesionales de puzzles
    /// </summary>
#if UNITY_EDITOR
    public class PuzzleRenderer : EditorWindow
    {
        [System.Serializable]
        public class RenderSettings
        {
            public int imageWidth = 1024;
            public int imageHeight = 1024;
            public float cameraDistance = 3f;
            public Vector3 cameraAngle = new Vector3(30f, -45f, 0f);
            public float fieldOfView = 30f;
            
            public Color gradientTop = new Color(0.4f, 0.6f, 0.9f, 1f);
            public Color gradientBottom = new Color(0.1f, 0.15f, 0.3f, 1f);
            
            // Material settings
            public enum MaterialMode { Color, CustomMaterial }
            public MaterialMode materialMode = MaterialMode.Color;
            public Material customMaterial;
            public Color cubeColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            
            public Color ambientLight = new Color(0.4f, 0.45f, 0.5f, 1f);
            
            public bool addShadows = true;
            public bool antiAliasing = true;
            public int superSampling = 2; // Para anti-aliasing
        }
        
        private RenderSettings settings = new RenderSettings();
        private string renderName = "PuzzleRender";
        private string savePath = "Assets/PuzzleCubes/Renders/";
        
        private GameObject previewContainer;
        private Camera renderCamera;
        private Light mainLight;
        private Light fillLight;
        private List<GameObject> currentCubes = new List<GameObject>();
        
        private Vector2 scrollPos;
        private bool isRendering = false;
        
        [MenuItem("Tools/Puzzle Cubes/Puzzle Renderer")]
        public static void ShowWindow()
        {
            var window = GetWindow<PuzzleRenderer>("Puzzle Renderer");
            window.minSize = new Vector2(400, 600);
        }
        
        private void OnEnable()
        {
            SetupRenderScene();
        }
        
        private void OnDisable()
        {
            CleanupRenderScene();
        }
        
        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            DrawHeader();
            DrawPuzzleSetup();
            DrawRenderSettings();
            DrawCameraSettings();
            DrawColorSettings();
            DrawActions();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Puzzle Renderer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Create professional renders of puzzle structures with gradient backgrounds.", MessageType.Info);
            EditorGUILayout.Space();
        }
        
        private void DrawPuzzleSetup()
        {
            EditorGUILayout.LabelField("Puzzle Setup", EditorStyles.boldLabel);
            
            // Load from PuzzleDefinition
            PuzzleDefinition puzzleDef = EditorGUILayout.ObjectField("Load Puzzle", null, typeof(PuzzleDefinition), false) as PuzzleDefinition;
            if (puzzleDef != null)
            {
                LoadPuzzleStructure(puzzleDef.PuzzleData);
                renderName = puzzleDef.PuzzleData.PuzzleName + "_Render";
            }
            
            EditorGUILayout.Space();
            
            // Manual cube creation
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Cube"))
            {
                AddCubeAtPosition(Vector3.zero);
            }
            if (GUILayout.Button("Clear All"))
            {
                ClearAllCubes();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField($"Current Cubes: {currentCubes.Count}");
            EditorGUILayout.Space();
        }
        
        private void DrawRenderSettings()
        {
            EditorGUILayout.LabelField("Render Settings", EditorStyles.boldLabel);
            
            renderName = EditorGUILayout.TextField("Render Name", renderName);
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Resolution", GUILayout.Width(80));
            settings.imageWidth = EditorGUILayout.IntField(settings.imageWidth, GUILayout.Width(60));
            EditorGUILayout.LabelField("x", GUILayout.Width(20));
            settings.imageHeight = EditorGUILayout.IntField(settings.imageHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            // Preset resolutions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("512x512")) SetResolution(512, 512);
            if (GUILayout.Button("1024x1024")) SetResolution(1024, 1024);
            if (GUILayout.Button("2048x2048")) SetResolution(2048, 2048);
            if (GUILayout.Button("HD")) SetResolution(1920, 1080);
            EditorGUILayout.EndHorizontal();
            
            settings.antiAliasing = EditorGUILayout.Toggle("Anti-Aliasing", settings.antiAliasing);
            if (settings.antiAliasing)
            {
                settings.superSampling = EditorGUILayout.IntSlider("Super Sampling", settings.superSampling, 1, 4);
            }
            
            EditorGUILayout.Space();
        }
        
        private void DrawCameraSettings()
        {
            EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);
            
            settings.cameraDistance = EditorGUILayout.Slider("Camera Distance", settings.cameraDistance, 1f, 10f);
            settings.fieldOfView = EditorGUILayout.Slider("Field of View", settings.fieldOfView, 10f, 60f);
            settings.cameraAngle = EditorGUILayout.Vector3Field("Camera Angle", settings.cameraAngle);
            
            // Camera angle presets
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Isometric")) settings.cameraAngle = new Vector3(30f, -45f, 0f);
            if (GUILayout.Button("Front")) settings.cameraAngle = new Vector3(0f, 0f, 0f);
            if (GUILayout.Button("Top")) settings.cameraAngle = new Vector3(90f, 0f, 0f);
            if (GUILayout.Button("Side")) settings.cameraAngle = new Vector3(0f, -90f, 0f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        private void DrawColorSettings()
        {
            EditorGUILayout.LabelField("Color Settings", EditorStyles.boldLabel);
            
            settings.gradientTop = EditorGUILayout.ColorField("Gradient Top", settings.gradientTop);
            settings.gradientBottom = EditorGUILayout.ColorField("Gradient Bottom", settings.gradientBottom);
            
            EditorGUILayout.Space();
            
            // Material settings
            EditorGUILayout.LabelField("Cube Material", EditorStyles.boldLabel);
            settings.materialMode = (RenderSettings.MaterialMode)EditorGUILayout.EnumPopup("Material Mode", settings.materialMode);
            
            switch (settings.materialMode)
            {
                case RenderSettings.MaterialMode.Color:
                    settings.cubeColor = EditorGUILayout.ColorField("Cube Color", settings.cubeColor);
                    break;
                    
                case RenderSettings.MaterialMode.CustomMaterial:
                    settings.customMaterial = (Material)EditorGUILayout.ObjectField("Custom Material", 
                        settings.customMaterial, typeof(Material), false);
                    
                    if (settings.customMaterial == null)
                    {
                        EditorGUILayout.HelpBox("Please assign a material from your project.", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"Using: {settings.customMaterial.name}", MessageType.Info);
                    }
                    break;
            }
            
            EditorGUILayout.Space();
            
            settings.ambientLight = EditorGUILayout.ColorField("Ambient Light", settings.ambientLight);
            settings.addShadows = EditorGUILayout.Toggle("Add Shadows", settings.addShadows);
            
            // Color presets
            EditorGUILayout.LabelField("Gradient Presets:");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sky Blue"))
            {
                settings.gradientTop = new Color(0.4f, 0.6f, 0.9f, 1f);
                settings.gradientBottom = new Color(0.1f, 0.15f, 0.3f, 1f);
            }
            if (GUILayout.Button("Sunset"))
            {
                settings.gradientTop = new Color(1f, 0.6f, 0.3f, 1f);
                settings.gradientBottom = new Color(0.8f, 0.2f, 0.2f, 1f);
            }
            if (GUILayout.Button("Forest"))
            {
                settings.gradientTop = new Color(0.2f, 0.6f, 0.3f, 1f);
                settings.gradientBottom = new Color(0.1f, 0.3f, 0.1f, 1f);
            }
            if (GUILayout.Button("Purple"))
            {
                settings.gradientTop = new Color(0.6f, 0.3f, 0.8f, 1f);
                settings.gradientBottom = new Color(0.2f, 0.1f, 0.4f, 1f);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Update Preview", GUILayout.Height(30)))
            {
                UpdatePreview();
            }
            
            GUI.enabled = currentCubes.Count > 0 && !isRendering;
            
            if (GUILayout.Button("Render to File", GUILayout.Height(40)))
            {
                RenderToFile();
            }
            
            GUI.enabled = true;
            
            if (isRendering)
            {
                EditorGUILayout.HelpBox("Rendering in progress...", MessageType.Info);
            }
        }
        
        private void SetupRenderScene()
        {
            // Create container
            previewContainer = new GameObject("PuzzleRenderPreview");
            previewContainer.hideFlags = HideFlags.HideAndDontSave;
            
            // Setup camera
            GameObject cameraObj = new GameObject("RenderCamera");
            cameraObj.transform.SetParent(previewContainer.transform);
            renderCamera = cameraObj.AddComponent<Camera>();
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = settings.gradientBottom;
            renderCamera.fieldOfView = settings.fieldOfView;
            renderCamera.nearClipPlane = 0.1f;
            renderCamera.farClipPlane = 100f;
            
            // Setup lights
            GameObject mainLightObj = new GameObject("MainLight");
            mainLightObj.transform.SetParent(previewContainer.transform);
            mainLight = mainLightObj.AddComponent<Light>();
            mainLight.type = LightType.Directional;
            mainLight.intensity = 1f;
            mainLight.color = Color.white;
            mainLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            
            GameObject fillLightObj = new GameObject("FillLight");
            fillLightObj.transform.SetParent(previewContainer.transform);
            fillLight = fillLightObj.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.5f;
            fillLight.color = new Color(0.5f, 0.5f, 0.7f);
            fillLight.transform.rotation = Quaternion.Euler(45f, 150f, 0f);
            
            UpdatePreview();
        }
        
        private void CleanupRenderScene()
        {
            ClearAllCubes();
            
            if (previewContainer)
                DestroyImmediate(previewContainer);
        }
        
        private void LoadPuzzleStructure(PuzzleData puzzleData)
        {
            ClearAllCubes();
            
            foreach (var pos in puzzleData.CubePositions)
            {
                Vector3 worldPos = new Vector3(pos.x * 0.1f, pos.y * 0.1f, pos.z * 0.1f);
                AddCubeAtPosition(worldPos);
            }
            
            UpdatePreview();
        }
        
        private void AddCubeAtPosition(Vector3 position)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(previewContainer.transform);
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * 0.1f;
            
            // Apply material based on mode
            ApplyMaterialToCube(cube);
            
            currentCubes.Add(cube);
        }
        
        private void ApplyMaterialToCube(GameObject cube)
        {
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            if (!renderer) return;
            
            switch (settings.materialMode)
            {
                case RenderSettings.MaterialMode.Color:
                    // Create a new material with color
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = settings.cubeColor;
                    renderer.material = mat;
                    break;
                    
                case RenderSettings.MaterialMode.CustomMaterial:
                    // Use custom material
                    if (settings.customMaterial != null)
                    {
                        // Create instance to avoid modifying the original
                        renderer.material = new Material(settings.customMaterial);
                    }
                    else
                    {
                        // Fallback to default material with color
                        Material fallbackMat = new Material(Shader.Find("Standard"));
                        fallbackMat.color = settings.cubeColor;
                        renderer.material = fallbackMat;
                    }
                    break;
            }
        }
        
        private void ClearAllCubes()
        {
            foreach (var cube in currentCubes)
            {
                if (cube) DestroyImmediate(cube);
            }
            currentCubes.Clear();
        }
        
        private void UpdatePreview()
        {
            if (!renderCamera) return;
            
            // Update ambient light
            //RenderSettings.ambientLight = settings.ambientLight;
            
            // Calculate bounds
            if (currentCubes.Count > 0)
            {
                Bounds bounds = new Bounds(currentCubes[0].transform.position, Vector3.zero);
                foreach (var cube in currentCubes)
                {
                    if (cube) bounds.Encapsulate(cube.transform.position);
                }
                
                // Position camera
                Vector3 center = bounds.center;
                float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float distance = settings.cameraDistance + maxExtent;
                
                renderCamera.transform.rotation = Quaternion.Euler(settings.cameraAngle);
                renderCamera.transform.position = center - renderCamera.transform.forward * distance;
                renderCamera.fieldOfView = settings.fieldOfView;
            }
            
            // Update materials
            foreach (var cube in currentCubes)
            {
                if (cube)
                {
                    ApplyMaterialToCube(cube);
                }
            }
            
            SceneView.RepaintAll();
        }
        
        private void RenderToFile()
        {
            isRendering = true;
            
            try
            {
                // Create directory if needed
                string fullPath = Path.Combine(savePath, renderName + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                
                // Setup render texture
                int width = settings.imageWidth;
                int height = settings.imageHeight;
                
                if (settings.antiAliasing)
                {
                    width *= settings.superSampling;
                    height *= settings.superSampling;
                }
                
                RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = settings.antiAliasing ? 8 : 1;
                
                // Create gradient background
                Texture2D gradientTexture = CreateGradientTexture(width, height);
                
                // Render
                renderCamera.targetTexture = rt;
                
                // Store original settings
                var originalClearFlags = renderCamera.clearFlags;
                var originalBgColor = renderCamera.backgroundColor;
                
                // First pass: Render gradient background
                renderCamera.clearFlags = CameraClearFlags.SolidColor;
                renderCamera.backgroundColor = Color.clear;
                renderCamera.cullingMask = 0; // Render nothing
                renderCamera.Render();
                
                // Apply gradient
                RenderTexture.active = rt;
                Graphics.Blit(gradientTexture, rt);
                
                // Second pass: Render cubes
                renderCamera.clearFlags = CameraClearFlags.Nothing;
                renderCamera.cullingMask = -1; // Render everything
                renderCamera.Render();
                
                // Read pixels
                Texture2D finalTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                finalTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                finalTexture.Apply();
                
                // Downscale if using supersampling
                if (settings.antiAliasing && settings.superSampling > 1)
                {
                    finalTexture = DownscaleTexture(finalTexture, settings.imageWidth, settings.imageHeight);
                }
                
                // Save
                byte[] pngData = finalTexture.EncodeToPNG();
                File.WriteAllBytes(fullPath, pngData);
                
                // Cleanup
                renderCamera.targetTexture = null;
                renderCamera.clearFlags = originalClearFlags;
                renderCamera.backgroundColor = originalBgColor;
                RenderTexture.active = null;
                
                DestroyImmediate(rt);
                DestroyImmediate(gradientTexture);
                DestroyImmediate(finalTexture);
                
                // Import asset
                AssetDatabase.ImportAsset(fullPath);
                
                EditorUtility.DisplayDialog("Success", $"Render saved to:\n{fullPath}", "OK");
                
                // Select in project
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(fullPath);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to render: {e.Message}", "OK");
                Debug.LogError(e);
            }
            finally
            {
                isRendering = false;
            }
        }
        
        private Texture2D CreateGradientTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / height;
                Color color = Color.Lerp(settings.gradientBottom, settings.gradientTop, t);
                
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = color;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
        
        private Texture2D DownscaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            Graphics.Blit(source, rt);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.ARGB32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }
        
        private void SetResolution(int width, int height)
        {
            settings.imageWidth = width;
            settings.imageHeight = height;
        }
    }
#endif
}