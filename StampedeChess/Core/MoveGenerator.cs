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

            // figure out the side based on the piece itself, not whose turn it is.
            // this makes sure the eval function sees mobility correctly for both players.
            bool isWhitePiece = (pieceType <= 5);

            ulong ownPieces = isWhitePiece ? board.GetWhitePieces() : board.GetBlackPieces();
            ulong allPieces = board.GetAllPieces();
            ulong enemyPieces = isWhitePiece ? board.GetBlackPieces() : board.GetWhitePieces();

            // knights just jump around
            if (pieceType == Piece.WhiteKnight || pieceType == Piece.BlackKnight)
            {
                moves = MoveTables.KnightAttacks[square];
                moves &= ~ownPieces;
            }
            // king moves
            else if (pieceType == Piece.WhiteKing || pieceType == Piece.BlackKing)
            {
                moves = MoveTables.KingAttacks[square];
                moves &= ~ownPieces;

                // castling logic. it's safe to check the turn here since you can't castle out of turn.
                if (isWhitePiece && board.IsWhiteToMove)
                {
                    // white king-side
                    if ((board.CastlingRights & 1) != 0 && square == 4)
                    {
                        if ((allPieces & ((1UL << 5) | (1UL << 6))) == 0)
                            if (!board.IsSquareAttacked(4, false) && !board.IsSquareAttacked(5, false)) moves |= (1UL << 6);
                    }
                    // white queen-side
                    if ((board.CastlingRights & 2) != 0 && square == 4)
                    {
                        if ((allPieces & ((1UL << 3) | (1UL << 2) | (1UL << 1))) == 0)
                            if (!board.IsSquareAttacked(4, false) && !board.IsSquareAttacked(3, false)) moves |= (1UL << 2);
                    }
                }
                else if (!isWhitePiece && !board.IsWhiteToMove)
                {
                    // black king-side
                    if ((board.CastlingRights & 4) != 0 && square == 60)
                    {
                        if ((allPieces & ((1UL << 61) | (1UL << 62))) == 0)
                            if (!board.IsSquareAttacked(60, true) && !board.IsSquareAttacked(61, true)) moves |= (1UL << 62);
                    }
                    // black queen-side
                    if ((board.CastlingRights & 8) != 0 && square == 60)
                    {
                        if ((allPieces & ((1UL << 59) | (1UL << 58) | (1UL << 57))) == 0)
                            if (!board.IsSquareAttacked(60, true) && !board.IsSquareAttacked(59, true)) moves |= (1UL << 58);
                    }
                }
            }
            // sliding pieces (rooks, bishops, queens)
            else if (IsSlidingPiece(pieceType))
            {
                moves = GenerateSlidingMoves(square, pieceType, board);
                moves &= ~ownPieces;
            }
            // pawns are complicated
            else
            {
                if (isWhitePiece)
                {
                    // one step forward
                    int up1 = square + 8;
                    if (up1 < 64 && ((allPieces & (1UL << up1)) == 0))
                    {
                        moves |= (1UL << up1);
                        // two steps forward
                        if (square >= 8 && square <= 15) { int up2 = square + 16; if ((allPieces & (1UL << up2)) == 0) moves |= (1UL << up2); }
                    }
                    // capture diagonally
                    if (square % 8 != 0) { int capLeft = square + 7; if ((enemyPieces & (1UL << capLeft)) != 0) moves |= (1UL << capLeft); }
                    if (square % 8 != 7) { int capRight = square + 9; if ((enemyPieces & (1UL << capRight)) != 0) moves |= (1UL << capRight); }
                }
                else
                {
                    // one step forward (down for black)
                    int down1 = square - 8;
                    if (down1 >= 0 && ((allPieces & (1UL << down1)) == 0))
                    {
                        moves |= (1UL << down1);
                        // two steps forward
                        if (square >= 48 && square <= 55) { int down2 = square - 16; if ((allPieces & (1UL << down2)) == 0) moves |= (1UL << down2); }
                    }
                    // capture diagonally
                    if (square % 8 != 7) { int capRight = square - 7; if ((enemyPieces & (1UL << capRight)) != 0) moves |= (1UL << capRight); }
                    if (square % 8 != 0) { int capLeft = square - 9; if ((enemyPieces & (1UL << capLeft)) != 0) moves |= (1UL << capLeft); }
                }
            }
            return moves;
        }

        private static bool IsSlidingPiece(int p) => p == Piece.WhiteRook || p == Piece.BlackRook || p == Piece.WhiteBishop || p == Piece.BlackBishop || p == Piece.WhiteQueen || p == Piece.BlackQueen;

        public static ulong GenerateSlidingMoves(int square, int piece, Board board)
        {
            ulong attacks = 0;
            int[] directions = GetDirections(piece);
            ulong allPieces = board.GetAllPieces();

            // raycast in all directions until we hit something
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

        // checks if we are about to wrap around the board so we can prevent errors in sliding piece move generation
        private static bool IsEdge(int square, int offset)
        {
            if (offset == 1 || offset == -7 || offset == 9) if (square % 8 == 7) return true;
            if (offset == -1 || offset == 7 || offset == -9) if (square % 8 == 0) return true;
            if (square >= 56 && (offset == 8 || offset == 9 || offset == 7)) return true;
            if (square <= 7 && (offset == -8 || offset == -9 || offset == -7)) return true;
            return false;
        }
    }
}