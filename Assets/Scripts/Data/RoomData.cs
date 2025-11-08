using System;
using System.Collections.Generic;

/// <summary>
/// Data class representing a sensor from roomdata+sensor.json
/// </summary>
[System.Serializable]
public class SensorData
{
    [UnityEngine.SerializeField]
    private string SensorID_FIELD;

    [UnityEngine.SerializeField]
    private string SensorName_FIELD;

    [UnityEngine.SerializeField]
    private string SensorType_FIELD;

    // Properties to handle "Sensor ID", "Sensor Name", "Sensor Type" fields with spaces
    public string SensorID
    {
        get { return SensorID_FIELD; }
        set { SensorID_FIELD = value; }
    }

    public string SensorName
    {
        get { return SensorName_FIELD; }
        set { SensorName_FIELD = value; }
    }

    public string SensorType
    {
        get { return SensorType_FIELD; }
        set { SensorType_FIELD = value; }
    }

    // Constructor
    public SensorData(string sensorID, string sensorName, string sensorType)
    {
        SensorID = sensorID;
        SensorName = sensorName;
        SensorType = sensorType;
    }

    public override string ToString()
    {
        return $"Sensor: {SensorName} | Type: {SensorType} | ID: {SensorID}";
    }
}

/// <summary>
/// Data class representing a room from RoomData.json
/// Matches the JSON structure exactly
/// Note: Field name MUST match JSON exactly including spaces
/// </summary>
[System.Serializable]
public class RoomData
{
    // Field name matches JSON exactly (with space)
    [UnityEngine.SerializeField]
    private string EntityID_FIELD; // Temporary workaround

    public string Name;
    public string Floor;
    public List<SensorData> Sensors; // List of sensors in this room

    // Expose as property
    public string EntityID
    {
        get { return EntityID_FIELD; }
        set { EntityID_FIELD = value; }
    }

    // Constructor for easy creation
    public RoomData(string entityID, string name, string floor)
    {
        EntityID = entityID;
        Name = name;
        Floor = floor;
        Sensors = new List<SensorData>();
    }

    // Constructor with sensors
    public RoomData(string entityID, string name, string floor, List<SensorData> sensors)
    {
        EntityID = entityID;
        Name = name;
        Floor = floor;
        Sensors = sensors ?? new List<SensorData>();
    }

    // Helper method to get a clean room name (removes prefix if needed)
    public string GetCleanRoomName()
    {
        if (string.IsNullOrEmpty(Name))
            return "Unknown Room";

        // Extract just the room name after the underscore (if exists)
        int underscoreIndex = Name.LastIndexOf('_');
        if (underscoreIndex >= 0 && underscoreIndex < Name.Length - 1)
        {
            return Name.Substring(underscoreIndex + 1);
        }

        // Extract after the last hyphen (if exists)
        int hyphenIndex = Name.LastIndexOf('-');
        if (hyphenIndex >= 0 && hyphenIndex < Name.Length - 1)
        {
            return Name.Substring(hyphenIndex + 1).Trim();
        }

        return Name;
    }

    // Helper method to get floor display name
    public string GetFloorDisplayName()
    {
        if (string.IsNullOrEmpty(Floor))
            return "Unknown Floor";

        // Extract the last part after the last slash
        int lastSlashIndex = Floor.LastIndexOf('/');
        if (lastSlashIndex >= 0 && lastSlashIndex < Floor.Length - 1)
        {
            return Floor.Substring(lastSlashIndex + 1);
        }

        return Floor;
    }

    public override string ToString()
    {
        return $"Room: {Name} | Floor: {Floor} | ID: {EntityID}";
    }
}

/// <summary>
/// Wrapper class for deserializing the JSON array
/// Unity's JsonUtility requires a wrapper for arrays
/// </summary>
[System.Serializable]
public class RoomDataList
{
    public List<RoomData> rooms;
}
