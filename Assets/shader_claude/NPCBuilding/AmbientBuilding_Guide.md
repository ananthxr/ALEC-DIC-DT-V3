# Ambient Building Shader - Background Buildings Guide

## Overview

The **AmbientBuilding.shader** is designed for background buildings that provide city ambience. These buildings intentionally fade into the background to **enhance the main building's visibility** and reduce visual clutter.

---

## Key Design Philosophy

### Primary Goal: Make Main Building Stand Out

Background buildings use several techniques to "go low":

1. **Desaturation** - Remove color intensity (gray/muted tones)
2. **Low Brightness** - Darker than main building
3. **High Transparency** - More see-through
4. **Distance Fade** - Fade as camera moves away
5. **Minimal Lighting** - Less response to scene lights
6. **Atmospheric Fog** - Blend into environment

---

## Key Features

### 1. **Desaturation System**
Converts colors to grayscale while preserving some original hue:
- `Desaturation = 0` → Full original color
- `Desaturation = 1` → Complete grayscale
- **Recommended: 0.6 - 0.8** for background buildings

**Why:** Desaturated objects naturally recede visually, making colorful objects (main building) pop.

### 2. **Distance-Based Fading**
Buildings fade based on distance from camera:
- `Distance Fade Start` → Where fade begins (e.g., 50m)
- `Distance Fade End` → Where fully transparent (e.g., 100m)

**Why:** Reduces visual clutter at distance, focuses attention on nearby main building.

### 3. **Low Brightness Control**
Independent brightness multiplier:
- `Brightness = 0.3 - 0.5` → Very dark, subtle
- Main building would use 0.8 - 1.0

**Why:** Darker objects recede, brighter objects advance (depth perception).

### 4. **Minimal Edge Definition**
Subtle fresnel (much weaker than GhostWall):
- `Edge Brightness = 0.2 - 0.4` (vs. 1.5-2.5 for main building)
- Just enough to see building silhouette

**Why:** Edges define structure without drawing attention.

### 5. **Fog Integration**
Blends buildings with atmospheric fog:
- `Fog Density` → How much buildings fade into fog
- `Fog Tint` → Atmospheric color (usually blue-gray)

**Why:** Creates depth, unifies background buildings into cohesive environment.

---

## Setup Instructions

### Step 1: Create Material

1. Right-click in `Assets/shader_claude/` → Create → Material
2. Name: `AmbientBuilding_Material`
3. Shader: `Custom/URP/AmbientBuilding`

### Step 2: Configure for Background Buildings

**Recommended Preset: "Recessive Background"**

| Property | Value | Purpose |
|----------|-------|---------|
| **Base Color** | Dark gray (0.15, 0.18, 0.22, 1) | Muted base |
| **Desaturation** | 0.7 | Remove most color |
| **Brightness** | 0.35 - 0.5 | Keep dark |
| **Overall Transparency** | 0.4 - 0.6 | More transparent than main building |
| **Distance Fade Start** | 50 | Start fading at 50 units |
| **Distance Fade End** | 120 | Fully faded at 120 units |
| **Light Response** | 0.15 | Minimal lighting |
| **Shadow Darkness** | 0.4 | Subtle shadows |
| **Edge Brightness** | 0.25 | Subtle edges |
| **Fog Density** | 0.3 - 0.5 | Atmospheric blending |

### Step 3: Apply to Background Buildings

1. Select all background building GameObjects
2. Apply `AmbientBuilding_Material` to their MeshRenderers
3. Adjust distance fade based on scene scale

---

## Visual Hierarchy: Main Building vs. Background

### Main Building (GhostWall Shader)
- ✅ **Brighter** (0.7 - 0.8 brightness equivalent)
- ✅ **More Saturated** (blue tint, colored edges)
- ✅ **Stronger Edge Glow** (fresnel 1.5 - 2.5)
- ✅ **Higher Lighting Response** (0.4 - 0.5)
- ✅ **Less Transparent** (0.25 - 0.35 alpha)

### Background Buildings (AmbientBuilding Shader)
- ✅ **Darker** (0.3 - 0.5 brightness)
- ✅ **Desaturated** (0.6 - 0.8 grayscale)
- ✅ **Weaker Edge Glow** (fresnel 0.2 - 0.4)
- ✅ **Lower Lighting Response** (0.15 - 0.25)
- ✅ **More Transparent** (0.4 - 0.6 alpha)
- ✅ **Distance Fade** (unique to background)

**Result:** Main building **visually dominates**, background provides context without distraction.

---

## Distance Fade System Explained

### How It Works

```hlsl
half distanceFade = 1.0 - saturate((distance - _DistanceFadeStart) / (_DistanceFadeEnd - _DistanceFadeStart));
alpha *= distanceFade;
```

**Behavior:**
- **Distance < Start**: Full alpha (fully visible)
- **Distance between Start and End**: Linear fade
- **Distance > End**: Alpha = 0 (invisible)

### Configuration Examples

**Tight City Block (Small Scene):**
```
Distance Fade Start: 30
Distance Fade End: 60
```
→ Buildings fade quickly, tight focus

**Large Urban Area (Big Scene):**
```
Distance Fade Start: 80
Distance Fade End: 150
```
→ Buildings visible further out

**No Distance Fade (Always Visible):**
```
Distance Fade Start: 1000
Distance Fade End: 2000
```
→ Effectively disabled for huge distances

---

## Color Strategy for Background Buildings

### Monochromatic Gray Scheme (Recommended)

**Best for maximum main building emphasis:**

```
Base Color: (0.15, 0.18, 0.22, 1) - Cool dark gray
Desaturation: 0.75
Brightness: 0.4
Fog Tint: (0.5, 0.55, 0.6, 1) - Blue-gray fog
```

**Effect:** Completely neutral, main building's color pops.

### Warm vs. Cool Separation

**If main building is COOL (blue):**
```
Background Base Color: (0.22, 0.18, 0.15, 1) - Warm gray
Desaturation: 0.7
Fog Tint: (0.6, 0.55, 0.5, 1) - Warm fog
```
→ Color contrast separates buildings

**If main building is WARM (orange/red):**
```
Background Base Color: (0.15, 0.18, 0.22, 1) - Cool gray
Desaturation: 0.7
Fog Tint: (0.5, 0.55, 0.6, 1) - Cool fog
```
→ Temperature contrast

### Slight Blue Tint (Common in Architecture Visualization)

```
Base Color: (0.12, 0.15, 0.2, 1)
Desaturation: 0.6
Brightness: 0.45
Fog Tint: (0.4, 0.5, 0.65, 1)
```

**Effect:** Atmospheric, professional, recessive.

---

## Lighting Configuration

### Scene Light Settings for Background Buildings

**Directional Light (Sun/Key Light):**
- Intensity: 0.8 - 1.0
- Color: Slightly warm or cool (not pure white)
- Shadows: Enabled (adds depth)

**Ambient Light (Environment Lighting):**
- Source: Skybox or Gradient
- Intensity: 0.3 - 0.5
- Color: Blue-gray for outdoor, warm for indoor

**Material Response:**
- `Light Response = 0.15 - 0.25` (minimal)
- `Ambient Influence = 0.4 - 0.6` (moderate)
- `Shadow Darkness = 0.3 - 0.5` (subtle contrast)

**Why Low Response:**
Strong lighting makes objects visually prominent. Background buildings should be **underlit** to recede.

---

## Integration with Main Building

### Rendering Order

Shader uses `"Queue" = "Transparent-1"`:
- Background buildings render **before** main building
- Ensures proper depth sorting
- Main building alpha blends **over** background

### Shared Lighting Environment

Both shaders respond to same scene lights:
- **Consistency:** Unified lighting direction
- **Differentiation:** Different response strengths
- **Depth:** Shadows create spatial relationships

### Camera Positioning Strategy

**For Maximum Effect:**
1. Position camera to view **main building prominently**
2. Background buildings fill periphery
3. Distance fade removes distant clutter
4. Fog unifies background into horizon

---

## Performance Optimizations

### Why This Shader is Fast

1. **No Textures** - Zero texture bandwidth
2. **Minimal Lighting** - Simple Lambert diffuse only
3. **No Complex Noise** - Unlike GhostWall (has noise)
4. **Single Pass** - One draw call per material batch
5. **Early Fragment Culling** - Alpha test opportunities

### LOD Strategy for Background Buildings

**Level 0 (Close): 0-40m**
- Full geometry
- AmbientBuilding shader

**Level 1 (Medium): 40-80m**
- Simplified geometry (50% vertices)
- Same shader

**Level 2 (Far): 80-120m**
- Very simple geometry (box/low-poly)
- Shader starts distance fade

**Level 3 (Very Far): >120m**
- Faded to invisible, can disable renderer

### Batching for Performance

**Create Material Variants:**
- `AmbientBuilding_Gray` - Neutral buildings
- `AmbientBuilding_Warm` - Warm-tinted buildings
- `AmbientBuilding_Cool` - Cool-tinted buildings

**Apply Same Material to Multiple Buildings:**
- Unity batches meshes with identical materials
- Reduces draw calls dramatically
- Critical for WebGL/VR performance

---

## Advanced Techniques

### Height-Based Fog Gradient

**Add to Properties:**
```hlsl
_HeightFogStart ("Height Fog Start", Float) = 0
_HeightFogEnd ("Height Fog End", Float) = 50
```

**In Fragment Shader:**
```hlsl
half heightFog = saturate((input.positionWS.y - _HeightFogStart) / (_HeightFogEnd - _HeightFogStart));
finalColor = lerp(_FogTint.rgb, finalColor, heightFog);
```

**Effect:** Buildings fade more at ground level, enhancing depth.

### Per-Building Variation

**Use Vertex Colors for Variation:**
- Paint vertex colors in modeling software
- Sample in shader: `input.color.r` for variation

**Or use World Position:**
```hlsl
half variation = frac(input.positionWS.x * 0.1 + input.positionWS.z * 0.1);
_Brightness *= lerp(0.8, 1.2, variation);
```

**Effect:** Prevents identical-looking buildings.

### Time-of-Day Adaptation

**Script to Control Based on Time:**
```csharp
// Day: Lighter, more visible
ambientMaterial.SetFloat("_Brightness", 0.5f);
ambientMaterial.SetFloat("_Desaturation", 0.6f);

// Night: Darker, more faded
ambientMaterial.SetFloat("_Brightness", 0.2f);
ambientMaterial.SetFloat("_Desaturation", 0.8f);
```

---

## Comparison: GhostWall vs. AmbientBuilding

| Feature | GhostWall (Main) | AmbientBuilding (Background) |
|---------|------------------|------------------------------|
| **Purpose** | Architectural detail | Environmental context |
| **Brightness** | 0.7 - 0.8 | 0.3 - 0.5 |
| **Color** | Saturated (blue) | Desaturated (gray) |
| **Edge Glow** | Strong (1.5-2.5) | Weak (0.2-0.4) |
| **Transparency** | 0.25 - 0.35 | 0.4 - 0.6 |
| **Lighting Response** | Moderate (0.4) | Low (0.15) |
| **Distance Fade** | Optional (toggle) | Always active |
| **Fog** | No | Yes |
| **Shadows** | Receives + Casts | Receives only (no cast) |
| **Noise Detail** | Yes (surface detail) | No (smooth) |
| **Render Queue** | Transparent | Transparent-1 |

---

## Troubleshooting

### Problem: Background buildings still too visible

**Solutions:**
1. Increase `Desaturation` (0.7 → 0.85)
2. Decrease `Brightness` (0.4 → 0.3)
3. Increase `Overall Transparency` (0.5 → 0.7)
4. Decrease `Distance Fade Start` (bring fade closer)
5. Increase `Fog Density` (0.3 → 0.5)

### Problem: Background buildings completely invisible

**Solutions:**
1. Decrease `Overall Transparency` (0.6 → 0.4)
2. Increase `Brightness` (0.3 → 0.5)
3. Increase `Edge Brightness` (0.2 → 0.4)
4. Decrease `Desaturation` (0.7 → 0.5)
5. Check `Distance Fade End` isn't too close

### Problem: Main building doesn't stand out enough

**Solutions:**
1. **Main building:** Increase brightness, saturation, edge glow
2. **Background buildings:** Decrease all visibility parameters
3. **Lighting:** Increase light on main building (spot light)
4. **Camera:** Position to favor main building prominence

### Problem: Distance fade too harsh

**Solutions:**
1. Increase gap between `Fade Start` and `Fade End`
2. Example: Start=50, End=80 → Start=50, End=120
3. Wider range = smoother fade

### Problem: Buildings look flat/boring

**Solutions:**
1. Increase `Shadow Darkness` (0.3 → 0.5)
2. Increase `Light Response` slightly (0.15 → 0.25)
3. Ensure scene has proper directional light with shadows
4. Add subtle `Edge Brightness` (0.2 → 0.35)

---

## Preset Configurations

### Preset 1: "Maximum Recession" (Very Subtle)
```
Base Color: (0.12, 0.14, 0.16, 1)
Desaturation: 0.85
Brightness: 0.3
Overall Transparency: 0.65
Distance Fade: 30 → 80
Light Response: 0.1
Edge Brightness: 0.15
Fog Density: 0.5
```
→ Barely visible, maximum main building emphasis

### Preset 2: "Balanced Context" (Recommended)
```
Base Color: (0.15, 0.18, 0.22, 1)
Desaturation: 0.7
Brightness: 0.4
Overall Transparency: 0.5
Distance Fade: 50 → 120
Light Response: 0.2
Edge Brightness: 0.25
Fog Density: 0.35
```
→ Visible context, clear hierarchy

### Preset 3: "Prominent Background" (Detailed City)
```
Base Color: (0.2, 0.22, 0.25, 1)
Desaturation: 0.55
Brightness: 0.5
Overall Transparency: 0.4
Distance Fade: 70 → 150
Light Response: 0.25
Edge Brightness: 0.35
Fog Density: 0.25
```
→ Clear buildings, still secondary to main building

---

## Usage with Your VR Project

### Floor Transition Integration

When floor changes in `FloorTransitionManager`:
- Background buildings remain static (provide stable reference)
- Main building floors animate
- Background provides spatial grounding

### Navigation System Integration

**Highlight Path to Main Building:**
- Background buildings stay faded
- Main building gets brighter material temporarily
- Navigation path stands out against muted background

### Alarm Visualization

**When alarm triggers:**
```csharp
// Main building alarm location: BRIGHT RED
mainBuildingMaterial.SetColor("_EdgeColor", Color.red);

// Background buildings: Even more faded
ambientMaterial.SetFloat("_GlobalAlpha", 0.3f);
```
→ Focuses attention on alarm

---

## File Metadata

**Created:** 2025-10-09
**Shader:** AmbientBuilding.shader
**Purpose:** Background city buildings (non-interactive)
**Render Pipeline:** Universal Render Pipeline (URP)
**Target Platform:** VR (Meta Quest) + WebGL
**Shader Model:** 3.0
**Optimized For:** High building count, low draw calls

---

## Quick Start Summary

1. **Create Material** with `Custom/URP/AmbientBuilding` shader
2. **Use "Balanced Context" preset** from above
3. **Apply to all background buildings** in scene
4. **Adjust distance fade** to match scene scale
5. **Compare visually** with main building (should be clearly dominant)
6. **Tweak brightness/desaturation** until hierarchy feels right

**Main building should be 2-3x more visually prominent than background buildings.**

---

**End of Guide**
