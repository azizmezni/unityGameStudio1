# Unity 6.x — Deprecated APIs

Last verified: 2026-03-22

## Don't Use -> Use Instead

| Deprecated | Replacement | Since |
|------------|-------------|-------|
| `Object.FindObjectsOfType<T>()` | `Object.FindObjectsByType<T>(FindObjectsSortMode)` | 6.0 |
| `Object.FindObjectOfType<T>()` | `Object.FindFirstObjectByType<T>()` or `Object.FindAnyObjectByType<T>()` | 6.0 |
| `GraphicsFormat.DepthAuto` | `GraphicsFormat.None` | 6.0 |
| `GraphicsFormat.ShadowAuto` | `GraphicsFormat.None` | 6.0 |
| `ExecuteDefaultAction()` | `HandleEventBubbleUp()` | 6.0 |
| `ExecuteDefaultActionAtTarget()` | `HandleEventTrickleDown()` | 6.0 |
| `PreventDefault()` | `StopPropagation()` | 6.0 |
| `CustomEditorForRenderPipelineAttribute` | `CustomEditor` + `SupportedOnRenderPipelineAttribute` | 6.0 |
| `filteringGaussRadiusAO` | `filteringGaussianRadiusAO` (float) | 6.0 |
| `filteringGaussRadiusDirect` | `filteringGaussianRadiusDirect` (float) | 6.0 |
| `filteringGaussRadiusIndirect` | `filteringGaussianRadiusIndirect` (float) | 6.0 |
| `SetupRenderPasses` (URP) | `AddRenderPasses` (render graph) | 6.0 |
| `AccessibilityNode.selected` | `AccessibilityNode.invoked` | 6.3 |
| `Social` API | No direct replacement (removed) | 6.0 |

## Removed (No Replacement)

| Removed | Since | Notes |
|---------|-------|-------|
| `GraphicsFormat.VideoAuto` | 6.0 | No replacement needed |
| Enlighten Baked GI | 6.0 | Use Progressive Lightmapper |
| Custom Search Indices | 6.3 | Unified index only |
| Search Index Manager | 6.3 | No replacement |
| Round/Legacy Android icons | 6.3 | Use adaptive icons |
