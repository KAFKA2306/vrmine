using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class OrapaMineGame : UdonSharpBehaviour
{
    public const int Width = 10;
    public const int Height = 8;
    public const int PieceCount = 5;
    [UdonSynced] public int[] occupiedPlayerIds = new int[5];
    [UdonSynced] public byte playerCount = 2;
    [UdonSynced] public byte currentSeat;
    [UdonSynced] public int winnerPlayerId;
    [UdonSynced] public uint puzzleSeed;
    [UdonSynced] public byte logCount;
    [UdonSynced] public byte[] logEntries = new byte[20];
    [UdonSynced] public byte[] logExits = new byte[20];
    [UdonSynced] public byte[] logColors = new byte[20];
    [UdonSynced] public byte[] logFlags = new byte[20];
    [UdonSynced] public byte[] attempts = new byte[5];
    public byte[] pieceTypes = new byte[PieceCount];
    public byte[] pieceX = new byte[PieceCount];
    public byte[] pieceY = new byte[PieceCount];
    public byte[] pieceRotation = new byte[PieceCount];
    public byte[] guessX = new byte[PieceCount];
    public byte[] guessY = new byte[PieceCount];
    public byte[] guessRotation = new byte[PieceCount];
    public byte resultExit;
    public byte resultColor;
    public byte resultFlags;
    public int localSeat = -1;
    public int selectedGuessPiece;

    void Start()
    {
        BuildPuzzle();
    }

    public override void OnDeserialization()
    {
        BuildPuzzle();
        if (!HasLocalSeat()) localSeat = -1;
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject) || player == null) return;
        bool changed = false;
        bool currentLeft = false;
        for (int i = 0; i < occupiedPlayerIds.Length; i++)
        {
            if (occupiedPlayerIds[i] != player.playerId) continue;
            occupiedPlayerIds[i] = 0;
            currentLeft |= i == currentSeat;
            changed = true;
        }
        if (!changed) return;
        if (currentLeft) currentSeat = (byte)NextActiveSeat(currentSeat);
        RequestSerialization();
    }

    public void ConfigurePlayers(int count)
    {
        Own();
        playerCount = (byte)Mathf.Clamp(count, 2, 5);
        for (int i = playerCount; i < occupiedPlayerIds.Length; i++) occupiedPlayerIds[i] = 0;
        if (currentSeat >= playerCount || occupiedPlayerIds[currentSeat] == 0) currentSeat = (byte)FirstActiveSeat();
        RequestSerialization();
    }

    public void ResetGame()
    {
        Own();
        puzzleSeed = (uint)Random.Range(1, int.MaxValue);
        winnerPlayerId = 0;
        currentSeat = (byte)FirstActiveSeat();
        logCount = 0;
        selectedGuessPiece = 0;
        for (int i = 0; i < attempts.Length; i++) attempts[i] = 0;
        for (int i = 0; i < PieceCount; i++)
        {
            guessX[i] = 0;
            guessY[i] = 0;
            guessRotation[i] = 0;
        }
        BuildPuzzle();
        RequestSerialization();
    }

    public void JoinGame(int seat)
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null || seat < 0 || seat >= playerCount) return;
        for (int i = 0; i < occupiedPlayerIds.Length; i++)
            if (occupiedPlayerIds[i] == player.playerId && i != seat) return;
        Own();
        if (occupiedPlayerIds[seat] != 0 && occupiedPlayerIds[seat] != player.playerId) return;
        bool noPlayers = OccupiedCount() == 0;
        localSeat = seat;
        occupiedPlayerIds[seat] = player.playerId;
        if (noPlayers || occupiedPlayerIds[currentSeat] == 0 || attempts[currentSeat] >= 2) currentSeat = (byte)seat;
        RequestSerialization();
    }

    public void QueryWave(int entry)
    {
        if (!CanAct() || entry < 0 || entry >= 36) return;
        Own();
        Simulate((byte)entry);
        int slot = logCount < 20 ? logCount : 19;
        if (logCount >= 20)
        {
            for (int i = 1; i < 20; i++)
            {
                logEntries[i - 1] = logEntries[i];
                logExits[i - 1] = logExits[i];
                logColors[i - 1] = logColors[i];
                logFlags[i - 1] = logFlags[i];
            }
        }
        else logCount++;
        logEntries[slot] = (byte)entry;
        logExits[slot] = resultExit;
        logColors[slot] = resultColor;
        logFlags[slot] = resultFlags;
        AdvanceTurn();
    }

    public byte QueryCell(int x, int y)
    {
        if (!CanAct() || x < 0 || x >= Width || y < 0 || y >= Height) return 0;
        Own();
        byte color = ColorAt(x + 0.5f, y + 0.5f);
        int slot = logCount < 20 ? logCount : 19;
        if (logCount >= 20)
        {
            for (int i = 1; i < 20; i++)
            {
                logEntries[i - 1] = logEntries[i];
                logExits[i - 1] = logExits[i];
                logColors[i - 1] = logColors[i];
                logFlags[i - 1] = logFlags[i];
            }
        }
        else logCount++;
        logEntries[slot] = (byte)(36 + y * Width + x);
        logExits[slot] = 255;
        logColors[slot] = color;
        logFlags[slot] = 4;
        AdvanceTurn();
        return color;
    }

    public bool SubmitGuess()
    {
        if (!CanAct()) return false;
        Own();
        bool match = true;
        for (int i = 0; i < PieceCount; i++)
            if (guessX[i] != pieceX[i] || guessY[i] != pieceY[i] || (guessRotation[i] & 3) != pieceRotation[i]) match = false;
        if (match) winnerPlayerId = occupiedPlayerIds[localSeat];
        else
        {
            attempts[localSeat]++;
            AdvanceTurn(false);
        }
        RequestSerialization();
        return match;
    }

    public void SelectGuessPiece(int piece)
    {
        if (!HasLocalSeat()) return;
        selectedGuessPiece = Mathf.Clamp(piece, 0, PieceCount - 1);
    }

    public void MoveGuess(int dx, int dy)
    {
        if (!HasLocalSeat()) return;
        int x = Mathf.Clamp(guessX[selectedGuessPiece] + dx, 0, Width - 1);
        int y = Mathf.Clamp(guessY[selectedGuessPiece] + dy, 0, Height - 1);
        guessX[selectedGuessPiece] = (byte)x;
        guessY[selectedGuessPiece] = (byte)y;
    }

    public void RotateGuess()
    {
        if (!HasLocalSeat()) return;
        guessRotation[selectedGuessPiece] = (byte)((guessRotation[selectedGuessPiece] + 1) & 3);
    }

    public void Simulate(byte entry)
    {
        float x;
        float y;
        float dx;
        float dy;
        Entry(entry, out x, out y, out dx, out dy);
        int color = 0;
        resultExit = 255;
        resultFlags = 0;
        for (int bounce = 0; bounce < 64; bounce++)
        {
            float boundary = BoundaryDistance(x, y, dx, dy);
            float nearest = boundary;
            int hitPiece = -1;
            float hitEdgeX = 0f;
            float hitEdgeY = 0f;
            for (int piece = 0; piece < PieceCount; piece++)
            {
                int vertices = VertexCount(pieceTypes[piece]);
                for (int edge = 0; edge < vertices; edge++)
                {
                    float ax;
                    float ay;
                    float bx;
                    float by;
                    Vertex(piece, edge, out ax, out ay);
                    Vertex(piece, (edge + 1) % vertices, out bx, out by);
                    float ex = bx - ax;
                    float ey = by - ay;
                    float cross = dx * ey - dy * ex;
                    if (Mathf.Abs(cross) < 0.0001f) continue;
                    float qx = ax - x;
                    float qy = ay - y;
                    float distance = (qx * ey - qy * ex) / cross;
                    float segment = (qx * dy - qy * dx) / cross;
                    if (distance <= 0.001f || distance >= nearest || segment < -0.0001f || segment > 1.0001f) continue;
                    nearest = distance;
                    hitPiece = piece;
                    hitEdgeX = ex;
                    hitEdgeY = ey;
                }
            }
            if (hitPiece < 0)
            {
                x += dx * boundary;
                y += dy * boundary;
                resultExit = ExitId(x, y);
                resultColor = (byte)color;
                return;
            }
            x += dx * nearest;
            y += dy * nearest;
            byte type = pieceTypes[hitPiece];
            if (type == 6)
            {
                resultFlags = 1;
                resultColor = (byte)color;
                return;
            }
            if (type == 0) color |= 1;
            else if (type == 1) color |= 2;
            else if (type == 2) color |= 4;
            else if (type == 3 || type == 4) color |= 8;
            float length = Mathf.Sqrt(hitEdgeX * hitEdgeX + hitEdgeY * hitEdgeY);
            float nx = -hitEdgeY / length;
            float ny = hitEdgeX / length;
            float dot = dx * nx + dy * ny;
            dx -= 2f * dot * nx;
            dy -= 2f * dot * ny;
            dx = Mathf.Round(dx);
            dy = Mathf.Round(dy);
            x += dx * 0.002f;
            y += dy * 0.002f;
        }
        resultFlags = 2;
        resultColor = (byte)color;
    }

    public int VerifySimulation()
    {
        int failures = 0;
        for (int i = 0; i < PieceCount; i++)
        {
            pieceTypes[i] = 1;
            pieceX[i] = 200;
            pieceY[i] = 200;
            pieceRotation[i] = 0;
        }
        pieceX[0] = 4;
        pieceY[0] = 2;
        Simulate(4);
        if (resultExit != 4 || resultColor != 2 || resultFlags != 0) failures++;
        pieceTypes[0] = 2;
        Simulate(5);
        if (resultColor != 4 || resultFlags != 0 || resultExit == 23) failures++;

        byte savedCount = playerCount;
        int[] savedPlayers = new int[occupiedPlayerIds.Length];
        byte[] savedAttempts = new byte[attempts.Length];
        for (int i = 0; i < occupiedPlayerIds.Length; i++)
        {
            savedPlayers[i] = occupiedPlayerIds[i];
            savedAttempts[i] = attempts[i];
            occupiedPlayerIds[i] = 0;
            attempts[i] = 0;
        }
        playerCount = 5;
        occupiedPlayerIds[0] = 10;
        occupiedPlayerIds[3] = 30;
        if (NextActiveSeat(0) != 3 || NextActiveSeat(3) != 0) failures++;
        attempts[3] = 2;
        if (NextActiveSeat(0) != 0) failures++;
        playerCount = savedCount;
        for (int i = 0; i < occupiedPlayerIds.Length; i++)
        {
            occupiedPlayerIds[i] = savedPlayers[i];
            attempts[i] = savedAttempts[i];
        }
        BuildPuzzle();
        return failures;
    }

    void BuildPuzzle()
    {
        uint seed = puzzleSeed == 0 ? 137u : puzzleSeed;
        pieceTypes[0] = 0;
        pieceTypes[1] = 1;
        pieceTypes[2] = 2;
        pieceTypes[3] = 3;
        pieceTypes[4] = 4;
        byte shift = (byte)(seed - seed / 3u * 3u);
        pieceX[0] = (byte)(1 + shift);
        pieceY[0] = 1;
        pieceRotation[0] = 0;
        pieceX[1] = 6;
        pieceY[1] = 1;
        pieceRotation[1] = 0;
        pieceX[2] = 8;
        pieceY[2] = 5;
        pieceRotation[2] = 2;
        pieceX[3] = 4;
        pieceY[3] = 7;
        pieceRotation[3] = 3;
        pieceX[4] = 9;
        pieceY[4] = 2;
        pieceRotation[4] = 1;
    }

    void Entry(byte entry, out float x, out float y, out float dx, out float dy)
    {
        if (entry < 10)
        {
            x = entry + 0.5f;
            y = 0f;
            dx = 0f;
            dy = 1f;
            return;
        }
        if (entry < 18)
        {
            x = Width;
            y = entry - 10 + 0.5f;
            dx = -1f;
            dy = 0f;
            return;
        }
        if (entry < 28)
        {
            x = Width - (entry - 18) - 0.5f;
            y = Height;
            dx = 0f;
            dy = -1f;
            return;
        }
        x = 0f;
        y = Height - (entry - 28) - 0.5f;
        dx = 1f;
        dy = 0f;
    }

    float BoundaryDistance(float x, float y, float dx, float dy)
    {
        if (dx > 0f) return (Width - x) / dx;
        if (dx < 0f) return -x / dx;
        if (dy > 0f) return (Height - y) / dy;
        return -y / dy;
    }

    byte ExitId(float x, float y)
    {
        if (y <= 0.001f) return (byte)Mathf.Clamp(Mathf.FloorToInt(x), 0, 9);
        if (x >= Width - 0.001f) return (byte)(10 + Mathf.Clamp(Mathf.FloorToInt(y), 0, 7));
        if (y >= Height - 0.001f) return (byte)(18 + 9 - Mathf.Clamp(Mathf.FloorToInt(x), 0, 9));
        return (byte)(28 + 7 - Mathf.Clamp(Mathf.FloorToInt(y), 0, 7));
    }

    byte ColorAt(float x, float y)
    {
        for (int piece = 0; piece < PieceCount; piece++) if (InsidePiece(piece, x, y)) return PieceColor(pieceTypes[piece]);
        return 0;
    }

    bool InsidePiece(int piece, float x, float y)
    {
        bool inside = false;
        int count = VertexCount(pieceTypes[piece]);
        float ax;
        float ay;
        Vertex(piece, count - 1, out ax, out ay);
        for (int i = 0; i < count; i++)
        {
            float bx;
            float by;
            Vertex(piece, i, out bx, out by);
            if ((by > y) != (ay > y) && x < (ax - bx) * (y - by) / (ay - by) + bx) inside = !inside;
            ax = bx;
            ay = by;
        }
        return inside;
    }

    void Vertex(int piece, int index, out float x, out float y)
    {
        byte type = pieceTypes[piece];
        float px = 0f;
        float py = 0f;
        if (type == 0)
        {
            if (index == 1) px = 2f;
            else if (index == 2) { px = 3f; py = 1f; }
            else if (index == 3) { px = 1f; py = 1f; }
        }
        else if (type == 1 || type == 5)
        {
            if (index == 1) px = 2f;
            else if (index == 2) { px = 2f; py = 2f; }
            else if (index == 3) py = 2f;
        }
        else if (type == 6)
        {
            if (index == 1) px = 2f;
            else if (index == 2) { px = 2f; py = 1f; }
            else if (index == 3) py = 1f;
        }
        else
        {
            float size = type == 3 ? 4f : type == 4 ? 3f : 2f;
            if (index == 1) px = size;
            else if (index == 2) py = size;
        }
        int rotation = pieceRotation[piece] & 3;
        for (int i = 0; i < rotation; i++)
        {
            float swap = px;
            px = -py;
            py = swap;
        }
        x = pieceX[piece] + px;
        y = pieceY[piece] + py;
    }

    int VertexCount(byte type)
    {
        return type == 2 || type == 3 || type == 4 ? 3 : 4;
    }

    byte PieceColor(byte type)
    {
        if (type == 0) return 1;
        if (type == 1) return 2;
        if (type == 2) return 4;
        if (type == 3 || type == 4) return 8;
        if (type == 6) return 16;
        return 0;
    }

    bool CanAct()
    {
        return winnerPlayerId == 0 && HasLocalSeat() && localSeat == currentSeat && attempts[localSeat] < 2;
    }

    bool HasLocalSeat()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        return player != null && localSeat >= 0 && localSeat < playerCount && occupiedPlayerIds[localSeat] == player.playerId;
    }

    int OccupiedCount()
    {
        int count = 0;
        for (int i = 0; i < playerCount; i++) if (occupiedPlayerIds[i] != 0) count++;
        return count;
    }

    int FirstActiveSeat()
    {
        for (int i = 0; i < playerCount; i++) if (occupiedPlayerIds[i] != 0 && attempts[i] < 2) return i;
        return 0;
    }

    int NextActiveSeat(int from)
    {
        for (int offset = 1; offset <= playerCount; offset++)
        {
            int seat = (from + offset) % playerCount;
            if (occupiedPlayerIds[seat] != 0 && attempts[seat] < 2) return seat;
        }
        return from < playerCount ? from : 0;
    }

    void AdvanceTurn()
    {
        AdvanceTurn(true);
    }

    void AdvanceTurn(bool serialize)
    {
        currentSeat = (byte)NextActiveSeat(currentSeat);
        if (serialize) RequestSerialization();
    }

    void Own()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player != null && !Networking.IsOwner(gameObject)) Networking.SetOwner(player, gameObject);
    }
}