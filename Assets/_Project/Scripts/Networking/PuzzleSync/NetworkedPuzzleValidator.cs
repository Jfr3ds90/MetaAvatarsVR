using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
    public class NetworkedPuzzleValidator : NetworkBehaviour
    {
        [Header("Anti-Cheat Settings")]
        [SerializeField] private bool _enableValidation = true;
        [SerializeField] private float _maxInteractionRate = 10f; // Max interactions per second
        [SerializeField] private float _maxSequenceTime = 60f; // Max time to complete sequence
        [SerializeField] private int _maxFailureAttempts = 5; // Max failures before timeout
        [SerializeField] private float _failureTimeout = 30f; // Timeout duration after max failures
        
        [Header("Network State")]
        [Networked, Capacity(8)]
        public NetworkDictionary<PlayerRef, PlayerStats> PlayerStatsDict { get; }
        
        [Networked]
        public int TotalValidationChecks { get; set; }
        
        [Networked]
        public int TotalViolations { get; set; }
        
        public struct PlayerStats : INetworkStruct
        {
            public int InteractionCount;
            public float LastInteractionTime;
            public int FailureCount;
            public float TimeoutUntil;
            public NetworkBool IsSuspected;
            
            public PlayerStats(PlayerRef player)
            {
                InteractionCount = 0;
                LastInteractionTime = 0f;
                FailureCount = 0;
                TimeoutUntil = 0f;
                IsSuspected = false;
            }
        }
        
        private static NetworkedPuzzleValidator _instance;
        public static NetworkedPuzzleValidator Instance => _instance;
        
        private Dictionary<PlayerRef, Queue<float>> _recentInteractions = new Dictionary<PlayerRef, Queue<float>>();
        
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
            if (HasStateAuthority)
            {
                TotalValidationChecks = 0;
                TotalViolations = 0;
            }
        }
        
        public bool ValidatePlayerAction(PlayerRef player, string actionType, Vector3 position)
        {
            if (!_enableValidation || !HasStateAuthority)
                return true;
                
            TotalValidationChecks++;
            
            // Check if player is in timeout
            if (IsPlayerInTimeout(player))
            {
                LogViolation(player, "Action during timeout", actionType);
                return false;
            }
            
            // Check interaction rate
            if (!ValidateInteractionRate(player))
            {
                LogViolation(player, "Excessive interaction rate", actionType);
                return false;
            }
            
            // Update player stats
            UpdatePlayerStats(player);
            
            return true;
        }
        
        public bool ValidatePuzzleSequence(PlayerRef player, string puzzleType, int[] sequence, float sequenceTime)
        {
            if (!_enableValidation || !HasStateAuthority)
                return true;
                
            TotalValidationChecks++;
            
            // Check if player is in timeout
            if (IsPlayerInTimeout(player))
            {
                LogViolation(player, "Puzzle attempt during timeout", puzzleType);
                return false;
            }
            
            // Check sequence completion time (too fast = suspicious)
            if (sequenceTime < 0.5f && sequence.Length > 2)
            {
                LogViolation(player, "Suspiciously fast sequence completion", $"{puzzleType}: {sequenceTime}s");
                MarkPlayerSuspected(player);
                return false;
            }
            
            // Check maximum sequence time
            if (sequenceTime > _maxSequenceTime)
            {
                LogViolation(player, "Sequence timeout exceeded", $"{puzzleType}: {sequenceTime}s");
                return false;
            }
            
            return true;
        }
        
        public bool ValidateObjectInteraction(PlayerRef player, NetworkObject networkObject, Vector3 playerPosition)
        {
            if (!_enableValidation || !HasStateAuthority)
                return true;
                
            TotalValidationChecks++;
            
            // Check distance validation (prevent remote interactions)
            float distance = Vector3.Distance(playerPosition, networkObject.transform.position);
            if (distance > 5f) // Max interaction distance
            {
                LogViolation(player, "Remote interaction attempt", $"Distance: {distance:F1}m");
                return false;
            }
            
            // Check if player is in timeout
            if (IsPlayerInTimeout(player))
            {
                LogViolation(player, "Interaction during timeout", networkObject.name);
                return false;
            }
            
            return ValidatePlayerAction(player, "Object Interaction", playerPosition);
        }
        
        private bool IsPlayerInTimeout(PlayerRef player)
        {
            if (PlayerStatsDict.TryGet(player, out PlayerStats stats))
            {
                return Runner.SimulationTime < stats.TimeoutUntil;
            }
            return false;
        }
        
        private bool ValidateInteractionRate(PlayerRef player)
        {
            float currentTime = Runner.SimulationTime;
            
            if (!_recentInteractions.ContainsKey(player))
            {
                _recentInteractions[player] = new Queue<float>();
            }
            
            var interactions = _recentInteractions[player];
            
            // Remove old interactions (older than 1 second)
            while (interactions.Count > 0 && currentTime - interactions.Peek() > 1f)
            {
                interactions.Dequeue();
            }
            
            // Check if rate limit exceeded
            if (interactions.Count >= _maxInteractionRate)
            {
                return false;
            }
            
            // Add current interaction
            interactions.Enqueue(currentTime);
            return true;
        }
        
        private void UpdatePlayerStats(PlayerRef player)
        {
            PlayerStats stats;
            if (!PlayerStatsDict.TryGet(player, out stats))
            {
                stats = new PlayerStats(player);
            }
            
            stats.InteractionCount++;
            stats.LastInteractionTime = Runner.SimulationTime;
            
            PlayerStatsDict.Set(player, stats);
        }
        
        private void MarkPlayerSuspected(PlayerRef player)
        {
            PlayerStats stats;
            if (!PlayerStatsDict.TryGet(player, out stats))
            {
                stats = new PlayerStats(player);
            }
            
            stats.IsSuspected = true;
            PlayerStatsDict.Set(player, stats);
            
            Debug.LogWarning($"[NetworkedPuzzleValidator] Player {player} marked as suspected");
        }
        
        public void RegisterFailure(PlayerRef player, string failureType)
        {
            if (!HasStateAuthority)
                return;
                
            PlayerStats stats;
            if (!PlayerStatsDict.TryGet(player, out stats))
            {
                stats = new PlayerStats(player);
            }
            
            stats.FailureCount++;
            
            if (stats.FailureCount >= _maxFailureAttempts)
            {
                stats.TimeoutUntil = Runner.SimulationTime + _failureTimeout;
                RPC_NotifyPlayerTimeout(player, _failureTimeout);
                
                Debug.LogWarning($"[NetworkedPuzzleValidator] Player {player} timed out for {_failureTimeout}s");
            }
            
            PlayerStatsDict.Set(player, stats);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPlayerTimeout(PlayerRef player, float duration)
        {
            Debug.Log($"[NetworkedPuzzleValidator] Player {player} has been timed out for {duration} seconds");
        }
        
        private void LogViolation(PlayerRef player, string violationType, string details)
        {
            TotalViolations++;
            Debug.LogWarning($"[NetworkedPuzzleValidator] VIOLATION: Player {player} - {violationType}: {details}");
            
            // Could send to analytics or ban system here
        }
        
        public bool CanPlayerInteract(PlayerRef player)
        {
            if (!_enableValidation)
                return true;
                
            return !IsPlayerInTimeout(player);
        }
        
        public PlayerStats GetPlayerStats(PlayerRef player)
        {
            PlayerStatsDict.TryGet(player, out PlayerStats stats);
            return stats;
        }
        
        public void ResetPlayerStats(PlayerRef player)
        {
            if (HasStateAuthority)
            {
                PlayerStatsDict.Remove(player);
                if (_recentInteractions.ContainsKey(player))
                {
                    _recentInteractions[player].Clear();
                }
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ReportSuspiciousActivity(PlayerRef suspectedPlayer, string activityType, string evidence, RpcInfo info = default)
        {
            if (info.Source == suspectedPlayer)
            {
                // Player reporting themselves? Suspicious
                LogViolation(info.Source, "Self-reporting suspicious activity", activityType);
                return;
            }
            
            LogViolation(suspectedPlayer, $"Reported by {info.Source}", $"{activityType}: {evidence}");
        }
        
        public void EnableValidation(bool enable)
        {
            _enableValidation = enable;
            Debug.Log($"[NetworkedPuzzleValidator] Validation {(enable ? "enabled" : "disabled")}");
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
                
            // Clean up old interaction history periodically
            if (Runner.Tick % 60 == 0) // Every second
            {
                CleanupOldInteractions();
            }
        }
        
        private void CleanupOldInteractions()
        {
            float currentTime = Runner.SimulationTime;
            var playersToRemove = new List<PlayerRef>();
            
            foreach (var kvp in _recentInteractions)
            {
                var interactions = kvp.Value;
                
                // Remove old interactions
                while (interactions.Count > 0 && currentTime - interactions.Peek() > 60f)
                {
                    interactions.Dequeue();
                }
                
                // Mark empty queues for cleanup
                if (interactions.Count == 0)
                {
                    playersToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var player in playersToRemove)
            {
                _recentInteractions.Remove(player);
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}