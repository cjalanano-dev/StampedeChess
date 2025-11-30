using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace StampedeChess.Core
{
    public class Board
    {
        public ulong[] Bitboards { get; private set; }
        public bool IsWhiteToMove { get; set; }

        public Board()
        {
            Bitboards = new ulong[12];
            MoveTables.Init();
        }

        // returns true if the move is legal and doesn't leave king in check
        private bool IsMoveSafe(int from, int to)
        {
            // rember state
            bool wasWhiteTurn = IsWhiteToMove;

            // make the move
            (int captured, int moved) = MakeMoveFast(from, to);

            // check if the king is under attack
            int myKingSq = GetKingSquare(wasWhiteTurn);

            bool isSelfCheck = IsSquareAttacked(myKingSq, IsWhiteToMove);

            // undo the move
            UnmakeMoveFast(from, to, captured, moved);

            return !isSelfCheck;
        }

        // ai move logic
        public (int captured, int moved) MakeMoveFast(int from, int to)
        {
            int movingPiece = GetPieceAtSquare(from);
            int targetPiece = GetPieceAtSquare(to);

            if (movingPiece == -1) return (-1, -1);

            if (targetPiece != -1) Bitboards[targetPiece] &= ~(1UL << to);

            Bitboards[movingPiece] &= ~(1UL << from);
            Bitboards[movingPiece] |= (1UL << to);

            IsWhiteToMove = !IsWhiteToMove;

            return (targetPiece, movingPiece);
        }

        public void UnmakeMoveFast(int from, int to, int capturedPiece, int movingPiece)
        {
            if (movingPiece == -1) return;
            IsWhiteToMove = !IsWhiteToMove;

            Bitboards[movingPiece] &= ~(1UL << to);
            Bitboards[movingPiece] |= (1UL << from);

            if (capturedPiece != -1) Bitboards[capturedPiece] |= (1UL << to);
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
                            if (IsMoveSafe(i, target))
                            {
                                moves.Add((i, target));
                            }
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

        // trailing zero count helper
        private int TrailingZeroCount(ulong value)
        {
            if (value == 0) return 64;
            int count = 0;
            while ((value & 1) == 0) { value >>= 1; count++; }
            return count;
        }

        // main move logic & store moves data
        public List<string> MoveHistory { get; private set; } = new List<string>();

        public string MakeMove(string moveString, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(moveString)) { errorMessage = "Empty input."; return null; }

            string rawInput = moveString.Trim();
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

                if (isCapture)
                {
                    bool selfWhite = movingPieceType <= 5;
                    bool targetWhite = targetPieceType <= 5;
                    if (selfWhite == targetWhite) { errorMessage = "Cannot capture own piece."; return null; }
                }

                ulong legalMoves = MoveGenerator.GetPseudoLegalMoves(fromIndex, movingPieceType, this);
                if ((legalMoves & (1UL << toIndex)) == 0) { errorMessage = "Illegal Move (Geometry)."; return null; }

                // we simulate the move 
                // if king is in check after move
                // then illegal
                if (!IsMoveSafe(fromIndex, toIndex))
                {
                    errorMessage = "Illegal Move: King is in check (or pinned).";
                    return null;
                }

                // executes move
                if (isCapture) Bitboards[targetPieceType] &= ~(1UL << toIndex);
                Bitboards[movingPieceType] &= ~(1UL << fromIndex);
                Bitboards[movingPieceType] |= (1UL << toIndex);

                IsWhiteToMove = !IsWhiteToMove;

                // logs formatting
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
                string captureMark = isCapture ? "x" : "";
                string resultNotation = string.Format("{0}{1}{2}{3}", prefix, coordsOnly.Substring(0, 2), captureMark, coordsOnly.Substring(2, 2));

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

        // helper methods
        public ulong GetWhitePieces() => Bitboards[0] | Bitboards[1] | Bitboards[2] | Bitboards[3] | Bitboards[4] | Bitboards[5];
        public ulong GetBlackPieces() => Bitboards[6] | Bitboards[7] | Bitboards[8] | Bitboards[9] | Bitboards[10] | Bitboards[11];
        public ulong GetAllPieces() => GetWhitePieces() | GetBlackPieces();

        public void LoadPosition(string fen)
        {
            Array.Clear(Bitboards, 0, 12);
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
            // checkmate IF the king is in check AND there are 0 legal moves
            bool isWhite = IsWhiteToMove;
            int kingSquare = GetKingSquare(isWhite);

            if (!IsSquareAttacked(kingSquare, !isWhite)) return false;

            // the method GetAllLegalMoves() now filters out moves that leave king in check.
            // and if count is 0, it is mate.
            var validMoves = GetAllLegalMoves();
            return validMoves.Count == 0;
        }

        public void MovePiece(int from, int to)
        {
            int movingPiece = GetPieceAtSquare(from);
            int targetPiece = GetPieceAtSquare(to);
            if (targetPiece != -1) Bitboards[targetPiece] &= ~(1UL << to);
            Bitboards[movingPiece] &= ~(1UL << from);
            Bitboards[movingPiece] |= (1UL << to);
            IsWhiteToMove = !IsWhiteToMove;
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