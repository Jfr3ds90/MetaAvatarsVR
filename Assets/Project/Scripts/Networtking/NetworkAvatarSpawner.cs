 using System;
using Fusion;
using UnityEngine;

public class NetworkAvatarSpawner : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private NetworkRunner _networkRunner;

    [SerializeField] private NetworkEvents _networkEvents;
    [SerializeField] private UserEntitlement _userEntitlement;

    [Header("Prefabs")] 
    [SerializeField] private NetworkObject _avatarPrefeb;

    [Header("Ovr Rig")] 
    [SerializeField] private Transform _cameraRigTransform;

    [Header("Spawn Points")] [SerializeField]
    private Transform[] _spawnPoints;

    [SerializeField] private bool _isServerConnected = false;
    [SerializeField] private bool _isEntitlementGranted = false;

    private void Awake()
    {
        _networkEvents.OnConnectedToServer.AddListener(ConnectToServer);
        _userEntitlement.OnEntitlementGranted += EntitlementGaranted;
    }

    private void OnDestroy()
    {
        _networkEvents.OnConnectedToServer.RemoveListener(ConnectToServer);
        _userEntitlement.OnEntitlementGranted -= EntitlementGaranted;
    }

    private void ConnectToServer(NetworkRunner runner)
    {
        _isServerConnected = true;
        TrySpawnAvatar();
    }

    private void EntitlementGaranted()
    {
        _isEntitlementGranted = true;
        TrySpawnAvatar();
    }

    private void TrySpawnAvatar()
    {
        if (!_isServerConnected || !_isEntitlementGranted) return;

        SetPlayerSpawnPosition();
        SpawnAvatar();
    }

    private void SetPlayerSpawnPosition()
    {
        Vector3 boxSize = new(1, 1, 1);

        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            bool isOccupied = Physics.CheckBox(_spawnPoints[i].position, boxSize, Quaternion.identity,
                LayerMask.GetMask("Player"));
            if (!isOccupied)
            {
                _cameraRigTransform.SetPositionAndRotation(_spawnPoints[i].position, _spawnPoints[i].rotation);
                break;
            }
        }
    }

    private void SpawnAvatar()
    {
        var avatar = _networkRunner.Spawn(_avatarPrefeb, _cameraRigTransform.position, _cameraRigTransform.rotation, _networkRunner.LocalPlayer);
        avatar.transform.SetParent(_cameraRigTransform);
    }
}