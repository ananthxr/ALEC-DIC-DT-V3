# Ghost Wall Shader - Implementation Guide

## Overview

The **GhostWall.shader** creates semi-transparent building walls that maintain visual definition through edge lighting and proper lighting response. This prevents the "monotonic" flat appearance while keeping walls ghosted.

---

## Key Features

### 1. **Edge Definition (Fresnel Effect)**
- Walls have **bright edges** that make them visible even when transparent
- Prevents the "invisible wall" problem
- Creates architectural depth and definition

### 2. **Lighting Response**
- Walls **respond to scene lighting** (directional light, shadows)
- Creates natural depth variation across surfaces
- Blue tint can come from lighting (explained below)

### 3. **Depth Fade**
- Walls fade when **very close to camera** to prevent visual clutter
- Helps with navigation in VR

### 4. **Subtle Surface Detail**
- Procedural noise adds slight variation (prevents perfectly flat look)
- Very subtle - just enough to break up uniformity

---

## Setup Instructions

### Step 1: Create the Material

1. In Unity Project window, navigate to `Assets/shader_claude/`
2. Right-click → Create → Material
3. Name it `WallGhost_Material`
4. In Inspector, change Shader to: `Custom/URP/GhostWall`

### Step 2: Configure Material Settings

**Recommended Starting Values:**

| Property | Value | Purpose |
|----------|-------|---------|
| **Base Color** | Light gray (0.7, 0.7, 0.7, 1) | Wall base tint |
| **Transparency** | 0.25 - 0.4 | How see-through walls are |
| **Edge Sharpness** | 2.5 | How defined edges are |
| **Edge Brightness** | 1.5 - 2.5 | How bright edges glow |
| **Edge Tint** | Light blue (0.5, 0.7, 1.0, 1) | Edge color |
| **Light Response** | 0.3 - 0.5 | How much lighting affects walls |
| **Shadow Depth** | 0.3 - 0.5 | How dark shadowed areas get |
| **Detail Scale** | 2 - 3 | Surface noise size |
| **Detail Strength** | 0.05 - 0.1 | Surface noise intensity |

### Step 3: Apply to Wall Objects

1. Select your building wall GameObjects in Hierarchy
2. Drag `WallGhost_Material` onto them in Inspector
3. Or: In Mesh Renderer component → Materials → Element 0 → Select material

---

## Achieving the Blue Lighting Effect (Image #2)

The blue tint in your reference image comes from **lighting**, not just the material. Here's how to recreate it:

### Method 1: Blue-Tinted Directional Light (Recommended)

1. **Select Main Directional Light** in your scene
2. **Change Light Color** to light blue:
   - RGB: (0.7, 0.85, 1.0) - Cool blue-white
   - Or: (0.5, 0.7, 1.0) - More saturated blue
3. **Adjust Intensity**: 0.8 - 1.2
4. **In Material Settings**:
   - Set `Light Color Influence` to **0.6 - 0.8** (walls pick up light color)
   - Keep `Base Color` neutral gray

**Why this works:**
- Walls inherit the blue tone from scene lighting
- Creates unified look across entire building
- Natural lighting variation creates depth

### Method 2: Blue Base Color

1. **In Material**:
   - Set `Base Color` to blue-gray: (0.5, 0.6, 0.8, 1)
   - Set `Edge Tint` to brighter blue: (0.6, 0.8, 1.0, 1)
2. **Keep Scene Light** white or slightly warm
3. **Adjust `Light Color Influence`** to 0.3 - 0.5

**Why this works:**
- Walls always blue regardless of lighting
- More control over exact color
- Good for consistent branding

### Method 3: Combination Approach (Best Results)

1. **Material Settings**:
   - `Base Color`: Light blue-gray (0.65, 0.7, 0.85, 1)
   - `Edge Tint`: Bright blue (0.5, 0.8, 1.0, 1)
2. **Scene Light**:
   - Color: Cool white (0.85, 0.9, 1.0)
   - Intensity: 1.0
3. **Material Response**:
   - `Light Color Influence`: 0.5
   - `Light Response`: 0.4

**Result:**
- Natural blue tint that responds to lighting
- Edges glow with blue highlight
- Shadows create depth definition

---

## Preventing Monotonic Appearance

### Problem: Flat, Featureless Walls

Your concern about walls looking "monotonic" (no edges visible) is addressed by:

#### 1. **Fresnel Edge Glow**
```hlsl
half fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
```
- **Surfaces glow at edges** where they turn away from camera
- Creates **visual separation** between walls
- Works from any viewing angle

#### 2. **Lighting Response**
```hlsl
half NdotL = dot(normalWS, mainLight.direction);
half3 diffuse = NdotL * mainLight.color * _DiffuseStrength;
```
- Walls **facing light are brighter**
- Walls **perpendicular to light are darker**
- Creates natural depth variation

#### 3. **Shadow Reception**
```hlsl
half shadow = mainLight.shadowAttenuation;
```
- Walls **receive shadows** from other geometry
- Adds depth and spatial understanding
- Critical for architectural visualization

#### 4. **Alpha Modulation**
```hlsl
alpha += fresnel * 0.3;
```
- Edges become **slightly more opaque**
- Ensures edges are always visible
- Prevents "dissolving" appearance

### Tuning for Maximum Definition

**If walls still look too flat:**

1. **Increase Edge Brightness**: 2.5 → 3.5
2. **Increase Light Response**: 0.4 → 0.6
3. **Increase Shadow Depth**: 0.3 → 0.5
4. **Decrease Edge Sharpness**: 2.5 → 1.5 (softer, more visible glow)

**If edges are too harsh:**

1. **Decrease Edge Brightness**: 1.5 → 1.0
2. **Increase Edge Sharpness**: 2.5 → 4.0 (tighter falloff)
3. **Reduce Edge Tint alpha**: Keep RGB, but mentally "tone it down"

---

## Technical Explanation: Why This Looks Good

### Fresnel Effect (Rim Lighting)

Real-world materials become more reflective at grazing angles. This shader simulates that:

- **Facing camera**: Base color shows, slightly transparent
- **Edge-on to camera**: Bright glow, more opaque
- **Result**: Natural architectural definition

### Lighting Integration

Unlike simple transparency, this shader **responds to URP lighting**:

- Uses `GetMainLight()` for directional light
- Supports shadow reception via `TransformWorldToShadowCoord()`
- Calculates proper **Lambert diffuse** (NdotL)

**What this means:**
- Walls integrate with scene lighting naturally
- Shadows from other objects fall on walls
- Different wall faces have different brightness

### Depth Fade

```hlsl
half depthFade = saturate(input.depth / _DepthFadeDistance);
alpha *= depthFade;
```

**Purpose:**
- Walls **very close to camera fade out**
- Prevents VR discomfort (walls in face)
- Improves spatial navigation

---

## VR-Specific Optimizations

This shader is optimized for VR (Meta Quest):

1. **Single Pass**: No multi-pass rendering
2. **Shader Model 3.0**: WebGL 2.0 / Mobile compatible
3. **No Textures**: Zero texture bandwidth
4. **Efficient Math**: Only necessary calculations
5. **Shadow Caster Pass**: Included but can be disabled if not needed

### Performance Settings

**For lower-end VR devices:**

1. **Disable shadow casting**:
   - Comment out the "ShadowCaster" pass in shader
2. **Reduce Detail**: Set `Detail Strength` to 0
3. **Simplify lighting**: Set `Light Response` to 0.2

**For high-end devices:**

1. **Enable all features** (default)
2. **Add subtle animation** (optional, see below)

---

## Advanced Customization

### Adding Subtle Pulse Effect

To add a gentle "breathing" effect to walls:

**Add to Properties:**
```hlsl
[Header(Animation)]
_PulseSpeed ("Pulse Speed", Range(0, 2)) = 0.5
_PulseIntensity ("Pulse Intensity", Range(0, 0.2)) = 0.05
```

**Add to CBUFFER:**
```hlsl
half _PulseSpeed;
half _PulseIntensity;
```

**In fragment shader, before final color:**
```hlsl
half pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
pulse = lerp(1.0, pulse, _PulseIntensity);
finalColor *= pulse;
```

### Per-Floor Color Coding

Create **separate materials** for each floor with different colors:

- **Ground Floor**: Cyan (0.5, 0.8, 1.0)
- **First Floor**: Blue (0.5, 0.6, 1.0)
- **Roof**: Purple (0.7, 0.5, 1.0)

Helps users understand which floor they're viewing.

---

## Comparison: Your Reference Images

### Image #1 Analysis (Blurry but Good Walls)

**What makes it work:**
- ✅ Clear edge definition (fresnel/lighting)
- ✅ Blue color scheme
- ✅ Transparency allows seeing through
- ✅ Lighting creates depth

**This shader provides:**
- ✅ Fresnel edge glow (same effect)
- ✅ Configurable blue tint
- ✅ Adjustable transparency
- ✅ Full URP lighting support

### Image #2 Analysis (Good Blue Lighting)

**What makes it work:**
- ✅ Strong blue color palette
- ✅ Lighting differentiates surfaces
- ✅ Clear spatial depth
- ✅ Professional architectural feel

**How to achieve this:**
1. Use **Method 3 (Combination Approach)** above
2. Set Directional Light to blue-white
3. Increase `Light Response` to 0.5
4. Set `Edge Tint` to bright blue

---

## Troubleshooting

### Problem: Walls completely invisible

**Solution:**
- Increase `Transparency` value (higher = more opaque)
- Increase `Edge Brightness`
- Check material is assigned to objects

### Problem: Walls too opaque

**Solution:**
- Decrease `Transparency` value
- Reduce `Edge Brightness`
- Reduce fresnel contribution to alpha

### Problem: No blue tint

**Solution:**
- Change Directional Light color to blue
- Set material `Base Color` to blue-gray
- Increase `Light Color Influence`

### Problem: Edges too harsh/glowing

**Solution:**
- Decrease `Edge Brightness`
- Increase `Edge Sharpness` (tighter falloff)
- Reduce `Edge Tint` color intensity

### Problem: Walls look flat (monotonic)

**Solution:**
- Increase `Light Response` (0.4 → 0.6)
- Increase `Shadow Depth` (0.3 → 0.5)
- Increase `Edge Brightness` (1.5 → 2.5)
- Check scene has proper directional light

### Problem: Performance issues in VR

**Solution:**
- Set `Detail Strength` to 0 (disables noise)
- Reduce number of walls (LOD system)
- Remove ShadowCaster pass from shader
- Batch walls using same material

---

## Quick Reference: Preset Configurations

### Preset 1: "Subtle Ghost" (Minimal Distraction)
```
Transparency: 0.2
Edge Brightness: 1.2
Edge Sharpness: 3.0
Light Response: 0.3
Base Color: (0.8, 0.8, 0.8, 1)
Edge Tint: (0.9, 0.9, 1.0, 1)
```

### Preset 2: "Defined Architecture" (Your Use Case)
```
Transparency: 0.35
Edge Brightness: 2.0
Edge Sharpness: 2.5
Light Response: 0.45
Shadow Depth: 0.4
Base Color: (0.65, 0.7, 0.85, 1) - Blue-gray
Edge Tint: (0.5, 0.8, 1.0, 1) - Bright blue
```

### Preset 3: "Bold Wireframe" (Maximum Visibility)
```
Transparency: 0.5
Edge Brightness: 3.5
Edge Sharpness: 1.8
Light Response: 0.5
Base Color: (0.5, 0.6, 0.9, 1)
Edge Tint: (0.4, 0.7, 1.0, 1)
```

---

## Integration with Your Project

### Working with Floor Transition System

When floors transition (from your `FloorTransitionManager.cs`):

**Option A: Fade walls during transition**
- Add script to control material `Transparency` parameter
- Animate from current → 0.1 (very transparent) during lift
- Restore to 0.35 when floor settles

**Option B: Hide non-active floor walls completely**
- Disable MeshRenderer on non-selected floor walls
- Only show walls for current floor + adjacent floors
- Better performance, cleaner view

### Working with Alarm System

Walls near alarm locations could **pulse** or **change color**:

```csharp
// In AlarmManager, when alarm is active:
Material wallMaterial = alarmLocation.GetComponent<Renderer>().material;
wallMaterial.SetColor("_EdgeColor", Color.red);
wallMaterial.SetFloat("_EdgeBrightness", 3.5f);
```

### Working with Navigation System

Make walls **more transparent** along navigation path:

```csharp
// When showing navigation path:
foreach (var wall in wallsAlongPath)
{
    wall.GetComponent<Renderer>().material.SetFloat("_Transparency", 0.15f);
}
```

---

## File Metadata

**Created:** 2025-10-09
**Shader:** GhostWall.shader
**Render Pipeline:** Universal Render Pipeline (URP)
**Unity Version:** Unity 6 (6000.0.x)
**Target Platform:** VR (Meta Quest) + WebGL
**Shader Model:** 3.0

---

**End of Guide**
