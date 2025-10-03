using UnityEngine;
using MetaAvatarsVR.Networking.PuzzleSync.SlotSystem;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedNoteSlotV2 : NetworkedSlot
    {
        [Header("Musical Slot Configuration")]
        [SerializeField] private string _expectedNoteName = "";
        [SerializeField] private GameObject _noteGlowEffect;
        [SerializeField] private ParticleSystem _correctPlacementParticles;
        
        public string ExpectedNoteName => _expectedNoteName;
        
        protected override void OnItemPlacedCustom(int itemId, bool isCorrect)
        {
            base.OnItemPlacedCustom(itemId, isCorrect);
            
            if (isCorrect)
            {
                if (_noteGlowEffect != null)
                    _noteGlowEffect.SetActive(true);
                    
                if (_correctPlacementParticles != null)
                    _correctPlacementParticles.Play();
            }
        }
        
        protected override void OnItemRemovedCustom(int itemId)
        {
            base.OnItemRemovedCustom(itemId);
            
            if (_noteGlowEffect != null)
                _noteGlowEffect.SetActive(false);
                
            if (_correctPlacementParticles != null)
                _correctPlacementParticles.Stop();
        }
    }
}