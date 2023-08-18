# MagicSpeller 🧙‍

**Solver for the Discord puzzle game SpellCast.**

## Installing

To use MagicSpeller, head to the [release page](https://github.com/GhostKilllaX/MagicSpeller/releases) and download the
latest pre compiled program or build the project yourself.

## Features

* **Fast**. Multithreading and C#.
* **Easy to use**. Uses image processing algorithms to process the game board from your current screen.
* **Different output formats.** You can select between simple, with a table (like in the example) and json output.

## How it works

* After starting the executable, you can either input the game board by hand or let the program analyze your current  
  screen and find the game board by itself.
* For the image analyzing, the program uses the OpenCV library and basically searches for the biggest square.
  So make sure no other big squares exist lol
* After entering the board the program will search for the best word with no, one and two swaps and prints the result
  to the console.

## Example

![Example](/images/example.gif)

## CLI

Via the command line interface different options can be set.

![img.png](/images/help.png)

For example, if you want to call this solver from another process, you could call it like
"MagicSpeller.exe --input Screenshot --format Json --output File result.json" and process the json file further.

## License

MIT License