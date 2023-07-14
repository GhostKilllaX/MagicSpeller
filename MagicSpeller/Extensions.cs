using Emgu.CV;
using Emgu.CV.Structure;
using Spectre.Console;

namespace MagicSpeller;

public static class Extensions
{
    public static UMat SubUMat(this UMat mat, CircleF circle)
    {
        return new(mat,
            new((int)(circle.Center.X - circle.Radius),
                (int)(circle.Center.Y - circle.Radius),
                (int)(circle.Radius * 2),
                (int)(circle.Radius * 2)));
    }

    public static string ToDisplay(this SpecialField special) => special switch
    {
        SpecialField.None => "",
        SpecialField.DoubleLetter => "-(DL)",
        SpecialField.TripleLetter => "-(TL)",
        SpecialField.DoubleWord => "-(2X)",
        SpecialField.TripleWord => "-(3X)",
        _ => throw new ArgumentOutOfRangeException(nameof(special), special, null)
    };

    public static Color Lerp(this Color s, Color t, float k)
    {
        var bk = 1 - k;
        var r = s.R * bk + t.R * k;
        var g = s.G * bk + t.G * k;
        var b = s.B * bk + t.B * k;
        return new((byte)r, (byte)g, (byte)b);
    }
}
