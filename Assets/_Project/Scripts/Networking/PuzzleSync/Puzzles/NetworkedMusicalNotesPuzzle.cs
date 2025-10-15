using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using MetaAvatarsVR.Networking.PuzzleSync.SlotSystem;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedMusicalNotesPuzzle : NetworkedSlotPuzzleController
    {
        [Header("Musical Puzzle Configuration")]
        [SerializeField] private bool _keepPatternVisible = false;
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
        [Networked, Capacity(7)] public NetworkArray<int> NetworkedPattern { get; }
        
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
            
            if (Runner.IsSharedModeMasterClient)
            {
                PatternShown = false;
                PianoPhaseActive = false;
                
                Debug.Log($"[NetworkedMusicalNotesPuzzle] Master Client initializing puzzle");
                
                // Generate pattern first, then display it after a short delay to ensure sync
                StartCoroutine(DelayedPatternDisplay());
            }
            else
            {
                Debug.Log($"[NetworkedMusicalNotesPuzzle] Non-Master Client waiting for pattern sync");
                
                // Non-master clients also need to configure their items
                StartCoroutine(ConfigureItemsAfterSync());
            }
        }
        
        private System.Collections.IEnumerator DelayedPatternDisplay()
        {
            // Wait a frame to ensure the pattern is synced across network
            yield return null;
            
            if (_patternDisplay != null)
            {
                StartPatternDisplay();
            }
        }
        
        private System.Collections.IEnumerator ConfigureItemsAfterSync()
        {
            // Wait a frame for network state to sync
            yield return null;
            
            // Reconstruct pattern from network
            _expectedPattern = new List<int>();
            for (int i = 0; i < NetworkedPattern.Length; i++)
            {
                if (NetworkedPattern[i] >= 0)
                {
                    _expectedPattern.Add(NetworkedPattern[i]);
                }
            }
            
            if (_expectedPattern.Count > 0)
            {
                Debug.Log($"[NetworkedMusicalNotesPuzzle] Client pattern synced: {string.Join(", ", _expectedPattern.Select(i => _noteNames[i % _noteNames.Length]))}");
                ConfigureSlots();
                ConfigureItems();
            }
        }
        
        protected override void GenerateExpectedPattern()
        {
            _expectedPattern = new List<int>();
            
            // Inicializar el NetworkedPattern con valores negativos
            for (int i = 0; i < NetworkedPattern.Length; i++)
            {
                NetworkedPattern.Set(i, -1);
            }
            
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
            
            // Sync the pattern to network array
            for (int i = 0; i < _expectedPattern.Count && i < NetworkedPattern.Length; i++)
            {
                NetworkedPattern.Set(i, _expectedPattern[i]);
            }
            
            Debug.Log($"[NetworkedMusicalNotesPuzzle] Pattern generated: {string.Join(", ", _expectedPattern.Select(i => _noteNames[i % _noteNames.Length]))}");
        }
        
        private void StartPatternDisplay()
        {
            if (!_keepPatternVisible)
            {
                PatternTimer = TickTimer.CreateFromSeconds(Runner, _patternDisplayDuration);
            }
            PatternShown = true;
            RPC_ShowPattern();
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_ShowPattern()
        {
            Debug.Log($"[NetworkedMusicalNotesPuzzle] RPC_ShowPattern called on client (IsMaster: {Runner.IsSharedModeMasterClient})");
            
            // Reconstruct the pattern from networked array for clients
            if (_expectedPattern == null || _expectedPattern.Count == 0)
            {
                _expectedPattern = new List<int>();
                for (int i = 0; i < NetworkedPattern.Length; i++)
                {
                    if (NetworkedPattern[i] >= 0) // Valid pattern index
                    {
                        _expectedPattern.Add(NetworkedPattern[i]);
                    }
                }
                Debug.Log($"[NetworkedMusicalNotesPuzzle] Pattern reconstructed from network: {string.Join(", ", _expectedPattern.Select(i => _noteNames[i % _noteNames.Length]))}");
            }
            
            if (_patternDisplay != null)
            {
                _patternDisplay.SetActive(true);
                OnPatternDisplayStarted?.Invoke();
                
                if (_patternRenderer != null && _colorMaterials != null && _expectedPattern != null)
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
            
            Debug.Log("[NetworkedMusicalNotesPuzzle] Pattern displayed to all players");
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_HidePattern()
        {
            if (_patternDisplay != null)
            {
                _patternDisplay.SetActive(false);
                OnPatternDisplayEnded?.Invoke();
            }
            
            Debug.Log("[NetworkedMusicalNotesPuzzle] Pattern hidden");
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
            if (!Runner.IsSharedModeMasterClient) return;
            
            PianoPhaseActive = true;
            BuildExpectedPianoSequence();
            RPC_ActivatePiano();
        }
        
        private void BuildExpectedPianoSequence()
        {
            List<string> notes = new List<string>();
            
            // Usar el patrón original que se mostró, no las notas colocadas
            // El patrón original está en NetworkedPattern
            for (int i = 0; i < NetworkedPattern.Length; i++)
            {
                int noteIndex = NetworkedPattern[i];
                if (noteIndex >= 0 && noteIndex < _noteNames.Length)
                {
                    notes.Add(_noteNames[noteIndex]);
                }
            }
            
            if (_piano != null && notes.Count > 0)
            {
                string sequence = string.Join(",", notes);
                _piano.SetExpectedSequence(sequence);
                Debug.Log($"[NetworkedMusicalNotesPuzzle] Piano sequence set from original pattern: {sequence}");
            }
            else
            {
                Debug.LogWarning($"[NetworkedMusicalNotesPuzzle] Could not build piano sequence. Notes count: {notes.Count}");
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_ActivatePiano()
        {
            // Reconstruir la secuencia esperada desde NetworkedPattern para asegurar sincronización
            List<string> expectedNotes = new List<string>();
            for (int i = 0; i < NetworkedPattern.Length; i++)
            {
                int noteIndex = NetworkedPattern[i];
                if (noteIndex >= 0 && noteIndex < _noteNames.Length)
                {
                    expectedNotes.Add(_noteNames[noteIndex]);
                }
            }
            
            if (_piano != null)
            {
                _piano.gameObject.SetActive(true);
                
                // Establecer la secuencia esperada en todos los clientes
                if (expectedNotes.Count > 0)
                {
                    string sequence = string.Join(",", expectedNotes);
                    _piano.SetExpectedSequence(sequence);
                    Debug.Log($"[NetworkedMusicalNotesPuzzle] Piano activated with sequence: {sequence}");
                }
                
                _piano.ActivatePiano();
                OnPianoPhaseStarted?.Invoke();
            }
            
            Debug.Log("[NetworkedMusicalNotesPuzzle] Piano phase activated for all clients");
        }
        
        private void OnPianoComplete()
        {
            if (Runner.IsSharedModeMasterClient && PianoPhaseActive)
            {
                TransitionToState(SlotPuzzleState.Completed);
            }
        }
        
        private void OnPianoFailed()
        {
            Debug.Log("[NetworkedMusicalNotesPuzzle] Piano sequence failed, try again");
        }
        
        protected override void OnFixedUpdateCustom()
        {
            base.OnFixedUpdateCustom();
            
            // Solo ocultar el patrón si no está configurado para permanecer visible
            if (!_keepPatternVisible && PatternShown && PatternTimer.Expired(Runner))
            {
                PatternShown = false;
                RPC_HidePattern();
            }
        }
        
        public override void ResetPuzzle()
        {
            base.ResetPuzzle();
            
            if (Runner.IsSharedModeMasterClient)
            {
                PianoPhaseActive = false;
                
                // Solo ocultar el patrón si no está configurado para permanecer visible
                if (!_keepPatternVisible)
                {
                    PatternShown = false;
                    RPC_HidePattern();
                }
                
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
                    Debug.Log($"[NetworkedMusicalNotesPuzzle] SetPuzzleController called on {item.name}");
                }
            }
            
            // NOTA: Las notas musicales ya tienen sus propios materiales configurados
            // No sobrescribir los materiales de las notas musicales ya que tienen geometría compleja
            // con letras que indican la nota musical correspondiente
            
            // Solo logging para debug sin cambiar materiales
            if (_expectedPattern != null && _expectedPattern.Count > 0)
            {
                for (int i = 0; i < _expectedPattern.Count && i < _items.Length; i++)
                {
                    int colorIndex = _expectedPattern[i];
                    if (colorIndex >= 0 && colorIndex < _items.Length && _items[colorIndex] is NetworkedMusicalNote musicalNote)
                    {
                        Debug.Log($"[NetworkedMusicalNotesPuzzle] Note {musicalNote.name} has pattern index {colorIndex}");
                    }
                }
            }
        }
        
        #if UNITY_EDITOR
        protected override void OnGUI()
        {
            base.OnGUI();
            
            if (!_debugMode || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 260, 300, 120));
            GUILayout.Label("Musical Puzzle Specific:");
            GUILayout.Label($"Pattern Shown: {PatternShown}");
            GUILayout.Label($"Keep Pattern Visible: {_keepPatternVisible}");
            GUILayout.Label($"Piano Phase: {PianoPhaseActive}");
            
            if (Runner.IsSharedModeMasterClient)
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