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
            // Ensure Board.cs has GetWhitePieces(), GetBlackPieces(), GetAllPieces() as public methods
            ulong ownPieces = board.IsWhiteToMove ? board.GetWhitePieces() : board.GetBlackPieces();
            ulong allPieces = board.GetAllPieces();
            ulong enemyPieces = board.IsWhiteToMove ? board.GetBlackPieces() : board.GetWhitePieces();

            // 1. Knights (Jumpers)
            // Note: Assuming Piece class exists. If not, replace with: if (pieceType == 1 || pieceType == 7)
            if (pieceType == Piece.WhiteKnight || pieceType == Piece.BlackKnight)
            {
                moves = MoveTables.KnightAttacks[square];
                moves &= ~ownPieces; // Can't land on own pieces
            }
            // 2. Kings (One step)
            else if (pieceType == Piece.WhiteKing || pieceType == Piece.BlackKing)
            {
                moves = MoveTables.KingAttacks[square];
                moves &= ~ownPieces;
            }
            // 3. Sliding Pieces (Rooks, Bishops, Queens)
            else if (IsSlidingPiece(pieceType))
            {
                moves = GenerateSlidingMoves(square, pieceType, board);
                moves &= ~ownPieces;
            }
            // 4. PAWNS (Complex Physics)
            else
            {
                if (board.IsWhiteToMove) // --- WHITE PAWNS ---
                {
                    // A. Single Push (North +8)
                    int up1 = square + 8;
                    // Can only move forward if square is EMPTY
                    if (up1 < 64 && ((allPieces & (1UL << up1)) == 0))
                    {
                        moves |= (1UL << up1);

                        // B. Double Push (North +16)
                        // Only allowed if on Rank 2 (Index 8-15) AND path is clear
                        if (square >= 8 && square <= 15)
                        {
                            int up2 = square + 16;
                            if ((allPieces & (1UL << up2)) == 0)
                            {
                                moves |= (1UL << up2);
                            }
                        }
                    }

                    // C. Captures (Diagonals +7 and +9)
                    // Capture Left (NorthWest +7) - Not allowed from Column A
                    if (square % 8 != 0)
                    {
                        int capLeft = square + 7;
                        if ((enemyPieces & (1UL << capLeft)) != 0)
                            moves |= (1UL << capLeft);
                    }

                    // Capture Right (NorthEast +9) - Not allowed from Column H
                    if (square % 8 != 7)
                    {
                        int capRight = square + 9;
                        if ((enemyPieces & (1UL << capRight)) != 0)
                            moves |= (1UL << capRight);
                    }
                }
                else // --- BLACK PAWNS ---
                {
                    // A. Single Push (South -8)
                    int down1 = square - 8;
                    if (down1 >= 0 && ((allPieces & (1UL << down1)) == 0))
                    {
                        moves |= (1UL << down1);

                        // B. Double Push (South -16)
                        // Only allowed if on Rank 7 (Index 48-55)
                        if (square >= 48 && square <= 55)
                        {
                            int down2 = square - 16;
                            if ((allPieces & (1UL << down2)) == 0)
                            {
                                moves |= (1UL << down2);
                            }
                        }
                    }

                    // C. Captures (Diagonals -7 and -9)
                    // Capture Right (SouthEast -7) - Not allowed from Column H
                    if (square % 8 != 7)
                    {
                        int capRight = square - 7;
                        if ((enemyPieces & (1UL << capRight)) != 0)
                            moves |= (1UL << capRight);
                    }

                    // Capture Left (SouthWest -9) - Not allowed from Column A
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

                    // Add square
                    attacks |= (1UL << current);

                    // If we hit ANY piece (friend or foe), we stop sliding
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
                if (square % 8 == 7) return true; // H-file boundary

            if (offset == -1 || offset == 7 || offset == -9)
                if (square % 8 == 0) return true; // A-file boundary

            if (offset > 0 && square >= 56) return true;
            if (offset < 0 && square <= 7) return true;

            return false;
        }
    }
}