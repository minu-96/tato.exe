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
        const string NoticeKey = "tato_reward_notice";

        public class Result
        {
            public readonly List<CardData> cards = new();
            public int Count => cards.Count;
            public bool Any => cards.Count > 0;
            public string summary = "";
        }

        /// <summary>
        /// 미니게임 1판의 성과를 카드로 바꾼다. record는 게임마다 의미가 다르다
        /// (늘어나라·모아모아 = 점수, 밭의 생존자 = 생존 라운드).
        /// 기록이 최저 구간에 못 미치면 아무것도 주지 않는다.
        /// <b>판이 끝날 때 한 번만</b> 부를 것 — 여러 번 부르면 그만큼 더 준다.
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

            var tier = rule.TierFor(record);
            if (tier == null) return result;   // 기준 미달 — 조용히 빈손

            for (int i = 0; i < tier.cards; i++)
            {
                var rarity = Random.Range(0, 100) < tier.rareChance ? Rarity.Rare : Rarity.Common;
                var pick = lib.PickRandom(source, rarity) ?? lib.PickRandom(source, Rarity.Common);
                if (pick == null) break;       // 이 출처에 카드가 없음
                PlayerData.AddCard(pick.id, pick.rarity);   // 희귀도로 수명이 정해진다(§8.1)
                result.cards.Add(pick);
            }

            if (result.Any)
            {
                string names = string.Join(", ", result.cards.Select(c => c.displayName));
                result.summary = $"{rule.recordLabel} {record} — 카드 {result.Count}장 획득: {names}";
                PlayerPrefs.SetString(NoticeKey, result.summary);
                PlayerPrefs.Save();
                Debug.Log("[TatoGames] " + result.summary);
            }
            return result;
        }

        /// <summary>
        /// 아직 안 보여준 획득 알림을 가져오고 지운다. 런처 복귀 연출이 한 번 표시한다.
        /// </summary>
        public static string TakePendingNotice()
        {
            string s = PlayerPrefs.GetString(NoticeKey, "");
            if (!string.IsNullOrEmpty(s))
            {
                PlayerPrefs.DeleteKey(NoticeKey);
                PlayerPrefs.Save();
            }
            return s;
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
