using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.OCR;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Drawing;

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
        SpecialField.None => "     ",
        SpecialField.DoubleLetter => "-(DL)",
        SpecialField.TripleLetter => "-(TL)",
        SpecialField.DoubleWord => "-(2X)",
        SpecialField.TripleWord => "-(3X)",
        _ => throw new ArgumentOutOfRangeException(nameof(special), special, null)
    };
}

public class ImageProcessor
{
    private readonly Tesseract _ocr =
        new("./tessdata", "eng", OcrEngineMode.Default, "23ABCDEFGHIJKLMNOPQRSTUVWXYZ");

    public ImageProcessor() => _ocr.SetVariable("debug_file", "NUL");

    ~ImageProcessor() => _ocr.Dispose();

    private char? RecognizeChar(UMat image)
    {
        _ocr.PageSegMode = PageSegMode.SingleChar;
        _ocr.SetImage(image);
        _ocr.Recognize();
        var text = _ocr.GetUTF8Text().Trim();
        if (text.Length == 0)
            return null;
        if (text.Length > 1)
            Console.WriteLine($"Unsure field text detected, thinking the text is: {text}");

        return text[0];
    }

    private static List<Rectangle> FindRectangles(UMat image)
    {
        using var gray = new UMat();
        using var cannyEdges = new UMat();

        CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);
        CvInvoke.GaussianBlur(gray, gray, new(3, 3), 1);

        const double cannyThreshold = 180.0;
        const double cannyThresholdLinking = 120.0;
        CvInvoke.Canny(gray, cannyEdges, cannyThreshold, cannyThresholdLinking);

        var rectangles = new List<Rectangle>();
        using var contours = new VectorOfVectorOfPoint();
        CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

        var count = contours.Size;
        for (var i = 0; i < count; i++)
        {
            using var contour = contours[i];
            using var approxContour = new VectorOfPoint();
            CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.055, true);
            if (CvInvoke.ContourArea(approxContour) <= 250)
                continue;
            if (approxContour.Size != 4)
                continue;

            rectangles.Add(CvInvoke.BoundingRectangle(contour));
        }

#if DEBUG
        var rectangleMat = new UMat(image.Size, DepthType.Cv8U, 3);
        rectangleMat.SetTo(new MCvScalar(0));
        foreach (var box in rectangles)
            CvInvoke.Rectangle(rectangleMat, box, new Bgr(Color.DarkOrange).MCvScalar, 2);
        CvInvoke.Rectangle(rectangleMat, new(Point.Empty, new(rectangleMat.Size.Width - 1, rectangleMat.Size.Height - 1)), new(120, 120, 120));
        CvInvoke.PutText(rectangleMat, "Rectangles", new(20, 20), FontFace.HersheyDuplex, 0.5, new(120, 120, 120));
        var result = new UMat();
        CvInvoke.VConcat(new VectorOfUMat(image, rectangleMat), result);
        result.Save("parsed_image.png");
#endif
        return rectangles;
    }

    private static CircleF[] FindSpecialCircle(UMat image)
    {
        const double cannyThreshold = 180.0;
        const double circleAccumulatorThreshold = 60;
        var circles = CvInvoke.HoughCircles(image, HoughModes.Gradient, 1.5, 20.0, cannyThreshold, circleAccumulatorThreshold, 10, 50)
            .Where(circle =>
            {
                if (circle.Center.X is < 40 or > 960) // Ignore corner circles
                    return false;
                using var circleMat = image.SubUMat(circle);
                return CvInvoke.Mean(circleMat).V0 < 100; // Check if inside is mostly black (ignoring circles on field O G C)
            })
            .ToArray();
#if DEBUG
        var circleMat = new UMat(image.Size, DepthType.Cv8U, 1);
        circleMat.SetTo(new MCvScalar(0));
        foreach (var circle in circles)
            CvInvoke.Circle(circleMat, Point.Round(circle.Center), (int)circle.Radius, new Bgr(Color.Brown).MCvScalar, 2);
        CvInvoke.Rectangle(circleMat, new(Point.Empty, new(circleMat.Size.Width - 1, circleMat.Size.Height - 1)), new(120, 120, 120));
        CvInvoke.PutText(circleMat, "Circles", new(20, 20), FontFace.HersheyDuplex, 0.5, new(120, 120, 120));
        var result = new UMat();
        CvInvoke.VConcat(new VectorOfUMat(image, circleMat), result);
        result.Save("parsed_circle_image.png");
#endif
        return circles;
    }

    private SpecialField ParseSpecialCircle(UMat image)
    {
        _ocr.PageSegMode = PageSegMode.SingleLine;
        _ocr.SetImage(image);
        _ocr.Recognize();
        var text = _ocr.GetUTF8Text().Trim();
        if (text.Contains('2'))
            return SpecialField.DoubleWord;
        if (text.Contains('3'))
            return SpecialField.TripleWord;
        if (text.Contains('D'))
            return SpecialField.DoubleLetter;
        if (text.Contains('T'))
            return SpecialField.TripleLetter;
        Console.WriteLine($"Unsure circle detected, thinking the text is: {text}");
        return SpecialField.None;
    }

    public PlayingField LoadPlayingField(UMat image)
    {
        var treshold = -0.025 * image.Size.Width + 40;
        var modified = new UMat();
        CvInvoke.Resize(image, image, new(1000, 1000));
        CvInvoke.GaussianBlur(image, modified, new(3, 3), 1, 1);
        CvInvoke.CvtColor(modified, modified, ColorConversion.Bgr2Gray);

        var circles = FindSpecialCircle(modified);

        CvInvoke.Threshold(modified, modified, treshold, 255, ThresholdType.Binary);

        var fields = new Field[5, 5];
        const double charBoxWidthPercantage = 0.15D;
        const double charBoxInnerSpaceWidthPercantage = 0.25D;
        const double charBoxInnerSpaceHeightPercantage = 0.2D;
        const double innerSpaceWidthPercantage = 0.033D;
        const double outerSpacePercantage = 0.06D;
        var charBoxWidth = charBoxWidthPercantage * image.Size.Width;
        var charBoxInnerSpaceWidth = charBoxInnerSpaceWidthPercantage * charBoxWidth;
        var charBoxInnerSpaceHeight = charBoxInnerSpaceHeightPercantage * charBoxWidth;
        var innerSpaceWidth = innerSpaceWidthPercantage * image.Size.Width;
        var outerSpace = outerSpacePercantage * image.Size.Width;
        var start = new Point((int)outerSpace, (int)outerSpace);

        var parsedCircles = new List<(CircleF, SpecialField)>();
        foreach (var circle in circles)
        {
            using var sub = image.SubUMat(circle);
            var offsetX = sub.Size.Width * 0.1;
            var offsetY = sub.Size.Width * 0.2;
            using var newSub = new UMat(sub,
                new((int)offsetX, (int)offsetY, (int)(sub.Size.Width - offsetX * 2), (int)(sub.Size.Height - offsetY * 2)));
            CvInvoke.CvtColor(newSub, newSub, ColorConversion.Bgr2Gray);
            CvInvoke.Threshold(newSub, newSub, 150, 255, ThresholdType.BinaryInv);
#if DEBUG
            newSub.Save($"{circle.Center.X:F}_{circle.Center.Y:F}_circle.png");
#endif
            var special = ParseSpecialCircle(newSub);
            parsedCircles.Add((circle, special));
        }

        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            var rect = new Rectangle((int)(start.X + x * innerSpaceWidth + x * charBoxWidth + charBoxInnerSpaceWidth),
                (int)(start.Y + y * innerSpaceWidth + y * charBoxWidth + charBoxInnerSpaceHeight),
                (int)(charBoxWidth - charBoxInnerSpaceWidth * 2),
                (int)(charBoxWidth - charBoxInnerSpaceHeight * 2));
            using var sub = new UMat(modified, rect);
#if DEBUG
            sub.Save($"{y}{x}.png");
#endif
            var special = SpecialField.None;
            foreach (var (circle, circleSpecial) in parsedCircles)
            {
                var circleY = (int)(circle.Center.Y + circle.Radius * 2);
                var circleX = circleSpecial switch
                {
                    SpecialField.DoubleLetter or SpecialField.TripleLetter => (int)(circle.Center.X + circle.Radius * 2),
                    SpecialField.DoubleWord or SpecialField.TripleWord => (int)(circle.Center.X - circle.Radius * 2),
                    SpecialField.None => 10000,
                    _ => throw new NotSupportedException($"SpecialField enum value of {circleSpecial} not supported by image parser")
                };
                if (rect.Contains(circleX, circleY))
                {
                    special = circleSpecial;
                    break;
                }
            }
            fields[y, x] = new(RecognizeChar(sub) ?? ' ', special);
        }
        return new(fields);
    }

    public static Rectangle FindPlayAreaRect(UMat image) =>
        FindRectangles(image)
            .Where(box => (float)box.Size.Height / box.Size.Width is >= 0.9f and <= 1.1f)
            .MaxBy(box => box.Size.Height * box.Size.Width);
}
