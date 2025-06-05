using UnityEngine;
using System.Collections.Generic;

namespace PuzzleCubes.Core
{
    /// <summary>
    /// Gestiona la matriz 3D dinámica de cubos
    /// </summary>
    public class CubeGrid : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int maxGridSize = 4;
        [SerializeField] private float cubeSize = 0.1f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGrid = true;
        [SerializeField] private Color gridColor = new Color(0, 1, 0, 0.3f);
        [SerializeField] private Color occupiedColor = new Color(1, 0, 0, 0.5f);
        
        private Dictionary<Vector3Int, SnapCube> grid = new Dictionary<Vector3Int, SnapCube>();
        private Vector3Int minBounds = Vector3Int.zero;
        private Vector3Int maxBounds = Vector3Int.zero;
        
        public float CubeSize => cubeSize;
        public Dictionary<Vector3Int, SnapCube> Grid => grid;
        
        /// <summary>
        /// Convierte posición del mundo a coordenadas de la grilla
        /// </summary>
        public Vector3Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            return new Vector3Int(
                Mathf.RoundToInt(localPos.x / cubeSize),
                Mathf.RoundToInt(localPos.y / cubeSize),
                Mathf.RoundToInt(localPos.z / cubeSize)
            );
        }
        
        /// <summary>
        /// Convierte coordenadas de la grilla a posición del mundo
        /// </summary>
        public Vector3 GridToWorld(Vector3Int gridPos)
        {
            Vector3 localPos = new Vector3(
                gridPos.x * cubeSize,
                gridPos.y * cubeSize,
                gridPos.z * cubeSize
            );
            return transform.TransformPoint(localPos);
        }
        
        /// <summary>
        /// Registra un cubo en la grilla
        /// </summary>
        public bool RegisterCube(SnapCube cube, Vector3Int gridPos)
        {
            if (grid.ContainsKey(gridPos))
                return false;
            
            // Verificar límites de la grilla
            if (Mathf.Abs(gridPos.x) > maxGridSize || 
                Mathf.Abs(gridPos.y) > maxGridSize || 
                Mathf.Abs(gridPos.z) > maxGridSize)
                return false;
            
            grid[gridPos] = cube;
            UpdateBounds();
            return true;
        }
        
        /// <summary>
        /// Desregistra un cubo de la grilla
        /// </summary>
        public void UnregisterCube(Vector3Int gridPos)
        {
            if (grid.Remove(gridPos))
            {
                UpdateBounds();
            }
        }
        
        /// <summary>
        /// Obtiene las posiciones válidas de snap para un cubo
        /// </summary>
        public List<Vector3Int> GetValidSnapPositions(Vector3 worldPosition)
        {
            List<Vector3Int> validPositions = new List<Vector3Int>();
            Vector3Int currentGridPos = WorldToGrid(worldPosition);
            
            // Si no hay cubos, solo la posición base (0,0,0) es válida
            if (grid.Count == 0)
            {
                validPositions.Add(Vector3Int.zero);
                return validPositions;
            }
            
            // Buscar posiciones adyacentes a cubos existentes
            foreach (var kvp in grid)
            {
                Vector3Int[] neighbors = GetNeighborPositions(kvp.Key);
                foreach (var neighbor in neighbors)
                {
                    if (!grid.ContainsKey(neighbor) && IsWithinBounds(neighbor))
                    {
                        validPositions.Add(neighbor);
                    }
                }
            }
            
            return validPositions;
        }
        
        /// <summary>
        /// Encuentra la posición de snap más cercana
        /// </summary>
        public (bool found, Vector3Int position, Vector3 worldPos) FindNearestSnapPosition(Vector3 worldPosition, float snapDistance)
        {
            List<Vector3Int> validPositions = GetValidSnapPositions(worldPosition);
            
            if (validPositions.Count == 0)
                return (false, Vector3Int.zero, Vector3.zero);
            
            float minDistance = float.MaxValue;
            Vector3Int nearestGridPos = Vector3Int.zero;
            Vector3 nearestWorldPos = Vector3.zero;
            
            foreach (var gridPos in validPositions)
            {
                Vector3 snapWorldPos = GridToWorld(gridPos);
                float distance = Vector3.Distance(worldPosition, snapWorldPos);
                
                if (distance < minDistance && distance <= snapDistance)
                {
                    minDistance = distance;
                    nearestGridPos = gridPos;
                    nearestWorldPos = snapWorldPos;
                }
            }
            
            return minDistance <= snapDistance ? 
                (true, nearestGridPos, nearestWorldPos) : 
                (false, Vector3Int.zero, Vector3.zero);
        }
        
        /// <summary>
        /// Obtiene las 6 posiciones vecinas (caras del cubo)
        /// </summary>
        private Vector3Int[] GetNeighborPositions(Vector3Int pos)
        {
            return new Vector3Int[]
            {
                pos + Vector3Int.up,
                pos + Vector3Int.down,
                pos + Vector3Int.left,
                pos + Vector3Int.right,
                pos + Vector3Int.forward,
                pos + Vector3Int.back
            };
        }
        
        /// <summary>
        /// Verifica si una posición está dentro de los límites permitidos
        /// </summary>
        private bool IsWithinBounds(Vector3Int pos)
        {
            return Mathf.Abs(pos.x) <= maxGridSize && 
                   Mathf.Abs(pos.y) <= maxGridSize && 
                   Mathf.Abs(pos.z) <= maxGridSize;
        }
        
        /// <summary>
        /// Actualiza los límites de la estructura actual
        /// </summary>
        private void UpdateBounds()
        {
            if (grid.Count == 0)
            {
                minBounds = Vector3Int.zero;
                maxBounds = Vector3Int.zero;
                return;
            }
            
            bool first = true;
            foreach (var pos in grid.Keys)
            {
                if (first)
                {
                    minBounds = pos;
                    maxBounds = pos;
                    first = false;
                }
                else
                {
                    minBounds = Vector3Int.Min(minBounds, pos);
                    maxBounds = Vector3Int.Max(maxBounds, pos);
                }
            }
        }
        
        /// <summary>
        /// Obtiene los datos actuales del puzzle
        /// </summary>
        public PuzzleData GetCurrentPuzzleData()
        {
            PuzzleData data = new PuzzleData("Current");
            
            foreach (var pos in grid.Keys)
            {
                data.AddCubePosition(pos);
            }
            
            data.NormalizePositions();
            return data;
        }
        
        /// <summary>
        /// Limpia toda la grilla
        /// </summary>
        public void ClearGrid()
        {
            // Destruir todos los cubos
            foreach (var cube in grid.Values)
            {
                if (cube != null)
                    Destroy(cube.gameObject);
            }
            
            grid.Clear();
            UpdateBounds();
        }
        
        #region Debug Visualization
        
        private void OnDrawGizmos()
        {
            if (!showDebugGrid) return;
            
            // Dibujar cubos ocupados
            Gizmos.color = occupiedColor;
            foreach (var kvp in grid)
            {
                Vector3 worldPos = GridToWorld(kvp.Key);
                Gizmos.DrawWireCube(worldPos, Vector3.one * cubeSize * 0.9f);
            }
            
            // Dibujar posiciones válidas de snap
            Gizmos.color = gridColor;
            List<Vector3Int> validPositions = GetValidSnapPositions(transform.position);
            foreach (var pos in validPositions)
            {
                Vector3 worldPos = GridToWorld(pos);
                Gizmos.DrawWireCube(worldPos, Vector3.one * cubeSize * 0.8f);
            }
            
            // Dibujar límites de la grilla
            if (grid.Count > 0)
            {
                Gizmos.color = Color.yellow;
                Vector3 minWorld = GridToWorld(minBounds) - Vector3.one * cubeSize * 0.5f;
                Vector3 maxWorld = GridToWorld(maxBounds) + Vector3.one * cubeSize * 0.5f;
                Vector3 size = maxWorld - minWorld;
                Vector3 center = (minWorld + maxWorld) * 0.5f;
                Gizmos.DrawWireCube(center, size);
            }
        }
        
        #endregion
    }
}