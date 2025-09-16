// ============================================
// NetworkedNoteSlot.cs - Simplificado
// ============================================
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    /// <summary>
    /// Slot donde se coloca una nota musical
    /// </summary>
    public class NetworkedNoteSlot : NetworkBehaviour
    {
        [Header("Slot Configuration")]
        [SerializeField] private int _slotIndex = 0;
        [SerializeField] private Transform _noteAnchor;
        
        [Header("Visual Feedback")]
        [SerializeField] private MeshRenderer _slotRenderer;
        [SerializeField] private Material _emptyMaterial;
        [SerializeField] private Material _correctMaterial;
        [SerializeField] private Material _incorrectMaterial;
        
        [Header("Network State")]
        [Networked] public int SlotIndex { get; set; }
        [Networked] public int ExpectedColorIndex { get; set; }
        [Networked] public NetworkBool IsOccupied { get; set; }
        [Networked] public int PlacedNoteIndex { get; set; }
        [Networked] public NetworkBool IsCorrect { get; set; }
        
        [Header("Events")]
        public UnityEvent OnCorrectPlacement = new UnityEvent();
        public UnityEvent OnIncorrectPlacement = new UnityEvent();
        
        private NetworkedMusicalNote _currentNote;
        private AudioSource _audioSource;
        
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            if (_slotRenderer == null)
                _slotRenderer = GetComponent<MeshRenderer>();
                
            if (_noteAnchor == null)
                _noteAnchor = transform;
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                SlotIndex = _slotIndex;
                IsOccupied = false;
                PlacedNoteIndex = -1;
                IsCorrect = false;
            }
            
            UpdateVisual();
        }
        
        public void SetExpectedColor(int colorIndex)
        {
            ExpectedColorIndex = colorIndex;
        }
        
        public bool TryPlaceNote(NetworkedMusicalNote note)
        {
            if (IsOccupied) return false;
            
            _currentNote = note;
            IsOccupied = true;
            PlacedNoteIndex = note.NoteIndex;
            
            // Verificar si es correcto
            IsCorrect = (note.ColorIndex == ExpectedColorIndex);
            
            // Posicionar la nota
            note.transform.position = _noteAnchor.position;
            note.transform.rotation = _noteAnchor.rotation;
            
            RPC_NotifyPlacement(IsCorrect);
            
            return true;
        }
        
        public void RemoveNote()
        {
            _currentNote = null;
            IsOccupied = false;
            PlacedNoteIndex = -1;
            IsCorrect = false;
            
            UpdateVisual();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPlacement(NetworkBool correct)
        {
            if (correct)
                OnCorrectPlacement?.Invoke();
            else
                OnIncorrectPlacement?.Invoke();
                
            UpdateVisual();
        }
        
        private void UpdateVisual()
        {
            if (_slotRenderer != null)
            {
                if (!IsOccupied)
                    _slotRenderer.material = _emptyMaterial;
                else if (IsCorrect)
                    _slotRenderer.material = _correctMaterial;
                else
                    _slotRenderer.material = _incorrectMaterial;
            }
        }
        
        public void SetExpectedColorIndex(int colorIndex)
        {
            ExpectedColorIndex = colorIndex;
            if (HasStateAuthority)
            {
                // Sincronizar si es necesario
            }
        }
        
        public bool TryPlaceNote(NetworkedMusicalNote note, bool isCorrect)
        {
            if (IsOccupied) return false;
            
            IsOccupied = true;
            PlacedNoteIndex = note.GetNoteIndex();
            IsCorrect = isCorrect;
            
            // Posicionar la nota
            note.transform.position = transform.position + Vector3.up * 0.2f;
            note.transform.rotation = transform.rotation;
            
            UpdateVisual();
            
            return true;
        }
        
        public void ResetSlot()
        {
            RemoveNote();
        }
    }
}