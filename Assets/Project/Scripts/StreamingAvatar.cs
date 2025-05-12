using System;
using System.Collections;
using UnityEngine;
using Oculus.Avatar2;

public class StreamingAvatar : OvrAvatarEntity
{
    public OculusStuff _playerCon;
    public bool isLocal;

    public void StartAvatar(OculusStuff playerCon)
    {
        if (isLocal)
        {
            _playerCon = playerCon;
            _userId = _playerCon._userId;

            StartCoroutine(LoadAvatarWithID());
        }
    }

    private IEnumerator LoadAvatarWithID()
    {
        var hasAvatarRequest = OvrAvatarManager.Instance.UserHasAvatarAsync(_userId);
        while (!hasAvatarRequest.IsCompleted) { yield return null; }

        LoadUser();
    }
}