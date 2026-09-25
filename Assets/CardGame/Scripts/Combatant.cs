using System.Collections.Generic;
using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>지속 효과 — duration턴 동안 매 턴 상태이상을 덧붙인다 (곰팡이 정원).</summary>
    public class PeriodicStatus
    {
        public StatusType status;
        public int value;
        public int turnsLeft;
    }

    /// <summary>
    /// 플레이어·적 공통 전투 상태. 방어 2종 분리가 코어(§9):
    ///  - block(공격 방어): 공격 피해를 수치만큼 상쇄. 중독은 무시.
    ///  - ward(효과 방어): 새로 걸리는 디버프를 건수로 무효화.
    ///
    /// 방어는 둘 다 <b>턴을 넘겨 누적</b>된다. 예외는 tempBlock — 철벽처럼 한 번에 크게
    /// 주는 블록은 누적되지 않고 소유자의 다음 턴 시작에 사라진다.
    /// </summary>
    public class Combatant
    {
        public string name;
        public int hp;
        public int maxHp;
        public int block;       // 공격 방어 — 턴을 넘겨 누적
        public int tempBlock;   // 이번 턴 한정 블록(철벽 등) — 소유자 턴 시작에 소멸
        public int ward;        // 효과 방어 (남은 건수) — 누적

        /// <summary>중독 피해 증폭 (뿌리내림) — 이 대상이 중독으로 받는 피해 +N.</summary>
        public int poisonAmp;

        /// <summary>돌진 부작용 — 이번 턴에는 블록을 얻을 수 없다.</summary>
        public bool blockDisabled;

        /// <summary>되받아치기 — 무장된 동안 받은 피해를 모아 두었다가 절반을 되돌려준다.</summary>
        public bool reflectArmed;
        public int damageWhileArmed;

        public readonly List<PeriodicStatus> periodic = new();
        public readonly Dictionary<StatusType, int> status = new();

        public bool IsDead => hp <= 0;

        /// <summary>화면에 보이는 방어 수치 = 누적 + 이번 턴 한정.</summary>
        public int TotalBlock => block + tempBlock;

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

        /// <summary>
        /// 공격 피해 적용. 이번 턴 한정 블록(tempBlock)을 먼저 태우고 — 어차피 턴이 끝나면
        /// 사라지므로 — 남으면 누적 블록으로 상쇄한 뒤 나머지를 체력에서 깎는다. 반환=실피해.
        /// </summary>
        public int TakeAttack(int rawDamage)
        {
            int dmg = Mathf.Max(0, rawDamage);
            if (reflectArmed) damageWhileArmed += dmg;   // 반사는 막아낸 몫까지 포함해 계산

            int fromTemp = Mathf.Min(tempBlock, dmg);
            tempBlock -= fromTemp;
            dmg -= fromTemp;

            int fromBlock = Mathf.Min(block, dmg);
            block -= fromBlock;

            int toHp = dmg - fromBlock;
            hp = Mathf.Max(0, hp - toHp);
            return toHp;
        }

        /// <summary>
        /// 쌓아둔 방어를 자원으로 쓴다 (흙사태 등). percent만큼 소모하고 실제 소모량을 돌려준다.
        /// 어차피 턴이 끝나면 사라질 tempBlock부터 태운다.
        /// </summary>
        public int SpendBlock(int percent)
        {
            int total = TotalBlock;
            int spend = Mathf.Clamp(total * Mathf.Max(0, percent) / 100, 0, total);
            int fromTemp = Mathf.Min(tempBlock, spend);
            tempBlock -= fromTemp;
            block -= spend - fromTemp;
            return spend;
        }

        /// <summary>블록 획득 — 민첩 보정과 '이번 턴 블록 불가'를 한곳에서 처리한다.</summary>
        public void GainBlock(int amount, bool temporary)
        {
            if (blockDisabled) return;
            int v = Mathf.Max(0, amount + Get(StatusType.Dexterity));
            if (temporary) tempBlock += v;
            else block += v;
        }

        /// <summary>되받아치기 무장. 이미 무장돼 있으면 누적분을 이어서 센다.</summary>
        public void ArmReflect()
        {
            if (!reflectArmed) { reflectArmed = true; damageWhileArmed = 0; }
        }

        /// <summary>무장 해제하고 반사할 피해량을 돌려준다(절반, 버림).</summary>
        public int ConsumeReflect()
        {
            if (!reflectArmed) return 0;
            int back = damageWhileArmed / 2;
            reflectArmed = false;
            damageWhileArmed = 0;
            return back;
        }

        /// <summary>지속 효과 등록 (곰팡이 정원). 같은 상태이상이면 턴 수를 갱신한다.</summary>
        public void AddPeriodic(StatusType s, int value, int turns)
        {
            if (s == StatusType.None || value == 0 || turns <= 0) return;
            var found = periodic.Find(p => p.status == s);
            if (found != null) { found.value = Mathf.Max(found.value, value); found.turnsLeft += turns; }
            else periodic.Add(new PeriodicStatus { status = s, value = value, turnsLeft = turns });
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

        /// <summary>
        /// 소유자 턴 시작: 이번 턴 한정 블록만 소멸 → 지속 효과 적용 → 중독 피해 → 재생 회복.
        /// 누적 블록(block)과 효과 방어(ward)는 여기서 건드리지 않는다 — 턴을 넘겨 쌓인다.
        /// </summary>
        public void OnTurnStart()
        {
            tempBlock = 0;

            TickPeriodic();

            int poison = Get(StatusType.Poison);
            if (poison > 0) { TakeLoss(poison + poisonAmp); Add(StatusType.Poison, -1); } // 발동 후 −1

            int regen = Get(StatusType.Regen);
            if (regen > 0) { Heal(regen); Add(StatusType.Regen, -1); }
        }

        /// <summary>지속 효과를 1턴치 적용하고 남은 턴을 줄인다. 디버프는 효과 방어를 거친다.</summary>
        void TickPeriodic()
        {
            for (int i = periodic.Count - 1; i >= 0; i--)
            {
                var p = periodic[i];
                TryApplyDebuff(p.status, p.value);
                if (--p.turnsLeft <= 0) periodic.RemoveAt(i);
            }
        }

        /// <summary>소유자 턴 종료: 취약·약화 1 감소(턴마다 −1), 블록 불가 해제.</summary>
        public void OnTurnEnd()
        {
            if (Get(StatusType.Vulnerable) > 0) Add(StatusType.Vulnerable, -1);
            if (Get(StatusType.Weak) > 0) Add(StatusType.Weak, -1);
            blockDisabled = false;
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
