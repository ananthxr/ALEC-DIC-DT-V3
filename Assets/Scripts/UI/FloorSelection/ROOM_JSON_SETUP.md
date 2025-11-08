# Room JSON Population System - Setup Guide

## Overview
This system loads room data from `RoomData.json` and populates the rooms scroll view with interactive room buttons, similar to how `SlidingPanelController` handles alarm data.

---

## Components Created

### 1. **RoomData.cs** (`Assets/Scripts/Data/`)
- Data class matching the JSON structure
- Fields: `EntityID`, `Name`, `Floor`
- Helper methods for clean display names

### 2. **RoomDataItem.cs** (`Assets/Scripts/UI/FloorSelection/`)
- Component attached to each room button prefab
- Displays room name
- Handles click events
- Visual feedback (selected/hover states)

### 3. **RoomScrollViewController.cs** (`Assets/Scripts/UI/FloorSelection/`)
- Loads JSON data on Start
- Populates scroll view with room buttons
- Smooth batched instantiation (avoids frame drops)
- Supports floor filtering

### 4. **FloorButtonStacking.cs** (Enhanced)
- Now calls `RoomScrollViewController.PopulateRoomList()` when showing scroll view
- Maintains closed-loop state management

---

## Unity Setup Instructions

### Step 1: Prepare the RoomDataPrefab

1. Select `Assets/Prefab/RoomDataPrefab.prefab`
2. Add the `RoomDataItem.cs` component to it
3. In the Inspector:
   - **Room Name Text**: Drag the "Room Name" TextMeshProUGUI child object
   - **Floor Text**: Leave EMPTY (will auto-format into Room Name Text) OR add a second TextMeshProUGUI for separate display
   - **Button Component**: Auto-finds Button component (or assign manually)
   - **Button Image**: Auto-finds Image component (or assign manually)
4. Configure colors:
   - Normal Color: White
   - Selected Color: Light Blue (0.5, 0.8, 1.0)
   - Hover Color: Light Gray (0.9, 0.9, 0.9)
5. **IMPORTANT:** Increase prefab Height to ~170-200 to fit both Name and Floor
6. Save the prefab

**See `PREFAB_DUAL_TEXT_SETUP.md` for detailed text layout options.**

### Step 2: Setup RoomScrollViewController

1. Find your Rooms Scroll View GameObject in the hierarchy
2. Add the `RoomScrollViewController.cs` component to it
3. In the Inspector, configure:

```
[UI References]
• Scroll View Content → The "Content" RectTransform inside the ScrollRect
• Scroll Rect → The ScrollRect component
• Scroll View Root → The root GameObject of the scroll view

[Prefab Settings]
• Room Item Prefab → Drag RoomDataPrefab here

[JSON Data]
• JSON File Name → "RoomData.json"
• JSON Folder Path → "Assets/Sensor Excels"

[Performance Settings]
• Batch Size → 5 (items per frame)
• Delay Between Batches → 0.02 seconds

[Filtering]
• Floor Filter → Leave empty to show all rooms
```

### Step 3: Connect to FloorButtonStacking

1. Select the GameObject with `FloorButtonStacking.cs`
2. In the Inspector, find **Rooms Scroll View Configuration**
3. Assign:
   - **Rooms Scroll View** → The scroll view GameObject
   - **Rooms Content Panel** → The Content panel inside scroll view
   - **Room Scroll View Controller** → The RoomScrollViewController component
   - **Animation Duration** → 0.5
   - **Animation Ease** → OutCubic

### Step 4: Verify JSON File Location

Ensure your JSON file is at:
```
Y:\ALEC DIC DT V3\Assets\Sensor Excels\RoomData.json
```

The script will automatically load this on Start.

---

## How It Works

### Data Flow

```
1. Start() → RoomScrollViewController loads RoomData.json
   ↓
2. JSON deserialized into List<RoomData>
   ↓
3. User clicks Rooms Button
   ↓
4. FloorButtonStacking.ShowRoomsScrollView() called
   ↓
5. roomScrollViewController.PopulateRoomList() called
   ↓
6. Batched instantiation of RoomDataPrefab (5 per frame)
   ↓
7. Each prefab gets RoomDataItem.SetRoomData(roomData)
   ↓
8. Room name displayed, button ready for click
```

### JSON Structure Expected

```json
[
  {
    "Entity ID": "fff89aa0-daf5-11ef-94c5-01236d0e69c4",
    "Name": "DIC Mezzanine Block 2_Z1-Corridor",
    "Floor": "DIC/Main/Mezzanine Floor"
  },
  {
    "Entity ID": "ffdf1f30-daf5-11ef-94c5-01236d0e69c4",
    "Name": "DIC Mezzanine Block 2_Z1-Seating Area",
    "Floor": "DIC/Main/Mezzanine Floor"
  }
]
```

**Note:** The JSON has `"Entity ID"` with a space, but C# field is `EntityID` (JsonUtility handles this automatically).

---

## Features

### ✓ Smooth Batched Loading
- Instantiates 5 room buttons per frame (configurable)
- Prevents frame drops/stuttering
- Similar to SlidingPanelController's smooth population

### ✓ Floor Filtering
```csharp
// Show only Ground Floor rooms
roomScrollViewController.SetFloorFilter("Ground Floor");

// Show all rooms
roomScrollViewController.ClearFloorFilter();
```

### ✓ Dynamic Sorting
- Rooms automatically sorted by name (A-Z)

### ✓ Room Stats Logging
On load, prints to console:
```
[RoomScrollViewController] ✓ Loaded 120 rooms from JSON
[RoomScrollViewController] Found 3 unique floors:
  - DIC/Main/Ground Floor: 45 rooms
  - DIC/Main/Mezzanine Floor: 60 rooms
  - DIC/Whitespace/GroundFloor: 15 rooms
```

---

## Customization

### Display Format Options

The `RoomDataItem` component automatically displays both Name and Floor:

**Default (Single Text Field - Auto-formatted):**
```
DIC Mezzanine Block 2_Z1-Corridor
DIC/Main/Mezzanine Floor
```
- Floor text is smaller (size 18) and gray (#888888)
- No setup needed - works automatically if Floor Text is left empty

**Separate Text Fields (More Control):**
- Add a second TextMeshProUGUI to the prefab
- Assign it to the Floor Text field in RoomDataItem
- Each text can be styled independently

**See `PREFAB_DUAL_TEXT_SETUP.md` for detailed layout options.**

### Adjust Performance Settings

For **more rooms**, **slower devices**:
```
Batch Size: 3
Delay Between Batches: 0.05
```

For **fewer rooms**, **fast devices**:
```
Batch Size: 10
Delay Between Batches: 0.01
```

### Add Floor Transition on Click

In `RoomDataItem.OnRoomButtonClicked()`, uncomment:
```csharp
if (floorTransitionManager != null)
{
    int floorIndex = GetFloorIndexFromFloorString(roomData.Floor);
    if (floorIndex >= 0)
    {
        floorTransitionManager.SelectFloor(floorIndex);
    }
}
```

Then implement `GetFloorIndexFromFloorString()` to map floor strings to indices.

---

## Hierarchy Structure

```
Canvas
├── FloorButtonStack [FloorButtonStacking.cs]
│   └── (existing floor buttons)
│
├── RoomsButton
│
└── RoomsScrollView [RoomScrollViewController.cs]
    ├── CloseButton
    └── Viewport (Mask)
        └── Content (RectTransform, VerticalLayoutGroup)
            ├── RoomDataPrefab (Clone) [RoomDataItem.cs]
            │   └── Room Name (TextMeshProUGUI)
            ├── RoomDataPrefab (Clone) [RoomDataItem.cs]
            ├── RoomDataPrefab (Clone) [RoomDataItem.cs]
            └── ... (populated at runtime)
```

---

## Testing Checklist

- [ ] RoomDataPrefab has RoomDataItem component
- [ ] RoomScrollViewController assigned in FloorButtonStacking
- [ ] JSON file loads successfully (check console logs)
- [ ] Click Rooms Button → Scroll view appears
- [ ] Room buttons populate smoothly (no stuttering)
- [ ] Room names display correctly
- [ ] Scroll view scrolls properly
- [ ] Click room button → Visual feedback works
- [ ] Click Main Button → Scroll view closes (closed-loop)
- [ ] Check console for stats: "Loaded X rooms from JSON"

---

## Debug Console Output Example

```
[RoomScrollViewController] Loading room data from: Y:\...\RoomData.json
[RoomScrollViewController] ✓ Loaded 120 rooms from JSON
[RoomScrollViewController] Found 3 unique floors:
  - DIC/Main/Ground Floor: 45 rooms
  - DIC/Main/Mezzanine Floor: 60 rooms
  - DIC/Whitespace/GroundFloor: 15 rooms
[RoomScrollViewController] No filter applied - showing all 120 rooms
[FloorButtonStacking] Rooms button clicked!
[FloorButtonStacking] Showing rooms scroll view
[FloorButtonStacking] Populating room list from JSON
[RoomScrollViewController] Starting to populate scroll view with 120 rooms
[RoomScrollViewController] ✓ Cleared all room items
[RoomDataItem] Set room data: DIC Mezzanine Block 2_Z1-Corridor
[RoomDataItem] Set room data: DIC Mezzanine Block 2_Z1-Seating Area
...
[RoomScrollViewController] ✓ Populated 120 room items smoothly
```

---

## Troubleshooting

**Problem: No rooms appear**
- Check console for JSON loading errors
- Verify JSON file path is correct
- Ensure RoomScrollViewController is assigned in FloorButtonStacking
- Check that RoomDataPrefab has RoomDataItem component

**Problem: Rooms appear but no names**
- Verify Room Name Text is assigned in RoomDataItem
- Check TextMeshProUGUI component exists on prefab

**Problem: Frame drops/stuttering when populating**
- Reduce Batch Size (try 3 instead of 5)
- Increase Delay Between Batches (try 0.05)

**Problem: Scroll view doesn't scroll**
- Verify Content panel has VerticalLayoutGroup
- Ensure Content panel height > Viewport height
- Check ScrollRect settings (Movement Type: Elastic, Vertical: ON)

---

## Future Enhancements

**Floor-based filtering:**
- Add dropdown to filter by specific floor
- Auto-filter based on currently selected floor

**Search functionality:**
- Add search bar to filter rooms by name
- Real-time filtering as user types

**Room categories:**
- Group rooms by type (Office, Toilet, Meeting Room, etc.)
- Collapsible category headers

**Room details panel:**
- Click room → Show detailed info panel
- Display Entity ID, sensors, alarms, etc.

---

**Created:** 2025-10-31
**Version:** 1.0
**Compatible with:** FloorButtonStacking v2.0+, Unity 2019.4+
