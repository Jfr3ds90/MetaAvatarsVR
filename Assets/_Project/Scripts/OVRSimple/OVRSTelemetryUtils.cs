using System;
using System.Collections.Generic;
using UnityEngine;

internal static partial class OVRSTelemetry
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class MarkersAttribute : Attribute
    {
    }

    private static string _sdkVersionString;

    public static OVRSTelemetryMarker AddSDKVersionAnnotation(this OVRSTelemetryMarker marker)
    {
        if (string.IsNullOrEmpty(_sdkVersionString))
        {
            _sdkVersionString = OVRPlugin.version.ToString();
        }

        return marker.AddAnnotation("sdk_version", _sdkVersionString);
    }

    public static string GetPlayModeOrigin() => Application.isPlaying
        ? Application.isEditor ? "Editor Play" : "Build Play"
        : "Editor";

    public static OVRSTelemetryMarker AddPlayModeOrigin(this OVRSTelemetryMarker marker)
    {
        return marker.AddAnnotation(OVRSTelemetryConstants.OVRSManager.AnnotationTypes.Origin, GetPlayModeOrigin());
    }

    public static string GetTelemetrySettingString(bool value) => value ? "enabled" : "disabled";
}

