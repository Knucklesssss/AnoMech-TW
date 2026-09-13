using System.Text;
using System.Text.RegularExpressions;

namespace ClientData;

// Lumina has no macro evaluator. Client descriptions for level-90 jobs only use
// colortype/edgecolortype/br and if([gnum68..]/[gnum72..]); everything else stays raw.
public static class MacroText
{
    private static readonly Regex Condition = new(@"^gnum(\d+)(==|>=)(\d+)$");

    public static (string Text, bool Unresolved) Evaluate(string macro, uint classJob, int level)
    {
        var unresolved = false;
        var text = Eval(macro, classJob, level, ref unresolved);
        return (text, unresolved);
    }

    private static string Eval(string s, uint job, int level, ref bool unresolved)
    {
        var sb = new StringBuilder();
        var i = 0;
        while (i < s.Length)
        {
            if (s[i] != '<') { sb.Append(s[i++]); continue; }
            var end = TagEnd(s, i);
            if (end < 0) { unresolved = true; sb.Append(s, i, s.Length - i); break; }
            var tag = s[i..(end + 1)];
            i = end + 1;
            if (tag == "<br>") sb.Append('\n');
            else if (tag.StartsWith("<colortype(") || tag.StartsWith("<edgecolortype(")) { }
            else if (tag.StartsWith("<if(") && TryIf(tag, job, level, ref unresolved, out var chosen)) sb.Append(chosen);
            else { unresolved = true; sb.Append(tag); }
        }
        return sb.ToString();
    }

    // Brackets are skipped so the '>' in ">=" does not close a tag.
    private static int TagEnd(string s, int start)
    {
        var depth = 0;
        for (var j = start; j < s.Length; j++)
        {
            if (s[j] == '[') { j = s.IndexOf(']', j); if (j < 0) return -1; continue; }
            if (s[j] == '<') depth++;
            else if (s[j] == '>' && --depth == 0) return j;
        }
        return -1;
    }

    private static bool TryIf(string tag, uint job, int level, ref bool unresolved, out string chosen)
    {
        chosen = "";
        if (!tag.EndsWith(")>") || tag.Length < 8 || tag[4] != '[') return false;
        var inner = tag[4..^2];
        var close = inner.IndexOf(']');
        if (close < 0 || close + 1 >= inner.Length || inner[close + 1] != ',') return false;
        var match = Condition.Match(inner[1..close]);
        if (!match.Success) return false;
        var value = int.Parse(match.Groups[3].Value);
        int? actual = match.Groups[1].Value switch { "68" => (int)job, "72" => level, _ => null };
        if (actual == null) return false;
        var args = SplitTopLevel(inner[(close + 2)..]);
        if (args == null) return false;
        var holds = match.Groups[2].Value == "==" ? actual == value : actual >= value;
        chosen = Eval(holds ? args.Value.Then : args.Value.Else, job, level, ref unresolved);
        return true;
    }

    private static (string Then, string Else)? SplitTopLevel(string s)
    {
        var depth = 0;
        var commas = new List<int>();
        for (var j = 0; j < s.Length; j++)
        {
            if (s[j] == '[') { j = s.IndexOf(']', j); if (j < 0) return null; continue; }
            if (s[j] == '<') depth++;
            else if (s[j] == '>') depth--;
            else if (s[j] == ',' && depth == 0) commas.Add(j);
        }
        return commas.Count == 1 ? (s[..commas[0]], s[(commas[0] + 1)..]) : null;
    }
}
