using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;


public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private NetworkRunner _networkRunner;
    [SerializeField] private NetworkEvents _networkEvents;
    [SerializeField] private NetworkObject[] _networkObjectsArray;

    private void Awake()
    {
        _networkObjectsArray = GetNetworkObjects();
        _networkRunner = GetComponent<NetworkRunner>();
        _networkEvents = GetComponent<NetworkEvents>();

        _networkEvents.OnConnectedToServer.AddListener(RegisterNetworkObjects);
    }

    private void Start()
    {
        _networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = SceneManager.GetActiveScene().name,
            // Es necesario añadir el SceneManager para gestionar escenas en Fusion
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    private void OnDestroy() => _networkEvents.OnConnectedToServer.RemoveListener(RegisterNetworkObjects);

    private NetworkObject[] GetNetworkObjects() => FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

    private void RegisterNetworkObjects(NetworkRunner runner) =>
        runner.RegisterSceneObjects(SceneRef.FromIndex(0), _networkObjectsArray);
}