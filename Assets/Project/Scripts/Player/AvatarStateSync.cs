using Fusion;
using UnityEngine;
using static Oculus.Avatar2.OvrAvatarEntity;

public class AvatarStateSync : NetworkBehaviour
{
    [SerializeField] private AvatarEntityState avatarEntityState;
    [Networked] public ulong OculusID { get; set; }
    [Networked] private uint AvatarDataCount { get; set; }
    
    private const int AvatarDataSize = 1200;
    [Networked, Capacity(AvatarDataSize)] private NetworkArray<byte> AvatarData { get; }
    
    private byte[] byteArray = new byte[AvatarDataSize];
    private uint _lastAppliedCount = 0;
    
    public override void Spawned()
    {
        if (Object.HasStateAuthority) OculusID = UserEntitlement.OculusID;
    }
    
    public override void Render()
    {
        if (!Object.HasStateAuthority && _lastAppliedCount != AvatarDataCount && AvatarDataCount > 0)
        {
            _lastAppliedCount = AvatarDataCount;
            ApplyAvatarData();
        }
    }
    
    public void RecordAvatarState(StreamLOD streamLOD)
    {
        if (!Object.HasStateAuthority) return;
        
        AvatarDataCount = avatarEntityState.RecordStreamData_AutoBuffer(streamLOD, ref byteArray);
        
        AvatarData.CopyFrom(byteArray, 0, (int)AvatarDataCount);
    }
    
    private void ApplyAvatarData()
    {
        if (AvatarDataCount == 0) return;
        
        var slicedData = new byte[AvatarDataCount];
        
        AvatarData.CopyTo(slicedData, throwIfOverflow: false);
        
        avatarEntityState.AddToDataBuffer(slicedData);
    }
}