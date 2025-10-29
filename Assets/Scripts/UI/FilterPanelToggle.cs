using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles toggling the filter panel on/off with a filter button
/// The button acts as a toggle - click to show, click again to hide
/// </summary>
public class FilterPanelToggle : MonoBehaviour
{
    [Header("=== UI REFERENCES ===")]
    [SerializeField] private Button filterButton;
    [SerializeField] private GameObject filterPanel;

    [Header("=== BUTTON VISUAL FEEDBACK (OPTIONAL) ===")]
    [Tooltip("Optional: Image component to change color when active/inactive")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Color activeColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Green when active
    [SerializeField] private Color inactiveColor = Color.white; // White when inactive

    [Header("=== BUTTON ICON SWAP (OPTIONAL) ===")]
    [Tooltip("Optional: Swap button icon when toggled")]
    [SerializeField] private Image buttonIcon;
    [SerializeField] private Sprite filterActiveIcon;
    [SerializeField] private Sprite filterInactiveIcon;

    // Track panel state
    private bool isPanelActive = false;

    private void Start()
    {
        // Validate references
        if (filterButton == null)
        {
            Debug.LogError("[FilterPanelToggle] Filter button is not assigned!");
            return;
        }

        if (filterPanel == null)
        {
            Debug.LogError("[FilterPanelToggle] Filter panel is not assigned!");
            return;
        }

        // Add button listener
        filterButton.onClick.AddListener(OnFilterButtonClicked);

        // Initialize panel state (start hidden)
        SetPanelState(false);

        Debug.Log("[FilterPanelToggle] Initialized successfully");
    }

    private void OnFilterButtonClicked()
    {
        // Toggle the state
        isPanelActive = !isPanelActive;

        Debug.Log($"[FilterPanelToggle] Filter button clicked - Panel now {(isPanelActive ? "ACTIVE" : "INACTIVE")}");

        // Update panel visibility
        SetPanelState(isPanelActive);
    }

    private void SetPanelState(bool isActive)
    {
        isPanelActive = isActive;

        // Show/hide the filter panel
        if (filterPanel != null)
        {
            filterPanel.SetActive(isActive);
        }

        // Update button visual feedback (if assigned)
        UpdateButtonVisuals(isActive);
    }

    private void UpdateButtonVisuals(bool isActive)
    {
        // Update button color (if buttonImage is assigned)
        if (buttonImage != null)
        {
            buttonImage.color = isActive ? activeColor : inactiveColor;
        }

        // Update button icon (if buttonIcon and sprites are assigned)
        if (buttonIcon != null)
        {
            if (isActive && filterActiveIcon != null)
            {
                buttonIcon.sprite = filterActiveIcon;
            }
            else if (!isActive && filterInactiveIcon != null)
            {
                buttonIcon.sprite = filterInactiveIcon;
            }
        }
    }

    /// <summary>
    /// Public method to programmatically show the filter panel
    /// </summary>
    public void ShowFilterPanel()
    {
        SetPanelState(true);
    }

    /// <summary>
    /// Public method to programmatically hide the filter panel
    /// </summary>
    public void HideFilterPanel()
    {
        SetPanelState(false);
    }

    /// <summary>
    /// Public method to programmatically set panel state
    /// </summary>
    public void SetFilterPanelActive(bool isActive)
    {
        SetPanelState(isActive);
    }

    /// <summary>
    /// Public property to check if panel is currently active
    /// </summary>
    public bool IsPanelActive => isPanelActive;

    private void OnDestroy()
    {
        // Clean up button listener
        if (filterButton != null)
        {
            filterButton.onClick.RemoveListener(OnFilterButtonClicked);
        }
    }
}
