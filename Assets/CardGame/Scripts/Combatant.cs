using System.Collections.Generic;
using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 플레이어·적 공통 전투 상태. 방어 2종 분리가 코어(§9):
    ///  - block(공격 방어): 공격 피해를 수치만큼 상쇄. 중독은 무시. 소유자 턴 시작 시 소멸.
    ///  - ward(효과 방어): 새로 걸리는 디버프를 건수로 무효화. 회복은 규칙에 따름.
    /// </summary>
    public class Combatant
    {
        public string name;
        public int hp;
        public int maxHp;
        public int block;   // 공격 방어
        public int ward;    // 효과 방어 (남은 건수)

        public readonly Dictionary<StatusType, int> status = new();

        public bool IsDead => hp <= 0;

        public int Get(StatusType s) => status.TryGetValue(s, out var v) ? v : 0;

        public void Add(StatusType s, int amount)
        {
            if (s == StatusType.None || amount == 0) return;
            int v = Get(s) + amount;
            if (v <= 0) status.Remove(s);
            else status[s] = v;
        }

        /// <summary>디버프 부여 시도. 효과 방어가 있으면 1건 소모하고 무효화하며 false 반환.</summary>
        public bool TryApplyDebuff(StatusType s, int amount)
        {
            if (ward > 0) { ward--; return false; }
            Add(s, amount);
            return true;
        }

        /// <summary>공격 피해 적용. 블록으로 먼저 상쇄하고 나머지를 체력에서 깎는다. 반환=실피해.</summary>
        public int TakeAttack(int rawDamage)
        {
            int dmg = Mathf.Max(0, rawDamage);
            int blocked = Mathf.Min(block, dmg);
            block -= blocked;
            int toHp = dmg - blocked;
            hp = Mathf.Max(0, hp - toHp);
            return toHp;
        }

        /// <summary>중독·고정 피해 등 블록을 무시하는 피해.</summary>
        public void TakeLoss(int amount)
        {
            hp = Mathf.Max(0, hp - Mathf.Max(0, amount));
        }

        public void Heal(int amount)
        {
            hp = Mathf.Min(maxHp, hp + Mathf.Max(0, amount));
        }

        /// <summary>소유자 턴 시작: 블록 소멸 → 중독 피해 → 재생 회복.</summary>
        public void OnTurnStart()
        {
            block = 0;

            int poison = Get(StatusType.Poison);
            if (poison > 0) { TakeLoss(poison); Add(StatusType.Poison, -1); } // 발동 후 −1

            int regen = Get(StatusType.Regen);
            if (regen > 0) { Heal(regen); Add(StatusType.Regen, -1); }
        }

        /// <summary>소유자 턴 종료: 취약·약화 1 감소(턴마다 −1).</summary>
        public void OnTurnEnd()
        {
            if (Get(StatusType.Vulnerable) > 0) Add(StatusType.Vulnerable, -1);
            if (Get(StatusType.Weak) > 0) Add(StatusType.Weak, -1);
        }

        /// <summary>이 대상이 가하는 공격 피해 보정: 힘 +, 약화 −25%.</summary>
        public int ModifyOutgoing(int baseDamage)
        {
            int d = baseDamage + Get(StatusType.Strength);
            if (Get(StatusType.Weak) > 0) d = Mathf.FloorToInt(d * 0.75f);
            return Mathf.Max(0, d);
        }

        /// <summary>이 대상이 받는 공격 피해 보정: 취약 +50%.</summary>
        public int ModifyIncoming(int damage)
        {
            if (Get(StatusType.Vulnerable) > 0) damage = Mathf.FloorToInt(damage * 1.5f);
            return damage;
        }
    }
}
