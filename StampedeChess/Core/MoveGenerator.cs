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
                moves &= ~ownPieces; // exclude own pieces
            }
            // kings
            else if (pieceType == Piece.WhiteKing || pieceType == Piece.BlackKing)
            {
                moves = MoveTables.KingAttacks[square];
                moves &= ~ownPieces;
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
                if (board.IsWhiteToMove) // white pawns
                {
                    // single push
                    int up1 = square + 8;
                    // empty square check
                    if (up1 < 64 && ((allPieces & (1UL << up1)) == 0))
                    {
                        moves |= (1UL << up1);

                        // double push
                        // rank 2 check
                        if (square >= 8 && square <= 15)
                        {
                            int up2 = square + 16;
                            if ((allPieces & (1UL << up2)) == 0)
                            {
                                moves |= (1UL << up2);
                            }
                        }
                    }

                    // captures
                    // capture left
                    if (square % 8 != 0)
                    {
                        int capLeft = square + 7;
                        if ((enemyPieces & (1UL << capLeft)) != 0)
                            moves |= (1UL << capLeft);
                    }

                    // capture right
                    if (square % 8 != 7)
                    {
                        int capRight = square + 9;
                        if ((enemyPieces & (1UL << capRight)) != 0)
                            moves |= (1UL << capRight);
                    }
                }
                else // black pawns
                {
                    // single push
                    int down1 = square - 8;
                    if (down1 >= 0 && ((allPieces & (1UL << down1)) == 0))
                    {
                        moves |= (1UL << down1);

                        // double push
                        // rank 7 check
                        if (square >= 48 && square <= 55)
                        {
                            int down2 = square - 16;
                            if ((allPieces & (1UL << down2)) == 0)
                            {
                                moves |= (1UL << down2);
                            }
                        }
                    }

                    // captures
                    // capture right
                    if (square % 8 != 7)
                    {
                        int capRight = square - 7;
                        if ((enemyPieces & (1UL << capRight)) != 0)
                            moves |= (1UL << capRight);
                    }

                    // capture left
                    if (square % 8 != 0)
                    {
                        int capLeft = square - 9;
                        if ((enemyPieces & (1UL << capLeft)) != 0)
                            moves |= (1UL << capLeft);
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

                    // add square
                    attacks |= (1UL << current);

                    // stop if blocked
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
                if (square % 8 == 7) return true; // h-file boundary

            if (offset == -1 || offset == 7 || offset == -9)
                if (square % 8 == 0) return true; // a-file boundary

            if (offset > 0 && square >= 56) return true;
            if (offset < 0 && square <= 7) return true;

            return false;
        }
    }
}