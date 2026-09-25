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

        static List<Achievement> Build() => new()
        {
            new Achievement { id = "first_run",   title = "첫 수확",     desc = "런을 한 번 시작한다",
                              check = () => Get(RunsStarted) >= 1 },
            new Achievement { id = "first_win",   title = "첫 승리",     desc = "전투에서 한 번 이긴다",
                              check = () => Get(BattlesWon) >= 1 },
            new Achievement { id = "win_10",      title = "밭을 갈다",   desc = "전투에서 10번 이긴다",
                              check = () => Get(BattlesWon) >= 10 },
            new Achievement { id = "win_50",      title = "대풍년",      desc = "전투에서 50번 이긴다",
                              check = () => Get(BattlesWon) >= 50 },
            new Achievement { id = "boss_1",      title = "허수아비 사냥", desc = "보스를 한 번 쓰러뜨린다",
                              check = () => Get(BossesKilled) >= 1 },
            new Achievement { id = "boss_3",      title = "세 층 아래로", desc = "보스를 3번 쓰러뜨린다",
                              check = () => Get(BossesKilled) >= 3 },
            new Achievement { id = "clear_run",   title = "깊은 토양까지", desc = "런을 클리어한다",
                              check = () => Get(RunsCleared) >= 1 },
            new Achievement { id = "minigame_1",  title = "곁다리 농사",  desc = "미니게임을 한 번 끝낸다",
                              check = () => Get(MinigamesPlayed) >= 1 },
            new Achievement { id = "minigame_10", title = "부업 전문",    desc = "미니게임을 10번 끝낸다",
                              check = () => Get(MinigamesPlayed) >= 10 },
            new Achievement { id = "collect_20",  title = "감자 수집가",  desc = "카드를 20장 모은다",
                              check = () => PlayerData.Instances().Count >= 20 },
            new Achievement { id = "collect_40",  title = "창고가 좁다",  desc = "카드를 40장 모은다",
                              check = () => PlayerData.Instances().Count >= 40 },
            new Achievement { id = "rotten_1",    title = "썩은 감자",    desc = "카드를 한 장 썩힌다",
                              check = () => Get(CardsRotted) >= 1 },
            new Achievement { id = "rotten_10",   title = "퇴비 더미",    desc = "카드를 10장 썩힌다",
                              check = () => Get(CardsRotted) >= 10 },
            new Achievement { id = "upgrade_1",   title = "대장간 단골",  desc = "대장간에서 카드를 강화한다",
                              check = () => Get(CardsUpgraded) >= 1 },
            new Achievement { id = "rich",        title = "토인 부자",    desc = "토인을 200개 모은다",
                              check = () => PlayerData.Toin >= 200 },
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
    }
}
