using UnityEngine;

/// <summary>
/// Controls lighting and post-processing effects to enhance the holographic shader appearance
/// Attach this to your building/scene to automatically configure optimal lighting for holographic materials
/// </summary>
public class HolographicLightingController : MonoBehaviour
{
    [Header("Lighting Setup")]
    [Tooltip("Automatically create directional light if none exists")]
    [SerializeField] private bool autoCreateLight = true;

    [Tooltip("Main light color (cyan/blue tint works best)")]
    [SerializeField] private Color mainLightColor = new Color(0.5f, 0.8f, 1f, 1f);

    [Tooltip("Main light intensity")]
    [SerializeField] private float mainLightIntensity = 0.3f;

    [Header("Ambient Lighting")]
    [Tooltip("Ambient color for holographic effect")]
    [SerializeField] private Color ambientColor = new Color(0.1f, 0.2f, 0.3f, 1f);

    [Tooltip("Ambient intensity")]
    [SerializeField] private float ambientIntensity = 0.5f;

    [Header("Accent Lights")]
    [Tooltip("Create accent point lights at corners/edges")]
    [SerializeField] private bool createAccentLights = true;

    [Tooltip("Accent light color (pink/magenta for highlights)")]
    [SerializeField] private Color accentLightColor = new Color(1f, 0.3f, 0.8f, 1f);

    [Tooltip("Number of accent lights to create")]
    [SerializeField] private int accentLightCount = 4;

    [Tooltip("Accent light range")]
    [SerializeField] private float accentLightRange = 15f;

    [Tooltip("Accent light intensity")]
    [SerializeField] private float accentLightIntensity = 2f;

    [Header("Pulsing Effect")]
    [Tooltip("Enable pulsing lights")]
    [SerializeField] private bool enablePulsing = true;

    [Tooltip("Pulse speed")]
    [SerializeField] private float pulseSpeed = 1.5f;

    [Tooltip("Pulse intensity variation")]
    [SerializeField] private float pulseIntensity = 0.5f;

    [Header("Fog Settings")]
    [Tooltip("Enable fog for atmospheric effect")]
    [SerializeField] private bool enableFog = true;

    [Tooltip("Fog color")]
    [SerializeField] private Color fogColor = new Color(0.05f, 0.1f, 0.2f, 1f);

    [Tooltip("Fog density")]
    [SerializeField] private float fogDensity = 0.01f;

    // Private references
    private Light mainLight;
    private Light[] accentLights;
    private float[] accentLightBaseIntensities;
    private float[] accentLightPhaseOffsets;

    private void Start()
    {
        SetupLighting();
        SetupAmbient();
        SetupFog();

        if (createAccentLights)
        {
            CreateAccentLights();
        }
    }

    private void Update()
    {
        if (enablePulsing && accentLights != null)
        {
            PulseLights();
        }
    }

    private void SetupLighting()
    {
        // Find or create main directional light
        mainLight = FindObjectOfType<Light>();

        if (mainLight == null && autoCreateLight)
        {
            GameObject lightObj = new GameObject("Main Holographic Light");
            mainLight = lightObj.AddComponent<Light>();
            mainLight.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        if (mainLight != null)
        {
            mainLight.color = mainLightColor;
            mainLight.intensity = mainLightIntensity;

            // URP specific settings would go here if needed
            // For built-in, we can set shadows
            mainLight.shadows = LightShadows.Soft;
        }
    }

    private void SetupAmbient()
    {
        // Set ambient lighting
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = ambientIntensity;

        // Set skybox tint if available
        if (RenderSettings.skybox != null)
        {
            RenderSettings.skybox.SetColor("_Tint", new Color(0.3f, 0.5f, 0.7f, 1f));
        }
    }

    private void SetupFog()
    {
        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = fogDensity;
        }
    }

    private void CreateAccentLights()
    {
        // Get bounds of the building
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combinedBounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            combinedBounds.Encapsulate(r.bounds);
        }

        // Create array for accent lights
        accentLights = new Light[accentLightCount];
        accentLightBaseIntensities = new float[accentLightCount];
        accentLightPhaseOffsets = new float[accentLightCount];

        Vector3 center = combinedBounds.center;
        Vector3 extents = combinedBounds.extents;

        for (int i = 0; i < accentLightCount; i++)
        {
            GameObject lightObj = new GameObject($"Accent Light {i}");
            lightObj.transform.parent = transform;

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = accentLightColor;
            light.intensity = accentLightIntensity;
            light.range = accentLightRange;
            light.shadows = LightShadows.None; // No shadows for performance

            // Position lights around the building
            float angle = (i / (float)accentLightCount) * Mathf.PI * 2f;
            float radius = Mathf.Max(extents.x, extents.z) * 1.5f;

            Vector3 position = center + new Vector3(
                Mathf.Cos(angle) * radius,
                extents.y * 0.5f + Random.Range(-extents.y * 0.3f, extents.y * 0.3f),
                Mathf.Sin(angle) * radius
            );

            lightObj.transform.position = position;

            accentLights[i] = light;
            accentLightBaseIntensities[i] = accentLightIntensity;
            accentLightPhaseOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void PulseLights()
    {
        if (accentLights == null) return;

        float time = Time.time * pulseSpeed;

        for (int i = 0; i < accentLights.Length; i++)
        {
            if (accentLights[i] != null)
            {
                float pulse = Mathf.Sin(time + accentLightPhaseOffsets[i]) * 0.5f + 0.5f;
                accentLights[i].intensity = accentLightBaseIntensities[i] + (pulse * pulseIntensity);
            }
        }
    }

    // Public methods for runtime control
    public void SetMainLightColor(Color color)
    {
        mainLightColor = color;
        if (mainLight != null) mainLight.color = color;
    }

    public void SetAccentLightColor(Color color)
    {
        accentLightColor = color;
        if (accentLights != null)
        {
            foreach (Light light in accentLights)
            {
                if (light != null) light.color = color;
            }
        }
    }

    public void SetPulseSpeed(float speed)
    {
        pulseSpeed = speed;
    }

    public void TogglePulsing(bool enable)
    {
        enablePulsing = enable;
    }

    // Cleanup
    private void OnDestroy()
    {
        if (accentLights != null)
        {
            foreach (Light light in accentLights)
            {
                if (light != null) Destroy(light.gameObject);
            }
        }
    }

    // Gizmos for visualizing light positions in editor
    private void OnDrawGizmosSelected()
    {
        if (accentLights != null)
        {
            Gizmos.color = accentLightColor;
            foreach (Light light in accentLights)
            {
                if (light != null)
                {
                    Gizmos.DrawWireSphere(light.transform.position, accentLightRange * 0.1f);
                }
            }
        }
    }
}
