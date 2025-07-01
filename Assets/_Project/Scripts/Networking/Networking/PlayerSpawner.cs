using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Se encarga de spawnear el LobbyPlayer cuando un jugador se conecta
    /// </summary>
    public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Prefabs")]
        [SerializeField] private LobbyPlayer lobbyPlayerPrefab;
        
        private NetworkRunner _runner;
        
        private void Start()
        {
            // Buscar el NetworkRunner cuando se cree
            StartCoroutine(WaitForRunner());
        }
        
        private System.Collections.IEnumerator WaitForRunner()
        {
            while (_runner == null)
            {
                var runner = FindObjectOfType<NetworkRunner>();
                if (runner != null && runner.IsRunning)
                {
                    _runner = runner;
                    _runner.AddCallbacks(this);
                    Debug.Log("[PlayerSpawner] Connected to NetworkRunner");
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[PlayerSpawner] Player {player} joined");
            
            // Solo el host/server spawnea objetos
            if (runner.IsServer)
            {
                // Spawn del LobbyPlayer para este jugador
                Vector3 spawnPosition = Vector3.zero;
                NetworkObject networkPlayerObject = runner.Spawn(
                    lobbyPlayerPrefab.gameObject, 
                    spawnPosition, 
                    Quaternion.identity, 
                    player
                );
                
                Debug.Log($"[PlayerSpawner] Spawned LobbyPlayer for player {player}");
            }
        }
        
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[PlayerSpawner] Player {player} left");
            
            // El NetworkObject se destruye automáticamente cuando el jugador se desconecta
        }
        
        // Implementación vacía de otros callbacks
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        
        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
            }
        }
    }
}