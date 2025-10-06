using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using MetaAvatarsVR.Networking.PuzzleSync.SlotSystem;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedMusicalNotesPuzzleV2 : NetworkedSlotPuzzleController
    {
        [Header("Musical Puzzle Configuration")]
        [SerializeField] private float _patternDisplayDuration = 10f;
        [SerializeField] private GameObject _patternDisplay;
        [SerializeField] private MeshRenderer _patternRenderer;
        [SerializeField] private Material[] _colorMaterials;
        
        [Header("Piano Phase")]
        [SerializeField] private NetworkedPiano _piano;
        [SerializeField] private bool _requirePianoSequence = true;
        
        [Header("Musical Events")]
        public UnityEvent OnPatternDisplayStarted = new UnityEvent();
        public UnityEvent OnPatternDisplayEnded = new UnityEvent();
        public UnityEvent OnPianoPhaseStarted = new UnityEvent();
        
        [Networked] public TickTimer PatternTimer { get; set; }
        [Networked] public NetworkBool PatternShown { get; set; }
        [Networked] public NetworkBool PianoPhaseActive { get; set; }
        
        private readonly string[] _noteNames = { "Do", "Re", "Mi", "Fa", "Sol", "La", "Si" };
        private Material[] _originalPatternMaterials;
        
        protected override void Awake()
        {
            base.Awake();
            
            if (_patternRenderer != null)
            {
                _originalPatternMaterials = _patternRenderer.sharedMaterials;
            }
            
            if (_piano != null)
            {
                _piano.OnSequenceCompleted.AddListener(OnPianoComplete);
                _piano.OnSequenceFailed.AddListener(OnPianoFailed);
                _piano.gameObject.SetActive(false);
            }
        }
        
        protected override void InitializePuzzle()
        {
            base.InitializePuzzle();
            
            if (HasStateAuthority)
            {
                PatternShown = false;
                PianoPhaseActive = false;
                
                if (_patternDisplay != null)
                {
                    StartPatternDisplay();
                }
            }
        }
        
        protected override void GenerateExpectedPattern()
        {
            _expectedPattern = new List<int>();
            
            List<int> indices = Enumerable.Range(0, Mathf.Min(7, _items.Length)).ToList();
            System.Random random = new System.Random(Runner.Tick);
            
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }
            
            _expectedPattern = indices;
            
            Debug.Log($"[NetworkedMusicalNotesPuzzleV2] Pattern generated: {string.Join(", ", _expectedPattern.Select(i => _noteNames[i % _noteNames.Length]))}");
        }
        
        private void StartPatternDisplay()
        {
            PatternTimer = TickTimer.CreateFromSeconds(Runner, _patternDisplayDuration);
            PatternShown = true;
            RPC_ShowPattern();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowPattern()
        {
            if (_patternDisplay != null)
            {
                _patternDisplay.SetActive(true);
                OnPatternDisplayStarted?.Invoke();
                
                if (_patternRenderer != null && _colorMaterials != null)
                {
                    Material[] materials = _patternRenderer.materials;
                    
                    for (int i = 0; i < _expectedPattern.Count && i < materials.Length - 1; i++)
                    {
                        int colorIndex = _expectedPattern[i];
                        if (colorIndex >= 0 && colorIndex < _colorMaterials.Length)
                        {
                            materials[i + 1] = _colorMaterials[colorIndex];
                        }
                    }
                    
                    _patternRenderer.materials = materials;
                }
            }
            
            Debug.Log("[NetworkedMusicalNotesPuzzleV2] Pattern displayed to all players");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_HidePattern()
        {
            if (_patternDisplay != null)
            {
                _patternDisplay.SetActive(false);
                OnPatternDisplayEnded?.Invoke();
            }
            
            Debug.Log("[NetworkedMusicalNotesPuzzleV2] Pattern hidden");
        }
        
        protected override bool ValidateFinalConfiguration()
        {
            bool slotsValid = base.ValidateFinalConfiguration();
            
            if (slotsValid && _requirePianoSequence && !PianoPhaseActive)
            {
                StartPianoPhase();
                return false;
            }
            
            return slotsValid && (!_requirePianoSequence || PianoPhaseActive);
        }
        
        private void StartPianoPhase()
        {
            if (!HasStateAuthority) return;
            
            PianoPhaseActive = true;
            BuildExpectedPianoSequence();
            RPC_ActivatePiano();
        }
        
        private void BuildExpectedPianoSequence()
        {
            string sequence = "";
            
            foreach (var slot in _slots)
            {
                if (slot != null && slot.IsOccupied)
                {
                    var item = GetItem(slot.PlacedItemId);
                    if (item is NetworkedMusicalNoteV2 musicalNote)
                    {
                        sequence += musicalNote.NoteName;
                    }
                }
            }
            
            if (_piano != null)
            {
                _piano.SetExpectedSequence(sequence);
                Debug.Log($"[NetworkedMusicalNotesPuzzleV2] Piano sequence set: {sequence}");
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ActivatePiano()
        {
            if (_piano != null)
            {
                _piano.gameObject.SetActive(true);
                _piano.ActivatePiano();
                OnPianoPhaseStarted?.Invoke();
            }
            
            Debug.Log("[NetworkedMusicalNotesPuzzleV2] Piano phase activated");
        }
        
        private void OnPianoComplete()
        {
            if (HasStateAuthority && PianoPhaseActive)
            {
                TransitionToState(SlotPuzzleState.Completed);
            }
        }
        
        private void OnPianoFailed()
        {
            Debug.Log("[NetworkedMusicalNotesPuzzleV2] Piano sequence failed, try again");
        }
        
        protected override void OnFixedUpdateCustom()
        {
            base.OnFixedUpdateCustom();
            
            if (PatternShown && PatternTimer.Expired(Runner))
            {
                PatternShown = false;
                RPC_HidePattern();
            }
        }
        
        public override void ResetPuzzle()
        {
            base.ResetPuzzle();
            
            if (HasStateAuthority)
            {
                PianoPhaseActive = false;
                PatternShown = false;
                
                if (_piano != null)
                {
                    _piano.ResetSequence();
                    _piano.gameObject.SetActive(false);
                }
            }
        }
        
        protected override void ConfigureItems()
        {
            base.ConfigureItems();
            
            // Asegurar que TODAS las notas tengan el controller configurado
            foreach (var item in _items)
            {
                if (item != null)
                {
                    item.SetPuzzleController(this);
                    Debug.Log($"[NetworkedMusicalNotesPuzzleV2] SetPuzzleController called on {item.name}");
                }
            }
            
            for (int i = 0; i < _items.Length && i < _colorMaterials.Length; i++)
            {
                if (_items[i] is NetworkedMusicalNoteV2 musicalNote && i < _noteNames.Length)
                {
                    if (_colorMaterials[i] != null)
                    {
                        var meshRenderer = musicalNote.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            meshRenderer.material = _colorMaterials[i];
                        }
                    }
                }
            }
        }
        
        #if UNITY_EDITOR
        protected override void OnGUI()
        {
            base.OnGUI();
            
            if (!_debugMode || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 260, 300, 100));
            GUILayout.Label("Musical Puzzle Specific:");
            GUILayout.Label($"Pattern Shown: {PatternShown}");
            GUILayout.Label($"Piano Phase: {PianoPhaseActive}");
            
            if (HasStateAuthority)
            {
                if (GUILayout.Button("Skip to Piano"))
                {
                    StartPianoPhase();
                }
            }
            GUILayout.EndArea();
        }
        #endif
    }
}