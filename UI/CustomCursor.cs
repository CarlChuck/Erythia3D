using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    [Tooltip("The texture to use for the custom cursor.")]
    [SerializeField] private Texture2D cursorTexture;

    [Tooltip("The offset from the top-left of the texture to use as the cursor's hotspot (the precise click point).")]
    [SerializeField] private Vector2 hotspot = Vector2.zero; // (0,0) is the top-left corner

    [Tooltip("Specifies whether the cursor is rendered by hardware or software.")]
    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    [Tooltip("The color to tint the cursor.")]
    [SerializeField] private Color cursorTint = Color.white;

    private Texture2D tintedCursorTexture; // Instance for the tinted cursor

    void Start()
    {
        // Check if the texture is readable
        try
        {
            cursorTexture.GetPixel(0, 0); // Attempt to read a pixel
        }
        catch (UnityException e)
        {
            Debug.LogError($"CustomCursor: Cursor texture '{cursorTexture.name}' is not readable. Please enable 'Read/Write Enabled' in its import settings. Error: {e.Message}", this);
            cursorTexture = null; // Prevent further errors
        }

        ApplyTintedCursor();
    }

    // Optional: Reset to default cursor when this object is destroyed or disabled
    void OnDisable()
    {
        // Reset to default hardware cursor
        Cursor.SetCursor(null, Vector2.zero, cursorMode);

        // Clean up the temporary texture instance
        if (tintedCursorTexture != null)
        {
            Destroy(tintedCursorTexture);
            tintedCursorTexture = null;
        }
    }

    void OnEnable()
    {
        // Re-apply custom cursor if enabled after being disabled
        ApplyTintedCursor();
    }

    /// <summary>
    /// Sets the tint color for the cursor at runtime.
    /// </summary>
    /// <param name="newTint">The desired tint color.</param>
    public void SetCursorTint(Color newTint)
    {
        cursorTint = newTint;
        ApplyTintedCursor();
    }

    /// <summary>
    /// Creates a tinted version of the cursor texture and applies it.
    /// </summary>
    private void ApplyTintedCursor()
    {
        if (cursorTexture == null)
        {
            // Reset to default if base texture is missing or unreadable
            Cursor.SetCursor(null, Vector2.zero, cursorMode);
            return;
        }

        // Destroy previous temporary texture if it exists
        if (tintedCursorTexture != null)
        {
            Destroy(tintedCursorTexture);
        }

        // Create a new Texture2D instance for the tinted cursor
        tintedCursorTexture = new Texture2D(cursorTexture.width, cursorTexture.height, cursorTexture.format, false);

        // Get pixels from original texture
        Color[] pixels = cursorTexture.GetPixels();

        // Apply tint
        for (int i = 0; i < pixels.Length; i++)
        {
            // Multiply original color by tint color (component-wise)
            // Keep original alpha multiplied by tint alpha
            pixels[i] = pixels[i] * cursorTint;
        }

        // Apply tinted pixels to the new texture
        tintedCursorTexture.SetPixels(pixels);
        tintedCursorTexture.Apply();

        // Set the tinted cursor
        Cursor.SetCursor(tintedCursorTexture, hotspot, cursorMode);
    }
} 