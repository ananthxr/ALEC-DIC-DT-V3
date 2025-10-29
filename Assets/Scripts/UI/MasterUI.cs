using UnityEngine;
using System;
using System.Collections.Generic;

public class MasterUI : MonoBehaviour
{
    [Header("UI Element Tracking")]
    [SerializeField] private bool debugMode = true;

    [Header("Manager References")]
    [SerializeField] private FloorTransitionManager floorTransitionManager;
    [SerializeField] private CameraController cameraController;

    [Header("Ground Plane Reference")]
    [SerializeField] private GameObject groundPlane; // Reference to ground plane (optional - FloorTransitionManager handles it)

    // Dictionary to track UI element states by name
    private Dictionary<string, UIElementState> uiElementStates = new Dictionary<string, UIElementState>();

    // Events for state changes
    public event Action<string, bool> OnUIElementVisibilityChanged;
    public event Action<string, UIElementState> OnUIElementStateChanged;

    // Singleton instance for easy access
    private static MasterUI instance;
    public static MasterUI Instance => instance;

    private void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("[MasterUI] Multiple MasterUI instances detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Debug.Log("[MasterUI] Master UI controller initialized");
        AutoFindManagers();
    }

    private void AutoFindManagers()
    {
        // Auto-find managers if not assigned
        if (floorTransitionManager == null)
        {
            floorTransitionManager = FindObjectOfType<FloorTransitionManager>();
            if (floorTransitionManager != null)
            {
                Debug.Log("[MasterUI] Auto-found FloorTransitionManager");
            }
        }

        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
            if (cameraController != null)
            {
                Debug.Log("[MasterUI] Auto-found CameraController");
            }
        }
    }

    // Called by SlidingPanelController when panel state changes
    public void OnPanelStateChanged(bool isVisible)
    {
        string panelName = "SlidingPanel";
        UpdateUIElementState(panelName, isVisible);

        if (debugMode)
        {
            Debug.Log($"[MasterUI] Panel state changed - {panelName}: visible={isVisible}");
        }
    }

    // Called by FloorButtonStacking when floor button state changes
    public void UpdateFloorButtonState(int floorIndex, bool isSelected)
    {
        string floorName = GetFloorName(floorIndex);
        UpdateUIElementState(floorName, isSelected, $"Floor {floorIndex}");

        if (debugMode)
        {
            Debug.Log($"[MasterUI] Floor button state changed - {floorName}: selected={isSelected}");
        }
    }

    // Helper method to get floor display names
    private string GetFloorName(int floorIndex)
    {
        return floorIndex switch
        {
            0 => "GroundFloor",
            1 => "FirstFloor",
            2 => "RoofFloor",
            _ => $"Floor{floorIndex}"
        };
    }

    // Generic method to update any UI element state
    public void UpdateUIElementState(string elementName, bool isVisible, string additionalInfo = "")
    {
        // Get or create state for this element
        if (!uiElementStates.ContainsKey(elementName))
        {
            uiElementStates[elementName] = new UIElementState
            {
                elementName = elementName,
                isVisible = isVisible,
                lastUpdateTime = Time.time,
                additionalInfo = additionalInfo
            };
        }
        else
        {
            // Update existing state
            uiElementStates[elementName].isVisible = isVisible;
            uiElementStates[elementName].lastUpdateTime = Time.time;
            uiElementStates[elementName].additionalInfo = additionalInfo;
        }

        // Invoke events
        OnUIElementVisibilityChanged?.Invoke(elementName, isVisible);
        OnUIElementStateChanged?.Invoke(elementName, uiElementStates[elementName]);

        if (debugMode)
        {
            Debug.Log($"[MasterUI] UI Element '{elementName}' updated: visible={isVisible}, info='{additionalInfo}'");
        }
    }

    // Query methods to check UI element states
    public bool IsUIElementVisible(string elementName)
    {
        if (uiElementStates.TryGetValue(elementName, out UIElementState state))
        {
            return state.isVisible;
        }
        return false;
    }

    public UIElementState GetUIElementState(string elementName)
    {
        if (uiElementStates.TryGetValue(elementName, out UIElementState state))
        {
            return state;
        }
        return null;
    }

    public Dictionary<string, UIElementState> GetAllUIStates()
    {
        return new Dictionary<string, UIElementState>(uiElementStates);
    }

    // Debug method to print all current UI states
    [ContextMenu("Print All UI States")]
    public void PrintAllUIStates()
    {
        Debug.Log($"[MasterUI] === Current UI States ({uiElementStates.Count} elements) ===");
        foreach (var kvp in uiElementStates)
        {
            Debug.Log($"[MasterUI] {kvp.Key}: visible={kvp.Value.isVisible}, lastUpdate={kvp.Value.lastUpdateTime}, info='{kvp.Value.additionalInfo}'");
        }
    }

    // Method to clear a specific UI element state
    public void ClearUIElementState(string elementName)
    {
        if (uiElementStates.ContainsKey(elementName))
        {
            uiElementStates.Remove(elementName);
            Debug.Log($"[MasterUI] Cleared state for: {elementName}");
        }
    }

    // Method to clear all UI element states
    public void ClearAllStates()
    {
        uiElementStates.Clear();
        Debug.Log("[MasterUI] All UI states cleared");
    }

    // === MASTER CONTROL METHODS ===

    /// <summary>
    /// Master method to reset entire UI system to default state
    /// </summary>
    public void ResetToDefaultState()
    {
        Debug.Log("[MasterUI] 🔄 Resetting entire UI system to default state");

        // Reset floor transitions (this will also show the ground plane)
        if (floorTransitionManager != null)
        {
            floorTransitionManager.ResetToNoneState();
        }

        // Camera will be reset by FloorTransitionManager
        // Clear UI states
        ClearAllStates();

        Debug.Log("[MasterUI] ✅ UI system reset complete");
        Debug.Log("[MasterUI] Note: Ground plane visibility is managed by FloorTransitionManager");
    }

    /// <summary>
    /// Master method to select a floor with full coordination
    /// NOTE: Floor buttons now directly trigger FloorTransitionManager via button references in FloorData
    /// This method can still be called programmatically if needed
    /// </summary>
    public void SelectFloor(int floorIndex)
    {
        Debug.Log($"[MasterUI] Master command: Select floor {floorIndex}");
        Debug.Log($"[MasterUI] Note: Floor buttons are now wired directly to FloorTransitionManager.OnFloorButtonClicked()");

        if (floorTransitionManager != null)
        {
            floorTransitionManager.SelectFloor(floorIndex);
            UpdateUIElementState($"Floor{floorIndex}", true, $"Master selected floor {floorIndex}");
        }
        else
        {
            Debug.LogError("[MasterUI] Cannot select floor - FloorTransitionManager not found!");
        }
    }

    /// <summary>
    /// Get current floor state from FloorTransitionManager
    /// </summary>
    public FloorState GetCurrentFloorState()
    {
        if (floorTransitionManager != null)
        {
            return floorTransitionManager.CurrentState;
        }
        return FloorState.None;
    }

    /// <summary>
    /// Get current camera view state from FloorTransitionManager
    /// </summary>
    public CameraViewState GetCurrentCameraView()
    {
        if (floorTransitionManager != null)
        {
            return floorTransitionManager.CurrentCameraView;
        }
        return CameraViewState.Default;
    }

    /// <summary>
    /// Check if any transition is currently happening
    /// </summary>
    public bool IsSystemTransitioning()
    {
        bool floorTransitioning = floorTransitionManager != null && floorTransitionManager.IsTransitioning;
        bool cameraTransitioning = cameraController != null && cameraController.IsTransitioning;

        return floorTransitioning || cameraTransitioning;
    }

    /// <summary>
    /// Check if ground plane is currently visible
    /// </summary>
    public bool IsGroundPlaneVisible()
    {
        if (groundPlane != null)
        {
            return groundPlane.activeSelf;
        }
        Debug.LogWarning("[MasterUI] Ground plane reference not assigned");
        return false;
    }

    /// <summary>
    /// Get system status summary for debugging
    /// </summary>
    [ContextMenu("Print System Status")]
    public void PrintSystemStatus()
    {
        Debug.Log("=== MASTER UI SYSTEM STATUS ===");
        Debug.Log($"Floor State: {GetCurrentFloorState()}");
        Debug.Log($"Camera View: {GetCurrentCameraView()}");
        Debug.Log($"Is Transitioning: {IsSystemTransitioning()}");
        Debug.Log($"Ground Plane Visible: {IsGroundPlaneVisible()}");
        Debug.Log($"UI Elements Tracked: {uiElementStates.Count}");
        Debug.Log("================================");
    }

    // Public accessors for managers
    public FloorTransitionManager FloorTransitionManager => floorTransitionManager;
    public CameraController CameraController => cameraController;
    public GameObject GroundPlane => groundPlane;
}

// Data structure to hold UI element state information
[System.Serializable]
public class UIElementState
{
    public string elementName;
    public bool isVisible;
    public float lastUpdateTime;
    public string additionalInfo;

    public override string ToString()
    {
        return $"{elementName}: visible={isVisible}, updated={lastUpdateTime}, info='{additionalInfo}'";
    }
}
