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

            // Regular search for the root moves
            var moves = board.GetAllLegalMoves(capturesOnly: false);
            moves = OrderMoves(board, moves);

            foreach (var move in moves)
            {
                var undoInfo = board.MakeMoveFast(move.From, move.To);
                if (undoInfo.moved == -1) continue;

                // We start the recursive Minimax
                int moveVal = Minimax(board, SearchDepth - 1, int.MinValue, int.MaxValue, !board.IsWhiteToMove);

                board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved, undoInfo.oldRights, undoInfo.oldEP);

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
            // BASE CASE: If depth is 0, enter Quiescence Search instead of raw Evaluate()
            if (depth == 0)
                return QuiescenceSearch(board, alpha, beta, isMaximizing);

            var moves = board.GetAllLegalMoves(capturesOnly: false);

            // Checkmate / Stalemate detection
            if (moves.Count == 0)
            {
                if (board.IsCheckmate()) return isMaximizing ? -100000 + depth : 100000 - depth; // Prefer faster mates
                return 0; // Stalemate
            }

            if (depth > 1) moves = OrderMoves(board, moves);

            if (isMaximizing)
            {
                int maxEval = int.MinValue;
                foreach (var move in moves)
                {
                    var undoInfo = board.MakeMoveFast(move.From, move.To);
                    int eval = Minimax(board, depth - 1, alpha, beta, false);
                    board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved, undoInfo.oldRights, undoInfo.oldEP);

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
                    int eval = Minimax(board, depth - 1, alpha, beta, true);
                    board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved, undoInfo.oldRights, undoInfo.oldEP);

                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha) break;
                }
                return minEval;
            }
        }

        // quiescence search added on dec 12, 10:45 PM by carlos :)
        private static int QuiescenceSearch(Board board, int alpha, int beta, bool isMaximizing)
        {
            // stand_pat just means if i am already winning by a lot then i don't need to capture.
            int stand_pat = (int)(board.Evaluate() * 100);

            if (isMaximizing)
            {
                if (stand_pat >= beta) return beta;
                if (stand_pat > alpha) alpha = stand_pat;
            }
            else
            {
                if (stand_pat <= alpha) return alpha;
                if (stand_pat < beta) beta = stand_pat;
            }

            // only consider captures in quiescence search
            var moves = board.GetAllLegalMoves(capturesOnly: true);
            moves = OrderMoves(board, moves);

            if (isMaximizing)
            {
                foreach (var move in moves)
                {
                    var undoInfo = board.MakeMoveFast(move.From, move.To);
                    int eval = QuiescenceSearch(board, alpha, beta, false);
                    board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved, undoInfo.oldRights, undoInfo.oldEP);

                    if (eval >= beta) return beta;
                    if (eval > alpha) alpha = eval;
                }
                return alpha;
            }
            else
            {
                foreach (var move in moves)
                {
                    var undoInfo = board.MakeMoveFast(move.From, move.To);
                    int eval = QuiescenceSearch(board, alpha, beta, true);
                    board.UnmakeMoveFast(move.From, move.To, undoInfo.captured, undoInfo.moved, undoInfo.oldRights, undoInfo.oldEP);

                    if (eval <= alpha) return alpha;
                    if (eval < beta) beta = eval;
                }
                return beta;
            }
        }

        private static List<(int From, int To)> OrderMoves(Board board, List<(int From, int To)> moves)
        {
            // MVV-LVA (Most Valuable Victim - Least Valuable Aggressor) 
            // these methods or algorithms are researched carefully using real sources,
            // including chess programming wiki and youtube videos by top chess engine programmers.
            moves.Sort((a, b) =>
            {
                int scoreA = 0;
                int pieceA = board.GetPieceAtSquare(a.From) % 6; // 0-5 type
                int targetA = board.GetPieceAtSquare(a.To);

                if (targetA != -1)
                {
                    int victimVal = GetPieceValue(targetA % 6);
                    int aggressorVal = GetPieceValue(pieceA);
                    scoreA = 10 * victimVal - aggressorVal;
                }

                int scoreB = 0;
                int pieceB = board.GetPieceAtSquare(b.From) % 6;
                int targetB = board.GetPieceAtSquare(b.To);

                if (targetB != -1)
                {
                    int victimVal = GetPieceValue(targetB % 6);
                    int aggressorVal = GetPieceValue(pieceB);
                    scoreB = 10 * victimVal - aggressorVal;
                }

                return scoreB.CompareTo(scoreA); // descending sort
            });

            return moves;
        }

        private static int GetPieceValue(int pieceType)
        {
            switch (pieceType)
            {
                case 0: return 100; // Pawn
                case 1: return 300; // Knight
                case 2: return 310; // Bishop
                case 3: return 500; // Rook
                case 4: return 900; // Queen
                case 5: return 20000; // King
                default: return 0;
            }
        }
    }
}