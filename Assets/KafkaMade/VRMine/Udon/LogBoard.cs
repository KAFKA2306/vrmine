using TMPro;
using UdonSharp;

public class LogBoard : UdonSharpBehaviour
{
    public TextMeshProUGUI[] rows;
    readonly string[] palette = { "Clear", "Red", "Blue", "Purple", "Yellow", "Orange", "Green", "Black" };

    public void Render(LogStream stream)
    {
        int total = rows.Length;
        int count = stream.count;
        if (count > total) count = total;
        int start = stream.StartIndex();
        for (int i = 0; i < count; i++)
        {
            int slot = stream.Wrap(start + i);
            rows[i].text = Format(stream.Read(slot, 0), stream.Read(slot, 1), stream.Read(slot, 2), stream.Read(slot, 3));
        }
        for (int i = count; i < total; i++) rows[i].text = "";
    }

    string Format(byte entry, byte exitId, byte color, byte flag)
    {
        return FormatEdge(entry) + " → " + FormatExit(exitId, flag) + " / " + FormatColor(color, flag);
    }

    string FormatExit(byte exitId, byte flag)
    {
        if ((flag & NetConst.FlagAbsorb) != 0) return "×";
        if ((flag & NetConst.FlagLoop) != 0) return "∞";
        return FormatEdge(exitId);
    }

    string FormatEdge(byte id)
    {
        if (id < 10) return "T" + (id + 1);
        if (id < 18) return "R" + (char)('A' + id - 10);
        if (id < 28) return "B" + (id - 17);
        return "L" + (char)('A' + id - 28);
    }

    string FormatColor(byte color, byte flag)
    {
        if ((flag & NetConst.FlagAbsorb) != 0) return "None";
        if ((flag & NetConst.FlagLoop) != 0) return "Loop";
        return palette[color];
    }
}
