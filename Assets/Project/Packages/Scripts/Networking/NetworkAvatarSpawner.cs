using Fusion;
using UnityEngine;

public class NetworkAvatarSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkRunner networkRunner;
    [SerializeField] private NetworkEvents networkEvents;
    [SerializeField] private UserEntitlement userEntitlement;

    [Header("Prefabs")]
    [SerializeField] private NetworkObject avatarPrefab;

    [Header("Ovr Rig")]
    [SerializeField] Transform cameraRigTransform;

    [Header("Spawn Points")]
    [SerializeField] Transform[] spawnPoints;

    private bool isServerConnected = false;
    private bool isEntitlementGranted = false;

    private void Awake()
    {
        networkEvents.OnConnectedToServer.AddListener(ConnectedToServer);
        userEntitlement.OnEntitlementGranted += EntintlementGranted;
    }

    private void OnDestroy()
    {
        networkEvents.OnConnectedToServer.RemoveListener(ConnectedToServer);
        userEntitlement.OnEntitlementGranted -= EntintlementGranted;
    }

    private void ConnectedToServer(NetworkRunner runner)
    {
        isServerConnected = true;
        TrySpawnAvatar();
    }

    private void EntintlementGranted()
    {
        isEntitlementGranted = true;
        TrySpawnAvatar();
    }

    private void TrySpawnAvatar()
    {
        if (!isServerConnected || !isEntitlementGranted) return;

        SetPlayerSpawnPosition();
        SpawnAvatar();
    }

    private void SetPlayerSpawnPosition()
    {
        Vector3 boxSize = new(1, 1, 1);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            bool isOccupied = Physics.CheckBox(spawnPoints[i].position, boxSize, spawnPoints[i].rotation, LayerMask.GetMask("Player"));

            if (!isOccupied)
            {
                cameraRigTransform.SetPositionAndRotation(spawnPoints[i].position, spawnPoints[i].rotation);
                break;
            }
        }
    }

    private void SpawnAvatar()
    {
        var avatar = networkRunner.Spawn(avatarPrefab, cameraRigTransform.position, cameraRigTransform.rotation, networkRunner.LocalPlayer);
        avatar.transform.SetParent(cameraRigTransform);
    }
}
