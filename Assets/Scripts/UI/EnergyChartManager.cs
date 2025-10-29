using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NativeWebSocket;
using XCharts.Runtime;

// WebSocket response structures for energy data
[System.Serializable]
public class EnergyWebSocketResponse
{
    public int cmdId;
    public object data;
    public List<EnergyEntityUpdate> update;
    public int errorCode;
    public string errorMsg;
}

[System.Serializable]
public class EnergyEntityUpdate
{
    public EntityId entityId;
    public bool readAttrs;
    public bool readTs;
    public TimeseriesData timeseries;
}

[System.Serializable]
public class EntityId
{
    public string entityType;
    public string id;
}

[System.Serializable]
public class TimeseriesData
{
    public List<TimeseriesPoint> _ECD_BuildingWiseHourlyEnergyConsumption;
}

[System.Serializable]
public class TimeseriesPoint
{
    public long ts;          // Unix timestamp in milliseconds
    public string value;     // Energy consumption value as string
}

public class EnergyChartManager : MonoBehaviour
{
    [Header("Chart Reference")]
    [SerializeField] private LineChart energyChart;

    [Header("WebSocket Settings")]
    [SerializeField] private string apiBaseUrl = "https://pulse.alec.ae/api";
    [SerializeField] private string entityId = "b6499190-6798-11f0-a54f-c718692b063f"; // DIC/Main building
    [SerializeField] private int energyCmdId = 50; // Unique command ID for energy data

    [Header("Data Settings")]
    [SerializeField] private bool fetchHourlyData = true; // true = hourly (24h), false = daily (28 days)
    [SerializeField] private string timeZoneId = "Asia/Dubai";

    [Header("Authentication")]
    [SerializeField] private MasterAlarm masterAlarm; // Reference to get bearer token

    [Header("Control")]
    [SerializeField] private bool startFetching = false;

    private WebSocket energyWebSocket = null;
    private bool isWebSocketConnected = false;
    private bool isFetching = false;
    private string bearerToken = "";

    private void Start()
    {
        // Auto-find LineChart if not assigned
        if (energyChart == null)
        {
            energyChart = GetComponent<LineChart>();
        }

        if (energyChart == null)
        {
            Debug.LogError("[EnergyChart] LineChart component not found! Please assign it in the Inspector.");
            return;
        }

        // Initialize the chart
        InitializeChart();
    }

    private void Update()
    {
        // Dispatch WebSocket messages on main thread
        #if !UNITY_WEBGL || UNITY_EDITOR
        energyWebSocket?.DispatchMessageQueue();
        #endif

        if (startFetching && !isFetching)
        {
            StartFetchingEnergyData();
        }
        else if (!startFetching && isFetching)
        {
            StopFetchingEnergyData();
        }
    }

    private void InitializeChart()
    {
        Debug.Log("[EnergyChart] Initializing line chart...");

        // Set chart title
        var title = energyChart.EnsureChartComponent<Title>();
        title.show = true;
        title.text = "Energy Consumption";
        title.subText = fetchHourlyData ? "Hourly Data (Today)" : "Daily Data (This Month)";

        // Enable tooltip
        var tooltip = energyChart.EnsureChartComponent<Tooltip>();
        tooltip.show = true;

        // Enable legend
        var legend = energyChart.EnsureChartComponent<Legend>();
        legend.show = false; // Single series, no need for legend

        // Configure X-Axis (Category - Time labels)
        var xAxis = energyChart.EnsureChartComponent<XAxis>();
        xAxis.show = true;
        xAxis.type = Axis.AxisType.Category;
        xAxis.splitNumber = fetchHourlyData ? 24 : 28;
        xAxis.boundaryGap = true;

        // Configure Y-Axis (Value - Energy consumption)
        var yAxis = energyChart.EnsureChartComponent<YAxis>();
        yAxis.show = true;
        yAxis.type = Axis.AxisType.Value;
        yAxis.minMaxType = Axis.AxisMinMaxType.Default;

        // Clear any existing data
        energyChart.RemoveData();

        // Add a Line series
        var serie = energyChart.AddSerie<Line>("Energy");
        serie.lineType = LineType.Smooth; // Use Smooth line type for smooth curves

        // Enable area fill under the line
        serie.EnsureComponent<AreaStyle>();

        // Enable data point labels
        var label = serie.EnsureComponent<LabelStyle>();
        label.show = false; // Hide labels by default, show in tooltip

        // Enable animations
        serie.animation.enable = true;
        serie.animation.change.enable = true; // Enable animation on data change

        Debug.Log("[EnergyChart] Chart initialized successfully");
    }

    private void StartFetchingEnergyData()
    {
        if (masterAlarm == null)
        {
            Debug.LogError("[EnergyChart] MasterAlarm reference not set! Please assign it in the Inspector.");
            startFetching = false;
            return;
        }

        if (!masterAlarm.IsAuthenticated)
        {
            Debug.LogError("[EnergyChart] MasterAlarm is not authenticated. Please start MasterAlarm first.");
            startFetching = false;
            return;
        }

        bearerToken = masterAlarm.BearerToken;
        Debug.Log("[EnergyChart] Starting energy data fetching...");

        isFetching = true;
        ConnectWebSocket();
    }

    private void StopFetchingEnergyData()
    {
        Debug.Log("[EnergyChart] Stopping energy data fetching...");
        isFetching = false;
        DisconnectWebSocket();
    }

    private async void ConnectWebSocket()
    {
        if (energyWebSocket != null && energyWebSocket.State == WebSocketState.Open)
        {
            Debug.Log("[EnergyChart] WebSocket already connected");
            return;
        }

        try
        {
            string wsUrl = $"wss://pulse.alec.ae/api/ws?token={bearerToken}";
            Debug.Log($"[EnergyChart] Connecting to WebSocket...");

            energyWebSocket = new WebSocket(wsUrl);

            energyWebSocket.OnOpen += OnWebSocketOpen;
            energyWebSocket.OnMessage += OnWebSocketMessage;
            energyWebSocket.OnError += OnWebSocketError;
            energyWebSocket.OnClose += OnWebSocketClose;

            await energyWebSocket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyChart] WebSocket connection failed: {e.Message}");
        }
    }

    private void DisconnectWebSocket()
    {
        if (energyWebSocket != null)
        {
            energyWebSocket.OnOpen -= OnWebSocketOpen;
            energyWebSocket.OnMessage -= OnWebSocketMessage;
            energyWebSocket.OnError -= OnWebSocketError;
            energyWebSocket.OnClose -= OnWebSocketClose;

            if (energyWebSocket.State == WebSocketState.Open)
            {
                energyWebSocket.Close();
            }

            energyWebSocket = null;
        }

        isWebSocketConnected = false;
        Debug.Log("[EnergyChart] WebSocket disconnected");
    }

    private void OnWebSocketOpen()
    {
        Debug.Log("[EnergyChart] WebSocket connected successfully!");
        isWebSocketConnected = true;

        // Send energy data request
        SendEnergyDataRequest();
    }

    private void SendEnergyDataRequest()
    {
        // Calculate timestamps for the query
        DateTime now = DateTime.UtcNow;
        DateTime startTime;
        DateTime endTime;
        long interval;
        int limit;

        if (fetchHourlyData)
        {
            // Get today's data (hourly intervals) - from start of today to end of today
            startTime = now.Date; // Start of today (00:00)
            endTime = startTime.AddDays(1); // End of today (00:00 next day)
            interval = 3600000; // 1 hour in milliseconds
            limit = 24;
        }
        else
        {
            // Get this month's data (daily intervals)
            startTime = new DateTime(now.Year, now.Month, 1); // Start of current month
            endTime = startTime.AddMonths(1); // Start of next month
            interval = 86400000; // 1 day in milliseconds
            limit = 28;
        }

        long startTs = ((DateTimeOffset)startTime).ToUnixTimeMilliseconds();
        long endTs = ((DateTimeOffset)endTime).ToUnixTimeMilliseconds();

        // Build the WebSocket command (following the pattern from helper file)
        string jsonCommand = $@"
{{
    ""cmds"": [
        {{
            ""type"": ""ENTITY_DATA"",
            ""cmdId"": {energyCmdId},
            ""historyCmd"": {{
                ""keys"": [""_ECD_BuildingWiseHourlyEnergyConsumption""],
                ""startTs"": {startTs},
                ""endTs"": {endTs},
                ""interval"": {interval},
                ""intervalType"": ""MILLISECONDS"",
                ""limit"": {limit},
                ""timeZoneId"": ""{timeZoneId}"",
                ""agg"": ""SUM""
            }}
        }}
    ]
}}";

        Debug.Log($"[EnergyChart] Sending energy data request (cmdId: {energyCmdId})...");
        Debug.Log($"[EnergyChart] Time range: {startTime} to {endTime}");
        Debug.Log($"[EnergyChart] Duration: {(endTs - startTs) / 1000 / 3600} hours");
        Debug.Log($"[EnergyChart] 📤 EXACT WebSocket Message Being Sent:\n{jsonCommand}");

        energyWebSocket.SendText(jsonCommand);
    }

    private void OnWebSocketMessage(byte[] data)
    {
        try
        {
            string jsonMessage = Encoding.UTF8.GetString(data);

            Debug.Log($"[EnergyChart] Received message: {jsonMessage.Substring(0, Math.Min(300, jsonMessage.Length))}...");

            // Parse the response
            EnergyWebSocketResponse response = JsonUtility.FromJson<EnergyWebSocketResponse>(jsonMessage);

            // Check if this is our energy data response (matching cmdId)
            if (response != null && response.cmdId == energyCmdId)
            {
                Debug.Log($"[EnergyChart] Received energy data response (cmdId: {energyCmdId})");

                if (response.errorCode == 0 && response.update != null && response.update.Count > 0)
                {
                    ProcessEnergyData(response.update[0]);
                }
                else
                {
                    Debug.LogWarning($"[EnergyChart] Error in response or no data. ErrorCode: {response.errorCode}, ErrorMsg: {response.errorMsg}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyChart] Failed to parse WebSocket message: {e.Message}");
            Debug.LogError($"[EnergyChart] Stack trace: {e.StackTrace}");
        }
    }

    private void ProcessEnergyData(EnergyEntityUpdate entityUpdate)
    {
        if (entityUpdate.timeseries == null ||
            entityUpdate.timeseries._ECD_BuildingWiseHourlyEnergyConsumption == null)
        {
            Debug.LogWarning("[EnergyChart] No timeseries data in response");
            return;
        }

        var dataPoints = entityUpdate.timeseries._ECD_BuildingWiseHourlyEnergyConsumption;
        Debug.Log($"[EnergyChart] Processing {dataPoints.Count} energy data points");

        // Clear existing data
        energyChart.ClearData();

        // Add data to chart
        foreach (var point in dataPoints)
        {
            // Convert timestamp to readable format
            DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(point.ts).ToLocalTime().DateTime;
            string timeLabel;

            if (fetchHourlyData)
            {
                // For hourly: show time like "00:00", "06:00", "12:00"
                timeLabel = dateTime.ToString("HH:mm");
            }
            else
            {
                // For daily: show date like "Jan 1", "Jan 2"
                timeLabel = dateTime.ToString("MMM dd");
            }

            // Parse energy value
            if (float.TryParse(point.value, out float energyValue))
            {
                // Add to chart
                energyChart.AddXAxisData(timeLabel);
                energyChart.AddData(0, energyValue);
            }
            else
            {
                Debug.LogWarning($"[EnergyChart] Failed to parse value: {point.value}");
            }
        }

        Debug.Log($"[EnergyChart] ✓ Chart updated with {dataPoints.Count} data points");
    }

    private void OnWebSocketError(string errorMsg)
    {
        Debug.LogError($"[EnergyChart] WebSocket error: {errorMsg}");
    }

    private void OnWebSocketClose(WebSocketCloseCode closeCode)
    {
        Debug.Log($"[EnergyChart] WebSocket closed with code: {closeCode}");
        isWebSocketConnected = false;
    }

    private void OnDestroy()
    {
        DisconnectWebSocket();
    }
}
