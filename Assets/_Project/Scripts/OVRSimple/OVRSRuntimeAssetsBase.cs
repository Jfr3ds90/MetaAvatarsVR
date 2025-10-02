using UnityEngine;
using System.IO;
using System;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

/// <summary>
/// Base class for runtime assets with common functions.
/// </summary>
public class OVRSRuntimeAssetsBase : ScriptableObject
{
#if UNITY_EDITOR
    internal static string GetAssetPath(string assetName)
    {
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (!Directory.Exists(resourcesPath))
        {
            Directory.CreateDirectory(resourcesPath);
        }

        string assetPath = Path.GetFullPath(Path.Combine(resourcesPath, $"{assetName}.asset"));
        Uri configUri = new Uri(assetPath);
        Uri projectUri = new Uri(Application.dataPath);
        Uri relativeUri = projectUri.MakeRelativeUri(configUri);

        return relativeUri.ToString();
    }

    public bool AddToPreloadedAssets()
    {
        var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();

        if (!preloadedAssets.Contains(this))
        {
            preloadedAssets.Add(this);
            PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
            return true;
        }

        return false;
    }

    public bool RemoveFromPreloadedAssets()
    {
        var preloaded = PlayerSettings.GetPreloadedAssets().ToList();
        if (preloaded.RemoveAll(a => a == this) > 0)
        {
            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
            return true;
        }

        return false;
    }
#endif



    internal static void LoadAsset<T>(out T assetInstance, string assetName, Action<T> onCreateAsset = null) where T : OVRSRuntimeAssetsBase
    {
        assetInstance = null;
#if UNITY_EDITOR
        string instanceAssetPath = GetAssetPath(assetName);
        try
        {
            assetInstance =
                AssetDatabase.LoadAssetAtPath(instanceAssetPath, typeof(T)) as T;
        }
        catch (System.Exception e)
        {
            Debug.LogWarningFormat("Unable to load {0} from {1}, error {2}", assetName, instanceAssetPath,
                e.Message);
        }

        if (assetInstance == null && !BuildPipeline.isBuildingPlayer)
        {
            assetInstance = ScriptableObject.CreateInstance<T>();

            AssetDatabase.CreateAsset(assetInstance, GetAssetPath(assetName));
            onCreateAsset?.Invoke(assetInstance);
        }
#else
        assetInstance = Resources.Load<T>(assetName);
#endif
    }
}
