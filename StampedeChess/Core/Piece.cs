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

        // ui symbols
        public static char GetSymbol(int pieceIndex)
        {
            switch (pieceIndex)
            {
                case 0: return '♟'; // White Pawn
                case 1: return '♞'; // White Knight
                case 2: return '♝'; // White Bishop
                case 3: return '♜'; // White Rook
                case 4: return '♛'; // White Queen
                case 5: return '♚'; // White King

                case 6: return '♟'; // Black Pawn
                case 7: return '♞'; // Black Knight
                case 8: return '♝'; // Black Bishop
                case 9: return '♜'; // Black Rook
                case 10: return '♛'; // Black Queen
                case 11: return '♚'; // Black King

                default: return ' '; // Empty
            }
        }
    }
}