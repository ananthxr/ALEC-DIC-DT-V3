using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Individual room button component within the scrollable room selection view.
/// Handles room selection and visual feedback.
///
/// NOTE: This is a basic button component for UI functionality.
/// You can extend it later to integrate with FloorTransitionManager or other systems.
/// </summary>
public class RoomButton : MonoBehaviour
{
    [Header("Room Information")]
    [SerializeField] private string roomName;
    [SerializeField] private int roomID;
    [SerializeField] private string roomDescription;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.cyan;
    [SerializeField] private Color hoverColor = Color.gray;

    [Header("References")]
    [SerializeField] private Button buttonComponent;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Text roomNameText; // Optional: For displaying room name

    [Header("Optional: Floor Transition Integration")]
    [Tooltip("Optional: Assign FloorTransitionManager if you want to trigger floor transitions")]
    [SerializeField] private FloorTransitionManager floorTransitionManager;
    [Tooltip("Optional: Floor index to transition to when this room is selected")]
    [SerializeField] private int targetFloorIndex = -1;

    private bool isSelected = false;

    private void Awake()
    {
        // Auto-find components if not assigned
        if (buttonComponent == null)
            buttonComponent = GetComponent<Button>();

        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        // Setup button listener
        if (buttonComponent != null)
        {
            buttonComponent.onClick.AddListener(OnRoomButtonClicked);
        }
    }

    private void Start()
    {
        // Find FloorTransitionManager in scene if not assigned
        if (floorTransitionManager == null)
        {
            floorTransitionManager = FindObjectOfType<FloorTransitionManager>();
        }

        // Update visual state
        UpdateVisualState();

        // Set room name text if available
        if (roomNameText != null)
        {
            roomNameText.text = roomName;
        }
    }

    private void OnRoomButtonClicked()
    {
        Debug.Log($"[RoomButton] Room button clicked: {roomName} (ID: {roomID})");

        // Toggle selection state
        isSelected = !isSelected;
        UpdateVisualState();

        // Optional: Trigger floor transition if configured
        if (floorTransitionManager != null && targetFloorIndex >= 0)
        {
            Debug.Log($"[RoomButton] Transitioning to floor index: {targetFloorIndex} for room: {roomName}");
            floorTransitionManager.SelectFloor(targetFloorIndex);
        }

        // You can add additional logic here when implementing functional aspects:
        // - Notifying a RoomManager
        // - Loading room-specific data
        // - Triggering camera transitions to specific room positions
        // - Highlighting room objects in the 3D scene
        // - Showing room details panel
    }

    private void UpdateVisualState()
    {
        if (buttonImage != null)
        {
            buttonImage.color = isSelected ? selectedColor : normalColor;
        }
    }

    // Public methods for external control
    public void SetRoomInfo(string name, int id, string description = "")
    {
        roomName = name;
        roomID = id;
        roomDescription = description;

        if (roomNameText != null)
        {
            roomNameText.text = roomName;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisualState();
    }

    public string RoomName => roomName;
    public int RoomID => roomID;
    public bool IsSelected => isSelected;

    private void OnDestroy()
    {
        if (buttonComponent != null)
        {
            buttonComponent.onClick.RemoveListener(OnRoomButtonClicked);
        }
    }

    // Optional: Mouse hover effects
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
