using System;
using Oculus.Avatar2;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;
using HackMonkeys.Debugging;

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
            AdvancedDebugSystem.LogError("Platform failed to initialize", LogCategory.Networking | LogCategory.Photon);
            AdvancedDebugSystem.LogError(e.ToString(), LogCategory.Networking | LogCategory.Photon);
            throw;
        }
    }

    private void IsUserEntitledToApplicationCompleted(Message msg)
    {
        if (msg.IsError)
        {
            AdvancedDebugSystem.LogError($"Error: {msg.GetError()}", LogCategory.Networking | LogCategory.Photon);
            return;
        }
        
        AdvancedDebugSystem.Log($"You are entitled to application", LogCategory.Networking | LogCategory.Photon, LogLevel.Debug);
        Users.GetAccessToken().OnComplete(GetAccessTokenCompleted);
    }

    private void GetAccessTokenCompleted(Message<string> msg)
    {
        if (msg.IsError)
        {
            AdvancedDebugSystem.LogError($"Error geting the token", LogCategory.Networking | LogCategory.Photon);
            AdvancedDebugSystem.LogError(msg.GetError().ToString(), LogCategory.Networking | LogCategory.Photon);
            return;
        }
        
        OvrAvatarEntitlement.SetAccessToken(msg.Data);
        Users.GetLoggedInUser().OnComplete(GetLoggedInUserCompleted);
    }

    private void GetLoggedInUserCompleted(Message<User> msg)
    {
        if (msg.IsError)
        {
            AdvancedDebugSystem.LogError($"Error on logged user", LogCategory.Networking | LogCategory.Photon);
            AdvancedDebugSystem.LogError(msg.GetError().ToString(), LogCategory.Networking | LogCategory.Photon);
            return;
        }

        OculusID = msg.Data.ID;
        OnEntitlementGranted?.Invoke();
    }
}
