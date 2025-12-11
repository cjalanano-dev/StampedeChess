using Spectre.Console;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StampedeChess.Core;
using StampedeChess.UI;

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
                    case "Start": RunGame(); break;
                    case "Exit": appRunning = false; break;
                }
            }
            Console.ResetColor();
            Console.Clear();
            Console.WriteLine("System Shutdown Complete.");
        }

        static string RunMenu()
        {
            string[] options = { "Start", "Exit" };
            int selectedIndex = 0;
            string titleArt = @"
███████╗████████╗ █████╗ ███╗   ███╗██████╗ ███████╗██████╗ ███████╗
██╔════╝╚══██╔══╝██╔══██╗████╗ ████║██╔══██╗██╔════╝██╔══██╗██╔════╝
███████╗   ██║   ███████║██╔████╔██║██████╔╝█████╗  ██║  ██║█████╗  
╚════██║   ██║   ██╔══██║██║╚██╔╝██║██╔═══╝ ██╔══╝  ██║  ██║██╔══╝  
███████║   ██║   ██║  ██║██║ ╚═╝ ██║██║     ███████╗██████╔╝███████╗
╚══════╝   ╚═╝   ╚═╝  ╚═╝╚═╝     ╚═╝╚═╝     ╚══════╝╚═════╝ ╚══════╝
                ";
            string[] splashes = { "Checkmate in 4!", "Written in C#!", "Console Edition!", "No Mouse Required!", "Powered by Logic!", "Heavy Metal Chess!" };
            string currentSplash = splashes[new Random().Next(splashes.Length)];

            while (true)
            {
                AnsiConsole.Clear();
                var logo = new Text(titleArt, new Style(Color.Cyan1)).Centered();
                var splashText = new Text(currentSplash, new Style(Color.Yellow, decoration: Decoration.Bold)).Centered();
                AnsiConsole.Write(new Text("\n\n\n\n"));
                AnsiConsole.Write(logo);
                AnsiConsole.Write(splashText);
                AnsiConsole.Write(new Text("\n\n\n\n"));

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
                if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.W) { selectedIndex--; if (selectedIndex < 0) selectedIndex = options.Length - 1; }
                else if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.S) { selectedIndex++; if (selectedIndex >= options.Length) selectedIndex = 0; }
                else if (keyInfo.Key == ConsoleKey.Enter) return options[selectedIndex];
            }
        }

        static void RunGame()
        {
            // fake booting sequence
            AnsiConsole.Status().Start("Booting Engine...", ctx => { Thread.Sleep(500); });

            Board board = new Board();
            const string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            board.LoadPosition(StartFEN);

            string logs = "Game Started.\n";
            string inputBuffer = "";

            bool gameRunning = true;
            while (gameRunning)
            {
                float currentScore = board.Evaluate();

                // bot turn logic
                if (!board.IsWhiteToMove)
                {
                    Board aiBoard = board.Clone();

                    // run ai in background
                    Task<string> botTask = Task.Run(() => {
                        try { return StampedeChess.Core.AI.GetBestMove(aiBoard); }
                        catch (Exception ex) { return "ERROR: " + ex.Message; }
                    });

                    string[] spinner = { "/", "-", "\\", "|" };
                    int frame = 0;

                    // animation loop
                    while (!botTask.IsCompleted)
                    {
                        var thinkingVisual = Renderer.GetVisualBoard(board);
                        string spinnerIcon = $"[cyan]{spinner[frame]}[/]";
                        LayoutManager.DrawFullScreen(thinkingVisual, logs, inputBuffer + "_", currentScore, spinnerIcon);
                        Thread.Sleep(100);
                        frame = (frame + 1) % spinner.Length;
                    }

                    string botInput = botTask.Result;

                    // handle bot output
                    if (botInput.StartsWith("ERROR"))
                    {
                        logs = $"System Failure: {botInput}\n" + logs;
                        board.IsWhiteToMove = true;
                    }
                    else if (botInput == "resign")
                    {
                        ShowGameOverScreen("Bot Resigned", true, board);
                        gameRunning = false;
                    }
                    else
                    {
                        string error;
                        string botResult = board.MakeMove(botInput, out error);
                        if (botResult != null)
                        {
                            logs = $"Bot: {botResult}\n" + logs;

                            if (error == "GAME OVER")
                            {
                                ShowGameOverScreen(botResult, false, board);
                                gameRunning = false;
                            }
                        }
                        else
                        {
                            logs = $"Bot Error: {error}\n" + logs;
                        }
                    }
                    continue;
                }

                // user turn logic
                var visualBoard = Renderer.GetVisualBoard(board);
                LayoutManager.DrawFullScreen(visualBoard, logs, inputBuffer + "_", currentScore);

                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    if (!string.IsNullOrWhiteSpace(inputBuffer))
                    {
                        string command = inputBuffer.ToLower().Trim();

                        // system commands
                        if (command == "resign") { ShowGameOverScreen("Resigned", false, board); gameRunning = false; continue; }
                        if (command == "restart") { board.LoadPosition(StartFEN); logs = "Game Reset.\n"; inputBuffer = ""; continue; }

                        // execute move
                        string errorMsg;
                        string makeMoveResult = board.MakeMove(inputBuffer, out errorMsg);

                        if (makeMoveResult != null)
                        {
                            logs = $"You: {makeMoveResult}\n" + logs;
                            inputBuffer = "";

                            if (errorMsg == "GAME OVER")
                            {
                                ShowGameOverScreen(makeMoveResult, true, board);
                                gameRunning = false;
                            }
                        }
                        else
                        {
                            logs = $"Error: {errorMsg}\n" + logs;
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

        static void ShowGameOverScreen(string result, bool isPlayerWin, Board board)
        {
            string title = isPlayerWin ? "VICTORY" : "DEFEAT";
            var titleColor = isPlayerWin ? Color.Gold1 : Color.Red;
            string subText = isPlayerWin
                ? "[bold gold1]CHECKMATE! YOU WON![/]"
                : "[bold red]CHECKMATE! ENGINE WINS![/]";

            // results layout
            var layout = new Layout("GameOver")
                .SplitColumns(
                    new Layout("Left").Ratio(3),
                    new Layout("Right").Ratio(2)
                );

            layout["Left"].SplitRows(
                new Layout("FinalBoard").Ratio(2),
                new Layout("History").Ratio(1)
            );

            // final board state
            var visualBoard = Renderer.GetVisualBoard(board);
            var boardPanel = new Panel(Align.Center(visualBoard, VerticalAlignment.Middle))
                .Header("Final Position", Justify.Center)
                .Border(BoxBorder.Heavy)
                .Expand();

            // match history
            var historyPanel = Renderer.GetMoveHistoryPanel(board);

            // result message
            var messageGrid = new Grid().Centered();
            messageGrid.AddColumn();
            messageGrid.AddRow(new FigletText(title).Color(titleColor).Centered());
            messageGrid.AddRow(new Markup($"\n{subText}"));
            messageGrid.AddRow(new Markup($"[white]Final Move: {result}[/]"));
            messageGrid.AddRow(new Text("\n\n"));
            messageGrid.AddRow(new Markup("[grey]Press ANY KEY to return to Menu...[/]"));

            var messagePanel = new Panel(Align.Center(messageGrid, VerticalAlignment.Middle))
                .Header("Game Over")
                .Border(BoxBorder.Rounded)
                .Expand();

            // updates
            layout["FinalBoard"].Update(boardPanel);
            layout["History"].Update(historyPanel);
            layout["Right"].Update(messagePanel);

            AnsiConsole.Clear();
            AnsiConsole.Write(layout);

            Console.ReadKey(true);
        }

        // this is a placeholder for future options menu for settings
        //static void RunOptions()
        //{
        //    Console.Clear();
        //    Console.WriteLine("\n   [ OPTIONS ]");
        //    Console.WriteLine("   1. Search Depth: 3 (Fixed)");
        //    Console.WriteLine("   2. Play as: White");
        //    Console.WriteLine("\n   Press any key to return...");
        //    Console.ReadKey(true);
        //}
    }
}