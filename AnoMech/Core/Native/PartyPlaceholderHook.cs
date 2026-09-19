using System;
using AnoMech.Core.Game.Party;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using InteropGenerator.Runtime;

namespace AnoMech.Core.Native;

// Resolve textual party numbers before marking/action commands consume the target.
// MainGroup stays intact, including its native local-player slot.
internal sealed unsafe class PartyPlaceholderHook : IDisposable
{
    private readonly PartyListLayout layout;
    private readonly Hook<PronounModule.Delegates.ResolvePlaceholder>? hook;

    public PartyPlaceholderHook(PartyListLayout layout)
    {
        this.layout = layout;
        var address = SignatureReport.TrackAddress("PronounModule.ResolvePlaceholder", PronounModule.Addresses.ResolvePlaceholder.Value);
        if (address == 0)
        {
            Plugin.Log.Warning("隊伍數字巨集對應功能無法啟用；投標請暫用 <mo> 或 <me>。");
            return;
        }
        hook = Plugin.GameInterop.HookFromAddress<PronounModule.Delegates.ResolvePlaceholder>(address, Resolve);
        hook.Enable();
    }

    private GameObject* Resolve(PronounModule* self, CStringPointer placeholder, byte unknown0, byte unknown1)
    {
        var target = placeholder.HasValue ? layout.ResolveNumberedTarget(placeholder.AsSpan()) : 0;
        return target != 0 ? (GameObject*)target : hook!.Original(self, placeholder, unknown0, unknown1);
    }

    public void Dispose() => hook?.Dispose();
}
