using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;


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
        Material = 1 << 2,            // Randomizar materiales en slots específicos
        MaterialPerMesh = 1 << 3,     // Un material único por mesh
        Scale = 1 << 4,               // Randomizar escala
        Rotation = 1 << 5             // Randomizar rotación
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
        #region Configuration Classes
        
        [Serializable]
        public class PositionRandomizationSettings
        {
            [Header("Position Settings")]
            public bool ensureUniquePositions = true;
            public Transform[] possiblePositions;
            
            [Header("Spawn Mode")]
            public GameObject[] prefabsToSpawn;
            public int maxSpawnCount = 10;
            
            [Header("Existing Objects Mode")]
            public GameObject[] existingObjects;
            public bool hideUntilRandomized = true;
            public bool teleportRigidbodies = true;
        }
        
        [Serializable]
        public class MaterialRandomizationSettings
        {
            [Header("Slot-Based Material Randomization")]
            [Tooltip("Objects that will have their materials randomized")]
            public GameObject[] targetObjects;
            
            [Tooltip("Preserve atlas material in this slot")]
            public bool preserveAtlasSlot = true;
            public int atlasSlotIndex = 0;
            
            [Tooltip("Which material slots to randomize (e.g., 1,2,3)")]
            public int[] materialSlotsToRandomize = { 1, 2 };
            
            [Tooltip("Pool of materials to choose from")]
            public Material[] possibleMaterials;
        }
        
        [Serializable]
        public class MaterialPerMeshSettings
        {
            [Header("Material Per Mesh Randomization")]
            [Tooltip("Each mesh gets a unique material from the pool")]
            public GameObject[] targetMeshes;
            
            [Tooltip("Pool of materials (should be more than meshes)")]
            public Material[] materialPool;
            
            [Tooltip("Ensure each mesh gets a unique material")]
            public bool ensureUniqueMaterials = true;
            
            [Tooltip("Material slot to replace (usually 0)")]
            public int targetMaterialSlot = 0;
        }
        
        [Serializable]
        public class TransformRandomizationSettings
        {
            [Header("Scale Randomization")]
            public bool uniformScale = true;
            public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
            
            [Header("Rotation Randomization")]
            public bool useRotationPresets = true;
            public Vector3[] rotationPresets;
        }
        
        #endregion
        
        #region Inspector Fields
        
        [Header("Core Settings")]
        [SerializeField] private RandomizationType _randomizationTypes = RandomizationType.ExistingPosition;
        [SerializeField] private bool _randomizeOnStart = true;
        [SerializeField] private bool _useUniqueSeed = false;
        [SerializeField] private int _customSeed = 12345;
        
        [Space(10)]
        [Header("═══ Position Randomization ═══")]
        [SerializeField] private PositionRandomizationSettings _positionSettings = new PositionRandomizationSettings();
        
        [Space(10)]
        [Header("═══ Material Randomization (Slot-Based) ═══")]
        [SerializeField] private MaterialRandomizationSettings _materialSettings = new MaterialRandomizationSettings();
        
        [Space(10)]
        [Header("═══ Material Per Mesh ═══")]
        [SerializeField] private MaterialPerMeshSettings _materialPerMeshSettings = new MaterialPerMeshSettings();
        
        [Space(10)]
        [Header("═══ Transform Randomization ═══")]
        [SerializeField] private TransformRandomizationSettings _transformSettings = new TransformRandomizationSettings();
        
        [Space(10)]
        [Header("Debug & Validation")]
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
        private Dictionary<GameObject, int> _meshMaterialMapping = new Dictionary<GameObject, int>();
        private bool _isInitialized = false;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_validateSetup)
            {
                ValidateConfiguration();
            }
        }
        
        private void Start()
        {
            // For non-networked testing
            if (NetworkRunner.Instances.Count == 0 && _randomizeOnStart)
            {
                Debug.LogWarning("[NetworkedRandomizer] No NetworkRunner found - running in local mode");
                InitializeRandom(Time.frameCount);
                ExecuteLocalRandomization();
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
                RandomizeMaterialSlots();
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.MaterialPerMesh))
            {
                RandomizeMaterialPerMesh();
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
            var objects = _positionSettings.existingObjects;
            var positions = _positionSettings.possiblePositions;
            
            if (objects == null || objects.Length == 0 || positions == null || positions.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] Missing objects or positions for position randomization");
                return;
            }
            
            // Hide objects if configured
            if (_positionSettings.hideUntilRandomized)
            {
                foreach (var obj in objects.Where(o => o != null))
                {
                    obj.SetActive(false);
                }
            }
            
            // Create shuffled position indices
            List<int> availablePositions = Enumerable.Range(0, positions.Length).ToList();
            ShuffleList(availablePositions);
            
            // Assign positions to objects
            for (int i = 0; i < objects.Length && i < RandomizedData.Length; i++)
            {
                if (objects[i] == null) continue;
                
                int posIndex = _positionSettings.ensureUniquePositions && i < availablePositions.Count ? 
                    availablePositions[i] : 
                    _random.Next(0, positions.Length);
                
                var data = RandomizedData.Get(i);
                data.ObjectIndex = i;
                data.PositionIndex = posIndex;
                data.IsActive = true;
                
                // Add transform randomization if enabled
                if (_randomizationTypes.HasFlag(RandomizationType.Scale))
                {
                    data.ScaleMultiplier = Mathf.Lerp(
                        _transformSettings.scaleRange.x, 
                        _transformSettings.scaleRange.y, 
                        (float)_random.NextDouble()
                    );
                }
                
                if (_randomizationTypes.HasFlag(RandomizationType.Rotation))
                {
                    data.RotationPresetIndex = _transformSettings.useRotationPresets && 
                                              _transformSettings.rotationPresets.Length > 0 ? 
                        _random.Next(0, _transformSettings.rotationPresets.Length) : -1;
                }
                
                RandomizedData.Set(i, data);
                _objectDataMap[objects[i]] = data;
                
                if (_debugMode)
                {
                    Debug.Log($"[NetworkedRandomizer] Object {i} '{objects[i].name}' -> Position {posIndex}");
                }
            }
        }
        
        private void RandomizeSpawnPositions()
        {
            var prefabs = _positionSettings.prefabsToSpawn;
            var positions = _positionSettings.possiblePositions;
            
            if (prefabs == null || prefabs.Length == 0 || positions == null || positions.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] Missing prefabs or positions for spawn randomization");
                return;
            }
            
            int spawnCount = Mathf.Min(_positionSettings.maxSpawnCount, positions.Length);
            List<int> usedPositions = new List<int>();
            
            for (int i = 0; i < spawnCount && i < RandomizedData.Length; i++)
            {
                int prefabIndex = _random.Next(0, prefabs.Length);
                int posIndex;
                
                // Find unique position if required
                do
                {
                    posIndex = _random.Next(0, positions.Length);
                } while (_positionSettings.ensureUniquePositions && 
                         usedPositions.Contains(posIndex) && 
                         usedPositions.Count < positions.Length);
                
                usedPositions.Add(posIndex);
                
                // Spawn object
                if (Runner != null && prefabs[prefabIndex] != null)
                {
                    var spawned = Runner.Spawn(
                        prefabs[prefabIndex],
                        positions[posIndex].position,
                        positions[posIndex].rotation
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
        
        private void RandomizeMaterialSlots()
        {
            var targets = _materialSettings.targetObjects;
            var materials = _materialSettings.possibleMaterials;
            
            if (targets == null || targets.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] No target objects for material randomization");
                return;
            }
            
            if (materials == null || materials.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] No materials defined for randomization");
                return;
            }
            
            // Apply random material to each target
            foreach (var target in targets.Where(t => t != null))
            {
                int materialIndex = _random.Next(0, materials.Length);
                ApplyMaterialToSlots(target, materials[materialIndex]);
                
                if (_debugMode)
                {
                    Debug.Log($"[NetworkedRandomizer] Applied material {materials[materialIndex].name} to {target.name}");
                }
            }
        }
        
        private void RandomizeMaterialPerMesh()
        {
            var meshes = _materialPerMeshSettings.targetMeshes;
            var materials = _materialPerMeshSettings.materialPool;
            
            if (meshes == null || meshes.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] No target meshes for material-per-mesh randomization");
                return;
            }
            
            if (materials == null || materials.Length == 0)
            {
                Debug.LogWarning("[NetworkedRandomizer] No materials in pool");
                return;
            }
            
            if (_materialPerMeshSettings.ensureUniqueMaterials && materials.Length < meshes.Length)
            {
                Debug.LogWarning("[NetworkedRandomizer] Not enough unique materials for all meshes!");
            }
            
            // Create shuffled material indices
            List<int> availableMaterials = Enumerable.Range(0, materials.Length).ToList();
            ShuffleList(availableMaterials);
            
            // Assign unique material to each mesh
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null) continue;
                
                Renderer renderer = meshes[i].GetComponent<Renderer>();
                if (renderer == null)
                {
                    Debug.LogWarning($"[NetworkedRandomizer] No renderer on {meshes[i].name}");
                    continue;
                }
                
                // Get material index
                int materialIndex;
                if (_materialPerMeshSettings.ensureUniqueMaterials && i < availableMaterials.Count)
                {
                    materialIndex = availableMaterials[i];
                }
                else
                {
                    materialIndex = _random.Next(0, materials.Length);
                }
                
                // Store mapping for network sync
                _meshMaterialMapping[meshes[i]] = materialIndex;
                
                // Apply material to specified slot
                Material[] currentMaterials = renderer.materials;
                int slot = _materialPerMeshSettings.targetMaterialSlot;
                
                if (slot >= 0 && slot < currentMaterials.Length)
                {
                    currentMaterials[slot] = materials[materialIndex];
                    renderer.materials = currentMaterials;
                    
                    if (_debugMode)
                    {
                        Debug.Log($"[NetworkedRandomizer] Mesh {meshes[i].name} -> Material {materials[materialIndex].name}");
                    }
                }
                
                // Store in network data if space available
                if (i < RandomizedData.Length)
                {
                    var data = RandomizedData.Get(i);
                    data.ObjectIndex = i;
                    data.MaterialIndex = materialIndex;
                    data.IsActive = true;
                    RandomizedData.Set(i, data);
                }
            }
        }
        
        private void RandomizeScales()
        {
            // Scale is applied as part of position randomization
            if (_debugMode)
            {
                Debug.Log($"[NetworkedRandomizer] Scale randomization enabled: {_transformSettings.scaleRange}");
            }
        }
        
        private void RandomizeRotations()
        {
            // Rotation is applied as part of position randomization
            if (_debugMode)
            {
                Debug.Log($"[NetworkedRandomizer] Rotation randomization enabled with {_transformSettings.rotationPresets?.Length ?? 0} presets");
            }
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
            // Apply position randomization
            if (_randomizationTypes.HasFlag(RandomizationType.ExistingPosition))
            {
                ApplyPositionRandomization();
            }
            
            // Apply material slot randomization
            if (_randomizationTypes.HasFlag(RandomizationType.Material))
            {
                ApplyMaterialSlotRandomization();
            }
            
            // Apply material per mesh randomization
            if (_randomizationTypes.HasFlag(RandomizationType.MaterialPerMesh))
            {
                ApplyMaterialPerMeshRandomization();
            }
        }
        
        private void ApplyPositionRandomization()
        {
            var objects = _positionSettings.existingObjects;
            var positions = _positionSettings.possiblePositions;
            
            if (objects == null || positions == null) return;
            
            for (int i = 0; i < objects.Length && i < RandomizedData.Length; i++)
            {
                var data = RandomizedData.Get(i);
                if (!data.IsActive || objects[i] == null) continue;
                
                // Apply position
                if (data.PositionIndex >= 0 && data.PositionIndex < positions.Length)
                {
                    Transform targetTransform = positions[data.PositionIndex];
                    
                    if (_positionSettings.teleportRigidbodies && 
                        objects[i].TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb.position = targetTransform.position;
                        rb.rotation = targetTransform.rotation;
                    }
                    else
                    {
                        objects[i].transform.position = targetTransform.position;
                        objects[i].transform.rotation = targetTransform.rotation;
                    }
                }
                
                // Apply scale
                if (_randomizationTypes.HasFlag(RandomizationType.Scale) && data.ScaleMultiplier > 0)
                {
                    objects[i].transform.localScale = Vector3.one * data.ScaleMultiplier;
                }
                
                // Apply rotation
                if (_randomizationTypes.HasFlag(RandomizationType.Rotation))
                {
                    if (data.RotationPresetIndex >= 0 && 
                        data.RotationPresetIndex < _transformSettings.rotationPresets.Length)
                    {
                        objects[i].transform.rotation = Quaternion.Euler(
                            _transformSettings.rotationPresets[data.RotationPresetIndex]
                        );
                    }
                }
                
                // Show object
                objects[i].SetActive(true);
                
                // Store mapping
                _objectDataMap[objects[i]] = data;
                
                // Notify
                OnObjectRandomized?.Invoke(objects[i]);
            }
        }
        
        private void ApplyMaterialSlotRandomization()
        {
            // Re-randomize materials with same seed for consistency
            RandomizeMaterialSlots();
        }
        
        private void ApplyMaterialPerMeshRandomization()
        {
            var meshes = _materialPerMeshSettings.targetMeshes;
            var materials = _materialPerMeshSettings.materialPool;
            
            if (meshes == null || materials == null) return;
            
            // Apply materials based on network data
            for (int i = 0; i < meshes.Length && i < RandomizedData.Length; i++)
            {
                var data = RandomizedData.Get(i);
                if (!data.IsActive || meshes[i] == null) continue;
                
                if (data.MaterialIndex >= 0 && data.MaterialIndex < materials.Length)
                {
                    Renderer renderer = meshes[i].GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Material[] currentMaterials = renderer.materials;
                        int slot = _materialPerMeshSettings.targetMaterialSlot;
                        
                        if (slot >= 0 && slot < currentMaterials.Length)
                        {
                            currentMaterials[slot] = materials[data.MaterialIndex];
                            renderer.materials = currentMaterials;
                        }
                    }
                }
            }
        }
        
        private void ApplyMaterialToSlots(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;
            
            Material[] materials = renderer.materials;
            
            foreach (int slot in _materialSettings.materialSlotsToRandomize)
            {
                // Skip atlas slot if preserved
                if (_materialSettings.preserveAtlasSlot && 
                    slot == _materialSettings.atlasSlotIndex) continue;
                
                // Apply material to slot
                if (slot >= 0 && slot < materials.Length)
                {
                    materials[slot] = material;
                }
            }
            
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
            var objects = _positionSettings.existingObjects;
            if (objects != null)
            {
                foreach (var obj in objects.Where(o => o != null))
                {
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localRotation = Quaternion.identity;
                    obj.transform.localScale = Vector3.one;
                    
                    if (_positionSettings.hideUntilRandomized)
                    {
                        obj.SetActive(false);
                    }
                }
            }
            
            _objectDataMap.Clear();
            _meshMaterialMapping.Clear();
            
            RPC_NotifyReset();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyReset()
        {
            _objectDataMap.Clear();
            _meshMaterialMapping.Clear();
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
        
        private void ExecuteLocalRandomization()
        {
            // Local mode randomization for testing
            InitializeRandom(_customSeed > 0 ? _customSeed : Time.frameCount);
            
            if (_randomizationTypes.HasFlag(RandomizationType.ExistingPosition))
            {
                LocalRandomizePositions();
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.MaterialPerMesh))
            {
                RandomizeMaterialPerMesh();
            }
        }
        
        private void LocalRandomizePositions()
        {
            var objects = _positionSettings.existingObjects;
            var positions = _positionSettings.possiblePositions;
            
            if (objects == null || positions == null) return;
            
            List<int> positionIndices = Enumerable.Range(0, positions.Length).ToList();
            ShuffleList(positionIndices);
            
            for (int i = 0; i < objects.Length && i < positionIndices.Count; i++)
            {
                if (objects[i] != null)
                {
                    objects[i].transform.position = positions[positionIndices[i]].position;
                    objects[i].transform.rotation = positions[positionIndices[i]].rotation;
                    objects[i].SetActive(true);
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
                if (_positionSettings.prefabsToSpawn == null || 
                    _positionSettings.prefabsToSpawn.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] SpawnPosition requires prefabs!");
                    hasErrors = true;
                }
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.ExistingPosition))
            {
                if (_positionSettings.existingObjects == null || 
                    _positionSettings.existingObjects.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] ExistingPosition requires existing objects!");
                    hasErrors = true;
                }
                
                if (_positionSettings.possiblePositions == null || 
                    _positionSettings.possiblePositions.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] ExistingPosition requires positions!");
                    hasErrors = true;
                }
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.Material))
            {
                if (_materialSettings.targetObjects == null || 
                    _materialSettings.targetObjects.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] Material randomization requires target objects!");
                    hasErrors = true;
                }
                
                if (_materialSettings.possibleMaterials == null || 
                    _materialSettings.possibleMaterials.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] Material randomization requires materials!");
                    hasErrors = true;
                }
            }
            
            if (_randomizationTypes.HasFlag(RandomizationType.MaterialPerMesh))
            {
                if (_materialPerMeshSettings.targetMeshes == null || 
                    _materialPerMeshSettings.targetMeshes.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] MaterialPerMesh requires target meshes!");
                    hasErrors = true;
                }
                
                if (_materialPerMeshSettings.materialPool == null || 
                    _materialPerMeshSettings.materialPool.Length == 0)
                {
                    Debug.LogError("[NetworkedRandomizer] MaterialPerMesh requires material pool!");
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
            // Draw position connections
            if (_positionSettings.possiblePositions != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var pos in _positionSettings.possiblePositions.Where(p => p != null))
                {
                    Gizmos.DrawWireSphere(pos.position, 0.2f);
                    Gizmos.DrawRay(pos.position, pos.forward * 0.5f);
                }
            }
            
            // Draw existing object connections
            if (_positionSettings.existingObjects != null && _positionSettings.possiblePositions != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var obj in _positionSettings.existingObjects.Where(o => o != null))
                {
                    Transform nearest = _positionSettings.possiblePositions
                        .Where(p => p != null)
                        .OrderBy(p => Vector3.Distance(obj.transform.position, p.position))
                        .FirstOrDefault();
                    
                    if (nearest != null)
                    {
                        Gizmos.DrawLine(obj.transform.position, nearest.position);
                    }
                }
            }
            
            // Draw material targets
            if (_randomizationTypes.HasFlag(RandomizationType.Material) && 
                _materialSettings.targetObjects != null)
            {
                Gizmos.color = Color.magenta;
                foreach (var target in _materialSettings.targetObjects.Where(t => t != null))
                {
                    Gizmos.DrawWireCube(target.transform.position, Vector3.one * 0.15f);
                }
            }
            
            // Draw mesh material targets
            if (_randomizationTypes.HasFlag(RandomizationType.MaterialPerMesh) && 
                _materialPerMeshSettings.targetMeshes != null)
            {
                Gizmos.color = Color.green;
                foreach (var mesh in _materialPerMeshSettings.targetMeshes.Where(m => m != null))
                {
                    Gizmos.DrawWireSphere(mesh.transform.position, 0.25f);
                }
            }
        }
        
        #endregion
    }
}

// Custom Property Drawer for better Inspector organization
#if UNITY_EDITOR

namespace MetaAvatarsVR.Networking.PuzzleSync.Editor
{
    [CustomEditor(typeof(NetworkedRandomizer))]
    public class NetworkedRandomizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw default inspector but with visual grouping
            serializedObject.Update();
            
            // Title
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("NETWORKED RANDOMIZER", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Core Settings Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Core Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_randomizationTypes"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_randomizeOnStart"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_useUniqueSeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_customSeed"));
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Position Settings Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Position Randomization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_positionSettings"), true);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Material Slots Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Material Randomization (Slot-Based)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_materialSettings"), true);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Material Per Mesh Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Material Per Mesh", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_materialPerMeshSettings"), true);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Transform Settings Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Transform Randomization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_transformSettings"), true);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Debug Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Debug & Validation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_validateSetup"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_debugMode"));
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Events Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnRandomizationStarted"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnRandomizationComplete"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnObjectRandomized"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnRandomizationFailed"));
            EditorGUILayout.EndVertical();
            
            // Runtime buttons
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Randomize Now", GUILayout.Height(30)))
                {
                    var target = serializedObject.targetObject as NetworkedRandomizer;
                    target.RPC_RequestRandomization();
                }
                
                if (GUILayout.Button("Reset", GUILayout.Height(30)))
                {
                    var target = serializedObject.targetObject as NetworkedRandomizer;
                    target.RPC_RequestReset();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif