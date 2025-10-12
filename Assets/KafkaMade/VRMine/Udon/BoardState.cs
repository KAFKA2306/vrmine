using UdonSharp;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BoardState : UdonSharpBehaviour
{
    [UdonSynced] public byte[] cells = new byte[NetConst.GridWidth * NetConst.GridHeight];
    public byte[] blocks = new byte[] { NetConst.ColorRed, NetConst.ColorBlue, NetConst.ColorYellow, 8, 8 };

    public uint Bake(uint seed)
    {
        uint s = seed;
        if (s == 0) s = 1;
        int size = cells.Length;
        for (int i = 0; i < size; i++) cells[i] = 0;
        int blockCount = blocks.Length;
        for (int i = 0; i < blockCount; i++)
        {
            for (int t = 0; t < 160; t++)
            {
                s = s * 1664525u + 1013904223u;
                int index = (int)(s % size);
                if (cells[index] != 0) continue;
                cells[index] = blocks[i];
                break;
            }
        }
        uint hash = 0;
        for (int i = 0; i < size; i++) hash = hash * 16777619u + cells[i];
        return hash;
    }

    public bool Matches(byte[] data)
    {
        int size = cells.Length;
        if (data.Length != size) return false;
        for (int i = 0; i < size; i++)
        {
            if (cells[i] != data[i]) return false;
        }
        return true;
    }
}
