using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using MagicSpeller;
using Spectre.Console;
using System.CommandLine;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Color=Spectre.Console.Color;


var formatOption = new CliOption<OutputFormat>("-f", "--format")
{
    Description = "Output format.",
    DefaultValueFactory = _ => OutputFormat.Default
};
var maxSwapsOption = new CliOption<int>("-s", "--swaps")
{
    Description = "Maximum swaps to calculate word for.",
    DefaultValueFactory = _ => 2
};
maxSwapsOption.Validators.Add(result =>
{
    if (result.GetValueOrDefault<int>() < 0)
        result.AddError("Number of maximum swaps must be greater than 0.");
});
var inputOption = new CliOption<InputMode>("-i", "--input")
{
    Description = "The board to parse.",
    DefaultValueFactory = _ => new InputMode.Interactive(),
    Arity = ArgumentArity.OneOrMore,
    AllowMultipleArgumentsPerToken = true,
    HelpName = "File <path>|Interactive|Screenshot|Text <board>",
    CustomParser = result =>
    {
        switch (result.Tokens[0].Value.ToLowerInvariant())
        {
            case "file" when result.Tokens.Count == 2:
                return new InputMode.File(result.Tokens[1].Value);
            case "file":
                result.AddError("Input mode file must be of form 'file <path>'.");
                return null;
            case "interactive" when result.Tokens.Count == 1:
                return new InputMode.Interactive();
            case "interactive":
                result.AddError("Input mode interactive must be of form 'interactive'.");
                return null;
            case "screenshot" when result.Tokens.Count == 1:
                return new InputMode.Screenshot();
            case "screenshot":
                result.AddError("Input mode screenshot must be of form 'screenshot'.");
                return null;
            case "text" when result.Tokens.Count == 2:
                return new InputMode.Text(result.Tokens[1].Value);
            case "text":
                result.AddError("Input mode text must be of form 'text <board>'.");
                return null;
            default:
                result.AddError("Input mode not found. Must be one of 'file <path>', 'interactive', 'screenshot' or 'text <board>'.");
                return null;
        }
    }
};
var outputOption = new CliOption<OutputDestination>("-o", "--output")
{
    Description = "Output destination.",
    DefaultValueFactory = _ => new OutputDestination.Console(),
    Arity = ArgumentArity.OneOrMore,
    AllowMultipleArgumentsPerToken = true,
    HelpName = "Console|File <path>",
    CustomParser = result =>
    {
        switch (result.Tokens[0].Value.ToLowerInvariant())
        {
            case "console" when result.Tokens.Count == 1:
                return new OutputDestination.Console();
            case "console":
                result.AddError("Output destination console must be of form 'console'.");
                return null;
            case "file" when result.Tokens.Count == 2:
                return new OutputDestination.File(result.Tokens[1].Value);
            case "file":
                result.AddError("Output destination file must be of form 'file <path>'.");
                return null;
            default:
                result.AddError("Output destination not found. Must be either 'console' or 'file <path>'.");
                return null;
        }
    }
};

var rootCommand = new CliRootCommand("MagicSpeller") { formatOption, outputOption, inputOption, maxSwapsOption };
rootCommand.SetAction(result => Run(new(result.GetValue(formatOption),
    result.GetValue(inputOption)!,
    result.GetValue(outputOption)!,
    result.GetValue(maxSwapsOption))));
return new CliConfiguration(rootCommand).Invoke(args);


int Run(Settings settings)
{
    var words = new WordTrie("./words.txt");

    if (settings.OutputDestination is OutputDestination.File(var destination))
        SetupFileRedirection(destination);

    PrintWelcomeMessage(settings);

    var imageProcessor = LoadImageProcessor();
    if (imageProcessor is null && settings.InputMode is InputMode.Interactive)
    {
        AnsiConsole.MarkupLine("[red]Image processing files are not present![/]");
        AnsiConsole.MarkupLine("[red]Using any functions that depend on them will fail![/]");
    }

    PrintGuidance(settings);
    while (true)
    {
        var board = settings.InputMode switch
        {
            InputMode.Text(var text) => GetBoardFromString(text),
            InputMode.File(var path) => GetBoardFromString(File.ReadAllText(path)),
            InputMode.Screenshot => GetBoardFromScreen(imageProcessor),
            InputMode.Interactive => GetBoardInteractive(imageProcessor),
            _ => null
        };
        if (board is null)
            return 0;

        var watch = new Stopwatch();
        watch.Start();

        var tasks = new Task<SearchResult?>[settings.MaxSwaps + 1];
        tasks[0] = Task.Run(() => board.FindBestWord(words));
        for (var i = 1; i < tasks.Length; i++)
        {
            var theBetterI = i;
            tasks[i] = tasks[i - 1].ContinueWith(_ => board.FindBestWord(words, theBetterI));
        }
        tasks[^1].ContinueWith(_ => watch.Stop(), TaskContinuationOptions.ExecuteSynchronously);

        PrintResults(tasks, board, settings);
        if (settings.OutputFormat is not OutputFormat.Json)
            AnsiConsole.MarkupLine($"[gray]Search took {watch.Elapsed:g}[/]");

        if (settings.InputMode is not InputMode.Interactive) // Run only once
            break;
        if (settings.OutputFormat is OutputFormat.Json)
            continue;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Press [yellow3_1][[ENTER]][/] to analyze the current screen! Or input the field by hand!");
    }
    return 0;
}

ImageProcessor? LoadImageProcessor()
{
    try
    {
        return new();
    }
    catch (Exception ex) when (ex is TypeInitializationException or ArgumentException)
    {
        return null;
    }
}

void PrintWelcomeMessage(Settings settings)
{
    switch (settings.OutputFormat)
    {
        case OutputFormat.Json:
            break;
        case OutputFormat.Simple:
            AnsiConsole.MarkupLine("[mediumorchid]MagicSpeller[/]");
            AnsiConsole.MarkupLine("[gray]by Ingo[/]");
            break;
        case OutputFormat.Default:
        case OutputFormat.Animated:
        default:
            AnsiConsole.Write(new FigletText("MagicSpeller").Color(Color.MediumOrchid));
            AnsiConsole.MarkupLine("[gray]by Ingo[/]");
            break;
    }
}

void PrintGuidance(Settings settings)
{
    if (settings is { InputMode: InputMode.Interactive, OutputFormat: not OutputFormat.Json })
    {
        AnsiConsole.MarkupLine("Press [yellow3_1][[ENTER]][/] to analyze the current screen!");
        AnsiConsole.WriteLine("Or input the field by hand from left to right, from top to bottom with , seperating rows!");
        AnsiConsole.WriteLine("eg. A      B(2X)  C");
        AnsiConsole.WriteLine("    D      E      F");
        AnsiConsole.WriteLine("    G      H(TL)  I");
        AnsiConsole.WriteLine("will be AB(2X)C,DEF,GH(TL)I");
    }
}

void SetupFileRedirection(string path)
{
    AnsiConsole.Console = AnsiConsole.Create(new()
    {
        Ansi = AnsiSupport.No,
        ColorSystem = ColorSystemSupport.NoColors,
        Out = new AnsiConsoleOutput(new StreamWriter(File.Open(path, FileMode.Create), Encoding.Unicode) { AutoFlush = true }),
        Interactive = InteractionSupport.No,
    });
}

PlayingField? GetBoardFromScreen(ImageProcessor? imageProcessor)
{
    if (imageProcessor is null)
    {
        AnsiConsole.MarkupLine("[red]Error. Image processing files not present. Quiting..[/]");
        return null;
    }

    CvInvoke.UseOpenCL = false;
    using var screen = ScreenShooter.CaptureScreen();
    using var bitmap = new Bitmap(screen);
    using var image = bitmap.ToImage<Bgr, byte>();
    using var mat = image.Mat.GetUMat(AccessType.Fast);
    var playAreaRect = ImageProcessor.FindPlayAreaRect(mat);
    using var playArea = new UMat(mat, playAreaRect);
    return imageProcessor.LoadPlayingField(playArea);
}

PlayingField GetBoardFromString(string text)
{
    var lines = text.Split(',');
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
                AnsiConsole.WriteLine("Not enough characters entered! Filling with empty fields...");
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
                    AnsiConsole.WriteLine($"Invalid special field parameter '{row[x + xOffset]}'!");
                while (x + xOffset < row.Length - 1 && row[x + ++xOffset] != ')') {}
            }
            field[y, x] = new(letter, special);
        }
    }
    return new(field);
}

PlayingField? GetBoardInteractive(ImageProcessor? imageProcessor)
{
    var input = Console.ReadLine()?.ToUpperInvariant();
    return input switch
    {
        null => null,
        "" => GetBoardFromScreen(imageProcessor),
        _ => GetBoardFromString(input)
    };
}

Table CreateTable(PlayingField board)
{
    var table = new Table().HideHeaders();
    for (var x = 0; x < board.Cols; x++)
        table.AddColumn(new TableColumn("").Centered());
    for (var y = 0; y < board.Rows; y++)
        table.AddEmptyRow();

    for (var y = 0; y < board.Rows; y++)
    for (var x = 0; x < board.Cols; x++)
    {
        var letter = board.Fields[y, x].Letter;
        table.UpdateCell(y, x, new Markup($"{letter}{board.Fields[y, x].Special.ToDisplay()}"));
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
        var prefix = AnsiConsole.Profile.Capabilities.ColorSystem is not ColorSystem.NoColors
            ? $"[{baseColor.Lerp(lerpToColor, lettersFactor * index).ToMarkup()}]"
            : "[invert]";
        var suffix = "[/]";

        if (!AnsiConsole.Profile.Capabilities.Ansi && AnsiConsole.Profile.Capabilities.ColorSystem is ColorSystem.NoColors)
            suffix += "*";

        var swap = result.Swaps.FirstOrDefault(item => item.Position == new Point(point.X, point.Y));
        if (swap != default)
        {
            prefix += $"[red]{swap.OldChar}[/] [underline]";
            suffix += "[/]";
        }

        table.UpdateCell(point.Y, point.X, new Markup($"{prefix}{letter}{special.ToDisplay()}{suffix}"));
        index++;
        yield return 200;
    }
}

string IntToSwapString(int i) => i switch
{
    0 => "No Swaps",
    1 => "1 Swap",
    _ => $"{i} Swaps"
};

void PrintResults(Task<SearchResult?>[] tasks, PlayingField board, Settings settings)
{
    switch (settings.OutputFormat)
    {
        case OutputFormat.Simple:
            for (var i = 0; i < tasks.Length; i++)
                AnsiConsole.WriteLine($"{IntToSwapString(i)}: {tasks[i].Result?.ToString() ?? "No word found!"}");
            break;

        case OutputFormat.Json:
            AnsiConsole.WriteLine(JsonSerializer.Serialize(tasks.Select(task => task.Result).ToArray(), JsonContext.Default.SearchResultArray));
            break;

        case OutputFormat.Default or OutputFormat.Animated:
            var tables = new Table[tasks.Length];
            for (var i = 0; i < tables.Length; i++)
                tables[i] = CreateTable(board).Title(IntToSwapString(i), new(Color.Yellow));
            var all = new Rows(new Columns(tables).Collapse()).Collapse();

            // If output to file skip animation steps
            if (settings.OutputDestination is OutputDestination.File)
            {
                for (var i = 0; i < tasks.Length; i++)
                    if (tasks[i].Result is null)
                        tables[i].Caption("No word found!", new(Color.DarkOrange));
                    else
                        _ = UpdateTableAnimationStep(tables[i], tasks[i].Result!).Last();
                AnsiConsole.Write(all);
                break;
            }

            AnsiConsole.Live(all)
                .Start(ctx =>
                {
                    ctx.Refresh();
                    for (var i = 0; i < tasks.Length; i++)
                        UpdateTable(tables[i], tasks[i].Result);
                    return;

                    void UpdateTable(Table table, SearchResult? result)
                    {
                        if (result is null)
                        {
                            table.Caption("No word found!", new(Color.DarkOrange));
                            ctx.Refresh();
                            return;
                        }

                        if (settings.OutputFormat is OutputFormat.Animated)
                            foreach (var delay in UpdateTableAnimationStep(table, result))
                            {
                                ctx.Refresh();
                                Thread.Sleep(delay);
                            }
                        else
                            _ = UpdateTableAnimationStep(table, result).Last();
                        ctx.Refresh();
                    }
                });
            break;
    }
}


internal abstract record InputMode
{
    internal record File(string Path) : InputMode;

    internal record Text(string Board) : InputMode;

    internal record Screenshot : InputMode;

    internal record Interactive : InputMode
    {
        public override string ToString() => nameof(Interactive); // ToString overwrite here for default value in --help
    }
}

internal enum OutputFormat
{
    Default,
    Simple,
    Animated,
    Json
}

internal abstract record OutputDestination
{
    internal record File(string Path) : OutputDestination;

    internal record Console : OutputDestination
    {
        public override string ToString() => nameof(Console); // ToString overwrite here for default value in --help
    }
}

internal record Settings(OutputFormat OutputFormat, InputMode InputMode, OutputDestination OutputDestination, int MaxSwaps);

[JsonSerializable(typeof(SearchResult?[]))]
internal partial class JsonContext : JsonSerializerContext;
