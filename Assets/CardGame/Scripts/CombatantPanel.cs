using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 슬더스식 전투 주체 패널: 인텐트(적) / 초상화 / HP바 / 공격방어·효과방어 배지 / 상태이상 아이콘.
    /// 초상화는 EnemyData.artwork, 아이콘은 BattleTheme에서 가져온다. 비면 단색 폴백.
    /// </summary>
    public class CombatantPanel : MonoBehaviour
    {
        public Text nameText;
        public Image portrait;
        public Image barBg, barFill;
        public Text hpText;
        public Image blockBadge; public Text blockText;
        public Image wardBadge; public Text wardText;
        public RectTransform statusRow;
        public Image intentIcon; public Text intentText;

        Font font;

        public static CombatantPanel Create(Transform parent, Font font, Vector2 portraitSize, bool withIntent)
        {
            float w = portraitSize.x + 40f;
            float h = portraitSize.y + 190f;

            var root = UiKit.Rect("CombatantPanel", parent);
            root.sizeDelta = new Vector2(w, h);
            var p = root.gameObject.AddComponent<CombatantPanel>();
            p.font = font;

            float y = 0f;

            if (withIntent)
            {
                p.intentIcon = UiKit.Img("IntentIcon", root);
                UiKit.Place(p.intentIcon.rectTransform, new Vector2(0.5f, 1), new Vector2(-70, -18), new Vector2(34, 34));
                p.intentText = UiKit.Label("IntentText", root, font, 20, TextAnchor.MiddleLeft);
                UiKit.Place(p.intentText.rectTransform, new Vector2(0.5f, 1), new Vector2(40, -35), new Vector2(w, 30));
                y -= 44f;
            }

            p.nameText = UiKit.Label("Name", root, font, 19);
            UiKit.Place(p.nameText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y - 14), new Vector2(w, 26));
            y -= 32f;

            p.portrait = UiKit.Img("Portrait", root);
            UiKit.Place(p.portrait.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), portraitSize);
            p.portrait.preserveAspect = true;
            y -= portraitSize.y + 10f;

            // HP 바
            p.barBg = UiKit.Img("HpBarBg", root);
            UiKit.Place(p.barBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(w - 16, 24));
            p.barFill = UiKit.Img("HpBarFill", p.barBg.rectTransform);
            p.barFill.rectTransform.anchorMin = new Vector2(0, 0);
            p.barFill.rectTransform.anchorMax = new Vector2(1, 1);
            p.barFill.rectTransform.offsetMin = new Vector2(2, 2);
            p.barFill.rectTransform.offsetMax = new Vector2(-2, -2);
            p.barFill.type = Image.Type.Filled;
            p.barFill.fillMethod = Image.FillMethod.Horizontal;
            p.hpText = UiKit.Label("HpText", p.barBg.rectTransform, font, 17);
            UiKit.Stretch(p.hpText.rectTransform);
            y -= 30f;

            // 방어 배지 2종 (공격 방어 / 효과 방어)
            p.blockBadge = UiKit.Img("BlockBadge", root);
            UiKit.Place(p.blockBadge.rectTransform, new Vector2(0.5f, 1), new Vector2(-38, y), new Vector2(64, 28));
            p.blockText = UiKit.Label("BlockText", p.blockBadge.rectTransform, font, 16);
            UiKit.Stretch(p.blockText.rectTransform);

            p.wardBadge = UiKit.Img("WardBadge", root);
            UiKit.Place(p.wardBadge.rectTransform, new Vector2(0.5f, 1), new Vector2(38, y), new Vector2(64, 28));
            p.wardText = UiKit.Label("WardText", p.wardBadge.rectTransform, font, 16);
            UiKit.Stretch(p.wardText.rectTransform);
            y -= 34f;

            // 상태이상 아이콘 줄
            var rowGO = UiKit.Rect("StatusRow", root);
            UiKit.Place(rowGO, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(w, 30));
            var hlg = rowGO.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            p.statusRow = rowGO;

            // 패널 높이를 내용(상태이상 줄 아래끝)에 딱 맞춘다 — 아래쪽 기준으로 배치할 때
            // 빈 여백 없이 손패 바로 위에 붙도록
            root.sizeDelta = new Vector2(w, -y + 30f);

            return p;
        }

        public void Bind(Combatant c, BattleTheme theme, Sprite portraitSprite)
        {
            if (c == null) return;

            if (nameText != null) nameText.text = c.name;
            UiKit.Apply(portrait, portraitSprite, new Color(0.25f, 0.27f, 0.32f, 0.9f));

            UiKit.Apply(barBg, theme != null ? theme.barBackground : null, new Color(0, 0, 0, 0.6f));
            UiKit.Apply(barFill, theme != null ? theme.barFill : null,
                        theme != null ? theme.hpColor : new Color(0.78f, 0.25f, 0.25f));
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillAmount = c.maxHp > 0 ? Mathf.Clamp01((float)c.hp / c.maxHp) : 0f;
            hpText.text = $"{c.hp} / {c.maxHp}";

            SetBadge(blockBadge, blockText, theme != null ? theme.blockIcon : null,
                     theme != null ? theme.blockColor : new Color(0.35f, 0.62f, 0.85f),
                     c.TotalBlock, "방어");
            SetBadge(wardBadge, wardText, theme != null ? theme.wardIcon : null,
                     theme != null ? theme.wardColor : new Color(0.65f, 0.45f, 0.85f),
                     c.ward, "효과");

            BuildStatusIcons(c, theme);
        }

        void SetBadge(Image badge, Text text, Sprite icon, Color fallback, int value, string shortLabel)
        {
            bool show = value > 0;
            badge.gameObject.SetActive(show);
            if (!show) return;
            UiKit.Apply(badge, icon, fallback);
            text.text = icon != null ? value.ToString() : $"{shortLabel} {value}";
        }

        void BuildStatusIcons(Combatant c, BattleTheme theme)
        {
            for (int i = statusRow.childCount - 1; i >= 0; i--)
                Destroy(statusRow.GetChild(i).gameObject);

            foreach (var kv in c.status)
            {
                if (kv.Value == 0) continue;
                var cell = UiKit.Img($"St_{kv.Key}", statusRow);
                cell.rectTransform.sizeDelta = new Vector2(44, 26);
                UiKit.Apply(cell, theme != null ? theme.IconFor(kv.Key) : null,
                            BattleTheme.StatusColor(kv.Key));

                var t = UiKit.Label("V", cell.rectTransform, font, 15);
                UiKit.Stretch(t.rectTransform);
                t.text = cell.sprite != null ? kv.Value.ToString()
                                             : $"{CardText.Kor(kv.Key)}{kv.Value}";
                t.color = cell.sprite != null ? Color.white : new Color(0.1f, 0.1f, 0.12f);
            }

            // 상태이상 칸 말고도 걸려 있는 지속 효과 — 안 보이면 걸었는지 알 수 없다
            if (c.poisonAmp > 0)
                TextChip("St_PoisonAmp", $"뿌리+{c.poisonAmp}", new Color(0.36f, 0.55f, 0.28f));   // 뿌리내림: 중독 피해 +N
            foreach (var p in c.periodic)
                TextChip($"St_Periodic_{p.status}", $"{CardText.Kor(p.status)}+{p.value}·{p.turnsLeft}턴",
                         BattleTheme.StatusColor(p.status) * 0.8f);                           // 곰팡이 정원 등
        }

        void TextChip(string name, string text, Color color)
        {
            var cell = UiKit.Img(name, statusRow);
            cell.rectTransform.sizeDelta = new Vector2(78, 26);
            color.a = 1f;
            cell.color = color;
            var t = UiKit.Label("V", cell.rectTransform, font, 14);
            UiKit.Stretch(t.rectTransform);
            t.text = text;
            t.color = new Color(0.08f, 0.08f, 0.1f);
        }

        /// <summary>
        /// 적 인텐트 표시 (적 설계 §2.4 — 공격은 피해량, 다타면 n×m).
        /// shownAmount = 실제로 들어올 수치 (장별 배율·힘·약화·취약을 호출부가 반영해서 넘긴다).
        /// </summary>
        public void SetIntent(EnemyAction a, BattleTheme theme, int shownAmount)
        {
            if (intentText == null) return;
            if (a == null) { intentText.text = ""; if (intentIcon) intentIcon.gameObject.SetActive(false); return; }

            string name = string.IsNullOrEmpty(a.label) ? a.kind.ToString() : a.label;
            int amt = Mathf.Max(0, shownAmount);
            intentText.text = a.kind switch
            {
                EnemyActionKind.Attack => a.hits > 1 ? $"{name}  {amt}×{a.hits}" : $"{name}  {amt}",
                EnemyActionKind.Block => $"{name}  방어 {amt}",
                EnemyActionKind.Debuff => $"{name}  {CardText.Kor(a.status)} {a.amount}",
                EnemyActionKind.Buff => $"{name}  {CardText.Kor(a.status)} +{a.amount}",
                _ => name,
            };

            if (intentIcon == null) return;
            var sp = theme != null ? theme.IntentFor(a.kind) : null;
            intentIcon.gameObject.SetActive(true);
            UiKit.Apply(intentIcon, sp, a.kind switch
            {
                EnemyActionKind.Attack => new Color(0.9f, 0.35f, 0.3f),
                EnemyActionKind.Block => new Color(0.35f, 0.62f, 0.85f),
                EnemyActionKind.Debuff => new Color(0.7f, 0.45f, 0.85f),
                _ => new Color(0.9f, 0.75f, 0.3f),
            });
        }
    }
}
