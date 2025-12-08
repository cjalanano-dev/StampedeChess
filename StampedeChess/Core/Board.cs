using System;
using System.Text;
using System.Collections.Generic;

namespace StampedeChess.Core
{
    public class Board
    {
        public ulong[] Bitboards { get; private set; }
        public bool IsWhiteToMove { get; set; }
        public int CastlingRights { get; set; }
        public int EnPassantTarget { get; set; }
        public readonly List<string> MoveHistory = new List<string>();

        public Board()
        {
            Bitboards = new ulong[12];
            CastlingRights = 15;
            EnPassantTarget = -1;
            MoveTables.Init();
        }

        public float Evaluate()
        {
            float score = 0;
            for (int i = 0; i < 12; i++)
            {
                ulong bitboard = Bitboards[i];
                while (bitboard != 0)
                {
                    int square = TrailingZeroCount(bitboard);

                    float material = 0.0f;
                    switch (i)
                    {
                        case 0:
                        case 6:
                            material = 1.0f;   // pawn
                            break;
                        case 1:
                        case 7:
                            material = 3.0f;   // knight
                            break;
                        case 2:
                        case 8:
                            material = 3.1f;   // bishop
                            break;
                        case 3:
                        case 9:
                            material = 5.0f;   // rook
                            break;
                        case 4:
                        case 10:
                            material = 9.0f;   // queen
                            break;
                        default:
                            material = 200.0f; // king
                            break;
                    }

                    float position = 0.0f;

                    // determine piece type (0-5) regardless of color
                    int pieceType = i > 5 ? i - 6 : i;
                    bool isWhite = i <= 5;

                    // if black, we must flip the square vertically (mirror the board)
                    // square ^ 56 flips rank 1 to 8, 2 to 7, etc.
                    int tableIndex = isWhite ? square : (square ^ 56);

                    switch (pieceType)
                    {
                        case 0: position = PawnTable[tableIndex]; break;
                        case 1: position = KnightTable[tableIndex]; break;
                        case 2: position = BishopTable[tableIndex]; break;
                        case 3: position = RookTable[tableIndex]; break;
                        case 4: position = QueenTable[tableIndex]; break;
                        case 5: position = KingTable[tableIndex]; break;
                    }

                    // combine
                    if (isWhite) score += (material + position);
                    else score -= (material + position);

                    bitboard &= (bitboard - 1);
                }
            }
            return score;
        }

        // piece-square tables
        // defines where pieces like to be. values are added to the material score.
        // defined from white's perspective (bottom to top).

        private static readonly float[] PawnTable = {
             0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, // rank 1 (illegal)
             0.5f,  0.5f,  0.5f,  0.5f,  0.5f,  0.5f,  0.5f,  0.5f, // rank 2 (start)
             0.1f,  0.1f,  0.2f,  0.3f,  0.3f,  0.2f,  0.1f,  0.1f, // rank 3
             0.0f,  0.0f,  0.0f,  0.2f,  0.2f,  0.0f,  0.0f,  0.0f, // rank 4
             0.0f,  0.0f,  0.0f,  0.3f,  0.3f,  0.0f,  0.0f,  0.0f, // rank 5
             0.5f,  0.5f,  0.5f,  0.6f,  0.6f,  0.5f,  0.5f,  0.5f, // rank 6
             0.8f,  0.8f,  0.8f,  0.8f,  0.8f,  0.8f,  0.8f,  0.8f, // rank 7 (promotion imminent)
             0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f  // rank 8 (promotion)
        };

        private static readonly float[] KnightTable = {
            -0.5f, -0.4f, -0.3f, -0.3f, -0.3f, -0.3f, -0.4f, -0.5f, // rank 1 (back rank bad)
            -0.4f, -0.2f,  0.0f,  0.0f,  0.0f,  0.0f, -0.2f, -0.4f,
            -0.3f,  0.0f,  0.1f,  0.2f,  0.2f,  0.1f,  0.0f, -0.3f,
            -0.3f,  0.0f,  0.2f,  0.2f,  0.2f,  0.2f,  0.0f, -0.3f, // center good
            -0.3f,  0.0f,  0.2f,  0.2f,  0.2f,  0.2f,  0.0f, -0.3f,
            -0.3f,  0.0f,  0.1f,  0.2f,  0.2f,  0.1f,  0.0f, -0.3f,
            -0.4f, -0.2f,  0.0f,  0.0f,  0.0f,  0.0f, -0.2f, -0.4f,
            -0.5f, -0.4f, -0.3f, -0.3f, -0.3f, -0.3f, -0.4f, -0.5f  // corners bad
        };

        private static readonly float[] BishopTable = {
            -0.2f, -0.1f, -0.1f, -0.1f, -0.1f, -0.1f, -0.1f, -0.2f,
            -0.1f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, -0.1f,
            -0.1f,  0.0f,  0.1f,  0.1f,  0.1f,  0.1f,  0.0f, -0.1f,
            -0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f, -0.1f, // long diagonals good
            -0.1f,  0.0f,  0.1f,  0.1f,  0.1f,  0.1f,  0.0f, -0.1f,
            -0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f, -0.1f,
            -0.1f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, -0.1f,
            -0.2f, -0.1f, -0.1f, -0.1f, -0.1f, -0.1f, -0.1f, -0.2f
        };

        private static readonly float[] RookTable = {
             0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,
             0.1f,  0.2f,  0.2f,  0.2f,  0.2f,  0.2f,  0.2f,  0.1f,
            -0.1f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, -0.1f,
            -0.1f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, -0.1f,
            -0.1f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, -0.1f,
            -0.1f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, -0.1f,
             0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f, // 7th rank nice
             0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f
        };

        private static readonly float[] QueenTable = {
            -0.2f, -0.1f, -0.1f, -0.1f, -0.1f, -0.1f, -0.1f, -0.2f,
            -0.1f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, -0.1f,
            -0.1f,  0.0f,  0.1f,  0.1f,  0.1f,  0.1f,  0.0f, -0.1f,
            -0.1f,  0.0f,  0.1f,  0.1f,  0.1f,  0.1f,  0.0f, -0.1f,
             0.0f,  0.0f,  0.1f,  0.1f,  0.1f,  0.1f,  0.0f, -0.1f,
            -0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f,  0.1f, -0.1f,
            -0.1f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.0f, -0.1f,
            -0.2f, -0.1f, -0.1f, -0.1f, -0.1f, -0.1f, -0.1f, -0.2f
        };

        private static readonly float[] KingTable = {
             0.2f,  0.3f,  0.1f,  0.0f,  0.0f,  0.1f,  0.3f,  0.2f, // back rank safety
             0.2f,  0.2f,  0.0f,  0.0f,  0.0f,  0.0f,  0.2f,  0.2f,
            -0.1f, -0.2f, -0.2f, -0.2f, -0.2f, -0.2f, -0.2f, -0.1f,
            -0.2f, -0.3f, -0.3f, -0.4f, -0.4f, -0.3f, -0.3f, -0.2f, // center dangerous early
            -0.3f, -0.4f, -0.4f, -0.5f, -0.5f, -0.4f, -0.4f, -0.3f,
            -0.3f, -0.4f, -0.4f, -0.5f, -0.5f, -0.4f, -0.4f, -0.3f,
            -0.3f, -0.4f, -0.4f, -0.5f, -0.5f, -0.4f, -0.4f, -0.3f,
            -0.3f, -0.4f, -0.4f, -0.5f, -0.5f, -0.4f, -0.4f, -0.3f
        };

        public (int captured, int moved, int oldRights, int oldEP) MakeMoveFast(int from, int to)
        {
            int movingPiece = GetPieceAtSquare(from);
            int targetPiece = GetPieceAtSquare(to);
            int oldEP = EnPassantTarget;
            int oldRights = CastlingRights;

            if (movingPiece == -1) return (-1, -1, oldRights, oldEP);

            // en passant
            bool isEP = (to == EnPassantTarget) && (movingPiece == 0 || movingPiece == 6);

            if (isEP)
            {
                int victimSquare = (movingPiece == 0) ? to - 8 : to + 8;
                int victimPiece = (movingPiece == 0) ? 6 : 0;
                Bitboards[victimPiece] &= ~(1UL << victimSquare);
                targetPiece = victimPiece;
            }
            else if (targetPiece != -1)
            {
                Bitboards[targetPiece] &= ~(1UL << to);
                if (targetPiece == 3 || targetPiece == 9) UpdateCastlingRights(to);
            }

            Bitboards[movingPiece] &= ~(1UL << from);
            Bitboards[movingPiece] |= (1UL << to);

            // castling
            if (movingPiece == 5 && Math.Abs(to - from) == 2)
            {
                if (to == 6) { Bitboards[3] &= ~(1UL << 7); Bitboards[3] |= (1UL << 5); }
                else if (to == 2) { Bitboards[3] &= ~(1UL << 0); Bitboards[3] |= (1UL << 3); }
            }
            else if (movingPiece == 11 && Math.Abs(to - from) == 2)
            {
                if (to == 62) { Bitboards[9] &= ~(1UL << 63); Bitboards[9] |= (1UL << 61); }
                else if (to == 58) { Bitboards[9] &= ~(1UL << 56); Bitboards[9] |= (1UL << 59); }
            }

            // update ep
            EnPassantTarget = -1;
            if ((movingPiece == 0 || movingPiece == 6) && Math.Abs(to - from) == 16)
                EnPassantTarget = (from + to) / 2;

            UpdateCastlingRights(from);
            IsWhiteToMove = !IsWhiteToMove;

            return (targetPiece, movingPiece, oldRights, oldEP);
        }

        public void UnmakeMoveFast(int from, int to, int capturedPiece, int movingPiece, int oldRights, int oldEP)
        {
            if (movingPiece == -1) return;
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

            // un-castle
            if (movingPiece == 5 && Math.Abs(to - from) == 2)
            {
                if (to == 6) { Bitboards[3] &= ~(1UL << 5); Bitboards[3] |= (1UL << 7); }
                if (to == 2) { Bitboards[3] &= ~(1UL << 3); Bitboards[3] |= (1UL << 0); }
            }
            if (movingPiece == 11 && Math.Abs(to - from) == 2)
            {
                if (to == 62) { Bitboards[9] &= ~(1UL << 61); Bitboards[9] |= (1UL << 63); }
                if (to == 58) { Bitboards[9] &= ~(1UL << 59); Bitboards[9] |= (1UL << 56); }
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

        public List<(int From, int To)> GetAllLegalMoves()
        {
            var moves = new List<(int, int)>();
            bool isWhite = IsWhiteToMove;
            for (int i = 0; i < 64; i++)
            {
                int piece = GetPieceAtSquare(i);
                if (piece != -1 && (piece <= 5) == isWhite)
                {
                    ulong legalBitmask = MoveGenerator.GetPseudoLegalMoves(i, piece, this);
                    for (int target = 0; target < 64; target++)
                    {
                        if ((legalBitmask & (1UL << target)) != 0)
                        {
                            if (IsMoveSafe(i, target)) moves.Add((i, target));
                        }
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
            return newBoard;
        }

        private int TrailingZeroCount(ulong value)
        {
            if (value == 0) return 64;
            int count = 0;
            while ((value & 1) == 0) { value >>= 1; count++; }
            return count;
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

                int targetPieceType = GetPieceAtSquare(toIndex);
                bool isCapture = targetPieceType != -1;

                if ((movingPieceType <= 5) != IsWhiteToMove) { errorMessage = "Wrong turn."; return null; }

                bool isEP = (toIndex == EnPassantTarget) && (movingPieceType == 0 || movingPieceType == 6);
                if (isEP) isCapture = true;

                if (isCapture)
                {
                    bool selfWhite = movingPieceType <= 5;
                    if (targetPieceType != -1)
                    {
                        bool targetWhite = targetPieceType <= 5;
                        if (selfWhite == targetWhite) { errorMessage = "Cannot capture own piece."; return null; }
                    }
                }

                ulong legalMoves = MoveGenerator.GetPseudoLegalMoves(fromIndex, movingPieceType, this);
                if ((legalMoves & (1UL << toIndex)) == 0) { errorMessage = "Illegal Move (Geometry)."; return null; }

                if (!IsMoveSafe(fromIndex, toIndex)) { errorMessage = "Illegal Move: King is in check."; return null; }

                MakeMoveFast(fromIndex, toIndex);

                char pieceChar = '?';
                switch (movingPieceType)
                {
                    case 1: case 7: pieceChar = 'N'; break;
                    case 2: case 8: pieceChar = 'B'; break;
                    case 3: case 9: pieceChar = 'R'; break;
                    case 4: case 10: pieceChar = 'Q'; break;
                    case 5: case 11: pieceChar = 'K'; break;
                    default: pieceChar = '?'; break;
                }
                bool isPawn = (movingPieceType == 0 || movingPieceType == 6);
                string prefix = isPawn ? "" : pieceChar.ToString();

                string resultNotation;
                if (movingPieceType == 5 && Math.Abs(toIndex - fromIndex) == 2) resultNotation = (toIndex == 6) ? "0-0" : "0-0-0";
                else if (movingPieceType == 11 && Math.Abs(toIndex - fromIndex) == 2) resultNotation = (toIndex == 62) ? "0-0" : "0-0-0";
                else
                {
                    string captureMark = isCapture ? "x" : "";
                    resultNotation = string.Format("{0}{1}{2}{3}", prefix, coordsOnly.Substring(0, 2), captureMark, coordsOnly.Substring(2, 2));
                }

                int enemyKingSq = GetKingSquare(IsWhiteToMove);
                bool isCheck = IsSquareAttacked(enemyKingSq, !IsWhiteToMove);

                if (isCheck)
                {
                    if (IsCheckmate()) { resultNotation += "#"; errorMessage = "GAME OVER"; }
                    else { resultNotation += "+"; }
                }

                MoveHistory.Add(resultNotation);
                return resultNotation;
            }
            catch { errorMessage = "System Error."; return null; }
        }

        private bool IsMoveSafe(int from, int to)
        {
            bool wasWhiteTurn = IsWhiteToMove;
            (int captured, int moved, int oldRights, int oldEP) = MakeMoveFast(from, to);
            int myKingSq = GetKingSquare(wasWhiteTurn);
            bool isSelfCheck = IsSquareAttacked(myKingSq, IsWhiteToMove);
            UnmakeMoveFast(from, to, captured, moved, oldRights, oldEP);
            return !isSelfCheck;
        }

        public ulong GetWhitePieces() => Bitboards[0] | Bitboards[1] | Bitboards[2] | Bitboards[3] | Bitboards[4] | Bitboards[5];
        public ulong GetBlackPieces() => Bitboards[6] | Bitboards[7] | Bitboards[8] | Bitboards[9] | Bitboards[10] | Bitboards[11];
        public ulong GetAllPieces() => GetWhitePieces() | GetBlackPieces();

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
            if (sections.Length > 3 && sections[3] != "-")
            {
                EnPassantTarget = StringToIndex(sections[3]);
            }
        }

        public int GetPieceAtSquare(int squareIndex)
        {
            for (int i = 0; i < 12; i++)
            {
                ulong bit = 1UL << squareIndex;
                if ((Bitboards[i] & bit) != 0) return i;
            }
            return -1;
        }

        public int GetKingSquare(bool isWhite)
        {
            int kingType = isWhite ? 5 : 11;
            ulong kingBitboard = Bitboards[kingType];
            return TrailingZeroCount(kingBitboard);
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
            bool isWhite = IsWhiteToMove;
            int kingSquare = GetKingSquare(isWhite);
            if (!IsSquareAttacked(kingSquare, !isWhite)) return false;
            var moves = GetAllLegalMoves();
            return moves.Count == 0;
        }

        public void MovePiece(int from, int to)
        {
            MakeMoveFast(from, to);
        }

        public string IndexToString(int index) => string.Format("{0}{1}", (char)('a' + (index % 8)), (char)('1' + (index / 8)));
        public int StringToIndex(string square) => (square[1] - '1') * 8 + (square[0] - 'a');
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