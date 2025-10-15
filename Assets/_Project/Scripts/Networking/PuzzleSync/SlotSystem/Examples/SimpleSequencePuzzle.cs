using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MetaAvatarsVR.Networking.PuzzleSync.SlotSystem.Examples
{
    public class SimpleSequencePuzzle : NetworkedSlotPuzzleController
    {
        [Header("Sequence Configuration")]
        [SerializeField] private bool _randomizeSequence = true;
        [SerializeField] private int _sequenceLength = 5;
        
        protected override void GenerateExpectedPattern()
        {
            _expectedPattern = new List<int>();
            
            if (_randomizeSequence)
            {
                List<int> availableIds = Enumerable.Range(0, _items.Length).ToList();
                System.Random random = new System.Random(Runner.Tick);
                
                for (int i = 0; i < Mathf.Min(_sequenceLength, _slots.Length); i++)
                {
                    if (availableIds.Count > 0)
                    {
                        int randomIndex = random.Next(availableIds.Count);
                        _expectedPattern.Add(availableIds[randomIndex]);
                        availableIds.RemoveAt(randomIndex);
                    }
                }
            }
            else
            {
                for (int i = 0; i < Mathf.Min(_sequenceLength, _slots.Length); i++)
                {
                    _expectedPattern.Add(i);
                }
            }
            
            Debug.Log($"[SimpleSequencePuzzle] Generated pattern: {string.Join(", ", _expectedPattern)}");
        }
    }
}