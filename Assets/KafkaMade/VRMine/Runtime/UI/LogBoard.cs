using UnityEngine;
using UnityEngine.UI;

public sealed class LogBoard : MonoBehaviour
{
    public Text[] rows;
    public Color normalColor = new Color(0.86f, 0.90f, 0.95f, 1f);
    public Color accentColor = new Color(0.55f, 0.92f, 1f, 1f);
    public Color warningColor = new Color(0.88f, 0.55f, 0.76f, 1f);
    public Color dimColor = new Color(0.68f, 0.72f, 0.80f, 1f);

    public void Render(LogStream stream)
    {
        if (rows == null || stream == null) return;
        int total = rows.Length;
        int count = stream.count;
        if (count > total) count = total;
        int start = stream.StartIndex();
        for (int i = 0; i < count; i++)
        {
            int slot = stream.Wrap(start + count - 1 - i);
            Text row = rows[i];
            if (row == null) continue;
            byte entry = stream.Read(slot, 0);
            byte exitId = stream.Read(slot, 1);
            byte color = stream.Read(slot, 2);
            byte flag = stream.Read(slot, 3);
            row.text = Format(entry, exitId, color, flag);
            row.color = ColorFor(entry, exitId, color, flag, i);
        }
        for (int i = count; i < total; i++) if (rows[i] != null) rows[i].text = "";
    }

    string Format(byte entry, byte exitId, byte color, byte flag)
    {
        if (entry == 255) return FormatSpecial(exitId, color, flag);
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
        if (color == NetConst.ColorRed) return "Red";
        if (color == NetConst.ColorBlue) return "Blue";
        if (color == NetConst.ColorYellow) return "Yellow";
        return "Clear";
    }

    string FormatSpecial(byte kind, byte value, byte flag)
    {
        if (kind == LogStream.KindRule) return "[RULE] Rule selected";
        if (kind == LogStream.KindPlay) return "[PLAY] Player " + value + " played card";
        if (kind == LogStream.KindTrick) return "[TRICK] Player " + value + " won trick";
        if (kind == LogStream.KindScore) return "[SCORE] Player " + value + " +1";
        if (kind == LogStream.KindWarning) return "[WARN] Invalid action";
        return "[PHASE] Round started";
    }

    Color ColorFor(byte entry, byte kind, byte color, byte flag, int index)
    {
        if (entry == 255 && kind == LogStream.KindWarning) return warningColor;
        if (index == 0) return accentColor;
        if (entry == 255) return normalColor;
        return dimColor;
    }
}
