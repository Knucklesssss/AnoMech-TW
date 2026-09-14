using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Diagnostics;

namespace AnoMech.Core.Native;

// Temporary probe: logs what _CastBar holds during a cast, so a real cast and a simulated one can be compared.
internal static unsafe class CastBarProbe
{
    private const int MaxLines = 60;
    private static long lastLog;
    private static int lines;
    private static uint detailedAction;
    private static bool detailed;

    public static void Tick()
    {
        if (lines >= MaxLines || !Plugin.LogManager.Enabled) return;
        var player = Control.GetLocalPlayer();
        if (player == null || !player->CastInfo.IsCasting) { detailed = false; return; }
        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(lastLog, now).TotalSeconds < 0.25) return;
        lastLog = now;
        lines++;

        var holder = AtkStage.Instance()->AtkArrayDataHolder;
        var numArr = holder == null ? null : holder->GetNumberArrayData((int)NumberArrayType.CastBar);
        var strArr = holder == null ? null : holder->GetStringArrayData((int)StringArrayType.CastBar);
        var ints = numArr == null || numArr->IntArray == null ? "null"
            : $"[{numArr->IntArray[0]},{numArr->IntArray[1]},{numArr->IntArray[2]},{numArr->IntArray[3]},{numArr->IntArray[4]},{numArr->IntArray[5]},{numArr->IntArray[6]}] upd={numArr->UpdateState}";
        var name = strArr == null || strArr->Size < 1 ? "null" : strArr->ManagedStringArray[0].ToString();
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("_CastBar", 1).Address;
        var ui = addon == null ? "addon=null"
            : $"visible={addon->IsVisible} root={(addon->RootNode == null ? "null" : $"{addon->RootNode->IsVisible()}/{addon->RootNode->Alpha_2}")}";
        var sim = Plugin.GameInstance?.World.Combat is { Active: true } ? "sim" : "real";
        Plugin.LogManager.LogSkill($"CastBarProbe {sim} action={player->CastInfo.ActionId} t={player->CastInfo.CurrentCastTime:0.00}/{player->CastInfo.TotalCastTime:0.00} ints={ints} ui={ui} name={name}");
        if (addon != null && !detailed && player->CastInfo.CurrentCastTime >= player->CastInfo.TotalCastTime / 2)
        {
            detailed = true;
            var nodes = new System.Text.StringBuilder();
            for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node == null) continue;
                nodes.Append($"{node->NodeId}:{(int)node->Type}:{(node->IsVisible() ? 1 : 0)}:{node->Alpha_2}:{node->Width}x{node->Height} ");
            }
            Plugin.LogManager.LogSkill($"CastBarProbe {sim} addon alpha={addon->Alpha} visFlags={addon->VisibilityFlags} showHide={addon->ShowHideFlags} pos={addon->X},{addon->Y} scale={addon->Scale:0.00} drawOrder={addon->DrawOrderIndex} depth={addon->DepthLayer} ready={addon->IsReady} nodes=[{nodes}]");
        }
    }
}
