using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Singleton registry that maps Room Entity IDs to Camera_Placeholder transforms
/// Builds dictionary cache on Start for O(1) instant lookups
/// Uses RoomData.json to intelligently navigate building hierarchy
///
/// Hierarchy structure:
/// DIC/Main → Mezzanine Floor → [Entity ID GameObject] → Camera_Placeholder
/// </summary>
public class RoomCameraRegistry : MonoBehaviour
{
    public static RoomCameraRegistry Instance { get; private set; }

    [Header("Registry Stats")]
    [SerializeField] private int totalRoomsRegistered = 0;
    [SerializeField] private int roomsWithCameras = 0;
    [SerializeField] private int roomsWithoutCameras = 0;

    // Dictionary: Entity ID → Camera_Placeholder Transform
    private Dictionary<string, Transform> roomCameraLookup = new Dictionary<string, Transform>();

    // Cache of all room data from JSON
    private List<RoomData> allRoomData = new List<RoomData>();

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        BuildRoomCameraRegistry();
    }

    /// <summary>
    /// Build the registry by parsing RoomData.json and finding Camera_Placeholder transforms
    /// </summary>
    private void BuildRoomCameraRegistry()
    {
        Debug.Log("[RoomCameraRegistry] Building room camera registry...");
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Step 1: Load room data from JSON
        LoadRoomDataFromJSON();

        if (allRoomData.Count == 0)
        {
            Debug.LogError("[RoomCameraRegistry] No room data loaded from JSON - cannot build registry");
            return;
        }

        // Group rooms by building to show summary
        var buildingGroups = allRoomData.GroupBy(r =>
        {
            string[] parts = r.Floor.Split('/');
            return parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : "Unknown";
        });

        Debug.Log($"[RoomCameraRegistry] Found {allRoomData.Count} rooms across {buildingGroups.Count()} buildings:");
        foreach (var group in buildingGroups)
        {
            Debug.Log($"  Building '{group.Key}': {group.Count()} rooms");
        }

        // Step 2: For each room in JSON, find its Camera_Placeholder
        int debugLogLimit = 3; // Only show detailed logs for first 3 rooms
        int debugLogCount = 0;

        foreach (RoomData room in allRoomData)
        {
            if (string.IsNullOrEmpty(room.EntityID) || string.IsNullOrEmpty(room.Floor))
            {
                continue; // Skip invalid entries
            }

            // Enable detailed logging for first few rooms only
            bool enableDetailedLogs = debugLogCount < debugLogLimit;

            // Find the Camera_Placeholder for this room
            Transform cameraPlaceholder = FindCameraPlaceholderForRoom(room, enableDetailedLogs);

            if (cameraPlaceholder != null)
            {
                // Add to dictionary
                if (!roomCameraLookup.ContainsKey(room.EntityID))
                {
                    roomCameraLookup.Add(room.EntityID, cameraPlaceholder);
                    roomsWithCameras++;
                    Debug.Log($"[RoomCameraRegistry] ✓ Registered camera for: {room.Name}");
                }
            }
            else
            {
                roomsWithoutCameras++;
            }

            totalRoomsRegistered++;
            if (enableDetailedLogs) debugLogCount++;
        }

        stopwatch.Stop();
        Debug.Log($"[RoomCameraRegistry] ✓ Registry built in {stopwatch.ElapsedMilliseconds}ms");
        Debug.Log($"[RoomCameraRegistry] Total rooms: {totalRoomsRegistered} | With cameras: {roomsWithCameras} | Without cameras: {roomsWithoutCameras}");

        if (roomsWithCameras == 0)
        {
            Debug.LogError("[RoomCameraRegistry] NO CAMERAS FOUND! Check the detailed logs above for the first few rooms to see where the hierarchy search is failing.");
        }
    }

    /// <summary>
    /// Load room data from JSON file with sensors
    /// Manual parsing to handle "Entity ID", "Sensor ID" fields with spaces
    /// </summary>
    private void LoadRoomDataFromJSON()
    {
        string jsonPath = System.IO.Path.Combine(Application.dataPath, "Sensor Excels", "roomdata+sensor.json");

        if (!System.IO.File.Exists(jsonPath))
        {
            Debug.LogError($"[RoomCameraRegistry] roomdata+sensor.json not found at: {jsonPath}");
            return;
        }

        try
        {
            string jsonContent = System.IO.File.ReadAllText(jsonPath);

            // Manual JSON parsing using MiniJSON
            var jsonArray = MiniJSON.Json.Deserialize(jsonContent) as List<object>;

            if (jsonArray != null)
            {
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

                        if (!string.IsNullOrEmpty(floor))
                        {
                            RoomData roomData = new RoomData(entityID, name, floor, sensors);
                            allRoomData.Add(roomData);
                        }
                    }
                }

                int totalSensors = allRoomData.Sum(r => r.Sensors.Count);
                Debug.Log($"[RoomCameraRegistry] Loaded {allRoomData.Count} rooms with {totalSensors} sensors from JSON");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RoomCameraRegistry] Error loading JSON: {e.Message}");
        }
    }

    /// <summary>
    /// Find Camera_Placeholder for a specific room using JSON Floor data
    /// Hierarchy: DIC/Whitespace -> DIC/Whitespace/GroundFloor -> White Space_Office 01 -> Entity ID -> Camera_Placeholder
    /// </summary>
    private Transform FindCameraPlaceholderForRoom(RoomData room, bool enableDetailedLogs = false)
    {
        // Parse floor path: "DIC/Whitespace/GroundFloor"
        // Building: "DIC/Whitespace"
        // Floor full path: "DIC/Whitespace/GroundFloor"
        // Room name: "White Space_Office 01"
        // Entity ID: "b731f0b0-ac96-11ef-b3dd-63d74b5efe4e"

        if (enableDetailedLogs)
        {
            Debug.Log($"[RoomCameraRegistry] === Searching for room: {room.Name} | EntityID: {room.EntityID} | Floor: {room.Floor} ===");
        }

        string[] floorParts = room.Floor.Split('/');
        if (floorParts.Length < 3)
        {
            if (enableDetailedLogs) Debug.LogWarning($"[RoomCameraRegistry] Invalid floor format: {room.Floor}");
            return null;
        }

        // Building path: First two parts (e.g., "DIC/Whitespace")
        string buildingPath = $"{floorParts[0]}/{floorParts[1]}";

        // Floor full path: Complete floor path (e.g., "DIC/Whitespace/GroundFloor")
        string floorFullPath = room.Floor;

        if (enableDetailedLogs) Debug.Log($"[RoomCameraRegistry] Step 1: Looking for building: '{buildingPath}'");

        // Step 1: Find building GameObject (e.g., "DIC/Whitespace")
        GameObject buildingObject = GameObject.Find(buildingPath);
        if (buildingObject == null)
        {
            if (enableDetailedLogs) Debug.LogWarning($"[RoomCameraRegistry] ✗ Building not found: {buildingPath}");
            return null;
        }
        if (enableDetailedLogs) Debug.Log($"[RoomCameraRegistry] ✓ Building found: {buildingPath}");

        // Step 2: Find floor GameObject as immediate child (full path name)
        if (enableDetailedLogs)
        {
            Debug.Log($"[RoomCameraRegistry] Step 2: Looking for floor child: '{floorFullPath}'");
            Debug.Log($"[RoomCameraRegistry] Available children of {buildingPath}:");
            foreach (Transform child in buildingObject.transform)
            {
                Debug.Log($"  - '{child.name}'");
            }
        }

        Transform floorTransform = null;
        foreach (Transform child in buildingObject.transform)
        {
            if (child.name == floorFullPath)
            {
                floorTransform = child;
                break;
            }
        }

        if (floorTransform == null)
        {
            if (enableDetailedLogs) Debug.LogWarning($"[RoomCameraRegistry] ✗ Floor not found: {floorFullPath} in building {buildingPath}");
            return null;
        }
        if (enableDetailedLogs) Debug.Log($"[RoomCameraRegistry] ✓ Floor found: {floorFullPath}");

        // Step 3: Find room GameObject by Name (e.g., "White Space_Office 01")
        if (enableDetailedLogs)
        {
            Debug.Log($"[RoomCameraRegistry] Step 3: Looking for room: '{room.Name}'");
            Debug.Log($"[RoomCameraRegistry] Available children of {floorFullPath} (showing first 10):");
            int count = 0;
            foreach (Transform child in floorTransform)
            {
                Debug.Log($"  - '{child.name}'");
                count++;
                if (count >= 10) break;
            }
        }

        Transform roomTransform = null;
        foreach (Transform child in floorTransform)
        {
            if (child.name == room.Name)
            {
                roomTransform = child;
                break;
            }
        }

        if (roomTransform == null)
        {
            if (enableDetailedLogs) Debug.LogWarning($"[RoomCameraRegistry] ✗ Room not found: {room.Name} in floor {floorFullPath}");
            return null;
        }
        if (enableDetailedLogs) Debug.Log($"[RoomCameraRegistry] ✓ Room found: {room.Name}");

        // Step 4: Find Entity ID GameObject as child of room name
        if (enableDetailedLogs)
        {
            Debug.Log($"[RoomCameraRegistry] Step 4: Looking for Entity ID GameObject: '{room.EntityID}'");
            Debug.Log($"[RoomCameraRegistry] Available children of {room.Name}:");
            foreach (Transform child in roomTransform)
            {
                Debug.Log($"  - '{child.name}'");
            }
        }

        Transform entityTransform = null;
        foreach (Transform child in roomTransform)
        {
            if (child.name == room.EntityID)
            {
                entityTransform = child;
                break;
            }
        }

        if (entityTransform == null)
        {
            if (enableDetailedLogs) Debug.LogWarning($"[RoomCameraRegistry] ✗ Entity ID GameObject not found: {room.EntityID} in room {room.Name}");
            return null;
        }
        if (enableDetailedLogs) Debug.Log($"[RoomCameraRegistry] ✓ Entity ID GameObject found: {room.EntityID}");

        // Step 5: Find Camera_Placeholder as child of Entity ID
        if (enableDetailedLogs) Debug.Log($"[RoomCameraRegistry] Step 5: Looking for Camera_Placeholder");
        Transform cameraPlaceholder = entityTransform.Find("Camera_Placeholder");

        if (cameraPlaceholder == null)
        {
            if (enableDetailedLogs) Debug.LogWarning($"[RoomCameraRegistry] ✗ Camera_Placeholder not found in Entity ID: {room.EntityID}");
            return null;
        }

        if (enableDetailedLogs) Debug.Log($"[RoomCameraRegistry] ✓✓✓ SUCCESS! Camera_Placeholder found for room: {room.Name}");
        return cameraPlaceholder;
    }

    /// <summary>
    /// Get Camera_Placeholder transform for a room by Entity ID
    /// O(1) instant lookup
    /// </summary>
    public Transform GetRoomCamera(string entityID)
    {
        if (string.IsNullOrEmpty(entityID))
        {
            Debug.LogWarning("[RoomCameraRegistry] Entity ID is null or empty");
            return null;
        }

        if (roomCameraLookup.TryGetValue(entityID, out Transform cameraTransform))
        {
            return cameraTransform;
        }

        Debug.LogWarning($"[RoomCameraRegistry] No camera found for Entity ID: {entityID}");
        return null;
    }

    /// <summary>
    /// Check if a room has a registered camera
    /// </summary>
    public bool HasRoomCamera(string entityID)
    {
        return roomCameraLookup.ContainsKey(entityID);
    }

    /// <summary>
    /// Get all room data (for sensor auto-find)
    /// </summary>
    public List<RoomData> AllRoomData
    {
        get { return allRoomData; }
    }

    /// <summary>
    /// Rebuild the registry (useful if rooms are added/removed at runtime)
    /// </summary>
    public void RebuildRegistry()
    {
        Debug.Log("[RoomCameraRegistry] Rebuilding registry...");
        roomCameraLookup.Clear();
        totalRoomsRegistered = 0;
        roomsWithCameras = 0;
        roomsWithoutCameras = 0;
        BuildRoomCameraRegistry();
    }

    // Public properties
    public int TotalRoomsRegistered => totalRoomsRegistered;
    public int RoomsWithCameras => roomsWithCameras;
    public int RoomsWithoutCameras => roomsWithoutCameras;
}
