using UnityEngine;

/// <summary>
/// Handles click events on HVAC sensor GameObjects
/// Uses WebSocket to discover equipment and fetch real-time data
/// Attach this to the Sensor Name GameObject (e.g., "White Space_Office 01_HVAC-Office 01")
/// </summary>
public class HVACClickHandler : MonoBehaviour
{
    [Header("Room Reference")]
    [Tooltip("The room's Entity ID - will be auto-found from parent hierarchy")]
    private string roomEntityID;

    [Header("Camera Settings")]
    [SerializeField] private Transform hvacCameraPlaceholder; // Reference to HVAC_Camera_Placeholder child

    [Header("Auto-Find Settings")]
    [Tooltip("If true, automatically finds HVAC_Camera_Placeholder in children on Start")]
    [SerializeField] private bool autoFindCameraPlaceholder = true;

    private void Start()
    {
        // Auto-find room Entity ID from parent hierarchy
        AutoFindRoomEntityID();

        // Auto-find camera placeholder in children
        if (autoFindCameraPlaceholder && hvacCameraPlaceholder == null)
        {
            // Search in Sensor ID GameObject (child of this Sensor Name GameObject)
            Transform sensorIDObject = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (sensorIDObject != null)
            {
                hvacCameraPlaceholder = sensorIDObject.Find("HVAC_Camera_Placeholder");

                if (hvacCameraPlaceholder == null)
                {
                    Debug.LogWarning($"[HVACClickHandler] HVAC_Camera_Placeholder not found in children of {gameObject.name}");
                }
                else
                {
                    Debug.Log($"[HVACClickHandler] ✓ Auto-found HVAC_Camera_Placeholder for {gameObject.name}");
                }
            }
        }

        // Ensure this GameObject or its children have a collider for clicking
        ValidateCollider();
    }

    /// <summary>
    /// Auto-find room Entity ID by navigating up the hierarchy
    /// Hierarchy: Sensor ID (this) → Sensor Name → Room Entity ID
    /// Script is attached to Sensor ID GameObject (e.g., "e365c270-c1d0-11ef-94c5-01236d0e69c4")
    /// </summary>
    private void AutoFindRoomEntityID()
    {
        // Navigate: this (Sensor ID) → parent (Sensor Name) → parent (Room Entity ID)
        Transform current = transform.parent; // Sensor Name GameObject
        if (current != null)
        {
            current = current.parent; // Room Entity ID GameObject
            if (current != null)
            {
                roomEntityID = current.name; // The GameObject name IS the Room Entity ID!
                Debug.Log($"[HVACClickHandler] ✓ Auto-found Room Entity ID: {roomEntityID}");
                return;
            }
        }

        Debug.LogWarning($"[HVACClickHandler] ✗ Could not auto-find Room Entity ID from hierarchy for {gameObject.name}");
    }


    private void ValidateCollider()
    {
        Collider col = GetComponentInChildren<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"[HVACClickHandler] No collider found on {gameObject.name} or its children. Click detection will not work!");
        }
    }


    /// <summary>
    /// Called when user clicks on the sensor (via OnMouseDown or Raycast)
    /// </summary>
    private void OnMouseDown()
    {
        HandleSensorClick();
    }

    /// <summary>
    /// Public method to trigger sensor click from external raycast systems
    /// </summary>
    public void OnSensorClicked()
    {
        HandleSensorClick();
    }

    private void HandleSensorClick()
    {
        // Validate requirements
        if (CameraController.Instance == null)
        {
            Debug.LogError("[HVACClickHandler] CameraController.Instance is null!");
            return;
        }

        if (hvacCameraPlaceholder == null)
        {
            Debug.LogWarning($"[HVACClickHandler] HVAC_Camera_Placeholder not assigned for {gameObject.name}");
            return;
        }

        if (string.IsNullOrEmpty(roomEntityID))
        {
            Debug.LogError($"[HVACClickHandler] Room Entity ID is empty! Cannot discover HVAC equipment.");
            return;
        }

        Debug.Log($"[HVACClickHandler] HVAC sensor clicked in room: {roomEntityID}");

        // Move camera to sensor's camera placeholder
        CameraController.Instance.MoveToSensorInspectionMode(
            hvacCameraPlaceholder,
            0.5f, // 0.5 second transition
            () =>
            {
                // Callback after camera arrives
                Debug.Log($"[HVACClickHandler] Camera arrived at HVAC sensor");

                // Show HVAC panel and request data via WebSocket
                ShowHVACPanelWithLiveData();
            }
        );
    }

    /// <summary>
    /// Show HVAC panel and request live data from WebSocket
    /// </summary>
    private void ShowHVACPanelWithLiveData()
    {
        Debug.Log("[HVACClickHandler] Requesting live HVAC data for room: " + roomEntityID);

        // Find panel
        HVACDetailPanel panel = HVACDetailPanel.Instance;
        if (panel == null)
        {
            panel = FindObjectOfType<HVACDetailPanel>(true);
        }

        if (panel == null)
        {
            Debug.LogError("[HVACClickHandler] ✗ HVACDetailPanel NOT FOUND in scene!");
            return;
        }

        // Show panel immediately with room Entity ID
        panel.ShowPanelAndRequestData(roomEntityID, gameObject.name);

        Debug.Log($"[HVACClickHandler] ✓ Panel shown, WebSocket data request initiated for room: {roomEntityID}");
    }

    // Public getters
    public string RoomEntityID => roomEntityID;
    public Transform CameraPlaceholder => hvacCameraPlaceholder;

    // Gizmo to visualize camera placeholder in Scene view
    private void OnDrawGizmosSelected()
    {
        if (hvacCameraPlaceholder != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(hvacCameraPlaceholder.position, 0.3f);
            Gizmos.DrawLine(transform.position, hvacCameraPlaceholder.position);
        }
    }
}
