using System;
using System.Collections.Generic;

namespace StampedeChess.Core
{
    public static class AI
    {
        // depth 3 is the sweet spot. 
        // it sees ~30k-100k positions per move. 
        // with make/unmake optimization, this should take < 1 second.
        private const int SearchDepth = 3;

        public static string GetBestMove(Board board)
        {
            int bestVal = board.IsWhiteToMove ? int.MinValue : int.MaxValue;
            (int From, int To) bestMove = (-1, -1);

            var moves = board.GetAllLegalMoves();

            // optimization: sort moves (captures first)
            moves = OrderMoves(board, moves);

            foreach (var move in moves)
            {
                // 1. do: make the move and capture the undo info
                var undoInfo = board.MakeMoveFast(move.From, move.To);

                // safety: if move failed (moving air), skip
                if (undoInfo.moved == -1) continue;

                // 2. recurse
                int moveVal = Minimax(board, SearchDepth - 1, int.MinValue, int.MaxValue, !board.IsWhiteToMove);

                // 3. undo: pass both pieces back to restore state
                board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved);

                // 4. compare
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
            // base case: eval
            if (depth == 0) return (int)(board.Evaluate() * 100);

            var moves = board.GetAllLegalMoves();

            // checkmate / stalemate detection
            if (moves.Count == 0) return isMaximizing ? -10000 : 10000;

            // sort moves for speed boost (only at higher depths)
            if (depth > 1) moves = OrderMoves(board, moves);

            if (isMaximizing)
            {
                int maxEval = int.MinValue;
                foreach (var move in moves)
                {
                    // do
                    var undoInfo = board.MakeMoveFast(move.From, move.To);
                    if (undoInfo.moved == -1) continue;

                    // recurse
                    int eval = Minimax(board, depth - 1, alpha, beta, false);

                    // undo
                    board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved);

                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha) break; // pruning
                }
                return maxEval;
            }
            else
            {
                int minEval = int.MaxValue;
                foreach (var move in moves)
                {
                    // do
                    var undoInfo = board.MakeMoveFast(move.From, move.To);
                    if (undoInfo.moved == -1) continue;

                    // recurse
                    int eval = Minimax(board, depth - 1, alpha, beta, true);

                    // undo
                    board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved);

                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha) break; // pruning
                }
                return minEval;
            }
        }

        private static List<(int From, int To)> OrderMoves(Board board, List<(int From, int To)> moves)
        {
            // optimization: in-place sort (no linq) to reduce garbage collection
            moves.Sort((a, b) =>
            {
                int scoreA = 0;
                int targetA = board.GetPieceAtSquare(a.To);
                if (targetA != -1) scoreA = 10; // prioritize captures

                int scoreB = 0;
                int targetB = board.GetPieceAtSquare(b.To);
                if (targetB != -1) scoreB = 10;

                // descending sort
                return scoreB.CompareTo(scoreA);
            });

            return moves;
        }
    }
}