using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
    /// <summary>
    /// Tipos de randomización soportados
    /// </summary>
    [Flags]
    public enum RandomizationType
    {
        None = 0,
        SpawnPosition = 1 << 0,      // Spawn de prefabs
        ExistingPosition = 1 << 1,   // Reposicionar objetos existentes
        Material = 1 << 2,            // Randomizar materiales
        Scale = 1 << 3,               // Randomizar escala
        Rotation = 1 << 4             // Randomizar rotación
    }

    /// <summary>
    /// Datos de randomización para objetos
    /// </summary>
    [Serializable]
    public struct RandomizedObjectData : INetworkStruct
    {
        public int ObjectIndex;
        public int PositionIndex;
        public int MaterialIndex;
        public float ScaleMultiplier;
        public int RotationPresetIndex;
        public NetworkBool IsActive;
        
        public static RandomizedObjectData Default => new RandomizedObjectData
        {
            ObjectIndex = -1,
            PositionIndex = -1,
            MaterialIndex = -1,
            ScaleMultiplier = 1f,
            RotationPresetIndex = -1,
            IsActive = false
        };
    }

    /// <summary>
    /// Sistema modular de randomización networked para puzzles VR
    /// Soporta múltiples estrategias de randomización combinables
    /// </summary>
    public class NetworkedRandomizer : NetworkBehaviour
    {
        #region Configuration
        
        [Header("Randomization Settings")]
        [SerializeField] private RandomizationType _randomizationTypes = RandomizationType.ExistingPosition;
        [SerializeField] private bool _randomizeOnStart = true;
        [SerializeField] private bool _useUniqueSeed = false;
        [SerializeField] private int _customSeed = 12345;
        
        [Header("Position Randomization")]
        [SerializeField] private bool _ensureUniquePositions = true;
        [SerializeField] private Transform[] _possiblePositions;
        
        [Header("Spawn Mode (if using SpawnPosition)")]
        [SerializeField] private GameObject[] _prefabsToSpawn;
        [SerializeField] private int _maxSpawnCount = 10;
        
        [Header("Existing Objects Mode")]
        [SerializeField] private GameObject[] _existingObjects;
        [SerializeField] private bool _hideUntilRandomized = true;
        [SerializeField] private bool _teleportRigidbodies = true;
        
        [Header("Material Randomization")]
        [SerializeField] private bool _preserveAtlasSlot = true;
        [SerializeField] private int _atlasSlotIndex = 0;
        [SerializeField] private Material[] _possibleMaterials;
        [SerializeField] private int[] _materialSlotsToRandomize = { 1, 2 }; // Skip slot 0 by default
        
        [Header("Scale Randomization")]
        [SerializeField] private bool _uniformScale = true;
        [SerializeField] private Vector2 _scaleRange = new Vector2(0.8f, 1.2f);
        
        [Header("Rotation Randomization")]
        [SerializeField] private bool _useRotationPresets = true;
        [SerializeField] private Vector3[] _rotationPresets;
        
        [Header("Validation")]
        [SerializeField] private bool _validateSetup = true;
        [SerializeField] private bool _debugMode = false;
        
        #endregion
        
        #region Network State
        
        [Networked] public int RandomSeed { get; set; }
        [Networked] public NetworkBool IsRandomized { get; set; }
        
        [Networked, Capacity(50)]
        public NetworkArray<RandomizedObjectData> RandomizedData { get; }
        
        #endregion
        
        #region Events
        
        [Header("Events")]
        public UnityEvent OnRandomizationStarted = new UnityEvent();
        public UnityEvent OnRandomizationComplete = new UnityEvent();
        public UnityEvent<GameObject> OnObjectRandomized = new UnityEvent<GameObject>();
        public UnityEvent<string> OnRandomizationFailed = new UnityEvent<string>();
        
        #endregion
        
        #region Private Variables
        
        private System.Random _random;
        private Dictionary<GameObject, RandomizedObjectData> _objectDataMap = new Dictionary<GameObject, RandomizedObjectData>();
        private List<Renderer> _targetRenderers = new List<Renderer>();
        private bool _isInitialized = false;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_validateSetup)
            {
                ValidateConfiguration();
            }
            
            CacheRenderers();
        }
        
        private void Start()
        {
            // For non-networked testing
            if (NetworkRunner.Instances.Count == 0 && _randomizeOnStart)
            {
                Debug.LogWarning("[NetworkedRandomizer] No NetworkRunner found - running in local mode");
                InitializeRandom(Time.frameCount);
                ExecuteRandomization();
            }
        }
        
        #endregion
        
        #region Network Lifecycle
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                // Generate or use seed
                RandomSeed = _useUniqueSeed ? 
                    UnityEngine.Random.Range(1000, 99999) : 
                    (_customSeed > 0 ? _customSeed : GetSeedFromPuzzleManager());
                
                if (_randomizeOnStart)
                {
                    PerformRandomization();
                }
            }
            else
            {
                // Clients wait for randomization data
                if (IsRandomized)
                {
                    ApplyRandomizationFromNetwork();
                }
            }
            
            _isInitialized = true;
        }
        
        public override void FixedUpdateNetwork()
        {
            // Check if randomization just completed on client
            if (!HasStateAuthority && IsRandomized && !_isInitialized)
            {
                ApplyRandomizationFromNetwork();
                _isInitialized = true;
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Trigger randomization manually
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestRandomization()
        {
            if (HasStateAuthority && !IsRandomized)
            {
                PerformRandomization();
            }
        }
        
        /// <summary>
        /// Reset to initial state
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestReset()
        {
            if (HasStateAuthority)
            {
                ResetRandomization();
            }
        }
        
        /// <summary>
        /// Get randomized data for a specific object
        /// </summary>
        public RandomizedObjectData GetObjectData(GameObject obj)
        {
            return _objectDataMap.ContainsKey(obj) ? 
                _objectDataMap[obj] : 
                RandomizedObjectData.Default;
        }
        
        #endregion
        
        #region Randomization Logic
        
        private void PerformRandomization()
        {
            if (!HasStateAuthority) return;
            
            Debug.Log($"[NetworkedRandomizer] Starting randomization with seed {RandomSeed}");
            
            OnRandomizationStarted?.Invoke();
            InitializeRandom(RandomSeed);
            
            // Clear previous data
            for (int i = 0; i < RandomizedData.Length; i++)
            {
                RandomizedData.Set(i, RandomizedObjectData.Default);
            }
            
            // Execute randomization based on types
            if (_randomizationTypes.HasFlag(RandomizationType.SpawnPosition))
            {
                RandomizeSpawnPositions();
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.ExistingPosition))
            {
                RandomizeExistingPositions();
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.Material))
            {
                RandomizeMaterials();
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.Scale))
            {
                RandomizeScales();
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.Rotation))
            {
                RandomizeRotations();
            }
            
            IsRandomized = true;
            RPC_ApplyRandomization();
        }
        
        private void RandomizeExistingPositions()
        {
            if (_existingObjects == null || _existingObjects.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] No existing objects to randomize");
                return;
            }
            
            if (_possiblePositions == null || _possiblePositions.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] No positions defined");
                return;
            }
            
            // Hide objects if configured
            if (_hideUntilRandomized)
            {
                foreach (var obj in _existingObjects.Where(o => o != null))
                {
                    obj.SetActive(false);
                }
            }
            
            // Create shuffled position indices
            List<int> availablePositions = Enumerable.Range(0, _possiblePositions.Length).ToList();
            ShuffleList(availablePositions);
            
            // Assign positions to objects
            for (int i = 0; i < _existingObjects.Length && i < RandomizedData.Length; i++)
            {
                if (_existingObjects[i] == null) continue;
                
                int posIndex = _ensureUniquePositions && i < availablePositions.Count ? 
                    availablePositions[i] : 
                    _random.Next(0, _possiblePositions.Length);
                
                var data = RandomizedData.Get(i);
                data.ObjectIndex = i;
                data.PositionIndex = posIndex;
                data.IsActive = true;
                
                // Update data for this randomization pass
                if (_randomizationTypes.HasFlag(RandomizationType.Material))
                {
                    data.MaterialIndex = _random.Next(0, _possibleMaterials.Length);
                }
                
                if (_randomizationTypes.HasFlag(RandomizationType.Scale))
                {
                    data.ScaleMultiplier = Mathf.Lerp(_scaleRange.x, _scaleRange.y, (float)_random.NextDouble());
                }
                
                if (_randomizationTypes.HasFlag(RandomizationType.Rotation))
                {
                    data.RotationPresetIndex = _useRotationPresets && _rotationPresets.Length > 0 ? 
                        _random.Next(0, _rotationPresets.Length) : -1;
                }
                
                RandomizedData.Set(i, data);
                _objectDataMap[_existingObjects[i]] = data;
                
                if (_debugMode)
                {
                    Debug.Log($"[NetworkedRandomizer] Object {i} -> Position {posIndex}");
                }
            }
        }
        
        private void RandomizeSpawnPositions()
        {
            if (_prefabsToSpawn == null || _prefabsToSpawn.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] No prefabs to spawn");
                return;
            }
            
            int spawnCount = Mathf.Min(_maxSpawnCount, _possiblePositions.Length);
            List<int> usedPositions = new List<int>();
            
            for (int i = 0; i < spawnCount && i < RandomizedData.Length; i++)
            {
                int prefabIndex = _random.Next(0, _prefabsToSpawn.Length);
                int posIndex;
                
                // Find unique position if required
                do
                {
                    posIndex = _random.Next(0, _possiblePositions.Length);
                } while (_ensureUniquePositions && usedPositions.Contains(posIndex) && usedPositions.Count < _possiblePositions.Length);
                
                usedPositions.Add(posIndex);
                
                // Spawn object
                if (Runner != null && _prefabsToSpawn[prefabIndex] != null)
                {
                    var spawned = Runner.Spawn(
                        _prefabsToSpawn[prefabIndex],
                        _possiblePositions[posIndex].position,
                        _possiblePositions[posIndex].rotation
                    );
                    
                    // Store data
                    var data = new RandomizedObjectData
                    {
                        ObjectIndex = prefabIndex,
                        PositionIndex = posIndex,
                        IsActive = true
                    };
                    
                    RandomizedData.Set(i, data);
                    
                    if (spawned != null)
                    {
                        _objectDataMap[spawned.gameObject] = data;
                        OnObjectRandomized?.Invoke(spawned.gameObject);
                    }
                }
            }
        }
        
        private void RandomizeMaterials()
        {
            if (_possibleMaterials == null || _possibleMaterials.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] No materials defined for randomization");
                return;
            }
            
            // Material randomization is applied during the main randomization pass
            // The actual application happens in ApplyRandomizationToObject
            Debug.Log($"[NetworkedRandomizer] Material randomization configured for {_possibleMaterials.Length} materials");
        }
        
        private void RandomizeScales()
        {
            // Scale randomization is applied during the main randomization pass
            Debug.Log($"[NetworkedRandomizer] Scale randomization configured: {_scaleRange}");
        }
        
        private void RandomizeRotations()
        {
            // Rotation randomization is applied during the main randomization pass
            Debug.Log($"[NetworkedRandomizer] Rotation randomization configured with {_rotationPresets?.Length ?? 0} presets");
        }
        
        #endregion
        
        #region Network Synchronization
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ApplyRandomization()
        {
            Debug.Log($"[NetworkedRandomizer] Applying randomization on all clients");
            
            InitializeRandom(RandomSeed);
            ApplyRandomizationFromNetwork();
            
            OnRandomizationComplete?.Invoke();
        }
        
        private void ApplyRandomizationFromNetwork()
        {
            // Apply to existing objects
            if (_existingObjects != null)
            {
                for (int i = 0; i < _existingObjects.Length && i < RandomizedData.Length; i++)
                {
                    var data = RandomizedData.Get(i);
                    if (!data.IsActive || _existingObjects[i] == null) continue;
                    
                    ApplyRandomizationToObject(_existingObjects[i], data);
                }
            }
        }
        
        private void ApplyRandomizationToObject(GameObject obj, RandomizedObjectData data)
        {
            // Apply position
            if (_randomizationTypes.HasFlag(RandomizationType.ExistingPosition) && 
                data.PositionIndex >= 0 && data.PositionIndex < _possiblePositions.Length)
            {
                Transform targetTransform = _possiblePositions[data.PositionIndex];
                
                if (_teleportRigidbodies && obj.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.position = targetTransform.position;
                    rb.rotation = targetTransform.rotation;
                }
                else
                {
                    obj.transform.position = targetTransform.position;
                    obj.transform.rotation = targetTransform.rotation;
                }
            }
            
            // Apply material
            if (_randomizationTypes.HasFlag(RandomizationType.Material) && 
                data.MaterialIndex >= 0 && data.MaterialIndex < _possibleMaterials.Length)
            {
                ApplyMaterialToObject(obj, data.MaterialIndex);
            }
            
            // Apply scale
            if (_randomizationTypes.HasFlag(RandomizationType.Scale) && data.ScaleMultiplier > 0)
            {
                obj.transform.localScale = Vector3.one * data.ScaleMultiplier;
            }
            
            // Apply rotation
            if (_randomizationTypes.HasFlag(RandomizationType.Rotation))
            {
                if (data.RotationPresetIndex >= 0 && data.RotationPresetIndex < _rotationPresets.Length)
                {
                    obj.transform.rotation = Quaternion.Euler(_rotationPresets[data.RotationPresetIndex]);
                }
                else if (!_useRotationPresets)
                {
                    // Random rotation if not using presets
                    obj.transform.rotation = Quaternion.Euler(
                        _random.Next(0, 360),
                        _random.Next(0, 360),
                        _random.Next(0, 360)
                    );
                }
            }
            
            // Show object
            obj.SetActive(true);
            
            // Store mapping
            _objectDataMap[obj] = data;
            
            // Notify
            OnObjectRandomized?.Invoke(obj);
            
            if (_debugMode)
            {
                Debug.Log($"[NetworkedRandomizer] Applied randomization to {obj.name}");
            }
        }
        
        private void ApplyMaterialToObject(GameObject obj, int materialIndex)
        {
            if (materialIndex < 0 || materialIndex >= _possibleMaterials.Length) return;
            
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            
            Material newMaterial = _possibleMaterials[materialIndex];
            if (newMaterial == null) return;
            
            // Get current materials
            Material[] materials = renderer.materials;
            
            // Apply to specified slots
            foreach (int slot in _materialSlotsToRandomize)
            {
                // Skip if slot is the preserved atlas slot
                if (_preserveAtlasSlot && slot == _atlasSlotIndex) continue;
                
                // Apply if slot exists
                if (slot >= 0 && slot < materials.Length)
                {
                    materials[slot] = newMaterial;
                    
                    if (_debugMode)
                    {
                        Debug.Log($"[NetworkedRandomizer] Applied material {newMaterial.name} to slot {slot} of {obj.name}");
                    }
                }
            }
            
            // Apply materials back
            renderer.materials = materials;
        }
        
        #endregion
        
        #region Reset
        
        private void ResetRandomization()
        {
            if (!HasStateAuthority) return;
            
            IsRandomized = false;
            
            // Reset all data
            for (int i = 0; i < RandomizedData.Length; i++)
            {
                RandomizedData.Set(i, RandomizedObjectData.Default);
            }
            
            // Reset existing objects
            if (_existingObjects != null)
            {
                foreach (var obj in _existingObjects.Where(o => o != null))
                {
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localRotation = Quaternion.identity;
                    obj.transform.localScale = Vector3.one;
                    
                    if (_hideUntilRandomized)
                    {
                        obj.SetActive(false);
                    }
                }
            }
            
            _objectDataMap.Clear();
            
            RPC_NotifyReset();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyReset()
        {
            _objectDataMap.Clear();
            Debug.Log("[NetworkedRandomizer] Randomization reset");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void InitializeRandom(int seed)
        {
            _random = new System.Random(seed);
        }
        
        private int GetSeedFromPuzzleManager()
        {
            if (NetworkedPuzzleManager.Instance != null)
            {
                return NetworkedPuzzleManager.Instance.RandomizationSeed;
            }
            return UnityEngine.Random.Range(1000, 99999);
        }
        
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
        
        private void CacheRenderers()
        {
            _targetRenderers.Clear();
            
            if (_existingObjects != null)
            {
                foreach (var obj in _existingObjects.Where(o => o != null))
                {
                    Renderer renderer = obj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        _targetRenderers.Add(renderer);
                    }
                }
            }
        }
        
        private void ExecuteRandomization()
        {
            // Local mode randomization for testing
            InitializeRandom(_customSeed > 0 ? _customSeed : Time.frameCount);
            
            if (_randomizationTypes.HasFlag(RandomizationType.ExistingPosition))
            {
                LocalRandomizePositions();
            }
        }
        
        private void LocalRandomizePositions()
        {
            // Simplified local randomization for testing without networking
            if (_existingObjects == null || _possiblePositions == null) return;
            
            List<int> positions = Enumerable.Range(0, _possiblePositions.Length).ToList();
            ShuffleList(positions);
            
            for (int i = 0; i < _existingObjects.Length && i < positions.Count; i++)
            {
                if (_existingObjects[i] != null && i < _possiblePositions.Length)
                {
                    _existingObjects[i].transform.position = _possiblePositions[positions[i]].position;
                    _existingObjects[i].transform.rotation = _possiblePositions[positions[i]].rotation;
                }
            }
        }
        
        #endregion
        
        #region Validation
        
        private void ValidateConfiguration()
        {
            bool hasErrors = false;
            
            // Validate based on randomization type
            if (_randomizationTypes.HasFlag(RandomizationType.SpawnPosition))
            {
                if (_prefabsToSpawn == null || _prefabsToSpawn.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] SpawnPosition mode requires prefabs!");
                    hasErrors = true;
                }
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.ExistingPosition))
            {
                if (_existingObjects == null || _existingObjects.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] ExistingPosition mode requires existing objects!");
                    hasErrors = true;
                }
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.Material))
            {
                if (_possibleMaterials == null || _possibleMaterials.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] Material randomization requires materials!");
                    hasErrors = true;
                }
                
                // Validate material slots
                foreach (int slot in _materialSlotsToRandomize)
                {
                    if (_preserveAtlasSlot && slot == _atlasSlotIndex)
                    {
                        Debug.LogWarning($"[NetworkedRandomizer] Slot {slot} is set as atlas slot but also in randomize list!");
                    }
                }
            }
            
            // Check positions for any position-based randomization
            if ((_randomizationTypes & (RandomizationType.SpawnPosition | RandomizationType.ExistingPosition)) != 0)
            {
                if (_possiblePositions == null || _possiblePositions.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] Position randomization requires positions!");
                    hasErrors = true;
                }
            }
            
            if (hasErrors)
            {
                OnRandomizationFailed?.Invoke("Configuration validation failed");
            }
            else if (_debugMode)
            {
                Debug.Log("[NetworkedRandomizer] Configuration validated successfully");
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            // Draw possible positions
            if (_possiblePositions != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var pos in _possiblePositions.Where(p => p != null))
                {
                    Gizmos.DrawWireSphere(pos.position, 0.2f);
                    Gizmos.DrawRay(pos.position, pos.forward * 0.5f);
                }
            }
            
            // Draw existing object connections
            if (_existingObjects != null && _possiblePositions != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var obj in _existingObjects.Where(o => o != null))
                {
                    // Draw line to nearest position
                    Transform nearest = _possiblePositions
                        .Where(p => p != null)
                        .OrderBy(p => Vector3.Distance(obj.transform.position, p.position))
                        .FirstOrDefault();
                    
                    if (nearest != null)
                    {
                        Gizmos.DrawLine(obj.transform.position, nearest.position);
                    }
                }
            }
        }
        
        #endregion
    }
}