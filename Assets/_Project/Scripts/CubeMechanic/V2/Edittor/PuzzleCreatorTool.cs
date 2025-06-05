using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PuzzleCubes.Core
{
    /// <summary>
    /// Herramienta del editor para crear puzzles fácilmente
    /// </summary>
    public class PuzzleCreatorTool : EditorWindow
    {
        private string puzzleName = "NewPuzzle";
        private GameObject cubePreview;
        private List<Vector3Int> cubePositions = new List<Vector3Int>();
        private Camera previewCamera;
        private RenderTexture renderTexture;
        private Texture2D capturedTexture;
        
        private Vector2 scrollPos;
        private bool showGrid = true;
        private int gridSize = 4;
        
        [MenuItem("Tools/Puzzle Cubes/Puzzle Creator")]
        public static void ShowWindow()
        {
            GetWindow<PuzzleCreatorTool>("Puzzle Creator");
        }
        
        private void OnEnable()
        {
            // Crear preview cube
            if (!cubePreview)
            {
                cubePreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubePreview.transform.localScale = Vector3.one * 0.1f;
                cubePreview.hideFlags = HideFlags.HideAndDontSave;
            }
            
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            if (cubePreview)
                DestroyImmediate(cubePreview);
                
            if (renderTexture)
                DestroyImmediate(renderTexture);
                
            SceneView.duringSceneGui -= OnSceneGUI;
        }
        
        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            EditorGUILayout.LabelField("Puzzle Creator", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Configuración básica
            puzzleName = EditorGUILayout.TextField("Puzzle Name", puzzleName);
            gridSize = EditorGUILayout.IntSlider("Grid Size", gridSize, 2, 8);
            showGrid = EditorGUILayout.Toggle("Show Grid", showGrid);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Cube Count: {cubePositions.Count}");
            
            // Botones de acción
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Clear All"))
            {
                cubePositions.Clear();
                SceneView.RepaintAll();
            }
            
            if (GUILayout.Button("Center Structure"))
            {
                CenterStructure();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Captura y creación
            if (GUILayout.Button("Capture Preview", GUILayout.Height(30)))
            {
                CapturePreview();
            }
            
            if (capturedTexture != null)
            {
                EditorGUILayout.LabelField("Preview:");
                GUILayout.Label(capturedTexture, GUILayout.Width(200), GUILayout.Height(200));
            }
            
            EditorGUILayout.Space();
            
            GUI.enabled = cubePositions.Count > 0 && capturedTexture != null;
            if (GUILayout.Button("Create Puzzle Asset", GUILayout.Height(40)))
            {
                CreatePuzzleAsset();
            }
            GUI.enabled = true;
            
            // Lista de posiciones
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cube Positions:", EditorStyles.boldLabel);
            
            for (int i = 0; i < cubePositions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(30));
                EditorGUILayout.LabelField(cubePositions[i].ToString());
                
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    cubePositions.RemoveAt(i);
                    SceneView.RepaintAll();
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;
            
            // Dibujar grid
            if (showGrid)
            {
                DrawGrid();
            }
            
            // Dibujar cubos existentes
            DrawCubes();
            
            // Manejar input
            HandleSceneInput(e, sceneView);
        }
        
        private void DrawGrid()
        {
            Handles.color = new Color(0, 1, 0, 0.2f);
            
            float cubeSize = 0.1f;
            int halfGrid = gridSize / 2;
            
            // Dibujar plano base
            for (int x = -halfGrid; x <= halfGrid; x++)
            {
                Vector3 start = new Vector3(x * cubeSize, 0, -halfGrid * cubeSize);
                Vector3 end = new Vector3(x * cubeSize, 0, halfGrid * cubeSize);
                Handles.DrawLine(start, end);
            }
            
            for (int z = -halfGrid; z <= halfGrid; z++)
            {
                Vector3 start = new Vector3(-halfGrid * cubeSize, 0, z * cubeSize);
                Vector3 end = new Vector3(halfGrid * cubeSize, 0, z * cubeSize);
                Handles.DrawLine(start, end);
            }
        }
        
        private void DrawCubes()
        {
            Handles.color = Color.cyan;
            
            foreach (var pos in cubePositions)
            {
                Vector3 worldPos = new Vector3(pos.x * 0.1f, pos.y * 0.1f, pos.z * 0.1f);
                Handles.DrawWireCube(worldPos, Vector3.one * 0.1f);
            }
        }
        
        private void HandleSceneInput(Event e, SceneView sceneView)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                
                // Detectar posición en grid
                Vector3Int gridPos = GetGridPosition(ray);
                
                if (!cubePositions.Contains(gridPos))
                {
                    cubePositions.Add(gridPos);
                    e.Use();
                    Repaint();
                }
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && e.control)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                
                // Remover cubo
                Vector3Int gridPos = GetGridPosition(ray);
                
                if (cubePositions.Remove(gridPos))
                {
                    e.Use();
                    Repaint();
                }
            }
        }
        
        private Vector3Int GetGridPosition(Ray ray)
        {
            // Encontrar intersección con planos
            float distance;
            Vector3 point = Vector3.zero;
            
            // Probar con plano Y=0
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance))
            {
                point = ray.GetPoint(distance);
            }
            
            // Convertir a posición de grid
            return new Vector3Int(
                Mathf.RoundToInt(point.x / 0.1f),
                Mathf.RoundToInt(point.y / 0.1f),
                Mathf.RoundToInt(point.z / 0.1f)
            );
        }
        
        private void CenterStructure()
        {
            if (cubePositions.Count == 0) return;
            
            // Calcular centro
            Vector3 center = Vector3.zero;
            foreach (var pos in cubePositions)
            {
                center += pos;
            }
            center /= cubePositions.Count;
            
            Vector3Int centerInt = new Vector3Int(
                Mathf.RoundToInt(center.x),
                Mathf.RoundToInt(center.y),
                Mathf.RoundToInt(center.z)
            );
            
            // Mover todas las posiciones
            List<Vector3Int> newPositions = new List<Vector3Int>();
            foreach (var pos in cubePositions)
            {
                newPositions.Add(pos - centerInt);
            }
            
            cubePositions = newPositions;
            SceneView.RepaintAll();
        }
        
        private void CapturePreview()
        {
            // Crear cámara temporal
            GameObject tempCameraObj = new GameObject("TempCamera");
            Camera tempCamera = tempCameraObj.AddComponent<Camera>();
            
            // Configurar cámara
            tempCamera.orthographic = true;
            tempCamera.orthographicSize = gridSize * 0.1f;
            tempCamera.backgroundColor = Color.gray;
            tempCamera.clearFlags = CameraClearFlags.SolidColor;
            
            // Posicionar cámara
            tempCamera.transform.position = new Vector3(1, 1, -1).normalized * 2f;
            tempCamera.transform.LookAt(Vector3.zero);
            
            // Crear objetos temporales
            List<GameObject> tempCubes = new List<GameObject>();
            foreach (var pos in cubePositions)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = new Vector3(pos.x * 0.1f, pos.y * 0.1f, pos.z * 0.1f);
                cube.transform.localScale = Vector3.one * 0.1f;
                tempCubes.Add(cube);
            }
            
            // Render
            renderTexture = new RenderTexture(512, 512, 24);
            tempCamera.targetTexture = renderTexture;
            tempCamera.Render();
            
            // Capturar textura
            RenderTexture.active = renderTexture;
            capturedTexture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            capturedTexture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            capturedTexture.Apply();
            RenderTexture.active = null;
            
            // Limpiar
            foreach (var cube in tempCubes)
            {
                DestroyImmediate(cube);
            }
            DestroyImmediate(tempCameraObj);
            DestroyImmediate(renderTexture);
        }
        
        private void CreatePuzzleAsset()
        {
            // Crear ScriptableObject
            PuzzleDefinition puzzleDefinition = CreateInstance<PuzzleDefinition>();
            
            // Crear PuzzleData
            PuzzleData puzzleData = new PuzzleData(puzzleName);
            foreach (var pos in cubePositions)
            {
                puzzleData.AddCubePosition(pos);
            }
            puzzleData.NormalizePositions();
            
            // Guardar textura
            string texturePath = $"Assets/PuzzleCubes/Textures/{puzzleName}_Preview.png";
            System.IO.Directory.CreateDirectory("Assets/PuzzleCubes/Textures");
            byte[] pngData = capturedTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(texturePath, pngData);
            AssetDatabase.ImportAsset(texturePath);
            
            Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            
            // Configurar puzzle
            var serializedObject = new SerializedObject(puzzleDefinition);
            serializedObject.FindProperty("puzzleData").FindPropertyRelative("cubePositions").arraySize = puzzleData.CubePositions.Count;
            for (int i = 0; i < puzzleData.CubePositions.Count; i++)
            {
                var element = serializedObject.FindProperty("puzzleData").FindPropertyRelative("cubePositions").GetArrayElementAtIndex(i);
                element.vector3IntValue = puzzleData.CubePositions[i];
            }
            serializedObject.FindProperty("puzzleData").FindPropertyRelative("referenceImage").objectReferenceValue = savedTexture;
            serializedObject.FindProperty("puzzleData").FindPropertyRelative("puzzleName").stringValue = puzzleName;
            serializedObject.ApplyModifiedProperties();
            
            // Guardar asset
            string assetPath = $"Assets/PuzzleCubes/Puzzles/{puzzleName}.asset";
            System.IO.Directory.CreateDirectory("Assets/PuzzleCubes/Puzzles");
            AssetDatabase.CreateAsset(puzzleDefinition, assetPath);
            AssetDatabase.SaveAssets();
            
            EditorUtility.DisplayDialog("Success", $"Puzzle '{puzzleName}' created successfully!", "OK");
            
            // Limpiar
            cubePositions.Clear();
            capturedTexture = null;
            puzzleName = "NewPuzzle";
        }
    }
}