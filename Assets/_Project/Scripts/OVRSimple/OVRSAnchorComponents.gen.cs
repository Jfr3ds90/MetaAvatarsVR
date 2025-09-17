using System;
using System.Collections.Generic;
using static OVRPlugin;

public readonly partial struct OVRSLocatable : IOVRSAnchorComponent<OVRSLocatable>, IEquatable<OVRSLocatable>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSLocatable>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSLocatable>.Handle => Handle;

    OVRSLocatable IOVRSAnchorComponent<OVRSLocatable>.FromAnchor(OVRSAnchor anchor) => new OVRSLocatable(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSLocatable.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSLocatable Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// <summary>
    /// Sets the enabled status of this component.
    /// </summary>
    /// <remarks>
    /// A component must be enabled in order to access its data or do enable its functionality.
    ///
    /// This method is asynchronous. Use the returned task to track the completion of this operation. The task's value
    /// indicates whether the operation was successful.
    ///
    /// If the current enabled state matches <paramref name="enabled"/>, then the returned task completes immediately
    /// with a `true` result. If there is already a pending change to the enabled state, the new request is queued.
    /// </remarks>
    /// <param name="enabled">The desired state of the component.</param>
    /// <param name="timeout">The timeout, in seconds, for the operation. Use zero to indicate an infinite timeout.</param>
    /// <returns>Returns an <see cref="OVRSTask"/>&lt;bool&gt; whose result indicates the result of the operation.</returns>
    public OVRSTask<bool> SetEnabledAsync(bool enabled, double timeout = 0)
    {
        if (!GetSpaceComponentStatus(Handle, Type, out var isEnabled, out var changePending))
        {
            return OVRSTask.FromResult(false);
        }

        if (changePending)
        {
            return OVRSAnchor.CreateDeferredSpaceComponentStatusTask(Handle, Type, enabled, timeout);
        }

        return isEnabled == enabled
            ? OVRSTask.FromResult(true)
            : OVRSTask
                .Build(SetSpaceComponentStatus(Handle, Type, enabled, timeout, out var requestId), requestId)
                .ToTask(failureValue: false);
    }

    /// <summary>
    /// (Obsolete) Sets the enabled status of this component if it differs from the current enabled state.
    /// </summary>
    /// <remarks>
    /// This method is obsolete. Use <see cref="SetEnabledAsync"/> instead.
    /// </remarks>
    /// <param name="enabled">The desired state of the component.</param>
    /// <param name="timeout">The timeout, in seconds, for the operation. Use zero to indicate an infinite timeout.</param>
    /// <returns>Returns an <see cref="OVRSTask"/>&lt;bool&gt; whose result indicates the result of the operation.</returns>
    [Obsolete("Use SetEnabledAsync instead.")]
    public OVRSTask<bool> SetEnabledSafeAsync(bool enabled, double timeout = 0) => SetEnabledAsync(enabled, timeout);

    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSLocatable other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSLocatable lhs, OVRSLocatable rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSLocatable lhs, OVRSLocatable rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSLocatable and <see cref="Equals(OVRSLocatable)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSLocatable other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.Locatable";

    internal SpaceComponentType Type => SpaceComponentType.Locatable;

    internal ulong Handle { get; }

    private OVRSLocatable(OVRSAnchor anchor) => Handle = anchor.Handle;
}

public readonly partial struct OVRSStorable : IOVRSAnchorComponent<OVRSStorable>, IEquatable<OVRSStorable>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSStorable>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSStorable>.Handle => Handle;

    OVRSStorable IOVRSAnchorComponent<OVRSStorable>.FromAnchor(OVRSAnchor anchor) => new OVRSStorable(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSStorable.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSStorable Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// <summary>
    /// Sets the enabled status of this component.
    /// </summary>
    /// <remarks>
    /// A component must be enabled in order to access its data or do enable its functionality.
    ///
    /// This method is asynchronous. Use the returned task to track the completion of this operation. The task's value
    /// indicates whether the operation was successful.
    ///
    /// If the current enabled state matches <paramref name="enabled"/>, then the returned task completes immediately
    /// with a `true` result. If there is already a pending change to the enabled state, the new request is queued.
    /// </remarks>
    /// <param name="enabled">The desired state of the component.</param>
    /// <param name="timeout">The timeout, in seconds, for the operation. Use zero to indicate an infinite timeout.</param>
    /// <returns>Returns an <see cref="OVRSTask"/>&lt;bool&gt; whose result indicates the result of the operation.</returns>
    public OVRSTask<bool> SetEnabledAsync(bool enabled, double timeout = 0)
    {
        if (!GetSpaceComponentStatus(Handle, Type, out var isEnabled, out var changePending))
        {
            return OVRSTask.FromResult(false);
        }

        if (changePending)
        {
            return OVRSAnchor.CreateDeferredSpaceComponentStatusTask(Handle, Type, enabled, timeout);
        }

        return isEnabled == enabled
            ? OVRSTask.FromResult(true)
            : OVRSTask
                .Build(SetSpaceComponentStatus(Handle, Type, enabled, timeout, out var requestId), requestId)
                .ToTask(failureValue: false);
    }

    /// <summary>
    /// (Obsolete) Sets the enabled status of this component if it differs from the current enabled state.
    /// </summary>
    /// <remarks>
    /// This method is obsolete. Use <see cref="SetEnabledAsync"/> instead.
    /// </remarks>
    /// <param name="enabled">The desired state of the component.</param>
    /// <param name="timeout">The timeout, in seconds, for the operation. Use zero to indicate an infinite timeout.</param>
    /// <returns>Returns an <see cref="OVRSTask"/>&lt;bool&gt; whose result indicates the result of the operation.</returns>
    [Obsolete("Use SetEnabledAsync instead.")]
    public OVRSTask<bool> SetEnabledSafeAsync(bool enabled, double timeout = 0) => SetEnabledAsync(enabled, timeout);

    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSStorable other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSStorable lhs, OVRSStorable rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSStorable lhs, OVRSStorable rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSStorable and <see cref="Equals(OVRSStorable)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSStorable other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.Storable";

    internal SpaceComponentType Type => SpaceComponentType.Storable;

    internal ulong Handle { get; }

    private OVRSStorable(OVRSAnchor anchor) => Handle = anchor.Handle;
}

public readonly partial struct OVRSSharable : IOVRSAnchorComponent<OVRSSharable>, IEquatable<OVRSSharable>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSSharable>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSSharable>.Handle => Handle;

    OVRSSharable IOVRSAnchorComponent<OVRSSharable>.FromAnchor(OVRSAnchor anchor) => new OVRSSharable(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSSharable.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSSharable Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// <summary>
    /// Sets the enabled status of this component.
    /// </summary>
    /// <remarks>
    /// A component must be enabled in order to access its data or do enable its functionality.
    ///
    /// This method is asynchronous. Use the returned task to track the completion of this operation. The task's value
    /// indicates whether the operation was successful.
    ///
    /// If the current enabled state matches <paramref name="enabled"/>, then the returned task completes immediately
    /// with a `true` result. If there is already a pending change to the enabled state, the new request is queued.
    /// </remarks>
    /// <param name="enabled">The desired state of the component.</param>
    /// <param name="timeout">The timeout, in seconds, for the operation. Use zero to indicate an infinite timeout.</param>
    /// <returns>Returns an <see cref="OVRSTask"/>&lt;bool&gt; whose result indicates the result of the operation.</returns>
    public OVRSTask<bool> SetEnabledAsync(bool enabled, double timeout = 0)
    {
        if (!GetSpaceComponentStatus(Handle, Type, out var isEnabled, out var changePending))
        {
            return OVRSTask.FromResult(false);
        }

        if (changePending)
        {
            return OVRSAnchor.CreateDeferredSpaceComponentStatusTask(Handle, Type, enabled, timeout);
        }

        return isEnabled == enabled
            ? OVRSTask.FromResult(true)
            : OVRSTask
                .Build(SetSpaceComponentStatus(Handle, Type, enabled, timeout, out var requestId), requestId)
                .ToTask(failureValue: false);
    }

    /// <summary>
    /// (Obsolete) Sets the enabled status of this component if it differs from the current enabled state.
    /// </summary>
    /// <remarks>
    /// This method is obsolete. Use <see cref="SetEnabledAsync"/> instead.
    /// </remarks>
    /// <param name="enabled">The desired state of the component.</param>
    /// <param name="timeout">The timeout, in seconds, for the operation. Use zero to indicate an infinite timeout.</param>
    /// <returns>Returns an <see cref="OVRSTask"/>&lt;bool&gt; whose result indicates the result of the operation.</returns>
    [Obsolete("Use SetEnabledAsync instead.")]
    public OVRSTask<bool> SetEnabledSafeAsync(bool enabled, double timeout = 0) => SetEnabledAsync(enabled, timeout);

    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSSharable other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSSharable lhs, OVRSSharable rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSSharable lhs, OVRSSharable rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSSharable and <see cref="Equals(OVRSSharable)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSSharable other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.Sharable";

    internal SpaceComponentType Type => SpaceComponentType.Sharable;

    internal ulong Handle { get; }

   private OVRSSharable(OVRSAnchor anchor) => Handle = anchor.Handle;
}

public readonly partial struct OVRSBounded2D : IOVRSAnchorComponent<OVRSBounded2D>, IEquatable<OVRSBounded2D>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSBounded2D>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSBounded2D>.Handle => Handle;

    OVRSBounded2D IOVRSAnchorComponent<OVRSBounded2D>.FromAnchor(OVRSAnchor anchor) => new OVRSBounded2D(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSBounded2D.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSBounded2D Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// @cond
    OVRSTask<bool> IOVRSAnchorComponent<OVRSBounded2D>.SetEnabledAsync(bool enabled, double timeout)
        => throw new NotSupportedException("The Bounded2D component cannot be enabled or disabled.");
    /// @endcond


    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSBounded2D other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSBounded2D lhs, OVRSBounded2D rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSBounded2D lhs, OVRSBounded2D rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSBounded2D and <see cref="Equals(OVRSBounded2D)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSBounded2D other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.Bounded2D";

    internal SpaceComponentType Type => SpaceComponentType.Bounded2D;

    internal ulong Handle { get; }

    private OVRSBounded2D(OVRSAnchor anchor) => Handle = anchor.Handle;
}

public readonly partial struct OVRSBounded3D : IOVRSAnchorComponent<OVRSBounded3D>, IEquatable<OVRSBounded3D>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSBounded3D>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSBounded3D>.Handle => Handle;

    OVRSBounded3D IOVRSAnchorComponent<OVRSBounded3D>.FromAnchor(OVRSAnchor anchor) => new OVRSBounded3D(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSBounded3D.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSBounded3D Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// @cond
    OVRSTask<bool> IOVRSAnchorComponent<OVRSBounded3D>.SetEnabledAsync(bool enabled, double timeout)
        => throw new NotSupportedException("The Bounded3D component cannot be enabled or disabled.");
    /// @endcond


    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSBounded3D other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSBounded3D lhs, OVRSBounded3D rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSBounded3D lhs, OVRSBounded3D rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSBounded3D and <see cref="Equals(OVRSBounded3D)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSBounded3D other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.Bounded3D";

    internal SpaceComponentType Type => SpaceComponentType.Bounded3D;

    internal ulong Handle { get; }

    private OVRSBounded3D(OVRSAnchor anchor) => Handle = anchor.Handle;
}

public readonly partial struct OVRSSemanticLabels : IOVRSAnchorComponent<OVRSSemanticLabels>, IEquatable<OVRSSemanticLabels>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSSemanticLabels>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSSemanticLabels>.Handle => Handle;

    OVRSSemanticLabels IOVRSAnchorComponent<OVRSSemanticLabels>.FromAnchor(OVRSAnchor anchor) => new OVRSSemanticLabels(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSSemanticLabels.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSSemanticLabels Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// @cond
    OVRSTask<bool> IOVRSAnchorComponent<OVRSSemanticLabels>.SetEnabledAsync(bool enabled, double timeout)
        => throw new NotSupportedException("The SemanticLabels component cannot be enabled or disabled.");
    /// @endcond


    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSSemanticLabels other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSSemanticLabels lhs, OVRSSemanticLabels rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSSemanticLabels lhs, OVRSSemanticLabels rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSSemanticLabels and <see cref="Equals(OVRSSemanticLabels)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSSemanticLabels other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.SemanticLabels";

    internal SpaceComponentType Type => SpaceComponentType.SemanticLabels;

    internal ulong Handle { get; }

    private OVRSSemanticLabels(OVRSAnchor anchor) => Handle = anchor.Handle;
}

public readonly partial struct OVRSRoomLayout : IOVRSAnchorComponent<OVRSRoomLayout>, IEquatable<OVRSRoomLayout>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSRoomLayout>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSRoomLayout>.Handle => Handle;

    OVRSRoomLayout IOVRSAnchorComponent<OVRSRoomLayout>.FromAnchor(OVRSAnchor anchor) => new OVRSRoomLayout(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSRoomLayout.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSRoomLayout Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// @cond
    OVRSTask<bool> IOVRSAnchorComponent<OVRSRoomLayout>.SetEnabledAsync(bool enabled, double timeout)
        => throw new NotSupportedException("The RoomLayout component cannot be enabled or disabled.");
    /// @endcond


    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSRoomLayout other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSRoomLayout lhs, OVRSRoomLayout rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSRoomLayout lhs, OVRSRoomLayout rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSRoomLayout and <see cref="Equals(OVRSRoomLayout)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSRoomLayout other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.RoomLayout";

    internal SpaceComponentType Type => SpaceComponentType.RoomLayout;

    internal ulong Handle { get; }

    private OVRSRoomLayout(OVRSAnchor anchor) => Handle = anchor.Handle;
}

public readonly partial struct OVRSAnchorContainer : IOVRSAnchorComponent<OVRSAnchorContainer>, IEquatable<OVRSAnchorContainer>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSAnchorContainer>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSAnchorContainer>.Handle => Handle;

    OVRSAnchorContainer IOVRSAnchorComponent<OVRSAnchorContainer>.FromAnchor(OVRSAnchor anchor) => new OVRSAnchorContainer(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSAnchorContainer.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSAnchorContainer Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// @cond
    OVRSTask<bool> IOVRSAnchorComponent<OVRSAnchorContainer>.SetEnabledAsync(bool enabled, double timeout)
        => throw new NotSupportedException("The AnchorContainer component cannot be enabled or disabled.");
    /// @endcond


    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSAnchorContainer other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSAnchorContainer lhs, OVRSAnchorContainer rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSAnchorContainer lhs, OVRSAnchorContainer rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSAnchorContainer and <see cref="Equals(OVRSAnchorContainer)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSAnchorContainer other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.AnchorContainer";

    internal SpaceComponentType Type => SpaceComponentType.SpaceContainer;

    internal ulong Handle { get; }

    private OVRSAnchorContainer(OVRSAnchor anchor) => Handle = anchor.Handle;
}

public readonly partial struct OVRSTriangleMesh : IOVRSAnchorComponent<OVRSTriangleMesh>, IEquatable<OVRSTriangleMesh>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSTriangleMesh>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSTriangleMesh>.Handle => Handle;

    OVRSTriangleMesh IOVRSAnchorComponent<OVRSTriangleMesh>.FromAnchor(OVRSAnchor anchor) => new OVRSTriangleMesh(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSTriangleMesh.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSTriangleMesh Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// @cond
    OVRSTask<bool> IOVRSAnchorComponent<OVRSTriangleMesh>.SetEnabledAsync(bool enabled, double timeout)
        => throw new NotSupportedException("The TriangleMesh component cannot be enabled or disabled.");
    /// @endcond


    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSTriangleMesh other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSTriangleMesh lhs, OVRSTriangleMesh rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSTriangleMesh lhs, OVRSTriangleMesh rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSTriangleMesh and <see cref="Equals(OVRSTriangleMesh)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSTriangleMesh other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.TriangleMesh";

    internal SpaceComponentType Type => SpaceComponentType.TriangleMesh;

    internal ulong Handle { get; }

    private OVRSTriangleMesh(OVRSAnchor anchor) => Handle = anchor.Handle;
}



public readonly partial struct OVRSDynamicObject : IOVRSAnchorComponent<OVRSDynamicObject>, IEquatable<OVRSDynamicObject>
{
    /// @cond

    SpaceComponentType IOVRSAnchorComponent<OVRSDynamicObject>.Type => Type;

    ulong IOVRSAnchorComponent<OVRSDynamicObject>.Handle => Handle;

    OVRSDynamicObject IOVRSAnchorComponent<OVRSDynamicObject>.FromAnchor(OVRSAnchor anchor) => new OVRSDynamicObject(anchor);

    /// @endcond

    /// <summary>
    /// A null representation of an OVRSDynamicObject.
    /// </summary>
    /// <remarks>
    /// Use this to compare with another component to determine whether it is null.
    /// </remarks>
    public static readonly OVRSDynamicObject Null = default;

    /// <summary>
    /// Whether this object represents a valid anchor component.
    /// </summary>
    public bool IsNull => Handle == 0;

    /// <summary>
    /// True if this component is enabled and no change to its enabled status is pending.
    /// </summary>
    public bool IsEnabled => !IsNull && GetSpaceComponentStatus(Handle, Type, out var enabled, out var pending) && enabled && !pending;




    /// @cond
    OVRSTask<bool> IOVRSAnchorComponent<OVRSDynamicObject>.SetEnabledAsync(bool enabled, double timeout)
        => throw new NotSupportedException("The DynamicObject component cannot be enabled or disabled.");
    /// @endcond


    /// <summary>
    /// Compares this component for equality with <paramref name="other" />.
    /// </summary>
    /// <param name="other">The other component to compare with.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public bool Equals(OVRSDynamicObject other) => Handle == other.Handle;

    /// <summary>
    /// Compares two components for equality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if both components belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator ==(OVRSDynamicObject lhs, OVRSDynamicObject rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two components for inequality.
    /// </summary>
    /// <param name="lhs">The component to compare with <paramref name="rhs" />.</param>
    /// <param name="rhs">The component to compare with <paramref name="lhs" />.</param>
    /// <returns>True if the components do not belong to the same <see cref="OVRSAnchor" />, otherwise false.</returns>
    public static bool operator !=(OVRSDynamicObject lhs, OVRSDynamicObject rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Compares this component for equality with <paramref name="obj" />.
    /// </summary>
    /// <param name="obj">The `object` to compare with.</param>
    /// <returns>True if <paramref name="obj" /> is an OVRSDynamicObject and <see cref="Equals(OVRSDynamicObject)" /> is true, otherwise false.</returns>
    public override bool Equals(object obj) => obj is OVRSDynamicObject other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>A hashcode for this component.</returns>
    public override int GetHashCode() => unchecked(Handle.GetHashCode() * 486187739 + ((int)Type).GetHashCode());

    /// <summary>
    /// Gets a string representation of this component.
    /// </summary>
    /// <returns>A string representation of this component.</returns>
    public override string ToString() => $"{Handle}.DynamicObject";

    internal SpaceComponentType Type => SpaceComponentType.DynamicObject;

    internal ulong Handle { get; }

    private OVRSDynamicObject(OVRSAnchor anchor) => Handle = anchor.Handle;
}



partial struct OVRSAnchor
{
    internal static readonly Dictionary<Type, SpaceComponentType> _typeMap = new()
    {
        { typeof(OVRSLocatable), SpaceComponentType.Locatable },
        { typeof(OVRSStorable), SpaceComponentType.Storable },
        { typeof(OVRSSharable), SpaceComponentType.Sharable },
        { typeof(OVRSBounded2D), SpaceComponentType.Bounded2D },
        { typeof(OVRSBounded3D), SpaceComponentType.Bounded3D },
        { typeof(OVRSSemanticLabels), SpaceComponentType.SemanticLabels },
        { typeof(OVRSRoomLayout), SpaceComponentType.RoomLayout },
        { typeof(OVRSAnchorContainer), SpaceComponentType.SpaceContainer },
        { typeof(OVRSTriangleMesh), SpaceComponentType.TriangleMesh },
        { typeof(OVRSDynamicObject), SpaceComponentType.DynamicObject },
    };
}

