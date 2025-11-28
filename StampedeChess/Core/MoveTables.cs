using System;

namespace StampedeChess.Core
{
    public static class MoveTables
    {
        // attack maps
        public static ulong[] KnightAttacks = new ulong[64];
        public static ulong[] KingAttacks = new ulong[64];

        // init tables on startup
        public static void Init()
        {
            for (int square = 0; square < 64; square++)
            {
                KnightAttacks[square] = GenerateKnightAttacks(square);
                KingAttacks[square] = GenerateKingAttacks(square);
            }
        }

        // knight jumps logic
        private static ulong GenerateKnightAttacks(int square)
        {
            ulong attacks = 0;
            ulong b = 1UL << square;

            // file masks to prevent wrapping
            ulong notA = 0xfefefefefefefefe;
            ulong notAB = 0xfcfcfcfcfcfcfcfc;
            ulong notH = 0x7f7f7f7f7f7f7f7f;
            ulong notGH = 0x3f3f3f3f3f3f3f3f;

            // north jumps
            if ((b & notA) != 0) attacks |= (b << 15);
            if ((b & notH) != 0) attacks |= (b << 17);

            // east jumps
            if ((b & notGH) != 0) attacks |= (b << 10);
            if ((b & notGH) != 0) attacks |= (b >> 6);

            // south jumps
            if ((b & notH) != 0) attacks |= (b >> 15);
            if ((b & notA) != 0) attacks |= (b >> 17);

            // west jumps
            if ((b & notAB) != 0) attacks |= (b << 6);
            if ((b & notAB) != 0) attacks |= (b >> 10);

            return attacks;
        }

        // king steps logic
        private static ulong GenerateKingAttacks(int square)
        {
            ulong attacks = 0;
            ulong b = 1UL << square;
            ulong notA = 0xfefefefefefefefe;
            ulong notH = 0x7f7f7f7f7f7f7f7f;

            // north
            attacks |= (b << 8);
            if ((b & notA) != 0) attacks |= (b << 7);
            if ((b & notH) != 0) attacks |= (b << 9);

            // south
            attacks |= (b >> 8);
            if ((b & notA) != 0) attacks |= (b >> 9);
            if ((b & notH) != 0) attacks |= (b >> 7);

            // sides
            if ((b & notA) != 0) attacks |= (b >> 1);
            if ((b & notH) != 0) attacks |= (b << 1);

            return attacks;
        }
    }
}