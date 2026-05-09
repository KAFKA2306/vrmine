using UdonSharp;

public class LogStream : UdonSharpBehaviour
{
    public const int EntrySize = 4;
    public const byte KindStart = 1;
    public const byte KindRule = 2;
    public const byte KindPlay = 3;
    public const byte KindTrick = 4;
    public const byte KindScore = 5;
    public const byte KindWarning = 6;
    public byte head;
    public byte count;
    public byte[] data = new byte[NetConst.LogRingSize * EntrySize];

    public void Push(byte valueHead, byte valueCount, byte[] source)
    {
        head = valueHead;
        count = valueCount;
        data = source;
    }

    public int StartIndex()
    {
        int start = head - count;
        if (start < 0) start += NetConst.LogRingSize;
        return start;
    }

    public int Wrap(int value)
    {
        if (value >= NetConst.LogRingSize) value -= NetConst.LogRingSize;
        return value;
    }

    public byte Read(int slot, int lane)
    {
        return data[slot * EntrySize + lane];
    }
}
