using System.Collections.Generic;
using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 카드·상태이상 표기 공용 포맷터 (UI 여러 곳에서 같은 문구를 쓰도록).
    ///
    /// 용어는 하나로 통일한다 — 처음 하는 사람은 같은 것을 두 이름으로 부르면 다른 것으로 안다.
    ///   · <b>블록</b> = 공격 방어 (공격 피해를 막는 수치)
    ///   · <b>효과 방어</b> = 디버프를 건수로 막는 방어
    /// </summary>
    public static class CardText
    {
        public static string Kor(StatusType s) => s switch
        {
            StatusType.Vulnerable => "취약",
            StatusType.Weak => "약화",
            StatusType.Poison => "중독",
            StatusType.Strength => "힘",
            StatusType.Dexterity => "민첩",
            StatusType.Regen => "재생",
            _ => s.ToString(),
        };

        public static string Effect(CardEffect e) => e.type switch
        {
            EffectType.Damage => e.hits > 1 ? $"{e.value} 피해 ×{e.hits}" : $"{e.value} 피해",
            EffectType.Block => $"{e.value} 블록",
            EffectType.EffectDefense => $"효과 방어 +{e.value}",
            // 자기에게 거는 건 '+', 적에게 거는 건 '부여' — 누구에게 걸리는지 글만 보고 알 수 있게
            EffectType.ApplyStatus => e.target == TargetType.Self ? $"{Kor(e.status)} +{e.value}"
                                                                  : $"{Kor(e.status)} {e.value} 부여",
            EffectType.DrawPerRemainingEnergy => e.value == 1 ? "남은 에너지만큼 카드 뽑기"
                                                              : $"남은 에너지×{e.value}장 카드 뽑기",
            EffectType.TemporaryBlock => $"{e.value} 블록 (이번 턴만)",
            EffectType.ReflectHalfDamage => "다음 적 턴에 받는 피해의 절반 반사",
            EffectType.DisableBlockThisTurn => "이번 턴 블록 불가",
            EffectType.AmplifyPoisonPerTurn => $"적이 받는 중독 피해 +{e.value}",
            EffectType.ApplyStatusForTurns => $"{e.duration}턴 동안 매 턴 {Kor(e.status)} {e.value} 부여",
            EffectType.Heal => $"체력 {e.value} 회복",
            EffectType.Draw => $"카드 {e.value}장 뽑기",
            EffectType.GainEnergy => $"에너지 +{e.value}",
            EffectType.DamageFromBlock => $"블록의 {e.value}%만큼 추가 피해",
            EffectType.DamageConsumingBlock => e.value >= 100
                ? "블록을 모두 소모해 그만큼 피해"
                : $"블록의 {e.value}%를 소모해 그만큼 피해",
            _ => e.type.ToString(),
        };

        /// <summary>키워드 표기 (§10.3).</summary>
        public static string Keyword(CardKeyword k) => k switch
        {
            CardKeyword.Exhaust => "소멸",
            CardKeyword.Retain => "보존",
            CardKeyword.Innate => "무상",
            _ => "",
        };

        public static string RarityName(Rarity r) => r switch
        {
            Rarity.Rare => "희귀",
            Rarity.Transcendent => "초월",
            Rarity.Legendary => "전설",
            _ => "일반",
        };

        /// <summary>희귀도 색 — 미니게임 보상 팝업과 같은 색을 쓴다.</summary>
        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Rare => new Color(0.53f, 0.74f, 1f),
            Rarity.Transcendent => new Color(0.79f, 0.64f, 1f),
            Rarity.Legendary => new Color(1f, 0.82f, 0.4f),
            _ => new Color(0.86f, 0.86f, 0.86f),
        };

        public static string TypeName(CardType t) => t switch
        {
            CardType.Defense => "방어",
            CardType.Skill => "스킬",
            CardType.Root => "뿌리",
            _ => "공격",
        };

        /// <summary>
        /// 카드 설명 — <b>항상 실제 효과(effects)에서 조합한다.</b> 키워드는 뒤에 붙인다.
        /// 손으로 쓴 description은 쓰지 않는다: 수치를 고치면 글이 낡고, 실제 동작과 다른 설명이
        /// 나간 적이 있다(연쇄 수확 "턴 종료 시" ↔ 실제는 즉시). 카드목록.md도 같은 조합을 쓴다.
        /// description 필드는 기획 메모용으로만 남는다.
        /// </summary>
        public static string Describe(CardData c)
        {
            if (c == null) return "";
            string kw = Keyword(c.keyword);
            string tail = string.IsNullOrEmpty(kw) ? "" : "\n〈" + kw + "〉";
            if (c.effects == null || c.effects.Count == 0) return tail.TrimStart();
            var lines = new string[c.effects.Count];
            for (int i = 0; i < c.effects.Count; i++) lines[i] = Effect(c.effects[i]);
            return string.Join("\n", lines) + tail;
        }

        // ══════════════════════════════════════════ 전투 중 실제 수치 ══════════
        /// <summary>
        /// 손패 카드에 "지금 쓰면 실제로 들어갈 수치"를 넣기 위한 전투 문맥.
        /// 힘·약화(내 쪽)와 취약(적 쪽), 민첩·블록 불가가 모두 반영된 값을 돌려준다.
        /// </summary>
        public class Context
        {
            public System.Func<int, int> damage;   // 1타 기본 피해 → 실제 피해
            public System.Func<int, int> block;    // 기본 블록 → 실제로 얻는 블록
            public int currentBlock;               // 지금 쌓여 있는 블록 (블록 비례 피해용)
        }

        // 기본값보다 커지면 초록, 작아지면 빨강. 블록에서 나오는 피해처럼 기본값이 없는 건 노랑
        public const string UpColor = "#7CFC7C";
        public const string DownColor = "#FF7A6A";
        public const string LiveColor = "#FFD24A";

        /// <summary>숫자만 색을 바꾼다 — 기본값과 같으면 그대로.</summary>
        static string Num(int shown, int basis) =>
            shown > basis ? $"<color={UpColor}>{shown}</color>"
          : shown < basis ? $"<color={DownColor}>{shown}</color>"
          : shown.ToString();

        static string Live(int v) => $"<color={LiveColor}>{v}</color>";

        /// <summary>
        /// 전투 중 설명 — 피해·블록 숫자를 실제 값으로 바꾸고 달라진 숫자만 색을 입힌다.
        /// 한 카드 안에서 블록을 먼저 얻고 블록 비례로 때리는 경우가 있어,
        /// 효과를 순서대로 따라가며 쌓일 블록을 같이 계산한다(ResolveEffect와 같은 순서).
        /// </summary>
        public static string Describe(CardData c, Context ctx)
        {
            if (ctx == null || ctx.damage == null || ctx.block == null) return Describe(c);
            if (c == null) return "";

            string kw = Keyword(c.keyword);
            string tail = string.IsNullOrEmpty(kw) ? "" : "\n〈" + kw + "〉";
            if (c.effects == null || c.effects.Count == 0) return tail.TrimStart();

            int blk = ctx.currentBlock;
            bool noBlock = false;
            var lines = new string[c.effects.Count];
            for (int i = 0; i < c.effects.Count; i++)
            {
                var e = c.effects[i];
                switch (e.type)
                {
                    case EffectType.Damage:
                    {
                        int d = ctx.damage(e.value);
                        lines[i] = e.hits > 1 ? $"{Num(d, e.value)} 피해 ×{e.hits}" : $"{Num(d, e.value)} 피해";
                        break;
                    }
                    case EffectType.Block:
                    case EffectType.TemporaryBlock:
                    {
                        int g = noBlock ? 0 : ctx.block(e.value);
                        blk += g;
                        string tag = e.type == EffectType.TemporaryBlock ? " (이번 턴만)" : "";
                        lines[i] = $"{Num(g, e.value)} 블록{tag}";
                        break;
                    }
                    case EffectType.DamageFromBlock:
                    {
                        int from = blk * e.value / 100;
                        lines[i] = $"{Effect(e)} ({Live(from > 0 ? ctx.damage(from) : 0)})";
                        break;
                    }
                    case EffectType.DamageConsumingBlock:
                    {
                        int spend = Mathf.Clamp(blk * Mathf.Max(0, e.value) / 100, 0, blk);
                        blk -= spend;
                        lines[i] = $"{Effect(e)} ({Live(spend > 0 ? ctx.damage(spend) : 0)})";
                        break;
                    }
                    case EffectType.DisableBlockThisTurn:
                        noBlock = true;
                        lines[i] = Effect(e);
                        break;
                    default:
                        lines[i] = Effect(e);
                        break;
                }
            }
            return string.Join("\n", lines) + tail;
        }

        // ══════════════════════════════════════════ 카드 용어 설명 ═════════════
        /// <summary>
        /// 카드에 나오는 용어만 골라 한 줄씩 설명한다 — 카드에 마우스를 올리면 옆에 뜬다.
        /// 처음 하는 사람이 `〈소멸〉`·`취약 2 부여` 같은 말을 카드만 보고 이해할 수 있게.
        /// 설명할 게 없으면 빈 문자열.
        /// </summary>
        public static string Glossary(CardData c)
        {
            if (c == null) return "";
            var terms = new List<(string term, string desc)>();
            void Add(string term, string desc)
            {
                if (string.IsNullOrEmpty(desc)) return;
                foreach (var t in terms) if (t.term == term) return;
                terms.Add((term, desc));
            }

            if (c.effects != null)
                foreach (var e in c.effects)
                {
                    switch (e.type)
                    {
                        case EffectType.Block:
                            Add("블록", "공격 피해를 먼저 막아요. 턴이 지나도 사라지지 않고 쌓여요.");
                            break;
                        case EffectType.TemporaryBlock:
                            Add("블록 (이번 턴만)", "쌓이지 않는 블록이에요. 다음 내 턴이 시작되면 사라져요.");
                            break;
                        case EffectType.EffectDefense:
                            Add("효과 방어", "적이 거는 취약·약화·중독을 1건씩 막아요.");
                            break;
                        case EffectType.ApplyStatus:
                        case EffectType.ApplyStatusForTurns:
                            Add(Kor(e.status), StatusSummary(e.status));
                            if (e.target != TargetType.Self)
                                Add("효과 방어 (적)", "적에게 효과 방어가 있으면 거는 상태이상이 1건씩 막혀요.");
                            break;
                        case EffectType.AmplifyPoisonPerTurn:
                            Add(Kor(StatusType.Poison), StatusSummary(StatusType.Poison));
                            Add("중독 피해 +", "이번 전투 동안 적이 중독으로 받는 피해가 늘어나요.");
                            break;
                        case EffectType.ReflectHalfDamage:
                            Add("반사", "다음 적 턴에 받은 공격 피해(블록으로 막은 것 포함)의 절반을 적에게 돌려줘요.");
                            break;
                        case EffectType.DisableBlockThisTurn:
                            Add("블록 불가", "이 카드를 쓴 턴에는 블록을 더 얻을 수 없어요.");
                            break;
                        case EffectType.DamageFromBlock:
                            Add("블록 비례 피해", "지금 쌓인 블록에 비례해 피해를 줘요. 블록은 그대로 남아요.");
                            break;
                        case EffectType.DamageConsumingBlock:
                            Add("블록 소모", "쌓아둔 블록을 헐어 그만큼 피해를 줘요. 블록 빌드의 마무리!");
                            break;
                        case EffectType.DrawPerRemainingEnergy:
                            Add("남은 에너지", "이 카드를 쓰고 남은 에너지 1당 카드 1장을 바로 뽑아요.");
                            break;
                    }
                }

            if (c.keyword == CardKeyword.Exhaust)
                Add("소멸", "쓰면 이번 전투에서는 다시 나오지 않아요. 대신 효과가 강해요.");
            if (c.shownState == CardState.Sprouted)
                Add("싹", "수명의 마지막 런이라 효과가 더 강해졌어요. 이번 런이 끝나면 썩어요.");
            if (c.shownUpgrade == UpgradeKind.Plus)
                Add("강화 +", "대장간에서 수치를 올린 카드예요.");
            else if (c.shownUpgrade == UpgradeKind.Minus)
                Add("강화 −", "대장간에서 코스트를 1 줄인 카드예요.");

            if (terms.Count == 0) return "";
            var lines = new string[terms.Count];
            for (int i = 0; i < terms.Count; i++)
                lines[i] = $"<b><color=#FFC845>{terms[i].term}</color></b>  {terms[i].desc}";
            return string.Join("\n", lines);
        }

        /// <summary>카드 한 장의 요약 제목 — `희귀 · 공격 카드`.</summary>
        public static string CardKind(CardData c) =>
            c == null ? "" : $"{RarityName(c.rarity)} · {TypeName(c.type)} 카드";

        // ══════════════════════════════════════════ 아이콘 설명 (툴팁) ═════════
        /// <summary>상태이상 한 줄 요약 (§9 표 기준 — 실제 동작은 Combatant).</summary>
        public static string StatusSummary(StatusType s) => s switch
        {
            StatusType.Vulnerable => "받는 공격 피해 +50%. 턴이 끝날 때마다 1씩 줄어요.",
            StatusType.Weak => "주는 공격 피해 −25%. 턴이 끝날 때마다 1씩 줄어요.",
            StatusType.Poison => "턴 시작 시 수치만큼 피해 (블록 무시). 발동할 때마다 1씩 줄어요.",
            StatusType.Strength => "주는 공격 피해가 수치만큼 늘어요. 전투 내내 유지돼요.",
            StatusType.Dexterity => "블록을 얻을 때마다 수치만큼 더 얻어요. 전투 내내 유지돼요.",
            StatusType.Regen => "턴 시작 시 수치만큼 회복. 발동할 때마다 1씩 줄어요.",
            _ => "",
        };

        /// <summary>상태이상 설명 — 현재 스택을 넣어서.</summary>
        public static string StatusInfo(StatusType s, int stacks) => s switch
        {
            StatusType.Vulnerable => $"받는 공격 피해가 50% 늘어나요.\n턴이 끝날 때마다 1씩 줄어요. (남은 {stacks}턴)",
            StatusType.Weak => $"주는 공격 피해가 25% 줄어요.\n턴이 끝날 때마다 1씩 줄어요. (남은 {stacks}턴)",
            StatusType.Poison => $"턴 시작 시 {stacks} 피해 — 블록으로 막을 수 없어요.\n발동할 때마다 1씩 줄어요.",
            StatusType.Strength => $"주는 공격 피해 +{stacks}.\n전투가 끝날 때까지 유지돼요.",
            StatusType.Dexterity => $"블록을 얻을 때마다 +{stacks}.\n전투가 끝날 때까지 유지돼요.",
            StatusType.Regen => $"턴 시작 시 체력 {stacks} 회복.\n발동할 때마다 1씩 줄어요.",
            _ => "",
        };

        public static string BlockInfo(int temporary) =>
            "공격 피해를 이만큼 먼저 막아요. 중독은 막지 못해요.\n턴이 지나도 사라지지 않고 쌓여요." +
            (temporary > 0 ? $"\n(이 중 {temporary}은 이번 턴만 — 다음 턴 시작에 사라져요)" : "");

        public static string WardInfo(int count) =>
            $"새로 걸리는 디버프(취약·약화·중독)를 {count}건 막아요.\n막을 때마다 1씩 줄고, 턴이 지나도 남아요.";

        public static string PoisonAmpInfo(int amp) =>
            $"중독으로 받는 피해가 {amp} 늘어나요.\n전투가 끝날 때까지 유지돼요. (뿌리내림)";

        public static string PeriodicInfo(StatusType s, int value, int turns) =>
            $"앞으로 {turns}턴 동안, 턴 시작마다 {Kor(s)} +{value}.\n효과 방어로 막을 수 있어요.";

        /// <summary>적 인텐트 설명 — amount는 실제로 들어올 수치.</summary>
        public static string IntentInfo(EnemyAction a, int amount)
        {
            if (a == null) return "";
            return a.kind switch
            {
                EnemyActionKind.Attack => a.hits > 1
                    ? $"다음 턴에 {amount} 피해로 {a.hits}번 공격해요. (총 {amount * a.hits})\n블록이 있으면 먼저 막아요."
                    : $"다음 턴에 {amount} 피해로 공격해요.\n블록이 있으면 먼저 막아요.",
                EnemyActionKind.Block => $"다음 턴에 블록 {amount}을 얻어요.\n내 공격은 적의 블록부터 깎아요.",
                EnemyActionKind.Debuff => $"다음 턴에 {Kor(a.status)} {a.amount}을(를) 걸어요.\n" +
                                          $"{Kor(a.status)}: {StatusSummary(a.status)}\n효과 방어가 있으면 막을 수 있어요.",
                EnemyActionKind.Buff => $"다음 턴에 {Kor(a.status)} +{a.amount}을(를) 얻어요.\n" +
                                        $"{Kor(a.status)}: {StatusSummary(a.status)}",
                _ => "",
            };
        }

        public static string IntentTitle(EnemyAction a) => a == null ? "" : a.kind switch
        {
            EnemyActionKind.Attack => "적의 다음 행동 · 공격",
            EnemyActionKind.Block => "적의 다음 행동 · 방어",
            EnemyActionKind.Debuff => "적의 다음 행동 · 디버프",
            EnemyActionKind.Buff => "적의 다음 행동 · 강화",
            _ => "적의 다음 행동",
        };

        // ══════════════════════════════════════════ 문장 도우미 ═════════════════
        /// <summary>받침에 맞는 조사 — Josa("감자벌레", "이", "가") → "가".</summary>
        public static string Josa(string word, string withBatchim, string withoutBatchim)
        {
            if (string.IsNullOrEmpty(word)) return withoutBatchim;
            char ch = word[word.Length - 1];
            if (ch < 0xAC00 || ch > 0xD7A3) return withoutBatchim;
            return (ch - 0xAC00) % 28 != 0 ? withBatchim : withoutBatchim;
        }
    }
}
