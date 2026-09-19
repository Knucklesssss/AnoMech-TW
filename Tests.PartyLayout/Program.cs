using AnoMech;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using Dalamud.Game.Addon.Lifecycle;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

unsafe
{
    var nodes = stackalloc AtkResNode[8];
    var numbers = stackalloc AtkTextNode[8];
    var components = stackalloc Component[8];
    var members = stackalloc Member[8];
    var identities = stackalloc HudMember[8];
    var characters = stackalloc Character[8];
    var addon = new AddonPartyList { MemberCount = 8, PartyMembers = members };
    var hud = new AgentHUD { PartyMemberCount = 8, PartyMembers = identities };
    AgentHUD.Current = &hud;
    Plugin.GameGui.Address = (nint)(&addon);
    var party = new SimParty();
    for (var i = 0; i < 8; i++)
    {
        nodes[i] = new AtkResNode { Y = i * 40 };
        components[i].OwnerNode = &nodes[i];
        members[i].PartyMemberComponent = &components[i];
        numbers[i].SetText((i + 1).ToString());
        members[i].GroupSlotIndicator = &numbers[i];
        identities[i] = new HudMember { Index = (uint)i, EntityId = (uint)i + 1 };
        characters[i].EntityId = (uint)i + 1;
        party.Members[i] = new SimCharacter { BattleCharaPtr = &characters[i] };
    }
    void Check(bool custom, string stage)
    {
        for (var i = 0; i < 8; i++)
        {
            if (nodes[i].Y != (custom ? 7 - i : i) * 40)
                throw new Exception($"{stage}: row {i} jumped to {nodes[i].Y}");
            var number = System.Text.Encoding.UTF8.GetString(numbers[i].GetText().AsSpan());
            if (number != (custom ? 8 - i : i + 1).ToString())
                throw new Exception($"{stage}: displayed number {number} does not match the row.");
        }
    }
    void Fire(AddonEvent evt) => Plugin.AddonLifecycle.Fire(evt);
    using (var layout = new PartyListLayout())
    {
        layout.Refresh(party);
        Fire(AddonEvent.PreDraw);
        Check(true, "Initial custom order");
        for (var row = 0; row < 8; row++)
        {
            var token = System.Text.Encoding.UTF8.GetBytes($"<{row + 1}>");
            if (layout.ResolveNumberedTarget(token) != (nint)(&characters[7 - row]))
                throw new Exception($"Macro {row + 1} must follow the displayed row, not the original slot.");
        }
        foreach (var token in new[] { "<me>", "<mo>", "<t>", "<0>", "<9>", "<10>", "1", "<1>extra", "" })
            if (layout.ResolveNumberedTarget(System.Text.Encoding.UTF8.GetBytes(token)) != 0)
                throw new Exception($"Must leave {token} to the native resolver.");
        var order = Plugin.Config.PartyListOrder;
        (order[0], order[1]) = (order[1], order[0]);
        Fire(AddonEvent.PreDraw);
        if (layout.ResolveNumberedTarget("<1>"u8) != (nint)(&characters[6]) ||
            layout.ResolveNumberedTarget("<2>"u8) != (nint)(&characters[7]))
            throw new Exception("Changing the displayed order must update numeric macro targets.");
        (order[0], order[1]) = (order[1], order[0]);
        Fire(AddonEvent.PreDraw);
        for (var frame = 0; frame < 20; frame++)
        {
            Fire(AddonEvent.PreRequestedUpdate);
            Check(true, "Debuff update must not expose default order");
            // Model the native UI rebuilding row positions during an update.
            for (var i = 0; i < 8; i++) nodes[i].Y = i * 40;
            Fire(AddonEvent.PostRequestedUpdate);
            Check(true, "Debuff update completion");
            for (var i = 0; i < 8; i++) nodes[i].Y = i * 40;
            Fire(AddonEvent.PostUpdate);
            Check(true, "Cast update completion");
            Fire(AddonEvent.PreDraw);
            Check(true, "Draw after update");
        }
        identities[7].EntityId = 0;
        Fire(AddonEvent.PreDraw);
        Check(true, "Incomplete identities must not restore default order");
        identities[7].EntityId = 8;
        Plugin.Config.CustomPartyListOrder = false;
        Fire(AddonEvent.PostUpdate);
        Check(false, "Disable restores native order");
        if (layout.ResolveNumberedTarget("<1>"u8) != 0) throw new Exception("Disabled layout must use native targeting.");
        Plugin.Config.CustomPartyListOrder = true;
        Fire(AddonEvent.PostUpdate);
        Check(true, "Re-enable");
        // Native slot zero is the local player; give that actor the D4 role.
        (party.Members[0], party.Members[7]) = (party.Members[7], party.Members[0]);
        Plugin.Config.PartyListOrder = Enum.GetValues<PartyRole>();
        Fire(AddonEvent.PreDraw);
        if (nodes[0].Y != 7 * 40 ||
            System.Text.Encoding.UTF8.GetString(numbers[0].GetText().AsSpan()) != "8" ||
            layout.ResolveNumberedTarget("<8>"u8) != (nint)(&characters[0]))
            throw new Exception("Local D4 must occupy row 8, display 8 and resolve as <8>.");
        layout.Clear();
        Check(false, "Leaving simulation restores native order");
        if (layout.ResolveNumberedTarget("<1>"u8) != 0) throw new Exception("Leaving must restore native targeting.");
    }
    if (Plugin.AddonLifecycle.ListenerCount != 0) throw new Exception("Leaked addon listeners");
    Console.WriteLine("PASS: party layout and numeric macro targets follow displayed order, preserve other tokens and restore on exit.");
}

// Only the live-game boundary is substituted; the layout and event handlers above
// are the production implementation. This does not emulate the game's renderer.
namespace Dalamud.Game.Addon.Lifecycle
{
    public enum AddonEvent { PreRequestedUpdate, PostRequestedUpdate, PostUpdate, PreDraw, PreFinalize }
}
namespace Dalamud.Game.Addon.Lifecycle.AddonArgTypes
{
    public class AddonArgs { public AnoMech.AddonHandle Addon => new(Plugin.GameGui.Address); }
}
namespace AnoMech
{
    public record struct AddonHandle(nint Address);
    public class Gui
    {
        public nint Address;
        public AddonHandle GetAddonByName(string name, int index) => new(Address);
    }
    public class Config
    {
        public bool CustomPartyListOrder = true;
        public PartyRole[] PartyListOrder = Enum.GetValues<PartyRole>().Reverse().ToArray();
    }
    public class Lifecycle
    {
        private readonly List<(AddonEvent Event, Action<AddonEvent, Dalamud.Game.Addon.Lifecycle.AddonArgTypes.AddonArgs> Handler)> listeners = [];
        public int ListenerCount => listeners.Count;
        public void RegisterListener(AddonEvent evt, string name, Action<AddonEvent, Dalamud.Game.Addon.Lifecycle.AddonArgTypes.AddonArgs> handler) => listeners.Add((evt, handler));
        public void UnregisterListener(AddonEvent evt, string name, Action<AddonEvent, Dalamud.Game.Addon.Lifecycle.AddonArgTypes.AddonArgs> handler) => listeners.Remove((evt, handler));
        public void Fire(AddonEvent evt)
        {
            foreach (var listener in listeners.Where(x => x.Event == evt).ToArray()) listener.Handler(evt, new());
        }
    }
    public static class Plugin
    {
        public static readonly Gui GameGui = new();
        public static readonly Config Config = new();
        public static readonly Lifecycle AddonLifecycle = new();
    }
}
namespace AnoMech.Core.SimObjects
{
    public struct Character { public uint EntityId; }
    public unsafe class SimCharacter { public Character* BattleCharaPtr; }
    public class SimParty
    {
        public SimCharacter[] Members = new SimCharacter[8];
        public SimCharacter Get(int i) => Members[i];
    }
}
namespace FFXIVClientStructs.FFXIV.Component.GUI
{
    public unsafe struct TextPointer
    {
        public byte* Value;
        public ReadOnlySpan<byte> AsSpan()
        {
            var length = 0;
            while (Value[length] != 0) length++;
            return new(Value, length);
        }
    }
    public unsafe struct AtkTextNode
    {
        private fixed byte text[16];
        public TextPointer GetText() { fixed (byte* p = text) return new() { Value = p }; }
        public void SetText(string value) => SetText(System.Text.Encoding.UTF8.GetBytes(value));
        public void SetText(ReadOnlySpan<byte> value)
        {
            fixed (byte* p = text) { value.CopyTo(new Span<byte>(p, 15)); p[value.Length] = 0; }
        }
    }
    public unsafe struct AtkResNode
    {
        public float X, Y;
        public AtkResNode* ParentNode;
        public void SetPositionFloat(float x, float y) { X = x; Y = y; }
    }
}
namespace FFXIVClientStructs.FFXIV.Client.UI
{
    public unsafe struct Component { public AtkResNode* OwnerNode; }
    public unsafe struct Member { public Component* PartyMemberComponent; public AtkTextNode* GroupSlotIndicator; }
    public unsafe struct AddonPartyList
    {
        public int MemberCount;
        public Member* PartyMembers;
        public AtkResNode* LeaderMarkResNode;
        public AtkResNode* MpBarSpecialResNode;
        public AtkResNode* MpBarSpecialTextNode;
    }
}
namespace FFXIVClientStructs.FFXIV.Client.UI.Agent
{
    public struct HudMember { public uint Index, EntityId; }
    public unsafe struct AgentHUD
    {
        public static AgentHUD* Current;
        public static AgentHUD* Instance() => Current;
        public int PartyMemberCount;
        public HudMember* PartyMembers;
    }
}
