using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RoundsBots.Logic;

// Використання: PickReplay <human_cards.txt> <report.md>
string input = args[0];
string output = args[1];
string[] lines = File.ReadAllLines(input);

var offerHeader = new Regex(@"^picker=(\d+) time=([\d,\.]+)");
var offerLine = new Regex(@"^(\d+): (.+?) \[(\w+)\](?: mod=(\S+))?");
var pickLine = new Regex(@"^>>> PICK picker=(\d+) who=human card=(.+?) (?:mod=|role=)");

var picks = new List<(string session, List<string> offered, string pick, List<string> owned)>();
List<string>? offered = null;
var owned = new List<string>();
float lastTime = float.MaxValue;
int session = 0;
string lastPicker = "";

foreach (string raw in lines)
{
    var h = offerHeader.Match(raw);
    if (h.Success)
    {
        float time = float.Parse(h.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        if (time < lastTime - 1f && offered == null || h.Groups[1].Value != lastPicker || time < lastTime)
        {
            if (time < lastTime || h.Groups[1].Value != lastPicker)
            {
                session++;
                owned = new List<string>();
            }
        }

        lastTime = time;
        lastPicker = h.Groups[1].Value;
        offered = new List<string>();
        continue;
    }

    var o = offerLine.Match(raw);
    if (o.Success && offered != null)
    {
        offered.Add(o.Groups[2].Value.Trim());
        continue;
    }

    var p = pickLine.Match(raw);
    if (p.Success && offered != null && offered.Count > 0)
    {
        string pick = p.Groups[2].Value.Trim();
        picks.Add(($"S{session}", offered, pick, new List<string>(owned)));
        owned.Add(pick);
        offered = null;
    }
}

float ScoreOf(string name, List<string> have)
{
    float score = CardKnowledge.TryGet(name, out CardTraits t) && t.Features != null ? CardScoring.Score(t.Features) : 0f;
    int close = have.Count(c => CardKnowledge.TryGet(c, out CardTraits ct) && ct.CloseRange);
    var roles = have.Select(c => CardKnowledge.TryGet(c, out CardTraits rt) && rt.Features != null ? CardScoring.RoleOf(rt.Features) : CardRole.None).ToList();
    var role = CardKnowledge.TryGet(name, out CardTraits r2) && r2.Features != null ? CardScoring.RoleOf(r2.Features) : CardRole.None;
    return score
        + CardKnowledge.Adjustment(name, close, selfDamageEnabled: false)
        + CardScoring.BalanceAdjustment(role, roles)
        + CardKnowledge.DuplicateAdjustment(name, have.Count(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
        + CardSynergy.Adjustment(name, have);
}

var sb = new StringBuilder();
sb.AppendLine("# Вибори людини vs оцінка бота (поточна логіка, самошкода вимкнена)");
sb.AppendLine();
int agree = 0;
var rankSum = 0;
var pickedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var underrated = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
foreach (var (s, offer, pick, have) in picks)
{
    var scored = offer.Select(n => (n, sc: ScoreOf(n, have))).OrderByDescending(x => x.sc).ToList();
    int rank = scored.FindIndex(x => string.Equals(x.n, pick, StringComparison.OrdinalIgnoreCase)) + 1;
    if (rank == 1) agree++;
    rankSum += rank;
    pickedCounts[pick] = pickedCounts.GetValueOrDefault(pick) + 1;
    if (!underrated.ContainsKey(pick)) underrated[pick] = new List<int>();
    underrated[pick].Add(rank);
    known(pick);
    sb.AppendLine($"## {s}: взяв **{pick}** — ранг бота {rank}/{offer.Count}");
    sb.AppendLine($"Уже мав: {(have.Count == 0 ? "-" : string.Join(", ", have))}");
    sb.AppendLine();
    foreach (var (n, sc) in scored)
    {
        string mark = string.Equals(n, pick, StringComparison.OrdinalIgnoreCase) ? " ← людина" : "";
        string db = CardKnowledge.TryGet(n, out _) ? "" : " (нема в базі)";
        sb.AppendLine($"- {sc,6:0.00} {n}{db}{mark}");
    }

    sb.AppendLine();
}

void known(string _) { }

var head = new StringBuilder();
head.AppendLine($"Виборів: {picks.Count}. Бот узяв би те саме: {agree}. Середній ранг вибору людини: {(picks.Count == 0 ? 0 : (float)rankSum / picks.Count):0.0} з 8.");
head.AppendLine();
head.AppendLine("| Картка | Скільки разів узято | Ранги бота |");
head.AppendLine("|---|---|---|");
foreach (var kv in pickedCounts.OrderByDescending(k => k.Value))
{
    head.AppendLine($"| {kv.Key} | {kv.Value} | {string.Join(", ", underrated[kv.Key])} |");
}

head.AppendLine();
File.WriteAllText(output, sb.ToString().Replace("(поточна логіка, самошкода вимкнена)\n", "(поточна логіка, самошкода вимкнена)\n\n" + head), Encoding.UTF8);
Console.WriteLine(head.ToString());
