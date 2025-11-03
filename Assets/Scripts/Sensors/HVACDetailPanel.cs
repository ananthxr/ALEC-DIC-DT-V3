using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Displays HVAC sensor details in a UI panel
/// Single reusable panel for all HVAC sensors
/// Attach this to a Canvas GameObject (panel on the right side)
/// </summary>
public class HVACDetailPanel : MonoBehaviour
{
    public static HVACDetailPanel Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject panelRoot; // The panel GameObject to activate/deactivate
    [SerializeField] private TextMeshProUGUI sensorNameText;
    [SerializeField] private TextMeshProUGUI sensorTypeText;

    [Header("HVAC Temperature Data")]
    [SerializeField] private TextMeshProUGUI ambientTemperatureText;
    [SerializeField] private TextMeshProUGUI setTemperatureText;

    [Header("Optional: Status Info")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI lastUpdatedText;

    [Header("Optional: Close Button")]
    [SerializeField] private Button closeButton;

    // Current sensor info
    private string currentRoomEntityID;
    private string currentSensorName;

    private void Awake()
    {
        Debug.Log($"[HVACDetailPanel] Awake called on GameObject: {gameObject.name}");

        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[HVACDetailPanel] Duplicate instance found! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log($"[HVACDetailPanel] ✓ Instance set successfully on {gameObject.name}");

        // Auto-find panel root if not assigned
        if (panelRoot == null)
        {
            Debug.Log("[HVACDetailPanel] panelRoot is null, attempting to auto-find first child...");
            panelRoot = transform.GetChild(0)?.gameObject;
            if (panelRoot != null)
            {
                Debug.Log($"[HVACDetailPanel] Auto-found panelRoot: {panelRoot.name}");
            }
            else
            {
                Debug.LogWarning("[HVACDetailPanel] Could not auto-find panelRoot (no children found)");
            }
        }
        else
        {
            Debug.Log($"[HVACDetailPanel] panelRoot already assigned: {panelRoot.name}");
        }

        // Setup close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HidePanel);
            Debug.Log("[HVACDetailPanel] Close button listener added");
        }
    }

    private void Start()
    {
        Debug.Log($"[HVACDetailPanel] Start called. Instance is {(Instance != null ? "SET" : "NULL")}");

        // Debug: Show which fields are assigned
        Debug.Log($"[HVACDetailPanel] Fields assigned: Name={sensorNameText != null}, Type={sensorTypeText != null}, AmbientTemp={ambientTemperatureText != null}, SetTemp={setTemperatureText != null}");

        // Hide panel initially
        if (panelRoot != null)
        {
            Debug.Log($"[HVACDetailPanel] Hiding panel initially: {panelRoot.name}");
            panelRoot.SetActive(false);
        }
        else
        {
            Debug.LogError("[HVACDetailPanel] ✗ panelRoot is NULL in Start! Panel will not work.");
        }
    }

    /// <summary>
    /// Show the panel and request live HVAC data via WebSocket
    /// Called by HVACClickHandler when user clicks HVAC sensor
    /// </summary>
    public void ShowPanelAndRequestData(string roomEntityID, string sensorName)
    {
        Debug.Log($"[HVACDetailPanel] ShowPanelAndRequestData called for room: {roomEntityID}, sensor: {sensorName}");

        if (string.IsNullOrEmpty(roomEntityID))
        {
            Debug.LogError("[HVACDetailPanel] Room Entity ID is null or empty - cannot request data");
            return;
        }

        // Store current sensor info
        currentRoomEntityID = roomEntityID;
        currentSensorName = sensorName;

        // Show panel immediately with sensor name
        if (sensorNameText != null)
        {
            sensorNameText.text = sensorName;
        }

        if (sensorTypeText != null)
        {
            sensorTypeText.text = "Type: HVAC";
        }

        // Show loading state for temperature data
        if (ambientTemperatureText != null)
        {
            ambientTemperatureText.text = "Ambient Temp: Loading...";
        }

        if (setTemperatureText != null)
        {
            setTemperatureText.text = "Set Temp: Loading...";
        }

        if (statusText != null)
        {
            statusText.text = "Status: Requesting data...";
        }

        // Show panel
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            Debug.Log($"[HVACDetailPanel] ✓ Panel activated");
        }

        // Request HVAC data via WebSocket
        RequestHVACData();
    }

    /// <summary>
    /// Request HVAC telemetry data from MasterAlarm via WebSocket
    /// </summary>
    private void RequestHVACData()
    {
        if (MasterAlarm.Instance == null)
        {
            Debug.LogError("[HVACDetailPanel] MasterAlarm.Instance is NULL - cannot request data");
            if (statusText != null)
            {
                statusText.text = "Status: Error - MasterAlarm not found";
            }
            return;
        }

        if (!MasterAlarm.Instance.IsAuthenticated)
        {
            Debug.LogError("[HVACDetailPanel] MasterAlarm is not authenticated - cannot request data");
            if (statusText != null)
            {
                statusText.text = "Status: Error - Not authenticated";
            }
            return;
        }

        Debug.Log($"[HVACDetailPanel] Requesting HVAC data for room: {currentRoomEntityID}");

        // Define telemetry keys for HVAC (must match ThingsBoard keys exactly)
        List<string> telemetryKeys = new List<string>
        {
            "Ambient Temperature",
            "Set Temperature"
        };

        // Request data from MasterAlarm
        MasterAlarm.Instance.RequestSensorData(
            currentRoomEntityID,
            "HVAC",
            telemetryKeys,
            OnHVACDataReceived
        );
    }

    /// <summary>
    /// Callback when HVAC data is received from WebSocket
    /// </summary>
    private void OnHVACDataReceived(Dictionary<string, string> telemetryData)
    {
        if (telemetryData == null)
        {
            Debug.LogWarning("[HVACDetailPanel] HVAC data is null - no data received");
            if (statusText != null)
            {
                statusText.text = "Status: No HVAC found in room";
            }
            if (ambientTemperatureText != null)
            {
                ambientTemperatureText.text = "Ambient Temp: N/A";
            }
            if (setTemperatureText != null)
            {
                setTemperatureText.text = "Set Temp: N/A";
            }
            return;
        }

        Debug.Log($"[HVACDetailPanel] ✓ HVAC data received with {telemetryData.Count} values");

        // Update ambient temperature (key has space: "Ambient Temperature")
        if (telemetryData.TryGetValue("Ambient Temperature", out string ambientTemp))
        {
            if (ambientTemperatureText != null)
            {
                // Parse and format temperature
                if (float.TryParse(ambientTemp, out float temp))
                {
                    ambientTemperatureText.text = $"Ambient Temp: {temp:F1}°C";
                    Debug.Log($"[HVACDetailPanel] ✓ Ambient Temperature: {temp:F1}°C");
                }
                else
                {
                    ambientTemperatureText.text = $"Ambient Temp: {ambientTemp}°C";
                }
            }
        }
        else
        {
            if (ambientTemperatureText != null)
            {
                ambientTemperatureText.text = "Ambient Temp: N/A";
            }
            Debug.LogWarning("[HVACDetailPanel] 'Ambient Temperature' not found in telemetry data");
        }

        // Update set temperature (key has space: "Set Temperature")
        if (telemetryData.TryGetValue("Set Temperature", out string setTemp))
        {
            if (setTemperatureText != null)
            {
                // Parse and format temperature
                if (float.TryParse(setTemp, out float temp))
                {
                    setTemperatureText.text = $"Set Temp: {temp:F1}°C";
                    Debug.Log($"[HVACDetailPanel] ✓ Set Temperature: {temp:F1}°C");
                }
                else
                {
                    setTemperatureText.text = $"Set Temp: {setTemp}°C";
                }
            }
        }
        else
        {
            if (setTemperatureText != null)
            {
                setTemperatureText.text = "Set Temp: N/A";
            }
            Debug.LogWarning("[HVACDetailPanel] 'Set Temperature' not found in telemetry data");
        }

        // Update status
        if (statusText != null)
        {
            statusText.text = "Status: Active";
        }

        if (lastUpdatedText != null)
        {
            lastUpdatedText.text = $"Last Updated: {System.DateTime.Now:HH:mm:ss}";
        }

        Debug.Log($"[HVACDetailPanel] ✓✓✓ Panel updated with live HVAC data");
    }

    /// <summary>
    /// Hide the panel
    /// </summary>
    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        currentRoomEntityID = null;
        currentSensorName = null;
        Debug.Log("[HVACDetailPanel] Panel hidden");
    }

    /// <summary>
    /// Check if panel is currently visible
    /// </summary>
    public bool IsVisible()
    {
        return panelRoot != null && panelRoot.activeSelf;
    }

    // Public getters
    public string CurrentRoomEntityID => currentRoomEntityID;
    public string CurrentSensorName => currentSensorName;

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HidePanel);
        }
    }
}
