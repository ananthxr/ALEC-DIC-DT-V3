# Fresnel Glow Effect Shader

A customizable unlit shader that creates a holographic/sci-fi glow effect using Fresnel lighting, pulsing animation, and wireframe grid overlay.

## 📁 Files Included

- **FresnelGlowEffect.shader** - Main shader file (works in URP and Built-in Pipeline)
- **FresnelGlowShader.shadergraph** - Shader Graph version (requires Shader Graph package)
- **FresnelGlowMaterial.mat** - Pre-configured material
- **GridTextureGenerator.cs** - Utility script to generate grid textures
- **README.md** - This documentation

---

## 🎯 Features

### 1. **Fresnel Effect**
- Creates edge-based glow that intensifies when viewing the object at glancing angles
- Controlled by the `Fresnel Power` parameter

### 2. **Pulsing Animation**
- Animated glow that pulses in a sine wave pattern
- Speed controlled by `Pulse Speed` parameter
- Creates a "breathing" holographic effect

### 3. **Grid Wireframe Overlay**
- Customizable grid texture for wireframe appearance
- Tiling controlled by `Grid Tiling` parameter
- Works with procedurally generated or custom textures

### 4. **Transparency**
- Surface type set to Transparent
- Adjustable alpha for see-through effects
- Proper blending with background

### 5. **Customizable Emission**
- HDR emission color support
- Intensity multiplier for brightness control
- Combines with Fresnel for edge-focused glow

---

## 🔧 Shader Properties

### Base Properties
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| **Base Color** | Color | - | (0.2, 0.5, 1.0) | Main color of the object |
| **Alpha** | Float | 0-1 | 0.3 | Transparency level |

### Emission Properties
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| **Emission Color** | Color (HDR) | - | Cyan (0, 1, 1) | Color of the glow effect |
| **Emission Intensity** | Float | 0-10 | 2.0 | Brightness multiplier for emission |

### Fresnel Properties
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| **Fresnel Power** | Float | 0.1-10 | 3.0 | Controls edge falloff (higher = sharper edges) |

### Grid/Wireframe Properties
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| **Grid Texture** | Texture2D | - | White | Texture used for wireframe pattern |
| **Grid Tiling** | Float | 1-50 | 10.0 | How many times to tile the grid texture |

### Animation Properties
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| **Pulse Speed** | Float | 0-10 | 2.0 | Speed of the pulsing animation |

---

## 📋 Setup Instructions

### Quick Start

1. **Import the shader files** into your Unity project at `Assets/shader_claude/FresnelGlow/`

2. **Generate a Grid Texture** (choose one method):

   **Method A: Using the Generator Script**
   - Create an empty GameObject in your scene
   - Attach the `GridTextureGenerator.cs` script
   - Adjust parameters in the Inspector:
     - Texture Size: 512 (recommended)
     - Grid Divisions: 10
     - Line Thickness: 2-4 pixels
   - Click "Generate Grid Texture" button
   - The texture will be saved to `Assets/shader_claude/FresnelGlow/GridTexture.png`

   **Method B: Use Your Own Texture**
   - Create or import a grid/wireframe texture
   - Set the texture's Wrap Mode to "Repeat"
   - Set Filter Mode to "Bilinear"

3. **Assign the Material**
   - Select or create a 3D object in your scene
   - Drag the `FresnelGlowMaterial` onto the object
   - OR create a new material and select the `Custom/FresnelGlowEffect` shader

4. **Assign the Grid Texture**
   - In the Material Inspector, find the "Grid Texture" slot
   - Drag your generated grid texture into this slot

5. **Customize the Effect**
   - Adjust the shader properties to your liking
   - Try different colors and intensities

---

## 🎨 Recommended Settings

### Holographic Effect
```
Base Color: Light Blue (0.2, 0.5, 1.0)
Alpha: 0.3
Emission Color: Cyan (0, 1, 1)
Emission Intensity: 2.5
Fresnel Power: 4.0
Grid Tiling: 15
Pulse Speed: 1.5
```

### Energy Shield
```
Base Color: Transparent Blue (0.1, 0.3, 0.8)
Alpha: 0.2
Emission Color: Electric Blue (0.3, 0.7, 1.0)
Emission Intensity: 3.0
Fresnel Power: 5.0
Grid Tiling: 20
Pulse Speed: 3.0
```

### Wireframe Ghost
```
Base Color: Dark (0.1, 0.1, 0.1)
Alpha: 0.4
Emission Color: Green (0, 1, 0.2)
Emission Intensity: 2.0
Fresnel Power: 3.0
Grid Tiling: 25
Pulse Speed: 1.0
```

### Sci-Fi Portal
```
Base Color: Purple (0.5, 0, 1)
Alpha: 0.5
Emission Color: Magenta (1, 0, 1)
Emission Intensity: 4.0
Fresnel Power: 2.5
Grid Tiling: 12
Pulse Speed: 4.0
```

---

## 🧮 Technical Details

### Shader Formula

The shader implements the following calculation:

```
FinalOutput = (BaseColor * Alpha) + (Emission * Fresnel * Pulse * Grid)
```

Where:
- **Fresnel** = `pow(1 - dot(normal, viewDir), FresnelPower)`
- **Pulse** = `sin(Time * PulseSpeed) * 0.5 + 1.0` (oscillates 0.5 to 1.5)
- **Grid** = Red channel of the sampled grid texture
- **Emission** = `EmissionColor * EmissionIntensity`

### Render Pipeline Compatibility

The shader includes two SubShaders:

1. **Universal Render Pipeline (URP)** - Primary implementation
   - Uses HLSL and URP shader library
   - Optimized for modern Unity versions
   - File: `FresnelGlowEffect.shader:1-179`

2. **Built-in Render Pipeline** - Fallback
   - Uses CG and UnityCG.cginc
   - Compatible with older Unity projects
   - File: `FresnelGlowEffect.shader:182-279`

### Performance Characteristics

- **Vertex Shader**: Lightweight - transforms positions, normals, UVs
- **Fragment Shader**: Moderate - calculates Fresnel, samples 1 texture
- **Transparency**: Uses standard alpha blending (SrcAlpha, OneMinusSrcAlpha)
- **Recommended Use**: Best for hero objects, UI elements, special effects
- **Mobile Friendly**: Yes, with lower resolution grid textures

---

## 🛠️ Customization Tips

### Adjusting Edge Glow Intensity

The edge glow is controlled by three parameters working together:

1. **Fresnel Power** (3.0 default)
   - Lower values (1-2): Wide, soft glow
   - Medium values (3-5): Balanced edge glow
   - Higher values (6-10): Sharp, thin edge highlights

2. **Emission Intensity** (2.0 default)
   - Directly multiplies brightness
   - Use higher values (3-5) for bright, visible effects
   - Use lower values (0.5-1.5) for subtle highlights

3. **Emission Color**
   - Use HDR colors (values > 1) for bloom effects
   - Saturated colors appear more vibrant
   - Match with post-processing bloom for best results

### Creating Different Grid Patterns

Modify `GridTextureGenerator.cs` to create variations:

```csharp
// Diagonal lines
bool isOnDiagonalLine = ((x + y) % (int)spacing) < lineThickness;

// Hexagonal grid
// (requires more complex math - see Voronoi/cellular algorithms)

// Random dots
bool isOnDot = (Random.value < 0.1f) && ((x % 10 == 0) && (y % 10 == 0));
```

### Adding Color Variation

To add color variation over time, modify the fragment shader:

```hlsl
// Add this in the fragment shader, after the pulse calculation
half3 colorShift = half3(
    sin(_Time.y * 0.5) * 0.5 + 0.5,
    sin(_Time.y * 0.7 + 2.0) * 0.5 + 0.5,
    sin(_Time.y * 0.9 + 4.0) * 0.5 + 0.5
);
half3 emission = _EmissionColor.rgb * colorShift * fresnel * _EmissionIntensity * pulseValue * gridMask;
```

---

## 🐛 Troubleshooting

### Issue: No glow visible
**Solutions:**
- Increase Emission Intensity to 3-5
- Check that Emission Color is not black
- Verify Alpha is not 0
- Ensure camera is viewing object at an angle (Fresnel requires edge view)

### Issue: No wireframe pattern
**Solutions:**
- Assign a grid texture to the Grid Texture slot
- Verify grid texture has contrast (white lines on black background)
- Increase Grid Tiling value
- Check texture import settings (Wrap Mode = Repeat)

### Issue: Object is invisible
**Solutions:**
- Check Alpha value (should be 0.1-0.9 for visibility)
- Verify Render Queue is set to "Transparent"
- Ensure Base Color is not completely black
- Check camera culling mask

### Issue: Shader not working in URP
**Solutions:**
- Verify URP is installed (Package Manager)
- Check that your project uses a URP Renderer
- The shader includes URP support - ensure Surface Type is "Transparent" in material

### Issue: Pulsing too fast/slow
**Solutions:**
- Adjust Pulse Speed parameter (try 0.5-2.0 for slower, 3.0-8.0 for faster)
- For custom timing, modify the sine wave in the shader:
  ```hlsl
  half pulseValue = sin(_Time.y * _PulseSpeed) * 0.5 + 1.0;
  ```

---

## 📚 Use Cases

### Game Development
- Holographic UI elements
- Force fields and energy shields
- Sci-fi object highlights
- Ghost/spirit effects
- Scanning/analysis overlays
- Portal effects

### Visualization
- Architectural wireframes
- Data visualization overlays
- Medical imaging displays
- Technical schematics

### VR/AR
- Interactive object highlights
- Spatial UI elements
- Boundary indicators

---

## 🔄 Version History

### Version 1.0
- Initial release
- Fresnel-based edge glow
- Pulsing animation
- Grid texture overlay
- URP and Built-in Pipeline support
- Procedural grid texture generator

---

## 📖 Further Learning

### Understanding Fresnel Effect
The Fresnel effect is based on the Fresnel equations in physics, which describe how light reflects at different angles. In computer graphics, we simplify this:

```
FresnelFactor = 1 - dot(Normal, ViewDirection)
```

This gives us a value of:
- **0** when viewing directly (normal perpendicular to view)
- **1** when viewing at glancing angles (normal parallel to view)

By applying a power function, we control the falloff curve:
```
FresnelFactor = pow(FresnelFactor, Power)
```

### Shader Graph vs. ShaderLab
This package includes both:
- **Shader Graph** (.shadergraph): Visual node-based editing
- **ShaderLab** (.shader): Code-based, more flexible, better documented

The ShaderLab version is fully functional and recommended for this effect.

---

## 💡 Credits

Shader created for Unity projects requiring holographic/sci-fi visual effects.
Compatible with Unity 2020.3+ (URP) and Unity 2019+ (Built-in).

---

## 📞 Support

For issues or questions:
1. Check the Troubleshooting section above
2. Verify all files are properly imported
3. Ensure your project uses a compatible render pipeline
4. Review the shader properties and recommended settings

---

**Enjoy creating stunning holographic effects! ✨**
