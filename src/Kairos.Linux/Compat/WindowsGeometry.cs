// The shared engine uses WPF's Rect, Point, Size and Int32Rect for fractions of a picture; WPF does not exist on Linux,
// so the same names are defined here with the members the engine uses.
namespace System.Windows;

public readonly record struct Point(double X, double Y);

public readonly record struct Size(double Width, double Height);

public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(Point p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;
}

public readonly record struct Int32Rect(int X, int Y, int Width, int Height);
