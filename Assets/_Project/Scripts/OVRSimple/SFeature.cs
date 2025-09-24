using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meta.XR.Util
{
    internal enum SFeature
    {
        Anchors,
        BodyTracking,
        EyeTracking,
        FaceTracking,
        Hands,
        Interaction,
        Passthrough,
        Scene,
        TrackedKeyboard,
        VirtualKeyboard
    }

    /// <summary>
    /// Represents an attribute for marking classes with feature they belong to.
    /// </summary>
    /// <remarks>
    /// This attribute is used internally.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    internal class SFeatureAttribute : Attribute
    {
        public SFeatureAttribute(SFeature feature)
        {
            SFeature = feature;
        }
        public SFeature SFeature { get; }
    }
}
