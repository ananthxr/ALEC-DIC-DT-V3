using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AlarmItemUI : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI originatorText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI severityText;
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Panel References")]
    [SerializeField] private GameObject alarmOptionsPanel;
    [SerializeField] private Button alarmButton;

    // Static reference to track currently active panel
    private static GameObject currentlyActivePanel = null;

    private void Start()
    {
        // Ensure panel starts inactive
        if (alarmOptionsPanel != null)
        {
            alarmOptionsPanel.SetActive(false);
        }

        // Setup button click listener
        if (alarmButton != null)
        {
            alarmButton.onClick.AddListener(OnAlarmItemClicked);
        }
    }

    private void OnAlarmItemClicked()
    {
        if (alarmOptionsPanel == null) return;

        // If this panel is already active, toggle it off
        if (currentlyActivePanel == alarmOptionsPanel)
        {
            alarmOptionsPanel.SetActive(false);
            currentlyActivePanel = null;
            return;
        }

        // Deactivate previously active panel
        if (currentlyActivePanel != null)
        {
            currentlyActivePanel.SetActive(false);
        }

        // Activate this panel
        alarmOptionsPanel.SetActive(true);
        currentlyActivePanel = alarmOptionsPanel;
    }

    public void SetAlarmData(AlarmData alarmData)
    {
        if (alarmData == null)
        {
            Debug.LogWarning("[AlarmItemUI] Alarm data is null!");
            return;
        }

        // Set alarm type
        if (typeText != null)
        {
            typeText.text = alarmData.description;
        }

        // Set originator/location
        if (originatorText != null)
        {
            originatorText.text = alarmData.location;
        }

        // Set status
        if (statusText != null)
        {
            string statusDisplay = alarmData.isActive ? "Active" : "Cleared";
            statusText.text = statusDisplay;
        }

        // Set severity with color coding
        if (severityText != null)
        {
            severityText.text = alarmData.severity;

            // Apply color based on severity
            switch (alarmData.severity.ToUpper())
            {
                case "CRITICAL":
                    severityText.color = new Color(0.8f, 0f, 0f); // Dark red
                    break;
                case "MAJOR":
                    severityText.color = new Color(1f, 0.3f, 0f); // Orange-red
                    break;
                case "WARNING":
                    severityText.color = new Color(1f, 0.65f, 0f); // Orange
                    break;
                case "MINOR":
                    severityText.color = new Color(1f, 1f, 0f); // Yellow
                    break;
                default:
                    severityText.color = Color.gray;
                    break;
            }
        }

        // Set timestamp
        if (timeText != null)
        {
            timeText.text = alarmData.timestamp;
        }
    }

    public void ClearData()
    {
        if (typeText != null) typeText.text = "";
        if (originatorText != null) originatorText.text = "";
        if (statusText != null) statusText.text = "";
        if (severityText != null) severityText.text = "";
        if (timeText != null) timeText.text = "";
    }

    private void OnDestroy()
    {
        // Clean up button listener
        if (alarmButton != null)
        {
            alarmButton.onClick.RemoveListener(OnAlarmItemClicked);
        }

        // Clear static reference if this was the active panel
        if (currentlyActivePanel == alarmOptionsPanel)
        {
            currentlyActivePanel = null;
        }
    }
}
