using Fusion;
using UnityEngine;
using Oculus.Avatar2;
using System.Collections;
using System.Collections.Generic;

public class AvatarEntityState : OvrAvatarEntity
{
    [SerializeField] private AvatarStateSync avatarStateSync;
    [SerializeField] private OvrAvatarLipSyncContext lipSyncContext;

    [SerializeField] float intervalDataStream = 0.05f;
    [SerializeField] private int maxDataBuffer = 6;
    [SerializeField] StreamLOD streamLOD = StreamLOD.Low;

    [SerializeField] private NetworkObject networkObject;

    private List<byte[]> receivedDataBuffer = new();
    private float lastStreamedTime;

    protected override void Awake() { }

    private void Start()
    {
        ConfigureAvatarEntity();

        base.Awake();

        SetActiveView(networkObject.HasStateAuthority ? CAPI.ovrAvatar2EntityViewFlags.FirstPerson : CAPI.ovrAvatar2EntityViewFlags.ThirdPerson);
        StartCoroutine(LoadAvatarID());
    }

    private void ConfigureAvatarEntity()
    {
        if (networkObject.HasStateAuthority)
        {
            SetIsLocal(true);

            _creationInfo.features = CAPI.ovrAvatar2EntityFeatures.Preset_Default;
            SetBodyTracking(OvrAvatarManager.Instance.gameObject.GetComponent<SampleInputManager>());

            gameObject.name = "Local Avatar";
        }
        else
        {
            SetIsLocal(false);
            SetLipSync(null);

            lipSyncContext.CaptureAudio = false;

            _creationInfo.features = CAPI.ovrAvatar2EntityFeatures.Preset_Remote;

            gameObject.name = "Remote Avatar";
        }
    }

    private IEnumerator LoadAvatarID()
    {
        while (avatarStateSync.OculusID == 0) yield return null;

        _userId = avatarStateSync.OculusID;
        var avatarRequest = OvrAvatarManager.Instance.UserHasAvatarAsync(_userId);
        while (!avatarRequest.IsCompleted) yield return null;

        LoadUser();
    }

    private void LateUpdate()
    {
        if (!IsLocal || CurrentState != AvatarState.UserAvatar) return;

        if (intervalDataStream > Time.time - lastStreamedTime) return;

        avatarStateSync.RecordAvatarState(streamLOD);

        lastStreamedTime = Time.time;
    }

    public void AddToDataBuffer(byte[] avatarStateData)
    {
        if (receivedDataBuffer.Count >= maxDataBuffer) receivedDataBuffer.RemoveAt(receivedDataBuffer.Count - 1);

        receivedDataBuffer.Add(avatarStateData);
    }

    private void Update()
    {
        if (IsLocal || CurrentState != AvatarState.UserAvatar || receivedDataBuffer.Count <= 0) return;

        ApplyStreamData(receivedDataBuffer[0]);

        SetPlaybackTimeDelay(intervalDataStream);

        receivedDataBuffer.RemoveAt(0);
    }
}
