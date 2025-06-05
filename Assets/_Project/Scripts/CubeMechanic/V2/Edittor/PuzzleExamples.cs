using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PuzzleCubes.Core
{
    /// <summary>
    /// Clase con ejemplos de puzzles predefinidos y generador
    /// </summary>
    public static class PuzzleExamples
    {
        /// <summary>
        /// Crea un puzzle en forma de L
        /// </summary>
        public static PuzzleData CreateLShape()
        {
            PuzzleData puzzle = new PuzzleData("L-Shape");
            
            // Base horizontal
            puzzle.AddCubePosition(new Vector3Int(0, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(1, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(2, 0, 0));
            
            // Parte vertical
            puzzle.AddCubePosition(new Vector3Int(0, 1, 0));
            puzzle.AddCubePosition(new Vector3Int(0, 2, 0));
            
            puzzle.NormalizePositions();
            return puzzle;
        }
        
        /// <summary>
        /// Crea un puzzle en forma de T
        /// </summary>
        public static PuzzleData CreateTShape()
        {
            PuzzleData puzzle = new PuzzleData("T-Shape");
            
            // Barra horizontal
            puzzle.AddCubePosition(new Vector3Int(-1, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(0, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(1, 0, 0));
            
            // Barra vertical
            puzzle.AddCubePosition(new Vector3Int(0, 1, 0));
            puzzle.AddCubePosition(new Vector3Int(0, 2, 0));
            
            puzzle.NormalizePositions();
            return puzzle;
        }
        
        /// <summary>
        /// Crea un puzzle de escalera simple
        /// </summary>
        public static PuzzleData CreateStairs()
        {
            PuzzleData puzzle = new PuzzleData("Simple Stairs");
            
            // Primer escalón
            puzzle.AddCubePosition(new Vector3Int(0, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(1, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(2, 0, 0));
            
            // Segundo escalón
            puzzle.AddCubePosition(new Vector3Int(1, 1, 0));
            puzzle.AddCubePosition(new Vector3Int(2, 1, 0));
            
            // Tercer escalón
            puzzle.AddCubePosition(new Vector3Int(2, 2, 0));
            
            puzzle.NormalizePositions();
            return puzzle;
        }
        
        /// <summary>
        /// Crea un puzzle de cubo 2x2x2
        /// </summary>
        public static PuzzleData CreateCube2x2()
        {
            PuzzleData puzzle = new PuzzleData("Cube 2x2x2");
            
            // Capa inferior
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    puzzle.AddCubePosition(new Vector3Int(x, 0, z));
                }
            }
            
            // Capa superior
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    puzzle.AddCubePosition(new Vector3Int(x, 1, z));
                }
            }
            
            puzzle.NormalizePositions();
            return puzzle;
        }
        
        /// <summary>
        /// Crea un puzzle de pirámide simple
        /// </summary>
        public static PuzzleData CreatePyramid()
        {
            PuzzleData puzzle = new PuzzleData("Simple Pyramid");
            
            // Base 3x3
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    puzzle.AddCubePosition(new Vector3Int(x, 0, z));
                }
            }
            
            // Nivel medio 2x2
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    puzzle.AddCubePosition(new Vector3Int(x, 1, z));
                }
            }
            
            // Punta
            puzzle.AddCubePosition(new Vector3Int(0, 2, 0));
            
            puzzle.NormalizePositions();
            return puzzle;
        }
        
        /// <summary>
        /// Crea un puzzle de silla simple
        /// </summary>
        public static PuzzleData CreateChair()
        {
            PuzzleData puzzle = new PuzzleData("Simple Chair");
            
            // Asiento
            puzzle.AddCubePosition(new Vector3Int(0, 1, 0));
            puzzle.AddCubePosition(new Vector3Int(1, 1, 0));
            puzzle.AddCubePosition(new Vector3Int(0, 1, 1));
            puzzle.AddCubePosition(new Vector3Int(1, 1, 1));
            
            // Patas
            puzzle.AddCubePosition(new Vector3Int(0, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(1, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(0, 0, 1));
            puzzle.AddCubePosition(new Vector3Int(1, 0, 1));
            
            // Respaldo
            puzzle.AddCubePosition(new Vector3Int(0, 2, 1));
            puzzle.AddCubePosition(new Vector3Int(1, 2, 1));
            puzzle.AddCubePosition(new Vector3Int(0, 3, 1));
            puzzle.AddCubePosition(new Vector3Int(1, 3, 1));
            
            puzzle.NormalizePositions();
            return puzzle;
        }
        
        /// <summary>
        /// Crea un puzzle de cruz
        /// </summary>
        public static PuzzleData CreateCross()
        {
            PuzzleData puzzle = new PuzzleData("Cross");
            
            // Centro
            puzzle.AddCubePosition(new Vector3Int(0, 0, 0));
            
            // Brazos
            puzzle.AddCubePosition(new Vector3Int(1, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(-1, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(0, 0, 1));
            puzzle.AddCubePosition(new Vector3Int(0, 0, -1));
            
            puzzle.NormalizePositions();
            return puzzle;
        }
        
        /// <summary>
        /// Crea un puzzle de zigzag
        /// </summary>
        public static PuzzleData CreateZigzag()
        {
            PuzzleData puzzle = new PuzzleData("Zigzag");
            
            // Primer segmento
            puzzle.AddCubePosition(new Vector3Int(0, 0, 0));
            puzzle.AddCubePosition(new Vector3Int(1, 0, 0));
            
            // Giro
            puzzle.AddCubePosition(new Vector3Int(1, 0, 1));
            puzzle.AddCubePosition(new Vector3Int(1, 0, 2));
            
            // Segundo giro
            puzzle.AddCubePosition(new Vector3Int(2, 0, 2));
            puzzle.AddCubePosition(new Vector3Int(3, 0, 2));
            
            puzzle.NormalizePositions();
            return puzzle;
        }
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Editor para crear puzzles de ejemplo rápidamente
    /// </summary>
    public class PuzzleExampleCreator : EditorWindow
    {
        private string savePath = "Assets/PuzzleCubes/Puzzles/Examples/";
        
        [MenuItem("Tools/Puzzle Cubes/Create Example Puzzles")]
        public static void ShowWindow()
        {
            GetWindow<PuzzleExampleCreator>("Create Example Puzzles");
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Puzzle Example Creator", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox("Click on any button to create that puzzle as a ScriptableObject asset.", MessageType.Info);
            EditorGUILayout.Space();
            
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            EditorGUILayout.Space();
            
            // Botones para crear cada tipo de puzzle
            if (GUILayout.Button("Create L-Shape", GUILayout.Height(30)))
                CreatePuzzleAsset(PuzzleExamples.CreateLShape());
                
            if (GUILayout.Button("Create T-Shape", GUILayout.Height(30)))
                CreatePuzzleAsset(PuzzleExamples.CreateTShape());
                
            if (GUILayout.Button("Create Stairs", GUILayout.Height(30)))
                CreatePuzzleAsset(PuzzleExamples.CreateStairs());
                
            if (GUILayout.Button("Create 2x2 Cube", GUILayout.Height(30)))
                CreatePuzzleAsset(PuzzleExamples.CreateCube2x2());
                
            if (GUILayout.Button("Create Pyramid", GUILayout.Height(30)))
                CreatePuzzleAsset(PuzzleExamples.CreatePyramid());
                
            if (GUILayout.Button("Create Chair", GUILayout.Height(30)))
                CreatePuzzleAsset(PuzzleExamples.CreateChair());
                
            if (GUILayout.Button("Create Cross", GUILayout.Height(30)))
                CreatePuzzleAsset(PuzzleExamples.CreateCross());
                
            if (GUILayout.Button("Create Zigzag", GUILayout.Height(30)))
                CreatePuzzleAsset(PuzzleExamples.CreateZigzag());
                
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Create ALL Examples", GUILayout.Height(40)))
            {
                CreateAllExamples();
            }
        }
        
        private void CreatePuzzleAsset(PuzzleData puzzleData)
        {
            // Crear el directorio si no existe
            if (!System.IO.Directory.Exists(savePath))
            {
                System.IO.Directory.CreateDirectory(savePath);
            }
            
            // Crear el ScriptableObject
            PuzzleDefinition puzzleDefinition = ScriptableObject.CreateInstance<PuzzleDefinition>();
            
            // Configurar los datos usando reflexión (ya que los campos son privados)
            var puzzleDefType = typeof(PuzzleDefinition);
            var puzzleDataField = puzzleDefType.GetField("puzzleData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            puzzleDataField.SetValue(puzzleDefinition, puzzleData);
            
            // Generar preview (imagen placeholder por ahora)
            Texture2D previewTexture = GeneratePreviewTexture(puzzleData);
            string texturePath = savePath + puzzleData.PuzzleName + "_Preview.png";
            SaveTextureAsPNG(previewTexture, texturePath);
            AssetDatabase.ImportAsset(texturePath);
            
            // Asignar la textura al puzzle
            var serializedObject = new SerializedObject(puzzleDefinition);
            serializedObject.FindProperty("puzzleData").FindPropertyRelative("referenceImage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            serializedObject.ApplyModifiedProperties();
            
            // Guardar el asset
            string assetPath = savePath + puzzleData.PuzzleName + ".asset";
            AssetDatabase.CreateAsset(puzzleDefinition, assetPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Created puzzle asset: {assetPath}");
            
            // Cleanup
            DestroyImmediate(previewTexture);
        }
        
        private void CreateAllExamples()
        {
            CreatePuzzleAsset(PuzzleExamples.CreateLShape());
            CreatePuzzleAsset(PuzzleExamples.CreateTShape());
            CreatePuzzleAsset(PuzzleExamples.CreateStairs());
            CreatePuzzleAsset(PuzzleExamples.CreateCube2x2());
            CreatePuzzleAsset(PuzzleExamples.CreatePyramid());
            CreatePuzzleAsset(PuzzleExamples.CreateChair());
            CreatePuzzleAsset(PuzzleExamples.CreateCross());
            CreatePuzzleAsset(PuzzleExamples.CreateZigzag());
            
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "All example puzzles created successfully!", "OK");
        }
        
        private Texture2D GeneratePreviewTexture(PuzzleData puzzleData)
        {
            // Crear una textura simple con vista isométrica del puzzle
            int textureSize = 256;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            
            // Llenar con color de fondo
            Color[] pixels = new Color[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0.2f, 0.2f, 0.2f, 1f);
            }
            
            // Dibujar cubos (vista isométrica simple)
            float scale = 20f;
            Vector2 center = new Vector2(textureSize / 2, textureSize / 2);
            
            foreach (var cubePos in puzzleData.CubePositions)
            {
                // Proyección isométrica simple
                float x = (cubePos.x - cubePos.z) * scale * 0.866f;
                float y = cubePos.y * scale + (cubePos.x + cubePos.z) * scale * 0.5f;
                
                Vector2 screenPos = center + new Vector2(x, -y);
                
                // Dibujar un cuadrado para representar el cubo
                DrawCube(pixels, textureSize, (int)screenPos.x, (int)screenPos.y, (int)(scale * 0.8f));
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
        
        private void DrawCube(Color[] pixels, int textureSize, int centerX, int centerY, int size)
        {
            Color cubeColor = new Color(0.5f, 0.7f, 1f, 1f);
            int halfSize = size / 2;
            
            for (int x = -halfSize; x < halfSize; x++)
            {
                for (int y = -halfSize; y < halfSize; y++)
                {
                    int px = centerX + x;
                    int py = centerY + y;
                    
                    if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
                    {
                        pixels[py * textureSize + px] = cubeColor;
                    }
                }
            }
        }
        
        private void SaveTextureAsPNG(Texture2D texture, string path)
        {
            byte[] pngData = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, pngData);
        }
    }
#endif
}