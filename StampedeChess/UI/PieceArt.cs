namespace StampedeChess.UI
{
    public static class PieceArt
    {
        public static string[] GetArt(int pieceIndex)
        {
            switch (pieceIndex)
            {
                // --- WHITE PIECES (0-5) ---
                case 0: // pawns
                    return new[] {
                        "       ",
                        "   o   ",
                        "  /_\\  "
                    };
                case 1: // knights
                    return new[] {
                        "  ,^.  ",
                        "  /N|  ",
                        "  __L  "
                    };
                case 2: // bishops
                    return new[] {
                        "   o   ",
                        "  (B)  ",
                        "  _|_  "
                    };
                case 3: // rooks
                    return new[] {
                        "  UUU  ",
                        "  |R|  ",
                        "  |_|  "
                    };
                case 4: // queen
                    return new[] {
                        "  ooo  ",
                        "  (Q)  ",
                        "  _M_  "
                    };
                case 5: // king
                    return new[] {
                        "   +   ",
                        "  {K}  ",
                        "  _M_  "
                    };

                // --- BLACK PIECES (6-11) ---
                // same shape, but the Renderer will color them ORANGE/BLACK
                case 6:
                    return new[] { "       ", "   o   ", "  /_\\  " };
                case 7:
                    return new[] { "  ,^.  ", "  /N|  ", "  __L  " };
                case 8:
                    return new[] { "   o   ", "  (B)  ", "  _|_  " };
                case 9:
                    return new[] { "  UUU  ", "  |R|  ", "  |_|  " };
                case 10:
                    return new[] { "  ooo  ", "  (Q)  ", "  _M_  " };
                case 11:
                    return new[] { "   +   ", "  {K}  ", "  _M_  " };

                // empty square
                default:
                    return new[] {
                        "       ",
                        "       ",
                        "       "
                    };
            }
        }
    }
}