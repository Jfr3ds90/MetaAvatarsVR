using UnityEngine;
using System.Collections.Generic;
using System;

namespace PuzzleCubes.Core
{
    /// <summary>
    /// Representa la estructura de datos de un puzzle
    /// </summary>
    [System.Serializable]
    public class PuzzleData
    {
        [SerializeField] private List<Vector3Int> cubePositions = new List<Vector3Int>();
        [SerializeField] private Texture2D referenceImage;
        [SerializeField] private string puzzleName;
        [SerializeField] private Vector3Int minBounds;
        [SerializeField] private Vector3Int maxBounds;
        
        public List<Vector3Int> CubePositions => cubePositions;
        public Texture2D ReferenceImage => referenceImage;
        public string PuzzleName => puzzleName;
        public Vector3Int MinBounds => minBounds;
        public Vector3Int MaxBounds => maxBounds;
        
        public PuzzleData(string name)
        {
            puzzleName = name;
            cubePositions = new List<Vector3Int>();
        }
        
        /// <summary>
        /// Añade una posición de cubo y actualiza los bounds
        /// </summary>
        public void AddCubePosition(Vector3Int position)
        {
            if (!cubePositions.Contains(position))
            {
                cubePositions.Add(position);
                UpdateBounds();
            }
        }
        
        /// <summary>
        /// Remueve una posición de cubo y actualiza los bounds
        /// </summary>
        public void RemoveCubePosition(Vector3Int position)
        {
            if (cubePositions.Remove(position))
            {
                UpdateBounds();
            }
        }
        
        /// <summary>
        /// Normaliza las posiciones para que el punto mínimo sea (0,0,0)
        /// </summary>
        public void NormalizePositions()
        {
            if (cubePositions.Count == 0) return;
            
            Vector3Int offset = minBounds;
            List<Vector3Int> normalizedPositions = new List<Vector3Int>();
            
            foreach (var pos in cubePositions)
            {
                normalizedPositions.Add(pos - offset);
            }
            
            cubePositions = normalizedPositions;
            UpdateBounds();
        }
        
        /// <summary>
        /// Rota la estructura 90 grados en el eje Y
        /// </summary>
        public PuzzleData RotateY90()
        {
            PuzzleData rotated = new PuzzleData(puzzleName + "_Rotated");
            
            foreach (var pos in cubePositions)
            {
                // Rotación 90° en Y: (x,y,z) -> (z,y,-x)
                Vector3Int newPos = new Vector3Int(pos.z, pos.y, -pos.x);
                rotated.AddCubePosition(newPos);
            }
            
            rotated.NormalizePositions();
            return rotated;
        }
        
        /// <summary>
        /// Genera todas las rotaciones posibles en Y (0°, 90°, 180°, 270°)
        /// </summary>
        public List<PuzzleData> GetAllYRotations()
        {
            List<PuzzleData> rotations = new List<PuzzleData>();
            PuzzleData current = this;
            
            for (int i = 0; i < 4; i++)
            {
                PuzzleData copy = new PuzzleData(puzzleName + "_Rot" + (i * 90));
                copy.cubePositions = new List<Vector3Int>(current.cubePositions);
                copy.UpdateBounds();
                copy.NormalizePositions();
                rotations.Add(copy);
                
                current = current.RotateY90();
            }
            
            return rotations;
        }
        
        /// <summary>
        /// Actualiza los límites de la estructura
        /// </summary>
        private void UpdateBounds()
        {
            if (cubePositions.Count == 0)
            {
                minBounds = Vector3Int.zero;
                maxBounds = Vector3Int.zero;
                return;
            }
            
            minBounds = cubePositions[0];
            maxBounds = cubePositions[0];
            
            foreach (var pos in cubePositions)
            {
                minBounds = Vector3Int.Min(minBounds, pos);
                maxBounds = Vector3Int.Max(maxBounds, pos);
            }
        }
        
        /// <summary>
        /// Compara si dos estructuras son iguales (mismas posiciones)
        /// </summary>
        public bool Equals(PuzzleData other)
        {
            if (other == null || cubePositions.Count != other.cubePositions.Count)
                return false;
            
            HashSet<Vector3Int> thisSet = new HashSet<Vector3Int>(cubePositions);
            HashSet<Vector3Int> otherSet = new HashSet<Vector3Int>(other.cubePositions);
            
            return thisSet.SetEquals(otherSet);
        }
        
        /// <summary>
        /// Calcula el porcentaje de similitud con otra estructura
        /// </summary>
        public float CalculateSimilarity(PuzzleData other)
        {
            if (other == null) return 0f;
            
            HashSet<Vector3Int> thisSet = new HashSet<Vector3Int>(cubePositions);
            HashSet<Vector3Int> otherSet = new HashSet<Vector3Int>(other.cubePositions);
            
            int intersection = 0;
            foreach (var pos in thisSet)
            {
                if (otherSet.Contains(pos))
                    intersection++;
            }
            
            int union = thisSet.Count + otherSet.Count - intersection;
            return union > 0 ? (float)intersection / union : 0f;
        }
    }
}