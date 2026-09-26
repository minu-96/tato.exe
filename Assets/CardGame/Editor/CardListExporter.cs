using System.Collections.Generic;
using System.Linq;
using System.Text;
using TatoGames.CardGame;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 카드 SO 에셋을 읽어 `Docs/카드목록.md`를 다시 만든다.
/// 메뉴: TatoGames ▸ Export Card List.
///
/// 수치를 고칠 때마다 다시 돌리면 문서가 낡지 않는다.
/// 표기는 게임 안에서 쓰는 <see cref="CardText"/>를 그대로 써서, 카드에 적힌 글과 어긋나지 않는다.
/// </summary>
public static class CardListExporter
{
    const string CardsDir = "Assets/CardGame/Data/Cards";
    const string OutPath = "Docs/카드목록.md";

    static readonly (AcquireSource src, string title, string axis)[] Groups =
    {
        (AcquireSource.Starter,       "시작덱",              "귀속 — 늙지도 썩지도 않는다"),
        (AcquireSource.Neulteona,     "늘어나라 pooo-tato",  "공격"),
        (AcquireSource.MoaMoa,        "모아모아 10tato",     "방어 (블록·효과방어)"),
        (AcquireSource.FieldSurvivor, "밭의 생존자",         "중독·뿌리"),
        (AcquireSource.Combat,        "전투 (메인 게임)",    "흐름 · 하이브리드 · 누적 방어 활용"),
    };

    static readonly string[] RarityName = { "일반", "희귀", "초월", "전설" };
    static readonly string[] TypeName = { "공격", "방어", "스킬", "뿌리" };

    [MenuItem("TatoGames/Export Card List")]
    public static void Export()
    {
        var cards = AssetDatabase.FindAssets("t:CardData", new[] { CardsDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .ToList();

        if (cards.Count == 0)
        {
            Debug.LogError("[TatoGames] 카드 에셋이 없습니다 — Generate MVP Cards 를 먼저 실행하세요");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("# tato.exe 카드 목록");
        sb.AppendLine();
        sb.AppendLine("> **이 문서는 자동 생성된다.** 손으로 고치지 말 것 —");
        sb.AppendLine("> `TatoGames ▸ Export Card List` 를 다시 실행하면 SO 에셋에서 새로 뽑는다.");
        sb.AppendLine("> 설계 의도는 [기획서](기획서.md), 구현은 [구현현황](구현현황.md) 참조.");
        sb.AppendLine();
        sb.AppendLine($"총 **{cards.Count}종**.");
        sb.AppendLine();

        foreach (var (src, title, axis) in Groups)
        {
            var group = cards.Where(c => c.source == src)
                             .OrderBy(c => (int)c.rarity).ThenBy(c => c.cost).ThenBy(c => c.displayName)
                             .ToList();
            if (group.Count == 0) continue;

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"## {title} ({group.Count}종)");
            sb.AppendLine();
            sb.AppendLine($"축: {axis}");
            sb.AppendLine();
            sb.AppendLine("| 이름 | 등급 | 타입 | 코스트 | 효과 (생) | 싹 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var c in group)
            {
                string kw = c.keyword == CardKeyword.None ? "" : $" 〈{CardText.Keyword(c.keyword)}〉";
                sb.AppendLine($"| **{c.displayName}** | {RarityName[(int)c.rarity]} | {TypeName[(int)c.type]} | " +
                              $"{c.cost} | {Line(c.effects)}{kw} | {Line(c.sproutEffects)} |");
            }
            sb.AppendLine();

            if (src == AcquireSource.Starter)
                sb.AppendLine("> 귀속이라 실제로는 **싹이 나지 않는다.** 위 싹 수치는 데이터에만 있고 쓰이지 않는다.\n");
        }

        AppendSummary(sb, cards);

        System.IO.Directory.CreateDirectory("Docs");
        System.IO.File.WriteAllText(OutPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[TatoGames] 카드 목록 {cards.Count}종 → {OutPath}");
    }

    /// <summary>효과 여러 줄을 한 칸에 담는다. 표기는 게임과 동일(CardText).</summary>
    static string Line(List<CardEffect> effects)
    {
        if (effects == null || effects.Count == 0) return "—";
        return string.Join(" + ", effects.Select(e =>
        {
            string s = CardText.Effect(e);
            // 힘·민첩처럼 자기에게 거는 버프는 표에서 구분이 필요하다
            if (e.type == EffectType.ApplyStatus && e.target == TargetType.Self) s += "(자신)";
            return s;
        }));
    }

    static void AppendSummary(StringBuilder sb, List<CardData> cards)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 분포");
        sb.AppendLine();
        sb.AppendLine("| 출처 | 일반 | 희귀 | 초월 | 전설 | 계 |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var (src, title, _) in Groups)
        {
            var g = cards.Where(c => c.source == src).ToList();
            if (g.Count == 0) continue;
            sb.Append($"| {title} ");
            for (int r = 0; r < 4; r++)
            {
                int n = g.Count(c => (int)c.rarity == r);
                sb.Append($"| {(n == 0 ? "–" : n.ToString())} ");
            }
            sb.AppendLine($"| **{g.Count}** |");
        }
        sb.Append("| **계** ");
        for (int r = 0; r < 4; r++) sb.Append($"| **{cards.Count(c => (int)c.rarity == r)}** ");
        sb.AppendLine($"| **{cards.Count}** |");
        sb.AppendLine();

        int exhaust = cards.Count(c => c.keyword == CardKeyword.Exhaust);
        var byCost = cards.GroupBy(c => c.cost).OrderBy(g => g.Key);
        var byType = Enumerable.Range(0, 4).Select(t => $"{TypeName[t]} {cards.Count(c => (int)c.type == t)}");

        sb.AppendLine($"- **소멸 {exhaust}종** — 쓰면 그 전투에서 다시 안 나온다");
        sb.AppendLine($"- 코스트: {string.Join(" / ", byCost.Select(g => $"{g.Key}코 {g.Count()}종"))} " +
                      "— 에너지가 2/턴이라 3코 이상은 만들지 않는다");
        sb.AppendLine($"- 타입: {string.Join(" / ", byType)}");

        // 아무 카드도 쓰지 않는 상태이상이 있으면 알려준다
        var used = new HashSet<StatusType>(cards.SelectMany(c => c.effects.Concat(c.sproutEffects))
                                                .Where(e => e.status != StatusType.None)
                                                .Select(e => e.status));
        var unused = System.Enum.GetValues(typeof(StatusType)).Cast<StatusType>()
                         .Where(s => s != StatusType.None && !used.Contains(s)).ToList();
        if (unused.Count > 0)
            sb.AppendLine($"- ⚠️ 아무 카드도 쓰지 않는 상태이상: **{string.Join(", ", unused.Select(CardText.Kor))}**");
    }
}
