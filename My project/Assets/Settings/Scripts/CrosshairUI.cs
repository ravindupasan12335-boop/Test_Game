using UnityEngine;

public class CrosshairUI : MonoBehaviour
{
    [Header("Crosshair (+) Appearance")]
    public float armLength = 10f;
    public float thickness = 2f;
    public float gap = 3f; // Small center gap so the '+' stays crisp
    public Color crosshairColor = Color.white;
    public bool showOutline = true;
    public Color outlineColor = Color.black;

    private Texture2D pixelTex;

    void Awake()
    {
        pixelTex = new Texture2D(1, 1);
        pixelTex.SetPixel(0, 0, Color.white);
        pixelTex.Apply();
    }

    void OnGUI()
    {
        if (pixelTex == null) return;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        if (showOutline)
        {
            DrawPlus(cx, cy, armLength + 1f, thickness + 2f, Mathf.Max(1f, gap - 1f), outlineColor);
        }

        DrawPlus(cx, cy, armLength, thickness, gap, crosshairColor);
    }

    void DrawPlus(float cx, float cy, float len, float thick, float centerGap, Color col)
    {
        Color prev = GUI.color;
        GUI.color = col;

        float halfThick = thick * 0.5f;

        // Top bar
        GUI.DrawTexture(new Rect(cx - halfThick, cy - centerGap - len, thick, len), pixelTex);
        // Bottom bar
        GUI.DrawTexture(new Rect(cx - halfThick, cy + centerGap, thick, len), pixelTex);
        // Left bar
        GUI.DrawTexture(new Rect(cx - centerGap - len, cy - halfThick, len, thick), pixelTex);
        // Right bar
        GUI.DrawTexture(new Rect(cx + centerGap, cy - halfThick, len, thick), pixelTex);

        GUI.color = prev;
    }

    void OnDestroy()
    {
        if (pixelTex != null)
        {
            Destroy(pixelTex);
        }
    }
}