using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generates a procedural grid texture for wireframe effects
/// Attach this script to any GameObject and click "Generate Grid Texture" in the Inspector
/// </summary>
public class GridTextureGenerator : MonoBehaviour
{
    [Header("Grid Texture Settings")]
    [Tooltip("Resolution of the generated texture")]
    public int textureSize = 512;

    [Tooltip("Number of grid lines")]
    [Range(2, 50)]
    public int gridDivisions = 10;

    [Tooltip("Thickness of grid lines in pixels")]
    [Range(1, 20)]
    public int lineThickness = 2;

    [Tooltip("Color of the grid lines")]
    public Color lineColor = Color.white;

    [Tooltip("Background color")]
    public Color backgroundColor = Color.black;

    [Header("Output")]
    [Tooltip("Path where the texture will be saved (relative to Assets folder)")]
    public string savePath = "shader_claude/FresnelGlow/GridTexture.png";

    /// <summary>
    /// Generates the grid texture procedurally
    /// </summary>
    public Texture2D GenerateGridTexture()
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[textureSize * textureSize];

        // Calculate grid spacing
        float spacing = textureSize / (float)gridDivisions;

        // Fill all pixels
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // Check if pixel is on a grid line
                bool isOnVerticalLine = (x % (int)spacing) < lineThickness;
                bool isOnHorizontalLine = (y % (int)spacing) < lineThickness;

                // Set pixel color
                if (isOnVerticalLine || isOnHorizontalLine)
                {
                    pixels[y * textureSize + x] = lineColor;
                }
                else
                {
                    pixels[y * textureSize + x] = backgroundColor;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Saves the generated texture to the project
    /// </summary>
    public void SaveTexture()
    {
        Texture2D texture = GenerateGridTexture();

        // Encode to PNG
        byte[] bytes = texture.EncodeToPNG();

        // Ensure directory exists
        string fullPath = Application.dataPath + "/" + savePath;
        string directory = System.IO.Path.GetDirectoryName(fullPath);

        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        // Write file
        System.IO.File.WriteAllBytes(fullPath, bytes);

        // Refresh Unity's asset database
        AssetDatabase.Refresh();

        Debug.Log($"Grid texture saved to: Assets/{savePath}");

        // Set import settings
        string assetPath = "Assets/" + savePath;
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
#endif
}

#if UNITY_EDITOR
/// <summary>
/// Custom Inspector for GridTextureGenerator
/// Adds a button to generate the texture
/// </summary>
[CustomEditor(typeof(GridTextureGenerator))]
public class GridTextureGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GridTextureGenerator generator = (GridTextureGenerator)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Click the button below to generate and save the grid texture. " +
            "The texture will be saved to: Assets/" + generator.savePath,
            MessageType.Info
        );

        if (GUILayout.Button("Generate Grid Texture", GUILayout.Height(40)))
        {
            generator.SaveTexture();
        }

        EditorGUILayout.Space();

        // Preview section
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        if (GUILayout.Button("Generate Preview (not saved)"))
        {
            Texture2D preview = generator.GenerateGridTexture();
            EditorGUIUtility.PingObject(preview);
        }
    }
}
#endif
