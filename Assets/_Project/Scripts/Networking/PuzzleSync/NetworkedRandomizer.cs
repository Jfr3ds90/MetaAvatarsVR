using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
    [Serializable]
    public struct RandomizedObject : INetworkStruct
    {
        public int ObjectIndex;
        public int PositionIndex;
        public NetworkBool IsActive;
        
        public RandomizedObject(int objIndex, int posIndex, bool active = true)
        {
            ObjectIndex = objIndex;
            PositionIndex = posIndex;
            IsActive = active;
        }
    }
    
    public class NetworkedRandomizer : NetworkBehaviour
    {
        [Header("Randomization Configuration")]
        [SerializeField] private GameObject[] _objectPrefabs;
        [SerializeField] private Transform[] _spawnPositions;
        [SerializeField] private bool _randomizeOnStart = true;
        [SerializeField] private bool _allowDuplicates = false;
        [SerializeField] private int _maxActiveObjects = -1;
        
        [Header("Material Randomization")]
        [SerializeField] private bool _randomizeMaterials = false;
        [SerializeField] private Material[] _availableMaterials;
        [SerializeField] private GameObject[] _materialTargets;
        
        [Header("Network State")]
        [Networked, Capacity(50)]
        public NetworkArray<RandomizedObject> RandomizedObjects { get; }
        
        [Networked]
        public int ActiveObjectCount { get; set; }
        
        [Networked]
        public NetworkBool IsRandomized { get; set; }
        
        [Networked]
        public int RandomSeed { get; set; }
        
        [Header("Events")]
        public UnityEvent OnRandomizationComplete = new UnityEvent();
        public UnityEvent<GameObject, Transform> OnObjectSpawned = new UnityEvent<GameObject, Transform>();
        
        private List<GameObject> _spawnedObjects = new List<GameObject>();
        private System.Random _random;
        private bool _hasInitialized = false;
        
        public override void Spawned()
        {
            if (HasStateAuthority && _randomizeOnStart && !IsRandomized)
            {
                PerformRandomization();
            }
            else if (!HasStateAuthority && IsRandomized)
            {
                ApplyRandomization();
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestRandomization(RpcInfo info = default)
        {
            if (HasStateAuthority)
            {
                PerformRandomization();
            }
        }
        
        private void PerformRandomization()
        {
            if (IsRandomized)
            {
                Debug.LogWarning("[NetworkedRandomizer] Already randomized. Call RPC_ClearRandomization first.");
                return;
            }
            
            int seed = GetRandomSeed();
            RandomSeed = seed;
            _random = new System.Random(seed);
            
            Debug.Log($"[NetworkedRandomizer] Performing randomization with seed: {seed}");
            
            ClearSpawnedObjects();
            
            if (_objectPrefabs.Length > 0 && _spawnPositions.Length > 0)
            {
                RandomizeObjectPositions();
            }
            
            if (_randomizeMaterials && _availableMaterials.Length > 0 && _materialTargets.Length > 0)
            {
                RandomizeMaterialsNetwork();
            }
            
            IsRandomized = true;
            
            RPC_NotifyRandomizationComplete();
        }
        
        private void RandomizeObjectPositions()
        {
            List<int> availablePositions = Enumerable.Range(0, _spawnPositions.Length).ToList();
            List<int> availableObjects = Enumerable.Range(0, _objectPrefabs.Length).ToList();
            
            if (!_allowDuplicates && _objectPrefabs.Length > _spawnPositions.Length)
            {
                Debug.LogWarning("[NetworkedRandomizer] More objects than positions without duplicates allowed!");
            }
            
            int objectsToSpawn = _maxActiveObjects > 0 
                ? Mathf.Min(_maxActiveObjects, _objectPrefabs.Length) 
                : _objectPrefabs.Length;
                
            ActiveObjectCount = 0;
            
            for (int i = 0; i < objectsToSpawn && availablePositions.Count > 0; i++)
            {
                int posIndex = availablePositions[_random.Next(availablePositions.Count)];
                int objIndex;
                
                if (_allowDuplicates)
                {
                    objIndex = _random.Next(_objectPrefabs.Length);
                }
                else
                {
                    if (availableObjects.Count == 0)
                        break;
                        
                    int randomObjIdx = _random.Next(availableObjects.Count);
                    objIndex = availableObjects[randomObjIdx];
                    availableObjects.RemoveAt(randomObjIdx);
                }
                
                availablePositions.Remove(posIndex);
                
                var randomizedObj = new RandomizedObject(objIndex, posIndex, true);
                RandomizedObjects.Set(ActiveObjectCount, randomizedObj);
                ActiveObjectCount++;
                
                SpawnObjectAtPosition(objIndex, posIndex);
            }
            
            Debug.Log($"[NetworkedRandomizer] Spawned {ActiveObjectCount} objects");
        }
        
        private void RandomizeMaterialsNetwork()
        {
            for (int i = 0; i < _materialTargets.Length && i < RandomizedObjects.Length; i++)
            {
                int materialIndex = _random.Next(_availableMaterials.Length);
                
                var randomizedObj = new RandomizedObject(materialIndex, i, true);
                RandomizedObjects.Set(ActiveObjectCount + i, randomizedObj);
            }
            
            RPC_ApplyMaterials();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ApplyMaterials()
        {
            int startIndex = ActiveObjectCount;
            for (int i = 0; i < _materialTargets.Length; i++)
            {
                var data = RandomizedObjects.Get(startIndex + i);
                if (data.IsActive && _materialTargets[i] != null)
                {
                    ApplyMaterialToTarget(_materialTargets[i], data.ObjectIndex);
                }
            }
        }
        
        private void ApplyMaterialToTarget(GameObject target, int materialIndex)
        {
            if (materialIndex >= 0 && materialIndex < _availableMaterials.Length)
            {
                var renderer = target.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material[] mats = renderer.materials;
                    if (mats.Length > 1)
                    {
                        mats[1] = _availableMaterials[materialIndex];
                    }
                    else if (mats.Length > 0)
                    {
                        mats[0] = _availableMaterials[materialIndex];
                    }
                    renderer.materials = mats;
                    
                    Debug.Log($"[NetworkedRandomizer] Applied material {materialIndex} to {target.name}");
                }
            }
        }
        
        private void ApplyRandomization()
        {
            if (_hasInitialized)
                return;
                
            _hasInitialized = true;
            _random = new System.Random(RandomSeed);
            
            Debug.Log($"[NetworkedRandomizer] Applying randomization from network state (seed: {RandomSeed})");
            
            ClearSpawnedObjects();
            
            for (int i = 0; i < ActiveObjectCount; i++)
            {
                var randomizedObj = RandomizedObjects.Get(i);
                if (randomizedObj.IsActive)
                {
                    SpawnObjectAtPosition(randomizedObj.ObjectIndex, randomizedObj.PositionIndex);
                }
            }
            
            if (_randomizeMaterials)
            {
                RPC_ApplyMaterials();
            }
            
            OnRandomizationComplete?.Invoke();
        }
        
        private void SpawnObjectAtPosition(int objectIndex, int positionIndex)
        {
            if (objectIndex >= 0 && objectIndex < _objectPrefabs.Length &&
                positionIndex >= 0 && positionIndex < _spawnPositions.Length)
            {
                GameObject prefab = _objectPrefabs[objectIndex];
                Transform spawnPoint = _spawnPositions[positionIndex];
                
                if (prefab != null && spawnPoint != null)
                {
                    GameObject spawned = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
                    spawned.name = $"{prefab.name}_Pos{positionIndex}";
                    _spawnedObjects.Add(spawned);
                    
                    OnObjectSpawned?.Invoke(spawned, spawnPoint);
                    
                    Debug.Log($"[NetworkedRandomizer] Spawned {prefab.name} at position {positionIndex}");
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ClearRandomization(RpcInfo info = default)
        {
            if (HasStateAuthority)
            {
                IsRandomized = false;
                ActiveObjectCount = 0;
                
                for (int i = 0; i < RandomizedObjects.Length; i++)
                {
                    RandomizedObjects.Set(i, default);
                }
                
                RPC_ClearObjects();
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ClearObjects()
        {
            ClearSpawnedObjects();
        }
        
        private void ClearSpawnedObjects()
        {
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _spawnedObjects.Clear();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyRandomizationComplete()
        {
            OnRandomizationComplete?.Invoke();
            Debug.Log("[NetworkedRandomizer] Randomization complete");
        }
        
        private int GetRandomSeed()
        {
            if (NetworkedPuzzleManager.Instance != null && NetworkedPuzzleManager.Instance.RandomizationSeed != 0)
            {
                return NetworkedPuzzleManager.Instance.RandomizationSeed + GetInstanceID();
            }
            
            return UnityEngine.Random.Range(1000, 99999);
        }
        
        public void SetObjectPrefabs(GameObject[] prefabs)
        {
            _objectPrefabs = prefabs;
        }
        
        public void SetSpawnPositions(Transform[] positions)
        {
            _spawnPositions = positions;
        }
        
        public void SetMaterialTargets(GameObject[] targets, Material[] materials)
        {
            _materialTargets = targets;
            _availableMaterials = materials;
            _randomizeMaterials = true;
        }
        
        public List<GameObject> GetSpawnedObjects()
        {
            return new List<GameObject>(_spawnedObjects);
        }
        
        public bool HasObject(GameObject obj)
        {
            return _spawnedObjects.Contains(obj);
        }
        
        private void OnDestroy()
        {
            ClearSpawnedObjects();
        }
        
        private void OnDrawGizmosSelected()
        {
            if (_spawnPositions != null)
            {
                Gizmos.color = Color.green;
                foreach (var pos in _spawnPositions)
                {
                    if (pos != null)
                    {
                        Gizmos.DrawWireSphere(pos.position, 0.3f);
                        Gizmos.DrawRay(pos.position, pos.forward * 0.5f);
                    }
                }
            }
        }
    }
}