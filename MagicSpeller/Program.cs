using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using MagicSpeller;
using Spectre.Console;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using Color=Spectre.Console.Color;

var imageProcessor = new ImageProcessor();
var words = new WordList("./words.txt");

AnsiConsole.Write(new FigletText("MagicSpeller").Color(Color.MediumOrchid));
AnsiConsole.MarkupLine("[gray]by Ingo[/]");
AnsiConsole.MarkupLine("Press [yellow3_1][[ENTER]][/] to analyze the current screen!");
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
        AnsiConsole.Cursor.MoveUp(1);
        Console.WriteLine("Analyzing current screen!");
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
    var noSwap = CreateTable().Title("No Swaps", new(Color.Yellow));
    var oneSwap = CreateTable().Title("One Swap", new(Color.Yellow));
    var twoSwap = CreateTable().Title("Two Swaps", new(Color.Yellow));
    var all = new Columns(noSwap, oneSwap, twoSwap).Collapse();

    AnsiConsole.Live(all)
        .Start(ctx =>
        {
            ctx.Refresh();
            var bestNoSwap = game.FindBestWord(words.WordTree);
            var bestOneSwap = Task.Run(() => game.FindBestWordWithSwaps(words, 1));
            var bestTwoSwap = bestOneSwap.ContinueWith(_ => game.FindBestWordWithSwaps(words, 2));

            UpdateTable(noSwap, bestNoSwap);
            UpdateTable(oneSwap, bestOneSwap.Result);
            UpdateTable(twoSwap, bestTwoSwap.Result);
            return;

            void UpdateTable(Table table, SearchResult? result)
            {
                if (result is null)
                {
                    table.Caption("No word found!", new(Color.DarkOrange));
                    ctx.Refresh();
                    return;
                }

                foreach (var delay in UpdateTableAnimationStep(table, result))
                {
                    if (args.Length > 0 && args[0] == "--anim")
                    {
                        ctx.Refresh();
                        Thread.Sleep(delay);
                    }
                }
                ctx.Refresh();
            }
        });

    AnsiConsole.MarkupLine($"[gray]Search took {watch.Elapsed:g}[/]");
    Console.WriteLine();
    AnsiConsole.MarkupLine("Press [yellow3_1][[ENTER]][/] to analyze the current screen! Or input the field by hand!");
    continue;


    Table CreateTable()
    {
        var table = new Table().HideHeaders();
        for (var x = 0; x < game.Cols; x++)
            table.AddColumn(new TableColumn("").Centered());
        for (var y = 0; y < game.Rows; y++)
            table.AddEmptyRow();

        for (var y = 0; y < game.Rows; y++)
        for (var x = 0; x < game.Cols; x++)
        {
            var letter = game.Fields[y, x].Letter;
            table.UpdateCell(y, x, new Markup($"{letter}{game.Fields[y, x].Special.ToDisplay()}"));
        }

        table.Caption("Searching...", new(Color.Orange1));
        return table;
    }

    IEnumerable<int> UpdateTableAnimationStep(Table table, SearchResult result)
    {
        var baseColor = Color.DodgerBlue3;
        var lerpToColor = Color.SpringGreen3;
        var lettersFactor = 1f / (result.Fields.Count - 1);

        var word = string.Concat(result.Word.Select((letter, i) =>
            $"[{baseColor.Lerp(lerpToColor, i * lettersFactor).ToMarkup()}]{letter}[/]"));
        table.Caption($"{word} {result.Points}P", new(Color.Green3));

        var index = 0;
        foreach (var ((letter, special), point) in result.Fields)
        {
            var prefix = $"[{baseColor.Lerp(lerpToColor, lettersFactor * index).ToMarkup()}]";
            var suffix = "[/]";

            if (result is SearchResultWithSwaps swaps)
            {
                var swap = swaps.Swaps.FirstOrDefault(item => item.Position == new Point(point.X, point.Y));
                if (swap != default)
                {
                    prefix += $"[red]{swap.OldChar}[/] [underline]";
                    suffix += "[/]";
                }
            }

            table.UpdateCell(point.Y, point.X, new Markup($"{prefix}{letter}{special.ToDisplay()}{suffix}"));
            index++;
            yield return 200;
        }
    }
}
