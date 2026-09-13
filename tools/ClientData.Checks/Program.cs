using ClientData;

const string FellCleave = "對目標發動物理攻擊　<colortype(504)><edgecolortype(505)>威力：<edgecolortype(0)><colortype(0)><if([gnum68==21],<if([gnum72>=94],580,520)>,520)><br><if([gnum68==21],<if([gnum72>=80],該技能在<colortype(500)><edgecolortype(501)>原初的混沌<edgecolortype(0)><colortype(0)>狀態中，且<colortype(500)><edgecolortype(501)>獸魂<edgecolortype(0)><colortype(0)>在50點以上時變為狂魂<br>,)>,)><colortype(504)><edgecolortype(505)>發動條件：<edgecolortype(0)><colortype(0)><colortype(500)><edgecolortype(501)>獸魂<edgecolortype(0)><colortype(0)>50點";

void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

var war90 = MacroText.Evaluate(FellCleave, 21, 90);
Require(!war90.Unresolved, "Warrior 90 Fell Cleave must fully evaluate.");
Require(war90.Text == "對目標發動物理攻擊　威力：520\n該技能在原初的混沌狀態中，且獸魂在50點以上時變為狂魂\n發動條件：獸魂50點", $"Warrior 90 text wrong: {war90.Text}");
Require(MacroText.Evaluate(FellCleave, 21, 94).Text.Contains("威力：580"), "Level 94 branch must choose 580.");
var drk = MacroText.Evaluate(FellCleave, 32, 90);
Require(drk.Text.StartsWith("對目標發動物理攻擊　威力：520\n發動條件"), $"Other job must take else branches: {drk.Text}");
var unknownParam = MacroText.Evaluate("A<if([gnum69>=3],B,C)>D", 21, 90);
Require(unknownParam.Unresolved && unknownParam.Text == "A<if([gnum69>=3],B,C)>D", "Unsupported gnum must stay raw and be flagged.");
var unknownTag = MacroText.Evaluate("X<sheet(Action,1,0)>Y", 21, 90);
Require(unknownTag.Unresolved && unknownTag.Text == "X<sheet(Action,1,0)>Y", "Unknown tags must stay raw and be flagged.");
var plain = MacroText.Evaluate("猛攻的累積次數增加到3次", 21, 90);
Require(!plain.Unresolved && plain.Text == "猛攻的累積次數增加到3次", "Plain text must pass through.");
var unterminated = MacroText.Evaluate("A<if([gnum68==21],B", 21, 90);
Require(unterminated.Unresolved && unterminated.Text == "A<if([gnum68==21],B", "Unterminated tag must stay raw and be flagged.");
Console.WriteLine("PASS: macro evaluation for job/level conditions and fail-closed unknowns.");

var names = new Dictionary<string, uint> { ["狂暴"] = 38, ["原初的解放"] = 7389, ["原初"] = 1, ["地毀人亡"] = 3550, ["混沌旋風"] = 16463, ["旋風"] = 2, ["鋼鐵旋風"] = 51 };
Require(TraitUpgrades.Find("狂暴變為原初的解放", names).SequenceEqual(new[] { (38u, 7389u) }), "Longest target name must win.");
Require(TraitUpgrades.Find("鋼鐵旋風變為地毀人亡", names).SequenceEqual(new[] { (51u, 3550u) }), "Longest source name must win.");
Require(TraitUpgrades.Find("原初的混沌效果：獲得50點獸魂，地毀人亡變為混沌旋風", names).SequenceEqual(new[] { (3550u, 16463u) }), "Mid-sentence replacement must be found.");
Require(TraitUpgrades.Find("猛攻的累積次數增加到3次", names).Count == 0, "No 變為 means no replacement.");
Require(TraitUpgrades.Find("未知技能變為原初的解放", names).Count == 0, "Unknown source name must not produce a replacement.");

var mchNames = new Dictionary<string, uint> { ["暴雪"] = 142, ["火焰"] = 141, ["悖論"] = 25797 };
Require(TraitUpgrades.Find("暴雪與火焰變為悖論", mchNames).SequenceEqual(new[] { (142u, 25797u), (141u, 25797u) }), "「A與B變為C」must produce a pair for each listed source name, in text order.");
Require(TraitUpgrades.Find("暴雪、火焰變為悖論", mchNames).SequenceEqual(new[] { (142u, 25797u), (141u, 25797u) }), "「A、B變為C」must produce a pair for each listed source name, in text order.");
Console.WriteLine("PASS: trait text replacement detection.");

FieldValue[] local = [new("Action", 31, "CooldownGroup", "58"), new("Action", 31, "Recast100ms", "25"), new("Action", 52, "MaxCharges", "2")];
FieldValue[] remote = [new("Action", 31, "CooldownGroup", "58"), new("Action", 31, "Recast100ms", "30")];
var diffs = XivapiCompare.Diff(local, remote);
Require(diffs.SequenceEqual(new[] { new FieldDiff("Action", 31, "Recast100ms", "25", "30"), new FieldDiff("Action", 52, "MaxCharges", "2", null) }), "Diff must report changed and missing fields in local order.");
Require(XivapiCompare.PickVersion(["7.3", "7.31", "7.35"], new Dictionary<string, int> { ["7.3"] = 4, ["7.31"] = 1, ["7.35"] = 1 }) == "7.31", "Fewest diffs wins; ties keep earlier version.");
Console.WriteLine("PASS: xivapi field diff and version pick.");
