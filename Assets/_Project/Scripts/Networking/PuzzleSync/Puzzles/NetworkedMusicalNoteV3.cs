using UnityEngine;
using MetaAvatarsVR.Networking.PuzzleSync.SlotSystem;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedMusicalNoteV3 : NetworkedSlottableItemV2
    {
        [Header("Musical Note Configuration")]
        [SerializeField] private string _noteName = "Do";
        [SerializeField] private AudioClip _noteSound;
        [SerializeField] private Material _noteMaterial;
        [SerializeField] private int _noteColorIndex = 0;
        
        public string NoteName => _noteName;
        public int ColorIndex => _noteColorIndex;
        
        protected override void Awake()
        {
            base.Awake();
            
            // Configurar valores predeterminados específicos para notas musicales
            if (_snapDistance < 0.3f)
                _snapDistance = 0.5f;
            
            if (string.IsNullOrEmpty(_slotTag))
                _slotTag = "NoteSlot";
            
            DebugLog($"[MusicalNote {_noteName}] Awake completed with snapDistance: {_snapDistance}, tag: {_slotTag}");
        }
        
        protected override void OnSpawnedCustom()
        {
            base.OnSpawnedCustom();
            
            if (_noteMaterial != null && _meshRenderer != null)
            {
                _meshRenderer.material = _noteMaterial;
                DebugLog($"[MusicalNote {_noteName}] Material set");
            }
            
            // Establecer el ID basado en el índice de color para el sistema de validación
            if (_itemId == 0)
            {
                _itemId = _noteColorIndex;
                SetItemId(_noteColorIndex);
            }
            
            DebugLog($"[MusicalNote {_noteName}] Spawned with ColorIndex: {_noteColorIndex}, ItemId: {_itemId}");
        }
        
        protected override void OnGrabbed()
        {
            base.OnGrabbed();
            DebugLog($"[MusicalNote {_noteName}] Grabbed by player");
            
            // Reproducir sonido de la nota al agarrarla
            if (_noteSound != null)
            {
                PlaySound(_noteSound);
            }
        }
        
        protected override void CheckNearbySlot()
        {
            DebugLog($"[MusicalNote {_noteName}] Checking for musical note slots...");
            base.CheckNearbySlot();
        }
        
        protected override void OnPlacedCustom(int slotIndex, bool isCorrect)
        {
            base.OnPlacedCustom(slotIndex, isCorrect);
            
            DebugLog($"[MusicalNote {_noteName}] Placed in slot {slotIndex}. Correct: {isCorrect}");
            
            if (_noteSound != null)
            {
                // Reproducir sonido con pitch diferente si es correcto
                if (isCorrect)
                {
                    _audioSource.pitch = 1.2f;
                }
                else
                {
                    _audioSource.pitch = 0.8f;
                }
                
                PlaySound(_noteSound);
                _audioSource.pitch = 1.0f; // Restaurar pitch
            }
            
            // Añadir efecto visual adicional
            if (isCorrect)
            {
                StartCoroutine(CorrectPlacementAnimation());
            }
        }
        
        protected override void OnRemovedCustom(int slotIndex)
        {
            base.OnRemovedCustom(slotIndex);
            DebugLog($"[MusicalNote {_noteName}] Removed from slot {slotIndex}");
        }
        
        private System.Collections.IEnumerator CorrectPlacementAnimation()
        {
            Vector3 originalScale = transform.localScale;
            float duration = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f;
                transform.localScale = originalScale * scale;
                yield return null;
            }
            
            transform.localScale = originalScale;
        }
        
        public void SetNoteData(string noteName, int colorIndex)
        {
            _noteName = noteName;
            _noteColorIndex = colorIndex;
            _itemId = colorIndex;
            
            DebugLog($"[MusicalNote] Note data set - Name: {noteName}, ColorIndex: {colorIndex}");
        }
        
        public override void ResetToOrigin()
        {
            DebugLog($"[MusicalNote {_noteName}] Reset requested");
            base.ResetToOrigin();
        }
        
        #if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // Dibujar el nombre de la nota
            if (Application.isPlaying)
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, $"Note: {_noteName}\nColor: {_noteColorIndex}");
            }
        }
        #endif
    }
}