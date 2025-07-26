using UnityEngine;
using Fusion;
using System.Collections;
using HackMonkeys.Core;
using HackMonkeys.Gameplay;

namespace HackMonkeys.DebugNet
{
    /// <summary>
    /// Script de diagnóstico para entender el flujo de red
    /// </summary>
    public class DebugNetworkFlow : MonoBehaviour
    {
        private NetworkBootstrapper _networkBootstrapper;
        private GameCore _gameCore;
        private LobbyController _lobbyController;
        
        private void Start()
        {
            StartCoroutine(MonitorFlow());
        }
        
        private IEnumerator MonitorFlow()
        {
            while (true)
            {
                // Buscar instancias
                if (_networkBootstrapper == null)
                    _networkBootstrapper = NetworkBootstrapper.Instance;
                if (_gameCore == null)
                    _gameCore = GameCore.Instance;
                if (_lobbyController == null)
                    _lobbyController = LobbyController.Instance;
                
                yield return new WaitForSeconds(1f);
            }
        }
        
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 600));
            
            GUILayout.Label("=== NETWORK FLOW DEBUG ===");
            
            // NetworkBootstrapper Status
            if (_networkBootstrapper != null)
            {
                GUILayout.Label($"NetworkBootstrapper: EXISTS");
                GUILayout.Label($"  - IsConnected: {_networkBootstrapper.IsConnected}");
                GUILayout.Label($"  - IsInRoom: {_networkBootstrapper.IsInRoom}");
                GUILayout.Label($"  - IsHost: {_networkBootstrapper.IsHost}");
                GUILayout.Label($"  - CurrentRoom: {_networkBootstrapper.CurrentRoomName}");
                GUILayout.Label($"  - Runner: {_networkBootstrapper.Runner != null}");
                
                if (_networkBootstrapper.Runner != null)
                {
                    var runner = _networkBootstrapper.Runner;
                    GUILayout.Label($"  - Runner.IsRunning: {runner.IsRunning}");
                    GUILayout.Label($"  - Runner.IsServer: {runner.IsServer}");
                    GUILayout.Label($"  - Runner.GameMode: {runner.GameMode}");
                    GUILayout.Label($"  - Active Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                }
            }
            else
            {
                GUILayout.Label("NetworkBootstrapper: NULL");
            }
            
            GUILayout.Space(10);
            
            // GameCore Status
            if (_gameCore != null)
            {
                GUILayout.Label($"GameCore State: {_gameCore.CurrentState}");
            }
            
            GUILayout.Space(10);
            
            // NetworkRunner Instances
            GUILayout.Label($"Active NetworkRunners: {NetworkRunner.Instances.Count}");
            foreach (var runner in NetworkRunner.Instances)
            {
                GUILayout.Label($"  - {runner.name}: {runner.GameMode}");
            }
            
            GUILayout.Space(10);
            
            // GameplayManager
            var gameplayManager = FindObjectOfType<GameplayManager>();
            GUILayout.Label($"GameplayManager: {(gameplayManager != null ? "EXISTS" : "NOT FOUND")}");
            
            // Test Buttons
            if (GUILayout.Button("Test LoadScene Directly"))
            {
                TestLoadSceneDirectly();
            }
            
            if (GUILayout.Button("List All Callbacks"))
            {
                ListAllCallbacks();
            }
            
            GUILayout.EndArea();
        }
        
        private void TestLoadSceneDirectly()
        {
            if (_networkBootstrapper?.Runner != null && _networkBootstrapper.Runner.IsServer)
            {
                Debug.Log("[DEBUG] Testing LoadScene directly...");
                var sceneRef = SceneRef.FromIndex(1); // Ajusta el índice
                _networkBootstrapper.Runner.LoadScene(sceneRef);
            }
        }
        
        private void ListAllCallbacks()
        {
            // if (_networkBootstrapper?.Runner != null)
            // {
            //     Debug.Log("[DEBUG] Listing all registered callbacks...");
            //     var callbacks = _networkBootstrapper.Runner.GetCallbacks();
            //     Debug.Log($"[DEBUG] Total callbacks: {callbacks.Count()}");
            //     foreach (var callback in callbacks)
            //     {
            //         Debug.Log($"[DEBUG] - Callback: {callback.GetType().Name}");
            //     }
            // }
        }
    }
}