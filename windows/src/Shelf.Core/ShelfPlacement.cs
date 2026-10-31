namespace Shelf.Core;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public static class ShelfPlacement
{
    // All incoming and outgoing coordinates are physical pixels. Only sizes originate in DIPs.
    public static PixelRect Calculate(PixelRect workArea, uint dpi, bool collapsed)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0 || dpi == 0)
            throw new ArgumentOutOfRangeException(nameof(workArea));
        double scale = dpi / 96d;
        int width = Math.Min(workArea.Width, Math.Max(1, (int)Math.Round((collapsed ? 36 : 320) * scale)));
        int height = Math.Min(workArea.Height, Math.Max(1, (int)Math.Round((collapsed ? 128 : 500) * scale)));
        int top = workArea.Top + (workArea.Height - height) / 2;
        return new(workArea.Left, top, workArea.Left + width, top + height);
    }
}
