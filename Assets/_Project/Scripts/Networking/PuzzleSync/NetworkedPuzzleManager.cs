using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
    public enum PuzzleState
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }

    [Serializable]
    public struct PuzzleProgress : INetworkStruct
    {
        public int PuzzleId;
        public PuzzleState State;
        public int CurrentStep;
        public int TotalSteps;
        public float CompletionTime;
        
        public PuzzleProgress(int puzzleId, int totalSteps)
        {
            PuzzleId = puzzleId;
            State = PuzzleState.NotStarted;
            CurrentStep = 0;
            TotalSteps = totalSteps;
            CompletionTime = 0f;
        }
    }

    public class NetworkedPuzzleManager : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int _totalPuzzles = 5;
        [SerializeField] private bool _requireSequentialCompletion = true;
        
        [Header("Network State")]
        [Networked, Capacity(10)] 
        public NetworkArray<PuzzleProgress> PuzzleProgresses { get; }
        
        [Networked] 
        public int CurrentPuzzleIndex { get; set; }
        
        [Networked] 
        public NetworkBool AllPuzzlesCompleted { get; set; }
        
        [Networked] 
        public int RandomizationSeed { get; set; }
        
        [Networked, Capacity(32)]
        public NetworkString<_32> CurrentRoomName { get; set; }
        
        [Networked]
        public float TotalPlayTime { get; set; }
        
        [Header("Events")]
        public UnityEvent<int> OnPuzzleStarted = new UnityEvent<int>();
        public UnityEvent<int> OnPuzzleCompleted = new UnityEvent<int>();
        public UnityEvent<int> OnPuzzleFailed = new UnityEvent<int>();
        public UnityEvent<int, int> OnPuzzleProgressUpdated = new UnityEvent<int, int>();
        public UnityEvent OnAllPuzzlesCompleted = new UnityEvent();
        public UnityEvent<string> OnRoomTransition = new UnityEvent<string>();
        
        private static NetworkedPuzzleManager _instance;
        public static NetworkedPuzzleManager Instance => _instance;
        
        private Dictionary<int, List<Action<PuzzleProgress>>> _puzzleCallbacks = new Dictionary<int, List<Action<PuzzleProgress>>>();
        private NetworkRunner _runner;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        public override void Spawned()
        {
            _runner = Runner;
            
            if (HasStateAuthority)
            {
                InitializePuzzles();
                GenerateRandomizationSeed();
            }
            
            RegisterNetworkCallbacks();
        }
        
        private void InitializePuzzles()
        {
            for (int i = 0; i < _totalPuzzles && i < PuzzleProgresses.Length; i++)
            {
                PuzzleProgresses.Set(i, new PuzzleProgress(i, GetPuzzleSteps(i)));
            }
            
            CurrentPuzzleIndex = 0;
            AllPuzzlesCompleted = false;
        }
        
        private void GenerateRandomizationSeed()
        {
            RandomizationSeed = UnityEngine.Random.Range(1000, 99999);
            Debug.Log($"[NetworkedPuzzleManager] Generated randomization seed: {RandomizationSeed}");
        }
        
        private int GetPuzzleSteps(int puzzleId)
        {
            switch (puzzleId)
            {
                case 0: return 5; // Puzzle 1: Linternas y palancas (5 palancas para DATOS)
                case 1: return 4; // Puzzle 2: Piano con notas musicales
                case 2: return 3; // Puzzle 3: Por definir
                case 3: return 3; // Puzzle 4: Por definir
                case 4: return 1; // Puzzle 5: Final
                default: return 1;
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestStartPuzzle(int puzzleId, RpcInfo info = default)
        {
            if (!ValidatePuzzleAccess(puzzleId))
            {
                Debug.LogWarning($"[NetworkedPuzzleManager] Player cannot start puzzle {puzzleId} yet");
                return;
            }
            
            var progress = PuzzleProgresses.Get(puzzleId);
            if (progress.State == PuzzleState.NotStarted)
            {
                progress.State = PuzzleState.InProgress;
                PuzzleProgresses.Set(puzzleId, progress);
                CurrentPuzzleIndex = puzzleId;
                
                RPC_NotifyPuzzleStarted(puzzleId);
                Debug.Log($"[NetworkedPuzzleManager] Puzzle {puzzleId} started by player {info.Source}");
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPuzzleStarted(int puzzleId)
        {
            OnPuzzleStarted?.Invoke(puzzleId);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_UpdatePuzzleProgress(int puzzleId, int newStep, RpcInfo info = default)
        {
            if (puzzleId < 0 || puzzleId >= _totalPuzzles)
                return;
                
            var progress = PuzzleProgresses.Get(puzzleId);
            if (progress.State != PuzzleState.InProgress)
                return;
                
            progress.CurrentStep = Mathf.Clamp(newStep, 0, progress.TotalSteps);
            PuzzleProgresses.Set(puzzleId, progress);
            
            RPC_NotifyProgressUpdated(puzzleId, progress.CurrentStep);
            
            if (progress.CurrentStep >= progress.TotalSteps)
            {
                CompletePuzzle(puzzleId);
            }
            
            Debug.Log($"[NetworkedPuzzleManager] Puzzle {puzzleId} progress: {progress.CurrentStep}/{progress.TotalSteps}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyProgressUpdated(int puzzleId, int currentStep)
        {
            OnPuzzleProgressUpdated?.Invoke(puzzleId, currentStep);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_CompletePuzzle(int puzzleId, RpcInfo info = default)
        {
            CompletePuzzle(puzzleId);
        }
        
        private void CompletePuzzle(int puzzleId)
        {
            if (puzzleId < 0 || puzzleId >= _totalPuzzles)
                return;
                
            var progress = PuzzleProgresses.Get(puzzleId);
            if (progress.State == PuzzleState.Completed)
                return;
                
            progress.State = PuzzleState.Completed;
            progress.CompletionTime = TotalPlayTime;
            progress.CurrentStep = progress.TotalSteps;
            PuzzleProgresses.Set(puzzleId, progress);
            
            RPC_NotifyPuzzleCompleted(puzzleId);
            
            CheckAllPuzzlesCompleted();
            
            Debug.Log($"[NetworkedPuzzleManager] Puzzle {puzzleId} completed!");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPuzzleCompleted(int puzzleId)
        {
            OnPuzzleCompleted?.Invoke(puzzleId);
            
            if (_puzzleCallbacks.ContainsKey(puzzleId))
            {
                var progress = PuzzleProgresses.Get(puzzleId);
                foreach (var callback in _puzzleCallbacks[puzzleId])
                {
                    callback?.Invoke(progress);
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_FailPuzzle(int puzzleId, RpcInfo info = default)
        {
            if (puzzleId < 0 || puzzleId >= _totalPuzzles)
                return;
                
            var progress = PuzzleProgresses.Get(puzzleId);
            progress.State = PuzzleState.Failed;
            progress.CurrentStep = 0;
            PuzzleProgresses.Set(puzzleId, progress);
            
            RPC_NotifyPuzzleFailed(puzzleId);
            
            Debug.Log($"[NetworkedPuzzleManager] Puzzle {puzzleId} failed!");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPuzzleFailed(int puzzleId)
        {
            OnPuzzleFailed?.Invoke(puzzleId);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ResetPuzzle(int puzzleId, RpcInfo info = default)
        {
            if (puzzleId < 0 || puzzleId >= _totalPuzzles)
                return;
                
            var progress = PuzzleProgresses.Get(puzzleId);
            progress.State = PuzzleState.NotStarted;
            progress.CurrentStep = 0;
            PuzzleProgresses.Set(puzzleId, progress);
            
            Debug.Log($"[NetworkedPuzzleManager] Puzzle {puzzleId} reset");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_TransitionToRoom(string roomName)
        {
            CurrentRoomName = roomName;
            OnRoomTransition?.Invoke(roomName);
            Debug.Log($"[NetworkedPuzzleManager] Transitioning to room: {roomName}");
        }
        
        private void CheckAllPuzzlesCompleted()
        {
            bool allCompleted = true;
            for (int i = 0; i < _totalPuzzles; i++)
            {
                if (PuzzleProgresses.Get(i).State != PuzzleState.Completed)
                {
                    allCompleted = false;
                    break;
                }
            }
            
            if (allCompleted && !AllPuzzlesCompleted)
            {
                AllPuzzlesCompleted = true;
                RPC_NotifyAllPuzzlesCompleted();
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyAllPuzzlesCompleted()
        {
            OnAllPuzzlesCompleted?.Invoke();
            Debug.Log("[NetworkedPuzzleManager] All puzzles completed! Game finished!");
        }
        
        private bool ValidatePuzzleAccess(int puzzleId)
        {
            if (!_requireSequentialCompletion)
                return true;
                
            if (puzzleId == 0)
                return true;
                
            return PuzzleProgresses.Get(puzzleId - 1).State == PuzzleState.Completed;
        }
        
        public void RegisterPuzzleCallback(int puzzleId, Action<PuzzleProgress> callback)
        {
            if (!_puzzleCallbacks.ContainsKey(puzzleId))
                _puzzleCallbacks[puzzleId] = new List<Action<PuzzleProgress>>();
                
            _puzzleCallbacks[puzzleId].Add(callback);
        }
        
        public void UnregisterPuzzleCallback(int puzzleId, Action<PuzzleProgress> callback)
        {
            if (_puzzleCallbacks.ContainsKey(puzzleId))
                _puzzleCallbacks[puzzleId].Remove(callback);
        }
        
        public PuzzleProgress GetPuzzleProgress(int puzzleId)
        {
            if (puzzleId < 0 || puzzleId >= _totalPuzzles)
                return default;
                
            return PuzzleProgresses.Get(puzzleId);
        }
        
        public bool IsPuzzleCompleted(int puzzleId)
        {
            return GetPuzzleProgress(puzzleId).State == PuzzleState.Completed;
        }
        
        public bool CanStartPuzzle(int puzzleId)
        {
            return ValidatePuzzleAccess(puzzleId);
        }
        
        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                TotalPlayTime += Runner.DeltaTime;
            }
        }
        
        private void RegisterNetworkCallbacks()
        {
            Debug.Log($"[NetworkedPuzzleManager] Initialized with {_totalPuzzles} puzzles");
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}