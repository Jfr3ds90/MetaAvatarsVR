#if USING_XR_MANAGEMENT && (USING_XR_SDK_OCULUS || USING_XR_SDK_OPENXR)
#define USING_XR_SDK
#endif

#if UNITY_2020_1_OR_NEWER
#define REQUIRES_XR_SDK
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
#define OVR_ANDROID_MRC
#endif

#if UNITY_Y_FLIP_FIX_2021 || UNITY_Y_FLIP_FIX_2022 || UNITY_Y_FLIP_FIX_6
#define UNITY_Y_FLIP_FIX
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine.Rendering;

#if USING_XR_SDK
using UnityEngine.XR;
using UnityEngine.Experimental.XR;
#endif

#if USING_XR_SDK_OPENXR
using Meta.XR;
using UnityEngine.XR.OpenXR;
#endif

#if USING_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

#if USING_URP
using UnityEngine.Rendering.Universal;
#endif

#if USING_XR_SDK_OCULUS
using Unity.XR.Oculus;
#endif

using Settings = UnityEngine.XR.XRSettings;
using Node = UnityEngine.XR.XRNode;

public class OVRSimple : MonoBehaviour
{
    public enum XrApi
    {
        Unknown = OVRPlugin.XrApi.Unknown,
        CAPI = OVRPlugin.XrApi.CAPI,
        VRAPI = OVRPlugin.XrApi.VRAPI,
        OpenXR = OVRPlugin.XrApi.OpenXR,
    }

    public enum TrackingOrigin
    {
        EyeLevel = OVRPlugin.TrackingOrigin.EyeLevel,
        FloorLevel = OVRPlugin.TrackingOrigin.FloorLevel,
        Stage = OVRPlugin.TrackingOrigin.Stage,
    }

    public enum EyeTextureFormat
    {
        Default = OVRPlugin.EyeTextureFormat.Default,
        R16G16B16A16_FP = OVRPlugin.EyeTextureFormat.R16G16B16A16_FP,
        R11G11B10_FP = OVRPlugin.EyeTextureFormat.R11G11B10_FP,
    }

    public enum FoveatedRenderingLevel
    {
        Off = OVRPlugin.FoveatedRenderingLevel.Off,
        Low = OVRPlugin.FoveatedRenderingLevel.Low,
        Medium = OVRPlugin.FoveatedRenderingLevel.Medium,
        High = OVRPlugin.FoveatedRenderingLevel.High,
        HighTop = OVRPlugin.FoveatedRenderingLevel.HighTop,
    }

    public enum SystemHeadsetType
    {
        None = OVRPlugin.SystemHeadset.None,

        // Standalone headsets
        Oculus_Quest = OVRPlugin.SystemHeadset.Oculus_Quest,
        Oculus_Quest_2 = OVRPlugin.SystemHeadset.Oculus_Quest_2,
        Meta_Quest_Pro = OVRPlugin.SystemHeadset.Meta_Quest_Pro,
        Meta_Quest_3 = OVRPlugin.SystemHeadset.Meta_Quest_3,
        Meta_Quest_3S = OVRPlugin.SystemHeadset.Meta_Quest_3S,
        Placeholder_13 = OVRPlugin.SystemHeadset.Placeholder_13,
        Placeholder_14 = OVRPlugin.SystemHeadset.Placeholder_14,
        Placeholder_15 = OVRPlugin.SystemHeadset.Placeholder_15,
        Placeholder_16 = OVRPlugin.SystemHeadset.Placeholder_16,
        Placeholder_17 = OVRPlugin.SystemHeadset.Placeholder_17,
        Placeholder_18 = OVRPlugin.SystemHeadset.Placeholder_18,
        Placeholder_19 = OVRPlugin.SystemHeadset.Placeholder_19,
        Placeholder_20 = OVRPlugin.SystemHeadset.Placeholder_20,

        // PC headsets
        Rift_DK1 = OVRPlugin.SystemHeadset.Rift_DK1,
        Rift_DK2 = OVRPlugin.SystemHeadset.Rift_DK2,
        Rift_CV1 = OVRPlugin.SystemHeadset.Rift_CV1,
        Rift_CB = OVRPlugin.SystemHeadset.Rift_CB,
        Rift_S = OVRPlugin.SystemHeadset.Rift_S,
        Oculus_Link_Quest = OVRPlugin.SystemHeadset.Oculus_Link_Quest,
        Oculus_Link_Quest_2 = OVRPlugin.SystemHeadset.Oculus_Link_Quest_2,
        Meta_Link_Quest_Pro = OVRPlugin.SystemHeadset.Meta_Link_Quest_Pro,
        Meta_Link_Quest_3 = OVRPlugin.SystemHeadset.Meta_Link_Quest_3,
        Meta_Link_Quest_3S = OVRPlugin.SystemHeadset.Meta_Link_Quest_3S,
        PC_Placeholder_4106 = OVRPlugin.SystemHeadset.PC_Placeholder_4106,
        PC_Placeholder_4107 = OVRPlugin.SystemHeadset.PC_Placeholder_4107,
        PC_Placeholder_4108 = OVRPlugin.SystemHeadset.PC_Placeholder_4108,
        PC_Placeholder_4109 = OVRPlugin.SystemHeadset.PC_Placeholder_4109,
        PC_Placeholder_4110 = OVRPlugin.SystemHeadset.PC_Placeholder_4110,
        PC_Placeholder_4111 = OVRPlugin.SystemHeadset.PC_Placeholder_4111,
        PC_Placeholder_4112 = OVRPlugin.SystemHeadset.PC_Placeholder_4112,
        PC_Placeholder_4113 = OVRPlugin.SystemHeadset.PC_Placeholder_4113,
    }

    public enum SystemHeadsetTheme
    {
        Dark,
        Light
    }

    public enum XRDevice
    {
        Unknown = 0,
        Oculus = 1,
        OpenVR = 2,
    }

    public enum ColorSpace
    {
        Unknown = OVRPlugin.ColorSpace.Unknown,
        Unmanaged = OVRPlugin.ColorSpace.Unmanaged,
        Rec_2020 = OVRPlugin.ColorSpace.Rec_2020,
        Rec_709 = OVRPlugin.ColorSpace.Rec_709,
        Rift_CV1 = OVRPlugin.ColorSpace.Rift_CV1,
        Rift_S = OVRPlugin.ColorSpace.Rift_S,

        [InspectorName("Quest 1")]
        Quest = OVRPlugin.ColorSpace.Quest,

        [InspectorName("DCI-P3 (Recommended)")]
        P3 = OVRPlugin.ColorSpace.P3,
        Adobe_RGB = OVRPlugin.ColorSpace.Adobe_RGB,
    }

    public enum ProcessorPerformanceLevel
    {
        PowerSavings = OVRPlugin.ProcessorPerformanceLevel.PowerSavings,
        SustainedLow = OVRPlugin.ProcessorPerformanceLevel.SustainedLow,
        SustainedHigh = OVRPlugin.ProcessorPerformanceLevel.SustainedHigh,
        Boost = OVRPlugin.ProcessorPerformanceLevel.Boost,
    }


    public enum ControllerDrivenHandPosesType
    {
        None,
        ConformingToController,
        Natural,
    }


    public interface EventListener
    {
        void OnEvent(OVRPlugin.EventDataBuffer eventData);
    }

    public static OVRSimple instance { get; private set; }

    public static OVRDisplay display { get; private set; }

    public static OVRTracker tracker { get; private set; }

    public static OVRBoundary boundary { get; private set; }

    public static OVRRuntimeSettings runtimeSettings { get; private set; }

    protected static OVRProfile _profile;

    public static OVRProfile profile
    {
        get
        {
            if (_profile == null)
                _profile = new OVRProfile();

            return _profile;
        }
    }

    protected IEnumerable<Camera> disabledCameras;

    public static event Action HMDAcquired;

    public static event Action HMDLost;

    public static event Action HMDMounted;

    public static event Action HMDUnmounted;

    public static event Action VrFocusAcquired;

    public static event Action VrFocusLost;

    public static event Action InputFocusAcquired;

    public static event Action InputFocusLost;

    public static event Action AudioOutChanged;

    public static event Action AudioInChanged;

    public static event Action TrackingAcquired;

    public static event Action TrackingLost;

    public static event Action<float, float> DisplayRefreshRateChanged;

    public static event Action<UInt64, bool, OVRSpace, Guid> SpatialAnchorCreateComplete;

    public static event Action<UInt64, bool, OVRSpace, Guid, OVRPlugin.SpaceComponentType, bool>
        SpaceSetComponentStatusComplete;

    public static event Action<UInt64> SpaceQueryResults;

    public static event Action<UInt64, bool> SpaceQueryComplete;

    public static event Action<UInt64, OVRSpace, bool, Guid> SpaceSaveComplete;

    public static event Action<UInt64, bool, Guid, OVRPlugin.SpaceStorageLocation> SpaceEraseComplete;

    public static event Action<UInt64, OVRSpatialAnchor.OperationResult> ShareSpacesComplete;

    public static event Action<UInt64, OVRSpatialAnchor.OperationResult> SpaceListSaveComplete;

    public static event Action<UInt64, bool> SceneCaptureComplete;

    public static event Action<int> PassthroughLayerResumed;

    public static event Action<OVRPlugin.BoundaryVisibility> BoundaryVisibilityChanged;
  
#pragma warning restore

    private static int _isHmdPresentCacheFrame = -1;
    private static bool _isHmdPresent = false;
    private static bool _wasHmdPresent = false;

    public static bool isHmdPresent
    {
        get
        {
            // Caching to ensure that IsHmdPresent() is called only once per frame
            if (_isHmdPresentCacheFrame != Time.frameCount)
            {
                _isHmdPresentCacheFrame = Time.frameCount;
                _isHmdPresent = OVRNodeStateProperties.IsHmdPresent();
            }
            return _isHmdPresent;
        }
    }

    public static string audioOutId
    {
        get { return OVRPlugin.audioOutId; }
    }

    public static string audioInId
    {
        get { return OVRPlugin.audioInId; }
    }

    private static bool _hasVrFocusCached = false;
    private static bool _hasVrFocus = false;
    private static bool _hadVrFocus = false;

    public static bool hasVrFocus
    {
        get
        {
            if (!_hasVrFocusCached)
            {
                _hasVrFocusCached = true;
                _hasVrFocus = OVRPlugin.hasVrFocus;
            }

            return _hasVrFocus;
        }

        private set
        {
            _hasVrFocusCached = true;
            _hasVrFocus = value;
        }
    }

    private static bool _hadInputFocus = true;

    public static bool hasInputFocus
    {
        get { return OVRPlugin.hasInputFocus; }
    }


    public bool chromatic
    {
        get
        {
            if (!isHmdPresent)
                return false;

            return OVRPlugin.chromatic;
        }

        set
        {
            if (!isHmdPresent)
                return;

            OVRPlugin.chromatic = value;
        }
    }

    [Header("Performance/Quality")]

    [Tooltip("If true, Unity will use the optimal antialiasing level for quality/performance on the current hardware.")]
    public bool useRecommendedMSAALevel = true;


    [SerializeField]
    [Tooltip("If true, both eyes will see the same image, rendered from the center eye pose, saving performance.")]
    private bool _monoscopic = false;

    public bool monoscopic
    {
        get
        {
            if (!isHmdPresent)
                return _monoscopic;

            return OVRPlugin.monoscopic;
        }

        set
        {
            if (!isHmdPresent)
                return;

            OVRPlugin.monoscopic = value;
            _monoscopic = value;
        }
    }

    [SerializeField]
    [Tooltip("The sharpen filter of the eye buffer. This amplifies contrast and fine details.")]
    private OVRPlugin.LayerSharpenType _sharpenType = OVRPlugin.LayerSharpenType.None;

    public OVRPlugin.LayerSharpenType sharpenType
    {
        get { return _sharpenType; }
        set
        {
            _sharpenType = value;
            OVRPlugin.SetEyeBufferSharpenType(_sharpenType);
        }
    }

    [HideInInspector]
    private OVRSimple.ColorSpace _colorGamut = OVRSimple.ColorSpace.P3;

    public OVRSimple.ColorSpace colorGamut
    {
        get { return _colorGamut; }
        set
        {
            _colorGamut = value;
            OVRPlugin.SetClientColorDesc((OVRPlugin.ColorSpace)_colorGamut);
        }
    }

    public OVRSimple.ColorSpace nativeColorGamut
    {
        get { return (OVRSimple.ColorSpace)OVRPlugin.GetHmdColorDesc(); }
    }

    [SerializeField]
    [HideInInspector]
    [Tooltip("Enable Dynamic Resolution. This will allocate render buffers to maxDynamicResolutionScale size and " +
             "will change the viewport to adapt performance. Mobile only.")]
    private bool _enableDynamicResolution = false;
    public bool enableDynamicResolution
    {
        get { return _enableDynamicResolution; }
        set
        {
            _enableDynamicResolution = value;

#if USING_XR_SDK_OPENXR && UNITY_ANDROID
            OVRPlugin.SetExternalLayerDynresEnabled(value ? OVRPlugin.Bool.True : OVRPlugin.Bool.False);
#endif
        }
    }

    [HideInInspector]
    public float minDynamicResolutionScale = 1.0f;
    [HideInInspector]
    public float maxDynamicResolutionScale = 1.0f;

    [SerializeField]
    [HideInInspector]
    public float quest2MinDynamicResolutionScale = 0.7f;

    [SerializeField]
    [HideInInspector]
    public float quest2MaxDynamicResolutionScale = 1.3f;

    [SerializeField]
    [HideInInspector]
    public float quest3MinDynamicResolutionScale = 0.7f;

    [SerializeField]
    [HideInInspector]
    public float quest3MaxDynamicResolutionScale = 1.6f;

    private const int _pixelStepPerFrame = 32;
   
    [SerializeField]
    [Tooltip("Set the relative offset rotation of head poses")]
    private Vector3 _headPoseRelativeOffsetRotation;

    public Vector3 headPoseRelativeOffsetRotation
    {
        get { return _headPoseRelativeOffsetRotation; }
        set
        {
            OVRPlugin.Quatf rotation;
            OVRPlugin.Vector3f translation;
            if (OVRPlugin.GetHeadPoseModifier(out rotation, out translation))
            {
                Quaternion finalRotation = Quaternion.Euler(value);
                rotation = finalRotation.ToQuatf();
                OVRPlugin.SetHeadPoseModifier(ref rotation, ref translation);
            }

            _headPoseRelativeOffsetRotation = value;
        }
    }

    [SerializeField]
    [Tooltip("Set the relative offset translation of head poses")]
    private Vector3 _headPoseRelativeOffsetTranslation;

    public Vector3 headPoseRelativeOffsetTranslation
    {
        get { return _headPoseRelativeOffsetTranslation; }
        set
        {
            OVRPlugin.Quatf rotation;
            OVRPlugin.Vector3f translation;
            if (OVRPlugin.GetHeadPoseModifier(out rotation, out translation))
            {
                if (translation.FromFlippedZVector3f() != value)
                {
                    translation = value.ToFlippedZVector3f();
                    OVRPlugin.SetHeadPoseModifier(ref rotation, ref translation);
                }
            }

            _headPoseRelativeOffsetTranslation = value;
        }
    }

    public int profilerTcpPort = OVRSystemPerfMetrics.TcpListeningPort;

    [HideInInspector]
    public static bool eyeFovPremultipliedAlphaModeEnabled
    {
        get { return OVRPlugin.eyeFovPremultipliedAlphaModeEnabled; }
        set { OVRPlugin.eyeFovPremultipliedAlphaModeEnabled = value; }
    }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_ANDROID

    public enum CompositionMethod
    {
        External,

        [System.Obsolete("Deprecated. Direct composition is no longer supported", false)]
        Direct
    }

    [HideInInspector]
    public CompositionMethod compositionMethod = CompositionMethod.External;

    [HideInInspector, Tooltip("Extra hidden layers")]
    public LayerMask extraHiddenLayers;

    [HideInInspector, Tooltip("Extra visible layers")]
    public LayerMask extraVisibleLayers;


    [HideInInspector, Tooltip("Dynamic Culling Mask")]
    public bool dynamicCullingMask = true;

    [HideInInspector, Tooltip("Backdrop color for Rift (External Compositon)")]
    public Color externalCompositionBackdropColorRift = Color.green;

    [HideInInspector, Tooltip("Backdrop color for Quest (External Compositon)")]
    public Color externalCompositionBackdropColorQuest = Color.clear;

    public enum MrcActivationMode
    {
        Automatic,
        Disabled
    }

    [HideInInspector, Tooltip("(Quest-only) control if the mixed reality capture mode can be activated automatically " +
                              "through remote network connection.")]
    public MrcActivationMode mrcActivationMode;

    public enum MrcCameraType
    {
        Normal,
        Foreground,
        Background
    }

    public delegate GameObject InstantiateMrcCameraDelegate(GameObject mainCameraGameObject, MrcCameraType cameraType);

  
#endif

    [HideInInspector, Tooltip("Specify if simultaneous hands and controllers should be enabled. ")]
    public bool launchSimultaneousHandsControllersOnStartup = false;

    [HideInInspector, Tooltip("Specify if Insight Passthrough should be enabled. " +
                              "Passthrough layers can only be used if passthrough is enabled.")]
    public bool isInsightPassthroughEnabled = false;

    [HideInInspector] public bool shouldBoundaryVisibilityBeSuppressed = false;

    public bool isBoundaryVisibilitySuppressed { get; private set; } = false;

    // boundary logging helper to avoid spamming
    private bool _updateBoundaryLogOnce = false;

    #region Permissions

    [SerializeField, HideInInspector]
    internal bool requestBodyTrackingPermissionOnStartup;

    [SerializeField, HideInInspector]
    internal bool requestFaceTrackingPermissionOnStartup;

    [SerializeField, HideInInspector]
    internal bool requestEyeTrackingPermissionOnStartup;

    [SerializeField, HideInInspector]
    internal bool requestScenePermissionOnStartup;

    [SerializeField, HideInInspector]
    internal bool requestRecordAudioPermissionOnStartup;
    #endregion
    public XrApi xrApi
    {
        get { return (XrApi)OVRPlugin.nativeXrApi; }
    }

    public UInt64 xrInstance
    {
        get { return OVRPlugin.GetNativeOpenXRInstance(); }
    }

    public UInt64 xrSession
    {
        get { return OVRPlugin.GetNativeOpenXRSession(); }
    }

    public int vsyncCount
    {
        get
        {
            if (!isHmdPresent)
                return 1;

            return OVRPlugin.vsyncCount;
        }

        set
        {
            if (!isHmdPresent)
                return;

            OVRPlugin.vsyncCount = value;
        }
    }

    public static string OCULUS_UNITY_NAME_STR = "Oculus";
    public static string OPENVR_UNITY_NAME_STR = "OpenVR";

    public static XRDevice loadedXRDevice;

    public static ProcessorPerformanceLevel suggestedCpuPerfLevel
    {
        get
        {
            if (!isHmdPresent)
                return ProcessorPerformanceLevel.PowerSavings;

            return (ProcessorPerformanceLevel)OVRPlugin.suggestedCpuPerfLevel;
        }

        set
        {
            if (!isHmdPresent)
                return;

            OVRPlugin.suggestedCpuPerfLevel = (OVRPlugin.ProcessorPerformanceLevel)value;
        }
    }

    public static ProcessorPerformanceLevel suggestedGpuPerfLevel
    {
        get
        {
            if (!isHmdPresent)
                return ProcessorPerformanceLevel.PowerSavings;

            return (ProcessorPerformanceLevel)OVRPlugin.suggestedGpuPerfLevel;
        }

        set
        {
            if (!isHmdPresent)
                return;

            OVRPlugin.suggestedGpuPerfLevel = (OVRPlugin.ProcessorPerformanceLevel)value;
        }
    }

    public static bool isPowerSavingActive
    {
        get
        {
            if (!isHmdPresent)
                return false;

            return OVRPlugin.powerSaving;
        }
    }

    public static EyeTextureFormat eyeTextureFormat
    {
        get { return (EyeTextureFormat)(OVRSimple.EyeTextureFormat)OVRPlugin.GetDesiredEyeTextureFormat(); }

        set { OVRPlugin.SetDesiredEyeTextureFormat((OVRPlugin.EyeTextureFormat)value); }
    }

    protected static void OnPermissionGranted(string permissionId)
    {
        if (permissionId == OVRPermissionsRequester.GetPermissionId(OVRPermissionsRequester.Permission.EyeTracking))
        {
            OVRPermissionsRequester.PermissionGranted -= OnPermissionGranted;
        }
    }

    public static bool gpuUtilSupported
    {
        get { return OVRPlugin.gpuUtilSupported; }
    }

    public static float gpuUtilLevel
    {
        get
        {
            return OVRPlugin.gpuUtilLevel;
        }
    }

    public static SystemHeadsetType systemHeadsetType
    {
        get { return (SystemHeadsetType)OVRPlugin.GetSystemHeadsetType(); }
    }

    public static SystemHeadsetTheme systemHeadsetTheme
    {
        get { return GetSystemHeadsetTheme(); }
    }

    private static bool _isSystemHeadsetThemeCached = false;
    private static SystemHeadsetTheme _cachedSystemHeadsetTheme = SystemHeadsetTheme.Dark;

    static private SystemHeadsetTheme GetSystemHeadsetTheme()
    {
        if (!_isSystemHeadsetThemeCached)
        {
#if UNITY_ANDROID
            const int UI_MODE_NIGHT_MASK = 0x30;
            const int UI_MODE_NIGHT_NO = 0x10;
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject currentResources = currentActivity.Call<AndroidJavaObject>("getResources");
            AndroidJavaObject currentConfiguration = currentResources.Call<AndroidJavaObject>("getConfiguration");
            int uiMode = currentConfiguration.Get<int>("uiMode");
            int currentUIMode = uiMode & UI_MODE_NIGHT_MASK;
            _cachedSystemHeadsetTheme = currentUIMode == UI_MODE_NIGHT_NO ? SystemHeadsetTheme.Light : SystemHeadsetTheme.Dark;
#endif // UNITY_ANDROID
            _isSystemHeadsetThemeCached = true;
        }
        return _cachedSystemHeadsetTheme;
    }

    public static void SetOpenVRLocalPose(Vector3 leftPos, Vector3 rightPos, Quaternion leftRot, Quaternion rightRot)
    {
        if (loadedXRDevice == XRDevice.OpenVR)
            OVRInput.SetOpenVRLocalPose(leftPos, rightPos, leftRot, rightRot);
    }

    //Series of offsets that line up the virtual controllers to the phsyical world.
    protected static Vector3 OpenVRTouchRotationOffsetEulerLeft = new Vector3(40.0f, 0.0f, 0.0f);
    protected static Vector3 OpenVRTouchRotationOffsetEulerRight = new Vector3(40.0f, 0.0f, 0.0f);
    protected static Vector3 OpenVRTouchPositionOffsetLeft = new Vector3(0.0075f, -0.005f, -0.0525f);
    protected static Vector3 OpenVRTouchPositionOffsetRight = new Vector3(-0.0075f, -0.005f, -0.0525f);

    public static OVRPose GetOpenVRControllerOffset(Node hand)
    {
        OVRPose poseOffset = OVRPose.identity;
        if ((hand == Node.LeftHand || hand == Node.RightHand) && loadedXRDevice == XRDevice.OpenVR)
        {
            int index = (hand == Node.LeftHand) ? 0 : 1;
            if (OVRInput.openVRControllerDetails[index].controllerType == OVRInput.OpenVRController.OculusTouch)
            {
                Vector3 offsetOrientation = (hand == Node.LeftHand)
                    ? OpenVRTouchRotationOffsetEulerLeft
                    : OpenVRTouchRotationOffsetEulerRight;
                poseOffset.orientation =
                    Quaternion.Euler(offsetOrientation.x, offsetOrientation.y, offsetOrientation.z);
                poseOffset.position = (hand == Node.LeftHand)
                    ? OpenVRTouchPositionOffsetLeft
                    : OpenVRTouchPositionOffsetRight;
            }
        }

        return poseOffset;
    }

    public static void SetSpaceWarp(bool enabled)
    {
        Camera mainCamera = FindMainCamera();
        if (enabled)
        {
            if (mainCamera != null)
            {
                PrepareCameraForSpaceWarp(mainCamera);
                m_lastSpaceWarpCamera = new WeakReference<Camera>(mainCamera);
            }
        }
        else
        {
            Camera lastSpaceWarpCamera;
            if (mainCamera != null && m_lastSpaceWarpCamera != null && m_lastSpaceWarpCamera.TryGetTarget(out lastSpaceWarpCamera) && lastSpaceWarpCamera == mainCamera)
            {
                // Restore the depth texture mode only if we're disabling space warp on the same camera we enabled it on.
                mainCamera.depthTextureMode = m_CachedDepthTextureMode;
            }

            m_AppSpaceTransform = null;
            m_lastSpaceWarpCamera = null;
        }

       /* SetSpaceWarp_Internal(enabled);*/
        m_SpaceWarpEnabled = enabled;
    }

    private static void PrepareCameraForSpaceWarp(Camera camera)
    {
        m_CachedDepthTextureMode = camera.depthTextureMode;
        camera.depthTextureMode |= (DepthTextureMode.MotionVectors | DepthTextureMode.Depth);
        m_AppSpaceTransform = camera.transform.parent;
    }

    protected static WeakReference<Camera> m_lastSpaceWarpCamera;
    protected static bool m_SpaceWarpEnabled;
    protected static Transform m_AppSpaceTransform;
    protected static DepthTextureMode m_CachedDepthTextureMode;

    public static bool GetSpaceWarp()
    {
        return m_SpaceWarpEnabled;
    }

#if OCULUS_XR_3_3_0_OR_NEWER
    public static bool SetDepthSubmission(bool enable)
    {
#if USING_XR_SDK_OCULUS
        OculusXRPlugin.SetDepthSubmission(enable);
        return true;
#else
        return false;
#endif
    }
#endif

    [SerializeField]
    [Tooltip("Available only for devices that support local dimming. It improves visual quality with " +
             "a better display contrast ratio, but at a minor GPU performance cost.")]
    private bool _localDimming = true;

    [Header("Tracking")]
    [SerializeField]
    [Tooltip("Defines the current tracking origin type.")]
    private OVRSimple.TrackingOrigin _trackingOriginType = OVRSimple.TrackingOrigin.FloorLevel;

    public OVRSimple.TrackingOrigin trackingOriginType
    {
        get
        {
            if (!isHmdPresent)
                return _trackingOriginType;

            return (OVRSimple.TrackingOrigin)OVRPlugin.GetTrackingOriginType();
        }

        set
        {
            if (!isHmdPresent)
            {
                _trackingOriginType = value;
                return;
            }

            OVRPlugin.TrackingOrigin newOrigin = (OVRPlugin.TrackingOrigin)value;

#if USING_XR_SDK_OPENXR
            if (OVRPlugin.UnityOpenXR.Enabled)
            {
                if (GetCurrentInputSubsystem() == null)
                {
                    return;
                }

                TrackingOriginModeFlags mode = TrackingOriginModeFlags.Unknown;
                if (newOrigin == OVRPlugin.TrackingOrigin.EyeLevel)
                {
                    mode = TrackingOriginModeFlags.Device;
                }
#if UNITY_OPENXR_1_9_0
                else if (newOrigin == OVRPlugin.TrackingOrigin.FloorLevel)
                {
                    // Unity OpenXR Plugin defines Floor as Floor with Recentering on
                    mode = TrackingOriginModeFlags.Floor;
                    OpenXRSettings.SetAllowRecentering(true);
                }
                else if (newOrigin == OVRPlugin.TrackingOrigin.Stage)
                {
                    // Unity OpenXR Plugin defines Stage as Floor with Recentering off
                    mode = TrackingOriginModeFlags.Floor;
                    OpenXRSettings.SetAllowRecentering(false);
                }
#else
                else if (newOrigin == OVRPlugin.TrackingOrigin.FloorLevel || newOrigin == OVRPlugin.TrackingOrigin.Stage)
                {
                    mode = TrackingOriginModeFlags.Floor; // Stage in OpenXR
                }
#endif

                // if the tracking origin mode is unsupported in OpenXR, we set the origin via OVRPlugin
                if (mode != TrackingOriginModeFlags.Unknown)
                {
                    bool success = GetCurrentInputSubsystem().TrySetTrackingOriginMode(mode);
                    if (!success)
                    {
                    }
                    else
                    {
                        _trackingOriginType = value;
#if UNITY_OPENXR_1_9_0
                        OpenXRSettings.RefreshRecenterSpace();
#endif
                    }
                    return;
                }
            }
#endif

            if (OVRPlugin.SetTrackingOriginType(newOrigin))
            {
                // Keep the field exposed in the Unity Editor synchronized with any changes.
                _trackingOriginType = value;
            }
        }
    }

    [Tooltip("If true, head tracking will affect the position of each OVRCameraRig's cameras.")]
    public bool usePositionTracking = true;

    [HideInInspector]
    public bool useRotationTracking = true;

    [Tooltip("If true, the distance between the user's eyes will affect the position of each OVRCameraRig's cameras.")]
    public bool useIPDInPositionTracking = true;

    [Tooltip("If true, each scene load will cause the head pose to reset. This function only works on Rift.")]
    public bool resetTrackerOnLoad = false;

    [Tooltip("If true, the Reset View in the universal menu will cause the pose to be reset in PC VR. This should " +
             "generally be enabled for applications with a stationary position in the virtual world and will allow " +
             "the View Reset command to place the person back to a predefined location (such as a cockpit seat). " +
             "Set this to false if you have a locomotion system because resetting the view would effectively teleport " +
             "the player to potentially invalid locations.")]
    public bool AllowRecenter = true;


    [Tooltip("If true, rendered controller latency is reduced by several ms, as the left/right controllers will " +
             "have their positions updated right before rendering.")]
    public bool LateControllerUpdate = true;

#if UNITY_2020_3_OR_NEWER
    [Tooltip("Late latching is a feature that can reduce rendered head/controller latency by a substantial amount. " +
             "Before enabling, be sure to go over the documentation to ensure that the feature is used correctly. " +
             "This feature must also be enabled through the Oculus XR Plugin settings.")]
    public bool LateLatching = false;
#endif

    private static OVRSimple.ControllerDrivenHandPosesType _readOnlyControllerDrivenHandPosesType = OVRSimple.ControllerDrivenHandPosesType.None;
    [Tooltip("Defines if hand poses can be populated by controller data.")]
    public OVRSimple.ControllerDrivenHandPosesType controllerDrivenHandPosesType = OVRSimple.ControllerDrivenHandPosesType.None;

    [Tooltip("Allows the application to use simultaneous hands and controllers functionality. This option must be enabled at build time.")]
    public bool SimultaneousHandsAndControllersEnabled = false;

    [SerializeField]
    [HideInInspector]
    private bool _readOnlyWideMotionModeHandPosesEnabled = false;
    [Tooltip("Defines if hand poses can leverage algorithms to retrieve hand poses outside of the normal tracking area.")]
    public bool wideMotionModeHandPosesEnabled = false;

    public bool IsSimultaneousHandsAndControllersSupported
    {
        get => (_readOnlyControllerDrivenHandPosesType != OVRSimple.ControllerDrivenHandPosesType.None) || launchSimultaneousHandsControllersOnStartup;
    }
    public bool isSupportedPlatform { get; private set; }

    private static bool _isUserPresentCached = false;
    private static bool _isUserPresent = false;
    private static bool _wasUserPresent = false;

    public bool isUserPresent
    {
        get
        {
            if (!_isUserPresentCached)
            {
                _isUserPresentCached = true;
                _isUserPresent = OVRPlugin.userPresent;
            }

            return _isUserPresent;
        }

        private set
        {
            _isUserPresentCached = true;
            _isUserPresent = value;
        }
    }

    private static bool prevAudioOutIdIsCached = false;
    private static bool prevAudioInIdIsCached = false;
    private static string prevAudioOutId = string.Empty;
    private static string prevAudioInId = string.Empty;
    private static bool wasPositionTracked = false;

    private static OVRPlugin.EventDataBuffer eventDataBuffer = new OVRPlugin.EventDataBuffer();

    private HashSet<EventListener> eventListeners = new HashSet<EventListener>();

    public void RegisterEventListener(EventListener listener)
    {
        eventListeners.Add(listener);
    }

    public void DeregisterEventListener(EventListener listener)
    {
        eventListeners.Remove(listener);
    }

    public static System.Version utilitiesVersion
    {
        get { return OVRPlugin.wrapperVersion; }
    }

    public static System.Version pluginVersion
    {
        get { return OVRPlugin.version; }
    }

    public static System.Version sdkVersion
    {
        get { return OVRPlugin.nativeSDKVersion; }
    }
    
    public static bool IsUnityAlphaOrBetaVersion()
    {
        string ver = Application.unityVersion;
        int pos = ver.Length - 1;

        while (pos >= 0 && ver[pos] >= '0' && ver[pos] <= '9')
        {
            --pos;
        }

        if (pos >= 0 && (ver[pos] == 'a' || ver[pos] == 'b'))
            return true;

        return false;
    }

    public static string UnityAlphaOrBetaVersionWarningMessage =
        "WARNING: It's not recommended to use Unity alpha/beta release in Oculus development. Use a stable release if you encounter any issue.";

    #region Unity Messages

#if UNITY_EDITOR
    [AOT.MonoPInvokeCallback(typeof(OVRPlugin.LogCallback2DelegateType))]
    static void OVRPluginLogCallback(OVRPlugin.LogLevel logLevel, IntPtr message, int size)
    {
        string logString = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, size);
    }
#endif

    public static int MaxDynamicResolutionVersion = 1;
    [SerializeField]
    [HideInInspector]
    public int dynamicResolutionVersion = 0;

    private void Reset()
    {
        dynamicResolutionVersion = MaxDynamicResolutionVersion;
    }

    public static bool OVRSimpleinitialized = false;

    private void InitOVRSimple()
    {
       /* using var marker = new OVRTelemetryMarker(OVRTelemetryConstants.OVRSimple.MarkerId.Init);
        marker.AddSDKVersionAnnotation();*/

        // Only allow one instance at runtime.
        if (instance != null)
        {
            enabled = false;
            DestroyImmediate(this);

         /*   marker.SetResult(OVRPlugin.Qpl.ResultType.Fail);*/
            return;
        }

        instance = this;

        runtimeSettings = OVRRuntimeSettings.GetRuntimeSettings();

        // uncomment the following line to disable the callstack printed to log
        //Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);  // TEMPORARY

        string versionMessage = "Unity v" + Application.unityVersion + ", " +
                  "Oculus Utilities v" + OVRPlugin.wrapperVersion + ", " +
                  "OVRPlugin v" + OVRPlugin.version + ", " +
                  "SDK v" + OVRPlugin.nativeSDKVersion + ".";

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        var supportedTypes =
            UnityEngine.Rendering.GraphicsDeviceType.Direct3D11.ToString() + ", " +
            UnityEngine.Rendering.GraphicsDeviceType.Direct3D12.ToString();
#endif

        // Detect whether this platform is a supported platform
        RuntimePlatform currPlatform = Application.platform;
        if (currPlatform == RuntimePlatform.Android ||
            // currPlatform == RuntimePlatform.LinuxPlayer ||
            currPlatform == RuntimePlatform.OSXEditor ||
            currPlatform == RuntimePlatform.OSXPlayer ||
            currPlatform == RuntimePlatform.WindowsEditor ||
            currPlatform == RuntimePlatform.WindowsPlayer)
        {
            isSupportedPlatform = true;
        }
        else
        {
            isSupportedPlatform = false;
        }
        /*
        if (!isSupportedPlatform)
        {
            marker.SetResult(OVRPlugin.Qpl.ResultType.Fail);
            return;
        }*/

#if UNITY_EDITOR
        OVRPlugin.SetLogCallback2(OVRPluginLogCallback);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        // Turn off chromatic aberration by default to save texture bandwidth.
        chromatic = false;
#endif

        Initialize();
        InitPermissionRequest();
        /*
        marker.AddPoint(OVRTelemetryConstants.OVRSimple.InitPermissionRequest);

            display.displayFrequency,
            string.Join(", ", display.displayFrequenciesAvailable.Select(f => f.ToString()).ToArray()));
*/
        if (resetTrackerOnLoad)
            display.RecenterPose();

        // Refresh the client color space
    /*    OVRSimple.ColorSpace clientColorSpace = runtimeSettings.colorSpace;
        colorGamut = clientColorSpace;*/

        // Set the eyebuffer sharpen type at the start
        OVRPlugin.SetEyeBufferSharpenType(_sharpenType);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Force OcculusionMesh on all the time, you can change the value to false if you really need it be off for some reasons,
        // be aware there are performance drops if you don't use occlusionMesh.
        OVRPlugin.occlusionMesh = true;
#endif

        if (isInsightPassthroughEnabled)
        {
            InitializeInsightPassthrough();

        /*    marker.AddPoint(OVRTelemetryConstants.OVRSimple.InitializeInsightPassthrough);*/
        }

        // Apply validation criteria to _localDimming toggle to ensure it isn't active on invalid systems
        if (!OVRPlugin.localDimmingSupported)
        {
            _localDimming = false;
        }
        else
        {
            OVRPlugin.localDimming = _localDimming;
        }

        UpdateDynamicResolutionVersion();

        switch (systemHeadsetType)
        {
            case SystemHeadsetType.Oculus_Quest_2:
            case SystemHeadsetType.Meta_Quest_Pro:
                minDynamicResolutionScale = quest2MinDynamicResolutionScale;
                maxDynamicResolutionScale = quest2MaxDynamicResolutionScale;
                break;
            default:
                minDynamicResolutionScale = quest3MinDynamicResolutionScale;
                maxDynamicResolutionScale = quest3MaxDynamicResolutionScale;
                break;
        }

#if USING_XR_SDK && UNITY_ANDROID
// Dynamic resolution in the Unity OpenXR plugin is only supported on package versions 3.4.1 on Unity 2021 and 4.3.1 on Unity 2022 and up.
#if (USING_XR_SDK_OCULUS || (USING_XR_SDK_OPENXR && UNITY_Y_FLIP_FIX))
        if (enableDynamicResolution)
        {
#if USING_XR_SDK_OPENXR
            OVRPlugin.SetExternalLayerDynresEnabled(enableDynamicResolution ? OVRPlugin.Bool.True : OVRPlugin.Bool.False);
#endif

            XRSettings.eyeTextureResolutionScale = maxDynamicResolutionScale;
#if USING_URP
            if (GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urpPipelineAsset)
                urpPipelineAsset.renderScale = maxDynamicResolutionScale;
#endif
        }
#endif
#endif

        InitializeBoundary();

        if (OVRPlugin.HandSkeletonVersion != runtimeSettings.HandSkeletonVersion)
        {
            OVRPlugin.SetHandSkeletonVersion(runtimeSettings.HandSkeletonVersion);
        }

#if UNITY_OPENXR_PLUGIN_1_11_0_OR_NEWER
        var openXrSettings = OpenXRSettings.Instance;
        if (openXrSettings != null)
        {
            var subsampledFeature = openXrSettings.GetFeature<MetaXRSubsampledLayout>();
            var spaceWarpFeature = openXrSettings.GetFeature<MetaXRSpaceWarp>();

            bool subsampledOn = false;
            if (subsampledFeature != null)
                subsampledOn = subsampledFeature.enabled;

            bool spaceWarpOn = false;
            if (spaceWarpFeature != null)
                spaceWarpOn = spaceWarpFeature.enabled;

        }
#endif
#if OCULUS_XR_PLUGIN_4_3_0_OR_NEWER
        var oculusLoader = XRGeneralSettings.Instance.Manager.activeLoader as OculusLoader;
        if (oculusLoader != null)
        {
            var oculusSettings = oculusLoader.GetSettings();         
        }
#endif


        OVRSimpleinitialized = true;
    }

    private void InitPermissionRequest()
    {
        var permissions = new HashSet<OVRPermissionsRequester.Permission>();

        if (requestBodyTrackingPermissionOnStartup)
        {
            permissions.Add(OVRPermissionsRequester.Permission.BodyTracking);
        }

        if (requestFaceTrackingPermissionOnStartup)
        {
            permissions.Add(OVRPermissionsRequester.Permission.FaceTracking);
        }

        if (requestEyeTrackingPermissionOnStartup)
        {
            permissions.Add(OVRPermissionsRequester.Permission.EyeTracking);
        }

        if (requestScenePermissionOnStartup)
        {
            permissions.Add(OVRPermissionsRequester.Permission.Scene);
        }

        if (requestRecordAudioPermissionOnStartup)
        {
            permissions.Add(OVRPermissionsRequester.Permission.RecordAudio);
        }

        OVRPermissionsRequester.Request(permissions);
    }

    private void Awake()
    {
#if !USING_XR_SDK
        //For legacy, we should initialize OVRSimple in all cases.
        //For now, in XR SDK, only initialize if OVRPlugin is initialized.
        InitOVRSimple();
#else
        if (OVRPlugin.initialized)
            InitOVRSimple();
#endif
    }

#if UNITY_EDITOR
    private static bool _scriptsReloaded;

    [UnityEditor.Callbacks.DidReloadScripts]
    static void ScriptsReloaded()
    {
        _scriptsReloaded = true;
    }
#endif

    void SetCurrentXRDevice()
    {
#if USING_XR_SDK
        XRDisplaySubsystem currentDisplaySubsystem = GetCurrentDisplaySubsystem();
        XRDisplaySubsystemDescriptor currentDisplaySubsystemDescriptor = GetCurrentDisplaySubsystemDescriptor();
#endif
        if (OVRPlugin.initialized)
        {
            loadedXRDevice = XRDevice.Oculus;
        }
#if USING_XR_SDK
        else if (currentDisplaySubsystem != null && currentDisplaySubsystemDescriptor != null &&
                 currentDisplaySubsystem.running)
#else
        else if (Settings.enabled)
#endif
        {
#if USING_XR_SDK
            string loadedXRDeviceName = currentDisplaySubsystemDescriptor.id;
#else
            string loadedXRDeviceName = Settings.loadedDeviceName;
#endif
            if (loadedXRDeviceName == OPENVR_UNITY_NAME_STR)
                loadedXRDevice = XRDevice.OpenVR;
            else
                loadedXRDevice = XRDevice.Unknown;
        }
        else
        {
            loadedXRDevice = XRDevice.Unknown;
        }
    }

#if USING_XR_SDK
    static List<XRDisplaySubsystem> s_displaySubsystems;

    public static XRDisplaySubsystem GetCurrentDisplaySubsystem()
    {
        if (s_displaySubsystems == null)
            s_displaySubsystems = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(s_displaySubsystems);
        if (s_displaySubsystems.Count > 0)
            return s_displaySubsystems[0];
        return null;
    }

    static List<XRDisplaySubsystemDescriptor> s_displaySubsystemDescriptors;

    public static XRDisplaySubsystemDescriptor GetCurrentDisplaySubsystemDescriptor()
    {
        if (s_displaySubsystemDescriptors == null)
            s_displaySubsystemDescriptors = new List<XRDisplaySubsystemDescriptor>();
        SubsystemManager.GetSubsystemDescriptors(s_displaySubsystemDescriptors);
        if (s_displaySubsystemDescriptors.Count > 0)
            return s_displaySubsystemDescriptors[0];
        return null;
    }

    static List<XRInputSubsystem> s_inputSubsystems;
    public static XRInputSubsystem GetCurrentInputSubsystem()
    {
        if (s_inputSubsystems == null)
            s_inputSubsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(s_inputSubsystems);
        if (s_inputSubsystems.Count > 0)
            return s_inputSubsystems[0];
        return null;
    }
#endif

    void Initialize()
    {
        if (display == null)
            display = new OVRDisplay();
        if (tracker == null)
            tracker = new OVRTracker();
        if (boundary == null)
            boundary = new OVRBoundary();

        SetCurrentXRDevice();
    }

    private void Update()
    {
        //Only if we're using the XR SDK do we have to check if OVRSimple isn't yet initialized, and init it.
        //If we're on legacy, we know initialization occurred properly in Awake()
#if USING_XR_SDK
        if (!OVRSimpleinitialized)
        {
            XRDisplaySubsystem currentDisplaySubsystem = GetCurrentDisplaySubsystem();
            XRDisplaySubsystemDescriptor currentDisplaySubsystemDescriptor = GetCurrentDisplaySubsystemDescriptor();
            if (currentDisplaySubsystem == null || currentDisplaySubsystemDescriptor == null || !OVRPlugin.initialized)
                return;
            //If we're using the XR SDK and the display subsystem is present, and OVRPlugin is initialized, we can init OVRSimple
            InitOVRSimple();
        }
#endif

#if !USING_XR_SDK_OPENXR && (!OCULUS_XR_3_3_0_OR_NEWER || !UNITY_2021_1_OR_NEWER)
        if (enableDynamicResolution && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
            enableDynamicResolution = false;
        
#endif

#if USING_XR_SDK_OPENXR && !UNITY_Y_FLIP_FIX
        if (enableDynamicResolution)
            enableDynamicResolution = false;
        
#endif

#if UNITY_EDITOR
        if (_scriptsReloaded)
        {
            _scriptsReloaded = false;
            instance = this;
            Initialize();
        }
#endif

        SetCurrentXRDevice();

        if (OVRPlugin.shouldQuit)
        {
            ShutdownInsightPassthrough();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            // do an early return to avoid calling the rest of the Update() logic.
            return;
#else
            Application.Quit();
#endif
        }

#if USING_XR_SDK && UNITY_ANDROID
        if (enableDynamicResolution)
        {
            OVRPlugin.Sizei recommendedResolution;
            if (OVRPlugin.GetEyeLayerRecommendedResolution(out recommendedResolution))
            {
                OVRPlugin.Sizei currentScaledResolution = new OVRPlugin.Sizei {
                    w = (int)(XRSettings.eyeTextureWidth * XRSettings.renderViewportScale),
                    h = (int)(XRSettings.eyeTextureHeight * XRSettings.renderViewportScale)
                };

                // Don't scale up or down more than a certain number of pixels per frame to avoid submitting a viewport that has disabled tiles.
                recommendedResolution.w = Mathf.Clamp(recommendedResolution.w,
                    currentScaledResolution.w - _pixelStepPerFrame,
                    currentScaledResolution.w + _pixelStepPerFrame);
                recommendedResolution.h = Mathf.Clamp(recommendedResolution.h,
                    currentScaledResolution.h - _pixelStepPerFrame,
                    currentScaledResolution.h + _pixelStepPerFrame);

                OVRPlugin.Sizei minResolution = new OVRPlugin.Sizei {
                    w = (int)(XRSettings.eyeTextureWidth * minDynamicResolutionScale / maxDynamicResolutionScale),
                    h = (int)(XRSettings.eyeTextureHeight * minDynamicResolutionScale / maxDynamicResolutionScale)
                };

                int targetWidth = Mathf.Clamp(recommendedResolution.w, minResolution.w, XRSettings.eyeTextureWidth);
                int targetHeight = Mathf.Clamp(recommendedResolution.h, minResolution.h, XRSettings.eyeTextureHeight);

                float scalingFactorX = targetWidth / (float)Settings.eyeTextureWidth;
                float scalingFactorY = targetHeight / (float)Settings.eyeTextureHeight;

                // Scaling factor is a single floating point value.
                // Try to determine which scaling factor produces the recommended resolution.
                float scalingFactor;
                if ((int)(scalingFactorX * (float)Settings.eyeTextureHeight) == targetHeight) {
                    // scalingFactorX will produce the recommended resolution for both width and height.
                    scalingFactor = scalingFactorX;
                } else if ((int)(scalingFactorY * (float)Settings.eyeTextureWidth) == targetWidth) {
                    // scalingFactorY will produce the recommended resolution for both width and height.
                    scalingFactor = scalingFactorY;
                } else {
                    // otherwise, use the smaller of the two to make sure we don't exceed the the recommended
                    // resolution size.
                    scalingFactor = Mathf.Min(scalingFactorX, scalingFactorY);
                }

                XRSettings.renderViewportScale = scalingFactor;
                ScalableBufferManager.ResizeBuffers(scalingFactor, scalingFactor);
            }
        }
#endif

        if (AllowRecenter && OVRPlugin.shouldRecenter)
        {
            OVRSimple.display.RecenterPose();
        }

        if (trackingOriginType != _trackingOriginType)
            trackingOriginType = _trackingOriginType;

        tracker.isEnabled = usePositionTracking;

        OVRPlugin.rotation = useRotationTracking;

        OVRPlugin.useIPDInPositionTracking = useIPDInPositionTracking;

        // Dispatch HMD events.
        int currentMsaaLevel = 0;
#if USING_URP
        var renderPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (renderPipeline != null)    
            currentMsaaLevel = renderPipeline.msaaSampleCount;
        
        else
#endif
        {
            currentMsaaLevel = QualitySettings.antiAliasing;
        }

        if (useRecommendedMSAALevel && currentMsaaLevel != display.recommendedMSAALevel)
        {

#if USING_URP
            if (renderPipeline != null)
            {
                renderPipeline.msaaSampleCount = display.recommendedMSAALevel;
            }
            else
#endif
            {
                QualitySettings.antiAliasing = display.recommendedMSAALevel;
            }
        }

        if (monoscopic != _monoscopic)     
            monoscopic = _monoscopic;
        

        if (headPoseRelativeOffsetRotation != _headPoseRelativeOffsetRotation)
            headPoseRelativeOffsetRotation = _headPoseRelativeOffsetRotation;
        

        if (headPoseRelativeOffsetTranslation != _headPoseRelativeOffsetTranslation)
            headPoseRelativeOffsetTranslation = _headPoseRelativeOffsetTranslation;
        

        if (_wasHmdPresent && !isHmdPresent)
        {
            try
            {
                if (HMDLost != null)
                    HMDLost();
            }
            catch (Exception e) { }

        }

        if (!_wasHmdPresent && isHmdPresent)
        {
            try
            {
                if (HMDAcquired != null)
                    HMDAcquired();
            }
            catch (Exception e) { }

        }

        _wasHmdPresent = isHmdPresent;

        // Dispatch HMD mounted events.

        isUserPresent = OVRPlugin.userPresent;

        if (_wasUserPresent && !isUserPresent)
        {
            try
            {
                if (HMDUnmounted != null)
                    HMDUnmounted();
            }
            catch (Exception e) { }

        }

        if (!_wasUserPresent && isUserPresent)
        {
            try
            {
                if (HMDMounted != null)
                    HMDMounted();
            }
            catch (Exception e) { }

        }

        _wasUserPresent = isUserPresent;

        // Dispatch VR Focus events.

        hasVrFocus = OVRPlugin.hasVrFocus;

        if (_hadVrFocus && !hasVrFocus)
        {
            try
            {
                if (VrFocusLost != null)
                    VrFocusLost();
            }
            catch (Exception e) { }

        }

        if (!_hadVrFocus && hasVrFocus)
        {
            try
            {
                if (VrFocusAcquired != null)
                    VrFocusAcquired();
            }
            catch (Exception e) { }

        }

        _hadVrFocus = hasVrFocus;

        // Dispatch VR Input events.

        bool hasInputFocus = OVRPlugin.hasInputFocus;

        if (_hadInputFocus && !hasInputFocus)
        {
            try
            {
                if (InputFocusLost != null)
                    InputFocusLost();
            }
            catch (Exception e) { }

        }

        if (!_hadInputFocus && hasInputFocus)
        {
            try
            {
                if (InputFocusAcquired != null)
                    InputFocusAcquired();
            }            catch (Exception e) { }

        }

        _hadInputFocus = hasInputFocus;

        // Dispatch Audio Device events.

        string audioOutId = OVRPlugin.audioOutId;
        if (!prevAudioOutIdIsCached)
        {
            prevAudioOutId = audioOutId;
            prevAudioOutIdIsCached = true;
        }
        else if (audioOutId != prevAudioOutId)
        {
            try
            {
                if (AudioOutChanged != null)
                    AudioOutChanged();
            }
            catch (Exception e) { }

            prevAudioOutId = audioOutId;
        }

        string audioInId = OVRPlugin.audioInId;
        if (!prevAudioInIdIsCached)
        {
            prevAudioInId = audioInId;
            prevAudioInIdIsCached = true;
        }
        else if (audioInId != prevAudioInId)
        {
            try
            {
                if (AudioInChanged != null)
                    AudioInChanged();
            }
            catch (Exception e) { }

            prevAudioInId = audioInId;
        }

        // Dispatch tracking events.

        if (wasPositionTracked && !tracker.isPositionTracked)
        {
            try
            {
                if (TrackingLost != null)
                    TrackingLost();
            }
            catch (Exception e) { }
        }

        if (!wasPositionTracked && tracker.isPositionTracked)
        {
            try
            {
                if (TrackingAcquired != null)
                    TrackingAcquired();
            }
            catch (Exception e) { }

        }

        wasPositionTracked = tracker.isPositionTracked;

        display.Update();

#if UNITY_EDITOR
        if (Application.isBatchMode)
        {
            OVRPlugin.UpdateInBatchMode();
        }

        // disable head pose update when xrSession is invisible
        OVRPlugin.SetTrackingPoseEnabledForInvisibleSession(false);
#endif

        if (_readOnlyControllerDrivenHandPosesType != controllerDrivenHandPosesType)
        {
            _readOnlyControllerDrivenHandPosesType = controllerDrivenHandPosesType;
            switch (_readOnlyControllerDrivenHandPosesType)
            {
                case OVRSimple.ControllerDrivenHandPosesType.None:
                    OVRPlugin.SetControllerDrivenHandPoses(false);
                    OVRPlugin.SetControllerDrivenHandPosesAreNatural(false);
                    break;
                case OVRSimple.ControllerDrivenHandPosesType.ConformingToController:
                    OVRPlugin.SetControllerDrivenHandPoses(true);
                    OVRPlugin.SetControllerDrivenHandPosesAreNatural(false);
                    break;
                case OVRSimple.ControllerDrivenHandPosesType.Natural:
                    OVRPlugin.SetControllerDrivenHandPoses(true);
                    OVRPlugin.SetControllerDrivenHandPosesAreNatural(true);
                    break;
            }
        }

        if (_readOnlyWideMotionModeHandPosesEnabled != wideMotionModeHandPosesEnabled)
        {
            _readOnlyWideMotionModeHandPosesEnabled = wideMotionModeHandPosesEnabled;
            OVRPlugin.SetWideMotionModeHandPoses(_readOnlyWideMotionModeHandPosesEnabled);
        }


        OVRInput.Update();

        UpdateHMDEvents();//Revizaar

        UpdateInsightPassthrough(isInsightPassthroughEnabled);//Revizar
        UpdateBoundary();//Revizar

    }

    private void UpdateHMDEvents()
    {/*
        while (OVRPlugin.PollEvent(ref eventDataBuffer))
        {
            switch (eventDataBuffer.EventType)
            {
                case OVRPlugin.EventType.DisplayRefreshRateChanged:
                    if (DisplayRefreshRateChanged != null)
                    {
                        var data = OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.DisplayRefreshRateChangedData>(
                            eventDataBuffer.EventData);
                        DisplayRefreshRateChanged(data.FromRefreshRate, data.ToRefreshRate);
                    }

                    break;
                case OVRPlugin.EventType.SpatialAnchorCreateComplete:
                    {
                        var data =
                            OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpatialAnchorCreateCompleteData>(
                                eventDataBuffer.EventData);

                        OVRTask.SetResult(data.RequestId,
                            data.Result >= 0 ? new OVRAnchor(data.Space, data.Uuid) : OVRAnchor.Null);
                        SpatialAnchorCreateComplete?.Invoke(data.RequestId, data.Result >= 0, data.Space, data.Uuid);
                        break;
                    }
                case OVRPlugin.EventType.SpaceSetComponentStatusComplete:
                    {
                        var data = OVRSDeserialize
                            .ByteArrayToStructure<OVRSDeserialize.SpaceSetComponentStatusCompleteData>(eventDataBuffer
                                .EventData);
                        SpaceSetComponentStatusComplete?.Invoke(data.RequestId, data.Result >= 0, data.Space, data.Uuid,
                            data.ComponentType, data.Enabled != 0);

                        OVRTask.SetResult(data.RequestId, data.Result >= 0);
                        OVRAnchor.OnSpaceSetComponentStatusComplete(data);
                        break;
                    }
                case OVRPlugin.EventType.SpaceQueryResults:
                    if (SpaceQueryResults != null)
                    {
                        var data =
                            OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpaceQueryResultsData>(eventDataBuffer
                                .EventData);
                        SpaceQueryResults(data.RequestId);
                    }

                    break;
                case OVRPlugin.EventType.SpaceQueryComplete:
                    {
                        var data = OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpaceQueryCompleteData>(
                            eventDataBuffer.EventData);
                        SpaceQueryComplete?.Invoke(data.RequestId, data.Result >= 0);
                        OVRAnchor.OnSpaceQueryComplete(data);
                        break;
                    }
                case OVRPlugin.EventType.SpaceSaveComplete:
                    if (SpaceSaveComplete != null)
                    {
                        var data = OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpaceSaveCompleteData>(
                            eventDataBuffer.EventData);
                        SpaceSaveComplete(data.RequestId, data.Space, data.Result >= 0, data.Uuid);
                    }

                    break;
                case OVRPlugin.EventType.SpaceEraseComplete:
                    {
                        var data =
                            OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpaceEraseCompleteData>(eventDataBuffer
                                .EventData);

                        var result = data.Result >= 0;
                        OVRAnchor.OnSpaceEraseComplete(data);
                        SpaceEraseComplete?.Invoke(data.RequestId, result, data.Uuid, data.Location);
                        OVRTask.SetResult(data.RequestId, result);
                        break;
                    }
                case OVRPlugin.EventType.SpaceShareResult:
                    {
                        var data =
                            OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpaceShareResultData>(
                                eventDataBuffer.EventData);

                        OVRTask.SetResult(data.RequestId, OVRResult.From((OVRAnchor.ShareResult)data.Result));
                        ShareSpacesComplete?.Invoke(data.RequestId, (OVRSpatialAnchor.OperationResult)data.Result);
                        break;
                    }
                case OVRPlugin.EventType.SpaceListSaveResult:
                    {
                        var data =
                            OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpaceListSaveResultData>(
                                eventDataBuffer.EventData);

                        OVRAnchor.OnSpaceListSaveResult(data);
                        SpaceListSaveComplete?.Invoke(data.RequestId, (OVRSpatialAnchor.OperationResult)data.Result);
                        break;
                    }
                case OVRPlugin.EventType.SpaceShareToGroupsComplete:
                    {
                        var data = eventDataBuffer.MarshalEntireStructAs<OVRSDeserialize.ShareSpacesToGroupsCompleteData>();
                        OVRAnchor.OnShareAnchorsToGroupsComplete(data.RequestId, data.Result);
                        break;
                    }
                case OVRPlugin.EventType.SceneCaptureComplete:
                    {
                        var data =
                            OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SceneCaptureCompleteData>(eventDataBuffer
                                .EventData);
                        SceneCaptureComplete?.Invoke(data.RequestId, data.Result >= 0);
                        OVRTask.SetResult(data.RequestId, data.Result >= 0);
                    }

                    break;
                case OVRPlugin.EventType.ColocationSessionStartAdvertisementComplete:
                    {
                        var data = eventDataBuffer
                            .MarshalEntireStructAs<OVRSDeserialize.StartColocationSessionAdvertisementCompleteData>();
                        OVRColocationSession.OnColocationSessionStartAdvertisementComplete(data.RequestId, data.Result, data.AdvertisementUuid);
                        break;
                    }

                case OVRPlugin.EventType.ColocationSessionStopAdvertisementComplete:
                    {
                        var data = eventDataBuffer
                            .MarshalEntireStructAs<OVRSDeserialize.StopColocationSessionAdvertisementCompleteData>();
                        OVRColocationSession.OnColocationSessionStopAdvertisementComplete(data.RequestId, data.Result);
                        break;
                    }

                case OVRPlugin.EventType.ColocationSessionStartDiscoveryComplete:
                    {
                        var data =
                            eventDataBuffer.MarshalEntireStructAs<OVRSDeserialize.StartColocationSessionDiscoveryCompleteData>();
                        OVRColocationSession.OnColocationSessionStartDiscoveryComplete(data.RequestId, data.Result);
                        break;
                    }

                case OVRPlugin.EventType.ColocationSessionStopDiscoveryComplete:
                    {
                        var data =
                            eventDataBuffer.MarshalEntireStructAs<OVRSDeserialize.StopColocationSessionDiscoveryCompleteData>();
                        OVRColocationSession.OnColocationSessionStopDiscoveryComplete(
                            data.RequestId,
                            data.Result);
                        break;
                    }
                case OVRPlugin.EventType.ColocationSessionDiscoveryResult:
                    {
                        unsafe
                        {
                            var data = eventDataBuffer
                                .MarshalEntireStructAs<OVRSDeserialize.ColocationSessionDiscoveryResultData>();

                            OVRColocationSession.OnColocationSessionDiscoveryResult(
                                data.RequestId,
                                data.AdvertisementUuid,
                                data.AdvertisementMetadataCount,
                                data.AdvertisementMetadata);
                        }

                        break;
                    }
                case OVRPlugin.EventType.ColocationSessionAdvertisementComplete:
                    {
                        var data = eventDataBuffer
                            .MarshalEntireStructAs<OVRSDeserialize.ColocationSessionAdvertisementCompleteData>();
                        OVRColocationSession.OnColocationSessionAdvertisementComplete(data.RequestId, data.Result);
                        break;
                    }
                case OVRPlugin.EventType.ColocationSessionDiscoveryComplete:
                    {
                        var data = eventDataBuffer.MarshalEntireStructAs<OVRSDeserialize.ColocationSessionDiscoveryCompleteData>();
                        OVRColocationSession.OnColocationSessionDiscoveryComplete(data.RequestId, data.Result);
                        break;
                    }
                case OVRPlugin.EventType.SpaceDiscoveryComplete:
                    {
                        var data = OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpaceDiscoveryCompleteData>(
                            eventDataBuffer.EventData);
                        OVRAnchor.OnSpaceDiscoveryComplete(data);
                        break;
                    }
                case OVRPlugin.EventType.SpaceDiscoveryResultsAvailable:
                    {
                        var data = OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpaceDiscoveryResultsData>(
                            eventDataBuffer.EventData);
                        OVRAnchor.OnSpaceDiscoveryResultsAvailable(data);
                        break;
                    }
                case OVRPlugin.EventType.SpacesSaveResult:
                    {
                        var data = OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpacesSaveResultData>(
                            eventDataBuffer.EventData);
                        OVRAnchor.OnSaveSpacesResult(data);
                        OVRTask.SetResult(data.RequestId, OVRResult.From(data.Result));
                        break;
                    }
                case OVRPlugin.EventType.SpacesEraseResult:
                    {
                        var data = OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.SpacesEraseResultData>(
                            eventDataBuffer.EventData);
                        OVRAnchor.OnEraseSpacesResult(data);
                        OVRTask.SetResult(data.RequestId, OVRResult.From(data.Result));
                        break;
                    }
                case OVRPlugin.EventType.PassthroughLayerResumed:
                    {
                        if (PassthroughLayerResumed != null)

                        {
                            var data =
                                OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.PassthroughLayerResumedData>(
                                    eventDataBuffer.EventData);

                            PassthroughLayerResumed(data.LayerId);
                        }
                        break;
                    }
                case OVRPlugin.EventType.BoundaryVisibilityChanged:
                    {
                        var data = OVRSDeserialize.ByteArrayToStructure<OVRSDeserialize.BoundaryVisibilityChangedData>(
                            eventDataBuffer.EventData);
                        BoundaryVisibilityChanged?.Invoke(data.BoundaryVisibility);
                        isBoundaryVisibilitySuppressed = data.BoundaryVisibility == OVRPlugin.BoundaryVisibility.Suppressed;
                        break;
                    }
                case OVRPlugin.EventType.CreateDynamicObjectTrackerResult:
                    {
                        var data = eventDataBuffer.MarshalEntireStructAs<OVRSDeserialize.CreateDynamicObjectTrackerResultData>();
                        OVRTask.SetResult(
                            OVRTask.GetId(data.Tracker, data.EventType),
                            OVRResult<ulong, OVRPlugin.Result>.From(data.Tracker, data.Result));
                        break;
                    }
                case OVRPlugin.EventType.SetDynamicObjectTrackedClassesResult:
                    {
                        var data = eventDataBuffer.MarshalEntireStructAs<OVRSDeserialize.SetDynamicObjectTrackedClassesResultData>();
                        OVRTask.SetResult(
                            OVRTask.GetId(data.Tracker, data.EventType),
                            OVRResult<OVRPlugin.Result>.From(data.Result));
                        break;
                    }
                default:
                    foreach (var listener in eventListeners)
                    {
                        listener.OnEvent(eventDataBuffer);
                    }

                    break;
            }
        }*/
    }

    public void UpdateDynamicResolutionVersion()
    {
        if (dynamicResolutionVersion == 0)
        {
            quest2MinDynamicResolutionScale = minDynamicResolutionScale;
            quest2MaxDynamicResolutionScale = maxDynamicResolutionScale;
            quest3MinDynamicResolutionScale = minDynamicResolutionScale;
            quest3MaxDynamicResolutionScale = maxDynamicResolutionScale;
        }

        dynamicResolutionVersion = MaxDynamicResolutionVersion;
    }


    private static bool multipleMainCameraWarningPresented = false;
    private static bool suppressUnableToFindMainCameraMessage = false;
    private static WeakReference<Camera> lastFoundMainCamera = null;

    public static Camera FindMainCamera()
    {
        Camera lastCamera;
        if (lastFoundMainCamera != null &&
            lastFoundMainCamera.TryGetTarget(out lastCamera) &&
            lastCamera != null &&
            lastCamera.isActiveAndEnabled &&
            lastCamera.CompareTag("MainCamera"))
        {
            return lastCamera;
        }

        Camera result = null;

        GameObject[] objects = GameObject.FindGameObjectsWithTag("MainCamera");
        List<Camera> cameras = new List<Camera>(4);
        foreach (GameObject obj in objects)
        {
            Camera camera = obj.GetComponent<Camera>();
            if (camera != null && camera.enabled)
            {
                OVRCameraRig cameraRig = camera.GetComponentInParent<OVRCameraRig>();
                if (cameraRig != null && cameraRig.trackingSpace != null)
                {
                    cameras.Add(camera);
                }
            }
        }

        if (cameras.Count == 0)
        {
            result = Camera.main; // pick one of the cameras which tagged as "MainCamera"
        }
        else if (cameras.Count == 1)
        {
            result = cameras[0];
        }
        else
        {
            if (!multipleMainCameraWarningPresented)
            {
              /*      "Multiple MainCamera found. Assume the real MainCamera is the camera with the least depth");*/
                multipleMainCameraWarningPresented = true;
            }

            // return the camera with least depth
            cameras.Sort((Camera c0, Camera c1) =>
            {
                return c0.depth < c1.depth ? -1 : (c0.depth > c1.depth ? 1 : 0);
            });
            result = cameras[0];
        }

        if (result != null)
        {
            suppressUnableToFindMainCameraMessage = false;
        }
        else if (!suppressUnableToFindMainCameraMessage)
        {
            suppressUnableToFindMainCameraMessage = true;
        }

        lastFoundMainCamera = new WeakReference<Camera>(result);
        return result;
    }

    private void OnDisable()
    {
        OVRSystemPerfMetrics.OVRSystemPerfMetricsTcpServer perfTcpServer =
            GetComponent<OVRSystemPerfMetrics.OVRSystemPerfMetricsTcpServer>();
        if (perfTcpServer != null)
        {
            perfTcpServer.enabled = false;
        }
    }

    private void LateUpdate()
    {
        OVRHaptics.Process();

        if (m_SpaceWarpEnabled)
        {
            Camera currentMainCamera = FindMainCamera();

            if (currentMainCamera != null)
            {
                Camera lastSpaceWarpCamera = null;
                if (m_lastSpaceWarpCamera != null)
                {
                    m_lastSpaceWarpCamera.TryGetTarget(out lastSpaceWarpCamera);
                }
                if (currentMainCamera != lastSpaceWarpCamera)
                {
                    // If a camera is changed while space warp is still enabled, there is some setup we have to do
                    // to make sure space warp works properly such as setting the depth texture mode.
                    PrepareCameraForSpaceWarp(currentMainCamera);
                    m_lastSpaceWarpCamera = new WeakReference<Camera>(currentMainCamera);
                }

                var pos = m_AppSpaceTransform.position;
                var rot = m_AppSpaceTransform.rotation;

                // Strange behavior may occur with non-uniform scale
              /*  var scale = m_AppSpaceTransform.lossyScale;
                SetAppSpacePosition(pos.x / scale.x, pos.y / scale.y, pos.z / scale.z);
                SetAppSpaceRotation(rot.x, rot.y, rot.z, rot.w);*/
            }
           /* else
            {
                SetAppSpacePosition(0.0f, 0.0f, 0.0f);
                SetAppSpaceRotation(0.0f, 0.0f, 0.0f, 1.0f);
            }*/
        }
    }

    private void FixedUpdate()
    {
        OVRInput.FixedUpdate();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        OVRPlugin.SetLogCallback2(null);
#endif
        OVRSimpleinitialized = false;
    }

    #endregion // Unity Messages

    enum PassthroughInitializationState
    {
        Unspecified,
        Pending,
        Initialized,
        Failed
    };

    public static Action<bool> OnPassthroughInitializedStateChange;

    private static Observable<PassthroughInitializationState> _passthroughInitializationState
        = new Observable<PassthroughInitializationState>(PassthroughInitializationState.Unspecified,
            newValue => OnPassthroughInitializedStateChange?.Invoke(newValue == PassthroughInitializationState.Initialized));

    private static bool PassthroughInitializedOrPending(PassthroughInitializationState state)
    {
        return state == PassthroughInitializationState.Pending || state == PassthroughInitializationState.Initialized;
    }

    private static bool InitializeInsightPassthrough()
    {
        if (PassthroughInitializedOrPending(_passthroughInitializationState.Value))
            return false;

        bool passthroughResult = OVRPlugin.InitializeInsightPassthrough();
        OVRPlugin.Result result = OVRPlugin.GetInsightPassthroughInitializationState();
        if (result < 0)
        {
            _passthroughInitializationState.Value = PassthroughInitializationState.Failed;
#if UNITY_EDITOR_WIN
            // Looks like the developer is trying to run PT over Link. One possible failure cause is missing PTOL setup.
            string ptolDocLink = "https://developer.oculus.com/documentation/unity/unity-passthrough-gs/#prerequisites-1";
            string ptolDocLinkTag = $"<a href=\"{ptolDocLink}\">{ptolDocLink}</a>";
#else
#endif
        }
        else
        {
            if (result == OVRPlugin.Result.Success_Pending)
            {
                _passthroughInitializationState.Value = PassthroughInitializationState.Pending;
            }
            else
            {
                _passthroughInitializationState.Value = PassthroughInitializationState.Initialized;
            }
        }

        return PassthroughInitializedOrPending(_passthroughInitializationState.Value);
    }

    private static void ShutdownInsightPassthrough()
    {
        if (PassthroughInitializedOrPending(_passthroughInitializationState.Value))
        {
            if (OVRPlugin.ShutdownInsightPassthrough())
            {
                _passthroughInitializationState.Value = PassthroughInitializationState.Unspecified;
            }
            else
            {
                // If it did not shut down, it may already be deinitialized.
                bool isInitialized = OVRPlugin.IsInsightPassthroughInitialized();
                if (isInitialized)
                {
                }
                else
                {
                    _passthroughInitializationState.Value = PassthroughInitializationState.Unspecified;
                }
            }
        }
        else
        {
            // Allow initialization to proceed on restart.
            _passthroughInitializationState.Value = PassthroughInitializationState.Unspecified;
        }
    }

    private static void UpdateInsightPassthrough(bool shouldBeEnabled)
    {
        if (shouldBeEnabled != PassthroughInitializedOrPending(_passthroughInitializationState.Value))
        {
            if (shouldBeEnabled)
            {
                // Prevent attempts to initialize on every update if failed once.
                if (_passthroughInitializationState.Value != PassthroughInitializationState.Failed)
                    InitializeInsightPassthrough();
            }
            else
            {
                ShutdownInsightPassthrough();
            }
        }
        else
        {
            // If the initialization was pending, it may have successfully completed.
            if (_passthroughInitializationState.Value == PassthroughInitializationState.Pending)
            {
                OVRPlugin.Result result = OVRPlugin.GetInsightPassthroughInitializationState();
                if (result == OVRPlugin.Result.Success)
                {
                    _passthroughInitializationState.Value = PassthroughInitializationState.Initialized;
                }
                else if (result < 0)
                {
                    _passthroughInitializationState.Value = PassthroughInitializationState.Failed;
                }
            }
        }
    }

    private static PassthroughCapabilities _passthroughCapabilities;

    private void InitializeBoundary()
    {
        var result = OVRPlugin.GetBoundaryVisibility(out var boundaryVisibility);
        if (result == OVRPlugin.Result.Success)
        {
            isBoundaryVisibilitySuppressed = boundaryVisibility == OVRPlugin.BoundaryVisibility.Suppressed;
        }
        else if (result == OVRPlugin.Result.Failure_Unsupported || result == OVRPlugin.Result.Failure_NotYetImplemented)
        {
            isBoundaryVisibilitySuppressed = false;
            shouldBoundaryVisibilityBeSuppressed = false;
        }
        else
        {
            isBoundaryVisibilitySuppressed = false;
        }
    }

    private void UpdateBoundary()
    {
        // will repeat the request as long as Passthrough is setup and
        // the desired state != actual state of the boundary
        if (shouldBoundaryVisibilityBeSuppressed == isBoundaryVisibilitySuppressed)
            return;

        var ptSupported = PassthroughInitializedOrPending(
            _passthroughInitializationState.Value) && isInsightPassthroughEnabled;
        if (!ptSupported)
            return;

        var desiredVisibility = shouldBoundaryVisibilityBeSuppressed
            ? OVRPlugin.BoundaryVisibility.Suppressed
            : OVRPlugin.BoundaryVisibility.NotSuppressed;

        var result = OVRPlugin.RequestBoundaryVisibility(desiredVisibility);
        if (result == OVRPlugin.Result.Warning_BoundaryVisibilitySuppressionNotAllowed)
        {
            if (!_updateBoundaryLogOnce)
            {
                _updateBoundaryLogOnce = true;
            }
        }
        else if (result == OVRPlugin.Result.Success)
        {
            _updateBoundaryLogOnce = false;
            isBoundaryVisibilitySuppressed = shouldBoundaryVisibilityBeSuppressed;
        }
    }
    public static bool IsMultimodalHandsControllersSupported()
    {
        return OVRPlugin.IsMultimodalHandsControllersSupported();
    }

    public static bool IsInsightPassthroughSupported()
    {
        return OVRPlugin.IsInsightPassthroughSupported();
    }

    public class PassthroughCapabilities
    {
        public bool SupportsPassthrough { get; }
        public bool SupportsColorPassthrough { get; }

        public uint MaxColorLutResolution { get; }

        public PassthroughCapabilities(bool supportsPassthrough, bool supportsColorPassthrough,
            uint maxColorLutResolution)
        {
            SupportsPassthrough = supportsPassthrough;
            SupportsColorPassthrough = supportsColorPassthrough;
            MaxColorLutResolution = maxColorLutResolution;
        }
    }
    public static PassthroughCapabilities GetPassthroughCapabilities()
    {
        if (_passthroughCapabilities == null)
        {
            OVRPlugin.PassthroughCapabilities internalCapabilities = new OVRPlugin.PassthroughCapabilities();
            if (!OVRPlugin.IsSuccess(OVRPlugin.GetPassthroughCapabilities(ref internalCapabilities)))
            {
                // Fallback to querying flags only
                internalCapabilities.Flags = OVRPlugin.GetPassthroughCapabilityFlags();
                internalCapabilities.MaxColorLutResolution = 64; // 64 is the value supported at initial release
            }

            _passthroughCapabilities = new PassthroughCapabilities(
                supportsPassthrough: (internalCapabilities.Flags & OVRPlugin.PassthroughCapabilityFlags.Passthrough) ==
                                     OVRPlugin.PassthroughCapabilityFlags.Passthrough,
                supportsColorPassthrough: (internalCapabilities.Flags & OVRPlugin.PassthroughCapabilityFlags.Color) ==
                                          OVRPlugin.PassthroughCapabilityFlags.Color,
                maxColorLutResolution: internalCapabilities.MaxColorLutResolution
            );
        }

        return _passthroughCapabilities;
    }

    public static bool IsInsightPassthroughInitialized()
    {
        return _passthroughInitializationState.Value == PassthroughInitializationState.Initialized;
    }

    public static bool HasInsightPassthroughInitFailed()
    {
        return _passthroughInitializationState.Value == PassthroughInitializationState.Failed;
    }

    public static bool IsInsightPassthroughInitPending()
    {
        return _passthroughInitializationState.Value == PassthroughInitializationState.Pending;
    }

   /* public static bool IsPassthroughRecommended()
    {
        OVRPlugin.GetPassthroughPreferences(out var preferences)
        return (preferences.Flags & OVRPlugin.PassthroughPreferenceFlags.DefaultToActive) ==
            OVRPlugin.PassthroughPreferenceFlags.DefaultToActive;;
    }*/

    #region Utils

    private class Observable<T>
    {
        private T _value;

        public Action<T> OnChanged;

        public T Value
        {
            get { return _value; }
            set
            {
                var oldValue = _value;
                this._value = value;
                if (OnChanged != null)
                {
                    OnChanged(value);
                }
            }
        }

        public Observable()
        {
        }

        public Observable(T defaultValue)
        {
            _value = defaultValue;
        }

        public Observable(T defaultValue, Action<T> callback)
            : this(defaultValue)
        {
            OnChanged += callback;
        }
    }

    #endregion
    /*internal ulong Handle { get; }
    public Guid Uuid { get; }
    internal OVRSAnchor(ulong handle, Guid uuid)
    {
        Handle = handle;
        Uuid = uuid;
    }*/

}
