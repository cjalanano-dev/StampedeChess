using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StampedeChess.Core
{
    internal class Zobrist
    {
        // zobrist hashing stuff to identify board positions quickly

        // [12 pieces][64 squares] lookup table
        public static readonly ulong[,] Pieces = new ulong[12, 64];
        public static readonly ulong[] CastlingRights = new ulong[16];
        public static readonly ulong[] EnPassantFile = new ulong[9]; // 0-7 files, 8 means no en passant available
        public static ulong SideToMove;

        // fill the tables with random numbers on startup
        static Zobrist()
        {
            // use a fixed seed so the hashes are consistent across runs.
            // otherwise, loading a save or debugging would be a nightmare.
            Random rng = new Random(123456);

            for (int p = 0; p < 12; p++)
                for (int s = 0; s < 64; s++)
                    Pieces[p, s] = NextUlong(rng);

            for (int i = 0; i < 16; i++)
                CastlingRights[i] = NextUlong(rng);

            for (int i = 0; i < 9; i++)
                EnPassantFile[i] = NextUlong(rng);

            SideToMove = NextUlong(rng);
        }

        private static ulong NextUlong(Random rng)
        {
            byte[] buffer = new byte[8];
            rng.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }
    }
}