using UnityEngine;
using Fusion;
using MetaAvatarsVR.Networking.PuzzleSync.Puzzles;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
    public class PuzzleSyncSetup : MonoBehaviour
    {
        [Header("Required Components")]
        [SerializeField] private NetworkedPuzzleManager _puzzleManager;
        [SerializeField] private NetworkedPuzzleValidator _puzzleValidator;
        [SerializeField] private NetworkedRandomizer[] _randomizers;
        
        [Header("Setup Configuration")]
        [SerializeField] private bool _autoSetup = true;
        [SerializeField] private bool _validateSetup = true;
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = true;
        
        private void Start()
        {
            if (_autoSetup)
            {
                SetupPuzzleSync();
            }
        }
        
        [ContextMenu("Setup Puzzle Sync System")]
        public void SetupPuzzleSync()
        {
            LogDebug("Setting up Puzzle Sync System...");
            
            // Find or create required components
            FindOrCreateComponents();
            
            // Setup NetworkedPuzzleManager
            SetupPuzzleManager();
            
            // Setup NetworkedRandomizers
            SetupRandomizers();
            
            // Validate setup
            if (_validateSetup)
            {
                ValidateSetup();
            }
            
            LogDebug("Puzzle Sync System setup complete!");
        }
        
        private void FindOrCreateComponents()
        {
            // Find NetworkedPuzzleManager
            if (_puzzleManager == null)
            {
                _puzzleManager = FindObjectOfType<NetworkedPuzzleManager>();
                
                if (_puzzleManager == null)
                {
                    LogDebug("Creating NetworkedPuzzleManager...");
                    var managerGO = new GameObject("NetworkedPuzzleManager");
                    _puzzleManager = managerGO.AddComponent<NetworkedPuzzleManager>();
                    managerGO.AddComponent<NetworkObject>();
                }
            }
            
            // Find NetworkedPuzzleValidator
            if (_puzzleValidator == null)
            {
                _puzzleValidator = FindObjectOfType<NetworkedPuzzleValidator>();
                
                if (_puzzleValidator == null)
                {
                    LogDebug("Creating NetworkedPuzzleValidator...");
                    var validatorGO = new GameObject("NetworkedPuzzleValidator");
                    _puzzleValidator = validatorGO.AddComponent<NetworkedPuzzleValidator>();
                    validatorGO.AddComponent<NetworkObject>();
                }
            }
            
            // Find all NetworkedRandomizers
            if (_randomizers == null || _randomizers.Length == 0)
            {
                _randomizers = FindObjectsOfType<NetworkedRandomizer>();
                LogDebug($"Found {_randomizers.Length} NetworkedRandomizers");
            }
        }
        
        private void SetupPuzzleManager()
        {
            if (_puzzleManager == null)
                return;
                
            LogDebug("Setting up NetworkedPuzzleManager...");
            
            // Register puzzle completion events with doors
            var doors = FindObjectsOfType<NetworkedDoor>();
            foreach (var door in doors)
            {
                int doorPuzzleId = door.GetComponent<NetworkedDoor>().GetHashCode() % 10; // Simple mapping
                
                _puzzleManager.OnPuzzleCompleted.AddListener(puzzleId => {
                    if (puzzleId == doorPuzzleId)
                    {
                        door.RPC_UnlockDoor();
                    }
                });
            }
            
            LogDebug($"Registered {doors.Length} doors with PuzzleManager");
        }
        
        private void SetupRandomizers()
        {
            if (_randomizers == null)
                return;
                
            LogDebug($"Setting up {_randomizers.Length} NetworkedRandomizers...");
            
            foreach (var randomizer in _randomizers)
            {
                if (randomizer != null)
                {
                    // Setup randomizer events
                    randomizer.OnRandomizationComplete.AddListener(() => {
                        LogDebug($"Randomizer {randomizer.name} completed randomization");
                    });
                    
                    // Ensure NetworkObject component
                    if (randomizer.GetComponent<NetworkObject>() == null)
                    {
                        randomizer.gameObject.AddComponent<NetworkObject>();
                        LogDebug($"Added NetworkObject to {randomizer.name}");
                    }
                }
            }
        }
        
        private void ValidateSetup()
        {
            LogDebug("Validating Puzzle Sync Setup...");
            
            bool isValid = true;
            
            // Check NetworkedPuzzleManager
            if (_puzzleManager == null)
            {
                Debug.LogError("[PuzzleSyncSetup] NetworkedPuzzleManager is missing!");
                isValid = false;
            }
            else if (_puzzleManager.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError("[PuzzleSyncSetup] NetworkedPuzzleManager is missing NetworkObject component!");
                isValid = false;
            }
            
            // Check NetworkedPuzzleValidator
            if (_puzzleValidator == null)
            {
                Debug.LogWarning("[PuzzleSyncSetup] NetworkedPuzzleValidator is missing (optional)");
            }
            else if (_puzzleValidator.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError("[PuzzleSyncSetup] NetworkedPuzzleValidator is missing NetworkObject component!");
                isValid = false;
            }
            
            // Check NetworkedRandomizers
            foreach (var randomizer in _randomizers)
            {
                if (randomizer != null && randomizer.GetComponent<NetworkObject>() == null)
                {
                    Debug.LogError($"[PuzzleSyncSetup] NetworkedRandomizer '{randomizer.name}' is missing NetworkObject component!");
                    isValid = false;
                }
            }
            
            // Check for NetworkRunner
            var networkRunner = FindObjectOfType<NetworkRunner>();
            if (networkRunner == null)
            {
                Debug.LogError("[PuzzleSyncSetup] No NetworkRunner found in scene!");
                isValid = false;
            }
            
            if (isValid)
            {
                LogDebug("Setup validation passed ✓");
            }
            else
            {
                Debug.LogError("[PuzzleSyncSetup] Setup validation failed! Please fix the errors above.");
            }
        }
        
        [ContextMenu("Convert Existing Puzzles")]
        public void ConvertExistingPuzzles()
        {
            LogDebug("Converting existing puzzles to networked versions...");
            
            ConvertLevers();
            ConvertPiano();
            ConvertRandomizers();
            
            LogDebug("Puzzle conversion complete!");
        }
        
        private void ConvertLevers()
        {
            // Find existing Levers scripts
            var oldLevers = FindObjectsOfType<Levers>();
            
            foreach (var oldLever in oldLevers)
            {
                LogDebug($"Converting lever: {oldLever.name}");
                
                var go = oldLever.gameObject;
                
                // Add NetworkObject if missing
                if (go.GetComponent<NetworkObject>() == null)
                {
                    go.AddComponent<NetworkObject>();
                }
                
                // Add NetworkedLever
                var networkedLever = go.GetComponent<NetworkedLever>();
                if (networkedLever == null)
                {
                    networkedLever = go.AddComponent<NetworkedLever>();
                }
                
                // Disable old script (don't destroy to preserve settings)
                oldLever.enabled = false;
                
                LogDebug($"Converted lever: {oldLever.name}");
            }
        }
        
        private void ConvertPiano()
        {
            // Find existing Piano scripts
            var oldPianos = FindObjectsOfType<Piano>();
            
            foreach (var oldPiano in oldPianos)
            {
                LogDebug($"Converting piano: {oldPiano.name}");
                
                var go = oldPiano.gameObject;
                
                // Add NetworkObject if missing
                if (go.GetComponent<NetworkObject>() == null)
                {
                    go.AddComponent<NetworkObject>();
                }
                
                // Add NetworkedPiano
                var networkedPiano = go.GetComponent<NetworkedPiano>();
                if (networkedPiano == null)
                {
                    networkedPiano = go.AddComponent<NetworkedPiano>();
                }
                
                // Disable old script
                oldPiano.enabled = false;
                
                LogDebug($"Converted piano: {oldPiano.name}");
            }
        }
        
        private void ConvertRandomizers()
        {
            // Find existing spawnRandomLogic scripts
            var oldRandomizers = FindObjectsOfType<spawnRandomLogic>();
            
            foreach (var oldRandomizer in oldRandomizers)
            {
                LogDebug($"Converting randomizer: {oldRandomizer.name}");
                
                var go = oldRandomizer.gameObject;
                
                // Add NetworkObject if missing
                if (go.GetComponent<NetworkObject>() == null)
                {
                    go.AddComponent<NetworkObject>();
                }
                
                // Add NetworkedRandomizer
                var networkedRandomizer = go.GetComponent<NetworkedRandomizer>();
                if (networkedRandomizer == null)
                {
                    networkedRandomizer = go.AddComponent<NetworkedRandomizer>();
                }
                
                // Disable old script
                oldRandomizer.enabled = false;
                
                LogDebug($"Converted randomizer: {oldRandomizer.name}");
            }
        }
        
        private void LogDebug(string message)
        {
            if (_enableDebugLogs)
            {
                Debug.Log($"[PuzzleSyncSetup] {message}");
            }
        }
        
        private void OnValidate()
        {
            if (_puzzleManager == null)
                _puzzleManager = FindObjectOfType<NetworkedPuzzleManager>();
                
            if (_puzzleValidator == null)
                _puzzleValidator = FindObjectOfType<NetworkedPuzzleValidator>();
                
            if (_randomizers == null || _randomizers.Length == 0)
                _randomizers = FindObjectsOfType<NetworkedRandomizer>();
        }
    }
}