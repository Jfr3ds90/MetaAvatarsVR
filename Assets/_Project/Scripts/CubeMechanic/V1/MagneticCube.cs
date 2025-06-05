using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class MagneticCube : MonoBehaviour
{
    [Header("Magnetic Properties")]
    [SerializeField] private float magneticRange = 0.4f;
    [SerializeField] private float snapTolerance = 0.15f;
    [SerializeField] private float snapSpeed = 10f;
    
    [Header("Visual Feedback")]
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private Material originalMaterial;
    [SerializeField] private GameObject snapPreviewPrefab;
    
    [Header("Audio")]
    [SerializeField] private AudioClip snapSound;
    [SerializeField] private AudioClip magneticHumSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem snapEffect;
    
    // Components
    private Rigidbody rb;
    private Renderer cubeRenderer;
    private AudioSource audioSource;
    private Collider cubeCollider;
    
    // Grid System
    private CubeGrid_v1 targetGrid;
    
    // State
    public bool IsSnapped { get; private set; } = false;
    public bool IsBeingGrabbed { get; private set; } = false;
    public Vector3Int GridPosition { get; private set; } = new Vector3Int(-1, -1, -1);
    
    // Visual Elements
    private GameObject snapPreviewInstance;
    private bool isInMagneticRange = false;
    private Vector3Int lastValidPosition = new Vector3Int(-1, -1, -1);
    
    // Snapping Animation
    private bool isSnapping = false;
    private Vector3 snapTargetPosition;
    private Vector3 snapStartPosition;
    private float snapProgress = 0f;
    
    private Vector3 lastCheckedPosition;      // Para tracking de movimiento
    private float positionCheckThreshold = 0.01f;  // Umbral de movimiento
    private float lastMagneticCheck = 0f;     // Último check magnético
    private float magneticCheckInterval = 0.05f;   // Intervalo entre checks
    private float lastVisualUpdate = 0f;  
    
    // VR Integration
    private Oculus.Interaction.Grabbable grabbable;
    
    // Events
    public System.Action<MagneticCube> OnCubeSnapped;
    public System.Action<MagneticCube> OnCubeReleased;
    public System.Action<MagneticCube, bool> OnCubeGrabStateChanged;
    
    void Start()
    {
        InitializeComponents();
        SetupVRIntegration();
    }
    
    void Update()
    {
        // Only check magnetic range when not grabbed and not snapped
        if (!IsBeingGrabbed && !IsSnapped)
        {
            // PERFORMANCE OPTIMIZATION: Only check if moved enough or enough time passed
            Vector3 currentPos = transform.position;
            float timeSinceLastCheck = Time.time - lastMagneticCheck;
            
            bool movedEnough = Vector3.Distance(currentPos, lastCheckedPosition) > positionCheckThreshold;
            bool timeForCheck = timeSinceLastCheck > magneticCheckInterval;
            
            if (movedEnough || timeForCheck)
            {
                CheckMagneticRange();
                lastCheckedPosition = currentPos;
                lastMagneticCheck = Time.time;
            }
        }
        
        // Handle snapping animation (lightweight)
        if (isSnapping)
        {
            UpdateSnapping();
        }
        
        // Check grab state via Grabbable SelectingPointsCount (lightweight)
        if (grabbable != null && Time.frameCount % 3 == 0) // Check every 3rd frame
        {
            CheckGrabState();
        }
    }
    
    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody>();
        cubeRenderer = GetComponent<Renderer>();
        cubeCollider = GetComponent<Collider>();
        
        // Store original material
        if (cubeRenderer != null && originalMaterial == null)
        {
            originalMaterial = cubeRenderer.material;
        }
        
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 1.0f; // 3D audio
        audioSource.playOnAwake = false;
        
        // Find grid in scene
        targetGrid = FindObjectOfType<CubeGrid_v1>();
        if (targetGrid == null)
        {
            Debug.LogWarning($"[{gameObject.name}] No MVPGrid found in scene!");
        }
    }
    
    private void SetupVRIntegration()
    {
        grabbable = GetComponent<Oculus.Interaction.Grabbable>();
        if (grabbable == null)
        {
            grabbable = gameObject.AddComponent<Oculus.Interaction.Grabbable>();
        }
        
        // Subscribe to grabbable events
        grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }
    
    private void CheckGrabState()
    {
        if (grabbable == null) return;
        
        bool currentlyGrabbed = grabbable.SelectingPointsCount > 0;
        
        if (currentlyGrabbed != IsBeingGrabbed)
        {
            SetGrabState(currentlyGrabbed);
        }
    }
    
    private void HandlePointerEvent(Oculus.Interaction.PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case Oculus.Interaction.PointerEventType.Select:
                Debug.Log($"[{gameObject.name}] Pointer Select Event");
                break;
            case Oculus.Interaction.PointerEventType.Unselect:
                Debug.Log($"[{gameObject.name}] Pointer Unselect Event");
                break;
            case Oculus.Interaction.PointerEventType.Move:
                // Handle movement during grab if needed
                break;
            case Oculus.Interaction.PointerEventType.Cancel:
                Debug.Log($"[{gameObject.name}] Pointer Cancel Event");
                break;
        }
    }
    
    private void SetGrabState(bool grabbed)
    {
        IsBeingGrabbed = grabbed;
        
        if (grabbed)
        {
            OnGrabBegin();
        }
        else
        {
            OnGrabEnd();
        }
        
        OnCubeGrabStateChanged?.Invoke(this, grabbed);
    }
    
    private void OnGrabBegin()
    {
        Debug.Log($"[{gameObject.name}] Grab Begin");
        
        // Release from grid if snapped
        if (IsSnapped)
        {
            ReleaseFromGrid();
        }
        
        // Enable physics
        rb.isKinematic = false;
        
        // Stop magnetic hum
        StopMagneticHum();
    }
    
    private void OnGrabEnd()
    {
        Debug.Log($"[{gameObject.name}] Grab End");
        
        // Physics will be handled by magnetic system or grid snapping
    }
    
    #region Magnetic Detection & Snapping
    
    private void CheckMagneticRange()
    {
        if (targetGrid == null) return;
        
        // Cache position to avoid multiple transform.position calls
        Vector3 currentPosition = transform.position;
        
        // OPTIMIZATION: Quick distance check before expensive grid calculations
        Vector3 gridCenterWorld = targetGrid.gridCenter.position;
        Vector3Int dimensions = targetGrid.Dimensions;
        float cellSize = targetGrid.CellSize;
        float maxGridDistance = Mathf.Max(dimensions.x, dimensions.y, dimensions.z) * cellSize;
        
        if (Vector3.Distance(currentPosition, gridCenterWorld) > maxGridDistance + magneticRange)
        {
            // Too far from grid, early exit
            if (isInMagneticRange)
            {
                OnExitMagneticRange();
                StopMagneticHum();
            }
            return;
        }
        
        // Get nearest grid position (optimized method)
        Vector3Int nearestGridPos = targetGrid.GetNearestGridPosition(currentPosition);
        
        // Quick validation
        if (!targetGrid.IsValidPosition(nearestGridPos))
        {
            if (isInMagneticRange)
            {
                OnExitMagneticRange();
                StopMagneticHum();
            }
            return;
        }
        
        // Calculate distance once
        Vector3 gridWorldPos = targetGrid.GridToWorldPosition(nearestGridPos);
        float distanceToGrid = Vector3.Distance(currentPosition, gridWorldPos);
        
        bool wasInRange = isInMagneticRange;
        isInMagneticRange = distanceToGrid <= magneticRange;
        
        if (isInMagneticRange)
        {
            // OPTIMIZATION: Only check placement validity when in range
            bool isValidPlacement = targetGrid.IsValidPlacement(nearestGridPos);
            
            if (isValidPlacement)
            {
                // Entering magnetic range or position changed
                if (!wasInRange || nearestGridPos != lastValidPosition)
                {
                    OnEnterMagneticRange(nearestGridPos);
                    PlayMagneticHum();
                }
                
                lastValidPosition = nearestGridPos;
                
                // Check for snap
                if (distanceToGrid <= snapTolerance && !isSnapping)
                {
                    StartSnapping(nearestGridPos);
                }
            }
            else if (wasInRange)
            {
                // Invalid placement but still in range
                OnExitMagneticRange();
                StopMagneticHum();
            }
        }
        else if (wasInRange)
        {
            OnExitMagneticRange();
            StopMagneticHum();
        }
    }
    
    private void OnEnterMagneticRange(Vector3Int gridPos)
    {
        ShowSnapPreview(gridPos);
        
        // Visual highlight
        if (highlightMaterial != null && cubeRenderer != null)
        {
            cubeRenderer.material = highlightMaterial;
        }
    }
    
    private void OnExitMagneticRange()
    {
        HideSnapPreview();
        
        // Restore original material
        if (originalMaterial != null && cubeRenderer != null)
        {
            cubeRenderer.material = originalMaterial;
        }
        
        lastValidPosition = new Vector3Int(-1, -1, -1);
    }
    
    private void ShowSnapPreview(Vector3Int gridPos)
    {
        if (snapPreviewPrefab == null) return;
        
        if (snapPreviewInstance == null)
        {
            snapPreviewInstance = Instantiate(snapPreviewPrefab);
            snapPreviewInstance.name = $"{gameObject.name}_SnapPreview";
        }
        
        snapPreviewInstance.transform.position = targetGrid.GridToWorldPosition(gridPos);
        snapPreviewInstance.SetActive(true);
    }
    
    private void HideSnapPreview()
    {
        if (snapPreviewInstance != null)
        {
            snapPreviewInstance.SetActive(false);
        }
    }
    
    private void StartSnapping(Vector3Int gridPos)
    {
        if (isSnapping || IsSnapped) return;
        
        // Validate placement
        if (!targetGrid.IsValidPlacement(gridPos)) return;
        
        Debug.Log($"[{gameObject.name}] Starting snap to {gridPos}");
        
        // Occupy grid position
        if (!targetGrid.OccupyPosition(gridPos, this))
        {
            Debug.LogWarning($"[{gameObject.name}] Failed to occupy grid position {gridPos}");
            return;
        }
        
        // Update state
        IsSnapped = true;
        GridPosition = gridPos;
        
        // Start snapping animation
        isSnapping = true;
        snapProgress = 0f;
        snapStartPosition = transform.position;
        snapTargetPosition = targetGrid.GridToWorldPosition(gridPos);
        
        // Disable physics during snap
        rb.isKinematic = true;
        
        HideSnapPreview();
        PlaySnapEffects();
        
        OnCubeSnapped?.Invoke(this);
    }
    
    private void UpdateSnapping()
    {
        if (!isSnapping) return;
        
        snapProgress += Time.deltaTime * snapSpeed;
        snapProgress = Mathf.Clamp01(snapProgress);
        
        // Smooth interpolation
        float easedProgress = Mathf.SmoothStep(0f, 1f, snapProgress);
        transform.position = Vector3.Lerp(snapStartPosition, snapTargetPosition, easedProgress);
        
        if (snapProgress >= 1.0f)
        {
            CompleteSnap();
        }
    }
    
    private void CompleteSnap()
    {
        isSnapping = false;
        transform.position = snapTargetPosition;
        transform.rotation = Quaternion.identity; // Align to grid
        
        Debug.Log($"[{gameObject.name}] Snap completed at {GridPosition}");
    }
    
    #endregion
    
    #region Grid Management
    
    public void ReleaseFromGrid()
    {
        if (!IsSnapped || targetGrid == null) return;
        
        Debug.Log($"[{gameObject.name}] Releasing from grid position {GridPosition}");
        
        // Free grid position
        targetGrid.FreePosition(GridPosition);
        
        // Update state
        IsSnapped = false;
        GridPosition = new Vector3Int(-1, -1, -1);
        
        // Enable physics
        rb.isKinematic = false;
        
        OnCubeReleased?.Invoke(this);
    }
    
    public void ForceRelease()
    {
        if (IsSnapped)
        {
            ReleaseFromGrid();
        }
    }
    
    #endregion
    
    #region Audio & Effects
    
    private void PlaySnapEffects()
    {
        // Particle effect
        if (snapEffect != null)
        {
            snapEffect.Play();
        }
        
        // Audio
        PlaySnapSound();
    }
    
    private void PlaySnapSound()
    {
        if (snapSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(snapSound);
        }
    }
    
    private void PlayMagneticHum()
    {
        if (magneticHumSound != null && audioSource != null && !audioSource.isPlaying)
        {
            audioSource.clip = magneticHumSound;
            audioSource.loop = true;
            audioSource.volume = 0.3f;
            audioSource.Play();
        }
    }
    
    private void StopMagneticHum()
    {
        if (audioSource != null && audioSource.clip == magneticHumSound)
        {
            audioSource.Stop();
        }
    }
    
    #endregion
    
    #region Public Interface
    
    /// <summary>
    /// Manual grab control for testing
    /// </summary>
    public void TestGrab()
    {
        SetGrabState(true);
    }
    
    /// <summary>
    /// Manual release control for testing
    /// </summary>
    public void TestRelease()
    {
        SetGrabState(false);
    }
    
    /// <summary>
    /// Check if this cube can be grabbed
    /// </summary>
    public bool CanBeGrabbed()
    {
        return !IsSnapped || IsBeingGrabbed;
    }
    
    /// <summary>
    /// Get the current world position of this cube's grid cell (if snapped)
    /// </summary>
    public Vector3 GetGridWorldPosition()
    {
        if (!IsSnapped || targetGrid == null) return Vector3.zero;
        return targetGrid.GridToWorldPosition(GridPosition);
    }
    
    #endregion
    
    #region Cleanup
    
    void OnDestroy()
    {
        if (snapPreviewInstance != null)
        {
            DestroyImmediate(snapPreviewInstance);
        }
        
        if (IsSnapped && targetGrid != null)
        {
            targetGrid.FreePosition(GridPosition);
        }
        
        // Unsubscribe from grabbable events
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }
    }
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmosSelected()
    {
        // Magnetic range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, magneticRange);
        
        // Snap tolerance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, snapTolerance);
        
        // Grid position if snapped
        if (IsSnapped && targetGrid != null)
        {
            Gizmos.color = Color.green;
            Vector3 gridWorldPos = targetGrid.GridToWorldPosition(GridPosition);
            Gizmos.DrawLine(transform.position, gridWorldPos);
            Gizmos.DrawWireCube(gridWorldPos, Vector3.one * targetGrid.CellSize);
        }
        
        // Snap target during animation
        if (isSnapping)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(snapTargetPosition, Vector3.one * 0.1f);
            Gizmos.DrawLine(transform.position, snapTargetPosition);
        }
    }
    
    #endregion
}