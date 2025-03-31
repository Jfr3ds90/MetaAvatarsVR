using System;
using UnityEngine;
using Oculus.Avatar2;
using Oculus.Platform;
using Oculus.Platform.Models;

public class UserEntitlement : MonoBehaviour
{
    public static ulong OculusID;

    public Action OnEntitlementGranted;

    private void Awake() => EntitlementCheck();

    // initializes the Oculus Platform asynchronously and checks if the user is entitled to use the application. If the platform fails to initialize, it logs the exception.
    private void EntitlementCheck()
    {
        try
        {
            Core.AsyncInitialize();
            Entitlements.IsUserEntitledToApplication().OnComplete(IsUserEntitledToApplicationComplete);
        }
        catch (UnityException e)
        {
            Debug.LogError("Platform failed to initialize due to exception.");
            Debug.LogException(e);
        }
    }

    // If the user is not entitled to use the app, it logs an error. If the user is entitled, it retrieves the user's access token.
    private void IsUserEntitledToApplicationComplete(Message message)
    {
        if (message.IsError)
        {
            Debug.LogError(message.GetError());
            return;
        }

        Debug.Log("You are entitled to use this app.");

        Users.GetAccessToken().OnComplete(GetAccessTokenComplete);
    }

    // If successful, it sets the access token for the OvrAvatarEntitlement and tries to retrieves the logged-in user's information.
    private void GetAccessTokenComplete(Message<string> message)
    {
        if (message.IsError)
        {
            Debug.LogError(message.GetError());
            return;
        }

        OvrAvatarEntitlement.SetAccessToken(message.Data);

        Users.GetLoggedInUser().OnComplete(GetLoggedInUserComplete);
    }

    // If successful, we get the logged-in user's information.
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
