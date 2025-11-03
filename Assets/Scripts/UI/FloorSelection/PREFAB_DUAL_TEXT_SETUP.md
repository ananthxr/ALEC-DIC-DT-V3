# RoomDataPrefab - Dual Text Setup Guide

## Overview
Display both **Room Name** and **Floor** in the room button prefab.

You have **2 options** for the prefab structure:

---

## Option 1: Single Text (Simpler) ⭐ RECOMMENDED

### Prefab Structure
```
RoomDataPrefab (Button, Image, RoomDataItem)
└── Room Name (TextMeshProUGUI)
```

### Setup Steps
1. Keep existing prefab structure (single text)
2. In `RoomDataItem` component:
   - **Room Name Text**: Assign the TextMeshProUGUI component
   - **Floor Text**: Leave EMPTY

### What It Displays
```
DIC Mezzanine Block 2_Z1-Corridor
DIC/Main/Mezzanine Floor
```

The room name appears on the first line, floor on the second line (smaller, gray text).

### Prefab Settings
- **Room Name** TextMeshProUGUI settings:
  - Font Size: 20-24
  - Alignment: Left or Center
  - Word Wrapping: ON
  - Overflow: Overflow (or Ellipsis)
  - Vertical Alignment: Top or Middle

**That's it!** The script automatically formats it as:
```csharp
roomNameText.text = $"{Name}\n<size=18><color=#888888>{Floor}</color></size>";
```

---

## Option 2: Separate Text Fields (More Control)

### Prefab Structure
```
RoomDataPrefab (Button, Image, RoomDataItem)
├── Room Name (TextMeshProUGUI)
└── Floor Name (TextMeshProUGUI)
```

### Setup Steps

1. **Duplicate the "Room Name" text:**
   - Right-click "Room Name" in hierarchy
   - Duplicate
   - Rename duplicate to "Floor Name"

2. **Position the texts:**
   - **Room Name**:
     - Anchor: Top-Left
     - Position: X=10, Y=-10 (from top)
     - Size: Width=460, Height=80
   - **Floor Name**:
     - Anchor: Top-Left
     - Position: X=10, Y=-100 (below Room Name)
     - Size: Width=460, Height=60

3. **Configure text styles:**
   - **Room Name**:
     - Font Size: 24
     - Color: Black or Dark Gray
     - Alignment: Left
   - **Floor Name**:
     - Font Size: 18
     - Color: Gray (RGB: 136, 136, 136)
     - Alignment: Left

4. **In RoomDataItem component:**
   - **Room Name Text**: Assign "Room Name" TextMeshProUGUI
   - **Floor Text**: Assign "Floor Name" TextMeshProUGUI

### What It Displays
```
DIC Mezzanine Block 2_Z1-Corridor

DIC/Main/Mezzanine Floor
```

Each text is completely separate with independent styling.

---

## Visual Comparison

### Option 1 (Single Text - Auto-formatted)
```
┌─────────────────────────────────────────┐
│ DIC Mezzanine Block 2_Z1-Corridor       │
│ DIC/Main/Mezzanine Floor                │  ← Smaller, gray
└─────────────────────────────────────────┘
```

### Option 2 (Separate Texts)
```
┌─────────────────────────────────────────┐
│ DIC Mezzanine Block 2_Z1-Corridor       │  ← TextMeshProUGUI 1
│                                          │
│ DIC/Main/Mezzanine Floor                │  ← TextMeshProUGUI 2
└─────────────────────────────────────────┘
```

---

## Recommended Prefab Settings

### Button (RoomDataPrefab)
- **RectTransform**:
  - Width: 480
  - Height: 170-200 (depending on text size)
- **Image**:
  - Color: White (1, 1, 1, 1)
  - Sprite: UI/Skin/UISprite (or custom)

### Layout
- Add **VerticalLayoutGroup** to the Content panel:
  - Spacing: 10-15
  - Child Force Expand: Width ON, Height OFF
  - Child Control Size: Width ON, Height ON

---

## Testing Checklist

After setup:
- [ ] Prefab has RoomDataItem component
- [ ] Room Name Text is assigned
- [ ] (Optional) Floor Text is assigned
- [ ] Play mode → Click Rooms Button
- [ ] Room names appear correctly
- [ ] Floor names appear correctly
- [ ] Text is readable (not cut off)
- [ ] Scroll view scrolls smoothly
- [ ] Both fields display different data per room

---

## Example Data Display

Your JSON:
```json
{
  "Entity ID": "fff89aa0-daf5-11ef-94c5-01236d0e69c4",
  "Name": "DIC Mezzanine Block 2_Z1-Corridor",
  "Floor": "DIC/Main/Mezzanine Floor"
}
```

Will display as:

**Option 1 (Single Text):**
```
DIC Mezzanine Block 2_Z1-Corridor
DIC/Main/Mezzanine Floor
```

**Option 2 (Separate Texts):**
```
DIC Mezzanine Block 2_Z1-Corridor

DIC/Main/Mezzanine Floor
```

---

## Quick Setup (Copy-Paste Values)

### Option 1 - Single Text Component

**Room Name Text:**
- Font Size: `24`
- Line Spacing: `0`
- Alignment: `Left` or `Center`
- Overflow: `Overflow`
- Auto Size: OFF
- Word Wrapping: ON
- RectTransform: `Width=460, Height=140`

### Option 2 - Dual Text Components

**Room Name Text:**
- Font Size: `24`
- Color: `#323232` (dark gray)
- Alignment: `Left`
- RectTransform: `Width=460, Height=80, Y=-10`

**Floor Name Text:**
- Font Size: `18`
- Color: `#888888` (gray)
- Alignment: `Left`
- RectTransform: `Width=460, Height=60, Y=-100`

---

## Troubleshooting

**Problem: Text is cut off**
- Increase prefab Height to 200
- Enable Word Wrapping on text
- Use Overflow mode instead of Ellipsis

**Problem: Floor text not appearing**
- Verify Floor Text is assigned in Inspector
- Check that Floor data exists in JSON (not null)

**Problem: Text overlaps**
- Increase spacing in VerticalLayoutGroup
- Increase prefab Height

**Problem: Text too small**
- Increase Font Size (Room: 24, Floor: 18-20)

---

## My Recommendation

**Use Option 1 (Single Text)** because:
✓ Simpler setup
✓ Less hierarchy complexity
✓ Automatic formatting
✓ Easier to maintain
✓ Works immediately

Only use Option 2 if you need:
- Very specific positioning control
- Different animations per text
- Complex styling requirements

---

**Created:** 2025-10-31
**Version:** 1.0
