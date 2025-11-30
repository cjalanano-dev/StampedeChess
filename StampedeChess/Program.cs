using Spectre.Console;
using StampedeChess.Core;
using StampedeChess.UI;   
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StampedeChess
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Stampede Chess Engine";
            Console.CursorVisible = false;

            bool appRunning = true;
            while (appRunning)
            {
                string selectedOption = RunMenu(); 

                switch (selectedOption)
                {
                    case "Start":
                        RunGame();
                        break;
                    case "Options":
                        RunOptions();
                        break;
                    case "Exit":
                        appRunning = false;
                        break;
                }
            }

            Console.ResetColor();
            Console.Clear();
            Console.WriteLine("System Shutdown Complete.");
        }

        static string RunMenu()
        {
            string[] options = { "Start", "Options", "Exit" };
            int selectedIndex = 0;

            // Main Menu Title
            string titleArt = 
@"
███████╗████████╗ █████╗ ███╗   ███╗██████╗ ███████╗██████╗ ███████╗
██╔════╝╚══██╔══╝██╔══██╗████╗ ████║██╔══██╗██╔════╝██╔══██╗██╔════╝
███████╗   ██║   ███████║██╔████╔██║██████╔╝█████╗  ██║  ██║█████╗  
╚════██║   ██║   ██╔══██║██║╚██╔╝██║██╔═══╝ ██╔══╝  ██║  ██║██╔══╝  
███████║   ██║   ██║  ██║██║ ╚═╝ ██║██║     ███████╗██████╔╝███████╗
╚══════╝   ╚═╝   ╚═╝  ╚═╝╚═╝     ╚═╝╚═╝     ╚══════╝╚═════╝ ╚══════╝
";

            // splashes (meant to imitate the splashes on minecraft menu screen)
            string[] splashes =
            {
                "Checkmate in 4!", "Written in C#!", "Console Edition!",
                "No Mouse Required!", "Powered by Logic!", "Heavy Metal Chess!"
            };
            string currentSplash = splashes[new Random().Next(splashes.Length)];

            // layout of main menu
            while (true)
            {
                AnsiConsole.Clear();

                var logo = new Text(titleArt, new Style(Color.Cyan1)).Centered();
                var splashText = new Text(currentSplash, new Style(Color.Yellow, decoration: Decoration.Bold)).Centered();

                AnsiConsole.Write(logo);
                AnsiConsole.Write(splashText);
                AnsiConsole.Write(new Text("\n\n"));

                var menuGrid = new Grid().Centered();
                menuGrid.AddColumn();

                for (int i = 0; i < options.Length; i++)
                {
                    if (i == selectedIndex)
                    {
                        var selectedText = new Text($"> {options[i].ToUpper()} <", new Style(Color.White, Color.DarkCyan)).Centered();
                        var btn = new Panel(selectedText).Border(BoxBorder.Double).BorderColor(Color.Cyan1).Padding(2, 0).Expand();
                        menuGrid.AddRow(new Padder(btn).Padding(0, 0, 0, 1));
                    }
                    else
                    {
                        var unselectedText = new Text($"  {options[i]}  ", new Style(Color.Grey50)).Centered();
                        var btn = new Panel(unselectedText).Border(BoxBorder.Square).BorderColor(Color.Grey30).Padding(2, 0).Expand();
                        menuGrid.AddRow(new Padder(btn).Padding(0, 0, 0, 1));
                    }
                }

                AnsiConsole.Write(new Align(new Panel(menuGrid).Border(BoxBorder.None), HorizontalAlignment.Center));

                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                ConsoleKey key = keyInfo.Key;

                if (key == ConsoleKey.UpArrow || key == ConsoleKey.W)
                {
                    selectedIndex--;
                    if (selectedIndex < 0) selectedIndex = options.Length - 1;
                }
                else if (key == ConsoleKey.DownArrow || key == ConsoleKey.S)
                {
                    selectedIndex++;
                    if (selectedIndex >= options.Length) selectedIndex = 0;
                }
                else if (key == ConsoleKey.Enter)
                {
                    return options[selectedIndex];
                }
            }
        }

        // Main gameloop 
        // TODO: optimize the console to prevent flickering // DONE
        static void RunGame()
        {
            // fake booting (for design purpose haha)
            AnsiConsole.Status().Start("Booting Kernel...", ctx => { Thread.Sleep(500); });

            // initialize the board and load the starting positions of pieces
            Board board = new Board();
            const string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            board.LoadPosition(StartFEN);

            string logs = "Game Started.\n";
            string inputBuffer = "";

            bool gameRunning = true;
            while (gameRunning)
            {
                float currentScore = board.Evaluate();

                // engine's turn to do a move
                if (!board.IsWhiteToMove)
                {
                    // engine clones the board so it doesnt mess with the real time ui when looking up potential moves
                    Board aiBoard = board.Clone(); // Declare aiBoard here!

                    // engine runs in the bg so it doesnt freeze the program 
                    Task<string> botTask = Task.Run(() => // FIX: Task.Run, not TaskRun
                    {
                        try
                        {
                            return StampedeChess.Core.AI.GetBestMove(aiBoard); // Use aiBoard variable
                        }
                        catch (Exception ex)
                        {
                            return "ERROR: " + ex.Message;
                        }
                    });

                    string botInput = botTask.Result;

                    // handling of engine's inputs
                    if (botInput.StartsWith("ERROR"))
                    {
                        logs = $"System Failure: {botInput}\n" + logs;
                        board.IsWhiteToMove = true; // skips turn to prevent infinite loop
                    }
                    else if (botInput == "resign")
                    {
                        ShowGameOverScreen("Bot Resigned", true);
                        gameRunning = false;
                    }
                    else
                    {
                        // execute the move on the chessboard
                        string error;
                        string botResult = board.MakeMove(botInput, out error);

                        if (botResult != null)
                        {
                            logs = $"Bot: {botResult}\n" + logs;
                            if (error == "GAME OVER")
                            {
                                // bot caused game over -> player loses (false)
                                ShowGameOverScreen(botResult, false);
                                gameRunning = false;
                            }
                        }
                    }
                    continue; // restart loop to render the new board state
                }

                // user turn to play
                var visualBoard = Renderer.GetVisualBoard(board);
                LayoutManager.DrawFullScreen(visualBoard, logs, inputBuffer + "_", currentScore);

                // handle user input
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    if (!string.IsNullOrWhiteSpace(inputBuffer))
                    {
                        string command = inputBuffer.ToLower().Trim();

                        // commands available
                        if (command == "resign")
                        {
                            ShowGameOverScreen("Resigned", false);
                            gameRunning = false;
                            continue; // Important: prevent falling through
                        }
                        if (command == "restart")
                        {
                            board.LoadPosition(StartFEN);
                            logs = "Game Reset.\n";
                            inputBuffer = "";
                            continue;
                        }

                        // execute move on chessboard
                        string errorMsg;
                        string makeMoveResult = board.MakeMove(inputBuffer, out errorMsg);

                        if (makeMoveResult != null)
                        {
                            logs = $"You: {makeMoveResult}\n" + logs;
                            inputBuffer = "";

                            if (errorMsg == "GAME OVER")
                            {
                                // user caused game over = player wins (true)
                                ShowGameOverScreen(makeMoveResult, true);
                                gameRunning = false;
                            }
                        }
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (inputBuffer.Length > 0) inputBuffer = inputBuffer.Substring(0, inputBuffer.Length - 1);
                }
                else
                {
                    if (!char.IsControl(keyInfo.KeyChar)) inputBuffer += keyInfo.KeyChar;
                }
            }
        }

        static void ShowGameOverScreen(string result, bool isPlayerWin)
        {
            // check the text and color based on winner
            string title = isPlayerWin ? "VICTORY" : "DEFEAT";
            var titleColor = isPlayerWin ? Color.Gold1 : Color.Red;

            string message = isPlayerWin
                ? "CONGRATULATIONS! YOU WON!"
                : "THE ENGINE IS VICTORIOUS.";

            AnsiConsole.Clear();

            // draw the big title
            AnsiConsole.Write(new FigletText(title).Color(titleColor).Centered());

            // draw the details
            AnsiConsole.Write(new Markup($"\n[bold white]Final Move: {result}[/]").Centered());
            AnsiConsole.Write(new Markup($"\n[bold {titleColor}]{message}[/]").Centered());

            AnsiConsole.Write(new Markup("\n[grey]Press ANY KEY to return to the Main Menu...[/]").Centered());

            Console.ReadKey(true);
        }

        static void RunOptions()
        {
            Console.Clear();
            Console.WriteLine("\n   [ OPTIONS ]");
            Console.WriteLine("   1. Search Depth: 3 (Fixed)");
            Console.WriteLine("   2. Play as: White");
            Console.WriteLine("\n   Press any key to return...");
            Console.ReadKey(true);
        }
    }
}