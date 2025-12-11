using System;
using System.Collections.Generic;
using System.Text;

namespace StampedeChess.Core
{
    public class Board
    {
        public ulong[] Bitboards { get; private set; }
        public bool IsWhiteToMove { get; set; }
        public int CastlingRights { get; set; }
        public int EnPassantTarget { get; set; }
        public ulong CurrentHash { get; set; }
        // keep track of moves so we don't get confused about the opening
        public readonly List<string> MoveHistory = new List<string>();

        // masks to help us check pawn structures
        private static readonly ulong[] FileMasks = new ulong[8];
        private static readonly ulong[] RankMasks = new ulong[8];
        private static readonly ulong[] IsolatedMasks = new ulong[8];
        private static readonly ulong[,] PassedPawnMasks = new ulong[2, 64]; // [color, square]

        // --- bonus tables ---
        // reward pawns that are running down the board
        private static readonly int[] PassedPawnBonus = { 0, 5, 10, 20, 35, 60, 100, 0 };
        private const int BishopPairBonus = 40;
        // private const int TempoBonus = 10; // commented out to fix eval at start

        // king safety settings
        private const int PawnShieldBonus = 25; // bonus for having a pawn shield
        private const int OpenFilePenalty = -30; // scary to have no shield

        public Board()
        {
            Bitboards = new ulong[12];
            CastlingRights = 15;
            EnPassantTarget = -1;
            MoveTables.Init();
            InitMasks();
        }

        private void InitMasks()
        {
            for (int f = 0; f < 8; f++)
            {
                FileMasks[f] = 0x0101010101010101UL << f;
            }
            for (int r = 0; r < 8; r++)
            {
                RankMasks[r] = 0xFFUL << (r * 8);
            }

            for (int f = 0; f < 8; f++)
            {
                if (f > 0) IsolatedMasks[f] |= FileMasks[f - 1];
                if (f < 7) IsolatedMasks[f] |= FileMasks[f + 1];
            }

            // pre-calculate masks for checking passed pawns
            for (int i = 0; i < 64; i++)
            {
                int rank = i / 8;
                int file = i % 8;

                // white passed pawn: check ahead
                ulong forwardMaskW = 0;
                for (int r = rank + 1; r < 8; r++)
                {
                    forwardMaskW |= (1UL << (r * 8 + file));
                    if (file > 0) forwardMaskW |= (1UL << (r * 8 + (file - 1)));
                    if (file < 7) forwardMaskW |= (1UL << (r * 8 + (file + 1)));
                }
                PassedPawnMasks[0, i] = forwardMaskW;

                // black passed pawn: check behind (visually ahead for black)
                ulong forwardMaskB = 0;
                for (int r = rank - 1; r >= 0; r--)
                {
                    forwardMaskB |= (1UL << (r * 8 + file));
                    if (file > 0) forwardMaskB |= (1UL << (r * 8 + (file - 1)));
                    if (file < 7) forwardMaskB |= (1UL << (r * 8 + (file + 1)));
                }
                PassedPawnMasks[1, i] = forwardMaskB;
            }
        }

        // --- evaluation tables ---
        // tons of magic numbers here to tell pieces where they like to be
        private static readonly int[] MgPawnTable = { 0, 0, 0, 0, 0, 0, 0, 0, 50, 50, 50, 50, 50, 50, 50, 50, 10, 10, 20, 30, 30, 20, 10, 10, 5, 5, 10, 25, 25, 10, 5, 5, 0, 0, 0, 20, 20, 0, 0, 0, 5, -5, -10, 0, 0, -10, -5, 5, 5, 10, 10, -20, -20, 10, 10, 5, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly int[] EgPawnTable = { 0, 0, 0, 0, 0, 0, 0, 0, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 30, 30, 20, 20, 20, 30, 30, 30, 40, 40, 30, 30, 30, 40, 40, 40, 50, 50, 40, 40, 40, 50, 50, 50, 60, 60, 50, 50, 50, 60, 60, 60, 70, 70, 60, 60, 60, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly int[] MgKnightTable = { -50, -40, -30, -30, -30, -30, -40, -50, -40, -20, 0, 0, 0, 0, -20, -40, -30, 0, 10, 15, 15, 10, 0, -30, -30, 5, 15, 20, 20, 15, 5, -30, -30, 0, 15, 20, 20, 15, 0, -30, -30, 5, 10, 15, 15, 10, 5, -30, -40, -20, 0, 5, 5, 0, -20, -40, -50, -40, -30, -30, -30, -30, -40, -50 };
        private static readonly int[] EgKnightTable = MgKnightTable;
        private static readonly int[] MgBishopTable = { -20, -10, -10, -10, -10, -10, -10, -20, -10, 0, 0, 0, 0, 0, 0, -10, -10, 0, 5, 10, 10, 5, 0, -10, -10, 5, 5, 10, 10, 5, 5, -10, -10, 0, 10, 10, 10, 10, 0, -10, -10, 10, 10, 10, 10, 10, 10, -10, -10, 5, 0, 0, 0, 0, 5, -10, -20, -10, -10, -10, -10, -10, -10, -20 };
        private static readonly int[] EgBishopTable = MgBishopTable;
        private static readonly int[] MgRookTable = { 0, 0, 0, 0, 0, 0, 0, 0, 5, 10, 10, 10, 10, 10, 10, 5, -5, 0, 0, 0, 0, 0, 0, -5, -5, 0, 0, 0, 0, 0, 0, -5, -5, 0, 0, 0, 0, 0, 0, -5, -5, 0, 0, 0, 0, 0, 0, -5, 5, 10, 10, 10, 10, 10, 10, 5, 0, 0, 0, 5, 5, 0, 0, 0 };
        private static readonly int[] EgRookTable = MgRookTable;
        private static readonly int[] MgQueenTable = { -20, -10, -10, -5, -5, -10, -10, -20, -10, 0, 0, 0, 0, 0, 0, -10, -10, 0, 5, 5, 5, 5, 0, -10, -5, 0, 5, 5, 5, 5, 0, -5, 0, 0, 5, 5, 5, 5, 0, -5, -10, 5, 5, 5, 5, 5, 0, -10, -10, 0, 5, 0, 0, 0, 0, -10, -20, -10, -10, -5, -5, -10, -10, -20 };
        private static readonly int[] EgQueenTable = MgQueenTable;
        private static readonly int[] MgKingTable = { -30, -40, -40, -50, -50, -40, -40, -30, -30, -40, -40, -50, -50, -40, -40, -30, -30, -40, -40, -50, -50, -40, -40, -30, -30, -40, -40, -50, -50, -40, -40, -30, -20, -30, -30, -40, -40, -30, -30, -20, -10, -20, -20, -20, -20, -20, -20, -10, 20, 20, 0, 0, 0, 0, 20, 20, 20, 30, 10, 0, 0, 10, 30, 20 };
        private static readonly int[] EgKingTable = { -50, -40, -30, -20, -20, -30, -40, -50, -30, -20, -10, 0, 0, -10, -20, -30, -30, -10, 20, 30, 30, 20, -10, -30, -30, -10, 30, 40, 40, 30, -10, -30, -30, -10, 30, 40, 40, 30, -10, -30, -30, -10, 20, 30, 30, 20, -10, -30, -30, -30, 0, 0, 0, 0, -30, -30, -50, -30, -30, -30, -30, -30, -30, -50 };

        public float Evaluate()
        {
            int mgScore = 0;
            int egScore = 0;
            int gamePhase = 0;

            ulong whitePawns = Bitboards[0];
            ulong blackPawns = Bitboards[6];

            // count bishops for the pair bonus
            int whiteBishops = 0;
            int blackBishops = 0;

            for (int i = 0; i < 12; i++)
            {
                ulong bitboard = Bitboards[i];
                if (i == 2) whiteBishops = CountBits(bitboard);
                if (i == 8) blackBishops = CountBits(bitboard);

                while (bitboard != 0)
                {
                    int square = TrailingZeroCount(bitboard);
                    bool isWhite = i <= 5;
                    int pieceType = i > 5 ? i - 6 : i;

                    int mgMat = 0, egMat = 0;
                    int phaseVal = 0;

                    // material weights
                    switch (pieceType)
                    {
                        case 0: mgMat = 100; egMat = 100; phaseVal = 0; break;
                        case 1: mgMat = 320; egMat = 280; phaseVal = 1; break;
                        case 2: mgMat = 330; egMat = 300; phaseVal = 1; break;
                        case 3: mgMat = 500; egMat = 550; phaseVal = 2; break;
                        case 4: mgMat = 900; egMat = 950; phaseVal = 4; break;
                    }

                    // piece-square tables lookup
                    int tableIndex = isWhite ? square : (square ^ 56);
                    int mgPos = 0, egPos = 0;
                    int[] mgTable = null, egTable = null;

                    switch (pieceType)
                    {
                        case 0: mgTable = MgPawnTable; egTable = EgPawnTable; break;
                        case 1: mgTable = MgKnightTable; egTable = EgKnightTable; break;
                        case 2: mgTable = MgBishopTable; egTable = EgBishopTable; break;
                        case 3: mgTable = MgRookTable; egTable = EgRookTable; break;
                        case 4: mgTable = MgQueenTable; egTable = EgQueenTable; break;
                        case 5: mgTable = MgKingTable; egTable = EgKingTable; break;
                    }
                    if (mgTable != null)
                    {
                        mgPos = mgTable[tableIndex];
                        egPos = egTable[tableIndex];
                    }

                    // positional bonuses
                    int posBonus = 0;

                    // mobility calculation (skipping pawns/kings to save cpu)
                    if (pieceType != 0 && pieceType != 5)
                    {
                        ulong moves = MoveGenerator.GetPseudoLegalMoves(square, i, this);
                        int moveCount = CountBits(moves);
                        if (pieceType == 1) posBonus += moveCount * 3;
                        if (pieceType == 2) posBonus += moveCount * 3;
                        if (pieceType == 3) posBonus += moveCount * 2;
                        if (pieceType == 4) posBonus += moveCount * 1;
                    }

                    // pawn structure logic
                    if (pieceType == 0)
                    {
                        int file = square % 8;
                        int rank = square / 8;
                        int relativeRank = isWhite ? rank : 7 - rank;

                        ulong myPawns = isWhite ? whitePawns : blackPawns;
                        ulong enemyPawns = isWhite ? blackPawns : whitePawns;

                        // penalty for doubled pawns
                        if (CountBits(myPawns & FileMasks[file]) > 1) posBonus -= 20;

                        // penalty for isolated pawns
                        if ((myPawns & IsolatedMasks[file]) == 0) posBonus -= 20;

                        // huge bonus for passed pawns
                        ulong passedMask = PassedPawnMasks[isWhite ? 0 : 1, square];
                        if ((passedMask & enemyPawns) == 0)
                        {
                            posBonus += PassedPawnBonus[relativeRank];
                        }
                    }

                    // king safety (pawn shield)
                    if (pieceType == 5)
                    {
                        // check pawns in front of king
                        int file = square % 8;
                        int rank = square / 8;

                        // only care in middlegame (when phase is low)
                        ulong myPawns = isWhite ? whitePawns : blackPawns;

                        // simplified shield: check immediate 3 squares in front
                        int shieldCount = 0;
                        int forward = isWhite ? 8 : -8;

                        // check front-left, front, front-right
                        if (file > 0)
                        {
                            if ((myPawns & (1UL << (square + forward - 1))) != 0) shieldCount++;
                        }
                        if ((myPawns & (1UL << (square + forward))) != 0) shieldCount++;
                        if (file < 7)
                        {
                            if ((myPawns & (1UL << (square + forward + 1))) != 0) shieldCount++;
                        }

                        // bonus for shield, penalty for open king
                        if (shieldCount > 0) mgPos += (shieldCount * PawnShieldBonus);
                        else mgPos += OpenFilePenalty;
                    }

                    int totalMg = mgMat + mgPos + posBonus;
                    int totalEg = egMat + egPos + posBonus;

                    if (isWhite)
                    {
                        mgScore += totalMg;
                        egScore += totalEg;
                    }
                    else
                    {
                        mgScore -= totalMg;
                        egScore -= totalEg;
                    }

                    gamePhase += phaseVal;
                    bitboard &= (bitboard - 1);
                }
            }

            // bishop pair bonus
            if (whiteBishops >= 2)
            {
                mgScore += BishopPairBonus;
                egScore += BishopPairBonus;
            }
            if (blackBishops >= 2)
            {
                mgScore -= BishopPairBonus;
                egScore -= BishopPairBonus;
            }

            // scale score based on game phase
            if (gamePhase > 24) gamePhase = 24;
            int scaledScore = ((mgScore * gamePhase) + (egScore * (24 - gamePhase))) / 24;

            return scaledScore / 100.0f;
        }

        private int CountBits(ulong value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= (value - 1);
            }
            return count;
        }

        private int TrailingZeroCount(ulong value)
        {
            if (value == 0) return 64;
            int count = 0;
            while ((value & 1) == 0)
            {
                value >>= 1;
                count++;
            }
            return count;
        }

        public (int captured, int moved, int oldRights, int oldEP, ulong oldHash) MakeMoveFast(int from, int to)
        {
            int movingPiece = GetPieceAtSquare(from);
            int targetPiece = GetPieceAtSquare(to);
            int oldEP = EnPassantTarget;
            int oldRights = CastlingRights;
            ulong oldHash = CurrentHash;

            if (movingPiece == -1) return (-1, -1, oldRights, oldEP, oldHash);

            CurrentHash ^= Zobrist.Pieces[movingPiece, from];

            if (targetPiece != -1)
            {
                CurrentHash ^= Zobrist.Pieces[targetPiece, to];
                Bitboards[targetPiece] &= ~(1UL << to);
                if (targetPiece == 3 || targetPiece == 9) UpdateCastlingRights(to);
            }

            bool isEP = (to == EnPassantTarget) && (movingPiece == 0 || movingPiece == 6);
            if (isEP)
            {
                int victimSquare = (movingPiece == 0) ? to - 8 : to + 8;
                int victimPiece = (movingPiece == 0) ? 6 : 0;
                Bitboards[victimPiece] &= ~(1UL << victimSquare);
                CurrentHash ^= Zobrist.Pieces[victimPiece, victimSquare];
                targetPiece = victimPiece;
            }

            Bitboards[movingPiece] &= ~(1UL << from);
            Bitboards[movingPiece] |= (1UL << to);
            CurrentHash ^= Zobrist.Pieces[movingPiece, to];

            if (movingPiece == 5 && Math.Abs(to - from) == 2)
            {
                if (to == 6)
                {
                    Bitboards[3] &= ~(1UL << 7);
                    Bitboards[3] |= (1UL << 5);
                    CurrentHash ^= Zobrist.Pieces[3, 7];
                    CurrentHash ^= Zobrist.Pieces[3, 5];
                }
                else if (to == 2)
                {
                    Bitboards[3] &= ~(1UL << 0);
                    Bitboards[3] |= (1UL << 3);
                    CurrentHash ^= Zobrist.Pieces[3, 0];
                    CurrentHash ^= Zobrist.Pieces[3, 3];
                }
            }
            else if (movingPiece == 11 && Math.Abs(to - from) == 2)
            {
                if (to == 62)
                {
                    Bitboards[9] &= ~(1UL << 63);
                    Bitboards[9] |= (1UL << 61);
                    CurrentHash ^= Zobrist.Pieces[9, 63];
                    CurrentHash ^= Zobrist.Pieces[9, 61];
                }
                else if (to == 58)
                {
                    Bitboards[9] &= ~(1UL << 56);
                    Bitboards[9] |= (1UL << 59);
                    CurrentHash ^= Zobrist.Pieces[9, 56];
                    CurrentHash ^= Zobrist.Pieces[9, 59];
                }
            }

            CurrentHash ^= Zobrist.CastlingRights[oldRights];
            UpdateCastlingRights(from);
            CurrentHash ^= Zobrist.CastlingRights[CastlingRights];

            int oldEpFile = (oldEP == -1) ? 8 : (oldEP % 8);
            CurrentHash ^= Zobrist.EnPassantFile[oldEpFile];

            EnPassantTarget = -1;
            if ((movingPiece == 0 || movingPiece == 6) && Math.Abs(to - from) == 16)
                EnPassantTarget = (from + to) / 2;

            int newEpFile = (EnPassantTarget == -1) ? 8 : (EnPassantTarget % 8);
            CurrentHash ^= Zobrist.EnPassantFile[newEpFile];

            IsWhiteToMove = !IsWhiteToMove;
            CurrentHash ^= Zobrist.SideToMove;

            return (targetPiece, movingPiece, oldRights, oldEP, oldHash);
        }

        public void UnmakeMoveFast(int from, int to, int capturedPiece, int movingPiece, int oldRights, int oldEP, ulong oldHash)
        {
            if (movingPiece == -1) return;
            CurrentHash = oldHash;
            IsWhiteToMove = !IsWhiteToMove;
            CastlingRights = oldRights;
            EnPassantTarget = oldEP;

            Bitboards[movingPiece] &= ~(1UL << to);
            Bitboards[movingPiece] |= (1UL << from);

            if (capturedPiece != -1)
            {
                bool wasEP = (to == oldEP) && (movingPiece == 0 || movingPiece == 6);
                int captureSquare = to;
                if (wasEP) captureSquare = (movingPiece == 0) ? to - 8 : to + 8;
                Bitboards[capturedPiece] |= (1UL << captureSquare);
            }

            if (movingPiece == 5 && Math.Abs(to - from) == 2)
            {
                if (to == 6)
                {
                    Bitboards[3] &= ~(1UL << 5);
                    Bitboards[3] |= (1UL << 7);
                }
                if (to == 2)
                {
                    Bitboards[3] &= ~(1UL << 3);
                    Bitboards[3] |= (1UL << 0);
                }
            }
            if (movingPiece == 11 && Math.Abs(to - from) == 2)
            {
                if (to == 62)
                {
                    Bitboards[9] &= ~(1UL << 61);
                    Bitboards[9] |= (1UL << 63);
                }
                if (to == 58)
                {
                    Bitboards[9] &= ~(1UL << 59);
                    Bitboards[9] |= (1UL << 56);
                }
            }
        }

        private void UpdateCastlingRights(int square)
        {
            if (square == 4 || square == 60)
            {
                if (square == 4) CastlingRights &= ~3;
                if (square == 60) CastlingRights &= ~12;
            }
            if (square == 0) CastlingRights &= ~2;
            if (square == 7) CastlingRights &= ~1;
            if (square == 56) CastlingRights &= ~8;
            if (square == 63) CastlingRights &= ~4;
        }

        public List<(int From, int To)> GetAllLegalMoves(bool capturesOnly = false)
        {
            var moves = new List<(int, int)>();
            bool isWhite = IsWhiteToMove;
            for (int i = 0; i < 64; i++)
            {
                int piece = GetPieceAtSquare(i);
                if (piece != -1 && (piece <= 5) == isWhite)
                {
                    ulong legalBitmask = MoveGenerator.GetPseudoLegalMoves(i, piece, this);
                    while (legalBitmask != 0)
                    {
                        int target = TrailingZeroCount(legalBitmask);
                        bool addMove = true;
                        if (capturesOnly)
                        {
                            int targetPiece = GetPieceAtSquare(target);
                            bool isEP = (target == EnPassantTarget) && (piece == 0 || piece == 6);
                            if (targetPiece == -1 && !isEP) addMove = false;
                        }

                        if (addMove)
                        {
                            if (IsMoveSafe(i, target)) moves.Add((i, target));
                        }

                        legalBitmask &= ~(1UL << target);
                    }
                }
            }
            return moves;
        }

        public string GetBotMove() => AI.GetBestMove(this);

        public Board Clone()
        {
            Board newBoard = new Board();
            Array.Copy(this.Bitboards, newBoard.Bitboards, 12);
            newBoard.IsWhiteToMove = this.IsWhiteToMove;
            newBoard.CastlingRights = this.CastlingRights;
            newBoard.EnPassantTarget = this.EnPassantTarget;
            newBoard.CurrentHash = this.CurrentHash;
            newBoard.MoveHistory.AddRange(this.MoveHistory);
            return newBoard;
        }

        public string MakeMove(string moveString, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(moveString)) { errorMessage = "Empty input."; return null; }

            string rawInput = moveString.Trim();
            string lowerInput = rawInput.ToLower().Replace("0", "o");
            if (lowerInput == "o-o") rawInput = IsWhiteToMove ? "e1g1" : "e8g8";
            else if (lowerInput == "o-o-o") rawInput = IsWhiteToMove ? "e1c1" : "e8c8";

            char firstChar = rawInput[0];
            string stringToParse = char.IsUpper(firstChar) ? rawInput.Substring(1) : rawInput;
            string cleanMove = stringToParse.Replace("-", "").Replace("x", "").ToLower();

            string coordsOnly = "";
            foreach (char c in cleanMove)
            {
                if ((c >= 'a' && c <= 'h') || (c >= '1' && c <= '8')) coordsOnly += c;
            }

            if (coordsOnly.Length != 4) { errorMessage = "Invalid Format."; return null; }

            try
            {
                int fromIndex = StringToIndex(coordsOnly.Substring(0, 2));
                int toIndex = StringToIndex(coordsOnly.Substring(2, 2));
                int movingPieceType = GetPieceAtSquare(fromIndex);

                if (movingPieceType == -1) { errorMessage = "No piece there."; return null; }
                if ((movingPieceType <= 5) != IsWhiteToMove) { errorMessage = "Wrong turn."; return null; }

                ulong legalMoves = MoveGenerator.GetPseudoLegalMoves(fromIndex, movingPieceType, this);
                if ((legalMoves & (1UL << toIndex)) == 0) { errorMessage = "Illegal Move."; return null; }
                if (!IsMoveSafe(fromIndex, toIndex)) { errorMessage = "Illegal Move: King in check."; return null; }

                int targetPieceType = GetPieceAtSquare(toIndex);
                bool isCapture = targetPieceType != -1;
                bool isEP = (toIndex == EnPassantTarget) && (movingPieceType == 0 || movingPieceType == 6);
                if (isEP) isCapture = true;

                MakeMoveFast(fromIndex, toIndex);

                char pieceChar = GetNotationChar(movingPieceType);
                bool isPawn = (movingPieceType == 0 || movingPieceType == 6);
                string prefix = isPawn ? "" : pieceChar.ToString();
                string resultNotation;

                if (movingPieceType == 5 && Math.Abs(toIndex - fromIndex) == 2)
                    resultNotation = (toIndex == 6) ? "0-0" : "0-0-0";
                else if (movingPieceType == 11 && Math.Abs(toIndex - fromIndex) == 2)
                    resultNotation = (toIndex == 62) ? "0-0" : "0-0-0";
                else
                {
                    string captureMark = isCapture ? "x" : "";
                    resultNotation = string.Format("{0}{1}{2}{3}", prefix, coordsOnly.Substring(0, 2), captureMark, coordsOnly.Substring(2, 2));
                }

                if (IsCheckmate()) { resultNotation += "#"; errorMessage = "GAME OVER"; }
                else if (IsSquareAttacked(GetKingSquare(IsWhiteToMove), !IsWhiteToMove)) { resultNotation += "+"; }

                MoveHistory.Add(resultNotation);
                return resultNotation;
            }
            catch { errorMessage = "System Error."; return null; }
        }

        private bool IsMoveSafe(int from, int to)
        {
            bool wasWhiteTurn = IsWhiteToMove;
            var undo = MakeMoveFast(from, to);
            int myKingSq = GetKingSquare(wasWhiteTurn);
            bool isSelfCheck = IsSquareAttacked(myKingSq, IsWhiteToMove);
            UnmakeMoveFast(from, to, undo.captured, undo.moved, undo.oldRights, undo.oldEP, undo.oldHash);
            return !isSelfCheck;
        }

        public void LoadPosition(string fen)
        {
            Array.Clear(Bitboards, 0, 12);
            MoveHistory.Clear();
            string[] sections = fen.Split(' ');
            string boardLayout = sections[0];
            int rank = 7;
            int file = 0;
            foreach (char symbol in boardLayout)
            {
                if (symbol == '/') { rank--; file = 0; }
                else if (char.IsDigit(symbol)) { file += (int)char.GetNumericValue(symbol); }
                else
                {
                    int squareIndex = rank * 8 + file;
                    int pieceType = GetPieceTypeFromSymbol(symbol);
                    Bitboards[pieceType] |= (1UL << squareIndex);
                    file++;
                }
            }
            IsWhiteToMove = (sections.Length > 1 && sections[1] == "w");
            CastlingRights = 0;
            if (sections.Length > 2)
            {
                string rights = sections[2];
                if (rights.Contains("K")) CastlingRights |= 1;
                if (rights.Contains("Q")) CastlingRights |= 2;
                if (rights.Contains("k")) CastlingRights |= 4;
                if (rights.Contains("q")) CastlingRights |= 8;
            }
            EnPassantTarget = -1;
            if (sections.Length > 3 && sections[3] != "-") EnPassantTarget = StringToIndex(sections[3]);

            CurrentHash = 0;
            if (IsWhiteToMove) CurrentHash ^= Zobrist.SideToMove;
            CurrentHash ^= Zobrist.CastlingRights[CastlingRights];
            int epFile = (EnPassantTarget == -1) ? 8 : (EnPassantTarget % 8);
            CurrentHash ^= Zobrist.EnPassantFile[epFile];
            for (int sq = 0; sq < 64; sq++) { int p = GetPieceAtSquare(sq); if (p != -1) CurrentHash ^= Zobrist.Pieces[p, sq]; }
        }

        public int GetPieceAtSquare(int squareIndex)
        {
            for (int i = 0; i < 12; i++)
            {
                if ((Bitboards[i] & (1UL << squareIndex)) != 0) return i;
            }
            return -1;
        }

        public int GetKingSquare(bool isWhite)
        {
            int kingType = isWhite ? 5 : 11;
            return TrailingZeroCount(Bitboards[kingType]);
        }

        public bool IsSquareAttacked(int square, bool attackerIsWhite)
        {
            ulong enemyKnights = attackerIsWhite ? Bitboards[1] : Bitboards[7];
            if ((MoveTables.KnightAttacks[square] & enemyKnights) != 0) return true;
            ulong enemyKing = attackerIsWhite ? Bitboards[5] : Bitboards[11];
            if ((MoveTables.KingAttacks[square] & enemyKing) != 0) return true;
            ulong enemyRooks = attackerIsWhite ? Bitboards[3] : Bitboards[9];
            ulong enemyQueens = attackerIsWhite ? Bitboards[4] : Bitboards[10];
            ulong orthMoves = MoveGenerator.GenerateSlidingMoves(square, 3, this);
            if ((orthMoves & (enemyRooks | enemyQueens)) != 0) return true;
            ulong enemyBishops = attackerIsWhite ? Bitboards[2] : Bitboards[8];
            ulong diagMoves = MoveGenerator.GenerateSlidingMoves(square, 2, this);
            if ((diagMoves & (enemyBishops | enemyQueens)) != 0) return true;
            ulong enemyPawns = attackerIsWhite ? Bitboards[0] : Bitboards[6];
            ulong pawnAttacks = 0;

            if (attackerIsWhite)
            {
                if (square >= 8 && square % 8 != 0) pawnAttacks |= (1UL << (square - 9));
                if (square >= 8 && square % 8 != 7) pawnAttacks |= (1UL << (square - 7));
            }
            else
            {
                if (square < 56 && square % 8 != 0) pawnAttacks |= (1UL << (square + 7));
                if (square < 56 && square % 8 != 7) pawnAttacks |= (1UL << (square + 9));
            }
            return (pawnAttacks & enemyPawns) != 0;
        }

        public bool IsCheckmate()
        {
            int kingSquare = GetKingSquare(IsWhiteToMove);
            if (!IsSquareAttacked(kingSquare, !IsWhiteToMove)) return false;
            var moves = GetAllLegalMoves();
            return moves.Count == 0;
        }

        public ulong GetWhitePieces()
        {
            return Bitboards[0] | Bitboards[1] | Bitboards[2] | Bitboards[3] | Bitboards[4] | Bitboards[5];
        }

        public ulong GetBlackPieces()
        {
            return Bitboards[6] | Bitboards[7] | Bitboards[8] | Bitboards[9] | Bitboards[10] | Bitboards[11];
        }

        public ulong GetAllPieces()
        {
            return GetWhitePieces() | GetBlackPieces();
        }

        public string IndexToString(int index)
        {
            return string.Format("{0}{1}", (char)('a' + (index % 8)), (char)('1' + (index / 8)));
        }

        public int StringToIndex(string square)
        {
            return (square[1] - '1') * 8 + (square[0] - 'a');
        }

        private int GetPieceTypeFromSymbol(char symbol)
        {
            switch (symbol)
            {
                case 'P': return 0;
                case 'N': return 1;
                case 'B': return 2;
                case 'R': return 3;
                case 'Q': return 4;
                case 'K': return 5;
                case 'p': return 6;
                case 'n': return 7;
                case 'b': return 8;
                case 'r': return 9;
                case 'q': return 10;
                case 'k': return 11;
                default: throw new Exception("Invalid Symbol");
            }
        }

        private char GetNotationChar(int p)
        {
            switch (p)
            {
                case 1: case 7: return 'N';
                case 2: case 8: return 'B';
                case 3: case 9: return 'R';
                case 4: case 10: return 'Q';
                case 5: case 11: return 'K';
                default: return '?';
            }
        }
    }
}