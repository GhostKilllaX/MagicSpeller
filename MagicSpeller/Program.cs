using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using MagicSpeller;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;

var imageProcessor = new ImageProcessor();
var words = new WordList("./words.txt");

Console.WriteLine("MagicSpeller");
Console.WriteLine("Press [ENTER] to analyize the current screen!");
Console.WriteLine("Or input the field by hand from left to right, from top to bottom with , seperating rows!");
Console.WriteLine("eg. A      B(2X)  C");
Console.WriteLine("    D      E      F");
Console.WriteLine("    G      H(TL)  I");
Console.WriteLine("will be AB(2X)C,DEF,GH(TL)I");

while (true)
{
    PlayingField game;
    var line = Console.ReadLine()?.ToUpperInvariant();
    if (line is null)
        return;
    if (line.Length != 0)
    {
        var lines = line.Split(',');
        var regex = new Regex(@"\(.+?\)");
        var rows = lines.Max(row => regex.Replace(row, "").Length);

        var field = new Field[lines.Length, rows];
        for (var y = 0; y < lines.Length; y++)
        {
            var row = lines[y];
            var xOffset = 0;
            for (var x = 0; x < rows; x++)
            {
                if (x + xOffset >= row.Length)
                {
                    Console.WriteLine("Not enough characters entered! Filling with empty fields...");
                    row += new string(' ', rows);
                }

                var letter = row[x + xOffset];
                var special = SpecialField.None;
                if (x + xOffset + 1 < row.Length && row[x + xOffset + 1] == '(')
                {
                    xOffset += 2;
                    special = row[x + xOffset] switch
                    {
                        '2' => SpecialField.DoubleWord,
                        '3' => SpecialField.TripleWord,
                        'D' => SpecialField.DoubleLetter,
                        'T' => SpecialField.TripleLetter,
                        _ => SpecialField.None
                    };
                    if (special == SpecialField.None)
                        Console.WriteLine($"Invalid special field parameter '{row[x + xOffset]}'!");
                    while (x + xOffset < row.Length - 1 && row[x + ++xOffset] != ')') {}
                }
                field[y, x] = new(letter, special);
            }
        }
        game = new(field);
    }
    else
    {
        CvInvoke.UseOpenCL = false;
        using var screen = ScreenShooter.CaptureScreen();
        using var bitmap = new Bitmap(screen);
        using var image = bitmap.ToImage<Bgr, byte>();
        using var mat = image.Mat.GetUMat(AccessType.Fast);
        var playAreaRect = ImageProcessor.FindPlayAreaRect(mat);
        using var playArea = new UMat(mat, playAreaRect);
        game = imageProcessor.LoadPlayingField(playArea);
    }

    var watch = new Stopwatch();
    watch.Start();

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

    Console.WriteLine("Press [ENTER] to analyize the current screen! Or input the field by hand!");
}
