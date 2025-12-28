using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace StampedeChess.Core
{
    public static class AI
    {
        // settings for the bot
        private const int MaxDepth = 63;
        private const int TimeLimitMs = 2500;
        private const int NullMoveReduction = 2;

        // diagnostics stuff
        public static long NodesVisited;
        private static Stopwatch _timer;
        private static bool _abortSearch;

        // things that help sort moves
        private static int[,] _history;
        private static int[,] _killers;
        private const int MaxPly = 64;

        // simple wrapper to sort moves with their scores
        private struct MoveWrapper 
        { 
            public (int From, int To) Move; 
            public int Score; 
        }

        public static string GetBestMove(Board board)
        {
            // we do a deterministic opening book first
            // we check if we know the best theory move. if so, play it instantly.
            string bookMove = OpeningBook.GetBookMove(board);
            if (bookMove != null) return bookMove;

            // if we are out of book, we have to think.
            _timer = Stopwatch.StartNew();
            _abortSearch = false;
            NodesVisited = 0;

            // reset tables
            _history = new int[12, 64];
            _killers = new int[MaxDepth, 2];

            (int From, int To) bestMove = (-1, -1);
            int alpha = -50000;
            int beta = 50000;

            // iterative deepening loop
            // start at depth 1 and go deeper until we run out of time.
            for (int depth = 1; depth <= MaxDepth; depth++)
            {
                (int From, int To) bestMoveThisDepth = (-1, -1);
                int score = 0;

                try
                {
                    // search using negamax
                    score = Negamax(board, depth, alpha, beta, 0, allowNull: true);

                    // grab the best move from the hash table
                    if (TranspositionTable.Probe(board.CurrentHash, out var entry) && entry.BestMoveFrom != -1)
                    {
                        bestMoveThisDepth = (entry.BestMoveFrom, entry.BestMoveTo);
                    }
                }
                catch (TimeoutException)
                {
                    // time is up! stop searching.
                    break;
                }

                if (_abortSearch) break;

                // if we found a valid move, save it.
                if (bestMoveThisDepth.From != -1) bestMove = bestMoveThisDepth;

                // if we found checkmate, no need to search deeper. we already won.
                if (Math.Abs(score) > 90000) break;
            }

            _timer.Stop();

            if (bestMove.From == -1)
            {
                return "resign";
            }
            return board.IndexToString(bestMove.From) + board.IndexToString(bestMove.To);
        }

        private static int Negamax(Board board, int depth, int alpha, int beta, int ply, bool allowNull)
        {
            NodesVisited++;

            // check time every 2048 nodes so we don't lag the cpu
            if ((NodesVisited & 2047) == 0)
            {
                if (_timer.ElapsedMilliseconds > TimeLimitMs)
                {
                    _abortSearch = true;
                    throw new TimeoutException();
                }
            }

            // safety check for max depth
            if (ply >= MaxPly) return board.EvaluateInt();

            bool isRoot = (ply == 0);
            bool inCheck = board.IsSquareAttacked(board.GetKingSquare(board.IsWhiteToMove), !board.IsWhiteToMove);

            // check extension
            // if we are in check, we must search deeper to survive.
            if (inCheck) depth++;

            // transposition table probe
            // have we seen this position before? if so, use the stored score.
            int originalAlpha = alpha;
            (int From, int To) ttMove = (-1, -1);
            if (TranspositionTable.Probe(board.CurrentHash, out var ttEntry))
            {
                if (ttEntry.Depth >= depth && !isRoot)
                {
                    if (ttEntry.Flag == TranspositionTable.Exact) return ttEntry.Score;
                    if (ttEntry.Flag == TranspositionTable.LowerBound) alpha = Math.Max(alpha, ttEntry.Score);
                    if (ttEntry.Flag == TranspositionTable.UpperBound) beta = Math.Min(beta, ttEntry.Score);
                    if (alpha >= beta) return ttEntry.Score;
                }
                ttMove = (ttEntry.BestMoveFrom, ttEntry.BestMoveTo);
            }

            // quiescence search at the leaf nodes (avoids horizon effect)
            if (depth <= 0) return Quiescence(board, alpha, beta);

            // null move pruning
            // try skipping our turn. if we still have a good score, the position is winning.
            // don't do this if we are in check or only have pawns left.
            if (allowNull && !inCheck && depth >= 3 && !isRoot && HasNonPawnMaterial(board))
            {
                int storedEP = board.EnPassantTarget;
                ulong storedHash = board.CurrentHash;

                // make null move
                board.IsWhiteToMove = !board.IsWhiteToMove;
                board.EnPassantTarget = -1;
                board.CurrentHash ^= Zobrist.SideToMove;
                if (storedEP != -1)
                {
                    board.CurrentHash ^= Zobrist.EnPassantFile[storedEP % 8];
                    board.CurrentHash ^= Zobrist.EnPassantFile[8];
                }

                // search with reduced depth
                int nullScore = -Negamax(board, depth - 1 - NullMoveReduction, -beta, -beta + 1, ply + 1, false);

                // undo null move
                board.IsWhiteToMove = !board.IsWhiteToMove;
                board.EnPassantTarget = storedEP;
                board.CurrentHash = storedHash;

                if (_abortSearch) throw new TimeoutException();
                if (nullScore >= beta) return beta; // cutoff
            }

            var rawMoves = board.GetAllLegalMoves(capturesOnly: false);

            // game over detection
            if (rawMoves.Count == 0)
            {
                if (inCheck) return -100000 + ply; // checkmate (sooner is better)
                return 0; // stalemate
            }

            // sort moves to search best ones first
            List<MoveWrapper> sortedMoves = ScoreMoves(board, rawMoves, ttMove, ply);
            sortedMoves.Sort((a, b) => b.Score.CompareTo(a.Score));

            int bestScore = -1000000;
            (int From, int To) bestMoveInNode = (-1, -1);

            for (int i = 0; i < sortedMoves.Count; i++)
            {
                var move = sortedMoves[i].Move;
                var undo = board.MakeMoveFast(move.From, move.To);

                bool isCapture = (undo.captured != -1);
                bool givesCheck = board.IsSquareAttacked(board.GetKingSquare(board.IsWhiteToMove), !board.IsWhiteToMove);

                // initialize score to keep compiler happy
                int score = -50000;
                bool needsFullSearch = true;

                // lmr (late move reduction)
                // if this move is late in the list and "quiet", we search it less deep.
                if (depth >= 3 && i > 3 && !isCapture && !inCheck && !givesCheck && !isRoot)
                {
                    int reduction = 1;
                    if (depth > 6) reduction = 2; // reduce more if we are really deep

                    score = -Negamax(board, depth - 1 - reduction, -alpha - 1, -alpha, ply + 1, true);

                    // if the reduced search found something interesting, we have to search fully.
                    if (score > alpha) needsFullSearch = true;
                    else needsFullSearch = false;
                }

                if (needsFullSearch)
                {
                    // pvs (principal variation search)
                    // assume first move is best and search with full window.
                    if (i == 0)
                    {
                        score = -Negamax(board, depth - 1, -beta, -alpha, ply + 1, true);
                    }
                    else
                    {
                        // for other moves, assume they are worse (null window search)
                        score = -Negamax(board, depth - 1, -alpha - 1, -alpha, ply + 1, true);

                        // if our assumption was wrong, search again fully.
                        if (score > alpha && score < beta)
                            score = -Negamax(board, depth - 1, -beta, -alpha, ply + 1, true);
                    }
                }

                board.UnmakeMoveFast(move.From, move.To, undo.captured, undo.moved, undo.oldRights, undo.oldEP, undo.oldHash);

                if (_abortSearch) throw new TimeoutException();

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMoveInNode = move;
                    if (score > alpha)
                    {
                        alpha = score;
                        if (alpha >= beta)
                        {
                            // beta cutoff!
                            // save the killer move if it wasn't a capture
                            if (undo.captured == -1)
                            {
                                StoreKiller(ply, move);
                                UpdateHistory(board, move, depth);
                            }
                            break;
                        }
                    }
                }
            }

            // store result in transposition table
            int flag = TranspositionTable.Exact;
            if (bestScore <= originalAlpha) flag = TranspositionTable.UpperBound;
            else if (bestScore >= beta) flag = TranspositionTable.LowerBound;

            TranspositionTable.Store(board.CurrentHash, bestScore, depth, flag, bestMoveInNode.From, bestMoveInNode.To);

            return bestScore;
        }

        // checks only captures to stop the engine from hanging pieces at the horizon
        private static int Quiescence(Board board, int alpha, int beta)
        {
            NodesVisited++;

            // stand pat, means we do nothing and just evaluate the position
            int standPat = board.EvaluateInt();
            if (standPat >= beta) return beta;
            if (alpha < standPat) alpha = standPat;

            var rawMoves = board.GetAllLegalMoves(capturesOnly: true);
            var sortedMoves = ScoreMoves(board, rawMoves, (-1, -1), 0);
            sortedMoves.Sort((a, b) => b.Score.CompareTo(a.Score));

            foreach (var wrapper in sortedMoves)
            {
                var move = wrapper.Move;
                var undo = board.MakeMoveFast(move.From, move.To);

                int score = -Quiescence(board, -beta, -alpha);

                board.UnmakeMoveFast(move.From, move.To, undo.captured, undo.moved, undo.oldRights, undo.oldEP, undo.oldHash);

                if (score >= beta) return beta;
                if (score > alpha) alpha = score;
            }
            return alpha;
        }

        // give moves a score so we can sort them
        private static List<MoveWrapper> ScoreMoves(Board board, List<(int From, int To)> rawMoves, (int From, int To) ttBest, int ply)
        {
            var list = new List<MoveWrapper>(rawMoves.Count);
            int killer1 = _killers[ply, 0];
            int killer2 = _killers[ply, 1];

            foreach (var move in rawMoves)
            {
                int score = 0;
                int piece = board.GetPieceAtSquare(move.From);
                int capture = board.GetPieceAtSquare(move.To);

                if (move == ttBest) score = 2000000; // tt move is always first
                else if (capture != -1)
                {
                    // mvv-lva (most valuable victim - least valuable aggressor)
                    int victimVal = GetPieceValue(capture % 6);
                    int attackerVal = GetPieceValue(piece % 6);
                    score = 1000000 + (victimVal * 10) - attackerVal;
                }
                else
                {
                    // check killer moves and history
                    int packed = PackMove(move);
                    if (packed == killer1) score = 900000;
                    else if (packed == killer2) score = 800000;
                    else score = _history[piece, move.To];
                }
                list.Add(new MoveWrapper { Move = move, Score = score });
            }
            return list;
        }

        // save the move that caused a beta cutoff
        private static void StoreKiller(int ply, (int From, int To) move)
        {
            if (ply >= MaxDepth) return;
            int packed = PackMove(move);
            if (_killers[ply, 0] != packed)
            {
                _killers[ply, 1] = _killers[ply, 0];
                _killers[ply, 0] = packed;
            }
        }

        // give bonus to moves that are good in general
        private static void UpdateHistory(Board board, (int From, int To) move, int depth)
        {
            int piece = board.GetPieceAtSquare(move.From);
            if (_history[piece, move.To] < 100000)
                _history[piece, move.To] += depth * depth;
        }

        private static int PackMove((int From, int To) move) => (move.From << 6) | move.To;

        private static int GetPieceValue(int pieceType)
        {
            switch (pieceType)
            {
                case 0: return 100;
                case 1: return 300;
                case 2: return 310;
                case 3: return 500;
                case 4: return 900;
                case 5: return 20000;
                default: return 0;
            }
        }

        // we flip the score here so negamax works for both sides
        private static int EvaluateInt(this Board board)
        {
            int score = (int)(board.Evaluate() * 100);
            return board.IsWhiteToMove ? score : -score;
        }

        // helper to check if we can do null move
        private static bool HasNonPawnMaterial(Board board)
        {
            ulong myPieces = board.IsWhiteToMove ? board.GetWhitePieces() : board.GetBlackPieces();
            ulong myPawns = board.IsWhiteToMove ? board.Bitboards[0] : board.Bitboards[6];
            ulong myKing = board.IsWhiteToMove ? board.Bitboards[5] : board.Bitboards[11];
            return (myPieces ^ myPawns ^ myKing) != 0;
        }

        // --- deterministic opening book ---
        // we just hardcode the best theory moves so we don't blunder the opening
        private static class OpeningBook
        {
            public static string GetBookMove(Board board)
            {
                if (board.MoveHistory.Count > 10) return null; // out of book

                // clean up the history string  
                string line = string.Join(" ", board.MoveHistory.Select(m => Clean(m))).Trim();

                // white (start) - always e4
                if (string.IsNullOrEmpty(line)) return "e2e4";

                // black response to e4 - always sicilian (best stats)
                if (line == "e2e4") return "c7c5";

                // black response to d4 - always d5 (solid)
                if (line == "d2d4") return "d7d5";

                // white response to sicilian - open sicilian
                if (line == "e2e4 c7c5") return "g1f3";

                // white response to queen's gambit - c4
                if (line == "d2d4 d7d5") return "c2c4";

                return null;
            }

            private static string Clean(string notation)
            {
                // strips special chars so "e2e4x" matches "e2e4"
                return new string(notation.Where(c => char.IsLetterOrDigit(c)).ToArray()).Substring(0, 4);
            }
        }
    }
}