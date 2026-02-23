using System.Collections.Generic;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    /// <summary>
    /// Generates a 10x11 Flat-top hexagonal grid and stores the spawned objects.
    /// Handles automatic offset calculation for centering the grid.
    /// </summary>
    public class GridGenerator : MonoBehaviour
    {
        public static GridGenerator Instance { get; private set; }

        #region Fields

        [Header("Grid Dimensions")]
        [SerializeField] private int width = 10;
        [SerializeField] private int height = 11;

        [Header("Hexagon Settings")]
        [SerializeField] private float hexSize = 1.0f;
        [SerializeField] private GameObject hexPrefab;

        [Header("Storage")]
        [SerializeField, ReadOnly] private List<GameObject> spawnedHexes = new List<GameObject>();

        #endregion

        #region Properties

        public List<GameObject> SpawnedHexes => spawnedHexes;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            GenerateGrid();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Generates the hexagonal grid based on defined dimensions.
        /// </summary>
        [ContextMenu("Generate Grid")]
        public void GenerateGrid()
        {
            ClearGrid();

            if (hexPrefab == null)
            {
                Debug.LogError("GridGenerator: Hex Prefab is not assigned!");
                return;
            }

            // Calculation Constants for Flat-Top Hexagons
            // Width of a flat-top hexagon is 2 * size
            // Horizontal distance between adjacent hexes is 3/2 * size
            // Vertical distance between adjacent hexes is sqrt(3) * size
            // In odd-q or even-q layouts:
            // x_spacing = size * 1.5
            // y_spacing = size * sqrt(3)
            
            float xSpacing = hexSize * 1.5f;
            float ySpacing = hexSize * Mathf.Sqrt(3f);

            // Calculate total bounds for centering
            float totalWidth = (width - 1) * xSpacing;
            float totalHeight = (height - 1) * ySpacing;
            
            // Adjust for the vertical offset in columns
            // Every other column is offset by half the vertical spacing
            if (width > 1) totalHeight += ySpacing * 0.5f;

            Vector3 gridOffset = new Vector3(-totalWidth * 0.5f, 0, -totalHeight * 0.5f);

            for (int q = 0; q < width; q++)
            {
                for (int r = 0; r < height; r++)
                {
                    // Flat-top Odd-Q Offset Coordinates
                    float xPos = q * xSpacing;
                    float zPos = r * ySpacing;

                    // Offset every odd column downwards by half ySpacing
                    if (q % 2 != 0)
                    {
                        zPos += ySpacing * 0.5f;
                    }

                    Vector3 spawnPos = transform.position + gridOffset + new Vector3(xPos, 0, zPos);
                    
                    GameObject hexObj = Instantiate(hexPrefab, spawnPos, Quaternion.identity, transform);
                    hexObj.name = $"Hex_{q}_{r}";
                    
                    // Initialize HexCell if it exists
                    HexCell cell = hexObj.GetComponent<HexCell>();
                    if (cell != null)
                    {
                        cell.Initialize(q, r);
                    }
                    else
                    {
                        Debug.LogWarning($"GridGenerator: Hex prefab at {q},{r} is missing HexCell component!");
                    }
                    
                    spawnedHexes.Add(hexObj);
                }
            }

            Debug.Log($"GridGenerator: Generated {spawnedHexes.Count} hexes.");
        }

        /// <summary>
        /// Clears the existing grid objects.
        /// </summary>
        [ContextMenu("Clear Grid")]
        public void ClearGrid()
        {
            // First, clear based on the list
            foreach (var hex in spawnedHexes)
            {
                if (hex != null)
                {
                    if (Application.isPlaying)
                        Destroy(hex);
                    else
                        DestroyImmediate(hex);
                }
            }
            spawnedHexes.Clear();

            // Safety check for Editor: Remove any children named "Hex_" that might have been lost from the list
            if (!Application.isPlaying)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = transform.GetChild(i).gameObject;
                    if (child.name.StartsWith("Hex_"))
                    {
                        DestroyImmediate(child);
                    }
                }
            }
        }

        private void OnValidate()
        {
            // Ensure width and height are at least 1
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            hexSize = Mathf.Max(0.1f, hexSize);
        }

        #endregion
    }

    /// <summary>
    /// Simple attribute to make list read-only in inspector (Optional visual helper)
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}
