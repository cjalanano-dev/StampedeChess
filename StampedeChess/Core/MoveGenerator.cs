using StampedeChess.Core;
using System;
using System.Collections.Generic;

namespace StampedeChess.Core
{
    public static class MoveGenerator
    {
        public static ulong GetPseudoLegalMoves(int square, int pieceType, Board board)
        {
            ulong moves = 0;
            ulong ownPieces = board.IsWhiteToMove ? board.GetWhitePieces() : board.GetBlackPieces();
            ulong allPieces = board.GetAllPieces();
            ulong enemyPieces = board.IsWhiteToMove ? board.GetBlackPieces() : board.GetWhitePieces();

            // knights
            if (pieceType == Piece.WhiteKnight || pieceType == Piece.BlackKnight)
            {
                moves = MoveTables.KnightAttacks[square];
                moves &= ~ownPieces;
            }
            // kings (including castling)
            else if (pieceType == Piece.WhiteKing || pieceType == Piece.BlackKing)
            {
                moves = MoveTables.KingAttacks[square];
                moves &= ~ownPieces;

                // castling logic
                // verify: 1. right exists 2. path empty 3. squares safe (cannot move through check)
                if (board.IsWhiteToMove)
                {
                    // white king side (e1 -> g1)
                    // check mask 1 (WK), square e1(4), empty f1(5) g1(6)
                    if ((board.CastlingRights & 1) != 0 && square == 4)
                    {
                        if ((allPieces & ((1UL << 5) | (1UL << 6))) == 0)
                        {
                            // safety check: e1, f1, g1 must not be attacked
                            if (!board.IsSquareAttacked(4, false) && !board.IsSquareAttacked(5, false))
                                moves |= (1UL << 6);
                        }
                    }
                    // white queen side (e1 -> c1)
                    if ((board.CastlingRights & 2) != 0 && square == 4)
                    {
                        if ((allPieces & ((1UL << 3) | (1UL << 2) | (1UL << 1))) == 0)
                        {
                            if (!board.IsSquareAttacked(4, false) && !board.IsSquareAttacked(3, false))
                                moves |= (1UL << 2);
                        }
                    }
                }
                else
                {
                    // black king side (e8 -> g8)
                    if ((board.CastlingRights & 4) != 0 && square == 60)
                    {
                        if ((allPieces & ((1UL << 61) | (1UL << 62))) == 0)
                        {
                            if (!board.IsSquareAttacked(60, true) && !board.IsSquareAttacked(61, true))
                                moves |= (1UL << 62);
                        }
                    }
                    // black queen side (e8 -> c8)
                    if ((board.CastlingRights & 8) != 0 && square == 60)
                    {
                        if ((allPieces & ((1UL << 59) | (1UL << 58) | (1UL << 57))) == 0)
                        {
                            if (!board.IsSquareAttacked(60, true) && !board.IsSquareAttacked(59, true))
                                moves |= (1UL << 58);
                        }
                    }
                }
            }
            // sliding pieces
            else if (IsSlidingPiece(pieceType))
            {
                moves = GenerateSlidingMoves(square, pieceType, board);
                moves &= ~ownPieces;
            }
            // pawns
            else
            {
                if (board.IsWhiteToMove)
                {
                    // single push
                    int up1 = square + 8;
                    if (up1 < 64 && ((allPieces & (1UL << up1)) == 0))
                    {
                        moves |= (1UL << up1);
                        // double push
                        if (square >= 8 && square <= 15)
                        {
                            int up2 = square + 16;
                            if ((allPieces & (1UL << up2)) == 0) moves |= (1UL << up2);
                        }
                    }
                    // captures
                    if (square % 8 != 0)
                    {
                        int capLeft = square + 7;
                        if ((enemyPieces & (1UL << capLeft)) != 0) moves |= (1UL << capLeft);
                    }
                    if (square % 8 != 7)
                    {
                        int capRight = square + 9;
                        if ((enemyPieces & (1UL << capRight)) != 0) moves |= (1UL << capRight);
                    }
                }
                else
                {
                    // single push
                    int down1 = square - 8;
                    if (down1 >= 0 && ((allPieces & (1UL << down1)) == 0))
                    {
                        moves |= (1UL << down1);
                        // double push
                        if (square >= 48 && square <= 55)
                        {
                            int down2 = square - 16;
                            if ((allPieces & (1UL << down2)) == 0) moves |= (1UL << down2);
                        }
                    }
                    // captures
                    if (square % 8 != 7)
                    {
                        int capRight = square - 7;
                        if ((enemyPieces & (1UL << capRight)) != 0) moves |= (1UL << capRight);
                    }
                    if (square % 8 != 0)
                    {
                        int capLeft = square - 9;
                        if ((enemyPieces & (1UL << capLeft)) != 0) moves |= (1UL << capLeft);
                    }
                }
            }

            return moves;
        }

        private static bool IsSlidingPiece(int p)
        {
            return p == Piece.WhiteRook || p == Piece.BlackRook ||
                   p == Piece.WhiteBishop || p == Piece.BlackBishop ||
                   p == Piece.WhiteQueen || p == Piece.BlackQueen;
        }

        public static ulong GenerateSlidingMoves(int square, int piece, Board board)
        {
            ulong attacks = 0;
            int[] directions = GetDirections(piece);
            ulong allPieces = board.GetAllPieces();

            foreach (int offset in directions)
            {
                int current = square;
                while (true)
                {
                    if (IsEdge(current, offset)) break;
                    current += offset;
                    attacks |= (1UL << current);
                    if (((1UL << current) & allPieces) != 0) break;
                }
            }
            return attacks;
        }

        private static int[] GetDirections(int piece)
        {
            bool isRook = (piece == Piece.WhiteRook || piece == Piece.BlackRook);
            bool isBishop = (piece == Piece.WhiteBishop || piece == Piece.BlackBishop);

            if (isRook) return new[] { 8, -8, 1, -1 };
            if (isBishop) return new[] { 9, 7, -9, -7 };
            return new[] { 8, -8, 1, -1, 9, 7, -9, -7 };
        }

        private static bool IsEdge(int square, int offset)
        {
            if (offset == 1 || offset == -7 || offset == 9)
                if (square % 8 == 7) return true;

            if (offset == -1 || offset == 7 || offset == -9)
                if (square % 8 == 0) return true;

            if (offset > 0 && square >= 56) return true;
            if (offset < 0 && square <= 7) return true;

            return false;
        }
    }
}