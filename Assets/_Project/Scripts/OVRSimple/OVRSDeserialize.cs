using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static OVRPlugin;
public static class OVRSDeserialize
{
    public static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
    {
        T stuff;
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            stuff = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
        }
        finally
        {
            handle.Free();
        }

        return stuff;
    }

    public static unsafe T MarshalEntireStructAs<T>(this EventDataBuffer eventDataBuffer, Allocator allocator = Allocator.Temp)
    {
        using var buffer = new NativeArray<byte>(eventDataBuffer.EventData.Length + sizeof(OVRPlugin.EventType), allocator);
        var dst = (byte*)buffer.GetUnsafePtr();

        fixed (byte* src = eventDataBuffer.EventData)
        {
            *(OVRPlugin.EventType*)dst = eventDataBuffer.EventType;
            UnsafeUtility.MemCpy(
                destination: dst + sizeof(OVRPlugin.EventType),
                source: src,
                eventDataBuffer.EventData.Length);

            return Marshal.PtrToStructure<T>(new IntPtr(dst));
        }
    }

    public struct DisplayRefreshRateChangedData
    {
        public float FromRefreshRate;
        public float ToRefreshRate;
    }

    public struct SpaceQueryResultsData
    {
        public UInt64 RequestId;
    }

    public struct SpaceQueryCompleteData
    {
        public UInt64 RequestId;
        public int Result;
    }

    /// <summary>This is an internal type.</summary>
    public struct SceneCaptureCompleteData
    {
        public UInt64 RequestId;
        public int Result;
    }




    public struct SpatialAnchorCreateCompleteData
    {
        public UInt64 RequestId;
        public int Result;
        public UInt64 Space;
        public Guid Uuid;
    }

    public struct SpaceSetComponentStatusCompleteData
    {
        public UInt64 RequestId;
        public int Result;
        public UInt64 Space;
        public Guid Uuid;
        public OVRPlugin.SpaceComponentType ComponentType;
        public int Enabled;
    }

    public struct SpaceSaveCompleteData
    {
        public UInt64 RequestId;
        public UInt64 Space;
        public int Result;
        public Guid Uuid;
    }

    public struct SpaceEraseCompleteData
    {
        public UInt64 RequestId;
        public int Result;
        public Guid Uuid;
        public OVRPlugin.SpaceStorageLocation Location;
    }

    public struct SpaceShareResultData
    {
        public UInt64 RequestId;

        public int Result;
    }

    public struct SpaceListSaveResultData
    {
        public UInt64 RequestId;

        public int Result;
    }

    public struct StartColocationSessionAdvertisementCompleteData
    {
        public EventType EventType;

        public UInt64 RequestId;

        public Result Result;

        public Guid AdvertisementUuid;
    }

    public struct StopColocationSessionAdvertisementCompleteData
    {
        public EventType EventType;

        public UInt64 RequestId;

        public Result Result;
    }

    public struct StartColocationSessionDiscoveryCompleteData
    {
        public EventType EventType;

        public UInt64 RequestId;

        public Result Result;
    }

    public struct StopColocationSessionDiscoveryCompleteData
    {
        public EventType EventType;

        public UInt64 RequestId;

        public Result Result;
    }

    public unsafe struct ColocationSessionDiscoveryResultData
    {
        public EventType EventType;

        public UInt64 RequestId;

        public Guid AdvertisementUuid;

        public UInt32 AdvertisementMetadataCount;

        public fixed byte AdvertisementMetadata[1024];
    }

    public struct ColocationSessionAdvertisementCompleteData
    {
        public EventType EventType;

        public UInt64 RequestId;

        public Result Result;
    }

    public struct ColocationSessionDiscoveryCompleteData
    {
        public EventType EventType;

        public UInt64 RequestId;

        public Result Result;
    }

    public struct ShareSpacesToGroupsCompleteData
    {
        public EventType EventType;

        public UInt64 RequestId;

        public Result Result;
    }

    public struct SpaceDiscoveryCompleteData
    {
        public UInt64 RequestId;
        public int Result;
    }

    public struct SpaceDiscoveryResultsData
    {
        public UInt64 RequestId;
    }

    public struct SpacesSaveResultData
    {
        public UInt64 RequestId;
        public OVRAnchor.SaveResult Result;
    }

    public struct SpacesEraseResultData
    {
        public UInt64 RequestId;
        public OVRAnchor.EraseResult Result;
    }

    /// <summary>
    /// This is a low-level structure used to make the <see cref="OVRPassthroughLayer.PassthroughLayerResumed"/> event work.
    /// Use this event to get notifications when a passthrough layer has been rendered for the first time after being restarted.
    /// </summary>
    public struct PassthroughLayerResumedData
    {
        /// <summary>
        /// Stores the passthrough layer's id which has just been started or resumed after the pause.
        /// See <see cref="OVROverlay.layerId"/> of the <see cref="OVRPassthroughLayer"/> component.
        /// </summary>
        public int LayerId;
    }

    public struct BoundaryVisibilityChangedData
    {
        public BoundaryVisibility BoundaryVisibility;
    }

    public struct CreateDynamicObjectTrackerResultData
    {
        public EventType EventType;
        public ulong Tracker;
        public Result Result;
    }

    public struct SetDynamicObjectTrackedClassesResultData
    {
        public EventType EventType;
        public ulong Tracker;
        public Result Result;
    }

}
