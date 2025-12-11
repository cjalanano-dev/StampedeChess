using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

         // Transposition table implementation would go here
        // This is typically used in chess engines to store previously computed positions
namespace StampedeChess.Core
    { 

        public static class TranspositionTable
        {
            public const int Exact = 0;
            public const int LowerBound = 1; // Alpha - means the score is at least this good
            public const int UpperBound = 2; // Beta - means the score is at most this good

        public struct Entry
            {
                public ulong Key;
                public int Score;
                public int Depth;
                public byte Flag;
                public short BestMoveFrom; // store best move for move ordering
            public short BestMoveTo;
            }

            // 64MB hash table - can be updated based on available memory
            // 0x400000 = 4 million entries
            private static readonly Entry[] Table = new Entry[0x400000];

            public static void Clear()
            {
                Array.Clear(Table, 0, Table.Length);
            }

            public static void Store(ulong key, int score, int depth, int flag, int from, int to)
            {
                long index = (long)(key % (ulong)Table.Length);

            // this is overwrite strategy which replaces only if depth is greater or empty
            if (Table[index].Key == 0 || depth >= Table[index].Depth)
                {
                    Table[index].Key = key;
                    Table[index].Score = score;
                    Table[index].Depth = depth;
                    Table[index].Flag = (byte)flag;
                    Table[index].BestMoveFrom = (short)from;
                    Table[index].BestMoveTo = (short)to;
                }
            }

            public static bool Probe(ulong key, out Entry entry)
            {
                long index = (long)(key % (ulong)Table.Length);
                entry = Table[index];
                return entry.Key == key;
            }
        }
    }

