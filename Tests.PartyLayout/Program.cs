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
        identities[i] = new HudMember { Index = (uint)i, EntityId = (uint)i + 1 };
        characters[i].EntityId = (uint)i + 1;
        party.Members[i] = new SimCharacter { BattleCharaPtr = &characters[i] };
    }
    void Check(bool custom, string stage)
    {
        for (var i = 0; i < 8; i++)
            if (nodes[i].Y != (custom ? 7 - i : i) * 40)
                throw new Exception($"{stage}: row {i} jumped to {nodes[i].Y}");
    }
    void Fire(AddonEvent evt) => Plugin.AddonLifecycle.Fire(evt);
    using (var layout = new PartyListLayout())
    {
        layout.Refresh(party);
        Fire(AddonEvent.PreDraw);
        Check(true, "Initial custom order");
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
        Plugin.Config.CustomPartyListOrder = true;
        Fire(AddonEvent.PostUpdate);
        Check(true, "Re-enable");
        layout.Clear();
        Check(false, "Leaving simulation restores native order");
    }
    if (Plugin.AddonLifecycle.ListenerCount != 0) throw new Exception("Leaked addon listeners");
    Console.WriteLine("PASS: party layout stays custom across debuff/cast updates and restores on exit.");
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
    public unsafe struct Member { public Component* PartyMemberComponent; }
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
