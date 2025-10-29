# Meteor Glow Trail Shader - Development Guide

This document explains how the meteor shader system was created for the ALEC Digital Twin project.

## Overview

The meteor effect uses a **two-shader system** to create a realistic glowing object with a real-time trail that follows movement. This approach separates concerns and provides better performance than trying to do everything in one shader.

## System Architecture

```
Meteor Effect System
├── MeteorCore.shader        → Object surface shader (static glow)
├── MeteorTrail.shader       → Trail renderer shader (dynamic trail)
├── MeteorBlue.mat          → Material for object
├── MeteorTrailMat.mat      → Material for trail
└── MeteorTrailSetup.cs     → C# script to manage trail renderer
```

---

## Part 1: MeteorCore.shader (Object Surface)

**File:** `Assets/shader_claude/MeteorGlowTrail.shader`
**Shader Name:** `Custom/URP/MeteorCore`

### Purpose
Creates the glowing meteor appearance on the object itself (cube, sphere, etc.) with animated effects but NO movement-based trail.

### Key Techniques Used

#### 1. **Fresnel Rim Glow**
```hlsl
half NdotV = saturate(dot(normalWS, viewDirWS));
half fresnel = 1.0 - NdotV;
fresnel = pow(fresnel, _FresnelPower) * _FresnelIntensity;
```

**What it does:**
- Calculates angle between surface normal and view direction
- Objects glow more at edges (like atmosphere on planets)
- Uses power function to control falloff curve

**Why:**
- Makes object feel like it's emitting light
- Creates depth and dimension
- Common technique in sci-fi/energy effects

#### 2. **Procedural 3D Noise (FBM)**
```hlsl
float fbm(float3 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;

    for(int i = 0; i < 3; i++)
    {
        value += amplitude * noise3D(p * frequency);
        frequency *= 2.0;
        amplitude *= 0.5;
    }
    return value;
}
```

**What it does:**
- Generates 3D noise without textures
- Uses Fractional Brownian Motion (FBM) - layers of noise at different scales
- Animated by adding `_Time.y` to world position

**Why:**
- No texture memory needed (WebGL optimization)
- Organic, natural-looking surface variation
- Creates "energy/plasma" look on surface

#### 3. **Animated Pulse Effect**
```hlsl
float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
pulse = lerp(1.0, pulse, _PulseIntensity);
```

**What it does:**
- Sine wave oscillates between 0-1
- `lerp` controls how much pulsing affects final color
- Applied to both color and alpha

**Why:**
- Creates "breathing" energy effect
- Makes shader feel alive and dynamic
- User-controllable intensity

#### 4. **Flicker Effect**
```hlsl
float flickerNoise = noise3D(input.positionWS * 0.5 + _Time.y * _FlickerSpeed);
half flicker = lerp(1.0, flickerNoise, _FlickerIntensity);
```

**What it does:**
- Random noise-based intensity variation
- Different from pulse (organic vs. periodic)
- Multiplied into final color

**Why:**
- Adds realism (like fire or unstable energy)
- Breaks up too-perfect animations
- Subtle detail that enhances quality

#### 5. **Transparency with Additive Fresnel**
```hlsl
half alpha = _BaseTransparency;
alpha += fresnel * 0.6;
alpha *= pulse * flicker;
alpha = saturate(alpha);
```

**What it does:**
- Base transparency level
- Edges become more opaque (fresnel)
- Modulated by animation

**Why:**
- Creates ethereal, energy-like appearance
- Edges stay visible even when transparent
- Dynamic alpha makes it feel alive

### Color Combination Strategy
```hlsl
half3 coreColor = _CoreColor.rgb * (1.0 + surfaceNoise * _NoiseStrength);
half3 glowRim = _GlowColor.rgb * fresnel * _GlowIntensity * pulse * flicker;
half3 finalColor = coreColor + glowRim;
```

**Layered approach:**
1. Dark core with noise variation
2. Bright rim from fresnel
3. All modulated by pulse and flicker
4. **Additive blending** (colors add together for glow)

---

## Part 2: MeteorTrail.shader (Dynamic Trail)

**File:** `Assets/shader_claude/MeteorTrail.shader`
**Shader Name:** `Custom/URP/MeteorTrail`

### Purpose
Renders the glowing trail that follows the object as it moves. Works with Unity's Trail Renderer component.

### Key Techniques Used

#### 1. **UV-Based Trail Fade**
```hlsl
float trailProgress = input.uv.x;
float lengthFade = 1.0 - pow(trailProgress, _FadePower);
```

**What it does:**
- Trail Renderer automatically assigns UVs: `x=0` (newest) to `x=1` (oldest)
- Power curve controls fade shape
- Multiplied into final alpha

**Why:**
- Trails need to fade from bright→transparent
- Power curve gives natural-looking falloff
- No manual distance calculations needed

#### 2. **Edge Glow (Width-Based)**
```hlsl
float widthGradient = input.uv.y;
float edgeFade = 1.0 - abs(widthGradient - 0.5) * 2.0;
edgeFade = pow(edgeFade, _EdgeGlow);
```

**What it does:**
- UV.y goes from 0→1 across trail width
- Center (0.5) is brightest
- Edges (0, 1) are darker
- Power function controls sharpness

**Why:**
- Makes trail look like a glowing beam
- Prevents "ribbon" look
- Creates volumetric appearance

#### 3. **Animated Flow Effect**
```hlsl
float flow = sin(trailProgress * 10.0 - _Time.y * _FlowSpeed) * 0.5 + 0.5;
flow = pow(flow, 2.0) * 0.3 + 0.7;
```

**What it does:**
- Sine wave travels along trail (trailProgress * 10)
- Time offset makes it animate
- Softened to 0.7-1.0 range (subtle)

**Why:**
- Creates "energy flowing" effect
- Adds motion to static trail geometry
- Makes trail feel dynamic

#### 4. **Color Gradient Along Trail**
```hlsl
half3 trailGradient = lerp(_TrailColor.rgb, _TipColor.rgb, trailProgress);
```

**What it does:**
- Interpolates between two colors
- Newest part = TrailColor (bright)
- Oldest part = TipColor (dark/transparent)

**Why:**
- Natural color transition
- Can make trail "cool down" as it fades
- More visually interesting than solid color

#### 5. **Additive Blending**
```hlsl
Blend SrcAlpha One
```

**What it does:**
- Adds trail color ON TOP of background
- Unlike normal alpha blending (replaces)
- Makes overlapping trails brighter

**Why:**
- Creates "glow" effect (light emission)
- Trail appears to emit light
- Common in VFX for energy/magic effects

### Alpha Combination Strategy
```hlsl
half alpha = lengthFade * edgeFade;  // Geometric fades
alpha *= _TrailColor.a;              // Base transparency
alpha *= pulse;                       // Animation modulation
alpha *= _AlphaMultiplier;           // User control
alpha = saturate(alpha);
```

**Multiplicative approach:**
- All fade factors multiply together
- Any factor at 0 = fully transparent
- Allows independent control of each effect

---

## Part 3: MeteorTrailSetup.cs (Trail Manager)

**File:** `Assets/Scripts/MeteorTrailSetup.cs`

### Purpose
Automatically configures Unity's Trail Renderer component with correct settings and assigns the trail material.

### Key Design Decisions

#### 1. **RequireComponent Attribute**
```csharp
[RequireComponent(typeof(TrailRenderer))]
```

**What it does:**
- Automatically adds TrailRenderer when script is added
- Prevents errors from missing component

**Why:**
- User-friendly (one-click setup)
- Enforces dependency at compile time
- Common Unity pattern

#### 2. **Inspector-Driven Configuration**
```csharp
[Range(0.1f, 5f)]
public float trailTime = 1.0f;
```

**What it does:**
- Exposes settings in Unity Inspector
- Range attributes provide sliders
- Tooltips guide users

**Why:**
- Artists/designers can tweak without code
- Prevents invalid values
- Self-documenting

#### 3. **Awake() Setup**
```csharp
void Awake()
{
    SetupTrail();
}
```

**What it does:**
- Runs before Start(), before any gameplay code
- Configures trail renderer programmatically

**Why:**
- Ensures trail is ready immediately
- Centralizes configuration logic
- Can be called from editor (OnValidate)

#### 4. **Runtime Tweaking Support**
```csharp
#if UNITY_EDITOR
void OnValidate()
{
    if (Application.isPlaying)
    {
        SetupTrail();
    }
}
#endif
```

**What it does:**
- When inspector values change, re-apply settings
- Only in editor, only during play mode
- Editor-only code (stripped in build)

**Why:**
- Allows real-time tweaking during testing
- Immediate visual feedback
- Standard Unity workflow

#### 5. **Performance Optimizations**
```csharp
trailRenderer.minVertexDistance = minVertexDistance;
trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
trailRenderer.receiveShadows = false;
```

**What it does:**
- `minVertexDistance`: Reduces vertices when moving slowly
- Disables shadows (trails don't need them)

**Why:**
- Critical for WebGL performance
- Trails can generate many vertices
- Shadows expensive for transparent objects

---

## Technical Decisions & Rationale

### Why Two Shaders Instead of One?

**Option A: Single Shader with Trail**
- ❌ Can't create real-time trail (needs mesh generation)
- ❌ Would need vertex displacement (expensive, looks bad)
- ❌ Static effect only

**Option B: Two-Shader System** ✅
- ✅ Core shader focuses on surface quality
- ✅ Trail shader optimized for Trail Renderer UVs
- ✅ Real movement-based trail
- ✅ Better performance (separate rendering)

### Why Procedural Noise Instead of Textures?

**Textures:**
- ❌ Memory overhead (important for WebGL)
- ❌ UV mapping required
- ❌ Fixed resolution
- ❌ Tiling artifacts

**Procedural Noise:**
- ✅ Zero memory (calculated per-pixel)
- ✅ Infinite resolution
- ✅ True 3D (no UV stretching)
- ✅ Animates smoothly in all directions

### Why Additive Blending for Trail?

**Normal Alpha Blending:**
- Shows "ribbon" behind object
- Overlaps create dark bands
- Doesn't look like light emission

**Additive Blending:**
- Appears to emit light
- Overlaps get brighter (natural for energy)
- Common in VFX (fire, magic, lasers)
- WebGL-friendly (fast)

### Why Fresnel for Glow?

**Uniform Glow:**
- Looks flat
- No depth perception
- "Painted on" appearance

**Fresnel Glow:**
- Physically-based (light scatters at grazing angles)
- Creates rim lighting
- Works from all angles
- Instant "sci-fi energy" look
- Used in: Iron Man arc reactor, force fields, energy shields

---

## WebGL Optimization Strategies

### Shader Level
1. **Target 3.0** - WebGL 2.0 compatible
2. **No texture sampling** - Reduces bandwidth
3. **Simple math** - Dot products, lerps, pow (GPU-friendly)
4. **3-octave FBM** - Balance of quality vs. performance
5. **No branching** - All `if` statements avoided

### Trail Renderer Level
1. **minVertexDistance = 0.1** - Reduces vertex count
2. **No shadows** - Expensive for transparent objects
3. **View alignment** - Billboard mode (fewer triangles)
4. **Additive blend** - Cheaper than complex alpha

### Material Level
1. **Shared materials** - Reduces draw calls
2. **Single-pass** - No multi-pass rendering
3. **ZWrite Off** - Standard for transparent objects

---

## URP-Specific Implementation

### Required Includes
```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
```

### URP Helper Functions Used
```hlsl
VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
float3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
Light mainLight = GetMainLight();
```

**Why use helpers:**
- Handles SRP batcher compatibility
- Correct transformations for URP
- Forward/deferred rendering support
- Multi-camera support

### URP Tags
```hlsl
Tags
{
    "RenderType" = "Transparent"
    "Queue" = "Transparent"
    "RenderPipeline" = "UniversalPipeline"
}
```

**Critical for:**
- Proper render order
- URP material inspector
- Shader variant compilation

---

## Common Shader Patterns Explained

### 1. Remap Range Pattern
```hlsl
// Remap from [0, 1] to [-1, 1]
value = value * 2.0 - 1.0;

// Remap from [-1, 1] to [0, 1]
value = value * 0.5 + 0.5;
```
Used for noise, sine waves, any value that needs different range.

### 2. Power Curve Pattern
```hlsl
value = pow(value, exponent);
```
- `exponent > 1` = sharper falloff (steeper curve)
- `exponent < 1` = gentler falloff (flatter curve)
- Used for fresnel, fades, any non-linear response

### 3. Saturate Pattern
```hlsl
value = saturate(value);  // Clamps to [0, 1]
```
Prevents out-of-range values that cause artifacts.

### 4. Lerp/Mix Pattern
```hlsl
result = lerp(a, b, t);  // HLSL
result = mix(a, b, t);   // GLSL
```
Linear interpolation: `t=0` returns `a`, `t=1` returns `b`.

### 5. Time Animation Pattern
```hlsl
float animated = sin(_Time.y * speed + offset);
```
`_Time.y` = seconds since startup, perfect for continuous animation.

---

## Debugging Tips

### Visualizing Intermediate Values
```hlsl
// To see a value, output it as grayscale
return half4(value, value, value, 1);

// To see a vector, use as RGB
return half4(normalWS, 1);

// To see UVs
return half4(input.uv.x, input.uv.y, 0, 1);
```

### Common Issues

**Black Screen:**
- Check alpha isn't 0
- Verify material assigned
- Check shader compiles (Console)

**Too Bright/Blown Out:**
- Reduce `_GlowIntensity`
- Check additive blending (might be too much)
- Use HDR camera for bloom

**No Animation:**
- Verify `_Time.y` is used
- Check speed values aren't 0
- Play mode required (not edit mode)

**Trail Not Showing:**
- TrailRenderer component exists?
- Material assigned in script?
- Object actually moving?
- Check `minVertexDistance` (might be too high)

---

## Extension Ideas

### Easy Additions
1. **Color variations** - Duplicate materials with different colors
2. **Speed-based intensity** - Trail brighter when moving faster
3. **Particle sparks** - Add particle system at trail end
4. **Impact flash** - Detect collisions, flash brighter

### Advanced Additions
1. **Heat distortion** - GrabPass for refraction
2. **Texture overlay** - Add streak texture to trail
3. **Multi-colored trail** - Gradient texture sampling
4. **Mesh deformation** - Vertex displacement on core object
5. **Shadow blob** - Project shadow beneath (fake AO)

---

## Performance Benchmarks (Expected)

### Desktop (GTX 1060+)
- Core shader: ~0.5ms per object
- Trail shader: ~0.2ms per trail
- 50+ meteors at 60fps

### WebGL (Modern Browser)
- Core shader: ~1-2ms per object
- Trail shader: ~0.5ms per trail
- 20-30 meteors at 60fps

### Optimization Switches
If performance is low, reduce:
1. FBM octaves (3→2 in shader code)
2. Trail time (1.0→0.5 seconds)
3. Noise scale (fewer calculations)

---

## References & Learning Resources

### Techniques Used
- **Fresnel Effect**: https://en.wikipedia.org/wiki/Fresnel_equations
- **Perlin Noise**: https://en.wikipedia.org/wiki/Perlin_noise
- **FBM (Fractional Brownian Motion)**: https://iquilezles.org/articles/fbm/
- **Trail Renderer**: https://docs.unity3d.com/Manual/class-TrailRenderer.html

### Unity URP Shader Documentation
- URP Shader Structure: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest
- ShaderLab Syntax: https://docs.unity3d.com/Manual/SL-Reference.html
- HLSL in Unity: https://docs.unity3d.com/Manual/SL-ShaderPrograms.html

### Similar Effects in Games
- **Rocket League** - Boost trails (similar technique)
- **Resogun** - Enemy trails (additive blending)
- **Tron** - Light cycles (ribbon trails)

---

## File Metadata

**Created:** 2025-10-07
**Unity Version:** Unity 6 (6000.0.x)
**Render Pipeline:** Universal Render Pipeline (URP)
**Target Platform:** WebGL (optimized)
**Shader Model:** 3.0 (WebGL 2.0)
**Author:** Claude (Anthropic)
**Project:** ALEC Digital Twin VR Application

---

## Quick Reference: Property Cheat Sheet

### MeteorCore Material
| Property | Range | Purpose | Recommended |
|----------|-------|---------|-------------|
| Glow Intensity | 0-10 | Overall brightness | 3-5 |
| Fresnel Power | 0.5-8 | Edge sharpness | 2-3 |
| Pulse Speed | 0-10 | Breathing rate | 1-3 |
| Flicker Speed | 0-20 | Random variation | 5-10 |
| Noise Scale | 0-20 | Surface detail size | 2-5 |
| Base Transparency | 0-1 | Core opacity | 0.2-0.4 |

### MeteorTrail Material
| Property | Range | Purpose | Recommended |
|----------|-------|---------|-------------|
| Glow Intensity | 0-10 | Trail brightness | 2-4 |
| Edge Glow | 0-5 | Width falloff | 1-2 |
| Flow Speed | 0-10 | Animation speed | 1-3 |
| Fade Power | 0.1-5 | Length falloff | 1-2 |

### MeteorTrailSetup Script
| Property | Range | Purpose | Recommended |
|----------|-------|---------|-------------|
| Trail Time | 0.1-5s | How long trail lasts | 0.5-1.5s |
| Start Width | 0.01-2 | Trail origin size | 0.2-0.5 |
| End Width | 0.01-2 | Trail end size | 0.02-0.1 |
| Min Vertex Distance | 0-0.5 | Performance control | 0.05-0.15 |

---

**End of Documentation**
