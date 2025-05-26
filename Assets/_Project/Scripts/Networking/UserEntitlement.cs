using System;
using Oculus.Avatar2;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

public class UserEntitlement : MonoBehaviour
{
    public static ulong OculusID;

    public Action OnEntitlementGranted;

    private void Awake() => EntitlementCheck();

    private void EntitlementCheck()
    {
        try
        {
            Core.AsyncInitialize();
            Entitlements.IsUserEntitledToApplication().OnComplete(IsUserEntitledToApplicationCompleted);
        }
        catch (Exception e)
        {
            Debug.LogError("Platform failed to initialize");
            Debug.LogError(e);
            throw;
        }
    }

    private void IsUserEntitledToApplicationCompleted(Message msg)
    {
        if (msg.IsError)
        {
            Debug.LogError($"Error: {msg.GetError()}");
            return;
        }
        
        Debug.Log($"You are entitled to application");
        Users.GetAccessToken().OnComplete(GetAccessTokenCompleted);
    }

    private void GetAccessTokenCompleted(Message<string> msg)
    {
        if (msg.IsError)
        {
            Debug.LogError($"Error geting the token");
            Debug.LogError(msg.GetError());
            return;
        }
        
        OvrAvatarEntitlement.SetAccessToken(msg.Data);
        Users.GetLoggedInUser().OnComplete(GetLoggedInUserCompleted);
    }

    private void GetLoggedInUserCompleted(Message<User> msg)
    {
        if (msg.IsError)
        {
            Debug.LogError($"Error on logged user");
            Debug.LogError(msg.GetError());
            return;
        }

        OculusID = msg.Data.ID;
        OnEntitlementGranted?.Invoke();
    }
}
