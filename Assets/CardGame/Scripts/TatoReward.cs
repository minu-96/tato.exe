using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 미니게임 → 런처 카드 수급 다리 (§8 카드 획득처).
    ///
    /// 미니게임 쪽에서 해야 할 일은 <b>조건이 충족됐을 때 한 줄 호출</b>이 전부다:
    /// <code>TatoReward.Grant(AcquireSource.Neulteona, score);</code>
    ///
    /// 미니게임 씬은 카드 데이터도, PlayerData도, 인스펙터 배선도 필요 없다 —
    /// 카드 목록과 보상 규칙은 Resources의 CardLibrary에서 알아서 읽는다.
    /// 미니게임이 늘어나도 이 함수만 부르면 된다.
    /// </summary>
    public static class TatoReward
    {
        /// <summary>런처로 돌아가기 전까지 쌓인 미니게임 보상 (복귀 팝업이 한 번 보여주고 지운다).</summary>
        public const string PendingKey = "tato_reward_pending";

        public class Result
        {
            public readonly List<CardData> cards = new();
            public int Count => cards.Count;
            public bool Any => cards.Count > 0;
            public string summary = "";
        }

        /// <summary>미니게임 한 종류의 누적 성과 — 몇 판 했고 희귀도별로 몇 장 받았나.</summary>
        [System.Serializable]
        public class PendingEntry
        {
            public AcquireSource source;
            public int plays;
            public int[] byRarity = new int[4];   // Rarity 순서: 일반 · 희귀 · 초월 · 전설
            public int Total => byRarity.Sum();
        }

        [System.Serializable]
        public class PendingSummary
        {
            public List<PendingEntry> entries = new();
            public int TotalCards => entries.Sum(e => e.Total);
        }

        /// <summary>
        /// 미니게임 1판의 성과를 카드로 바꾼다. record는 게임마다 의미가 다르다
        /// (늘어나라·모아모아 = 점수, 밭의 생존자 = 끝까지 버틴 라운드 수).
        /// 기록이 최저 구간에 못 미치면 카드는 없지만 "한 판 했다"는 요약에는 남는다.
        /// <b>판이 끝날 때 한 번만</b> 부를 것 — 여러 번 부르면 그만큼 더 준다.
        ///
        /// 결과는 판마다 덮어쓰지 않고 <b>런처로 돌아갈 때까지 누적</b>한다 — 여러 판 하고 돌아와도
        /// 복귀 팝업이 전부 합쳐서 보여준다. 카드 이름은 알리지 않는다(감자창고에서 빨간 점으로 확인).
        /// </summary>
        public static Result Grant(AcquireSource source, int record)
        {
            var result = new Result();
            // 보상 여부와 무관하게 "한 판 했다"는 사실은 기록한다
            Launcher.Achievements.Bump(Launcher.Achievements.MinigamesPlayed);

            var lib = CardLibrary.Load();
            if (lib == null)
            {
                Debug.LogWarning("[TatoGames] CardLibrary를 찾지 못했습니다 — " +
                                 "'TatoGames ▸ Generate MVP Cards' 를 한 번 실행하세요");
                return result;
            }

            var rule = lib.RuleFor(source);
            if (rule == null)
            {
                Debug.LogWarning($"[TatoGames] {source} 보상 규칙이 없습니다 — CardLibrary 인스펙터 확인");
                return result;
            }

            var tier = rule.TierFor(record);   // null이면 기준 미달 — 빈손
            if (tier != null)
            {
                for (int i = 0; i < tier.cards; i++)
                {
                    var rarity = Random.Range(0, 100) < tier.rareChance ? Rarity.Rare : Rarity.Common;
                    var pick = lib.PickRandom(source, rarity) ?? lib.PickRandom(source, Rarity.Common);
                    if (pick == null) break;       // 이 출처에 카드가 없음
                    // 희귀도로 수명이 정해진다(§8.1). 덱에는 넣지 않고 감자창고로만 — 덱 편성은 플레이어가 직접
                    PlayerData.AddCard(pick.id, pick.rarity, toDeck: false);
                    result.cards.Add(pick);
                }
            }

            RecordPending(source, result.cards);

            string names = result.Any ? string.Join(", ", result.cards.Select(c => c.displayName)) : "없음";
            result.summary = $"{rule.recordLabel} {record} — 카드 {result.Count}장: {names}";
            Debug.Log("[TatoGames] " + result.summary);
            return result;
        }

        static void RecordPending(AcquireSource source, List<CardData> cards)
        {
            var summary = LoadPending() ?? new PendingSummary();
            var entry = summary.entries.FirstOrDefault(e => e.source == source);
            if (entry == null) { entry = new PendingEntry { source = source }; summary.entries.Add(entry); }
            if (entry.byRarity == null || entry.byRarity.Length < 4) entry.byRarity = new int[4];
            entry.plays++;
            foreach (var c in cards) entry.byRarity[Mathf.Clamp((int)c.rarity, 0, 3)]++;
            PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(summary));
            PlayerPrefs.Save();
        }

        static PendingSummary LoadPending()
        {
            string raw = PlayerPrefs.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(raw)) return null;
            try { return JsonUtility.FromJson<PendingSummary>(raw); }
            catch { return null; }
        }

        /// <summary>
        /// 쌓인 미니게임 보상 요약을 가져오고 지운다. 런처로 돌아왔을 때 팝업이 한 번 부른다.
        /// 한 판도 안 했으면 null.
        /// </summary>
        public static PendingSummary TakePending()
        {
            var summary = LoadPending();
            if (PlayerPrefs.HasKey(PendingKey)) { PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); }
            return summary != null && summary.entries.Count > 0 ? summary : null;
        }

        /// <summary>
        /// 활성 씬 이름 끝에 붙은 숫자 (InGame3 → 3). 라운드를 씬으로 관리하는
        /// 밭의 생존자가 "몇 라운드까지 갔나"를 알아내는 데 쓴다. 숫자가 없으면 0.
        /// </summary>
        public static int SceneNumber()
        {
            string name = SceneManager.GetActiveScene().name;
            int end = name.Length;
            while (end > 0 && char.IsDigit(name[end - 1])) end--;
            return end < name.Length && int.TryParse(name[end..], out int v) ? v : 0;
        }
    }
}
