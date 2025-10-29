# How to Create the Fresnel Glow Shader in Shader Graph

Since Shader Graph files must be created within Unity's visual editor, follow these step-by-step instructions to recreate the effect.

> **Note:** The `FresnelGlowEffect.shader` file already provides a fully functional version of this shader. Use Shader Graph only if you prefer visual node-based editing.

---

## 📋 Prerequisites

1. Install Shader Graph package via Package Manager
2. Ensure your project uses URP (Universal Render Pipeline)

---

## 🎨 Creating the Shader Graph

### Step 1: Create New Shader Graph

1. Right-click in `Assets/shader_claude/FresnelGlow/`
2. Select **Create > Shader Graph > URP > Unlit Shader Graph**
3. Name it: `FresnelGlowShaderGraph`

### Step 2: Configure Graph Settings

1. Open the Shader Graph (double-click)
2. Click the **Graph Inspector** button (top-right)
3. In **Graph Settings**:
   - **Surface Type:** Transparent
   - **Blend Mode:** Alpha
   - **Two Sided:** ✓ (optional)
   - **Cast Shadows:** ✗
   - **Receive Shadows:** ✗

---

## 🔧 Step 3: Create Properties

Click the **+** button in the **Blackboard** panel to add these properties:

| Name | Type | Mode | Default Value | Expose |
|------|------|------|---------------|--------|
| Base Color | Color | Default | (0.2, 0.5, 1.0, 1.0) | ✓ |
| Alpha | Float | Slider (0-1) | 0.3 | ✓ |
| Emission Color | Color | HDR | (0, 1, 1, 1) | ✓ |
| Emission Intensity | Float | Slider (0-10) | 2.0 | ✓ |
| Fresnel Power | Float | Slider (0.1-10) | 3.0 | ✓ |
| Grid Texture | Texture2D | Default | None | ✓ |
| Grid Tiling | Float | Slider (1-50) | 10.0 | ✓ |
| Pulse Speed | Float | Slider (0-10) | 2.0 | ✓ |

---

## 🔗 Step 4: Build the Node Graph

### A. Fresnel Effect Setup

1. Add **Fresnel Effect** node (Create Node > Input > Geometry > Fresnel Effect)
   - Connect **Power** port ← `Fresnel Power` property

### B. Pulse Animation Setup

1. Add **Time** node (Create Node > Input > Time)
2. Add **Multiply** node
   - Input A ← Time node (T output)
   - Input B ← `Pulse Speed` property
3. Add **Sine** node
   - Connect from Multiply output
4. Add **Multiply** node (Sine multiplier)
   - Input A ← Sine output
   - Input B ← Constant (0.5)
5. Add **Add** node (Offset)
   - Input A ← Previous Multiply
   - Input B ← Constant (1.0)
   - **Result = Pulse Value** (oscillates 0.5 to 1.5)

### C. Grid Texture Setup

1. Add **UV** node (Create Node > Input > Geometry > UV)
2. Add **Multiply** node
   - Input A ← UV node
   - Input B ← `Grid Tiling` property
3. Add **Sample Texture 2D** node
   - Texture ← `Grid Texture` property
   - UV ← Multiply output
4. Add **Split** node
   - Connect from Sample Texture 2D (RGBA output)
   - Use **R** output only

### D. Emission Calculation

1. Add **Multiply** node (Emission × Fresnel)
   - Input A ← `Emission Color` property
   - Input B ← Fresnel Effect output

2. Add **Multiply** node (× Intensity)
   - Input A ← Previous result
   - Input B ← `Emission Intensity` property

3. Add **Multiply** node (× Pulse)
   - Input A ← Previous result
   - Input B ← Pulse Value (from step B)

4. Add **Multiply** node (× Grid)
   - Input A ← Previous result
   - Input B ← Grid texture R channel (from step C)
   - **Result = Final Emission**

### E. Base Color with Alpha

1. Add **Multiply** node
   - Input A ← `Base Color` property
   - Input B ← `Alpha` property
   - **Result = Base with Alpha**

### F. Final Combination

1. Add **Add** node (Final combination)
   - Input A ← Base with Alpha (from E)
   - Input B ← Final Emission (from D)

2. Add **Split** node on the final result
   - Use RGB for Base Color output
   - Connect RGB ← Master Stack **Base Color**

3. Connect `Alpha` property → Master Stack **Alpha**

---

## 📊 Node Layout Diagram

```
[Fresnel Power] ──→ [Fresnel Effect]
                           │
[Time] ──→ [×] ──→ [Sin] ──→ [× 0.5] ──→ [+ 1.0] = Pulse
           ↑
    [Pulse Speed]

[UV] ──→ [× Grid Tiling] ──→ [Sample Texture 2D] ──→ [Split] → R
                                      ↑
                              [Grid Texture]

[Emission Color] ──→ [×] ──→ [×] ──→ [×] ──→ [×] = Final Emission
                      ↑      ↑      ↑      ↑
                   Fresnel  Inten  Pulse  Grid

[Base Color] ──→ [× Alpha] = Base with Alpha

[Base with Alpha] ──→ [+] ──→ [Split] → RGB → [Base Color Output]
[Final Emission]  ──→ ─┘

[Alpha] ────────────────────────────────→ [Alpha Output]
```

---

## ✅ Step 5: Connect to Master Stack

The Master Stack (Fragment context) on the right should have:

1. **Base Color** ← Final combined RGB (from Add node → Split → RGB)
2. **Alpha** ← `Alpha` property

---

## 💾 Step 6: Save and Test

1. Click **Save Asset** button (top-left)
2. Create a new Material
3. Select your new Shader Graph shader
4. Assign the Grid Texture
5. Apply to a 3D object

---

## 🎯 Quick Alternative: Use the Code-Based Shader

If Shader Graph seems complex, remember that **FresnelGlowEffect.shader** is already fully functional and provides identical results! Just use that shader instead.

**Advantages of the code-based shader:**
- ✓ No Shader Graph package required
- ✓ Works immediately after import
- ✓ More performant (no extra overhead)
- ✓ Easier to version control
- ✓ Works in both URP and Built-in Pipeline
- ✓ Better documented with inline comments

---

## 🐛 Troubleshooting Shader Graph

### Graph doesn't save
- Check console for errors
- Ensure all required nodes are connected
- Verify Surface Type is set to Transparent

### Nodes show errors
- Ensure all ports are connected correctly
- Check property types match node inputs
- Verify Shader Graph package is up-to-date

### Material appears black
- Check Alpha value (shouldn't be 0)
- Ensure Emission values are > 0
- Verify Grid Texture is assigned

### No transparency
- Confirm Surface Type = Transparent in Graph Settings
- Check that Alpha is properly connected to Master Stack

---

## 📚 Additional Resources

- [Unity Shader Graph Documentation](https://docs.unity3d.com/Packages/com.unity.shadergraph@latest)
- [Fresnel Effect Tutorial](https://docs.unity3d.com/Packages/com.unity.shadergraph@latest/index.html?subfolder=/manual/Fresnel-Effect-Node.html)

---

**Recommendation:** Use `FresnelGlowEffect.shader` for production. It's fully implemented, tested, and ready to use! 🚀
