using System;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static OVRSTelemetry;

#if UNITY_EDITOR
using UnityEditor;
#endif

internal struct OVRSTelemetryMarker : IDisposable
{
    internal struct OVRSTelemetryMarkerState
    {
        public bool Sent { get; set; }
        public OVRPlugin.Qpl.ResultType Result { get; set; }

        public OVRSTelemetryMarkerState(bool sent, OVRPlugin.Qpl.ResultType result)
        {
            Result = result;
            Sent = sent;
        }
    }



    private OVRSTelemetryMarkerState State { get; set; }

    public bool Sent => State.Sent;
    public OVRPlugin.Qpl.ResultType Result => State.Result;

    public int MarkerId { get; }
    public int InstanceKey { get; }

    private readonly TelemetryClient _client;

    public OVRSTelemetryMarker(
        int markerId,
        int instanceKey = OVRPlugin.Qpl.DefaultInstanceKey,
        long timestampMs = OVRPlugin.Qpl.AutoSetTimestampMs, string joindId = null)
        : this(
            OVRSTelemetry.Client,
            markerId,
            instanceKey,
            timestampMs, joindId)
    {
    }

    internal OVRSTelemetryMarker(
        TelemetryClient client,
        int markerId,
        int instanceKey = OVRPlugin.Qpl.DefaultInstanceKey,
        long timestampMs = OVRPlugin.Qpl.AutoSetTimestampMs,
        string joinId = null)
    {
        MarkerId = markerId;
        InstanceKey = instanceKey;
        _client = client;
        State = new OVRSTelemetryMarkerState(false, OVRPlugin.Qpl.ResultType.Success);

        _client.MarkerStart(markerId, instanceKey, timestampMs, joinId);
    }

    public OVRSTelemetryMarker SetResult(OVRPlugin.Qpl.ResultType result)
    {
        State = new OVRSTelemetryMarkerState(Sent, result);
        return this;
    }

    public OVRSTelemetryMarker AddAnnotation(string annotationKey, string annotationValue, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        if (string.IsNullOrEmpty(annotationKey))
        {
            return this;
        }

        annotationValue ??= string.Empty;

        if (eAnnotationType == OVRSTelemetryConstants.Editor.AnnotationVariant.Required || GetOVRSTelemetryConsent())
        {
            _client.MarkerAnnotation(MarkerId, annotationKey, annotationValue, InstanceKey);
        }
        return this;
    }

    public OVRSTelemetryMarker AddAnnotation(string annotationKey, bool annotationValue, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        if (string.IsNullOrEmpty(annotationKey))
        {
            return this;
        }

        if (eAnnotationType == OVRSTelemetryConstants.Editor.AnnotationVariant.Required || GetOVRSTelemetryConsent())
        {
            _client.MarkerAnnotation(MarkerId, annotationKey, annotationValue, InstanceKey);
        }
        return this;
    }

    public OVRSTelemetryMarker AddAnnotation(string annotationKey, double annotationValue, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        if (string.IsNullOrEmpty(annotationKey))
        {
            return this;
        }

        if (eAnnotationType == OVRSTelemetryConstants.Editor.AnnotationVariant.Required || GetOVRSTelemetryConsent())
        {
            _client.MarkerAnnotation(MarkerId, annotationKey, annotationValue, InstanceKey);
        }
        return this;
    }

    public OVRSTelemetryMarker AddAnnotation(string annotationKey, long annotationValue, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        if (string.IsNullOrEmpty(annotationKey))
        {
            return this;
        }

        if (eAnnotationType == OVRSTelemetryConstants.Editor.AnnotationVariant.Required || GetOVRSTelemetryConsent())
        {
            _client.MarkerAnnotation(MarkerId, annotationKey, annotationValue, InstanceKey);
        }
        return this;
    }

    public unsafe OVRSTelemetryMarker AddAnnotation(string annotationKey, byte** annotationValues, int count, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        if (string.IsNullOrEmpty(annotationKey))
        {
            return this;
        }

        if (eAnnotationType == OVRSTelemetryConstants.Editor.AnnotationVariant.Required || GetOVRSTelemetryConsent())
        {
            _client.MarkerAnnotation(MarkerId, annotationKey, annotationValues, count, InstanceKey);
        }
        return this;
    }

    public unsafe OVRSTelemetryMarker AddAnnotation(string annotationKey, ReadOnlySpan<long> annotationValues,
        OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType =
            OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        fixed (long* ptr = annotationValues)
        {
            return AddAnnotation(annotationKey, ptr, annotationValues.Length, eAnnotationType);
        }
    }

    public unsafe OVRSTelemetryMarker AddAnnotation(string annotationKey, long* annotationValues, int count, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        if (string.IsNullOrEmpty(annotationKey))
        {
            return this;
        }

        if (eAnnotationType == OVRSTelemetryConstants.Editor.AnnotationVariant.Required || GetOVRSTelemetryConsent())
        {
            _client.MarkerAnnotation(MarkerId, annotationKey, annotationValues, count, InstanceKey);
        }
        return this;
    }

    public unsafe OVRSTelemetryMarker AddAnnotation<T>(string annotationKey, ReadOnlySpan<T> annotationValues,
        OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType =
            OVRSTelemetryConstants.Editor.AnnotationVariant.Required) where T : unmanaged, Enum
    {
        // If the underlying type is already a long or ulong, we can just cast it.
        var underlyingType = Enum.GetUnderlyingType(typeof(T));
        if (underlyingType == typeof(long) || underlyingType == typeof(ulong))
        {
            fixed (T* values = annotationValues)
            {
                return AddAnnotation(annotationKey, (long*)values, annotationValues.Length, eAnnotationType);
            }
        }

        // Otherwise, we need to make a copy.
        var longs = new NativeArray<long>(annotationValues.Length, Allocator.Temp);
        try
        {
            for (var i = 0; i < annotationValues.Length; i++)
            {
                longs[i] = UnsafeUtility.EnumToInt(annotationValues[i]);
            }

            return AddAnnotation(annotationKey, (long*)longs.GetUnsafeReadOnlyPtr(), longs.Length, eAnnotationType);
        }
        finally
        {
            longs.Dispose();
        }
    }

    public unsafe OVRSTelemetryMarker AddAnnotation(string annotationKey, ReadOnlySpan<double> annotationValues,
        OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType =
            OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        fixed (double* ptr = annotationValues)
        {
            return AddAnnotation(annotationKey, ptr, annotationValues.Length, eAnnotationType);
        }
    }

    public unsafe OVRSTelemetryMarker AddAnnotation(string annotationKey, double* annotationValues, int count, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        if (string.IsNullOrEmpty(annotationKey))
        {
            return this;
        }

        if (eAnnotationType == OVRSTelemetryConstants.Editor.AnnotationVariant.Required || GetOVRSTelemetryConsent())
        {
            _client.MarkerAnnotation(MarkerId, annotationKey, annotationValues, count, InstanceKey);
        }
        return this;
    }

    public unsafe OVRSTelemetryMarker AddAnnotation(string annotationKey, ReadOnlySpan<OVRPlugin.Bool> annotationValues,
        OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType =
            OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        fixed (OVRPlugin.Bool* ptr = annotationValues)
        {
            return AddAnnotation(annotationKey, ptr, annotationValues.Length, eAnnotationType);
        }
    }

    public unsafe OVRSTelemetryMarker AddAnnotation(string annotationKey, OVRPlugin.Bool* annotationValues, int count, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        if (string.IsNullOrEmpty(annotationKey))
        {
            return this;
        }

        if (eAnnotationType == OVRSTelemetryConstants.Editor.AnnotationVariant.Required || GetOVRSTelemetryConsent())
        {
            _client.MarkerAnnotation(MarkerId, annotationKey, annotationValues, count, InstanceKey);
        }
        return this;
    }

    public OVRSTelemetryMarker AddAnnotationIfNotNullOrEmpty(string annotationKey, string annotationValue, OVRSTelemetryConstants.Editor.AnnotationVariant eAnnotationType = OVRSTelemetryConstants.Editor.AnnotationVariant.Required)
    {
        return string.IsNullOrEmpty(annotationValue) ? this : AddAnnotation(annotationKey, annotationValue, eAnnotationType);
    }

    private static string _applicationIdentifier;
    private static string ApplicationIdentifier => _applicationIdentifier ??= Application.identifier;

    private static string _unityVersion;
    private static string UnityVersion => _unityVersion ??= Application.unityVersion;

    private static bool? _isBatchMode;
    private static bool IsBatchMode => _isBatchMode ??= Application.isBatchMode;

    private const string TelemetryEnabledKey = "OVRSTelemetry.TelemetryEnabled";

    private bool GetOVRSTelemetryConsent()
    {
        const bool defaultTelemetryStatus = false;
#if !UNITY_EDITOR
        return defaultTelemetryStatus;
#else
        return EditorPrefs.GetBool(TelemetryEnabledKey, defaultTelemetryStatus);
#endif
    }

    public OVRSTelemetryMarker Send()
    {

        AddAnnotation(OVRSTelemetryConstants.OVRSManager.AnnotationTypes.ProjectName, ApplicationIdentifier, OVRSTelemetryConstants.Editor.AnnotationVariant.Optional);
        AddAnnotation(OVRSTelemetryConstants.OVRSManager.AnnotationTypes.ProjectGuid, OVRSRuntimeSettings.Instance.TelemetryProjectGuid);
        AddAnnotation(OVRSTelemetryConstants.OVRSManager.AnnotationTypes.BatchMode, IsBatchMode);
        AddAnnotation(OVRSTelemetryConstants.OVRSManager.AnnotationTypes.ProcessorType, SystemInfo.processorType);

        State = new OVRSTelemetryMarkerState(true, Result);
        _client.MarkerEnd(MarkerId, Result, InstanceKey);

        return this;
    }

    public OVRSTelemetryMarker SendIf(bool condition)
    {
        if (condition)
        {
            return Send();
        }

        State = new OVRSTelemetryMarkerState(true, Result);
        return this;
    }

    public OVRSTelemetryMarker AddPoint(OVRSTelemetry.MarkerPoint point)
    {
        _client.MarkerPointCached(MarkerId, point.NameHandle, InstanceKey);
        return this;
    }

    public OVRSTelemetryMarker AddPoint(string name)
    {
        _client.MarkerPoint(MarkerId, name, InstanceKey);
        return this;
    }

    public unsafe OVRSTelemetryMarker AddPoint(string name, OVRPlugin.Qpl.Annotation.Builder annotationBuilder)
    {
        using var array = annotationBuilder.ToNativeArray();
        return AddPoint(name, (OVRPlugin.Qpl.Annotation*)array.GetUnsafeReadOnlyPtr(), array.Length);
    }

    public unsafe OVRSTelemetryMarker AddPoint(string name, OVRPlugin.Qpl.Annotation* annotations, int annotationCount)
    {
        _client.MarkerPoint(MarkerId, name, annotations, annotationCount, InstanceKey);
        return this;
    }

    public void Dispose()
    {
        if (!Sent)
        {
            Send();
        }
    }

}

