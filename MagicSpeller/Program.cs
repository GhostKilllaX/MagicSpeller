using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using MagicSpeller;
using System.Diagnostics;
using System.Drawing;

var imageProcessor = new ImageProcessor();
var words = new WordList("./words.txt");

Console.WriteLine("MagicSpeller");
while (true)
{
    Console.WriteLine("Press [ANY KEY] to analyize the current screen!");
    Console.ReadKey();

    var watch = new Stopwatch();
    watch.Start();

    CvInvoke.UseOpenCL = false;
    using var screen = ScreenShooter.CaptureScreen();
    using var bitmap = new Bitmap(screen);
    using var image = bitmap.ToImage<Bgr, byte>();
    using var mat = image.Mat.GetUMat(AccessType.Fast);
    var playAreaRect = ImageProcessor.FindPlayAreaRect(mat);
    using var playArea = new UMat(mat, playAreaRect);
    var game = imageProcessor.LoadPlayingField(playArea);

    Console.WriteLine("Parsed Field:");
    for (var y = 0; y < game.Fields.GetLength(0); y++)
    {
        for (var x = 0; x < game.Fields.GetLength(1); x++)
            Console.Write($"{game.Fields[y, x].Letter}{game.Fields[y, x].Special.ToDisplay()} ");
        Console.WriteLine();
    }

    var bestMatch = game.FindBestWord(words.WordTree);
    Console.WriteLine($"Best word: {bestMatch}");
    bestMatch = game.FindBestWordWithSwaps(words, 1);
    Console.WriteLine($"Best word 1 swap: {bestMatch}");
    bestMatch = game.FindBestWordWithSwaps(words, 2);
    Console.WriteLine($"Best word 2 swap: {bestMatch}");
    Console.WriteLine($"Elapsed time: {watch.Elapsed:g}");
    Console.WriteLine();
}
