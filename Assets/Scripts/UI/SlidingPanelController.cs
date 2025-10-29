using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public class SlidingPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private Button alarmPanelButton;
    [SerializeField] private RectTransform scrollViewContent; // Content area of scroll view
    [SerializeField] private ScrollRect scrollRect;

    [Header("Prefab Settings")]
    [SerializeField] private GameObject alarmItemPrefab;
    [SerializeField] private int numberOfItemsToSpawn = 5; // How many items to spawn

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private Ease slideInEase = Ease.OutCubic;
    [SerializeField] private Ease slideOutEase = Ease.InCubic;
    [SerializeField] private float offsetDistance = 500f; // Distance to move panel outside canvas

    [Header("MasterUI Integration")]
    [SerializeField] private MasterUI masterUI;

    [Header("Alarm Data Integration")]
    [SerializeField] private MasterAlarm masterAlarm;

    // State management
    private bool isPanelVisible = false;
    private bool isAnimating = false;
    private Vector2 panelVisiblePosition; // Position when panel is visible inside canvas
    private Vector2 panelHiddenPosition; // Position when panel is outside canvas

    // List to keep track of spawned items
    private List<GameObject> spawnedItems = new List<GameObject>();

    // Current alarm data
    private List<AlarmData> currentAlarmData = new List<AlarmData>();

    private void Awake()
    {
        InitializePanel();

        // Subscribe to MasterAlarm alarm updates
        if (masterAlarm != null)
        {
            masterAlarm.OnAlarmsReceived += OnAlarmsReceived;
        }
        else
        {
            Debug.LogWarning("[SlidingPanelController] MasterAlarm reference is not assigned!");
        }
    }

    private void OnAlarmsReceived(List<AlarmData> alarms)
    {
        Debug.Log($"[SlidingPanelController] Received {alarms.Count} alarms from MasterAlarm");
        currentAlarmData = alarms;
        UpdateAlarmDisplay();
    }

    private void InitializePanel()
    {
        if (panel == null)
        {
            Debug.LogError("[SlidingPanelController] Panel RectTransform is not assigned!");
            return;
        }

        // Store the panel's current position as the visible position (inside canvas)
        panelVisiblePosition = panel.anchoredPosition;

        // Calculate hidden position (move panel LEFT by offsetDistance to hide outside canvas)
        panelHiddenPosition = new Vector2(panelVisiblePosition.x - offsetDistance, panelVisiblePosition.y);

        // Move panel to hidden position at start
        panel.anchoredPosition = panelHiddenPosition;

        Debug.Log($"[SlidingPanelController] Panel initialized: visible={panelVisiblePosition}, hidden={panelHiddenPosition}");

        // Setup button listener
        if (alarmPanelButton != null)
        {
            alarmPanelButton.onClick.AddListener(OnAlarmPanelButtonClicked);
        }
        else
        {
            Debug.LogError("[SlidingPanelController] Alarm Panel Button is not assigned!");
        }

        // Notify MasterUI of initial state
        if (masterUI != null)
        {
            masterUI.OnPanelStateChanged(false); // Panel starts hidden
        }
    }

    private void OnAlarmPanelButtonClicked()
    {
        if (isAnimating) return;

        if (!isPanelVisible)
        {
            ShowPanel();
        }
        else
        {
            HidePanel();
        }
    }

    private void ShowPanel()
    {
        if (panel == null || isPanelVisible) return;

        Debug.Log("[SlidingPanelController] Showing panel - sliding in");
        isAnimating = true;
        isPanelVisible = true;

        // Activate panel
        panel.gameObject.SetActive(true);

        // Animate panel sliding in
        panel.DOAnchorPos(panelVisiblePosition, animationDuration)
            .SetEase(slideInEase)
            .OnComplete(() => {
                isAnimating = false;
                Debug.Log("[SlidingPanelController] Panel shown");

                // Notify MasterUI
                if (masterUI != null)
                {
                    masterUI.OnPanelStateChanged(true);
                }
            });
    }

    private void HidePanel()
    {
        if (panel == null || !isPanelVisible) return;

        Debug.Log("[SlidingPanelController] Hiding panel - sliding out");
        isAnimating = true;
        isPanelVisible = false;

        // Animate panel sliding out
        panel.DOAnchorPos(panelHiddenPosition, animationDuration)
            .SetEase(slideOutEase)
            .OnComplete(() => {
                isAnimating = false;
                Debug.Log("[SlidingPanelController] Panel hidden");

                // Notify MasterUI
                if (masterUI != null)
                {
                    masterUI.OnPanelStateChanged(false);
                }
            });
    }

    // Public properties for external access
    public bool IsPanelVisible => isPanelVisible;
    public bool IsAnimating => isAnimating;

    // Public method to programmatically show/hide panel
    public void SetPanelVisibility(bool visible)
    {
        if (visible && !isPanelVisible)
        {
            ShowPanel();
        }
        else if (!visible && isPanelVisible)
        {
            HidePanel();
        }
    }

    // Update alarm display with real data (smoothly over time)
    private void UpdateAlarmDisplay()
    {
        if (alarmItemPrefab == null || scrollViewContent == null)
        {
            Debug.LogWarning("[SlidingPanelController] AlarmItemPrefab or ScrollViewContent is not assigned!");
            return;
        }

        // Use coroutine for smooth, gradual update to avoid frame spikes
        StartCoroutine(UpdateAlarmDisplaySmooth());
    }

    private IEnumerator UpdateAlarmDisplaySmooth()
    {
        // Step 1: Clear existing items gradually (batch by batch to avoid jitter)
        yield return StartCoroutine(ClearAlarmItemsSmooth());

        // Display ALL alarms (remove the limit if you want to show all filtered results)
        int alarmsToDisplay = currentAlarmData.Count;

        if (alarmsToDisplay == 0)
        {
            Debug.Log("[SlidingPanelController] No alarms to display (filtered result is empty)");
            yield break;
        }

        // Step 2: Spawn alarm item prefabs gradually (batch by batch)
        int batchSize = 1; // Create 3 items per frame for smooth performance
        float delayBetweenBatches = 0.05f; // Small delay between batches for buttery smoothness

        for (int i = 0; i < alarmsToDisplay; i += batchSize)
        {
            int itemsInThisBatch = Mathf.Min(batchSize, alarmsToDisplay - i);

            // Create a batch of items
            for (int j = 0; j < itemsInThisBatch; j++)
            {
                int index = i + j;
                GameObject item = Instantiate(alarmItemPrefab, scrollViewContent, false);
                spawnedItems.Add(item);

                // Get the AlarmItemUI component and set the data
                AlarmItemUI alarmUI = item.GetComponent<AlarmItemUI>();
                if (alarmUI != null)
                {
                    alarmUI.SetAlarmData(currentAlarmData[index]);
                }
                else
                {
                    Debug.LogWarning($"[SlidingPanelController] AlarmItemUI component not found on prefab instance {index}");
                }
            }

            // Wait before processing next batch
            yield return new WaitForSeconds(delayBetweenBatches);
        }

        // Step 3: Force rebuild the layout to ensure proper scroll view content size
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scrollViewContent);

        // Reset scroll position to top
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }

        Debug.Log($"[SlidingPanelController] ✓ Displayed {alarmsToDisplay} alarm items smoothly (batched over time)");
    }

    // Clear all spawned items gradually (batch by batch to avoid frame spike)
    private IEnumerator ClearAlarmItemsSmooth()
    {
        int batchSize = 1; // Destroy 5 items per frame
        float delayBetweenBatches = 0.03f; // Small delay between destruction batches

        for (int i = 0; i < spawnedItems.Count; i += batchSize)
        {
            int itemsInThisBatch = Mathf.Min(batchSize, spawnedItems.Count - i);

            // Destroy a batch of items
            for (int j = 0; j < itemsInThisBatch; j++)
            {
                int index = i + j;
                if (spawnedItems[index] != null)
                {
                    Destroy(spawnedItems[index]);
                }
            }

            // Wait before destroying next batch
            yield return new WaitForSeconds(delayBetweenBatches);
        }

        spawnedItems.Clear();
        Debug.Log("[SlidingPanelController] ✓ Cleared all alarm items smoothly (batched destruction)");
    }

    // Clear all spawned items immediately (fallback for instant cleanup)
    private void ClearAlarmItems()
    {
        foreach (GameObject item in spawnedItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedItems.Clear();
    }

    // Public method to re-spawn items (useful if you want to refresh the list)
    public void RefreshAlarmItems()
    {
        UpdateAlarmDisplay();
    }

    private void OnDestroy()
    {
        // Unsubscribe from MasterAlarm
        if (masterAlarm != null)
        {
            masterAlarm.OnAlarmsReceived -= OnAlarmsReceived;
        }

        // Clean up button listener
        if (alarmPanelButton != null)
        {
            alarmPanelButton.onClick.RemoveListener(OnAlarmPanelButtonClicked);
        }

        // Clean up spawned items
        ClearAlarmItems();

        // Kill any running DOTween animations on this panel
        DOTween.Kill(panel);
    }
}
