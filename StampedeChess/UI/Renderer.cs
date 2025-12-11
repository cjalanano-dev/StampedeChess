using Spectre.Console;
using Spectre.Console.Rendering;
using StampedeChess.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace StampedeChess.UI
{
    public static class Renderer
    {
        public static Table GetVisualBoard(Board board)
        {
            var table = new Table();
            table.Border(TableBorder.None);
            table.HideHeaders();
            table.Centered();

            table.AddColumn(new TableColumn("Rank").Centered().Padding(0, 0));
            for (int i = 0; i < 8; i++)
            {
                table.AddColumn(new TableColumn("File").Centered().Padding(0, 0));
            }

            // loop the rows
            for (int rank = 7; rank >= 0; rank--)
            {
                var cells = new List<IRenderable>();

                // rank labels (1-8)
                var rankLabel = new Text($"\n  {rank + 1}  \n", new Style(Color.Grey50));
                cells.Add(rankLabel);

                // loop columns
                for (int file = 0; file < 8; file++)
                {
                    int squareIndex = rank * 8 + file;
                    int piece = board.GetPieceAtSquare(squareIndex);
                    bool isLightSquare = (rank + file) % 2 != 0;

                    // colors
                    var bgColor = isLightSquare ? Color.Grey30 : Color.Grey15;
                    Color fgColor;
                    if (piece >= 0 && piece <= 5)
                    {
                        // white pieces
                        fgColor = Color.White;
                    }
                    else
                    {
                        // black/dark pieces
                        fgColor = Color.Orange1;
                    }
                    if (piece == -1) fgColor = Color.Grey23;

                    // custom ascii pieces art
                    string[] art = PieceArt.GetArt(piece);
                    string blockContent = $"{art[0]}\n{art[1]}\n{art[2]}";

                    cells.Add(new Text(blockContent, new Style(fgColor, bgColor, Decoration.Bold)));
                }

                table.AddRow(cells.ToArray());
            }

            // bottom labels on the board
            var bottomLabels = new List<IRenderable> { new Text("") };
            char[] files = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
            foreach (var f in files)
            {
                bottomLabels.Add(new Text(f.ToString(), new Style(Color.Grey50)).Centered());
            }
            table.AddRow(bottomLabels.ToArray());

            return table;
        }

        // for evaluation function
        public static Panel GetEvaluationBar(float score)
        {
            // max advantage we visualize is +/- 10 points
            float clampedScore = Math.Min(Math.Max(score, -10), 10); // clamped score ranges from -10 to +10

            // normalize to 0.0 - 1.0 range (0.5 is equal)
            // +10 -> 1.0 (while leads)
            // -10 -> 0.0 (black leads)
            // 0   -> 0.5 (50/50)
            float percentage = (clampedScore + 10) / 20.0f;

            // height of the board is roughly 24 lines (8 ranks by 3 lines per rank)
            int totalHeight = 24;
            int whiteBlocks = (int)(totalHeight * percentage);
            int blackBlocks = totalHeight - whiteBlocks;

            // build the vertical string
            // top is black, Bottom is white
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < blackBlocks; i++)
            {
                sb.Append("[orange1]█[/]\n");
            }
            for (int i = 0; i < whiteBlocks; i++)
            {
                sb.Append("[white]█[/]\n");
            }

            string barContent = sb.ToString().TrimEnd('\n');

            return new Panel(new Markup(barContent))
                .Border(BoxBorder.None) 
                .Padding(0, 0);
        }

        public static Panel GetMoveHistoryPanel(Board board)
        {
            var sb = new StringBuilder();
            var history = board.MoveHistory;    

            for (int i = 0; i < history.Count; i += 2)
            {
                int moveNumber = (i / 2) + 1;
                string whiteMove = history[i];
                string blackMove = (i + 1 < history.Count) ? history[i + 1] : "";

                sb.Append($"[grey]{moveNumber}.[/] [green]{whiteMove}[/] ");

                if (!string.IsNullOrEmpty(blackMove))
                {
                    sb.Append($"[orange1]{blackMove}[/] ");
                }

                if (moveNumber % 2 == 0) sb.Append("\n");
            }

            return new Panel(new Markup(sb.ToString()))
                .Header("Match History (PGN)")
                .Border(BoxBorder.Rounded)
                .Expand();
        }
    }
}