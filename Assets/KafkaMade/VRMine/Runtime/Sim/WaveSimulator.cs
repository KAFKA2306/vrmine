using UdonSharp;

public class WaveSimulator : UdonSharpBehaviour
{
    public byte exitId;
    public byte colorId;
    public byte flags;
    public sbyte[] stepX = new sbyte[] { 0, 1, 0, -1 };
    public sbyte[] stepY = new sbyte[] { -1, 0, 1, 0 };
    public byte[] turn = new byte[] { 3, 0, 1, 2 };

    public void Simulate(byte entryId, byte[] board)
    {
        int width = NetConst.GridWidth;
        int height = NetConst.GridHeight;
        int x;
        int y;
        int dir = EntryDir(entryId, width, height, out x, out y);
        byte color = 0;
        flags = 0;
        exitId = 255;
        colorId = 0;
        for (int step = 0; step < 2048; step++)
        {
            x += stepX[dir];
            y += stepY[dir];
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                exitId = EdgeId(x, y, width, height);
                break;
            }
            byte cell = board[y * width + x];
            if (cell == 0) continue;
            if (cell == 16)
            {
                flags = NetConst.FlagAbsorb;
                colorId = color;
                return;
            }
            if (cell != 8) color = (byte)(color | cell);
            dir = turn[dir];
        }
        if (exitId == 255) flags = NetConst.FlagLoop;
        colorId = color;
    }

    int EntryDir(byte entryId, int width, int height, out int x, out int y)
    {
        if (entryId < 10)
        {
            x = entryId;
            y = -1;
            return 2;
        }
        if (entryId < 18)
        {
            x = width;
            y = entryId - 10;
            return 3;
        }
        if (entryId < 28)
        {
            x = width - 1 - (entryId - 18);
            y = height;
            return 0;
        }
        x = -1;
        y = height - 1 - (entryId - 28);
        return 1;
    }

    byte EdgeId(int x, int y, int width, int height)
    {
        if (x < 0) return (byte)(28 + (height - 1 - y));
        if (x >= width) return (byte)(10 + y);
        if (y < 0) return (byte)x;
        return (byte)(18 + (width - 1 - x));
    }
}
