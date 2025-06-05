using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PuzzleManager_v1 : MonoBehaviour
{
    [Header("Setup Configuration")]
    [SerializeField] private GameObject magneticCubePrefab;
    [SerializeField] private int numberOfCubes = 5;
    [SerializeField] private Transform spawnArea;
    [SerializeField] private float spawnRadius = 2f;
    [SerializeField] private float spawnHeight = 1f;
    
    [Header("Grid Configuration")]
    [SerializeField] private Vector3Int gridDimensions = new Vector3Int(5, 5, 5);
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Transform gridCenter;
    
    [Header("Auto Setup")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private bool createGridOnStart = true;
    [SerializeField] private bool spawnCubesOnStart = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;
    [SerializeField] private bool showValidPositions = false;
    
    // System References
    private CubeGrid_v1 _puzzleCubeGrid;
    private List<MagneticCube> spawnedCubes = new List<MagneticCube>();
    
    // Game State
    public int CubeCount => spawnedCubes.Count;
    public int SnappedCubeCount => spawnedCubes.Count(cube => cube.IsSnapped);
    public bool IsSetupComplete => _puzzleCubeGrid != null && spawnedCubes.Count > 0;
    public bool IsStructureConnected => _puzzleCubeGrid != null && _puzzleCubeGrid.IsConnectedStructure();
    
    // Events
    public System.Action<MagneticCube> OnCubeSpawned;
    public System.Action<MagneticCube> OnCubeSnapped;
    public System.Action<MagneticCube> OnCubeReleased;
    public System.Action<bool> OnStructureConnectivityChanged;
    public System.Action<int> OnSnappedCubeCountChanged;
    
    // Properties
    public CubeGrid_v1 PuzzleCubeGrid => _puzzleCubeGrid;
    public List<MagneticCube> SpawnedCubes => new List<MagneticCube>(spawnedCubes);
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupPuzzleSystem();
        }
    }
    
    [ContextMenu("Setup Puzzle System")]
    public void SetupPuzzleSystem()
    {
        LogDebug("Setting up MVP Puzzle System...");
        
        if (createGridOnStart)
        {
            CreateGrid();
        }
        
        if (spawnCubesOnStart)
        {
            SpawnCubes();
        }
        
        LogDebug("MVP Puzzle System setup complete!");
    }
    
    #region Grid Creation
    
    private void CreateGrid()
    {
        // Check if grid already exists
        _puzzleCubeGrid = FindObjectOfType<CubeGrid_v1>();
        
        if (_puzzleCubeGrid == null)
        {
            LogDebug("Creating new grid...");
            
            GameObject gridObj = new GameObject("MVPGrid");
            gridObj.transform.SetParent(transform);
            gridObj.transform.localPosition = Vector3.zero;
            
            _puzzleCubeGrid = gridObj.AddComponent<CubeGrid_v1>();
            
            LogDebug($"Grid created with dimensions: {gridDimensions}");
        }
        else
        {
            LogDebug("Using existing grid in scene");
        }
        
        // Configure the grid with our settings
        if (_puzzleCubeGrid != null)
        {
            // Set position first
            if (gridCenter != null)
            {
                _puzzleCubeGrid.transform.position = gridCenter.position;
            }
            
            // Configure grid parameters
            _puzzleCubeGrid.ConfigureGrid(gridDimensions, cellSize, gridCenter);
            
            LogDebug($"Grid configured: Dimensions={gridDimensions}, CellSize={cellSize}, Center={gridCenter?.name ?? "null"}");
        }
        
        // Subscribe to grid events
        if (_puzzleCubeGrid != null)
        {
            _puzzleCubeGrid.OnCellOccupancyChanged += OnGridCellChanged;
            _puzzleCubeGrid.OnConnectivityChanged += OnGridConnectivityChanged;
            _puzzleCubeGrid.OnCubeCountChanged += OnGridCubeCountChanged;
            
            // Configure visual settings
            _puzzleCubeGrid.SetValidPositionsVisible(showValidPositions);
        }
    }
    
    #endregion
    
    #region Cube Spawning
    
    private void SpawnCubes()
    {
        if (magneticCubePrefab == null)
        {
            Debug.LogError("[MVPPuzzleManager] No magnetic cube prefab assigned!");
            return;
        }
        
        LogDebug($"Spawning {numberOfCubes} magnetic cubes...");
        
        Vector3 spawnCenter = GetSpawnCenter();
        
        for (int i = 0; i < numberOfCubes; i++)
        {
            SpawnSingleCube(spawnCenter, i);
        }
        
        LogDebug($"Spawned {spawnedCubes.Count} cubes successfully");
    }
    
    private Vector3 GetSpawnCenter()
    {
        if (spawnArea != null)
        {
            return spawnArea.position;
        }
        
        if (gridCenter != null)
        {
            return gridCenter.position + Vector3.up * spawnHeight;
        }
        
        return transform.position + Vector3.up * spawnHeight;
    }
    
    private void SpawnSingleCube(Vector3 spawnCenter, int index)
    {
        // Generate random position in spawn area
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
        randomOffset.y = Mathf.Abs(randomOffset.y) + spawnHeight; // Keep above ground
        Vector3 spawnPos = spawnCenter + randomOffset;
        
        // Create cube
        GameObject cubeObj = Instantiate(magneticCubePrefab, spawnPos, Random.rotation);
        cubeObj.name = $"MagneticCube_{index:00}";
        
        // Get component and configure
        MagneticCube magneticCube = cubeObj.GetComponent<MagneticCube>();
        if (magneticCube != null)
        {
            ConfigureCube(magneticCube, index);
            spawnedCubes.Add(magneticCube);
            
            OnCubeSpawned?.Invoke(magneticCube);
        }
        else
        {
            Debug.LogError($"[MVPPuzzleManager] Prefab {magneticCubePrefab.name} does not have MVPMagneticCube component!");
            DestroyImmediate(cubeObj);
        }
    }
    
    private void ConfigureCube(MagneticCube cube, int index)
    {
        // Subscribe to cube events
        cube.OnCubeSnapped += OnCubeSnappedHandler;
        cube.OnCubeReleased += OnCubeReleasedHandler;
        cube.OnCubeGrabStateChanged += OnCubeGrabStateChangedHandler;
        
        LogDebug($"Configured cube {index}: {cube.gameObject.name}");
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnGridCellChanged(Vector3Int gridPos, bool occupied, MagneticCube cube)
    {
        LogDebug($"Grid cell {gridPos} {(occupied ? "occupied" : "freed")} by {cube.gameObject.name}");
    }
    
    private void OnGridConnectivityChanged(bool isConnected)
    {
        LogDebug($"Grid connectivity changed: {isConnected}");
        OnStructureConnectivityChanged?.Invoke(isConnected);
    }
    
    private void OnGridCubeCountChanged(int cubeCount)
    {
        LogDebug($"Grid cube count changed: {cubeCount}");
        OnSnappedCubeCountChanged?.Invoke(cubeCount);
    }
    
    private void OnCubeSnappedHandler(MagneticCube cube)
    {
        LogDebug($"Cube snapped: {cube.gameObject.name} at {cube.GridPosition}");
        OnCubeSnapped?.Invoke(cube);
    }
    
    private void OnCubeReleasedHandler(MagneticCube cube)
    {
        LogDebug($"Cube released: {cube.gameObject.name}");
        OnCubeReleased?.Invoke(cube);
    }
    
    private void OnCubeGrabStateChangedHandler(MagneticCube cube, bool grabbed)
    {
        LogDebug($"Cube {cube.gameObject.name} {(grabbed ? "grabbed" : "released")}");
    }
    
    #endregion
    
    #region Game Management
    
    /// <summary>
    /// Reset all cubes to spawn area
    /// </summary>
    [ContextMenu("Reset All Cubes")]
    public void ResetAllCubes()
    {
        LogDebug("Resetting all cubes...");
        
        Vector3 spawnCenter = GetSpawnCenter();
        
        foreach (var cube in spawnedCubes)
        {
            if (cube != null)
            {
                // Force release from grid
                cube.ForceRelease();
                
                // Reposition randomly
                Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
                randomOffset.y = Mathf.Abs(randomOffset.y) + spawnHeight;
                cube.transform.position = spawnCenter + randomOffset;
                cube.transform.rotation = Random.rotation;
                
                // Reset physics
                Rigidbody rb = cube.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = false;
                }
            }
        }
        
        LogDebug("All cubes reset");
    }
    
    /// <summary>
    /// Clear all cubes from grid
    /// </summary>
    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        if (_puzzleCubeGrid != null)
        {
            _puzzleCubeGrid.ClearGrid();
        }
        
        LogDebug("Grid cleared");
    }
    
    /// <summary>
    /// Spawn additional cube
    /// </summary>
    [ContextMenu("Spawn Additional Cube")]
    public void SpawnAdditionalCube()
    {
        Vector3 spawnCenter = GetSpawnCenter();
        SpawnSingleCube(spawnCenter, spawnedCubes.Count);
        LogDebug("Additional cube spawned");
    }
    
    /// <summary>
    /// Remove a specific cube
    /// </summary>
    public void RemoveCube(MagneticCube cube)
    {
        if (spawnedCubes.Contains(cube))
        {
            // Unsubscribe from events
            cube.OnCubeSnapped -= OnCubeSnappedHandler;
            cube.OnCubeReleased -= OnCubeReleasedHandler;
            cube.OnCubeGrabStateChanged -= OnCubeGrabStateChangedHandler;
            
            spawnedCubes.Remove(cube);
            DestroyImmediate(cube.gameObject);
            
            LogDebug($"Cube removed: {cube.gameObject.name}");
        }
    }
    
    /// <summary>
    /// Validate the current puzzle state
    /// </summary>
    [ContextMenu("Validate Puzzle State")]
    public void ValidatePuzzleState()
    {
        if (_puzzleCubeGrid != null)
        {
            _puzzleCubeGrid.ValidateStructure();
        }
        
        int totalCubes = spawnedCubes.Count;
        int snappedCubes = SnappedCubeCount;
        bool isConnected = IsStructureConnected;
        
        LogDebug($"Puzzle State: {snappedCubes}/{totalCubes} cubes snapped, Connected: {isConnected}");
    }
    
    #endregion
    
    #region VR Integration Helpers
    
    /// <summary>
    /// Handle VR grab events from external systems
    /// </summary>
    public void OnVRCubeGrabbed(MagneticCube cube)
    {
        if (cube != null && spawnedCubes.Contains(cube))
        {
            cube.TestGrab();
        }
    }
    
    /// <summary>
    /// Handle VR release events from external systems
    /// </summary>
    public void OnVRCubeReleased(MagneticCube cube)
    {
        if (cube != null && spawnedCubes.Contains(cube))
        {
            cube.TestRelease();
        }
    }
    
    #endregion
    
    #region Configuration
    
    /// <summary>
    /// Toggle visual indicators
    /// </summary>
    public void SetValidPositionsVisible(bool visible)
    {
        showValidPositions = visible;
        
        if (_puzzleCubeGrid != null)
        {
            _puzzleCubeGrid.SetValidPositionsVisible(visible);
        }
    }
    
    /// <summary>
    /// Toggle grid lines
    /// </summary>
    public void SetGridLinesVisible(bool visible)
    {
        if (_puzzleCubeGrid != null)
        {
            _puzzleCubeGrid.SetGridLinesVisible(visible);
        }
    }
    
    /// <summary>
    /// Update grid configuration at runtime
    /// </summary>
    public void UpdateGridConfiguration(Vector3Int newDimensions, float newCellSize)
    {
        gridDimensions = newDimensions;
        cellSize = newCellSize;
        
        if (_puzzleCubeGrid != null)
        {
            _puzzleCubeGrid.ConfigureGrid(gridDimensions, cellSize, gridCenter);
            LogDebug($"Grid configuration updated: {gridDimensions}, cell size: {cellSize}");
        }
    }
    
    /// <summary>
    /// Update spawn configuration
    /// </summary>
    public void SetSpawnConfiguration(int cubeCount, float radius, float height)
    {
        numberOfCubes = cubeCount;
        spawnRadius = radius;
        spawnHeight = height;
        
        LogDebug($"Spawn configuration updated: {cubeCount} cubes, radius: {radius}, height: {height}");
    }
    
    /// <summary>
    /// Recreate grid with new settings
    /// </summary>
    [ContextMenu("Recreate Grid")]
    public void RecreateGrid()
    {
        if (_puzzleCubeGrid != null)
        {
            // Clear existing grid
            _puzzleCubeGrid.ClearGrid();
            
            // Unsubscribe from events
            _puzzleCubeGrid.OnCellOccupancyChanged -= OnGridCellChanged;
            _puzzleCubeGrid.OnConnectivityChanged -= OnGridConnectivityChanged;
            _puzzleCubeGrid.OnCubeCountChanged -= OnGridCubeCountChanged;
            
            // Destroy old grid
            DestroyImmediate(_puzzleCubeGrid.gameObject);
            _puzzleCubeGrid = null;
        }
        
        // Create new grid
        CreateGrid();
        LogDebug("Grid recreated with current settings");
    }
    
    #endregion
    
    #region Debug & Utilities
    
    private void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[MVPPuzzleManager] {message}");
        }
    }
    
    /// <summary>
    /// Validate setup configuration
    /// </summary>
    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        LogDebug("Validating setup...");
        
        bool isValid = true;
        
        if (magneticCubePrefab == null)
        {
            Debug.LogError("[MVPPuzzleManager] Magnetic cube prefab not assigned!");
            isValid = false;
        }
        else
        {
            MagneticCube cubeComponent = magneticCubePrefab.GetComponent<MagneticCube>();
            if (cubeComponent == null)
            {
                Debug.LogError("[MVPPuzzleManager] Prefab does not have MVPMagneticCube component!");
                isValid = false;
            }
        }
        
        if (numberOfCubes <= 0)
        {
            Debug.LogWarning("[MVPPuzzleManager] Number of cubes should be greater than 0");
        }
        
        if (spawnRadius <= 0)
        {
            Debug.LogWarning("[MVPPuzzleManager] Spawn radius should be greater than 0");
        }
        
        LogDebug(isValid ? "Setup validation passed!" : "Setup validation failed - check errors above");
    }
    
    /// <summary>
    /// Get comprehensive system status
    /// </summary>
    public string GetSystemStatus()
    {
        return $"MVP Puzzle System Status:\n" +
               $"- Setup Complete: {IsSetupComplete}\n" +
               $"- Total Cubes: {CubeCount}\n" +
               $"- Snapped Cubes: {SnappedCubeCount}\n" +
               $"- Structure Connected: {IsStructureConnected}\n" +
               $"- Grid Dimensions: {(_puzzleCubeGrid != null ? _puzzleCubeGrid.Dimensions.ToString() : gridDimensions.ToString())}\n" +
               $"- Grid Cell Size: {(_puzzleCubeGrid != null ? _puzzleCubeGrid.CellSize : cellSize)}\n" +
               $"- Grid Occupied Cells: {(_puzzleCubeGrid != null ? _puzzleCubeGrid.OccupiedCount : 0)}\n" +
               $"- Expected vs Actual Grid Match: {ValidateGridConfiguration()}";
    }
    
    /// <summary>
    /// Verify that grid configuration matches puzzle manager settings
    /// </summary>
    [ContextMenu("Validate Grid Configuration")]
    public bool ValidateGridConfiguration()
    {
        if (_puzzleCubeGrid == null)
        {
            LogDebug("❌ No grid found - cannot validate configuration");
            return false;
        }
        
        bool dimensionsMatch = _puzzleCubeGrid.Dimensions == gridDimensions;
        bool cellSizeMatch = Mathf.Approximately(_puzzleCubeGrid.CellSize, cellSize);
        
        LogDebug($"Grid Configuration Validation:");
        LogDebug($"  Expected Dimensions: {gridDimensions} | Actual: {_puzzleCubeGrid.Dimensions} | Match: {dimensionsMatch}");
        LogDebug($"  Expected Cell Size: {cellSize} | Actual: {_puzzleCubeGrid.CellSize} | Match: {cellSizeMatch}");
        
        if (dimensionsMatch && cellSizeMatch)
        {
            LogDebug("✅ Grid configuration matches puzzle manager settings");
            return true;
        }
        else
        {
            LogDebug("❌ Grid configuration mismatch detected!");
            LogDebug("   Use 'Recreate Grid' context menu to fix this.");
            return false;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw spawn area
        Vector3 spawnCenter = GetSpawnCenter();
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(spawnCenter, spawnRadius);
        
        // Draw grid center
        if (gridCenter != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(gridCenter.position, Vector3.one * 0.5f);
        }
        
        // Draw grid bounds
        if (_puzzleCubeGrid != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 gridSize = new Vector3(
                gridDimensions.x * cellSize,
                gridDimensions.y * cellSize,
                gridDimensions.z * cellSize
            );
            Gizmos.DrawWireCube(_puzzleCubeGrid.transform.position, gridSize);
        }
    }
    
    #endregion
    
    #region Cleanup
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (_puzzleCubeGrid != null)
        {
            _puzzleCubeGrid.OnCellOccupancyChanged -= OnGridCellChanged;
            _puzzleCubeGrid.OnConnectivityChanged -= OnGridConnectivityChanged;
            _puzzleCubeGrid.OnCubeCountChanged -= OnGridCubeCountChanged;
        }
        
        foreach (var cube in spawnedCubes)
        {
            if (cube != null)
            {
                cube.OnCubeSnapped -= OnCubeSnappedHandler;
                cube.OnCubeReleased -= OnCubeReleasedHandler;
                cube.OnCubeGrabStateChanged -= OnCubeGrabStateChangedHandler;
            }
        }
    }
    
    #endregion
}