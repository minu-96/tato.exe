using System.Collections.Generic;
using System.Linq;
using TatoGames.CardGame;
using UnityEngine;

namespace TatoGames.Launcher
{
    /// <summary>업적 1개. 조건은 코드로 판정하고 달성 여부만 저장한다.</summary>
    public class Achievement
    {
        public string id;
        public string title;
        public string desc;
        /// <summary>지금 조건을 만족하나. 저장된 카운터·컬렉션 상태로 판정한다.</summary>
        public System.Func<bool> check;
        /// <summary>지금 값 (진행도 표시 — "3 / 10").</summary>
        public System.Func<int> current;
        /// <summary>목표 값.</summary>
        public int target = 1;
    }

    /// <summary>
    /// 업적 — 달성 조건을 코드로 판정하고, 한 번 달성하면 그대로 남는다(되돌아가지 않음).
    /// 카운터는 게임 쪽에서 <see cref="Bump"/>로 올린다.
    /// </summary>
    public static class Achievements
    {
        // ── 카운터 (PlayerPrefs) ──
        public const string RunsStarted = "ach_runs_started";
        public const string BattlesWon = "ach_battles_won";
        public const string BossesKilled = "ach_bosses";
        public const string RunsCleared = "ach_runs_cleared";
        public const string MinigamesPlayed = "ach_minigames";
        public const string CardsRotted = "ach_rotted";
        public const string CardsUpgraded = "ach_upgraded";

        public static int Get(string key) => PlayerPrefs.GetInt(key, 0);

        /// <summary>카운터를 올린다(게임 쪽에서 호출).</summary>
        public static void Bump(string key, int amount = 1)
        {
            PlayerPrefs.SetInt(key, Get(key) + amount);
            PlayerPrefs.Save();
        }

        static List<Achievement> all;

        public static IReadOnlyList<Achievement> All => all ??= Build();

        /// <summary>카운터가 목표에 닿으면 달성 — 진행도("3 / 10")도 같은 값으로 보여준다.</summary>
        static Achievement A(string id, string title, string desc, System.Func<int> current, int target) => new()
        {
            id = id, title = title, desc = desc, current = current, target = target,
            check = () => current() >= target,
        };

        static List<Achievement> Build() => new()
        {
            A("first_run",   "첫 수확",       "런을 한 번 시작한다",        () => Get(RunsStarted), 1),
            A("first_win",   "첫 승리",       "전투에서 한 번 이긴다",      () => Get(BattlesWon), 1),
            A("win_10",      "밭을 갈다",     "전투에서 10번 이긴다",       () => Get(BattlesWon), 10),
            A("win_50",      "대풍년",        "전투에서 50번 이긴다",       () => Get(BattlesWon), 50),
            A("boss_1",      "허수아비 사냥", "보스를 한 번 쓰러뜨린다",    () => Get(BossesKilled), 1),
            A("boss_3",      "세 층 아래로",  "보스를 3번 쓰러뜨린다",      () => Get(BossesKilled), 3),
            A("clear_run",   "깊은 토양까지", "런을 클리어한다",            () => Get(RunsCleared), 1),
            A("minigame_1",  "곁다리 농사",   "미니게임을 한 번 끝낸다",    () => Get(MinigamesPlayed), 1),
            A("minigame_10", "부업 전문",     "미니게임을 10번 끝낸다",     () => Get(MinigamesPlayed), 10),
            A("collect_20",  "감자 수집가",   "카드를 20장 모은다",         () => PlayerData.Instances().Count, 20),
            A("collect_40",  "창고가 좁다",   "카드를 40장 모은다",         () => PlayerData.Instances().Count, 40),
            A("rotten_1",    "썩은 감자",     "카드를 한 장 썩힌다",        () => Get(CardsRotted), 1),
            A("rotten_10",   "퇴비 더미",     "카드를 10장 썩힌다",         () => Get(CardsRotted), 10),
            A("upgrade_1",   "대장간 단골",   "대장간에서 카드를 강화한다", () => Get(CardsUpgraded), 1),
            A("rich",        "토인 부자",     "토인을 200개 모은다",        () => PlayerData.Toin, 200),
        };

        /// <summary>한 번 달성하면 조건이 깨져도 유지된다(카드를 팔아도 업적은 남는다).</summary>
        public static bool IsUnlocked(Achievement a)
        {
            string key = "ach_done_" + a.id;
            if (PlayerPrefs.GetInt(key, 0) != 0) return true;
            if (a.check == null || !a.check()) return false;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static int UnlockedCount => All.Count(IsUnlocked);

        /// <summary>테스트용 — 카운터와 달성 기록을 전부 지운다.</summary>
        public static void ResetAll()
        {
            foreach (var key in new[] { RunsStarted, BattlesWon, BossesKilled, RunsCleared,
                                        MinigamesPlayed, CardsRotted, CardsUpgraded })
                PlayerPrefs.DeleteKey(key);
            foreach (var a in All) PlayerPrefs.DeleteKey("ach_done_" + a.id);
            PlayerPrefs.Save();
        }
    }
}
