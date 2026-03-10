using SkiaSharp;

namespace NdiTelop.Models;

/// <summary>
/// トランジション描画結果を保持するモデル。
/// </summary>
public sealed class TransitionFrame : IDisposable
{
    public required SKBitmap Bitmap { get; init; }
    public float Progress { get; init; }

    public void Dispose()
    {
        Bitmap.Dispose();
    }
}
