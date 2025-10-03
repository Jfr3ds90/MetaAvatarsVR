using UnityEngine;
using MetaAvatarsVR.Networking.PuzzleSync.SlotSystem;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedMusicalNoteV2 : NetworkedSlottableItem
    {
        [Header("Musical Note Configuration")]
        [SerializeField] private string _noteName = "Do";
        [SerializeField] private AudioClip _noteSound;
        [SerializeField] private Material _noteMaterial;
        
        public string NoteName => _noteName;
        
        protected override void OnSpawnedCustom()
        {
            base.OnSpawnedCustom();
            
            if (_noteMaterial != null && _meshRenderer != null)
            {
                _meshRenderer.material = _noteMaterial;
            }
        }
        
        protected override void OnPlacedCustom(int slotIndex, bool isCorrect)
        {
            base.OnPlacedCustom(slotIndex, isCorrect);
            
            if (_noteSound != null)
            {
                PlaySound(_noteSound);
            }
            
            Debug.Log($"[NetworkedMusicalNoteV2] Note {_noteName} placed in slot {slotIndex}. Correct: {isCorrect}");
        }
        
        protected override void OnRemovedCustom(int slotIndex)
        {
            base.OnRemovedCustom(slotIndex);
            Debug.Log($"[NetworkedMusicalNoteV2] Note {_noteName} removed from slot {slotIndex}");
        }
    }
}