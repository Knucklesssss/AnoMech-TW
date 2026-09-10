using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace AnoMech.Core;

// Chat feedback for things the player has to be told rather than shown: a mechanic they
// failed in a way the simulation cannot render, or the result of a command. Scenarios
// carry their own "[AnoMech]" prefix so the caller controls the whole line.
public static class ChatOutput
{
    public static void Coach(string text)
        => Plugin.ChatGui.Print(new XivChatEntry
        {
            Type = XivChatType.SystemMessage,
            Message = new SeStringBuilder().AddText(text).Build(),
        });
}
