namespace StampedeChess.Core
{
    public static class Piece
    {
        // bitboard indices
        public const int WhitePawn = 0;
        public const int WhiteKnight = 1;
        public const int WhiteBishop = 2;
        public const int WhiteRook = 3;
        public const int WhiteQueen = 4;
        public const int WhiteKing = 5;

        public const int BlackPawn = 6;
        public const int BlackKnight = 7;
        public const int BlackBishop = 8;
        public const int BlackRook = 9;
        public const int BlackQueen = 10;
        public const int BlackKing = 11;

        // piece values
        public static readonly int[] Values = {
            1, 3, 3, 5, 9, 0,  // White
            1, 3, 3, 5, 9, 0   // Black
        };
    }
}