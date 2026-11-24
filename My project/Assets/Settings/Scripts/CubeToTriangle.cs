using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CornerFloorShape : MonoBehaviour
{
    [Header("90-Degree Right Triangle Dimensions")]
    [Min(0.05f)]
    public float widthX = 4f;

    [Min(0.05f)]
    public float depthZ = 4f;

    [Min(0.01f)]
    public float thicknessY = 0.25f;

    [Tooltip("Y offset so top/bottom aligns cleanly with surrounding floor pieces.")]
    public float yOffset = 0f;

    [Header("Collider")]
    public bool syncMeshCollider = true;
    public bool convexCollider = false;

    private Mesh generatedMesh;
    private Mesh originalMesh;
    private bool pendingColliderSync = false;

    void Awake()
    {
        CacheOriginalMesh();
        ApplyShape(allowDestroyBoxCollider: true);
    }

    void OnValidate()
    {
        widthX = Mathf.Max(0.05f, widthX);
        depthZ = Mathf.Max(0.05f, depthZ);
        thicknessY = Mathf.Max(0.01f, thicknessY);

        if (!Application.isPlaying)
        {
            CacheOriginalMesh();
            ApplyShape(allowDestroyBoxCollider: false);
            pendingColliderSync = true;
        }
    }

    void Update()
    {
        if (pendingColliderSync && !Application.isPlaying)
        {
            pendingColliderSync = false;
            ApplyShape(allowDestroyBoxCollider: false);
        }
    }

    private void CacheOriginalMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && mf.sharedMesh != generatedMesh)
        {
            originalMesh = mf.sharedMesh;
        }
    }

    [ContextMenu("Apply 90-Degree Triangle Shape")]
    public void ApplyShapeContext()
    {
        ApplyShape(allowDestroyBoxCollider: !Application.isPlaying);
    }

    public void ApplyShape(bool allowDestroyBoxCollider = true)
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) return;

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh { name = gameObject.name + "_RightTriangleFloor" };
        }
        else
        {
            generatedMesh.Clear();
        }

        float yTop = yOffset;
        float yBot = yOffset - thicknessY;

        // 3 triangle corners in XZ:
        // 0: (0, 0)       -> 90-degree corner
        // 1: (widthX, 0)  -> along +X
        // 2: (0, depthZ)  -> along +Z
        // Hypotenuse is a straight line from (widthX, 0) to (0, depthZ).
        Vector3[] verts = new Vector3[]
        {
            // Top face (0..2)
            new Vector3(0f, yTop, 0f),
            new Vector3(widthX, yTop, 0f),
            new Vector3(0f, yTop, depthZ),

            // Bottom face (3..5)
            new Vector3(0f, yBot, 0f),
            new Vector3(widthX, yBot, 0f),
            new Vector3(0f, yBot, depthZ)
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f)
        };

        int[] tris = new int[]
        {
            // Top triangle (facing +Y)
            0, 2, 1,

            // Bottom triangle (facing -Y)
            3, 4, 5,

            // Edge 0->1 (along X, facing -Z)
            0, 1, 4,
            0, 4, 3,

            // Edge 1->2 (hypotenuse, facing +X/+Z)
            1, 2, 5,
            1, 5, 4,

            // Edge 2->0 (along Z, facing -X)
            2, 0, 3,
            2, 3, 5
        };

        generatedMesh.vertices = verts;
        generatedMesh.uv = uvs;
        generatedMesh.triangles = tris;
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();

        mf.sharedMesh = generatedMesh;

        if (syncMeshCollider)
        {
            BoxCollider boxCol = GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                if (allowDestroyBoxCollider && !Application.isPlaying)
                {
                    DestroyImmediate(boxCol);
                }
                else
                {
                    boxCol.enabled = false;
                }
            }

            MeshCollider meshCol = GetComponent<MeshCollider>();
            if (meshCol == null)
            {
                meshCol = gameObject.AddComponent<MeshCollider>();
            }
            meshCol.convex = convexCollider;
            meshCol.sharedMesh = null;
            meshCol.sharedMesh = generatedMesh;
        }
    }

    [ContextMenu("Restore Original Mesh")]
    public void RestoreOriginalMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && originalMesh != null)
        {
            mf.sharedMesh = originalMesh;
        }
    }
}