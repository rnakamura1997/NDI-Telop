namespace NdiTelop.Services;

internal static class RenderTransitionHelper
{
    internal static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}
