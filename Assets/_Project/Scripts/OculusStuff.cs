using System;
using Oculus.Avatar2;
using UnityEngine;
using Oculus.Platform;
using System.Collections;

public class OculusStuff : MonoBehaviour
{
    public UInt64 _userId;
    public const string SAMPLE_AVATAR = "Sample Avatar";
    [SerializeField] private StreamingAvatar _streamingAvatar;
    
    void Awake()
    {
        try
        {
            Core.AsyncInitialize().OnComplete(InitializeCallback);
        }
        catch (UnityException e)
        {
            Debug.LogErrorFormat("Platform failed to initialize due to exception: %s.", e.Message);
            UnityEngine.Application.Quit();
        }
    }

    void InitializeCallback(Message msg)
    {
        if (msg.IsError)
        {
            var err = msg.GetError();
            Debug.LogErrorFormat("Platform failed to initialize due to exception: %s.", err.ToString());
            UnityEngine.Application.Quit();
        }
        else
        {
            Entitlements.IsUserEntitledToApplication().OnComplete(EntitlementCallback);
        }
    }

    void EntitlementCallback(Message msg)
    {
        if (msg.IsError)
        {
            // Implements a default behavior for an entitlement check failure -- log the failure and exit the app.
            // Going into a limited demo mode, or displaying an error, is also valid.
            var err = msg.GetError();
            Debug.LogError("Error de verificación de derechos: " + msg.GetError().Message + " | Código: " + msg.GetError().Code);
            Debug.LogErrorFormat($"Entitlement check failed: %s. {err}", err.Message);
            UnityEngine.Application.Quit();
        }
        else
        {
            Debug.Log("You are entitled to use this app.");
            StartCoroutine(StartOvrPlatform());
        }
    }
    
    private IEnumerator StartOvrPlatform()
    {

        // Ensure OvrPlatform is Initialized
        if (OvrPlatformInit.status == OvrPlatformInitStatus.NotStarted)
        {
            OvrPlatformInit.InitializeOvrPlatform();
        }

        while (OvrPlatformInit.status != OvrPlatformInitStatus.Succeeded)
        {
            if (OvrPlatformInit.status == OvrPlatformInitStatus.Failed)
            {
                OvrAvatarLog.LogError($"Error initializing OvrPlatform. Falling back to local avatar", SAMPLE_AVATAR);
                //LoadLocalAvatar();
                yield break;
            }

            yield return null;
        }

        // user ID == 0 means we want to load logged in user avatar from CDN
        if (_userId == 0)
        {
            // Get User ID
            bool getUserIdComplete = false;
            Users.GetLoggedInUser().OnComplete(message =>
            {
                if (!message.IsError)
                {
                    _userId = message.Data.ID;
                    _streamingAvatar.gameObject.SetActive(true);
                    _streamingAvatar._playerCon = this;
                    _streamingAvatar.StartAvatar(this);
                }
                else
                {
                    var e = message.GetError();
                    OvrAvatarLog.LogError($"Error loading CDN avatar: {e.Message}. Falling back to local avatar", SAMPLE_AVATAR);
                }

                //getUserIdComplete = true;
            });

            //while (!getUserIdComplete) { yield return null; }
        }
        //yield return LoadUserAvatar();
    }
    /*
    private IEnumerator LoadUserAvatar()
    {
        if (_userId == 0)
        {
            LoadLocalAvatar();
            yield break;
        }

        yield return Retry_HasAvatarRequest();
    }
    */
}
