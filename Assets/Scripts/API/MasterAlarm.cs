using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;
using TMPro;

// Authentication data classes
[System.Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[System.Serializable]
public class AuthResponse
{
    public string token;
    public string refreshToken;
}

// JSON data classes for alarm API response
[System.Serializable]
public class AlarmResponse
{
    public AlarmItem[] data;
}

// WebSocket message structures
[System.Serializable]
public class WebSocketCommand
{
    public List<AlarmCommand> cmds;
}

[System.Serializable]
public class AlarmCommand
{
    public string type;
    public AlarmQuery query;
    public int cmdId;
}

[System.Serializable]
public class AlarmQuery
{
    public EntityFilter entityFilter;
    public PageLink pageLink;
    public List<AlarmFieldKey> alarmFields;
    public List<string> entityFields;
    public List<string> latestValues;
}

[System.Serializable]
public class EntityFilter
{
    public string type;
    public bool resolveMultiple;
    public bool rootStateEntity;
    public string stateEntityParamName;
    public EntityRef defaultStateEntity;
    public EntityRef rootEntity;
    public string direction;
    public int maxLevel;
    public bool fetchLastLevelOnly;
    public string relationType;
    public List<string> assetTypes;
}

[System.Serializable]
public class EntityRef
{
    public string entityType;
    public string id;
}

[System.Serializable]
public class PageLink
{
    public int page;
    public int pageSize;
    public object textSearch;
    public object typeList;
    public List<string> severityList;
    public List<string> statusList;
    public bool searchPropagatedAlarms;
    public object assigneeId;
    public SortOrder sortOrder;
    public long timeWindow;
}

[System.Serializable]
public class SortOrder
{
    public SortKey key;
    public string direction;
}

[System.Serializable]
public class SortKey
{
    public string key;
    public string type;
}

[System.Serializable]
public class AlarmFieldKey
{
    public string type;
    public string key;
}

[System.Serializable]
public class WebSocketResponse
{
    public int subscriptionId;
    public AlarmResponseData data;
}

[System.Serializable]
public class AlarmResponseData
{
    public AlarmItem[] data;
}

[System.Serializable]
public class AlarmItem
{
    public AlarmId id;
    public long createdTime;
    public string type;
    public AlarmOriginator originator;
    public string severity;
    public bool acknowledged;
    public bool cleared;
    public string originatorName;
    public string originatorLabel;
    public string status;
}

[System.Serializable]
public class AlarmId
{
    public string entityType;
    public string id;
}

[System.Serializable]
public class AlarmOriginator
{
    public string entityType;
    public string id;
}

// AlarmData class for storing alarm information
public class AlarmData
{
    public string alarmId;      // UUID from API
    public string title;        // originatorName (e.g., "DB-365")
    public string description;  // alarm type from API
    public string severity;     // Critical, Major, Warning, Minor, Indeterminate
    public string timestamp;    // formatted timestamp
    public bool isActive;       // true if not cleared
    public string location;     // originatorName for display
    public string deviceName;   // originatorLabel from API
    public int floorIndex;      // Floor index: 0=Ground, 1=First, 2=Roof (assigned by MasterAlarm)

    // Assignment fields (client-side only, not synced to server)
    public bool isAssigned;         // true if alarm has been assigned to someone
    public string assignedToEmail;  // Email of assigned person

    public AlarmData(string id, string title, string desc, string severity, string timestamp)
    {
        this.alarmId = id;
        this.title = title;
        this.description = desc;
        this.severity = severity;
        this.timestamp = timestamp;
        this.isActive = true;
        this.isAssigned = false;    // Default: not assigned
        this.assignedToEmail = null; // Default: no assignee
    }
}

public class MasterAlarm : MonoBehaviour
{
    [Header("Authentication Settings")]
    [SerializeField] private string apiBaseUrl = "https://pulse.alec.ae/api";
    [SerializeField] private string username = "email-here";
    [SerializeField] private string password = "password-here";

    [Header("Connection Mode")]
    [SerializeField] private bool useWebSocket = true; // true = WebSocket, false = REST API
    [Tooltip("WebSocket will auto-reconnect on disconnect")]
    [SerializeField] private bool autoReconnect = true;

    [Header("Data Fetching Settings (REST API Only)")]
    [SerializeField] private int pageSize = 300;
    [SerializeField] private int pageNumber = 0;
    [SerializeField] private string sortProperty = "createdTime";
    [SerializeField] private string sortOrder = "DESC";
    [SerializeField] private float pollingInterval = 10f;

    [Header("WebSocket Settings")]
    [SerializeField] private string entityId = "0cc4e030-67a1-11f0-a54f-c718692b063f";
    [SerializeField] private int wsPageSize = 300;
    [SerializeField] private long wsTimeWindow = 2592000000; // 30 days in milliseconds

    [Header("Control")]
    [SerializeField] private bool startFetching = false;

    [Header("Floor Assignment")]
    [SerializeField] private bool randomFloorAssignment = true;

    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI alarmDisplayText;

    private string bearerToken = "";
    private string refreshToken = "";
    private bool isAuthenticated = false;
    private bool isFetching = false;
    private Coroutine fetchingCoroutine;

    // Public property to allow other scripts to access bearer token for API calls
    public string BearerToken => bearerToken;
    public bool IsAuthenticated => isAuthenticated;

    // WebSocket variables
    private WebSocket webSocket = null;
    private bool isWebSocketConnected = false;
    private float reconnectDelay = 1f;
    private float maxReconnectDelay = 60f;
    private Coroutine reconnectCoroutine;

    // Store consistent floor assignments per alarm ID
    private Dictionary<string, int> alarmFloorMap = new Dictionary<string, int>();

    // Current status filter (for dynamic filtering)
    private List<string> currentStatusFilter = new List<string>();

    // Current severity filter (for dynamic filtering)
    private List<string> currentSeverityFilter = new List<string>();

    // Current floor entity ID filter (for floor-based alarm filtering)
    private string currentFloorEntityId = "54549790-77e9-11ef-8f9b-033ad0625bc8"; // Default to root building

    // Pagination state (for limiting alarm results)
    private int lastRequestedPage = 0;
    private int lastRequestedPageSize = 20; // Default to 20 items per page

    // Callback to send alarm data to AlarmsManager
    public System.Action<List<AlarmData>> OnAlarmsReceived;

    // ============ SENSOR DATA FETCHING (Generic for HVAC, Lights, Presence, Air Quality) ============

    // Track sensor data requests: cmdId → (assetType, telemetryKeys, callback)
    private Dictionary<int, SensorRequestInfo> sensorDiscoveryRequests = new Dictionary<int, SensorRequestInfo>();

    private int nextSensorCmdId = 100; // Start sensor commands at 100 to avoid collision with alarm cmdId (2)

    // Helper class to track sensor request info
    private class SensorRequestInfo
    {
        public string assetType;
        public List<string> telemetryKeys;
        public System.Action<Dictionary<string, string>> callback;
    }

    // ============ Public Singleton Instance ============
    public static MasterAlarm Instance { get; private set; }

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

    private void Update()
    {
        // Dispatch WebSocket messages on main thread
        #if !UNITY_WEBGL || UNITY_EDITOR
        webSocket?.DispatchMessageQueue();
        #endif

        if (startFetching && !isFetching)
        {
            StartFetchingData();
        }
        else if (!startFetching && isFetching)
        {
            StopFetchingData();
        }
    }

    private void StartFetchingData()
    {
        if (!isAuthenticated)
        {
            StartCoroutine(AuthenticateAndStartFetching());
        }
        else
        {
            StartDataFetching();
        }
    }

    private void StopFetchingData()
    {
        isFetching = false;

        if (useWebSocket)
        {
            Debug.Log("[MasterAlarm] Stopping WebSocket connection...");
            DisconnectWebSocket();
        }
        else
        {
            if (fetchingCoroutine != null)
            {
                StopCoroutine(fetchingCoroutine);
                fetchingCoroutine = null;
            }
            Debug.Log("[MasterAlarm] Stopped fetching alarm data via REST API");
        }
    }

    private IEnumerator AuthenticateAndStartFetching()
    {
        yield return StartCoroutine(Authenticate());

        if (isAuthenticated)
        {
            StartDataFetching();
        }
        else
        {
            startFetching = false;
            Debug.LogError("[MasterAlarm] Authentication failed. Cannot start data fetching.");
        }
    }

    private IEnumerator Authenticate()
    {
        Debug.Log("[MasterAlarm] Starting authentication...");

        string loginUrl = $"{apiBaseUrl}/auth/login";

        LoginRequest loginData = new LoginRequest
        {
            username = this.username,
            password = this.password
        };

        string jsonData = JsonUtility.ToJson(loginData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(loginUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    AuthResponse authResponse = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                    bearerToken = authResponse.token;
                    refreshToken = authResponse.refreshToken;
                    isAuthenticated = true;

                    Debug.Log("[MasterAlarm] Authentication successful!");
                    Debug.Log($"[MasterAlarm] Bearer Token: {bearerToken.Substring(0, Math.Min(20, bearerToken.Length))}...");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MasterAlarm] Failed to parse authentication response: {e.Message}");
                    Debug.LogError($"[MasterAlarm] Response: {request.downloadHandler.text}");
                    isAuthenticated = false;
                }
            }
            else
            {
                Debug.LogError($"[MasterAlarm] Authentication failed!");
                Debug.LogError($"[MasterAlarm] Request Result: {request.result}");
                Debug.LogError($"[MasterAlarm] Error: {request.error ?? "NULL"}");
                Debug.LogError($"[MasterAlarm] Response Code: {request.responseCode}");
                Debug.LogError($"[MasterAlarm] URL: {loginUrl}");
                Debug.LogError($"[MasterAlarm] Username: {(string.IsNullOrEmpty(this.username) ? "EMPTY" : "SET")}");
                Debug.LogError($"[MasterAlarm] Password: {(string.IsNullOrEmpty(this.password) ? "EMPTY" : "SET")}");

                if (request.downloadHandler != null && request.downloadHandler.text != null)
                {
                    Debug.LogError($"[MasterAlarm] Response: {request.downloadHandler.text}");
                }
                else
                {
                    Debug.LogError($"[MasterAlarm] Response: NULL or No Handler");
                }
                isAuthenticated = false;
            }
        }
    }

    private void StartDataFetching()
    {
        isFetching = true;

        if (useWebSocket)
        {
            Debug.Log("[MasterAlarm] Starting WebSocket connection...");
            ConnectWebSocket();
        }
        else
        {
            fetchingCoroutine = StartCoroutine(FetchDataPeriodically());
            Debug.Log($"[MasterAlarm] Started fetching alarm data via REST API every {pollingInterval} seconds");
        }
    }

    private IEnumerator FetchDataPeriodically()
    {
        while (isFetching && startFetching)
        {
            yield return StartCoroutine(FetchAlarmData());
            yield return new WaitForSeconds(pollingInterval);
        }
    }

    private IEnumerator FetchAlarmData()
    {
        string alarmUrl = $"{apiBaseUrl}/v2/alarms?pageSize={pageSize}&page={pageNumber}&sortProperty={sortProperty}&sortOrder={sortOrder}";

        using (UnityWebRequest request = UnityWebRequest.Get(alarmUrl))
        {
            request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log("[MasterAlarm] === ALARM DATA RECEIVED ===");

                    // Parse JSON response
                    AlarmResponse alarmResponse = JsonUtility.FromJson<AlarmResponse>(jsonResponse);

                    if (alarmResponse != null && alarmResponse.data != null)
                    {
                        // Convert API alarms to AlarmData
                        List<AlarmData> alarmDataList = ConvertToAlarmData(alarmResponse.data);

                        Debug.Log($"[MasterAlarm] Parsed {alarmDataList.Count} active alarms from API");

                        // Send data to AlarmsManager
                        OnAlarmsReceived?.Invoke(alarmDataList);
                    }
                    else
                    {
                        Debug.LogWarning("[MasterAlarm] No alarm data in response");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MasterAlarm] Failed to parse alarm data: {e.Message}");
                    Debug.LogError($"[MasterAlarm] Response: {request.downloadHandler.text}");
                }
            }
            else
            {
                Debug.LogError($"[MasterAlarm] Failed to fetch alarm data: {request.error}");
                Debug.LogError($"[MasterAlarm] Response Code: {request.responseCode}");
                Debug.LogError($"[MasterAlarm] Response: {request.downloadHandler.text}");

                if (request.responseCode == 401)
                {
                    Debug.Log("[MasterAlarm] Token might be expired. Attempting re-authentication...");
                    isAuthenticated = false;
                    yield return StartCoroutine(Authenticate());
                }
            }
        }
    }

    private List<AlarmData> ConvertToAlarmData(AlarmItem[] apiAlarms)
    {
        List<AlarmData> alarmDataList = new List<AlarmData>();

        foreach (AlarmItem apiAlarm in apiAlarms)
        {
            // NOTE: Do NOT filter out cleared alarms here - let the server-side filter handle it
            // The user may want to see cleared alarms based on their filter selection

            // Convert Unix timestamp (milliseconds) to readable format
            DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(apiAlarm.createdTime).LocalDateTime;
            string formattedTimestamp = dateTime.ToString("yyyy-MM-dd HH:mm");

            // Map API severity to UI severity format
            string uiSeverity = MapSeverity(apiAlarm.severity);

            // Format status for display
            string formattedStatus = FormatStatus(apiAlarm.status);

            // Create AlarmData object
            // Get or assign consistent floor index for this alarm ID
            int floorIndex = GetConsistentFloorIndex(apiAlarm.id.id);

            AlarmData alarmData = new AlarmData(
                apiAlarm.id.id,                    // alarmId
                apiAlarm.originatorName,           // title
                apiAlarm.type,                     // description
                uiSeverity,                        // severity
                formattedTimestamp                 // timestamp
            )
            {
                location = apiAlarm.originatorName,
                deviceName = apiAlarm.originatorLabel,
                isActive = !apiAlarm.cleared,      // Set based on cleared flag
                floorIndex = floorIndex
            };

            alarmDataList.Add(alarmData);
        }

        return alarmDataList;
    }

    private string MapSeverity(string apiSeverity)
    {
        // Map API severity to UI severity (keep original naming from API)
        return apiSeverity.ToUpper() switch
        {
            "CRITICAL" => "Critical",
            "MAJOR" => "Major",
            "WARNING" => "Warning",
            "MINOR" => "Minor",
            "INDETERMINATE" => "Indeterminate",
            _ => "Indeterminate"
        };
    }

    private string FormatStatus(string status)
    {
        // Format: ACTIVE_UNACK -> ACT_UNACK
        if (status.StartsWith("ACTIVE"))
        {
            return status.Replace("ACTIVE", "ACT");
        }
        else if (status.StartsWith("CLEARED"))
        {
            return status.Replace("CLEARED", "CLR");
        }
        return status;
    }

    private int GetConsistentFloorIndex(string alarmId)
    {
        // Check if this alarm already has a floor assigned
        if (alarmFloorMap.ContainsKey(alarmId))
        {
            return alarmFloorMap[alarmId];
        }

        // Assign new floor index
        int floorIndex;
        if (randomFloorAssignment)
        {
            // Use hash-based assignment for consistency across sessions
            int hash = alarmId.GetHashCode();
            floorIndex = Mathf.Abs(hash) % 3; // 0, 1, or 2
        }
        else
        {
            floorIndex = 0;
        }

        // Store the assignment
        alarmFloorMap[alarmId] = floorIndex;

        return floorIndex;
    }

    // ============ Public Filter Update Methods ============

    /// <summary>
    /// Updates the floor entity filter and resubscribes to WebSocket with new entity ID
    /// Called by FloorTransitionManager when floor selection changes
    /// </summary>
    public void UpdateFloorEntityFilter(string entityId)
    {
        if (!isWebSocketConnected || webSocket == null)
        {
            Debug.LogWarning("[MasterAlarm] Cannot update floor filter - WebSocket not connected");
            return;
        }

        if (string.IsNullOrEmpty(entityId))
        {
            Debug.LogWarning("[MasterAlarm] Entity ID is null or empty - ignoring update");
            return;
        }

        Debug.Log($"[MasterAlarm] Updating floor entity filter to: {entityId}");

        // Update current entity ID
        currentFloorEntityId = entityId;

        // Start coroutine to unsubscribe and resubscribe
        StartCoroutine(ResubscribeWithNewFilters());
    }

    /// <summary>
    /// Updates the filters with pagination support and resubscribes to WebSocket
    /// Called by AlarmFilterPanel when user changes filters or navigates pages
    /// </summary>
    public void UpdateFiltersWithPagination(List<string> statusList, List<string> severityList, int pageNumber, int pageSize)
    {
        if (!isWebSocketConnected || webSocket == null)
        {
            Debug.LogWarning("[MasterAlarm] Cannot update filters - WebSocket not connected");
            return;
        }

        Debug.Log($"[MasterAlarm] Updating filters with pagination - Page: {pageNumber}, PageSize: {pageSize}");
        Debug.Log($"[MasterAlarm] Status: [{string.Join(", ", statusList)}], Severity: [{string.Join(", ", severityList)}]");

        // Update current filters
        currentStatusFilter = new List<string>(statusList);
        currentSeverityFilter = new List<string>(severityList);

        // Update pagination state
        lastRequestedPage = pageNumber;
        lastRequestedPageSize = pageSize;

        // Start coroutine to unsubscribe and resubscribe
        StartCoroutine(ResubscribeWithNewFilters());
    }

    /// <summary>
    /// Updates the status and severity filters and resubscribes to WebSocket with new filters
    /// Called by AlarmFilterPanel when user clicks Update button (legacy method without pagination)
    /// </summary>
    public void UpdateFilters(List<string> statusList, List<string> severityList)
    {
        // Call the pagination version with default page settings
        UpdateFiltersWithPagination(statusList, severityList, 0, lastRequestedPageSize);
    }

    // ============ SENSOR DATA FETCHING (HVAC, Lights, Presence, Air Quality) ============

    /// <summary>
    /// Generic method to request sensor data from a room
    /// Flow: 1) Discover equipment in room → 2) Request telemetry data → 3) Callback with data
    /// </summary>
    /// <param name="roomEntityID">The room's Entity ID</param>
    /// <param name="assetType">Asset type: "HVAC", "Lights", "Presence", "Air Quality"</param>
    /// <param name="telemetryKeys">List of telemetry keys to fetch (e.g., ["AmbientTemperature", "SetTemperature"])</param>
    /// <param name="onDataReceived">Callback with dictionary of key→value (e.g., "AmbientTemperature"→"22.5")</param>
    public void RequestSensorData(string roomEntityID, string assetType, List<string> telemetryKeys, System.Action<Dictionary<string, string>> onDataReceived)
    {
        if (!isWebSocketConnected || webSocket == null)
        {
            Debug.LogError("[MasterAlarm] Cannot request sensor data - WebSocket not connected");
            onDataReceived?.Invoke(null); // Return null to indicate failure
            return;
        }

        if (string.IsNullOrEmpty(roomEntityID))
        {
            Debug.LogError("[MasterAlarm] Room Entity ID is null or empty");
            onDataReceived?.Invoke(null);
            return;
        }

        Debug.Log($"[MasterAlarm] 🔍 Requesting {assetType} data for room: {roomEntityID}");
        Debug.Log($"[MasterAlarm] Telemetry keys: [{string.Join(", ", telemetryKeys)}]");

        // Step 1: Send equipment discovery request
        int discoveryCmdId = nextSensorCmdId++;

        // Store request info for when discovery response comes back
        sensorDiscoveryRequests[discoveryCmdId] = new SensorRequestInfo
        {
            assetType = assetType,
            telemetryKeys = telemetryKeys,
            callback = onDataReceived
        };

        SendEquipmentDiscovery(roomEntityID, assetType, discoveryCmdId);
    }

    private async void SendEquipmentDiscovery(string roomEntityID, string assetType, int cmdId)
    {
        try
        {
            // Get telemetry keys for this request
            if (!sensorDiscoveryRequests.TryGetValue(cmdId, out SensorRequestInfo requestInfo))
            {
                Debug.LogError($"[MasterAlarm] No request info found for cmdId: {cmdId}");
                return;
            }

            // Build latestValues JSON array from telemetry keys
            StringBuilder latestValuesJson = new StringBuilder();
            latestValuesJson.Append("[");
            for (int i = 0; i < requestInfo.telemetryKeys.Count; i++)
            {
                latestValuesJson.Append(@"{""type"": ""TIME_SERIES"", ""key"": """);
                latestValuesJson.Append(requestInfo.telemetryKeys[i]);
                latestValuesJson.Append(@"""}");
                if (i < requestInfo.telemetryKeys.Count - 1)
                {
                    latestValuesJson.Append(", ");
                }
            }
            latestValuesJson.Append("]");

            // Build keyFilters for first telemetry key (ensures we get equipment with data)
            string firstTelemetryKey = requestInfo.telemetryKeys.Count > 0 ? requestInfo.telemetryKeys[0] : "Ambient Temperature";

            // Build request matching working example structure
            string jsonCommand = @"{
                ""cmds"": [{
                    ""type"": ""ENTITY_DATA"",
                    ""query"": {
                        ""entityFilter"": {
                            ""type"": ""assetSearchQuery"",
                            ""resolveMultiple"": true,
                            ""rootStateEntity"": true,
                            ""stateEntityParamName"": ""selectedEntity"",
                            ""defaultStateEntity"": {
                                ""entityType"": ""ASSET"",
                                ""id"": ""0cc4e030-67a1-11f0-a54f-c718692b063f""
                            },
                            ""rootEntity"": {
                                ""entityType"": ""ASSET"",
                                ""id"": """ + roomEntityID + @"""
                            },
                            ""direction"": ""FROM"",
                            ""maxLevel"": 5,
                            ""fetchLastLevelOnly"": false,
                            ""relationType"": ""has"",
                            ""assetTypes"": [""" + assetType + @"""]
                        },
                        ""pageLink"": {
                            ""pageSize"": 1,
                            ""page"": 0,
                            ""sortOrder"": {
                                ""key"": {
                                    ""type"": ""ENTITY_FIELD"",
                                    ""key"": ""createdTime""
                                },
                                ""direction"": ""DESC""
                            }
                        },
                        ""keyFilters"": [{
                            ""key"": {
                                ""type"": ""TIME_SERIES"",
                                ""key"": """ + firstTelemetryKey + @"""
                            },
                            ""valueType"": ""NUMERIC"",
                            ""predicate"": {
                                ""operation"": ""NOT_EQUAL"",
                                ""value"": {
                                    ""defaultValue"": 0
                                },
                                ""type"": ""NUMERIC""
                            }
                        }],
                        ""entityFields"": [
                            {""type"": ""ENTITY_FIELD"", ""key"": ""name""},
                            {""type"": ""ENTITY_FIELD"", ""key"": ""label""},
                            {""type"": ""ENTITY_FIELD"", ""key"": ""additionalInfo""}
                        ],
                        ""latestValues"": " + latestValuesJson.ToString() + @"
                    },
                    ""cmdId"": " + cmdId + @"
                }]
            }";

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                Debug.Log($"[MasterAlarm] 📤 SENSOR DATA REQUEST BEING SENT:\n{jsonCommand}");
                await webSocket.SendText(jsonCommand);
                Debug.Log($"[MasterAlarm] ✓ Sensor data request sent for {assetType} (cmdId: {cmdId})");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MasterAlarm] Failed to send sensor data request: {e.Message}");

            // Invoke callback with null to indicate failure
            if (sensorDiscoveryRequests.TryGetValue(cmdId, out SensorRequestInfo requestInfo))
            {
                requestInfo.callback?.Invoke(null);
                sensorDiscoveryRequests.Remove(cmdId);
            }
        }
    }


    private IEnumerator ResubscribeWithNewFilters()
    {
        // Step 1: Unsubscribe from current alarm subscription
        Debug.Log("[MasterAlarm] Step 1: Unsubscribing from current alarm data...");
        SendAlarmUnsubscribe();

        // No wait needed - let the floor transition animation provide natural breathing room
        // The server processes unsubscribe/subscribe asynchronously, and the floor animation
        // gives plenty of time for the subscription to update smoothly

        // Step 2: Resubscribe with new filters immediately
        Debug.Log("[MasterAlarm] Step 2: Resubscribing with new filters (floor transition provides natural delay)...");
        SendAlarmSubscription();

        yield break;
    }

    // ============ WebSocket Methods ============

    private async void ConnectWebSocket()
    {
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            Debug.Log("[MasterAlarm] WebSocket already connected");
            return;
        }

        try
        {
            string wsUrl = $"wss://pulse.alec.ae/api/ws?token={bearerToken}";
            Debug.Log($"[MasterAlarm] Connecting to WebSocket: {wsUrl.Substring(0, Math.Min(50, wsUrl.Length))}...");

            webSocket = new WebSocket(wsUrl);

            webSocket.OnOpen += OnWebSocketOpen;
            webSocket.OnMessage += OnWebSocketMessage;
            webSocket.OnError += OnWebSocketError;
            webSocket.OnClose += OnWebSocketClose;

            await webSocket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MasterAlarm] WebSocket connection failed: {e.Message}");
            if (autoReconnect && isFetching)
            {
                ScheduleReconnect();
            }
        }
    }

    private void OnWebSocketOpen()
    {
        Debug.Log("[MasterAlarm] WebSocket connected successfully!");
        isWebSocketConnected = true;
        reconnectDelay = 1f; // Reset reconnect delay on successful connection

        // Send alarm subscription (server will push updates automatically)
        SendAlarmSubscription();
    }

    private void OnWebSocketMessage(byte[] data)
    {
        try
        {
            string jsonMessage = Encoding.UTF8.GetString(data);

            // Log ALL messages to help debug subscription updates
            if (jsonMessage.Length > 500)
            {
                Debug.Log($"[MasterAlarm] WS Message ({jsonMessage.Length} chars): {jsonMessage.Substring(0, 300)}...");
            }
            else
            {
                Debug.Log($"[MasterAlarm] WS Message: {jsonMessage}");
            }

            // First, try to extract cmdId to determine message type
            var messageDict = MiniJSON.Json.Deserialize(jsonMessage) as Dictionary<string, object>;
            if (messageDict != null && messageDict.ContainsKey("cmdId"))
            {
                int cmdId = Convert.ToInt32(messageDict["cmdId"]);

                // Check if this is a sensor data response (cmdId >= 100)
                if (sensorDiscoveryRequests.ContainsKey(cmdId))
                {
                    HandleSensorDiscoveryResponse(jsonMessage, cmdId);
                    return;
                }
            }

            // Otherwise, parse as alarm data response
            WebSocketResponse wsResponse = JsonUtility.FromJson<WebSocketResponse>(jsonMessage);

            if (wsResponse != null && wsResponse.data != null && wsResponse.data.data != null)
            {
                // Convert API alarms to AlarmData
                List<AlarmData> alarmDataList = ConvertToAlarmData(wsResponse.data.data);

                Debug.Log($"[MasterAlarm] ✓ Parsed {alarmDataList.Count} active alarms from WebSocket");

                // Update TextMeshPro display
                UpdateAlarmDisplay(alarmDataList);

                // Send data to SlidingPanelController
                OnAlarmsReceived?.Invoke(alarmDataList);
            }
            else
            {
                // This might be a different message type (subscription confirmation, error, etc.)
                Debug.Log("[MasterAlarm] WebSocket message (no alarm data) - might be subscription response or update");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MasterAlarm] Failed to parse WebSocket message: {e.Message}");
        }
    }

    private void HandleSensorDiscoveryResponse(string jsonMessage, int cmdId)
    {
        try
        {
            Debug.Log($"[MasterAlarm] 📥 Sensor data response received (cmdId: {cmdId})");

            // Parse response using MiniJSON (matches working example structure)
            var responseDict = MiniJSON.Json.Deserialize(jsonMessage) as Dictionary<string, object>;
            if (responseDict == null || !responseDict.ContainsKey("data"))
            {
                Debug.LogWarning("[MasterAlarm] Response has no data field");
                InvokeDiscoveryCallbackWithNull(cmdId);
                return;
            }

            var dataDict = responseDict["data"] as Dictionary<string, object>;
            if (dataDict == null || !dataDict.ContainsKey("data"))
            {
                Debug.LogWarning("[MasterAlarm] Response data.data is null");
                InvokeDiscoveryCallbackWithNull(cmdId);
                return;
            }

            var dataArray = dataDict["data"] as List<object>;
            if (dataArray == null || dataArray.Count == 0)
            {
                Debug.LogWarning($"[MasterAlarm] No equipment found in room");
                InvokeDiscoveryCallbackWithNull(cmdId);
                return;
            }

            // Extract first equipment data
            var equipmentDict = dataArray[0] as Dictionary<string, object>;
            if (equipmentDict == null)
            {
                Debug.LogWarning("[MasterAlarm] Equipment data is null");
                InvokeDiscoveryCallbackWithNull(cmdId);
                return;
            }

            // Extract entityId (for logging)
            string equipmentEntityID = "unknown";
            if (equipmentDict.ContainsKey("entityId"))
            {
                var entityIdDict = equipmentDict["entityId"] as Dictionary<string, object>;
                if (entityIdDict != null && entityIdDict.ContainsKey("id"))
                {
                    equipmentEntityID = entityIdDict["id"] as string;
                }
            }
            Debug.Log($"[MasterAlarm] ✓ Equipment found: {equipmentEntityID}");

            // Extract latest TIME_SERIES data (this contains the temperature values!)
            if (!equipmentDict.ContainsKey("latest"))
            {
                Debug.LogWarning("[MasterAlarm] Equipment has no latest field");
                InvokeDiscoveryCallbackWithNull(cmdId);
                return;
            }

            var latestDict = equipmentDict["latest"] as Dictionary<string, object>;
            if (latestDict == null || !latestDict.ContainsKey("TIME_SERIES"))
            {
                Debug.LogWarning("[MasterAlarm] Equipment latest has no TIME_SERIES field");
                InvokeDiscoveryCallbackWithNull(cmdId);
                return;
            }

            var timeSeriesDict = latestDict["TIME_SERIES"] as Dictionary<string, object>;
            if (timeSeriesDict == null)
            {
                Debug.LogWarning("[MasterAlarm] TIME_SERIES is null");
                InvokeDiscoveryCallbackWithNull(cmdId);
                return;
            }

            // Extract telemetry values into dictionary
            Dictionary<string, string> telemetryData = new Dictionary<string, string>();
            foreach (var kvp in timeSeriesDict)
            {
                var valueDict = kvp.Value as Dictionary<string, object>;
                if (valueDict != null && valueDict.ContainsKey("value"))
                {
                    string value = valueDict["value"] as string;
                    telemetryData[kvp.Key] = value;
                    Debug.Log($"[MasterAlarm] ✓ {kvp.Key}: {value}");
                }
            }

            // Get request info and invoke callback
            if (sensorDiscoveryRequests.TryGetValue(cmdId, out SensorRequestInfo requestInfo))
            {
                requestInfo.callback?.Invoke(telemetryData);
                sensorDiscoveryRequests.Remove(cmdId);
                Debug.Log($"[MasterAlarm] ✓✓✓ Sensor data callback invoked with {telemetryData.Count} values");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MasterAlarm] Error handling sensor data response: {e.Message}");
            InvokeDiscoveryCallbackWithNull(cmdId);
        }
    }

    private void InvokeDiscoveryCallbackWithNull(int cmdId)
    {
        if (sensorDiscoveryRequests.TryGetValue(cmdId, out SensorRequestInfo requestInfo))
        {
            requestInfo.callback?.Invoke(null);
            sensorDiscoveryRequests.Remove(cmdId);
        }
    }

    private void OnWebSocketError(string errorMsg)
    {
        Debug.LogError($"[MasterAlarm] WebSocket error: {errorMsg}");
    }

    private void OnWebSocketClose(WebSocketCloseCode closeCode)
    {
        Debug.Log($"[MasterAlarm] WebSocket closed with code: {closeCode}");
        isWebSocketConnected = false;

        if (autoReconnect && isFetching && startFetching)
        {
            Debug.Log("[MasterAlarm] WebSocket disconnected, scheduling reconnection...");
            ScheduleReconnect();
        }
    }

    private string BuildStatusListJson()
    {
        // If no filter is set, return empty array (show all statuses)
        if (currentStatusFilter == null || currentStatusFilter.Count == 0)
        {
            return "[]";
        }

        // Build JSON array string from current status filter
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < currentStatusFilter.Count; i++)
        {
            sb.Append("\"");
            sb.Append(currentStatusFilter[i]);
            sb.Append("\"");
            if (i < currentStatusFilter.Count - 1)
            {
                sb.Append(", ");
            }
        }
        sb.Append("]");

        return sb.ToString();
    }

    private string BuildSeverityListJson()
    {
        // If no filter is set, return empty array (show all severities)
        if (currentSeverityFilter == null || currentSeverityFilter.Count == 0)
        {
            return "[]";
        }

        // Build JSON array string from current severity filter
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < currentSeverityFilter.Count; i++)
        {
            sb.Append("\"");
            sb.Append(currentSeverityFilter[i]);
            sb.Append("\"");
            if (i < currentSeverityFilter.Count - 1)
            {
                sb.Append(", ");
            }
        }
        sb.Append("]");

        return sb.ToString();
    }

    private async void SendAlarmSubscription()
    {
        try
        {
            // Build filter JSON strings
            string statusListJson = BuildStatusListJson();
            string severityListJson = BuildSeverityListJson();

            // Use raw JSON string matching EXACT web format
            string jsonCommand = @"{
                ""cmds"": [{
                    ""type"": ""ALARM_DATA"",
                    ""query"": {
                        ""entityFilter"": {
                            ""type"": ""assetSearchQuery"",
                            ""resolveMultiple"": true,
                            ""rootStateEntity"": true,
                            ""stateEntityParamName"": ""selectedEntity"",
                            ""defaultStateEntity"": {
                                ""entityType"": ""ASSET"",
                                ""id"": ""0cc4e030-67a1-11f0-a54f-c718692b063f""
                            },
                            ""rootEntity"": {
                                ""entityType"": ""ASSET"",
                                ""id"": """ + currentFloorEntityId + @"""
                            },
                            ""direction"": ""FROM"",
                            ""maxLevel"": 5,
                            ""fetchLastLevelOnly"": false,
                            ""relationType"": ""has"",
                            ""assetTypes"": [""Air Quality"", ""Fire"", ""Lights"", ""Presence"", ""HVAC""]
                        },
                        ""pageLink"": {
                            ""page"": " + lastRequestedPage + @",
                            ""pageSize"": " + lastRequestedPageSize + @",
                            ""textSearch"": null,
                            ""typeList"": null,
                            ""severityList"": " + severityListJson + @",
                            ""statusList"": " + statusListJson + @",
                            ""searchPropagatedAlarms"": true,
                            ""assigneeId"": null,
                            ""sortOrder"": {
                                ""key"": {
                                    ""key"": ""createdTime"",
                                    ""type"": ""ALARM_FIELD""
                                },
                                ""direction"": ""DESC""
                            },
                            ""timeWindow"": " + wsTimeWindow + @"
                        },
                        ""alarmFields"": [
                            {""type"": ""ALARM_FIELD"", ""key"": ""createdTime""},
                            {""type"": ""ALARM_FIELD"", ""key"": ""originator""},
                            {""type"": ""ALARM_FIELD"", ""key"": ""type""},
                            {""type"": ""ALARM_FIELD"", ""key"": ""severity""},
                            {""type"": ""ALARM_FIELD"", ""key"": ""status""},
                            {""type"": ""ALARM_FIELD"", ""key"": ""assignee""}
                        ],
                        ""entityFields"": [],
                        ""latestValues"": []
                    },
                    ""cmdId"": 2
                }]
            }";

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                // Debug: Print the exact WebSocket message being sent
                Debug.Log($"[MasterAlarm] 📤 WebSocket Message Being Sent:\n{jsonCommand}");

                await webSocket.SendText(jsonCommand);
                Debug.Log("[MasterAlarm] ► Alarm subscription sent (cmdId: 2) - waiting for server push updates...");
            }
            else
            {
                Debug.LogError("[MasterAlarm] Cannot send alarm subscription - WebSocket not connected");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MasterAlarm] Failed to send alarm subscription: {e.Message}");
        }
    }

    private async void SendAlarmUnsubscribe()
    {
        try
        {
            string jsonCommand = @"{
                ""cmds"": [{
                    ""type"": ""ALARM_DATA_UNSUBSCRIBE"",
                    ""cmdId"": 2
                }]
            }";

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                await webSocket.SendText(jsonCommand);
                Debug.Log("[MasterAlarm] ■ Alarm unsubscribe sent (cmdId: 2)");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MasterAlarm] Failed to send alarm unsubscribe: {e.Message}");
        }
    }

    private void ScheduleReconnect()
    {
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
        }

        reconnectCoroutine = StartCoroutine(ReconnectWithBackoff());
    }

    private IEnumerator ReconnectWithBackoff()
    {
        Debug.Log($"[MasterAlarm] Reconnecting in {reconnectDelay} seconds...");
        yield return new WaitForSeconds(reconnectDelay);

        // Exponential backoff with max cap
        reconnectDelay = Mathf.Min(reconnectDelay * 2f, maxReconnectDelay);

        // Re-authenticate if needed, then reconnect
        if (!isAuthenticated)
        {
            Debug.Log("[MasterAlarm] Re-authenticating before WebSocket reconnection...");
            yield return StartCoroutine(Authenticate());
        }

        if (isAuthenticated && isFetching && startFetching)
        {
            ConnectWebSocket();
        }
    }

    private async void DisconnectWebSocket()
    {
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }

        if (webSocket != null)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    // Unsubscribe from alarm updates before closing
                    SendAlarmUnsubscribe();

                    // Wait a moment for unsubscribe to send
                    await System.Threading.Tasks.Task.Delay(100);

                    await webSocket.Close();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MasterAlarm] Error closing WebSocket: {e.Message}");
            }

            webSocket = null;
            isWebSocketConnected = false;
            Debug.Log("[MasterAlarm] WebSocket disconnected");
        }
    }

    private void UpdateAlarmDisplay(List<AlarmData> alarms)
    {
        if (alarmDisplayText == null)
        {
            Debug.LogWarning("[MasterAlarm] Alarm Display TextMeshPro is not assigned!");
            return;
        }

        if (alarms == null || alarms.Count == 0)
        {
            alarmDisplayText.text = "No active alarms";
            return;
        }

        StringBuilder displayText = new StringBuilder();
        displayText.AppendLine($"=== BUILDING ALARMS ({alarms.Count}) ===\n");

        for (int i = 0; i < alarms.Count; i++)
        {
            var alarm = alarms[i];
            displayText.AppendLine($"--- Alarm {i + 1} ---");
            displayText.AppendLine($"Type: {alarm.description}");
            displayText.AppendLine($"Severity: {alarm.severity}");
            displayText.AppendLine($"Location: {alarm.location}");
            displayText.AppendLine($"Device: {alarm.deviceName}");
            displayText.AppendLine($"Time: {alarm.timestamp}");
            displayText.AppendLine($"Floor: {GetFloorName(alarm.floorIndex)}");
            displayText.AppendLine($"Status: {(alarm.isActive ? "ACTIVE" : "CLEARED")}");
            displayText.AppendLine();
        }

        alarmDisplayText.text = displayText.ToString();
        Debug.Log("[MasterAlarm] TextMeshPro display updated with alarm data");
    }

    private string GetFloorName(int floorIndex)
    {
        switch (floorIndex)
        {
            case 0: return "Ground Floor";
            case 1: return "First Floor";
            case 2: return "Roof";
            default: return $"Floor {floorIndex}";
        }
    }

    private void OnDestroy()
    {
        StopFetchingData();
    }
}
