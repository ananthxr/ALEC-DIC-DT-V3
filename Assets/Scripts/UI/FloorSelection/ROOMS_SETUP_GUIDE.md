# Rooms Button System - Setup Guide

## Overview
This guide explains how to set up the new Rooms button feature that allows users to access a scrollable room selection interface.

## Architecture

### Components
1. **FloorButtonStacking.cs** - Main controller (enhanced with rooms support)
2. **RoomButton.cs** - Individual room button component
3. **Rooms Button GameObject** - Button that pops out to the left
4. **Rooms Scroll View GameObject** - Scrollable container with room buttons

---

## Unity Setup Instructions

### Step 1: Create the Rooms Button

1. In your Canvas hierarchy, find the `FloorButtonStacking` GameObject
2. Create a new UI Button as a child (name it "RoomsButton")
3. Position it at the same location as the main floor button initially
4. Add appropriate icon/text to indicate "Rooms"

### Step 2: Create the Rooms Scroll View

1. Create a new UI ScrollRect in your Canvas (name it "RoomsScrollView")
2. Structure should be:
   ```
   RoomsScrollView (ScrollRect)
   └── Viewport (Mask, Image)
       └── Content (RectTransform) <- This is where room buttons go
   ```
3. Configure ScrollRect:
   - Set Movement Type: Elastic or Clamped
   - Enable Vertical scrolling
   - Disable Horizontal scrolling
   - Assign Viewport and Content references

### Step 3: Create Room Button Prefab

1. Create a new UI Button (name it "RoomButtonPrefab")
2. Add the `RoomButton.cs` component to it
3. Structure:
   ```
   RoomButtonPrefab (Button, RoomButton)
   ├── Icon (Image) - Optional
   └── RoomName (Text) - Optional
   ```
4. Configure RoomButton component:
   - Set Room Name
   - Set Room ID
   - Assign Button Component reference
   - Assign Button Image reference
   - Assign Room Name Text reference (if using text)
5. Save as prefab in your Prefabs folder

### Step 4: Populate Room Buttons

**Option A: Manual Setup**
1. Drag multiple instances of RoomButtonPrefab into the Content panel
2. Configure each instance with unique room data
3. Use a VerticalLayoutGroup on Content for automatic spacing

**Option B: Dynamic Setup (Recommended for many rooms)**
- Create a separate manager script that instantiates room buttons at runtime
- Example:
```csharp
public class RoomManager : MonoBehaviour
{
    [SerializeField] private GameObject roomButtonPrefab;
    [SerializeField] private Transform contentPanel;
    [SerializeField] private string[] roomNames;

    void Start()
    {
        for (int i = 0; i < roomNames.Length; i++)
        {
            GameObject roomBtn = Instantiate(roomButtonPrefab, contentPanel);
            RoomButton roomButton = roomBtn.GetComponent<RoomButton>();
            roomButton.SetRoomInfo(roomNames[i], i);
        }
    }
}
```

### Step 5: Configure FloorButtonStacking Component

1. Select the FloorButtonStacking GameObject
2. In the Inspector, find the **Rooms Button Configuration** section:
   - **Rooms Button**: Drag the RoomsButton GameObject here
   - **Rooms Button Left Distance**: Set how far left it should slide (e.g., 200)
   - **Rooms Button Animation Duration**: Animation speed (e.g., 0.5)
   - **Rooms Button Animation Ease**: Choose easing curve (e.g., OutCubic)

3. In the **Rooms Scroll View Configuration** section:
   - **Rooms Scroll View**: Drag the RoomsScrollView GameObject here
   - **Rooms Content Panel**: Drag the Content panel here
   - **Scroll View Animation Duration**: Fade in/out speed (e.g., 0.5)
   - **Scroll View Animation Ease**: Choose easing curve

---

## How It Works

### User Flow
1. User clicks **Main Floor Button**
   - Floor buttons stack down vertically
   - **Rooms button slides out to the LEFT**

2. User clicks **Rooms Button**
   - Floor buttons collapse
   - Rooms button collapses
   - Scroll view fades in with room selection

3. User scrolls and clicks a room
   - Room is selected/highlighted
   - Optional: Camera transitions to that room (if TransitionTarget is configured)

4. To exit scroll view:
   - Call `HideRoomsScrollView()` from another button (e.g., close/back button)
   - Returns to floor buttons view

### Animation Details

**Rooms Button Animation:**
- Starts at main button position
- Slides LEFT by `roomsButtonLeftDistance` pixels
- Uses configurable easing and duration

**Scroll View Animation:**
- Fades in using CanvasGroup alpha
- Optional: Can add scale/position animations
- Smooth transition from floor view to room view

---

## Advanced Configuration

### Adding a Close Button to Scroll View

1. Add a UI Button inside RoomsScrollView (name it "CloseButton")
2. Add this script to the button's onClick event:
```csharp
// In Inspector:
// FloorButtonStacking.HideRoomsScrollView()
```

### Integrating with Floor Transitions

If you want clicking a room to transition the camera:
1. In the RoomButton Inspector, find "Optional: Floor Transition Integration"
2. Assign the `FloorTransitionManager` reference (or leave empty to auto-find)
3. Set the `Target Floor Index` to the floor this room belongs to
4. The RoomButton script will automatically call `FloorTransitionManager.SelectFloor(targetFloorIndex)`

**For more advanced room-specific transitions:**
- Later, you can extend RoomButton to store specific camera positions per room
- Create a custom transition system for room-to-room navigation
- Add zoom/focus effects to specific areas within a floor

### Custom Room Data

Extend the RoomButton class to include:
- Room thumbnails
- Room descriptions
- Custom metadata
- Load different scenes or assets per room

---

## Testing Checklist

- [ ] Main floor button expands floor buttons AND rooms button
- [ ] Rooms button slides smoothly to the left
- [ ] Clicking rooms button shows scroll view
- [ ] Scroll view contains all room buttons
- [ ] Room buttons are scrollable
- [ ] Clicking a room button triggers selection
- [ ] Close/back button returns to floor view
- [ ] All animations are smooth with no jerking
- [ ] Colors update correctly on selection

---

## Troubleshooting

**Rooms button doesn't appear:**
- Check that it's assigned in FloorButtonStacking Inspector
- Ensure it's not disabled in the hierarchy
- Check that animation distance is not zero

**Scroll view doesn't show:**
- Verify ScrollRect is properly configured
- Check CanvasGroup is added (auto-added by script)
- Ensure RoomsScrollView reference is assigned

**Room buttons don't respond:**
- Verify RoomButton component is attached
- Check button component reference is assigned
- Ensure buttons are within the Content panel

**Scrolling doesn't work:**
- Check ScrollRect component settings
- Verify Content panel is larger than Viewport
- Add VerticalLayoutGroup to Content for proper sizing

---

## Future Enhancements

Potential features to add:
- Search/filter rooms functionality
- Room categories/groups
- Room favorites system
- Multi-select rooms
- Room preview images
- Tooltips on hover
- Keyboard navigation

---

**Created:** 2025-10-31
**Version:** 1.0
**Compatible with:** FloorButtonStacking v2.0+
