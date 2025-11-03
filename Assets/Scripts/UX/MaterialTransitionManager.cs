using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Button))]
public class MaterialTransitionManager : MonoBehaviour
{
    [Header("Material Settings")]
    [SerializeField] private Material importantMaterial;
    [SerializeField] private Material nonImportantMaterial;

    [Header("Transition Settings")]
    [SerializeField] private float colorTransitionDuration = 0.3f;
    [SerializeField] private bool useEmissionPulse = true;
    [SerializeField] private float emissionIntensity = 5f;

    [Header("Custom Shader Settings (for AlarmIndicator shader)")]
    [SerializeField] private bool useCustomShader = true;
    [SerializeField] private float shaderGlowIntensity = 15f;
    [SerializeField] private float shaderPulseSpeed = 120f;

    [Header("Lightning Strike Settings")]
    [SerializeField] private bool enableLightning = true;
    [SerializeField] private Material lightningMaterial;
    [SerializeField] private float lightningHeight = 20f;
    [SerializeField] private float lightningWidth = 0.08f;
    [SerializeField] private float lightningYOffset = 0f;
    [SerializeField] private float lightningFlashDuration = 0.3f;
    [SerializeField] private float lightningOffDuration = 1.5f;

    [Header("Object Categories")]
    [Tooltip("Parent object containing static FBX objects as immediate children")]
    [SerializeField] private GameObject staticObjectsParent;
    [Tooltip("Optional: Parent object containing HVAC tagged objects. If not assigned, searches entire scene.")]
    [SerializeField] private GameObject hvacParent;

    [Header("Camera Cycling")]
    [SerializeField] private CameraObjectCycler cameraObjectCycler;

    private List<GameObject> staticObjects = new List<GameObject>();

    private List<GameObject> activeElements = new List<GameObject>();
    private Dictionary<GameObject, ObjectData> objectDataCache = new Dictionary<GameObject, ObjectData>();
    private Button button;

    // Public property for external scripts to check highlight state
    public bool isHighlightActive { get; private set; } = false;

    private class ObjectData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public bool isActiveElement;
        public LineRenderer lightningStrike;
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ToggleHighlight);
    }

    private void Start()
    {
        LoadStaticObjects();
        LoadHVACObjects();
        CacheObjectData();
    }

    private void LoadStaticObjects()
    {
        staticObjects.Clear();

        if (staticObjectsParent == null)
        {
            Debug.LogWarning("MaterialTransitionManager: Static Objects Parent not assigned!");
            return;
        }

        // Search all children recursively for renderers, but exclude HVAC-tagged objects
        Renderer[] allRenderers = staticObjectsParent.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in allRenderers)
        {
            // Skip if this object or any of its parents has the HVAC tag
            if (!IsHVACTaggedObject(renderer.gameObject))
            {
                staticObjects.Add(renderer.gameObject);
            }
        }

        Debug.Log($"MaterialTransitionManager: Found {staticObjects.Count} static objects under parent '{staticObjectsParent.name}' (excluded HVAC-tagged objects)");
    }

    private bool IsHVACTaggedObject(GameObject obj)
    {
        // Check if this object or any of its parents has the HVAC tag
        Transform current = obj.transform;
        while (current != null)
        {
            if (current.CompareTag("HVAC"))
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    private void LoadHVACObjects()
    {
        activeElements.Clear();

        GameObject[] hvacObjects;

        // Search within parent if assigned, otherwise search entire scene
        if (hvacParent != null)
        {
            // Find all objects with "HVAC" tag under the parent (nested search)
            Transform[] allChildren = hvacParent.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.gameObject.CompareTag("HVAC"))
                {
                    activeElements.Add(child.gameObject);
                }
            }
            Debug.Log($"MaterialTransitionManager: Found {activeElements.Count} HVAC objects under parent '{hvacParent.name}'");
        }
        else
        {
            // Search entire scene
            hvacObjects = GameObject.FindGameObjectsWithTag("HVAC");
            activeElements.AddRange(hvacObjects);
            Debug.Log($"MaterialTransitionManager: Found {activeElements.Count} HVAC objects in scene");
        }
    }

    private void CacheObjectData()
    {
        objectDataCache.Clear();

        // Cache static objects
        foreach (GameObject obj in staticObjects)
        {
            if (obj != null)
            {
                CacheObject(obj, false);
            }
        }

        // Cache active alarm objects
        foreach (GameObject obj in activeElements)
        {
            if (obj != null)
            {
                CacheObject(obj, true);
            }
        }

        Debug.Log($"MaterialTransitionManager: Cached {objectDataCache.Count} objects ({staticObjects.Count} static, {activeElements.Count} active)");
    }

    private void CacheObject(GameObject obj, bool isActiveElement)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = obj.GetComponentInChildren<Renderer>();
        }

        if (renderer != null && !objectDataCache.ContainsKey(obj))
        {
            ObjectData data = new ObjectData
            {
                renderer = renderer,
                originalMaterials = renderer.sharedMaterials,
                isActiveElement = isActiveElement
            };

            objectDataCache.Add(obj, data);
        }
    }

    public void ToggleHighlight()
    {
        if (isHighlightActive)
        {
            DeactivateHighlight();
        }
        else
        {
            ActivateHighlight();
        }
    }

    public void ActivateHighlight()
    {
        if (isHighlightActive)
        {
            Debug.LogWarning("MaterialTransitionManager: Highlight already active");
            return;
        }

        isHighlightActive = true;
        StopAllCoroutines();

        foreach (var kvp in objectDataCache)
        {
            GameObject obj = kvp.Key;
            ObjectData data = kvp.Value;

            if (obj == null || data.renderer == null) continue;

            // Change material based on object type
            Material targetMaterial = data.isActiveElement ? importantMaterial : nonImportantMaterial;
            ChangeMaterial(data.renderer, targetMaterial, data.isActiveElement);

            // Create lightning strike for active alarm objects only
            if (data.isActiveElement && enableLightning)
            {
                CreateLightningStrike(obj, data);
                StartCoroutine(AnimateLightningStrike(data));
            }
        }

        // Activate scroll through button
        if (cameraObjectCycler != null)
        {
            cameraObjectCycler.ShowScrollThroughButton();
        }

        Debug.Log("MaterialTransitionManager: Highlight activated");
    }

    public void DeactivateHighlight()
    {
        if (!isHighlightActive)
        {
            Debug.LogWarning("MaterialTransitionManager: No active highlight to deactivate");
            return;
        }

        isHighlightActive = false;
        StopAllCoroutines();

        foreach (var kvp in objectDataCache)
        {
            GameObject obj = kvp.Key;
            ObjectData data = kvp.Value;

            if (obj == null || data.renderer == null) continue;

            // Revert to original materials
            RevertMaterial(data.renderer, data.originalMaterials);

            // Remove lightning strike
            RemoveLightningStrike(data);
        }

        // Hide scroll through button
        if (cameraObjectCycler != null)
        {
            cameraObjectCycler.HideScrollThroughButton();
        }

        Debug.Log("MaterialTransitionManager: Highlight deactivated");
    }

    private void ChangeMaterial(Renderer renderer, Material targetMaterial, bool isActiveElement)
    {
        // Create material array with target material for all slots
        Material[] newMaterials = new Material[renderer.materials.Length];
        for (int i = 0; i < newMaterials.Length; i++)
        {
            newMaterials[i] = targetMaterial;
        }

        // Assign materials immediately
        renderer.materials = newMaterials;

        // Configure shader properties for custom AlarmIndicator shader
        if (useCustomShader && isActiveElement)
        {
            SetupCustomShaderProperties(renderer);
        }
        else if (!useCustomShader)
        {
            // Only do color transition for standard shaders that have _Color property
            StartCoroutine(TransitionColor(renderer, targetMaterial.color, isActiveElement));
        }
    }

    private void SetupCustomShaderProperties(Renderer renderer)
    {
        Material[] materials = renderer.materials;

        foreach (Material mat in materials)
        {
            if (mat != null)
            {
                // Set properties for AlarmIndicator shader
                if (mat.HasProperty("_GlowIntensity"))
                    mat.SetFloat("_GlowIntensity", shaderGlowIntensity);

                if (mat.HasProperty("_PulseSpeed"))
                    mat.SetFloat("_PulseSpeed", shaderPulseSpeed);

                if (mat.HasProperty("_PulseIntensity"))
                    mat.SetFloat("_PulseIntensity", 0.8f);

                if (mat.HasProperty("_EmissionColor"))
                {
                    Color hdrEmission = mat.GetColor("_AlarmColor") * emissionIntensity;
                    mat.SetColor("_EmissionColor", hdrEmission);
                }

                if (mat.HasProperty("_CoreBrightness"))
                    mat.SetFloat("_CoreBrightness", 3f);

                if (mat.HasProperty("_FresnelPower"))
                    mat.SetFloat("_FresnelPower", 2f);
            }
        }
    }

    private void RevertMaterial(Renderer renderer, Material[] originalMaterials)
    {
        // Revert to original materials
        renderer.materials = originalMaterials;

        // Disable emission if it was enabled
        foreach (Material mat in renderer.materials)
        {
            if (mat != null && mat.HasProperty("_EmissionColor"))
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private IEnumerator TransitionColor(Renderer renderer, Color targetColor, bool isActiveElement)
    {
        float elapsed = 0f;
        Material[] materials = renderer.materials;
        Color[] startColors = new Color[materials.Length];
        bool[] hasColorProperty = new bool[materials.Length];

        // Store starting colors and check if materials have _Color property
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && materials[i].HasProperty("_Color"))
            {
                startColors[i] = materials[i].color;
                hasColorProperty[i] = true;
            }
            else
            {
                hasColorProperty[i] = false;
            }
        }

        // Smooth color transition (only for materials with _Color property)
        while (elapsed < colorTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / colorTransitionDuration;

            for (int i = 0; i < materials.Length; i++)
            {
                if (hasColorProperty[i] && materials[i] != null)
                {
                    materials[i].color = Color.Lerp(startColors[i], targetColor, t);
                }
            }

            yield return null;
        }

        // Ensure final color is set
        for (int i = 0; i < materials.Length; i++)
        {
            if (hasColorProperty[i] && materials[i] != null)
            {
                materials[i].color = targetColor;
            }
        }

        // Start emission pulse for active elements only
        if (isActiveElement && useEmissionPulse)
        {
            StartCoroutine(PulseEmission(renderer, targetColor));
        }
    }

    private IEnumerator PulseEmission(Renderer renderer, Color baseColor)
    {
        // For custom shader, pulse is already built-in, just keep it alive
        if (useCustomShader)
        {
            // Custom shader handles its own pulsing, just wait
            while (isHighlightActive)
            {
                yield return null;
            }
            yield break;
        }

        // Standard Unity shader emission handling
        Material[] materials = renderer.materials;

        // Enable emission on all materials
        foreach (Material mat in materials)
        {
            if (mat != null && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                // Enable HDR emission for URP
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        float time = 0f;

        while (isHighlightActive)
        {
            time += Time.deltaTime;
            // Pulse between half intensity and full intensity
            float intensityMultiplier = Mathf.Lerp(0.5f, 1f, (Mathf.Sin(time * 2f) + 1f) / 2f);
            float currentIntensity = emissionIntensity * intensityMultiplier;

            foreach (Material mat in materials)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    // Multiply color by intensity for bright emission (HDR color)
                    Color emissionColor = baseColor * Mathf.LinearToGammaSpace(currentIntensity);
                    mat.SetColor("_EmissionColor", emissionColor);
                }
            }

            yield return null;
        }

        // Turn off emission when done
        foreach (Material mat in materials)
        {
            if (mat != null && mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
        }
    }

    public void AddStaticObject(GameObject obj)
    {
        if (obj != null && !staticObjects.Contains(obj))
        {
            staticObjects.Add(obj);
            CacheObject(obj, false);
        }
    }

    public void AddActiveElement(GameObject obj)
    {
        if (obj != null && !activeElements.Contains(obj))
        {
            activeElements.Add(obj);
            CacheObject(obj, true);
        }
    }

    public bool IsActiveAlarmObject(GameObject obj)
    {
        if (obj == null) return false;

        // Check if object or any of its parents is in activeElements list
        Transform current = obj.transform;
        while (current != null)
        {
            if (activeElements.Contains(current.gameObject))
            {
                return true;
            }
            current = current.parent;
        }

        return false;
    }

    private void CreateLightningStrike(GameObject targetObject, ObjectData data)
    {
        if (lightningMaterial == null)
        {
            Debug.LogWarning("MaterialTransitionManager: Lightning material not assigned!");
            return;
        }

        // Create a new GameObject for the lightning
        GameObject lightningObject = new GameObject($"Lightning_{targetObject.name}");
        lightningObject.transform.SetParent(targetObject.transform, false);

        // Add and configure LineRenderer
        LineRenderer lineRenderer = lightningObject.AddComponent<LineRenderer>();
        lineRenderer.material = lightningMaterial;
        lineRenderer.startWidth = lightningWidth;
        lineRenderer.endWidth = lightningWidth * 0.5f; // Thinner at bottom
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;

        // Set render settings
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.sortingOrder = 1000; // Render on top

        // Calculate positions
        Vector3 objectPosition = targetObject.transform.position;
        Vector3 startPoint = objectPosition + new Vector3(0, lightningHeight + lightningYOffset, 0);
        Vector3 endPoint = objectPosition + new Vector3(0, lightningYOffset, 0);

        // Set line positions
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // Store reference
        data.lightningStrike = lineRenderer;

        Debug.Log($"Lightning strike created for {targetObject.name}");
    }

    private IEnumerator AnimateLightningStrike(ObjectData data)
    {
        if (data.lightningStrike == null) yield break;

        while (isHighlightActive && data.lightningStrike != null)
        {
            // Flash ON - enable the lightning
            if (data.lightningStrike != null)
            {
                data.lightningStrike.enabled = true;
            }

            yield return new WaitForSeconds(lightningFlashDuration);

            // Flash OFF - disable the lightning
            if (data.lightningStrike != null)
            {
                data.lightningStrike.enabled = false;
            }

            yield return new WaitForSeconds(lightningOffDuration);
        }
    }

    private void RemoveLightningStrike(ObjectData data)
    {
        if (data.lightningStrike != null)
        {
            if (data.lightningStrike.gameObject != null)
            {
                Destroy(data.lightningStrike.gameObject);
            }
            data.lightningStrike = null;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(ToggleHighlight);
        }

        // Clean up all lightning strikes
        foreach (var data in objectDataCache.Values)
        {
            RemoveLightningStrike(data);
        }

        StopAllCoroutines();
    }
}
