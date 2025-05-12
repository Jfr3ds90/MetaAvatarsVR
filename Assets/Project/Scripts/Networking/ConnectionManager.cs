using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private NetworkRunner _networkRunner;
    [SerializeField] private NetworkEvents _networkEvents;

    [SerializeField] private NetworkObject[] _networkObjectsArray;

    private void Awake()
    {
        _networkObjectsArray = GetNetworkObjetcs();
        
        if (_networkRunner == null)
        {
            _networkRunner = GetComponent<NetworkRunner>();
        }

        if (_networkEvents == null)
        {
            _networkEvents = GetComponent<NetworkEvents>();
        }
        
        _networkEvents.OnConnectedToServer.AddListener(RegisterNetworkObjects);

    }

    private void Start()
    {
        _networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = SceneManager.GetActiveScene().name
        });
    }

    private void OnDestroy() => _networkEvents.OnConnectedToServer.RemoveListener(RegisterNetworkObjects);

    private void RegisterNetworkObjects(NetworkRunner runner) => runner.RegisterSceneObjects(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex) , _networkObjectsArray);

    private NetworkObject[] GetNetworkObjetcs() => FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
}