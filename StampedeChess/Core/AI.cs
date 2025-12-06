using System;
using System.Collections.Generic;

namespace StampedeChess.Core
{
    public static class AI
    {
        private const int SearchDepth = 3;

        public static string GetBestMove(Board board)
        {
            int bestVal = board.IsWhiteToMove ? int.MinValue : int.MaxValue;
            (int From, int To) bestMove = (-1, -1);

            var moves = board.GetAllLegalMoves();
            moves = OrderMoves(board, moves);

            foreach (var move in moves)
            {
                // do: now captures old castling rights too
                var undoInfo = board.MakeMoveFast(move.From, move.To);

                if (undoInfo.moved == -1) continue;

                // recurse
                int moveVal = Minimax(board, SearchDepth - 1, int.MinValue, int.MaxValue, !board.IsWhiteToMove);

                // undo: pass the rights back
                board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved, undoInfo.oldRights);

                // compare
                if (board.IsWhiteToMove)
                {
                    if (moveVal > bestVal) { bestVal = moveVal; bestMove = move; }
                }
                else
                {
                    if (moveVal < bestVal) { bestVal = moveVal; bestMove = move; }
                }
            }

            if (bestMove.From == -1) return "resign";
            return board.IndexToString(bestMove.From) + board.IndexToString(bestMove.To);
        }

        private static int Minimax(Board board, int depth, int alpha, int beta, bool isMaximizing)
        {
            if (depth == 0) return (int)(board.Evaluate() * 100);

            var moves = board.GetAllLegalMoves();
            if (moves.Count == 0) return isMaximizing ? -10000 : 10000;

            if (depth > 1) moves = OrderMoves(board, moves);

            if (isMaximizing)
            {
                int maxEval = int.MinValue;
                foreach (var move in moves)
                {
                    var undoInfo = board.MakeMoveFast(move.From, move.To);
                    if (undoInfo.moved == -1) continue;

                    int eval = Minimax(board, depth - 1, alpha, beta, false);

                    board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved, undoInfo.oldRights);

                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha) break;
                }
                return maxEval;
            }
            else
            {
                int minEval = int.MaxValue;
                foreach (var move in moves)
                {
                    var undoInfo = board.MakeMoveFast(move.From, move.To);
                    if (undoInfo.moved == -1) continue;

                    int eval = Minimax(board, depth - 1, alpha, beta, true);

                    board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved, undoInfo.oldRights);

                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha) break;
                }
                return minEval;
            }
        }

        private static List<(int From, int To)> OrderMoves(Board board, List<(int From, int To)> moves)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = 0;
                int targetA = board.GetPieceAtSquare(a.To);
                if (targetA != -1) scoreA = 10;

                int scoreB = 0;
                int targetB = board.GetPieceAtSquare(b.To);
                if (targetB != -1) scoreB = 10;

                return scoreB.CompareTo(scoreA);
            });

            return moves;
        }
    }
}