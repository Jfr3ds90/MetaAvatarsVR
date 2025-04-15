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
            Entitlements.IsUserEntitledToApplication().OnComplete(IsUserEntitledToApplicationComplete);
        }
        catch (Exception e)
        {
            Debug.LogError($"Platfomr failed to initialize due to exception: {e}");
            throw;
        }
    }

    private void IsUserEntitledToApplicationComplete(Message message)
    {
        if (message.IsError)
        {
            Debug.LogError(message.GetError());
            return;
        }
        
        Debug.Log($"You are entitled to use this application");
        Users.GetAccessToken().OnComplete(GetAccsessTokenComplete);
    }

    private void GetAccsessTokenComplete(Message<string> message)
    {
        if (message.IsError)
        {
            Debug.LogError(message.GetError());
            return; 
        }
        
        OvrAvatarEntitlement.SetAccessToken(message.Data);
         Users.GetLoggedInUser().OnComplete(GetLoggedInUserComplete);
    }

    private void GetLoggedInUserComplete(Message<User> message)
    {
        if (message.IsError)
        {
            Debug.LogError(message.GetError());
            return;
        }

        OculusID = message.Data.ID;
        OnEntitlementGranted?.Invoke();
    }
}