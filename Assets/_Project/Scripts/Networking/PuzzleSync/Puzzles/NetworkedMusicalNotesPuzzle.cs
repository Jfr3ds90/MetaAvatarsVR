using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public enum MusicalPuzzlePhase
    {
        Initialization = 0,
        PatternDisplay = 1,
        NoteCollection = 2,
        PianoSequence = 3,
        Completed = 4
    }
    

    public class NetworkedMusicalNotesPuzzle : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int _puzzleId = 100;
        [SerializeField] private float _patternDisplayDuration = 10f;
        [SerializeField] private NetworkedDoor _puzzleDoor;
        
        [Header("Pattern Display")]
        [SerializeField] private GameObject _patternDisplay; 
        [SerializeField] private MeshRenderer _patternRenderer; 
        [Header("Color Materials")]
        [SerializeField] private Material[] _colorMaterials; 
        
        [Header("Notes & Slots")]
        [SerializeField] private NetworkedMusicalNote[] _notes; 
        [SerializeField] private NetworkedNoteSlot[] _slots; 
        
        [Header("Piano")]
        [SerializeField] private NetworkedPiano _piano;
        
        [Header("Audio")]
        [SerializeField] private AudioClip _phaseCompleteSound;
        [SerializeField] private AudioClip _puzzleCompleteSound;
        [SerializeField] private AudioClip _errorSound;
        
        [Header("Network State")]
        [Networked] public MusicalPuzzlePhase CurrentPhase { get; set; }
        [Networked] public NetworkBool IsSolved { get; set; }
        [Networked] public int CorrectNotesPlaced { get; set; }
        [Networked] public TickTimer PatternTimer { get; set; }
        
        [Networked, Capacity(7)]
        public NetworkArray<int> PatternOrder { get; } 
        
        [Header("Events")]
        public UnityEvent OnPuzzleStarted = new UnityEvent();
        public UnityEvent<MusicalPuzzlePhase> OnPhaseChanged = new UnityEvent<MusicalPuzzlePhase>();
        public UnityEvent OnAllNotesPlaced = new UnityEvent();
        public UnityEvent OnPuzzleSolved = new UnityEvent();
        
        private AudioSource _audioSource;
        private readonly string[] _noteNames = { "Do", "Re", "Mi", "Fa", "Sol", "La", "Si" };
        private Material[] _originalPatternMaterials; 
        
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            if (_patternRenderer == null && _patternDisplay != null)
            {
                _patternRenderer = _patternDisplay.GetComponent<MeshRenderer>();
            }
            
            if (_patternRenderer != null)
            {
                _originalPatternMaterials = _patternRenderer.sharedMaterials;
            }
            
            ValidateComponents();
        }
        
        private void ValidateComponents()
        {
            if (_notes == null || _notes.Length != 7)
            {
                Debug.LogError($"[NetworkedMusicalNotesPuzzle] Expected 7 notes, found {_notes?.Length ?? 0}");
            }
            
            if (_slots == null || _slots.Length != 7)
            {
                Debug.LogError($"[NetworkedMusicalNotesPuzzle] Expected 7 slots, found {_slots?.Length ?? 0}");
            }
            
            if (_colorMaterials == null || _colorMaterials.Length != 7)
            {
                Debug.LogError($"[NetworkedMusicalNotesPuzzle] Expected 7 color materials, found {_colorMaterials?.Length ?? 0}");
            }
            
            if (_patternRenderer != null)
            {
                Material[] mats = _patternRenderer.sharedMaterials;
                if (mats.Length < 8)
                {
                    Debug.LogWarning($"[NetworkedMusicalNotesPuzzle] Pattern renderer should have 8 material slots (0=base, 1-7=colors). Found: {mats.Length}");
                }
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                CurrentPhase = MusicalPuzzlePhase.Initialization;
                IsSolved = false;
                CorrectNotesPlaced = 0;
                
                InitializePuzzle();
                
                if (NetworkedPuzzleManager.Instance != null)
                {
                    NetworkedPuzzleManager.Instance.RPC_RequestStartPuzzle(_puzzleId);
                }
            }
            
            SetupComponents();
        }
        
        private void InitializePuzzle()
        {
            if (!HasStateAuthority) return;
            
            GenerateRandomPatternOrder();
            
            ConfigureSlots();
            
            TransitionToPhase(MusicalPuzzlePhase.PatternDisplay);
        }
        
        private void GenerateRandomPatternOrder()
        {
            List<int> indices = Enumerable.Range(0, 7).ToList();
            
            System.Random random = new System.Random(Runner.Tick);
            
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }
            
            for (int i = 0; i < 7; i++)
            {
                PatternOrder.Set(i, indices[i]);
            }
            
            Debug.Log($"[NetworkedMusicalNotesPuzzle] Pattern order generated: {string.Join(", ", indices)}");
        }
        
        private void ConfigureSlots()
        {
            for (int i = 0; i < _slots.Length && i < 7; i++)
            {
                if (_slots[i] != null)
                {
                    int expectedColorIndex = PatternOrder.Get(i);
                    _slots[i].SetExpectedColorIndex(expectedColorIndex);
                    
                    Debug.Log($"[NetworkedMusicalNotesPuzzle] Slot {i} expects color index {expectedColorIndex} ({_noteNames[expectedColorIndex]})");
                }
            }
        }
        
        private void SetupComponents()
        {
            
            for (int i = 0; i < _notes.Length && i < 7; i++)
            {
                if (_notes[i] != null)
                {
                    _notes[i].SetPuzzleController(this);
                    _notes[i].SetNoteIndex(i); 
                    
                    Debug.Log($"[NetworkedMusicalNotesPuzzle] Note {_noteNames[i]} configured with index {i}");
                }
            }
            
            if (_piano != null)
            {
                _piano.OnSequenceCompleted.AddListener(OnPianoComplete);
                _piano.OnSequenceFailed.AddListener(OnPianoFailed);
                _piano.gameObject.SetActive(false);
            }
        }
        
        private void TransitionToPhase(MusicalPuzzlePhase newPhase)
        {
            if (!HasStateAuthority) return;
            
            CurrentPhase = newPhase;
            RPC_NotifyPhaseChange(newPhase);
            
            switch (newPhase)
            {
                case MusicalPuzzlePhase.PatternDisplay:
                    StartPatternDisplay();
                    break;
                    
                case MusicalPuzzlePhase.NoteCollection:
                    StartNoteCollection();
                    break;
                    
                case MusicalPuzzlePhase.PianoSequence:
                    StartPianoSequence();
                    break;
                    
                case MusicalPuzzlePhase.Completed:
                    CompletePuzzle();
                    break;
            }
        }
        
        private void StartPatternDisplay()
        {
            PatternTimer = TickTimer.CreateFromSeconds(Runner, _patternDisplayDuration);
            RPC_ShowPattern();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowPattern()
        {
            if (_patternDisplay != null)
            {
                _patternDisplay.SetActive(true);
                
                if (_patternRenderer != null && _colorMaterials != null)
                {
                    Material[] materials = _patternRenderer.materials; 
                   
                    for (int i = 0; i < 7; i++)
                    {
                        int colorIndex = PatternOrder.Get(i);
                        if (colorIndex >= 0 && colorIndex < _colorMaterials.Length)
                        {
                            materials[i + 1] = _colorMaterials[colorIndex]; 
                        }
                    }
                    
                    _patternRenderer.materials = materials;
                }
            }
            
            Debug.Log("[NetworkedMusicalNotesPuzzle] Pattern displayed to all players");
        }
        
        private void StartNoteCollection()
        {
            RPC_HidePattern();
            RPC_ActivateNotes();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_HidePattern()
        {
            if (_patternDisplay != null)
            {
            }
            
            Debug.Log("[NetworkedMusicalNotesPuzzle] Pattern hidden, note collection phase started");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ActivateNotes()
        {
            foreach (var note in _notes)
            {
                if (note != null)
                {
                    note.EnableInteraction(true);
                }
            }
        }
        
        public bool TryPlaceNoteInSlot(NetworkedMusicalNote note, int slotIndex)
        {
            if (!HasStateAuthority) return false;
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
            
            var slot = _slots[slotIndex];
            if (slot == null || slot.IsOccupied) return false;
            
            
            bool isCorrect = (note.GetNoteIndex() == slot.ExpectedColorIndex);
            
            bool placed = slot.TryPlaceNote(note, isCorrect);
            
            if (placed)
            {
                if (isCorrect)
                {
                    CorrectNotesPlaced++;
                    RPC_NotifyCorrectPlacement(slotIndex, note.GetNoteIndex());
                }
                else
                {
                    RPC_NotifyIncorrectPlacement(slotIndex, note.GetNoteIndex());
                }
                
                CheckAllNotesPlaced();
            }
            
            return placed;
        }
        
        public void RemoveNoteFromSlot(int slotIndex)
        {
            if (!HasStateAuthority) return;
            
            if (slotIndex >= 0 && slotIndex < _slots.Length)
            {
                var slot = _slots[slotIndex];
                if (slot != null)
                {
                    if (slot.IsCorrect)
                    {
                        CorrectNotesPlaced--;
                    }
                    slot.RemoveNote();
                }
            }
        }
        
        private void CheckAllNotesPlaced()
        {
            if (CorrectNotesPlaced == 7)
            {
                RPC_NotifyAllNotesCorrect();
                
                StartCoroutine(DelayedPhaseTransition(2f, MusicalPuzzlePhase.PianoSequence));
            }
        }
        
        private System.Collections.IEnumerator DelayedPhaseTransition(float delay, MusicalPuzzlePhase nextPhase)
        {
            yield return new WaitForSeconds(delay);
            TransitionToPhase(nextPhase);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyCorrectPlacement(int slotIndex, int noteIndex)
        {
            Debug.Log($"[NetworkedMusicalNotesPuzzle] Correct! Note {_noteNames[noteIndex]} placed in slot {slotIndex}");
            PlaySound(_phaseCompleteSound);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyIncorrectPlacement(int slotIndex, int noteIndex)
        {
            Debug.Log($"[NetworkedMusicalNotesPuzzle] Incorrect. Note {_noteNames[noteIndex]} in slot {slotIndex}");
            PlaySound(_errorSound);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyAllNotesCorrect()
        {
            OnAllNotesPlaced?.Invoke();
            Debug.Log("[NetworkedMusicalNotesPuzzle] All notes placed correctly!");
        }
        
        private void StartPianoSequence()
        {
            BuildExpectedPianoSequence();
            RPC_ActivatePiano();
        }
        
        private void BuildExpectedPianoSequence()
        {
            string sequence = "";
            
            for (int i = 0; i < 7; i++)
            {
                if (_slots[i] != null && _slots[i].IsOccupied)
                {
                    int noteIndex = _slots[i].PlacedNoteIndex;
                    if (noteIndex >= 0 && noteIndex < 7)
                    {
                        sequence += _noteNames[noteIndex];
                    }
                }
            }
            
            if (_piano != null)
            {
                _piano.SetExpectedSequence(sequence);
                Debug.Log($"[NetworkedMusicalNotesPuzzle] Piano sequence set: {sequence}");
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ActivatePiano()
        {
            if (_piano != null)
            {
                _piano.gameObject.SetActive(true);
                _piano.ActivatePiano();
            }
            
            Debug.Log("[NetworkedMusicalNotesPuzzle] Piano activated");
        }
        
        private void OnPianoComplete()
        {
            if (HasStateAuthority)
            {
                TransitionToPhase(MusicalPuzzlePhase.Completed);
            }
        }
        
        private void OnPianoFailed()
        {
            Debug.Log("[NetworkedMusicalNotesPuzzle] Piano sequence failed, try again");
        }
        
        private void CompletePuzzle()
        {
            IsSolved = true;
            
            if (NetworkedPuzzleManager.Instance != null)
            {
                NetworkedPuzzleManager.Instance.RPC_CompletePuzzle(_puzzleId);
            }
            
            if (_puzzleDoor != null)
            {
                _puzzleDoor.RPC_UnlockDoor();
                _puzzleDoor.RPC_RequestOpen();
            }
            
            RPC_NotifyPuzzleSolved();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPuzzleSolved()
        {
            OnPuzzleSolved?.Invoke();
            PlaySound(_puzzleCompleteSound);
            
            Debug.Log("[NetworkedMusicalNotesPuzzle] PUZZLE COMPLETED! 🎉");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPhaseChange(MusicalPuzzlePhase phase)
        {
            OnPhaseChanged?.Invoke(phase);
            Debug.Log($"[NetworkedMusicalNotesPuzzle] Phase changed to: {phase}");
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
        
        public void ResetPuzzle()
        {
            if (!HasStateAuthority) return;
            
            CurrentPhase = MusicalPuzzlePhase.Initialization;
            IsSolved = false;
            CorrectNotesPlaced = 0;
            
            foreach (var note in _notes)
            {
                if (note != null)
                {
                    note.ResetNote();
                }
            }
            
            foreach (var slot in _slots)
            {
                if (slot != null)
                {
                    slot.ResetSlot();
                }
            }
            
            if (_piano != null)
            {
                _piano.ResetSequence();
                _piano.gameObject.SetActive(false);
            }
            
            InitializePuzzle();
        }
        
        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (CurrentPhase == MusicalPuzzlePhase.PatternDisplay)
                {
                    if (PatternTimer.Expired(Runner))
                    {
                        TransitionToPhase(MusicalPuzzlePhase.NoteCollection);
                    }
                }
            }
        }
        
        #if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool _debugMode = false;
        
        private void OnGUI()
        {
            if (!_debugMode || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Musical Puzzle Debug");
            GUILayout.Label($"Phase: {CurrentPhase}");
            GUILayout.Label($"Correct Notes: {CorrectNotesPlaced}/7");
            GUILayout.Label($"Pattern: {string.Join(",", Enumerable.Range(0, 7).Select(i => PatternOrder.Get(i)))}");
            
            if (HasStateAuthority)
            {
                if (GUILayout.Button("Skip to Piano"))
                {
                    CorrectNotesPlaced = 7;
                    TransitionToPhase(MusicalPuzzlePhase.PianoSequence);
                }
                
                if (GUILayout.Button("Complete Puzzle"))
                {
                    TransitionToPhase(MusicalPuzzlePhase.Completed);
                }
                
                if (GUILayout.Button("Reset Puzzle"))
                {
                    ResetPuzzle();
                }
            }
            GUILayout.EndArea();
        }
        #endif
    }
}