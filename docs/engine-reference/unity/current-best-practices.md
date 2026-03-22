# Unity 6.3 LTS — Current Best Practices

Last verified: 2026-03-22

## Rendering
- Use URP Render Graph for custom render passes (`AddRenderPasses`, not legacy `SetupRenderPasses`)
- GPU Lightmapper is default baking backend — prefer it over CPU
- xAtlas lightmap packing is default for new scenes
- Use Bloom filtering options (Kawase/Dual) for mobile optimization

## UI
- UI Toolkit is the recommended UI system for new projects
- Use UI Shader Graph for custom visual effects on UI elements
- USS filters support CSS-style post-processing (opacity, tint, grayscale, blur)
- Vector Graphics is now a core module with SVG import
- Use `PostProcessTextVertices` for glyph-level text animation

## Physics
- Box2D v3 API available under `UnityEngine.LowLevelPhysics2D` for 2D physics
- Supports multi-threading and determinism improvements

## Performance
- Use `FindObjectsByType` with explicit `FindObjectsSortMode` (not `FindObjectsOfType`)
- Use `FindAnyObjectByType` when order doesn't matter (faster)
- Use `Animator.ResetControllerState()` for object pooling (new in 6.3)

## Editor Workflow
- Build Profiles for managing multiple platform configurations
- LMDB-based search replaces old indexing (faster for large projects)
- Package signatures for security verification

## Testing
- UI Test Framework package for automated UI testing
- Render Graph Viewer connects to player builds for graphics debugging
- Adaptive Performance module for cross-platform framerate profiling

## Accessibility
- Native screen reader support: Windows (Narrator), macOS (VoiceOver)
- Use `AccessibilityRole` values for proper UI element identification

## Platform Notes
- Android minimum API level: 7.1 (API 25), adaptive icons only
- Web builds: IL2CPP variable-size metadata for smaller sizes
- Arm64 supported for dedicated Linux servers
