using UnityEngine;

public class FloorGridBuilder : MonoBehaviour
{
    [Header("Grid Size (10 x 10 = 100 tiles)")]
    public int columns = 10;
    public int rows = 10;

    [Header("Tile Dimensions")]
    public float tileWidth = 4f;     // X size of each flattened cube
    public float tileDepth = 4f;     // Z size of each flattened cube
    public float tileThickness = 0.5f; // Y thickness

    [Header("Optional Material")]
    public Material floorMaterial;   // Drag your grass material here if you want

    [ContextMenu("Generate Floor Grid")]
    public void GenerateGrid()
    {
        // Clear any previously generated child tiles so you don't stack duplicates
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // Center the grid around this object's position
        float startX = -((columns - 1) * tileWidth) / 2f;
        float startZ = -((rows - 1) * tileDepth) / 2f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Floor_Tile_{c}_{r}";
                tile.transform.SetParent(transform);

                tile.transform.localScale = new Vector3(tileWidth, tileThickness, tileDepth);

                float posX = startX + c * tileWidth;
                float posZ = startZ + r * tileDepth;
                // Place top surface flush with y = 0 of this parent object
                tile.transform.localPosition = new Vector3(posX, -tileThickness / 2f, posZ);

                if (floorMaterial != null)
                {
                    tile.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                }
            }
        }

        Debug.Log($"Generated {columns * rows} floor tiles with exact edge-to-edge spacing.");
    }
}
