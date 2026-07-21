using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ChessGame : UdonSharpBehaviour
{
    [UdonSynced] public byte[] squares = new byte[64];
    [UdonSynced] public int[] occupiedPlayerIds = new int[2];
    [UdonSynced] public byte sideToMove;
    [UdonSynced] public byte castlingRights;
    [UdonSynced] public byte enPassantSquare = 255;
    [UdonSynced] public byte halfmoveClock;
    [UdonSynced] public ushort fullmoveNumber;
    [UdonSynced] public byte result;
    [UdonSynced] public byte status;
    [UdonSynced] public int[] positionHistory = new int[100];
    [UdonSynced] public byte positionCount;
    public int localSeat;
    public int selectedSquare = -1;

    void Start()
    {
        if (Networking.IsOwner(gameObject) && fullmoveNumber == 0) ResetGame();
    }

    public void ResetGame()
    {
        Own();
        InitializeBoard();
        RequestSerialization();
    }

    void InitializeBoard()
    {
        for (int i = 0; i < 64; i++) squares[i] = 0;
        byte[] back = new byte[] { 4, 2, 3, 5, 6, 3, 2, 4 };
        for (int x = 0; x < 8; x++)
        {
            squares[x] = back[x];
            squares[8 + x] = 1;
            squares[48 + x] = 9;
            squares[56 + x] = (byte)(back[x] | 8);
        }
        sideToMove = 0;
        castlingRights = 15;
        enPassantSquare = 255;
        halfmoveClock = 0;
        fullmoveNumber = 1;
        result = 0;
        status = 0;
        positionCount = 0;
        RecordPosition();
    }

    public void JoinGame(int seat)
    {
        if (seat < 0 || seat > 1) return;
        Own();
        int playerId = Networking.LocalPlayer.playerId;
        if (occupiedPlayerIds[seat] != 0 && occupiedPlayerIds[seat] != playerId) return;
        localSeat = seat;
        occupiedPlayerIds[seat] = playerId;
        RequestSerialization();
    }

    public bool TryMove(int from, int to, int promotion)
    {
        if (result != 0 || from < 0 || from >= 64 || to < 0 || to >= 64) return false;
        if (localSeat != (sideToMove == 0 ? 0 : 1)) return false;
        byte piece = squares[from];
        if (piece == 0 || Color(piece) != sideToMove || !LegalMove(from, to, promotion)) return false;
        Own();
        byte captured = squares[to];
        int type = Type(piece);
        bool pawnMove = type == 1;
        int enPassantCapture = pawnMove && to == enPassantSquare && captured == 0 ? to + (sideToMove == 0 ? -8 : 8) : -1;
        if (enPassantCapture >= 0) captured = squares[enPassantCapture];
        ApplyMove(from, to, promotion);
        UpdateCastlingRights(from, to, piece, captured);
        enPassantSquare = pawnMove && Mathf.Abs(to - from) == 16 ? (byte)((to + from) / 2) : (byte)255;
        halfmoveClock = pawnMove || captured != 0 ? (byte)0 : (byte)(halfmoveClock + 1);
        if (sideToMove == 8) fullmoveNumber++;
        sideToMove = sideToMove == 0 ? (byte)8 : (byte)0;
        bool check = InCheck(sideToMove);
        bool moves = AnyLegalMove(sideToMove);
        status = check ? (byte)1 : (byte)0;
        if (!moves)
        {
            if (check)
            {
                status = 2;
                result = sideToMove == 0 ? (byte)2 : (byte)1;
            }
            else
            {
                status = 3;
                result = 3;
            }
        }
        if (result == 0 && (halfmoveClock >= 100 || InsufficientMaterial())) result = 3;
        RecordPosition();
        if (result == 0 && RepetitionCount(PositionHash()) >= 3) result = 3;
        RequestSerialization();
        return true;
    }

    public void Resign()
    {
        if (result != 0 || localSeat != (sideToMove == 0 ? 0 : 1)) return;
        Own();
        result = localSeat == 0 ? (byte)2 : (byte)1;
        RequestSerialization();
    }

    public void SelectSquare(int square)
    {
        if (selectedSquare < 0)
        {
            if (square >= 0 && square < 64 && squares[square] != 0 && Color(squares[square]) == sideToMove) selectedSquare = square;
            return;
        }
        int from = selectedSquare;
        selectedSquare = -1;
        int promotion = Type(squares[from]) == 1 && (square >> 3 == 0 || square >> 3 == 7) ? 5 : 0;
        TryMove(from, square, promotion);
    }

    public int VerifyRules()
    {
        int failures = 0;
        InitializeBoard();
        if (!LegalMove(12, 28, 0) || LegalMove(12, 36, 0) || !LegalMove(6, 21, 0)) failures++;
        squares[5] = 0;
        squares[6] = 0;
        if (!LegalMove(4, 6, 0)) failures++;
        for (int i = 0; i < 64; i++) squares[i] = 0;
        squares[63] = 14;
        squares[54] = 5;
        squares[45] = 6;
        if (!InCheck(8) || AnyLegalMove(8)) failures++;
        for (int i = 0; i < 64; i++) squares[i] = 0;
        squares[63] = 14;
        squares[53] = 6;
        squares[46] = 5;
        if (InCheck(8) || AnyLegalMove(8)) failures++;
        InitializeBoard();
        return failures;
    }

    bool LegalMove(int from, int to, int promotion)
    {
        if (!PseudoLegal(from, to, promotion)) return false;
        byte moving = squares[from];
        byte target = squares[to];
        int ep = Type(moving) == 1 && to == enPassantSquare && target == 0 ? to + (Color(moving) == 0 ? -8 : 8) : -1;
        byte epPiece = ep >= 0 ? squares[ep] : (byte)0;
        int rookFrom = -1;
        int rookTo = -1;
        byte rook = 0;
        if (Type(moving) == 6 && Mathf.Abs(to - from) == 2)
        {
            rookFrom = to > from ? from + 3 : from - 4;
            rookTo = to > from ? from + 1 : from - 1;
            rook = squares[rookFrom];
        }
        ApplyMove(from, to, promotion);
        bool check = InCheck(Color(moving));
        squares[from] = moving;
        squares[to] = target;
        if (ep >= 0) squares[ep] = epPiece;
        if (rookFrom >= 0)
        {
            squares[rookFrom] = rook;
            squares[rookTo] = 0;
        }
        return !check;
    }

    bool PseudoLegal(int from, int to, int promotion)
    {
        if (from == to) return false;
        byte piece = squares[from];
        byte target = squares[to];
        if (target != 0 && Color(target) == Color(piece)) return false;
        int fx = from & 7;
        int fy = from >> 3;
        int tx = to & 7;
        int ty = to >> 3;
        int dx = tx - fx;
        int dy = ty - fy;
        int ax = Mathf.Abs(dx);
        int ay = Mathf.Abs(dy);
        int type = Type(piece);
        if (type == 1)
        {
            int direction = Color(piece) == 0 ? 1 : -1;
            int startRank = Color(piece) == 0 ? 1 : 6;
            bool promotionRank = ty == 0 || ty == 7;
            if (promotionRank && (promotion < 2 || promotion > 5)) return false;
            if (!promotionRank && promotion != 0) return false;
            if (dx == 0 && dy == direction && target == 0) return true;
            if (dx == 0 && dy == direction * 2 && fy == startRank && target == 0 && squares[from + direction * 8] == 0) return true;
            return ax == 1 && dy == direction && (target != 0 || to == enPassantSquare);
        }
        if (type == 2) return ax * ay == 2;
        if (type == 3) return ax == ay && ClearPath(from, to);
        if (type == 4) return (dx == 0 || dy == 0) && ClearPath(from, to);
        if (type == 5) return (dx == 0 || dy == 0 || ax == ay) && ClearPath(from, to);
        if (ax <= 1 && ay <= 1) return true;
        if (type != 6 || ay != 0 || ax != 2 || InCheck(Color(piece))) return false;
        int right = Color(piece) == 0 ? (dx > 0 ? 1 : 2) : (dx > 0 ? 4 : 8);
        if ((castlingRights & right) == 0) return false;
        int step = dx > 0 ? 1 : -1;
        int rookSquare = dx > 0 ? from + 3 : from - 4;
        for (int square = from + step; square != rookSquare; square += step) if (squares[square] != 0) return false;
        return !SquareAttacked(from + step, Color(piece) == 0 ? 8 : 0) && !SquareAttacked(to, Color(piece) == 0 ? 8 : 0);
    }

    void ApplyMove(int from, int to, int promotion)
    {
        byte piece = squares[from];
        if (Type(piece) == 1 && to == enPassantSquare && squares[to] == 0) squares[to + (Color(piece) == 0 ? -8 : 8)] = 0;
        if (Type(piece) == 6 && Mathf.Abs(to - from) == 2)
        {
            int rookFrom = to > from ? from + 3 : from - 4;
            int rookTo = to > from ? from + 1 : from - 1;
            squares[rookTo] = squares[rookFrom];
            squares[rookFrom] = 0;
        }
        squares[to] = promotion == 0 ? piece : (byte)(Color(piece) | promotion);
        squares[from] = 0;
    }

    bool ClearPath(int from, int to)
    {
        int fx = from & 7;
        int fy = from >> 3;
        int tx = to & 7;
        int ty = to >> 3;
        int sx = tx == fx ? 0 : tx > fx ? 1 : -1;
        int sy = ty == fy ? 0 : ty > fy ? 1 : -1;
        int x = fx + sx;
        int y = fy + sy;
        while (x != tx || y != ty)
        {
            if (squares[y * 8 + x] != 0) return false;
            x += sx;
            y += sy;
        }
        return true;
    }

    bool InCheck(int color)
    {
        int king = -1;
        for (int square = 0; square < 64; square++) if (squares[square] == (byte)(color | 6)) king = square;
        return SquareAttacked(king, color == 0 ? 8 : 0);
    }

    bool SquareAttacked(int square, int attackerColor)
    {
        int x = square & 7;
        int y = square >> 3;
        int pawnY = y + (attackerColor == 0 ? -1 : 1);
        if (pawnY >= 0 && pawnY < 8)
        {
            if (x > 0 && squares[pawnY * 8 + x - 1] == (byte)(attackerColor | 1)) return true;
            if (x < 7 && squares[pawnY * 8 + x + 1] == (byte)(attackerColor | 1)) return true;
        }
        int[] knightX = new int[] { 1, 2, 2, 1, -1, -2, -2, -1 };
        int[] knightY = new int[] { 2, 1, -1, -2, -2, -1, 1, 2 };
        for (int i = 0; i < 8; i++)
        {
            int nx = x + knightX[i];
            int ny = y + knightY[i];
            if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8 && squares[ny * 8 + nx] == (byte)(attackerColor | 2)) return true;
        }
        int[] directionsX = new int[] { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] directionsY = new int[] { 0, 0, 1, -1, 1, -1, 1, -1 };
        for (int direction = 0; direction < 8; direction++)
        {
            int nx = x + directionsX[direction];
            int ny = y + directionsY[direction];
            int distance = 1;
            while (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
            {
                byte piece = squares[ny * 8 + nx];
                if (piece != 0)
                {
                    if (Color(piece) == attackerColor)
                    {
                        int type = Type(piece);
                        if (type == 5 || direction < 4 && type == 4 || direction >= 4 && type == 3 || distance == 1 && type == 6) return true;
                    }
                    break;
                }
                nx += directionsX[direction];
                ny += directionsY[direction];
                distance++;
            }
        }
        return false;
    }

    bool AnyLegalMove(int color)
    {
        for (int from = 0; from < 64; from++)
        {
            if (squares[from] == 0 || Color(squares[from]) != color) continue;
            for (int to = 0; to < 64; to++)
            {
                int promotion = Type(squares[from]) == 1 && (to >> 3 == 0 || to >> 3 == 7) ? 5 : 0;
                if (LegalMove(from, to, promotion)) return true;
            }
        }
        return false;
    }

    void UpdateCastlingRights(int from, int to, byte piece, byte captured)
    {
        if (Type(piece) == 6) castlingRights &= Color(piece) == 0 ? (byte)12 : (byte)3;
        if (from == 0 || to == 0) castlingRights &= 13;
        if (from == 7 || to == 7) castlingRights &= 14;
        if (from == 56 || to == 56) castlingRights &= 7;
        if (from == 63 || to == 63) castlingRights &= 11;
    }

    bool InsufficientMaterial()
    {
        int minor = 0;
        for (int square = 0; square < 64; square++)
        {
            int type = Type(squares[square]);
            if (type == 1 || type == 4 || type == 5) return false;
            if (type == 2 || type == 3) minor++;
        }
        return minor <= 1;
    }

    int PositionHash()
    {
        int hash = sideToMove | castlingRights << 8 | enPassantSquare << 16;
        for (int i = 0; i < 64; i++) hash = hash * 31 + squares[i];
        return hash;
    }

    void RecordPosition()
    {
        if (positionCount < positionHistory.Length) positionHistory[positionCount++] = PositionHash();
    }

    int RepetitionCount(int hash)
    {
        int count = 0;
        for (int i = 0; i < positionCount; i++) if (positionHistory[i] == hash) count++;
        return count;
    }

    int Type(byte piece)
    {
        return piece & 7;
    }

    int Color(byte piece)
    {
        return piece & 8;
    }

    void Own()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
    }
}
