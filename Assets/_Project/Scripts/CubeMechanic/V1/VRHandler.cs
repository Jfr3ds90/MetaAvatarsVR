using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// Simple VR handler for MVPMagneticCube without networking dependencies
/// Handles OVR integration and haptic feedback
/// </summary>
public class VRHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MagneticCube magneticCube;
    [SerializeField] private Grabbable grabbable;
    
    [Header("Haptic Feedback")]
    [SerializeField] private bool enableHapticFeedback = true;
    [SerializeField] private float snapHapticIntensity = 0.8f;
    [SerializeField] private float snapHapticDuration = 0.2f;
    [SerializeField] private float grabHapticIntensity = 0.3f;
    [SerializeField] private float grabHapticDuration = 0.1f;
    
    [Header("Audio")]
    [SerializeField] private bool enablePositionalAudio = true;
    [SerializeField] private float audioRange = 5f;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool enableGrabVisualFeedback = true;
    [SerializeField] private float grabScaleMultiplier = 1.1f;
    
    // State tracking
    private bool wasGrabbed = false;
    private bool wasSnapped = false;
    private bool wasInMagneticRange = false;
    
    // VR Controller detection
    private OVRInput.Controller activeController = OVRInput.Controller.None;
    
    // Visual feedback
    private Vector3 originalScale;
    private bool isScaled = false;
    
    void Awake()
    {
        InitializeComponents();
        originalScale = transform.localScale;
    }
    
    void Start()
    {
        SetupAudioSpatial();
        SubscribeToEvents();
    }
    
    void Update()
    {
        // Lightweight checks only
        CheckGrabState();
        CheckSnapState();
        
        // Visual feedback (lightweight)
        UpdateVisualFeedback();
        
        // Note: CheckMagneticRange() removed as it was redundant with MVPMagneticCube
    }
    
    private void InitializeComponents()
    {
        // Get components if not assigned
        if (magneticCube == null)
            magneticCube = GetComponent<MagneticCube>();
            
        if (grabbable == null)
            grabbable = GetComponent<Grabbable>();
            
        // Add Grabbable if missing
        if (grabbable == null)
        {
            grabbable = gameObject.AddComponent<Grabbable>();
            Debug.Log($"[MVPVRHandler] Added Grabbable to {gameObject.name}");
        }
        
        // Validate magnetic cube
        if (magneticCube == null)
        {
            Debug.LogError($"[MVPVRHandler] No MVPMagneticCube found on {gameObject.name}!");
        }
    }
    
    private void SetupAudioSpatial()
    {
        if (!enablePositionalAudio) return;
        
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.spatialBlend = 1.0f; // Full 3D
            audioSource.maxDistance = audioRange;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.dopplerLevel = 0.5f;
        }
    }
    
    private void SubscribeToEvents()
    {
        if (magneticCube != null)
        {
            magneticCube.OnCubeSnapped += OnCubeSnapped;
            magneticCube.OnCubeReleased += OnCubeReleased;
            magneticCube.OnCubeGrabStateChanged += OnCubeGrabStateChanged;
        }
        
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised += HandleGrabbableEvent;
        }
    }
    
    #region State Monitoring
    
    private void CheckGrabState()
    {
        if (grabbable == null) return;
        
        bool isCurrentlyGrabbed = grabbable.SelectingPointsCount > 0;
        
        if (isCurrentlyGrabbed != wasGrabbed)
        {
            if (isCurrentlyGrabbed)
            {
                OnGrabBegin();
            }
            else
            {
                OnGrabEnd();
            }
            
            wasGrabbed = isCurrentlyGrabbed;
        }
    }
    
    private void HandleGrabbableEvent(PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case PointerEventType.Select:
                Debug.Log($"[MVPVRHandler] Grabbable Select - {gameObject.name}");
                break;
            case PointerEventType.Unselect:
                Debug.Log($"[MVPVRHandler] Grabbable Unselect - {gameObject.name}");
                break;
            case PointerEventType.Move:
                // Handle movement during grab if needed for additional feedback
                break;
            case PointerEventType.Cancel:
                Debug.Log($"[MVPVRHandler] Grabbable Cancel - {gameObject.name}");
                break;
        }
    }
    
    private void CheckSnapState()
    {
        if (magneticCube == null) return;
        
        bool isCurrentlySnapped = magneticCube.IsSnapped;
        
        if (isCurrentlySnapped && !wasSnapped)
        {
            OnSnapCompleted();
        }
        
        wasSnapped = isCurrentlySnapped;
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnGrabBegin()
    {
        Debug.Log($"[MVPVRHandler] VR Grab Begin - {gameObject.name}");
        
        // Detect which controller is grabbing
        DetectActiveController();
        
        // Notify magnetic cube
        if (magneticCube != null)
        {
            magneticCube.TestGrab();
        }
        
        // Haptic feedback
        if (enableHapticFeedback)
        {
            TriggerHapticFeedback(grabHapticIntensity, grabHapticDuration);
        }
    }
    
    private void OnGrabEnd()
    {
        Debug.Log($"[MVPVRHandler] VR Grab End - {gameObject.name}");
        
        // Notify magnetic cube
        if (magneticCube != null)
        {
            magneticCube.TestRelease();
        }
        
        // Light haptic feedback on release
        if (enableHapticFeedback)
        {
            TriggerHapticFeedback(grabHapticIntensity * 0.5f, grabHapticDuration * 0.5f);
        }
        
        // Clear controller
        activeController = OVRInput.Controller.None;
    }
    
    private void OnCubeSnapped(MagneticCube cube)
    {
        Debug.Log($"[MVPVRHandler] Cube Snapped - {gameObject.name}");
        
        // Strong haptic feedback on snap
        if (enableHapticFeedback)
        {
            TriggerHapticFeedback(snapHapticIntensity, snapHapticDuration);
        }
    }
    
    private void OnCubeReleased(MagneticCube cube)
    {
        Debug.Log($"[MVPVRHandler] Cube Released - {gameObject.name}");
    }
    
    private void OnCubeGrabStateChanged(MagneticCube cube, bool grabbed)
    {
        Debug.Log($"[MVPVRHandler] Grab State Changed - {gameObject.name}: {grabbed}");
    }
    
    private void OnSnapCompleted()
    {
        Debug.Log($"[MVPVRHandler] Snap Completed - {gameObject.name}");
    }
    
    #endregion
    
    #region Controller Detection & Haptics
    
    private void DetectActiveController()
    {
        if (grabbable == null || grabbable.SelectingPointsCount == 0) return;
        
        // The new Grabbable system doesn't expose grabber directly
        // We need to detect based on proximity to controllers
        Vector3 leftControllerPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
        Vector3 rightControllerPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        
        float leftDistance = Vector3.Distance(transform.position, leftControllerPos);
        float rightDistance = Vector3.Distance(transform.position, rightControllerPos);
        
        // Use the closer controller
        if (leftDistance < rightDistance && leftDistance < 0.5f)
        {
            activeController = OVRInput.Controller.LTouch;
        }
        else if (rightDistance < 0.5f)
        {
            activeController = OVRInput.Controller.RTouch;
        }
        else
        {
            // Default to right hand if unclear
            activeController = OVRInput.Controller.RTouch;
        }
        
        Debug.Log($"[MVPVRHandler] Active controller detected: {activeController}");
    }
    
    private void TriggerHapticFeedback(float intensity, float duration)
    {
        if (!enableHapticFeedback || activeController == OVRInput.Controller.None) return;
        
        StartCoroutine(HapticFeedbackCoroutine(intensity, duration));
    }
    
    private System.Collections.IEnumerator HapticFeedbackCoroutine(float intensity, float duration)
    {
        float elapsed = 0f;
        intensity = Mathf.Clamp01(intensity);
        
        while (elapsed < duration && activeController != OVRInput.Controller.None)
        {
            // Apply haptic feedback
            float currentIntensity = intensity * (1f - (elapsed / duration)); // Fade out
            OVRInput.SetControllerVibration(currentIntensity, currentIntensity, activeController);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Stop vibration
        if (activeController != OVRInput.Controller.None)
        {
            OVRInput.SetControllerVibration(0f, 0f, activeController);
        }
    }
    
    #endregion
    
    #region Visual Feedback
    
    private void UpdateVisualFeedback()
    {
        if (!enableGrabVisualFeedback) return;
        
        bool shouldBeScaled = grabbable != null && grabbable.SelectingPointsCount > 0;
        
        if (shouldBeScaled && !isScaled)
        {
            // Scale up when grabbed
            transform.localScale = originalScale * grabScaleMultiplier;
            isScaled = true;
        }
        else if (!shouldBeScaled && isScaled)
        {
            // Scale back to normal when released
            transform.localScale = originalScale;
            isScaled = false;
        }
    }
    
    #endregion
    
    #region Public Interface
    
    /// <summary>
    /// Force grab for testing
    /// </summary>
    [ContextMenu("Test VR Grab")]
    public void TestVRGrab()
    {
        if (magneticCube != null)
        {
            magneticCube.TestGrab();
            OnGrabBegin();
        }
    }
    
    /// <summary>
    /// Force release for testing
    /// </summary>
    [ContextMenu("Test VR Release")]
    public void TestVRRelease()
    {
        if (magneticCube != null)
        {
            magneticCube.TestRelease();
            OnGrabEnd();
        }
    }
    
    /// <summary>
    /// Test haptic feedback
    /// </summary>
    [ContextMenu("Test Haptic Feedback")]
    public void TestHapticFeedback()
    {
        DetectActiveController();
        TriggerHapticFeedback(0.5f, 0.3f);
    }
    
    /// <summary>
    /// Check if currently being grabbed by VR
    /// </summary>
    public bool IsVRGrabbed => grabbable != null && grabbable.SelectingPointsCount > 0;
    
    /// <summary>
    /// Check if currently snapped to grid
    /// </summary>
    public bool IsSnapped => magneticCube != null && magneticCube.IsSnapped;
    
    /// <summary>
    /// Get the magnetic cube reference
    /// </summary>
    public MagneticCube MagneticCube => magneticCube;
    
    /// <summary>
    /// Get the Grabbable reference
    /// </summary>
    public Grabbable Grabbable => grabbable;
    
    #endregion
    
    #region Configuration
    
    /// <summary>
    /// Enable/disable haptic feedback
    /// </summary>
    public void SetHapticFeedbackEnabled(bool enabled)
    {
        enableHapticFeedback = enabled;
    }
    
    /// <summary>
    /// Set haptic intensity for different events
    /// </summary>
    public void SetHapticIntensities(float snap, float grab)
    {
        snapHapticIntensity = Mathf.Clamp01(snap);
        grabHapticIntensity = Mathf.Clamp01(grab);
    }
    
    /// <summary>
    /// Enable/disable visual feedback
    /// </summary>
    public void SetVisualFeedbackEnabled(bool enabled)
    {
        enableGrabVisualFeedback = enabled;
        
        if (!enabled && isScaled)
        {
            transform.localScale = originalScale;
            isScaled = false;
        }
    }
    
    #endregion
    
    #region Cleanup
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (magneticCube != null)
        {
            magneticCube.OnCubeSnapped -= OnCubeSnapped;
            magneticCube.OnCubeReleased -= OnCubeReleased;
            magneticCube.OnCubeGrabStateChanged -= OnCubeGrabStateChanged;
        }
        
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= HandleGrabbableEvent;
        }
        
        // Stop any ongoing haptic feedback
        if (activeController != OVRInput.Controller.None)
        {
            OVRInput.SetControllerVibration(0f, 0f, activeController);
        }
    }
    
    #endregion
}