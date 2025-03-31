using Fusion;
using UnityEngine;
using static Oculus.Avatar2.OvrAvatarEntity;

public class AvatarStateSync : NetworkBehaviour
{
    [SerializeField] AvatarEntityState avatarEntityState;

    [Networked] public ulong OculusID { get; set; }
    [Networked] private uint AvatarDataCount { get; set; }

    private const int AvatarDataSize = 1200;
    [Networked, Capacity(AvatarDataSize)] private NetworkArray<byte> AvatarData { get; }

    private byte[] byteArray = new byte[AvatarDataSize];

    public override void Spawned()
    {
        if (Object.HasStateAuthority) OculusID = UserEntitlement.OculusID;
    }

    public override void Render()
    {
        if (!Object.HasStateAuthority)
        {
            ApplyAvatarData();
        }
    }

    public void RecordAvatarState(StreamLOD streamLOD)
    {
        AvatarDataCount = avatarEntityState.RecordStreamData_AutoBuffer(streamLOD, ref byteArray);

        AvatarData.CopyFrom(byteArray, 0, byteArray.Length);
    }

    private void ApplyAvatarData()
    {
        if (AvatarDataCount == 0) return;
        
        var slicedData = new byte[AvatarDataCount];
        AvatarData.CopyTo(slicedData, throwIfOverflow: false);
        avatarEntityState.AddToDataBuffer(slicedData);
    }
}