using UnityEngine;
using Fusion;

namespace MetaAvatarsVR.Networking.PuzzleSync.SlotSystem
{
    public interface ISlottable
    {
        int ItemId { get; }
        Transform Transform { get; }
        bool IsPlaced { get; }
        int CurrentSlotIndex { get; }
        
        void OnPlacedInSlot(int slotIndex, bool isCorrect);
        void OnRemovedFromSlot(int slotIndex);
        void SetItemId(int id);
        void ResetToOrigin();
    }
}