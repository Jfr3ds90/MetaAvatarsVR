using UnityEngine;

namespace PuzzleCubes.Core
{
    /// <summary>
    /// ScriptableObject para definir puzzles predefinidos
    /// </summary>
    [CreateAssetMenu(fileName = "PuzzleDefinition", menuName = "PuzzleCubes/Puzzle Definition")]
    public class PuzzleDefinition : ScriptableObject
    {
        [SerializeField] private PuzzleData puzzleData;

        [SerializeField] private float completionThreshold =
            0.95f; // 95% de similitud para completar

        public PuzzleData PuzzleData => puzzleData;
        public float CompletionThreshold => completionThreshold;

        private void OnValidate()
        {
            if (puzzleData != null)
            {
                puzzleData.NormalizePositions();
            }
        }
    }
}