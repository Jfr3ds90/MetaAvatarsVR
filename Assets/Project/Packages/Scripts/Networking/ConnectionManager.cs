using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ConnectionManager : MonoBehaviour
{
    private NetworkRunner networkRunner;
    private NetworkEvents networkEvents;

    private List<NetworkObject> networkObjectList;

    private void Awake()
    {
        networkObjectList = new List<NetworkObject>(GetNetworkObjects());

        networkRunner = GetComponent<NetworkRunner>();
        networkEvents = GetComponent<NetworkEvents>();

        networkEvents.OnConnectedToServer.AddListener(RegisterNetworkObjects);
    }

    private void OnDestroy() => networkEvents.OnConnectedToServer.RemoveListener(RegisterNetworkObjects);

    private void Start()
    {
        networkRunner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = SceneManager.GetActiveScene().buildIndex.ToString(),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    private NetworkObject[] GetNetworkObjects() => FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

    private void RegisterNetworkObjects(NetworkRunner runner)
    {
        // En versiones recientes, usamos SceneRef.Current
        //runner.SceneManager.RegisterSceneObjects(networkObjectList, SceneRef.Current);
    }
}