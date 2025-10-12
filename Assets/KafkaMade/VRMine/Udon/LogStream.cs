using UdonSharp;

public class LogStream : UdonSharpBehaviour
{
    public const int EntrySize = 4;
    public byte head;
    public byte count;
    public byte[] data = new byte[NetConst.LogRingSize * EntrySize];
    public LogBoard view;

    public void Pull(byte valueHead, byte valueCount, byte[] source)
    {
        head = valueHead;
        count = valueCount;
        data = source;
        view.Render(this);
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
