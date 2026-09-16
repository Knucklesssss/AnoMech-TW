using AnoMech.Core;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P5Sigma;

public sealed class TopP5SigmaSettingsWindow
{
    public TopP5SigmaStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("自動##resetall")) ResetAll();
        if (SettingsGrid.Begin("##p5sigma"))
        {
#if DEBUG
            DrawNewNorthA();
#endif
            DrawCloseFar();
            DrawTowerNorthFlip();
#if DEBUG
            DrawNewNorthB();
#endif
            DrawSpinnerRotation();
            DrawOmegaFForm();
            DrawHelloWorld();
            DrawDynamis();
            DrawMarkers();
            DrawPlayerSign();
            SettingsGrid.End();
        }
    }

    private void ResetAll()
    {
#if DEBUG
        Overrides.NewNorthA = null;
        Overrides.NewNorthB = null;
#endif
        Overrides.CloseFarTether = null;
        Overrides.TowerNorthFlip = null;
        Overrides.SpinnerRotation = null;
        Overrides.OmegaFForm = null;
        Overrides.HelloWorld = HelloWorldOption.Auto;
        Overrides.Dynamis = null;
        Overrides.Markers = MarkerMode.System;
        Overrides.PlayerSign = null;
    }

    private void DrawMarkers()
    {
        var v = Overrides.Markers;
        SettingsGrid.Row("標記方式：");
        if (ImGui.RadioButton("系統標##marks", v == MarkerMode.System)) Overrides.Markers = MarkerMode.System;
        ImGui.SameLine();
        if (ImGui.RadioButton("玩家手標##marks", v == MarkerMode.Manual)) Overrides.Markers = MarkerMode.Manual;
    }

    private static readonly (string Label, Sign? Sign)[] PlayerSigns =
    [
        ("自動", null), ("攻擊1", Sign.Attack1), ("攻擊2", Sign.Attack2), ("攻擊3", Sign.Attack3),
        ("攻擊4", Sign.Attack4), ("攻擊5", Sign.Attack5), ("禁止", Sign.Ignore1),
    ];

    private void DrawPlayerSign()
    {
        SettingsGrid.Row("我的標記 (僅莫古力)：");
        for (var i = 0; i < PlayerSigns.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (ImGui.RadioButton($"{PlayerSigns[i].Label}##psign", Overrides.PlayerSign == PlayerSigns[i].Sign))
                Overrides.PlayerSign = PlayerSigns[i].Sign;
        }
    }

#if DEBUG
    private void DrawNewNorthA()
    {
        SettingsGrid.Row("新北 (A — sigma 解法)：");
        if (ImGui.RadioButton("自動##northA", Overrides.NewNorthA == null)) Overrides.NewNorthA = null;
        foreach (var d in Direction.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##northA", Overrides.NewNorthA == d)) Overrides.NewNorthA = d;
        }
    }
#endif

    private void DrawCloseFar()
    {
        var v = Overrides.CloseFarTether;
        SettingsGrid.Row("連線距離：");
        if (ImGui.RadioButton("自動##cf",  v == null))            Overrides.CloseFarTether = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("中##cf", v == GlitchType.Mid))  Overrides.CloseFarTether = GlitchType.Mid;
        ImGui.SameLine();
        if (ImGui.RadioButton("遠##cf",   v == GlitchType.Far))    Overrides.CloseFarTether = GlitchType.Far;
    }

    private void DrawTowerNorthFlip()
    {
        var v = Overrides.TowerNorthFlip;
        SettingsGrid.Row("塔-北翻轉：");
        if (ImGui.RadioButton("自動##flip", v == null))  Overrides.TowerNorthFlip = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("是##flip",  v == true))  Overrides.TowerNorthFlip = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("否##flip",   v == false)) Overrides.TowerNorthFlip = false;
    }

#if DEBUG
    private void DrawNewNorthB()
    {
        SettingsGrid.Row("新北 (B — 後半)：");
        if (ImGui.RadioButton("自動##northB", Overrides.NewNorthB == null)) Overrides.NewNorthB = null;
        foreach (var d in Direction.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##northB", Overrides.NewNorthB == d)) Overrides.NewNorthB = d;
        }
    }
#endif

    private void DrawSpinnerRotation()
    {
        var v = Overrides.SpinnerRotation;
        SettingsGrid.Row("Spinner 旋轉方向：");
        if (ImGui.RadioButton("自動##spin", v == null))                       Overrides.SpinnerRotation = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("順時針##spin",   v == Rotation.Clockwise))         Overrides.SpinnerRotation = Rotation.Clockwise;
        ImGui.SameLine();
        if (ImGui.RadioButton("逆時針##spin",  v == Rotation.CounterClockwise))  Overrides.SpinnerRotation = Rotation.CounterClockwise;
    }

    private void DrawOmegaFForm()
    {
        var v = Overrides.OmegaFForm;
        SettingsGrid.Row("Omega-F 型態：");
        if (ImGui.RadioButton("自動##form",       v == null))                  Overrides.OmegaFForm = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Leg blades##form", v == OmegaAttack.Legs))  Overrides.OmegaFForm = OmegaAttack.Legs;
        ImGui.SameLine();
        if (ImGui.RadioButton("Staff##form",      v == OmegaAttack.Staff))      Overrides.OmegaFForm = OmegaAttack.Staff;
    }

    private void DrawHelloWorld()
    {
        var h = Overrides.HelloWorld;
        SettingsGrid.Row("Hello World：");
        if (ImGui.RadioButton("自動##hw", h == HelloWorldOption.Auto)) Overrides.HelloWorld = HelloWorldOption.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("近##hw", h == HelloWorldOption.Near)) Overrides.HelloWorld = HelloWorldOption.Near;
        ImGui.SameLine();
        if (ImGui.RadioButton("遠##hw",  h == HelloWorldOption.Far))  Overrides.HelloWorld = HelloWorldOption.Far;
        ImGui.SameLine();
        if (ImGui.RadioButton("無##hw", h == HelloWorldOption.No))   Overrides.HelloWorld = HelloWorldOption.No;
    }

    private void DrawDynamis()
    {
        var d = Overrides.Dynamis;
        SettingsGrid.Row("從 Dynamis 開始：");
        if (ImGui.RadioButton("自動##dyn", d == null))  Overrides.Dynamis = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("是##dyn",  d == true))  Overrides.Dynamis = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("否##dyn",   d == false)) Overrides.Dynamis = false;
    }
}
