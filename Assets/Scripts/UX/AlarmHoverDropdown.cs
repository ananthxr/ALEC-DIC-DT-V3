using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AlarmHoverDropdown : MonoBehaviour
{
    [Header("Dropdown Settings")]
    [SerializeField] private GameObject dropdownPrefab; // Screen space canvas prefab with all UI inside
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private Vector2 dropdownSize = new Vector2(200f, 100f); // Width x Height of the dropdown panel

    [Header("Positioning")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(-10, -10); // Offset from cursor (top-right corner at cursor)

    [Header("References")]

    private GameObject currentDropdown;
    private GameObject lastHoveredObject;
    private Camera mainCamera;
    private Canvas dropdownCanvas;
    private RectTransform dropdownRectTransform;

    // Cached components from dropdown prefab
    private TextMeshProUGUI locationText;
    private Button actionButton;

    private void Awake()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("AlarmHoverDropdown: No main camera found!");
        }
    }

    private void Update()
    {
        // Perform raycast from mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            GameObject hoveredObject = hit.collider.gameObject;

            // Check if this is a new object or same object
            if (hoveredObject != lastHoveredObject)
            {
                lastHoveredObject = hoveredObject;
                ShowDropdown(hoveredObject);
            }

            // Update dropdown position to follow cursor
            if (currentDropdown != null && currentDropdown.activeSelf)
            {
                UpdateDropdownPosition();
            }
        }
        else
        {
            // No object hit, hide dropdown
            HideDropdown();
            lastHoveredObject = null;
        }
    }

    private void ShowDropdown(GameObject alarmObject)
    {
        if (dropdownPrefab == null)
        {
            Debug.LogWarning("AlarmHoverDropdown: Dropdown prefab not assigned!");
            return;
        }

        // Create dropdown if it doesn't exist
        if (currentDropdown == null)
        {
            // Instantiate world space canvas prefab (it's self-contained)
            currentDropdown = Instantiate(dropdownPrefab);

            // Get canvas component
            dropdownCanvas = currentDropdown.GetComponent<Canvas>();
            if (dropdownCanvas == null)
            {
                dropdownCanvas = currentDropdown.GetComponentInChildren<Canvas>();
            }

            if (dropdownCanvas == null)
            {
                Debug.LogError("AlarmHoverDropdown: No Canvas found in dropdown prefab!");
                return;
            }

            // Setup canvas based on render mode
            if (dropdownCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                // Assign camera for screen space camera mode
                dropdownCanvas.worldCamera = mainCamera;
            }
            else if (dropdownCanvas.renderMode == RenderMode.WorldSpace)
            {
                // Set camera reference at runtime
                dropdownCanvas.worldCamera = mainCamera;

                // Scale down the canvas to be small in world space
                currentDropdown.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            }

            // Get RectTransform of the PANEL inside canvas (not the canvas itself)
            // This is important for correct positioning
            RectTransform[] rectTransforms = currentDropdown.GetComponentsInChildren<RectTransform>();
            foreach (RectTransform rt in rectTransforms)
            {
                // Find the panel (not the canvas root)
                if (rt.gameObject != currentDropdown && rt.GetComponent<Canvas>() == null)
                {
                    dropdownRectTransform = rt;
                    break;
                }
            }

            if (dropdownRectTransform != null)
            {
                // Fix the panel size - set anchors to center point and use sizeDelta
                dropdownRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                dropdownRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dropdownRectTransform.pivot = new Vector2(1f, 1f); // Top-right corner

                // Set size from inspector value
                dropdownRectTransform.sizeDelta = dropdownSize;
            }

            // Cache components from prefab
            locationText = currentDropdown.GetComponentInChildren<TextMeshProUGUI>();
            actionButton = currentDropdown.GetComponentInChildren<Button>();

            if (locationText == null)
            {
                Debug.LogWarning("AlarmHoverDropdown: No TextMeshProUGUI found in dropdown prefab!");
            }

            if (actionButton == null)
            {
                Debug.LogWarning("AlarmHoverDropdown: No Button found in dropdown prefab!");
            }
        }

        // Show dropdown
        currentDropdown.SetActive(true);

        // Update information
        UpdateDropdownInfo(alarmObject);

        // Position at cursor
        UpdateDropdownPosition();
    }

    private void UpdateDropdownInfo(GameObject alarmObject)
    {
        if (locationText != null)
        {
            // Set location/description text
            // You can customize this to get info from a component on the alarm object
            string locationInfo = $"Location: {alarmObject.name}";

            // Try to get additional info if alarm object has a data component
            // Example: AlarmData component (you can create this later)
            // AlarmData alarmData = alarmObject.GetComponent<AlarmData>();
            // if (alarmData != null)
            // {
            //     locationInfo = alarmData.location;
            // }

            locationText.text = locationInfo;
        }
    }

    private void UpdateDropdownPosition()
    {
        if (dropdownCanvas == null || dropdownRectTransform == null) return;

        // Check if using Screen Space or World Space canvas
        if (dropdownCanvas.renderMode == RenderMode.ScreenSpaceOverlay ||
            dropdownCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            // Screen space positioning
            Vector2 mousePosition = Input.mousePosition;

            // Get the panel's rect (actual size)
            Rect panelRect = dropdownRectTransform.rect;

            // Position so top-right corner is at cursor
            // Since pivot is (1, 1), we just need to set position to cursor + offset
            Vector2 dropdownPosition = mousePosition + cursorOffset;

            // Apply position
            dropdownRectTransform.position = dropdownPosition;

            // Clamp to screen to prevent going off-screen
            ClampToScreen();
        }
        else if (dropdownCanvas.renderMode == RenderMode.WorldSpace)
        {
            // World space positioning - follow mouse in world
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            // Position at a fixed distance from camera
            float distanceFromCamera = 5f; // Adjust as needed
            Vector3 worldPosition = ray.GetPoint(distanceFromCamera);

            currentDropdown.transform.position = worldPosition;
        }
    }

    private void ClampToScreen()
    {
        if (dropdownRectTransform == null) return;

        Vector3[] corners = new Vector3[4];
        dropdownRectTransform.GetWorldCorners(corners);

        Vector2 dropdownSize = dropdownRectTransform.sizeDelta;
        Vector2 currentPos = dropdownRectTransform.position;

        // Clamp to screen boundaries
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // Check left edge
        if (corners[0].x < 0)
        {
            currentPos.x += Mathf.Abs(corners[0].x);
        }

        // Check right edge
        if (corners[2].x > screenWidth)
        {
            currentPos.x -= (corners[2].x - screenWidth);
        }

        // Check bottom edge
        if (corners[0].y < 0)
        {
            currentPos.y += Mathf.Abs(corners[0].y);
        }

        // Check top edge
        if (corners[2].y > screenHeight)
        {
            currentPos.y -= (corners[2].y - screenHeight);
        }

        dropdownRectTransform.position = currentPos;
    }

    private void HideDropdown()
    {
        if (currentDropdown != null)
        {
            currentDropdown.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (currentDropdown != null)
        {
            Destroy(currentDropdown);
        }
    }
}
