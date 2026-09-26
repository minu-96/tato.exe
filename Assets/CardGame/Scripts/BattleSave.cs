using System.Collections.Generic;
using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>전투 중인 카드 한 장 — 어느 보유 카드(인스턴스)에서 왔는지 기억해야 저장·복원이 된다.</summary>
    public class BattleCard
    {
        public CardData data;       // 강화·싹이 얹힌 런타임 카드
        public int instanceId;      // 보유 카드 인스턴스 (시작덱 폴백 카드는 -1)
        public string cardId;
    }

    /// <summary>
    /// 진행 중인 전투와 받지 않은 보상을 저장한다 (PlayerPrefs, JSON).
    ///
    /// 전투 중에 나갔다 들어오면 <b>그 자리에서 그대로</b> 이어진다 — 덱 순서·손패·버림 더미·
    /// 적 행동 순서·상태이상까지 전부. 그래서 나갔다 오는 것으로 얻을 게 없다
    /// (예전엔 전투 시작 체력으로 새로 시작해서 죽기 직전 이탈 = 공짜 재도전이었다).
    ///
    /// 보상도 마찬가지 — 이긴 뒤 보상 화면에서 나가면 같은 토인·같은 후보가 다시 뜬다.
    /// 둘 다 스테이지·열·행을 같이 적어 두고, 지금 노드와 다르면 버린다.
    /// </summary>
    public static class BattleSave
    {
        const string BattleKey = "tato_battle";
        const string RewardKey = "tato_reward";

        [System.Serializable] public class StatusEntry { public StatusType type; public int value; }
        [System.Serializable] public class PeriodicEntry { public StatusType status; public int value; public int turnsLeft; }

        [System.Serializable]
        public class CombatantState
        {
            public string name;
            public int hp, maxHp, block, tempBlock, ward, poisonAmp, damageWhileArmed;
            public bool blockDisabled, reflectArmed;
            public List<StatusEntry> status = new();
            public List<PeriodicEntry> periodic = new();
        }

        [System.Serializable] public class CardRef { public int instanceId; public string cardId; }

        [System.Serializable]
        public class BattleState
        {
            public int stage, col, row;
            public string enemyId;
            public int enemyIndex, energy;
            public bool transformed;
            public string notice;
            public CombatantState player, enemy;
            public List<CardRef> draw = new(), hand = new(), discard = new();
        }

        [System.Serializable]
        public class RewardState
        {
            public int stage, col, row;
            public int toin;
            public bool boss;
            public List<string> cardIds = new();
        }

        // ── 전투 ──
        public static void SaveBattle(BattleState s) => Write(BattleKey, Stamp(s));
        public static bool TryLoadBattle(out BattleState s) => TryRead(BattleKey, out s, x => Matches(x.stage, x.col, x.row));
        public static void ClearBattle() => Delete(BattleKey);

        // ── 보상 ──
        public static void SaveReward(RewardState s) => Write(RewardKey, Stamp(s));
        public static bool TryLoadReward(out RewardState s) => TryRead(RewardKey, out s, x => Matches(x.stage, x.col, x.row));
        public static void ClearReward() => Delete(RewardKey);

        public static void ClearAll() { ClearBattle(); ClearReward(); }

        // ── 변환 ──
        public static CombatantState Capture(Combatant c)
        {
            var s = new CombatantState
            {
                name = c.name, hp = c.hp, maxHp = c.maxHp, block = c.block, tempBlock = c.tempBlock,
                ward = c.ward, poisonAmp = c.poisonAmp, damageWhileArmed = c.damageWhileArmed,
                blockDisabled = c.blockDisabled, reflectArmed = c.reflectArmed,
            };
            foreach (var kv in c.status) s.status.Add(new StatusEntry { type = kv.Key, value = kv.Value });
            foreach (var p in c.periodic)
                s.periodic.Add(new PeriodicEntry { status = p.status, value = p.value, turnsLeft = p.turnsLeft });
            return s;
        }

        public static Combatant Restore(CombatantState s)
        {
            var c = new Combatant
            {
                name = s.name, hp = s.hp, maxHp = s.maxHp, block = s.block, tempBlock = s.tempBlock,
                ward = s.ward, poisonAmp = s.poisonAmp, damageWhileArmed = s.damageWhileArmed,
                blockDisabled = s.blockDisabled, reflectArmed = s.reflectArmed,
            };
            foreach (var e in s.status) c.Add(e.type, e.value);
            foreach (var p in s.periodic)
                c.periodic.Add(new PeriodicStatus { status = p.status, value = p.value, turnsLeft = p.turnsLeft });
            return c;
        }

        public static List<CardRef> Refs(List<BattleCard> pile)
        {
            var list = new List<CardRef>(pile.Count);
            foreach (var b in pile) list.Add(new CardRef { instanceId = b.instanceId, cardId = b.cardId });
            return list;
        }

        // ── 내부 ──
        static bool Matches(int stage, int col, int row) =>
            RunState.Active && stage == RunState.Stage && col == RunState.Col && row == RunState.Row;

        static BattleState Stamp(BattleState s) { s.stage = RunState.Stage; s.col = RunState.Col; s.row = RunState.Row; return s; }
        static RewardState Stamp(RewardState s) { s.stage = RunState.Stage; s.col = RunState.Col; s.row = RunState.Row; return s; }

        static void Write<T>(string key, T value)
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(value));
            PlayerPrefs.Save();
        }

        static bool TryRead<T>(string key, out T value, System.Func<T, bool> valid) where T : class
        {
            value = null;
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw)) return false;
            try { value = JsonUtility.FromJson<T>(raw); }
            catch (System.Exception e) { Debug.LogWarning($"[TatoGames] {key} 해석 실패 — 버림: {e.Message}"); }
            if (value != null && valid(value)) return true;
            value = null;
            Delete(key);   // 다른 노드의 낡은 저장이거나 깨진 저장
            return false;
        }

        static void Delete(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
