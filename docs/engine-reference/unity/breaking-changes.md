# Unity 6.x — Breaking Changes

Last verified: 2026-03-22

## Unity 6.0 (from 2023.x)

### Object Finding API
- `Object.FindObjectsOfType` -> `Object.FindObjectsByType` (requires `FindObjectsSortMode`)
- `Object.FindObjectOfType` -> `Object.FindFirstObjectByType` or `Object.FindAnyObjectByType`

### Graphics Formats (Obsolete)
- `GraphicsFormat.DepthAuto` -> `GraphicsFormat.None`
- `GraphicsFormat.ShadowAuto` -> `GraphicsFormat.None`
- `GraphicsFormat.VideoAuto` -> removed entirely

### Lighting
- Enlighten Baked GI backend removed — use Progressive Lightmapper
- Auto-generate lighting disabled — use `Lightmapping.Bake()` or manual UI
- Ambient probe and skybox Reflection Probe no longer baked by default

### Texture Mipmaps
- Runtime textures no longer have mipmap limits by default (now opt-in)
- Use `MipmapLimitDescriptor` constructor parameter for quality compliance

### Metal Shaders
- `min16float`, `half`, `real` compile to 32-bit floats on Metal
- Verify buffer layouts match C# data structures for compute shaders

### Android
- `UnityPlayer` renamed to `UnityPlayerForActivityOrService`
- `FrameLayout` accessed via `getFrameLayout()` instead of inheritance
- `UPM_CACHE_PATH` -> `UPM_CACHE_ROOT`

### UI Toolkit Events
- `ExecuteDefaultAction` / `ExecuteDefaultActionAtTarget` -> `HandleEventBubbleUp` / `HandleEventTrickleDown`
- `PreventDefault` -> `StopPropagation`

### Render Pipeline Attributes
- `CustomEditorForRenderPipelineAttribute` -> `CustomEditor` + `SupportedOnRenderPipelineAttribute`
- `VolumeComponentMenuForRenderPipelineAttribute` -> `VolumeComponentMenu` + `SupportedOnRenderPipelineAttribute`

## Unity 6.3 LTS

### Accessibility
- `AccessibilityRole` changed from flags enum to standard enum
- `AccessibilityState` underlying type changed to `byte`
- `AccessibilityNode.selected` deprecated -> renamed to `invoked`

### Search System
- Custom search indices no longer supported (single unified index)
- Search Index Manager removed

### Android
- Minimum API level raised to 7.1 (API 25)
- Round and legacy icon support deprecated (use adaptive icons)
