using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CubeGrid_v1 : MonoBehaviour
{
    [Header("Grid Configuration")]
    [SerializeField] private Vector3Int dimensions = new Vector3Int(5, 5, 5);
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] public Transform gridCenter;
    
    [Header("Visual Settings")]
    [SerializeField] private bool showGridLines = true;
    [SerializeField] private Material gridLineMaterial;
    [SerializeField] private Color gridLineColor = Color.white;
    [SerializeField] private float lineWidth = 0.02f;
    [SerializeField] private float gridOpacity = 0.3f;
    
    [Header("Valid Placement Indicators")]
    [SerializeField] private bool showValidPositions = false;
    [SerializeField] private GameObject positionIndicatorPrefab;
    [SerializeField] private Material validPositionMaterial;
    [SerializeField] private Material connectedPositionMaterial;
    
    // Grid state
    private Dictionary<Vector3Int, MagneticCube> occupiedCells = new Dictionary<Vector3Int, MagneticCube>();
    
    // Visual elements
    private LineRenderer[] gridLines;
    private List<GameObject> positionIndicators = new List<GameObject>();
    private bool needsIndicatorUpdate = true; // Flag to control when to update indicators
    
    // Events
    public System.Action<Vector3Int, bool, MagneticCube> OnCellOccupancyChanged;
    public System.Action<bool> OnConnectivityChanged;
    public System.Action<int> OnCubeCountChanged;
    
    // Properties
    public Vector3Int Dimensions => dimensions;
    public float CellSize => cellSize;
    
    #region Configuration Methods
    
    /// <summary>
    /// Configure grid dimensions and cell size programmatically
    /// </summary>
    public void ConfigureGrid(Vector3Int newDimensions, float newCellSize, Transform centerTransform = null)
    {
        dimensions = newDimensions;
        cellSize = newCellSize;
        
        if (centerTransform != null)
        {
            gridCenter = centerTransform;
        }
        
        Debug.Log($"[MVPGrid] Configured with dimensions: {dimensions}, cell size: {cellSize}");
        
        // Recreate visualization if already started
        if (Application.isPlaying && gridLines != null)
        {
            DestroyGridVisualization();
            CreateGridVisualization();
        }
    }
    
    /// <summary>
    /// Set grid dimensions
    /// </summary>
    public void SetDimensions(Vector3Int newDimensions)
    {
        dimensions = newDimensions;
        Debug.Log($"[MVPGrid] Dimensions set to: {dimensions}");
    }
    
    /// <summary>
    /// Set cell size
    /// </summary>
    public void SetCellSize(float newCellSize)
    {
        cellSize = newCellSize;
        Debug.Log($"[MVPGrid] Cell size set to: {cellSize}");
    }
    
    /// <summary>
    /// Set grid center transform
    /// </summary>
    public void SetGridCenter(Transform centerTransform)
    {
        gridCenter = centerTransform;
        Debug.Log($"[MVPGrid] Grid center set to: {(centerTransform != null ? centerTransform.name : "null")}");
    }
    
    /// <summary>
    /// Update visual settings
    /// </summary>
    public void ConfigureVisuals(bool showLines, Material lineMaterial = null, Color lineColor = default, float lineOpacity = 0.3f)
    {
        showGridLines = showLines;
        
        if (lineMaterial != null)
        {
            gridLineMaterial = lineMaterial;
        }
        
        if (lineColor != default)
        {
            gridLineColor = lineColor;
        }
        
        gridOpacity = lineOpacity;
        
        // Recreate visualization if already started
        if (Application.isPlaying && gridLines != null)
        {
            DestroyGridVisualization();
            if (showGridLines)
            {
                CreateGridVisualization();
            }
        }
    }
    
    #endregion
    public int OccupiedCount => occupiedCells.Count;
    
    void Start()
    {
        InitializeGrid();
        CreateGridVisualization();
        
        if (showValidPositions)
        {
            CreatePositionIndicators();
        }
    }
    
    void Update()
    {
        // REMOVED: This was causing freezing by calling GetValidPlacements() every frame
        // UpdatePositionIndicators() is now called only when needed
    }
    
    private void InitializeGrid()
    {
        // Setup grid center if not assigned
        if (gridCenter == null)
        {
            GameObject centerObj = new GameObject("GridCenter");
            centerObj.transform.SetParent(transform);
            centerObj.transform.localPosition = Vector3.zero;
            gridCenter = centerObj.transform;
        }
        
        Debug.Log($"[MVPGrid] Initialized grid with dimensions {dimensions} and cell size {cellSize}");
    }
    
    private void CreateGridVisualization()
    {
        if (!showGridLines) return;
        
        // Prevent creating too many lines that could freeze Unity
        int totalLines = (dimensions.x + 1) * (dimensions.y + 1) + 
                        (dimensions.x + 1) * (dimensions.z + 1) + 
                        4; // Only corner vertical lines
        
        if (totalLines > 500)
        {
            Debug.LogWarning($"[MVPGrid] Grid too large for visualization ({totalLines} lines). Skipping grid lines.");
            return;
        }
        
        // Clean up existing visualization
        DestroyGridVisualization();
        
        List<LineRenderer> lines = new List<LineRenderer>();
        
        // Create parent for organization
        GameObject linesParent = new GameObject("GridLines");
        linesParent.transform.SetParent(transform);
        linesParent.transform.localPosition = Vector3.zero;
        
        // Lines parallel to X axis (varying Z)
        for (int z = 0; z <= dimensions.z; z++)
        {
            for (int y = 0; y <= dimensions.y; y++)
            {
                GameObject lineObj = new GameObject($"GridLine_X_Y{y}_Z{z}");
                lineObj.transform.SetParent(linesParent.transform);
                
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                SetupLineRenderer(lr);
                
                Vector3 start = GridToWorldPosition(new Vector3Int(0, y, z));
                Vector3 end = GridToWorldPosition(new Vector3Int(dimensions.x, y, z));
                
                lr.positionCount = 2;
                lr.SetPosition(0, start);
                lr.SetPosition(1, end);
                
                lines.Add(lr);
            }
        }
        
        // Lines parallel to Z axis (varying X)
        for (int x = 0; x <= dimensions.x; x++)
        {
            for (int y = 0; y <= dimensions.y; y++)
            {
                GameObject lineObj = new GameObject($"GridLine_Z_X{x}_Y{y}");
                lineObj.transform.SetParent(linesParent.transform);
                
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                SetupLineRenderer(lr);
                
                Vector3 start = GridToWorldPosition(new Vector3Int(x, y, 0));
                Vector3 end = GridToWorldPosition(new Vector3Int(x, y, dimensions.z));
                
                lr.positionCount = 2;
                lr.SetPosition(0, start);
                lr.SetPosition(1, end);
                
                lines.Add(lr);
            }
        }
        
        // Lines parallel to Y axis (varying X and Z) - only edges for performance
        for (int x = 0; x <= dimensions.x; x += dimensions.x)
        {
            for (int z = 0; z <= dimensions.z; z += dimensions.z)
            {
                GameObject lineObj = new GameObject($"GridLine_Y_X{x}_Z{z}");
                lineObj.transform.SetParent(linesParent.transform);
                
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                SetupLineRenderer(lr);
                
                Vector3 start = GridToWorldPosition(new Vector3Int(x, 0, z));
                Vector3 end = GridToWorldPosition(new Vector3Int(x, dimensions.y, z));
                
                lr.positionCount = 2;
                lr.SetPosition(0, start);
                lr.SetPosition(1, end);
                
                lines.Add(lr);
            }
        }
        
        gridLines = lines.ToArray();
        Debug.Log($"[MVPGrid] Created {gridLines.Length} grid lines");
    }
    
    private void DestroyGridVisualization()
    {
        if (gridLines != null)
        {
            foreach (var line in gridLines)
            {
                if (line != null)
                {
                    DestroyImmediate(line.gameObject);
                }
            }
            gridLines = null;
        }
        
        // Also destroy the parent container if it exists
        Transform linesParent = transform.Find("GridLines");
        if (linesParent != null)
        {
            DestroyImmediate(linesParent.gameObject);
        }
    }
    
    private void SetupLineRenderer(LineRenderer lr)
    {
        lr.material = gridLineMaterial ?? CreateDefaultLineMaterial();
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }
    
    private Material CreateDefaultLineMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(gridLineColor.r, gridLineColor.g, gridLineColor.b, gridOpacity);
        return mat;
    }
    
    #region Coordinate System
    
    /// <summary>
    /// Convert world position to grid coordinates
    /// </summary>
    public Vector3Int WorldToGridPosition(Vector3 worldPos)
    {
        Vector3 localPos = worldPos - GetGridOrigin();
        return new Vector3Int(
            Mathf.RoundToInt(localPos.x / cellSize),
            Mathf.RoundToInt(localPos.y / cellSize),
            Mathf.RoundToInt(localPos.z / cellSize)
        );
    }
    
    /// <summary>
    /// Convert grid coordinates to world position
    /// </summary>
    public Vector3 GridToWorldPosition(Vector3Int gridPos)
    {
        return GetGridOrigin() + new Vector3(
            gridPos.x * cellSize,
            gridPos.y * cellSize,
            gridPos.z * cellSize
        );
    }
    
    /// <summary>
    /// Get the nearest valid grid position to a world position
    /// </summary>
    public Vector3Int GetNearestGridPosition(Vector3 worldPos)
    {
        Vector3Int gridPos = WorldToGridPosition(worldPos);
        return ClampToGridBounds(gridPos);
    }
    
    private Vector3 GetGridOrigin()
    {
        Vector3 center = gridCenter != null ? gridCenter.position : transform.position;
        return center - new Vector3(
            (dimensions.x - 1) * cellSize * 0.5f,
            (dimensions.y - 1) * cellSize * 0.5f,
            (dimensions.z - 1) * cellSize * 0.5f
        );
    }
    
    private Vector3Int ClampToGridBounds(Vector3Int gridPos)
    {
        return new Vector3Int(
            Mathf.Clamp(gridPos.x, 0, dimensions.x - 1),
            Mathf.Clamp(gridPos.y, 0, dimensions.y - 1),
            Mathf.Clamp(gridPos.z, 0, dimensions.z - 1)
        );
    }
    
    #endregion
    
    #region Grid State Management
    
    /// <summary>
    /// Check if a grid position is within bounds
    /// </summary>
    public bool IsValidPosition(Vector3Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < dimensions.x &&
               gridPos.y >= 0 && gridPos.y < dimensions.y &&
               gridPos.z >= 0 && gridPos.z < dimensions.z;
    }
    
    /// <summary>
    /// Check if a grid position is occupied
    /// </summary>
    public bool IsOccupied(Vector3Int gridPos)
    {
        if (!IsValidPosition(gridPos)) return true; // Out of bounds = occupied
        return occupiedCells.ContainsKey(gridPos);
    }
    
    /// <summary>
    /// Check if a position is valid for cube placement
    /// </summary>
    public bool IsValidPlacement(Vector3Int gridPos)
    {
        if (!IsValidPosition(gridPos) || IsOccupied(gridPos))
            return false;
        
        // First cube can go anywhere
        if (occupiedCells.Count == 0)
            return true;
        
        // Subsequent cubes must connect to existing structure
        return HasAdjacentOccupiedCell(gridPos);
    }
    
    /// <summary>
    /// Check if a position has adjacent occupied cells (connectivity)
    /// </summary>
    public bool HasAdjacentOccupiedCell(Vector3Int gridPos)
    {
        Vector3Int[] neighbors = GetNeighborPositions(gridPos);
        return neighbors.Any(neighbor => IsOccupied(neighbor));
    }
    
    /// <summary>
    /// Get the 6 adjacent positions (face connectivity)
    /// </summary>
    public Vector3Int[] GetNeighborPositions(Vector3Int gridPos)
    {
        return new Vector3Int[]
        {
            gridPos + Vector3Int.right,   // +X
            gridPos + Vector3Int.left,    // -X
            gridPos + Vector3Int.up,      // +Y
            gridPos + Vector3Int.down,    // -Y
            gridPos + Vector3Int.forward, // +Z
            gridPos + Vector3Int.back     // -Z
        };
    }
    
    /// <summary>
    /// Occupy a grid position with a cube
    /// </summary>
    public bool OccupyPosition(Vector3Int gridPos, MagneticCube cube)
    {
        if (!IsValidPlacement(gridPos))
        {
            Debug.LogWarning($"[MVPGrid] Cannot occupy position {gridPos} - invalid placement");
            return false;
        }
        
        occupiedCells[gridPos] = cube;
        
        Debug.Log($"[MVPGrid] Position {gridPos} occupied by {cube.gameObject.name}");
        
        // Mark indicators as needing update
        needsIndicatorUpdate = true;
        
        // Trigger events
        OnCellOccupancyChanged?.Invoke(gridPos, true, cube);
        OnCubeCountChanged?.Invoke(occupiedCells.Count);
        CheckConnectivity();
        
        // Update indicators if needed
        if (showValidPositions)
        {
            UpdatePositionIndicators();
        }
        
        return true;
    }
    
    /// <summary>
    /// Free a grid position
    /// </summary>
    public void FreePosition(Vector3Int gridPos)
    {
        if (!occupiedCells.ContainsKey(gridPos))
        {
            Debug.LogWarning($"[MVPGrid] Cannot free position {gridPos} - not occupied");
            return;
        }
        
        MagneticCube cube = occupiedCells[gridPos];
        occupiedCells.Remove(gridPos);
        
        Debug.Log($"[MVPGrid] Position {gridPos} freed from {cube.gameObject.name}");
        
        // Mark indicators as needing update
        needsIndicatorUpdate = true;
        
        // Trigger events
        OnCellOccupancyChanged?.Invoke(gridPos, false, cube);
        OnCubeCountChanged?.Invoke(occupiedCells.Count);
        CheckConnectivity();
        
        // Update indicators if needed
        if (showValidPositions)
        {
            UpdatePositionIndicators();
        }
    }
    
    /// <summary>
    /// Get cube at specific grid position
    /// </summary>
    public MagneticCube GetCubeAt(Vector3Int gridPos)
    {
        return occupiedCells.ContainsKey(gridPos) ? occupiedCells[gridPos] : null;
    }
    
    /// <summary>
    /// Get all occupied positions
    /// </summary>
    public List<Vector3Int> GetOccupiedPositions()
    {
        return new List<Vector3Int>(occupiedCells.Keys);
    }
    
    /// <summary>
    /// Get all cubes in the grid
    /// </summary>
    public List<MagneticCube> GetAllCubes()
    {
        return new List<MagneticCube>(occupiedCells.Values);
    }
    
    #endregion
    
    #region Connectivity Analysis
    
    /// <summary>
    /// Check if all occupied cells form a connected structure
    /// </summary>
    public bool IsConnectedStructure()
    {
        List<Vector3Int> occupied = GetOccupiedPositions();
        
        if (occupied.Count <= 1) return true;
        
        // BFS to check connectivity
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        
        queue.Enqueue(occupied[0]);
        visited.Add(occupied[0]);
        
        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            Vector3Int[] neighbors = GetNeighborPositions(current);
            
            foreach (Vector3Int neighbor in neighbors)
            {
                if (IsOccupied(neighbor) && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        
        bool isConnected = visited.Count == occupied.Count;
        return isConnected;
    }
    
    private void CheckConnectivity()
    {
        bool isConnected = IsConnectedStructure();
        OnConnectivityChanged?.Invoke(isConnected);
    }
    
    #endregion
    
    #region Position Indicators
    
    private void CreatePositionIndicators()
    {
        // Create parent for organization
        GameObject indicatorsParent = new GameObject("PositionIndicators");
        indicatorsParent.transform.SetParent(transform);
        indicatorsParent.transform.localPosition = Vector3.zero;
        
        // Pre-create some indicators for performance
        int maxIndicators = 20;
        for (int i = 0; i < maxIndicators; i++)
        {
            GameObject indicator = CreatePositionIndicator(indicatorsParent.transform);
            indicator.SetActive(false);
            positionIndicators.Add(indicator);
        }
    }
    
    private GameObject CreatePositionIndicator(Transform parent)
    {
        GameObject indicator;
        
        if (positionIndicatorPrefab != null)
        {
            indicator = Instantiate(positionIndicatorPrefab, parent);
        }
        else
        {
            // Create default indicator
            indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.transform.SetParent(parent);
            indicator.transform.localScale = Vector3.one * (cellSize * 0.1f);
            
            // Remove collider
            Collider col = indicator.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            
            // Setup material
            Renderer renderer = indicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = validPositionMaterial ?? CreateDefaultIndicatorMaterial();
            }
        }
        
        return indicator;
    }
    
    private Material CreateDefaultIndicatorMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0f, 1f, 0f, 0.5f);
        mat.SetFloat("_Mode", 2); // Fade mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }
    
    private void UpdatePositionIndicators()
    {
        if (!needsIndicatorUpdate) return;
        
        // Hide all indicators
        foreach (var indicator in positionIndicators)
        {
            indicator.SetActive(false);
        }
        
        // Get valid positions using optimized method
        List<Vector3Int> validPositions = GetValidPlacementsOptimized();
        
        int indicatorIndex = 0;
        foreach (Vector3Int pos in validPositions)
        {
            if (indicatorIndex >= positionIndicators.Count) break;
            
            GameObject indicator = positionIndicators[indicatorIndex];
            indicator.SetActive(true);
            indicator.transform.position = GridToWorldPosition(pos);
            
            // Update material based on connectivity
            UpdateIndicatorMaterial(indicator, pos);
            
            indicatorIndex++;
        }
        
        needsIndicatorUpdate = false;
    }
    
    private void UpdateIndicatorMaterial(GameObject indicator, Vector3Int gridPos)
    {
        Renderer renderer = indicator.GetComponent<Renderer>();
        if (renderer == null) return;
        
        bool hasAdjacent = HasAdjacentOccupiedCell(gridPos);
        
        if (hasAdjacent && connectedPositionMaterial != null)
        {
            renderer.material = connectedPositionMaterial;
        }
        else if (validPositionMaterial != null)
        {
            renderer.material = validPositionMaterial;
        }
    }
    
    private List<Vector3Int> GetValidPlacements()
    {
        // Use optimized version to prevent freezing
        return GetValidPlacementsOptimized();
    }
    
    /// <summary>
    /// Optimized version that only checks adjacent positions to occupied cells
    /// </summary>
    private List<Vector3Int> GetValidPlacementsOptimized()
    {
        List<Vector3Int> validPositions = new List<Vector3Int>();
        
        // If grid is empty, center position is valid
        if (occupiedCells.Count == 0)
        {
            Vector3Int centerPos = new Vector3Int(dimensions.x / 2, dimensions.y / 2, dimensions.z / 2);
            if (IsValidPosition(centerPos))
            {
                validPositions.Add(centerPos);
            }
            return validPositions;
        }
        
        // Use HashSet to avoid duplicates
        HashSet<Vector3Int> candidatePositions = new HashSet<Vector3Int>();
        
        // For each occupied cell, check its 6 neighbors
        foreach (Vector3Int occupiedPos in occupiedCells.Keys)
        {
            Vector3Int[] neighbors = GetNeighborPositions(occupiedPos);
            
            foreach (Vector3Int neighbor in neighbors)
            {
                // Only add if it's valid and not already occupied
                if (IsValidPosition(neighbor) && !IsOccupied(neighbor))
                {
                    candidatePositions.Add(neighbor);
                }
            }
        }
        
        validPositions.AddRange(candidatePositions);
        return validPositions;
    }
    
    #endregion
    
    #region Public Interface
    
    /// <summary>
    /// Clear all cubes from the grid
    /// </summary>
    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        List<MagneticCube> cubes = GetAllCubes();
        occupiedCells.Clear();
        
        foreach (var cube in cubes)
        {
            if (cube != null)
            {
                cube.ForceRelease();
            }
        }
        
        Debug.Log("[MVPGrid] Grid cleared");
        OnCubeCountChanged?.Invoke(0);
        CheckConnectivity();
    }
    
    /// <summary>
    /// Validate the current grid structure
    /// </summary>
    [ContextMenu("Validate Structure")]
    public void ValidateStructure()
    {
        bool isConnected = IsConnectedStructure();
        int cubeCount = occupiedCells.Count;
        
        Debug.Log($"[MVPGrid] Validation: {cubeCount} cubes, Connected: {isConnected}");
        
        if (!isConnected && cubeCount > 1)
        {
            Debug.LogWarning("[MVPGrid] Structure is not connected!");
        }
    }
    
    /// <summary>
    /// Toggle visual indicators
    /// </summary>
    public void SetValidPositionsVisible(bool visible)
    {
        showValidPositions = visible;
        needsIndicatorUpdate = true;
        
        if (!visible)
        {
            // Hide all indicators immediately
            foreach (var indicator in positionIndicators)
            {
                indicator.SetActive(false);
            }
        }
        else
        {
            // Update indicators to show current valid positions
            UpdatePositionIndicators();
        }
    }
    
    /// <summary>
    /// Toggle grid lines
    /// </summary>
    public void SetGridLinesVisible(bool visible)
    {
        showGridLines = visible;
        
        if (gridLines != null)
        {
            foreach (var line in gridLines)
            {
                if (line != null)
                {
                    line.enabled = visible;
                }
            }
        }
    }
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmos()
    {
        // Draw grid bounds
        Gizmos.color = Color.yellow;
        Vector3 center = GetGridOrigin() + new Vector3(
            (dimensions.x - 1) * cellSize * 0.5f,
            (dimensions.y - 1) * cellSize * 0.5f,
            (dimensions.z - 1) * cellSize * 0.5f
        );
        Vector3 size = new Vector3(dimensions.x * cellSize, dimensions.y * cellSize, dimensions.z * cellSize);
        Gizmos.DrawWireCube(center, size);
        
        // Draw occupied cells
        if (Application.isPlaying && occupiedCells != null)
        {
            Gizmos.color = Color.green;
            foreach (var kvp in occupiedCells)
            {
                Vector3 worldPos = GridToWorldPosition(kvp.Key);
                Gizmos.DrawWireCube(worldPos, Vector3.one * cellSize * 0.9f);
            }
        }
    }
    
    #endregion
}