using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StampedeChess.Core
{
    public class Board
    {
        public ulong[] Bitboards { get; private set; }
        public bool IsWhiteToMove { get; set; }

        // castling rights (bitmask: 1=WK, 2=WQ, 4=BK, 8=BQ)
        public int CastlingRights { get; set; }

        public Board()
        {
            Bitboards = new ulong[12];
            CastlingRights = 15; // 1111 (all allowed start)
            MoveTables.Init();
        }

        // optimized ai helpers
        // updated to return oldCastlingRights so we can undo properly
        public (int captured, int moved, int oldRights) MakeMoveFast(int from, int to)
        {
            int movingPiece = GetPieceAtSquare(from);
            int targetPiece = GetPieceAtSquare(to);
            int oldRights = CastlingRights; // save state

            if (movingPiece == -1) return (-1, -1, oldRights);

            // capture
            if (targetPiece != -1)
            {
                Bitboards[targetPiece] &= ~(1UL << to);

                // if rook captured, remove opponent castling right
                if (targetPiece == 3 || targetPiece == 9) UpdateCastlingRights(to);
            }

            // move piece
            Bitboards[movingPiece] &= ~(1UL << from);
            Bitboards[movingPiece] |= (1UL << to);

            // castling logic (king moves 2 squares)
            // white king (index 5) from e1(4) to g1(6) or c1(2)
            if (movingPiece == 5 && Math.Abs(to - from) == 2)
            {
                if (to == 6) // white short
                {
                    Bitboards[3] &= ~(1UL << 7); // remove rook h1
                    Bitboards[3] |= (1UL << 5);  // place rook f1
                }
                else if (to == 2) // white long
                {
                    Bitboards[3] &= ~(1UL << 0); // remove rook a1
                    Bitboards[3] |= (1UL << 3);  // place rook d1
                }
            }
            // black king (index 11) from e8(60) to g8(62) or c8(58)
            else if (movingPiece == 11 && Math.Abs(to - from) == 2)
            {
                if (to == 62) // black short
                {
                    Bitboards[9] &= ~(1UL << 63); // remove rook h8
                    Bitboards[9] |= (1UL << 61);  // place rook f8
                }
                else if (to == 58) // black long
                {
                    Bitboards[9] &= ~(1UL << 56); // remove rook a8
                    Bitboards[9] |= (1UL << 59);  // place rook d8
                }
            }

            // update rights if king or rook moved
            UpdateCastlingRights(from);

            IsWhiteToMove = !IsWhiteToMove;

            return (targetPiece, movingPiece, oldRights);
        }

        public void UnmakeMoveFast(int from, int to, int capturedPiece, int movingPiece, int oldRights)
        {
            if (movingPiece == -1) return;
            IsWhiteToMove = !IsWhiteToMove;

            // restore rights
            CastlingRights = oldRights;

            // move piece back
            Bitboards[movingPiece] &= ~(1UL << to);
            Bitboards[movingPiece] |= (1UL << from);

            // restore captured
            if (capturedPiece != -1) Bitboards[capturedPiece] |= (1UL << to);

            // un-castle (move rook back)
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

        // helper to strip rights
        private void UpdateCastlingRights(int square)
        {
            // if king or rook moves/captured, strip the bit
            // white
            if (square == 4 || square == 60) // kings
            {
                if (square == 4) CastlingRights &= ~3; // strip white both
                if (square == 60) CastlingRights &= ~12; // strip black both
            }

            // rooks
            if (square == 0) CastlingRights &= ~2; // strip white queen
            if (square == 7) CastlingRights &= ~1; // strip white king
            if (square == 56) CastlingRights &= ~8; // strip black queen
            if (square == 63) CastlingRights &= ~4; // strip black king
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
                        if ((legalBitmask & (1UL << target)) != 0) moves.Add((i, target));
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
            newBoard.CastlingRights = this.CastlingRights; // clone rights
            return newBoard;
        }

        // evaluation logic
        public float Evaluate()
        {
            float score = 0;
            for (int i = 0; i < 12; i++)
            {
                ulong bitboard = Bitboards[i];
                while (bitboard != 0)
                {
                    int square = TrailingZeroCount(bitboard);
                    float value = 0.0f;
                    switch (i)
                    {
                        case 0: case 6: value = 1.0f; break;
                        case 1: case 7: value = 3.0f; break;
                        case 2: case 8: value = 3.1f; break;
                        case 3: case 9: value = 5.0f; break;
                        case 4: case 10: value = 9.0f; break;
                        default: value = 200.0f; break;
                    }
                    if (square == 27 || square == 28 || square == 35 || square == 36) value += 0.2f;
                    else if (square >= 18 && square <= 21) value += 0.1f;

                    int rank = square / 8;
                    if (i <= 5) { if (rank >= 2) value += 0.05f * rank; }
                    else { if (rank <= 5) value += 0.05f * (7 - rank); }

                    if (i <= 5) score += value; else score -= value;
                    bitboard &= (bitboard - 1);
                }
            }
            return score;
        }

        private int TrailingZeroCount(ulong value)
        {
            if (value == 0) return 64;
            int count = 0;
            while ((value & 1) == 0) { value >>= 1; count++; }
            return count;
        }

        // main move logic
        public string MakeMove(string moveString, out string errorMessage)
        {

            errorMessage = "";
            if (string.IsNullOrWhiteSpace(moveString)) { errorMessage = "Empty input."; return null; }

            string rawInput = moveString.Trim();

            string lowerInput = rawInput.ToLower().Replace("0", "o");

            if (lowerInput == "o-o") // short castle (king side)
            {
                rawInput = IsWhiteToMove ? "e1g1" : "e8g8";
            }
            else if (lowerInput == "o-o-o") // long castle (queen side)
            {
                rawInput = IsWhiteToMove ? "e1c1" : "e8c8";
            }

            char firstChar = rawInput[0];
            string stringToParse = char.IsUpper(firstChar) ? rawInput.Substring(1) : rawInput;
            bool inputHasCapture = rawInput.Contains('x');

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

                if (isCapture)
                {
                    bool selfWhite = movingPieceType <= 5;
                    bool targetWhite = targetPieceType <= 5;
                    if (selfWhite == targetWhite) { errorMessage = "Cannot capture own piece."; return null; }
                }

                ulong legalMoves = MoveGenerator.GetPseudoLegalMoves(fromIndex, movingPieceType, this);
                if ((legalMoves & (1UL << toIndex)) == 0) { errorMessage = "Illegal Move (Geometry)."; return null; }

                // safety check
                if (!IsMoveSafe(fromIndex, toIndex))
                {
                    errorMessage = "Illegal Move: King is in check (or pinned).";
                    return null;
                }

                // execute actual move
                int oldRights = CastlingRights;

                if (isCapture) Bitboards[targetPieceType] &= ~(1UL << toIndex);
                Bitboards[movingPieceType] &= ~(1UL << fromIndex);
                Bitboards[movingPieceType] |= (1UL << toIndex);

                // handle castling rook move in main logic too
                if (movingPieceType == 5 && Math.Abs(toIndex - fromIndex) == 2)
                {
                    if (toIndex == 6) { Bitboards[3] &= ~(1UL << 7); Bitboards[3] |= (1UL << 5); }
                    if (toIndex == 2) { Bitboards[3] &= ~(1UL << 0); Bitboards[3] |= (1UL << 3); }
                }
                if (movingPieceType == 11 && Math.Abs(toIndex - fromIndex) == 2)
                {
                    if (toIndex == 62) { Bitboards[9] &= ~(1UL << 63); Bitboards[9] |= (1UL << 61); }
                    if (toIndex == 58) { Bitboards[9] &= ~(1UL << 56); Bitboards[9] |= (1UL << 59); }
                }

                // update rights
                UpdateCastlingRights(fromIndex);
                UpdateCastlingRights(toIndex); // also update if rook captured

                IsWhiteToMove = !IsWhiteToMove;

                // log formatting
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

                // check if castling for log
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

                // save history
                MoveHistory.Add(resultNotation);

                return resultNotation;
            }
            catch { errorMessage = "System Error."; return null; }
        }

        // king safety helper
        private bool IsMoveSafe(int from, int to)
        {
            bool wasWhiteTurn = IsWhiteToMove;
            (int captured, int moved, int oldRights) = MakeMoveFast(from, to);
            int myKingSq = GetKingSquare(wasWhiteTurn);
            bool isSelfCheck = IsSquareAttacked(myKingSq, IsWhiteToMove);
            UnmakeMoveFast(from, to, captured, moved, oldRights);
            return !isSelfCheck;
        }

        public List<string> MoveHistory { get; private set; } = new List<string>();

        public ulong GetWhitePieces() => Bitboards[0] | Bitboards[1] | Bitboards[2] | Bitboards[3] | Bitboards[4] | Bitboards[5];
        public ulong GetBlackPieces() => Bitboards[6] | Bitboards[7] | Bitboards[8] | Bitboards[9] | Bitboards[10] | Bitboards[11];
        public ulong GetAllPieces() => GetWhitePieces() | GetBlackPieces();

        public void LoadPosition(string fen)
        {
            Array.Clear(Bitboards, 0, 12);
            MoveHistory.Clear();
            string[] sections = fen.Split(' ');
            string boardLayout = sections[0];

            // load pieces
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

            // load castling rights (kqkq)
            CastlingRights = 0;
            if (sections.Length > 2)
            {
                string rights = sections[2];
                if (rights.Contains("K")) CastlingRights |= 1;
                if (rights.Contains("Q")) CastlingRights |= 2;
                if (rights.Contains("k")) CastlingRights |= 4;
                if (rights.Contains("q")) CastlingRights |= 8;
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
            // helper mostly for debug/testing
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