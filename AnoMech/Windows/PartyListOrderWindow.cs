using System;
using System.Numerics;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

public sealed class PartyListOrderWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public PartyListOrderWindow(Plugin plugin) : base("隊伍列表順序###AnoMechPartyListOrder")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 80),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = configuration.CustomPartyListOrder;
        if (ImGui.Checkbox("自訂模擬隊伍列表順序", ref enabled))
        {
            configuration.CustomPartyListOrder = enabled;
            configuration.Save();
        }
        if (!enabled) return;
        ImGui.TextWrapped("只調整八人的顯示位置，不改角色分工。原生隊員編號與快捷鍵仍跟隨原隊員，不重新編號。");
        var order = PartyListOrderRules.Normalize(configuration.PartyListOrder);
        for (var i = 0; i < order.Length; i++)
        {
            ImGui.PushID(i);
            ImGui.TextUnformatted($"{i + 1}. {RoleName(order[i])}");
            ImGui.SameLine(100);
            ImGui.BeginDisabled(i == 0);
            if (ImGui.SmallButton("上移"))
            {
                (order[i - 1], order[i]) = (order[i], order[i - 1]);
                configuration.PartyListOrder = order;
                configuration.Save();
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(i == order.Length - 1);
            if (ImGui.SmallButton("下移"))
            {
                (order[i + 1], order[i]) = (order[i], order[i + 1]);
                configuration.PartyListOrder = order;
                configuration.Save();
            }
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        if (ImGui.Button("還原 MT、ST、H1、H2、D1–D4"))
        {
            configuration.PartyListOrder = Enum.GetValues<PartyRole>();
            configuration.Save();
        }
    }

    private static string RoleName(PartyRole role) => role switch
    {
        PartyRole.MainTank => "MT", PartyRole.OffTank => "ST",
        PartyRole.RegenHealer => "H1", PartyRole.ShieldHealer => "H2",
        PartyRole.MeleeDpsA => "D1", PartyRole.MeleeDpsB => "D2",
        PartyRole.PhysRangedDps => "D3", PartyRole.CasterDps => "D4",
        _ => role.ToString(),
    };
}
