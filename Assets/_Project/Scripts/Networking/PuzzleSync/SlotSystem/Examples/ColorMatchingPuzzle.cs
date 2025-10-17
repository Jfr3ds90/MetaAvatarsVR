using System.Collections.Generic;
using UnityEngine;
using HackMonkeys.Debugging;

namespace MetaAvatarsVR.Networking.PuzzleSync.SlotSystem.Examples
{
    public class ColorMatchingPuzzle : NetworkedSlotPuzzleController
    {
        [Header("Color Matching Configuration")]
        [SerializeField] private Color[] _availableColors;
        [SerializeField] private bool _showHints = true;
        [SerializeField] private float _hintDuration = 5f;
        
        protected override void GenerateExpectedPattern()
        {
            _expectedPattern = new List<int>();
            
            for (int i = 0; i < _slots.Length; i++)
            {
                _expectedPattern.Add(i % _items.Length);
            }
            
            if (_showHints)
            {
                StartCoroutine(ShowColorHints());
            }
            
            AdvancedDebugSystem.Log($"[ColorMatchingPuzzle] Color pattern generated", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        }
        
        private System.Collections.IEnumerator ShowColorHints()
        {
            foreach (var slot in _slots)
            {
                if (slot != null && slot is ColorSlot colorSlot)
                {
                    colorSlot.ShowHintColor(_hintDuration);
                }
            }
            
            yield return new WaitForSeconds(_hintDuration);
            
            foreach (var slot in _slots)
            {
                if (slot != null && slot is ColorSlot colorSlot)
                {
                    colorSlot.HideHintColor();
                }
            }
        }
    }
    
    public class ColorSlot : NetworkedSlot
    {
        [Header("Color Slot")]
        [SerializeField] private Light _hintLight;
        [SerializeField] private Color _targetColor;
        
        public void ShowHintColor(float duration)
        {
            if (_hintLight != null)
            {
                _hintLight.color = _targetColor;
                _hintLight.enabled = true;
            }
        }
        
        public void HideHintColor()
        {
            if (_hintLight != null)
            {
                _hintLight.enabled = false;
            }
        }
    }
    
    public class ColorItem : NetworkedSlottableItem
    {
        [Header("Color Item")]
        [SerializeField] private Color _itemColor;
        
        protected override void OnSpawnedCustom()
        {
            base.OnSpawnedCustom();
            
            if (_meshRenderer != null)
            {
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                props.SetColor("_Color", _itemColor);
                _meshRenderer.SetPropertyBlock(props);
            }
        }
    }
}