/// <summary>
/// Interface shared by all <see cref="OVRSAnchor"/> components.
/// </summary>
/// <remarks>
/// For more information about the anchor-component model, see
/// [Spatial Anchor Overview](https://developer.oculus.com/documentation/unity/unity-spatial-anchors-persist-content/#ovrspatialanchor-component).
/// </remarks>
/// <typeparam name="T">The actual implementation Type of the interface.</typeparam>
/// <seealso cref="OVRSAnchor.FetchAnchorsAsync(System.Collections.Generic.List{OVRSAnchor},OVRSAnchor.FetchOptions,System.Action{System.Collections.Generic.List{OVRSAnchor},int})"/>
/// <seealso cref="OVRSAnchor.TryGetComponent{T}"/>
/// <seealso cref="OVRSAnchor.SupportsComponent{T}"/>
public interface IOVRSAnchorComponent<T>
{
    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull { get; }

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Sets the enabled status of this component.
    /// </summary>
    /// <remarks>
    /// A component must be enabled to access its data.
    /// </remarks>
    /// <param name="enable">The desired state of the component.</param>
    /// <param name="timeout">The timeout, in seconds, for the operation. Use zero to indicate an infinite timeout.</param>
    /// <returns>Returns an <see cref="OVRTask"/>&lt;bool&gt; whose result indicates the result of the operation.</returns>
    public OVRSTask<bool> SetEnabledAsync(bool enable, double timeout = 0);

    internal OVRPlugin.SpaceComponentType Type { get; }

    internal ulong Handle { get; }

    internal T FromAnchor(OVRSAnchor anchor);
}
