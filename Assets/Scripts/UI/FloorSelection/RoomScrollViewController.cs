using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Manages the rooms scroll view - loads JSON data and populates with room buttons
/// Similar to SlidingPanelController but for room data
///
/// CLOSED-LOOP SYSTEM:
/// - Loads room data from JSON on Start
/// - Populates scroll view with room button prefabs
/// - Handles smooth batched instantiation to avoid frame drops
/// - Provides filtering by floor
/// </summary>
public class RoomScrollViewController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform scrollViewContent; // Content area of scroll view
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private GameObject scrollViewRoot; // The entire scroll view GameObject

    [Header("Prefab Settings")]
    [SerializeField] private GameObject roomItemPrefab; // RoomDataPrefab

    [Header("JSON Data")]
    [SerializeField] private string jsonFileName = "roomdata+sensor.json";
    [SerializeField] private string jsonFolderPath = "Assets/Sensor Excels";

    [Header("Performance Settings")]
    [Tooltip("Number of room items to instantiate per frame (lower = smoother but slower)")]
    [SerializeField] private int batchSize = 5;
    [Tooltip("Delay between batches in seconds")]
    [SerializeField] private float delayBetweenBatches = 0.02f;

    [Header("Filtering")]
    [Tooltip("If set, only show rooms from this floor. Leave empty to show all.")]
    [SerializeField] private string floorFilter = "";

    // Data storage
    private List<RoomData> allRoomData = new List<RoomData>();
    private List<RoomData> filteredRoomData = new List<RoomData>();
    private List<GameObject> spawnedRoomItems = new List<GameObject>();

    // Search state
    private string currentSearchQuery = "";

    // State
    private bool isDataLoaded = false;
    private bool isPopulating = false;

    private void Start()
    {
        // Hide scroll view initially (FloorButtonStacking will show it)
        if (scrollViewRoot != null)
        {
            scrollViewRoot.SetActive(false);
        }

        // Load JSON data
        LoadRoomDataFromJSON();
    }

    /// <summary>
    /// Load room data from JSON file
    /// Manual parsing to handle "Entity ID" field with space
    /// </summary>
    private void LoadRoomDataFromJSON()
    {
        string fullPath = Path.Combine(Application.dataPath, "..", jsonFolderPath, jsonFileName);
        fullPath = Path.GetFullPath(fullPath); // Normalize path

        Debug.Log($"[RoomScrollViewController] Loading room data from: {fullPath}");

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[RoomScrollViewController] JSON file not found at: {fullPath}");
            return;
        }

        try
        {
            // Read JSON file
            string jsonContent = File.ReadAllText(fullPath);

            // Manual JSON parsing using MiniJSON to handle "Entity ID" field with space
            var jsonArray = MiniJSON.Json.Deserialize(jsonContent) as List<object>;

            if (jsonArray != null)
            {
                allRoomData.Clear();

                foreach (var item in jsonArray)
                {
                    var roomDict = item as Dictionary<string, object>;
                    if (roomDict != null)
                    {
                        string entityID = roomDict.ContainsKey("Entity ID") ? roomDict["Entity ID"] as string : "";
                        string name = roomDict.ContainsKey("Name") ? roomDict["Name"] as string : "";
                        string floor = roomDict.ContainsKey("Floor") ? roomDict["Floor"] as string : "";

                        // Parse sensors array
                        List<SensorData> sensors = new List<SensorData>();
                        if (roomDict.ContainsKey("Sensors"))
                        {
                            var sensorsArray = roomDict["Sensors"] as List<object>;
                            if (sensorsArray != null)
                            {
                                foreach (var sensorItem in sensorsArray)
                                {
                                    var sensorDict = sensorItem as Dictionary<string, object>;
                                    if (sensorDict != null)
                                    {
                                        string sensorID = sensorDict.ContainsKey("Sensor ID") ? sensorDict["Sensor ID"] as string : "";
                                        string sensorName = sensorDict.ContainsKey("Sensor Name") ? sensorDict["Sensor Name"] as string : "";
                                        string sensorType = sensorDict.ContainsKey("Sensor Type") ? sensorDict["Sensor Type"] as string : "";

                                        if (!string.IsNullOrEmpty(sensorID))
                                        {
                                            sensors.Add(new SensorData(sensorID, sensorName, sensorType));
                                        }
                                    }
                                }
                            }
                        }

                        // Only add rooms with valid Floor data
                        if (!string.IsNullOrEmpty(floor))
                        {
                            RoomData roomData = new RoomData(entityID, name, floor, sensors);
                            allRoomData.Add(roomData);
                        }
                    }
                }

                Debug.Log($"[RoomScrollViewController] ✓ Loaded {allRoomData.Count} rooms from JSON");

                // Print some stats
                var floorGroups = allRoomData.GroupBy(r => r.Floor);
                Debug.Log($"[RoomScrollViewController] Found {floorGroups.Count()} unique floors:");
                foreach (var group in floorGroups)
                {
                    Debug.Log($"  - {group.Key}: {group.Count()} rooms");
                }

                isDataLoaded = true;

                // Apply filter and populate
                ApplyFloorFilter();
            }
            else
            {
                Debug.LogError("[RoomScrollViewController] Failed to deserialize JSON - result is not an array");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RoomScrollViewController] Error loading JSON: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Apply floor filter and update the filtered list
    /// </summary>
    public void ApplyFloorFilter()
    {
        if (!isDataLoaded)
        {
            Debug.LogWarning("[RoomScrollViewController] Data not loaded yet");
            return;
        }

        List<RoomData> dataToFilter = allRoomData;

        // Apply floor filter first
        if (!string.IsNullOrEmpty(floorFilter))
        {
            dataToFilter = dataToFilter.Where(r => r.Floor != null && r.Floor.Contains(floorFilter)).ToList();
            Debug.Log($"[RoomScrollViewController] Floor filter '{floorFilter}' applied - {dataToFilter.Count} rooms");
        }

        // Apply search query with relevance ranking
        if (!string.IsNullOrEmpty(currentSearchQuery))
        {
            filteredRoomData = RoomSearchRanker.SearchAndRank(dataToFilter, currentSearchQuery);
            Debug.Log($"[RoomScrollViewController] Search '{currentSearchQuery}' applied - {filteredRoomData.Count} results (ranked by relevance)");
        }
        else
        {
            // No search - sort alphabetically
            filteredRoomData = dataToFilter.OrderBy(r => r.Name).ToList();
            Debug.Log($"[RoomScrollViewController] No search query - showing {filteredRoomData.Count} rooms (alphabetically)");
        }
    }

    /// <summary>
    /// Populate the scroll view with room items
    /// Called when the scroll view is shown
    /// </summary>
    public void PopulateRoomList()
    {
        if (!isDataLoaded)
        {
            Debug.LogWarning("[RoomScrollViewController] Cannot populate - data not loaded");
            return;
        }

        if (isPopulating)
        {
            Debug.LogWarning("[RoomScrollViewController] Already populating - skipping");
            return;
        }

        if (roomItemPrefab == null || scrollViewContent == null)
        {
            Debug.LogError("[RoomScrollViewController] RoomItemPrefab or ScrollViewContent is not assigned!");
            return;
        }

        Debug.Log($"[RoomScrollViewController] Starting to populate scroll view with {filteredRoomData.Count} rooms");

        // Use coroutine for smooth population
        StartCoroutine(PopulateRoomListSmooth());
    }

    /// <summary>
    /// Populate room list smoothly over multiple frames (batched)
    /// </summary>
    private IEnumerator PopulateRoomListSmooth()
    {
        isPopulating = true;

        // Step 1: Clear existing items
        yield return StartCoroutine(ClearRoomItemsSmooth());

        if (filteredRoomData.Count == 0)
        {
            Debug.Log("[RoomScrollViewController] No rooms to display (filtered result is empty)");
            isPopulating = false;
            yield break;
        }

        // Step 2: Instantiate room items in batches
        for (int i = 0; i < filteredRoomData.Count; i += batchSize)
        {
            int itemsInThisBatch = Mathf.Min(batchSize, filteredRoomData.Count - i);

            // Create a batch of items
            for (int j = 0; j < itemsInThisBatch; j++)
            {
                int index = i + j;
                GameObject item = Instantiate(roomItemPrefab, scrollViewContent, false);
                spawnedRoomItems.Add(item);

                // Set the room data
                RoomDataItem roomDataItem = item.GetComponent<RoomDataItem>();
                if (roomDataItem != null)
                {
                    roomDataItem.SetRoomData(filteredRoomData[index]);
                }
                else
                {
                    Debug.LogWarning($"[RoomScrollViewController] RoomDataItem component not found on prefab instance {index}");
                }
            }

            // Wait before next batch
            yield return new WaitForSeconds(delayBetweenBatches);
        }

        // Step 3: Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollViewContent);

        // Reset scroll position to top
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }

        Debug.Log($"[RoomScrollViewController] ✓ Populated {filteredRoomData.Count} room items smoothly");
        isPopulating = false;
    }

    /// <summary>
    /// Clear all spawned room items smoothly (batched destruction)
    /// </summary>
    private IEnumerator ClearRoomItemsSmooth()
    {
        if (spawnedRoomItems.Count == 0)
        {
            yield break;
        }

        Debug.Log($"[RoomScrollViewController] Clearing {spawnedRoomItems.Count} room items...");

        int destroyBatchSize = 10; // Destroy more items per batch since it's faster
        float destroyDelay = 0.01f;

        for (int i = 0; i < spawnedRoomItems.Count; i += destroyBatchSize)
        {
            int itemsInThisBatch = Mathf.Min(destroyBatchSize, spawnedRoomItems.Count - i);

            for (int j = 0; j < itemsInThisBatch; j++)
            {
                int index = i + j;
                if (spawnedRoomItems[index] != null)
                {
                    Destroy(spawnedRoomItems[index]);
                }
            }

            yield return new WaitForSeconds(destroyDelay);
        }

        spawnedRoomItems.Clear();
        Debug.Log("[RoomScrollViewController] ✓ Cleared all room items");
    }

    /// <summary>
    /// Clear all items immediately (for cleanup)
    /// </summary>
    private void ClearRoomItemsImmediate()
    {
        foreach (GameObject item in spawnedRoomItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedRoomItems.Clear();
    }

    /// <summary>
    /// Set floor filter and refresh the list
    /// </summary>
    public void SetFloorFilter(string filter)
    {
        floorFilter = filter;
        ApplyFloorFilter();
        PopulateRoomList();
    }

    /// <summary>
    /// Clear floor filter and show all rooms
    /// </summary>
    public void ClearFloorFilter()
    {
        SetFloorFilter("");
    }

    /// <summary>
    /// Refresh the room list (reload and repopulate)
    /// </summary>
    public void RefreshRoomList()
    {
        LoadRoomDataFromJSON();
        ApplyFloorFilter();
        PopulateRoomList();
    }

    /// <summary>
    /// Search rooms with relevance ranking
    /// Returns the number of results found
    /// </summary>
    public int SearchRooms(string searchQuery)
    {
        currentSearchQuery = searchQuery;

        Debug.Log($"[RoomScrollViewController] Searching for: '{searchQuery}'");

        // Re-apply filters with the new search query
        ApplyFloorFilter();

        // Repopulate the list with ranked results
        PopulateRoomList();

        return filteredRoomData.Count;
    }

    /// <summary>
    /// Clear search and show all rooms
    /// </summary>
    public void ClearSearch()
    {
        SearchRooms("");
    }

    // Public properties
    public bool IsDataLoaded => isDataLoaded;
    public int TotalRoomCount => allRoomData.Count;
    public int FilteredRoomCount => filteredRoomData.Count;
    public List<RoomData> AllRoomData => allRoomData;
    public List<RoomData> FilteredRoomData => filteredRoomData;
    public string CurrentSearchQuery => currentSearchQuery;

    private void OnDestroy()
    {
        // Clean up all spawned items
        ClearRoomItemsImmediate();
    }
}
