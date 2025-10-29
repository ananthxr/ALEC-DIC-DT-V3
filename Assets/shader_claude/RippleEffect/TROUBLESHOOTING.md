# Ripple Shader Troubleshooting Guide

## Problem: "I don't see any changes - looks the same as before"

Let's diagnose this step by step!

---

## Step 1: Verify Material Setup

### Check 1.1: Material Exists and Uses Correct Shader

1. **In Unity Project window**, navigate to your materials folder
2. **Find or create** a material called `RippleRingMaterial` (or any name)
3. **Select the material**
4. **In Inspector**, look at the top where it says "Shader"
5. **It should say:** `Custom/URP/GlowingRippleRing`

**If it doesn't:**
- Click on the shader dropdown
- Type "Glowing" in search
- Select `Custom/URP/GlowingRippleRing`

---

### Check 1.2: Material Properties Are Set

With the material selected, verify these values in Inspector:

```
Ring Colors:
├── Ring Color: NOT black! Try RGB(0, 255, 255) Alpha: 255
└── Glow Color (HDR): RGB(0, 1000, 1500)  ← See HDR picker!

Glow Settings:
├── Glow Intensity: 10 (NOT 0!)
└── Glow Spread: 0.4
```

**CRITICAL CHECK:**
- Click on **Glow Color**
- You should see "**HDR**" checkbox at bottom of color picker
- Values should be able to go **above 1.0** (like 3.0, 10.0, etc.)
- If you only see 0-1 range, the shader isn't loaded correctly!

---

## Step 2: Verify RippleEffect Script Assignment

### Check 2.1: Material Is Assigned

1. **Select your icon GameObject** (the one with RippleEffect script)
2. **In Inspector**, find the RippleEffect component
3. **Look at `Glow Material` field**
4. **It should reference** your `RippleRingMaterial`

**If it's empty or wrong:**
- Drag your material from Project window into this field

---

### Check 2.2: Ring Prefab Is Assigned

Still in RippleEffect Inspector:
- **`Ring Prefab` field should NOT be empty**
- It should reference a GameObject with an **Image** or **SpriteRenderer** component

**If empty:**
- You need to create a ring prefab (see "Creating Ring Prefab" below)

---

## Step 3: Test with Debug Shader

Let's use a special debug shader to see what's actually rendering:

### Test 3.1: Switch to Debug Shader

1. **Select your material**
2. **Change shader to:** `Custom/URP/GlowingRippleRing_Debug`
3. **Play the scene**
4. **Hover over your icon**

### Test 3.2: Debug Views

In the material Inspector, you'll see **"Debug View"** dropdown:

**Try each mode:**

| Mode | What You Should See | If You See This Instead |
|------|---------------------|------------------------|
| **Normal** | Glowing cyan rings | Nothing = material not assigned |
| **UVs** | Red-green gradient | Black = UVs broken |
| **Distance** | White center, black edges | All same color = distance broken |
| **Ring Mask** | White ring on black | All black = ring not rendering |
| **Glow** | Glowing ring | All black = glow intensity 0 |
| **Alpha** | White ring fading | All black = alpha is 0 |

**Take a screenshot** of what you see in each mode and let me know!

---

## Step 4: Check Camera & Post-Processing

### Check 4.1: Camera Has HDR Enabled

1. **Select your Main Camera**
2. **In Inspector**, find the Camera component
3. **Look for "Allow HDR"** checkbox
4. **It MUST be checked (✓)**

**If unchecked:**
- Check it!
- This is REQUIRED for bloom to work

---

### Check 4.2: Bloom Is Configured

1. **Check if Camera has "Volume" component**
   - If NO: Add Component > Rendering > Volume
   - Set `Profile` to create new profile

2. **Click "Add Override"** on Volume
3. **Select:** Post-processing > Bloom

4. **Configure Bloom:**
   ```
   Intensity: 0.4
   Threshold: 0.9
   Scatter: 0.7
   ```

5. **Check the box** next to each property to enable it

**If you don't see Volume component option:**
- Your project might not have URP properly installed
- Check Edit > Project Settings > Graphics
- Ensure "Scriptable Render Pipeline Settings" is assigned

---

## Step 5: Verify Ring Prefab Setup

### Check 5.1: Ring Prefab Structure

Your ring prefab should have:
- ✓ GameObject
- ✓ RectTransform (for UI) OR Transform (for world space)
- ✓ Image component (for UI) OR SpriteRenderer (for world space)
- ✓ Material field on Image/SpriteRenderer (leave as "None" - script assigns it)

**DO NOT:**
- ❌ Assign material directly to prefab (script does this)
- ❌ Have CanvasGroup blocking raycasts
- ❌ Have zero scale

---

### Check 5.2: Ring Prefab Size

If using UI (Canvas):
- RectTransform Width = Height = 200-500
- Anchors = centered
- Position = (0, 0, 0)

If using world space:
- Scale = (1, 1, 1) initially
- Has sprite assigned to SpriteRenderer

---

## Step 6: Common Issues & Fixes

### Issue: "Rings spawn but are invisible"

**Possible causes:**
1. **Alpha is 0** - Check RippleEffect script: `startAlpha` should be 0.8-1.0
2. **Color is black** - Check: `ringColor` in script (NOT black!)
3. **Scale is 0** - Check: `startScale` should be 0.5-1.0
4. **Shader not assigned** - Material's shader is wrong

**Fix:**
```
In RippleEffect Inspector:
- Ring Color: RGB(0, 200, 255)
- Start Alpha: 0.8
- Start Scale: 0.8
- End Scale: 1.8
```

---

### Issue: "Rings are there but not glowing"

**Possible causes:**
1. **Bloom not enabled** - Add Volume > Bloom (see Step 4.2)
2. **HDR not enabled** - Camera: Allow HDR ✓ (see Step 4.1)
3. **Glow Color not HDR** - Values must be > 1.0 (like 3-10)
4. **Glow Intensity too low** - Should be 8-15

**Fix:**
```
In Material:
- Glow Color (HDR): Click color, ensure HDR mode, use RGB(0, 3, 4)
- Glow Intensity: 10
- Glow Spread: 0.4

In Camera:
- Allow HDR: ✓

In Volume:
- Bloom Intensity: 0.4
- Bloom Threshold: 0.9
```

---

### Issue: "Shader dropdown doesn't show 'Custom/URP/GlowingRippleRing'"

**Possible causes:**
1. **Shader file not imported** - Check Assets/shader_claude/RippleEffect/
2. **Shader has compile errors** - Check Console (Ctrl+Shift+C)
3. **Unity needs refresh** - Right-click shader > Reimport

**Fix:**
1. Open Console (Window > General > Console)
2. Look for red shader errors
3. If present, send me the error message
4. If no errors, try: Assets > Refresh (or Ctrl+R)

---

### Issue: "Rings are squares, not circles"

**Possible causes:**
1. **RectTransform not square** - Width ≠ Height
2. **Parent canvas scaler stretching** - Canvas issues

**Fix:**
```
Ring Prefab RectTransform:
- Width: 400
- Height: 400  ← Must match width!
- Preserve Aspect: ✓ (on Image component)
```

---

## Step 7: Create Test Material from Scratch

Let's make sure everything is set up correctly:

### 7.1 Create New Material

1. **Right-click in Project** > Create > Material
2. **Name:** `TestRippleGlow`
3. **Select it**

### 7.2 Assign Shader

1. **Click shader dropdown** (top of Inspector)
2. **Type:** "glowing"
3. **Select:** Custom/URP/GlowingRippleRing
   - If you DON'T see it, shader isn't compiling!

### 7.3 Set Basic Properties

```
Ring Colors:
- Ring Color: Cyan (0, 1, 1, 1)
- Glow Color: Click it, enable HDR, set to (0, 3, 3, 1)

Ring Shape:
- Inner Radius: 0.35
- Outer Radius: 0.45
- Softness: 0.08
- Ring Thickness Boost: 1.5

Glow Settings:
- Glow Intensity: 10
- Glow Spread: 0.4
- Inner Glow: 0.5
- Outer Glow: 0.8
```

### 7.4 Test on Simple Quad

1. **Create:** GameObject > UI > Image
2. **Rename:** TestRing
3. **In Inspector:**
   - Width: 400
   - Height: 400
   - Material: TestRippleGlow

**You should see:** A glowing cyan ring!

**If you see:**
- Nothing = Material not assigned
- White square = Shader not working
- Solid color, no ring = Ring Shape values wrong

---

## Step 8: Console Errors

Open Console (Ctrl+Shift+C) and check for:

**Shader compilation errors:**
```
Shader error in 'Custom/URP/GlowingRippleRing'...
```
→ Send me the full error message!

**Material property errors:**
```
Material doesn't have property '_GlowColor'...
```
→ Wrong shader assigned to material

**Script errors:**
```
NullReferenceException: Object reference not set...
```
→ RippleEffect missing references

---

## Quick Diagnostic Checklist

Run through this checklist and tell me which ones FAIL:

- [ ] **Shader appears in shader dropdown**
- [ ] **Material uses GlowingRippleRing shader**
- [ ] **Glow Color shows HDR picker (values > 1)**
- [ ] **Glow Intensity is NOT 0**
- [ ] **Material assigned to RippleEffect.glowMaterial**
- [ ] **Ring prefab assigned to RippleEffect.ringPrefab**
- [ ] **Camera has "Allow HDR" checked**
- [ ] **Volume component exists on camera**
- [ ] **Bloom override added to Volume**
- [ ] **Console has no red errors**
- [ ] **Rings spawn when hovering icon** (even if not glowing)
- [ ] **Debug shader shows something in "Ring Mask" mode**

---

## Still Not Working? Send Me This Info:

1. **Screenshot of:**
   - Your material Inspector (showing all properties)
   - RippleEffect Inspector (showing all fields)
   - Camera Inspector (showing Volume component)

2. **Console errors** (screenshot or copy-paste)

3. **Debug shader results:**
   - What do you see in "Ring Mask" mode?
   - What do you see in "Glow" mode?

4. **Unity version** and **URP version**
   - Edit > Project Settings > Player > Version

---

## Nuclear Option: Start Fresh

If nothing works, let's create everything from scratch:

### Fresh Setup Steps:

1. **Delete old material**
2. **Create:** Right-click > Create > Material > Name: `FreshRippleGlow`
3. **Set shader:** Custom/URP/GlowingRippleRing_Debug
4. **Set Debug View:** Ring Mask
5. **Create test UI Image** (see Step 7.4)
6. **Assign material to Image**
7. **Look at it** - Do you see a white ring on black background?

**If YES:** Shader works! Issue is with RippleEffect integration
**If NO:** Shader not working - send me Console errors

---

**Tell me what you find and I'll help you fix it! 🔧**
