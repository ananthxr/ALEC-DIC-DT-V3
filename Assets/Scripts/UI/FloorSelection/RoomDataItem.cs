using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component attached to RoomDataPrefab
/// Displays room information and handles room button clicks
/// Similar to AlarmItemUI but for room data
/// </summary>
public class RoomDataItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI floorText; // Optional: Separate text for floor
    [SerializeField] private Button buttonComponent;
    [SerializeField] private Image buttonImage;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.5f, 0.8f, 1f, 1f); // Light blue
    [SerializeField] private Color hoverColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Light gray

    // Stored room data
    private RoomData roomData;
    private bool isSelected = false;

    // Optional: Floor transition integration
    [Header("Optional: Floor Transition")]
    [SerializeField] private FloorTransitionManager floorTransitionManager;

    private void Awake()
    {
        // Auto-find components if not assigned
        if (buttonComponent == null)
            buttonComponent = GetComponent<Button>();

        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        if (roomNameText == null)
            roomNameText = GetComponentInChildren<TextMeshProUGUI>();

        // Setup button listener
        if (buttonComponent != null)
        {
            buttonComponent.onClick.AddListener(OnRoomButtonClicked);
        }
    }

    private void Start()
    {
        // Find FloorTransitionManager if not assigned
        if (floorTransitionManager == null)
        {
            floorTransitionManager = FindObjectOfType<FloorTransitionManager>();
        }

        // Set initial visual state
        UpdateVisualState();
    }

    /// <summary>
    /// Set the room data to display
    /// Called by RoomScrollViewController when populating
    /// </summary>
    public void SetRoomData(RoomData data)
    {
        roomData = data;

        if (roomData != null)
        {
            // Display room name
            if (roomNameText != null)
            {
                roomNameText.text = roomData.Name;
            }

            // Display floor (if separate text component exists)
            if (floorText != null)
            {
                floorText.text = roomData.Floor;
            }
            // If no separate floor text, combine both in roomNameText
            else if (roomNameText != null)
            {
                roomNameText.text = $"{roomData.Name}\n<size=18><color=#888888>{roomData.Floor}</color></size>";
            }
        }

        Debug.Log($"[RoomDataItem] Set room data: {roomData?.Name ?? "NULL"} | Floor: {roomData?.Floor ?? "NULL"}");
    }

    private void OnRoomButtonClicked()
    {
        if (roomData == null)
        {
            Debug.LogWarning("[RoomDataItem] Room data is null - cannot process click");
            return;
        }

        Debug.Log($"[RoomDataItem] Room clicked: {roomData.Name} (Entity ID: {roomData.EntityID})");

        // Toggle selection
        isSelected = !isSelected;
        UpdateVisualState();

        // Move camera to room using registry
        MoveToRoom();
    }

    private void MoveToRoom()
    {
        // Check if registry and camera controller exist
        if (RoomCameraRegistry.Instance == null)
        {
            Debug.LogError("[RoomDataItem] RoomCameraRegistry.Instance is null!");
            return;
        }

        if (CameraController.Instance == null)
        {
            Debug.LogError("[RoomDataItem] CameraController.Instance is null!");
            return;
        }

        // Get camera position from registry (O(1) lookup)
        Transform cameraPlaceholder = RoomCameraRegistry.Instance.GetRoomCamera(roomData.EntityID);

        if (cameraPlaceholder == null)
        {
            Debug.LogWarning($"[RoomDataItem] No camera placeholder found for room: {roomData.Name}");
            return;
        }

        Debug.Log($"[RoomDataItem] Moving camera to room: {roomData.Name}");

        // Move camera to room with room inspection mode enabled (1 second duration)
        // This locks the camera position and allows first-person rotation to look around
        CameraController.Instance.MoveToRoomInspectionMode(cameraPlaceholder, 1.0f, () =>
        {
            Debug.Log($"[RoomDataItem] Camera arrived at room: {roomData.Name} - Right-click to look around");
        });
    }

    private void UpdateVisualState()
    {
        if (buttonImage != null)
        {
            buttonImage.color = isSelected ? selectedColor : normalColor;
        }
    }

    // Public method to set selection from outside
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisualState();
    }

    // Public properties
    public RoomData RoomData => roomData;
    public bool IsSelected => isSelected;
    public string RoomName => roomData?.Name ?? "Unknown";
    public string RoomFloor => roomData?.Floor ?? "Unknown";
    public string EntityID => roomData?.EntityID ?? "";

    private void OnDestroy()
    {
        if (buttonComponent != null)
        {
            buttonComponent.onClick.RemoveListener(OnRoomButtonClicked);
        }
    }

    // Optional: Hover effects (requires EventTrigger or pointer event interfaces)
    public void OnPointerEnter()
    {
        if (buttonImage != null && !isSelected)
        {
            buttonImage.color = hoverColor;
        }
    }

    public void OnPointerExit()
    {
        UpdateVisualState();
    }
}
