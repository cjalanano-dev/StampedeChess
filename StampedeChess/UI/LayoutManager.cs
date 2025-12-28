using Spectre.Console;
using Spectre.Console.Rendering;
using System;

namespace StampedeChess.UI
{
    public class LayoutManager
    {
        public static void DrawFullScreen(Table boardVisual, string logText, string currentInput, float score, string statusIcon = "")
        {
            var rootLayout = new Layout("Root")
                .SplitColumns(
                    new Layout("LeftGroup").Ratio(3),
                    new Layout("RightGroup").Ratio(1)
                );

            rootLayout["LeftGroup"].SplitColumns(
                new Layout("EvalBar").Ratio(1),
                new Layout("Board").Ratio(8)
            );

            rootLayout["RightGroup"].SplitRows(
                new Layout("Info").Ratio(2),
                new Layout("Input").Ratio(1),
                new Layout("Logs").Ratio(6),
                new Layout("Controls").Ratio(2)
            );

            // contents
            var evalPanel = Renderer.GetEvaluationBar(score);

            string headerText = string.IsNullOrEmpty(statusIcon) ? "Stampede" : $"{statusIcon} Stampede {statusIcon}";
            var boardPanel = new Panel(Align.Center(boardVisual, VerticalAlignment.Middle))
                .Header(headerText, Justify.Center)
                .Border(BoxBorder.Heavy)
                .Expand();

            string scoreColor = score > 0 ? "green" : (score < 0 ? "red" : "white");
            string scoreText = score > 0 ? $"+{score:0.0}" : $"{score:0.0}";

            var infoPanel = new Panel(
                    new Markup($"[bold yellow]Turn:[/] White\n[bold cyan]State:[/] Playing\n[bold {scoreColor}]Eval:[/] {scoreText}"))
                .Header("Status")
                .Border(BoxBorder.Rounded)
                .Expand();

            var inputPanel = new Panel(new Markup($"[bold white] > {currentInput}[/]"))
                .Header("Command Input")
                .Border(BoxBorder.Square)
                .BorderColor(Color.Cyan1)
                .Expand();

            var logPanel = new Panel(new Text(logText))
                .Header("Engine Logs")
                .Border(BoxBorder.Ascii)
                .Expand();

            var commandsPanel = new Panel(
                new Markup("[bold white]Available Commands:[/]\n• [red]resign[/]   (Surrender)\n• [cyan]restart[/]  (Reset)"))
                .Header("System Commands")
                .Border(BoxBorder.Rounded)
                .Expand();

            // updates
            rootLayout["EvalBar"].Update(Align.Center(evalPanel, VerticalAlignment.Middle));
            rootLayout["Board"].Update(boardPanel);
            rootLayout["Info"].Update(infoPanel);
            rootLayout["Input"].Update(inputPanel);
            rootLayout["Logs"].Update(logPanel);
            rootLayout["Controls"].Update(commandsPanel);

            try
            {
                Console.SetCursorPosition(0, 0);
            } catch
            {
                AnsiConsole.Clear();
            }
            AnsiConsole.Write(rootLayout);
        }
    }
}