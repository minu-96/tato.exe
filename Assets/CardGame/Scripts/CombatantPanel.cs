using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 슬더스식 전투 주체 패널: 인텐트(적) / 이름 / 초상화 / HP바 / 블록·효과 방어 배지 / 상태이상 아이콘.
    /// 초상화는 EnemyData.artwork, 아이콘은 BattleTheme에서 가져온다. 비면 단색 폴백.
    ///
    /// 아이콘 아트(블록·효과 방어 128×56, 상태이상 88×52)는 왼쪽에 그림, 오른쪽에 빈 숫자 칸이 있다.
    /// 숫자는 그 칸 안에 쓴다 — 아이콘 한가운데 쓰면 그림과 겹친다.
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
        public RectTransform intentRow;
        public Image intentIcon; public Text intentText;

        Font font;
        Color portraitBase = Color.white;
        Vector2 portraitHome;
        Coroutine flashing;

        // ── 아트 비율에 맞춘 크기 ──
        static readonly Vector2 BadgeSize = new(80, 35);    // 128×56
        static readonly Vector2 StatusSize = new(60, 35);   // 88×52
        const float BarHeight = 30f;
        const float IntentIconSize = 40f;

        // 아이콘 오른쪽 숫자 칸 (아트에서 잰 비율 — 왼·아래·오른·위)
        static readonly Rect BadgePlate = Rect.MinMaxRect(0.41f, 0.18f, 0.88f, 0.70f);
        static readonly Rect StatusPlate = Rect.MinMaxRect(0.43f, 0.20f, 0.86f, 0.72f);

        /// <summary>튜토리얼이 가리킬 곳들.</summary>
        public RectTransform PortraitRect => portrait != null ? portrait.rectTransform : null;
        public RectTransform BlockRect => blockBadge != null ? blockBadge.rectTransform : null;
        public RectTransform WardRect => wardBadge != null ? wardBadge.rectTransform : null;
        public RectTransform BarRect => barBg != null ? barBg.rectTransform : null;

        public static CombatantPanel Create(Transform parent, Font font, Vector2 portraitSize, bool withIntent)
        {
            float w = portraitSize.x + 40f;

            var root = UiKit.Rect("CombatantPanel", parent);
            root.sizeDelta = new Vector2(w, 400f);
            var p = root.gameObject.AddComponent<CombatantPanel>();
            p.font = font;

            float y = 0f;

            if (withIntent)
            {
                // 인텐트 — [아이콘][글자] 한 줄을 가운데 정렬 (글자 길이가 달라도 아이콘과 겹치지 않게)
                p.intentRow = UiKit.Rect("Intent", root);
                UiKit.Place(p.intentRow, new Vector2(0.5f, 1), new Vector2(0, y - 2), new Vector2(w + 60, IntentIconSize));
                var hlg = p.intentRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 6; hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true; hlg.childControlHeight = false;
                hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

                p.intentIcon = UiKit.Img("IntentIcon", p.intentRow);
                p.intentIcon.rectTransform.sizeDelta = new Vector2(IntentIconSize, IntentIconSize);
                p.intentIcon.preserveAspect = true;
                var ile = p.intentIcon.gameObject.AddComponent<LayoutElement>();
                ile.preferredWidth = IntentIconSize;

                p.intentText = UiKit.Label("IntentText", p.intentRow, font, 20, TextAnchor.MiddleLeft);
                p.intentText.rectTransform.sizeDelta = new Vector2(0, IntentIconSize);
                p.intentText.fontStyle = FontStyle.Bold;
                UiKit.Outline(p.intentText);
                y -= IntentIconSize + 6f;
            }

            p.nameText = UiKit.Label("Name", root, font, 19);
            UiKit.Place(p.nameText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y - 2), new Vector2(w + 40, 26));
            UiKit.Outline(p.nameText);
            y -= 30f;

            p.portrait = UiKit.Img("Portrait", root);
            UiKit.Place(p.portrait.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), portraitSize);
            p.portrait.preserveAspect = true;
            p.portraitHome = p.portrait.rectTransform.anchoredPosition;
            y -= portraitSize.y + 8f;

            // HP 바 — 테두리 그림은 9-slice라 폭이 달라도 양끝 장식이 늘어나지 않는다
            p.barBg = UiKit.Img("HpBarBg", root);
            UiKit.Place(p.barBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(w - 16, BarHeight));
            p.barFill = UiKit.Img("HpBarFill", p.barBg.rectTransform);
            p.barFill.type = Image.Type.Filled;
            p.barFill.fillMethod = Image.FillMethod.Horizontal;
            p.hpText = UiKit.Label("HpText", p.barBg.rectTransform, font, 16);
            UiKit.Stretch(p.hpText.rectTransform);
            p.hpText.fontStyle = FontStyle.Bold;
            UiKit.Outline(p.hpText);
            y -= BarHeight + 6f;

            // 방어 배지 2종 (블록 = 공격 방어 / 효과 방어)
            p.blockBadge = UiKit.Img("BlockBadge", root);
            UiKit.Place(p.blockBadge.rectTransform, new Vector2(0.5f, 1), new Vector2(-BadgeSize.x * 0.5f - 3f, y), BadgeSize);
            p.blockText = UiKit.Label("BlockText", p.blockBadge.rectTransform, font, 17);
            p.blockText.fontStyle = FontStyle.Bold;
            UiKit.Outline(p.blockText);

            p.wardBadge = UiKit.Img("WardBadge", root);
            UiKit.Place(p.wardBadge.rectTransform, new Vector2(0.5f, 1), new Vector2(BadgeSize.x * 0.5f + 3f, y), BadgeSize);
            p.wardText = UiKit.Label("WardText", p.wardBadge.rectTransform, font, 17);
            p.wardText.fontStyle = FontStyle.Bold;
            UiKit.Outline(p.wardText);
            y -= BadgeSize.y + 5f;

            // 상태이상 아이콘 줄
            var rowGO = UiKit.Rect("StatusRow", root);
            UiKit.Place(rowGO, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(w + 40, StatusSize.y));
            var shlg = rowGO.gameObject.AddComponent<HorizontalLayoutGroup>();
            shlg.spacing = 4; shlg.childAlignment = TextAnchor.MiddleCenter;
            shlg.childControlWidth = shlg.childControlHeight = false;
            shlg.childForceExpandWidth = shlg.childForceExpandHeight = false;
            p.statusRow = rowGO;
            y -= StatusSize.y;

            // 패널 높이를 내용(상태이상 줄 아래끝)에 딱 맞춘다 — 아래쪽 기준으로 배치할 때
            // 빈 여백 없이 손패 바로 위에 붙도록
            root.sizeDelta = new Vector2(w, -y + 4f);

            return p;
        }

        public void Bind(Combatant c, BattleTheme theme, Sprite portraitSprite)
        {
            if (c == null) return;

            if (nameText != null) nameText.text = c.name;
            UiKit.Apply(portrait, portraitSprite, new Color(0.25f, 0.27f, 0.32f, 0.9f));
            portraitBase = portrait.color;

            BindBar(c, theme);

            SetBadge(blockBadge, blockText, theme != null ? theme.blockIcon : null,
                     theme != null ? theme.blockColor : new Color(0.35f, 0.62f, 0.85f),
                     c.TotalBlock, "블록",
                     $"블록 {c.TotalBlock}", CardText.BlockInfo(c.tempBlock));
            SetBadge(wardBadge, wardText, theme != null ? theme.wardIcon : null,
                     theme != null ? theme.wardColor : new Color(0.65f, 0.45f, 0.85f),
                     c.ward, "효과",
                     $"효과 방어 {c.ward}", CardText.WardInfo(c.ward));

            BuildStatusIcons(c, theme);
        }

        /// <summary>
        /// 체력바 — 테두리 그림 안쪽(어두운 칸)에만 빨간 채움을 넣는다.
        /// 예전엔 채움이 테두리 장식까지 덮어서 바위 장식 위로 빨간 막대가 지나갔다.
        /// </summary>
        void BindBar(Combatant c, BattleTheme theme)
        {
            UiKit.Apply(barBg, theme != null ? theme.barBackground : null, new Color(0, 0, 0, 0.6f));
            UiKit.Apply(barFill, theme != null ? theme.barFill : null,
                        theme != null ? theme.hpColor : new Color(0.78f, 0.25f, 0.25f));
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = c.maxHp > 0 ? Mathf.Clamp01((float)c.hp / c.maxHp) : 0f;

            var frt = barFill.rectTransform;
            if (barBg.sprite != null && barBg.type == Image.Type.Sliced)
            {
                // 테두리를 바 높이에 맞게 줄이고(위아래 합이 높이의 40%), 채움은 그 안쪽에 딱 맞춘다
                float m = UiKit.FitSlicedBorders(barBg, 0.4f);
                var b = barBg.sprite.border / m;
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = new Vector2(b.x - 1f, b.y - 1f);
                frt.offsetMax = new Vector2(-(b.z - 1f), -(b.w - 1f));
            }
            else if (barBg.sprite != null)
            {
                // 9-slice 정보가 없는 테두리 그림 — 안쪽 칸 비율로
                frt.anchorMin = new Vector2(0.08f, 0.24f); frt.anchorMax = new Vector2(0.92f, 0.74f);
                frt.offsetMin = frt.offsetMax = Vector2.zero;
            }
            else
            {
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(-2, -2);
            }
            hpText.text = $"{c.hp} / {c.maxHp}";
        }

        void SetBadge(Image badge, Text text, Sprite icon, Color fallback, int value, string shortLabel,
                      string tipTitle, string tipBody)
        {
            bool show = value > 0;
            badge.gameObject.SetActive(show);
            if (!show) return;
            UiKit.Apply(badge, icon, fallback);
            badge.preserveAspect = false;
            bool art = icon != null;
            PlaceNumber(text, art, BadgePlate);
            text.text = art ? value.ToString() : $"{shortLabel} {value}";
            text.color = Color.white;
            TooltipTrigger.On(badge, tipTitle, tipBody);   // 아이콘이면 숫자만 보이므로 이름·설명은 마우스로
        }

        /// <summary>아트가 있으면 오른쪽 숫자 칸 안에, 없으면(단색 폴백) 칸 전체에 글자를 쓴다.</summary>
        static void PlaceNumber(Text t, bool art, Rect plate)
        {
            var rt = t.rectTransform;
            if (art) { rt.anchorMin = plate.min; rt.anchorMax = plate.max; }
            else { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; }
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void BuildStatusIcons(Combatant c, BattleTheme theme)
        {
            for (int i = statusRow.childCount - 1; i >= 0; i--)
            {
                var child = statusRow.GetChild(i);
                child.SetParent(null, false);   // Destroy는 프레임 끝이라 먼저 떼어야 줄이 바로 다시 선다
                Destroy(child.gameObject);
            }

            foreach (var kv in c.status)
            {
                if (kv.Value == 0) continue;
                var cell = UiKit.Img($"St_{kv.Key}", statusRow);
                cell.rectTransform.sizeDelta = StatusSize;
                var sprite = theme != null ? theme.IconFor(kv.Key) : null;
                UiKit.Apply(cell, sprite, BattleTheme.StatusColor(kv.Key));

                var t = UiKit.Label("V", cell.rectTransform, font, 16);
                t.fontStyle = FontStyle.Bold;
                PlaceNumber(t, sprite != null, StatusPlate);
                t.text = sprite != null ? kv.Value.ToString() : $"{CardText.Kor(kv.Key)}{kv.Value}";
                t.color = sprite != null ? Color.white : new Color(0.1f, 0.1f, 0.12f);
                if (sprite != null) UiKit.Outline(t);

                TooltipTrigger.On(cell, $"{CardText.Kor(kv.Key)} {kv.Value}", CardText.StatusInfo(kv.Key, kv.Value));
            }

            // 상태이상 칸 말고도 걸려 있는 지속 효과 — 안 보이면 걸었는지 알 수 없다
            if (c.poisonAmp > 0)
                TextChip("St_PoisonAmp", $"뿌리+{c.poisonAmp}", new Color(0.36f, 0.55f, 0.28f),   // 뿌리내림: 중독 피해 +N
                         $"뿌리내림 +{c.poisonAmp}", CardText.PoisonAmpInfo(c.poisonAmp));
            foreach (var p in c.periodic)
                TextChip($"St_Periodic_{p.status}", $"{CardText.Kor(p.status)}+{p.value}·{p.turnsLeft}턴",
                         BattleTheme.StatusColor(p.status) * 0.8f,                           // 곰팡이 정원 등
                         $"지속 {CardText.Kor(p.status)}", CardText.PeriodicInfo(p.status, p.value, p.turnsLeft));
        }

        void TextChip(string name, string text, Color color, string tipTitle, string tipBody)
        {
            var cell = UiKit.Img(name, statusRow);
            cell.rectTransform.sizeDelta = new Vector2(84, StatusSize.y - 6f);
            color.a = 1f;
            cell.color = color;
            var t = UiKit.Label("V", cell.rectTransform, font, 14);
            UiKit.Stretch(t.rectTransform);
            t.text = text;
            t.color = new Color(0.08f, 0.08f, 0.1f);
            TooltipTrigger.On(cell, tipTitle, tipBody);
        }

        /// <summary>
        /// 적 인텐트 표시 (적 설계 §2.4 — 공격은 피해량, 다타면 n×m).
        /// shownAmount = 실제로 들어올 수치 (장별 배율·힘·약화·취약을 호출부가 반영해서 넘긴다).
        /// </summary>
        public void SetIntent(EnemyAction a, BattleTheme theme, int shownAmount)
        {
            if (intentText == null) return;
            if (a == null)
            {
                intentText.text = "";
                TooltipTrigger.On(intentText, null, null);   // 빈 말풍선은 안 뜬다
                if (intentIcon) intentIcon.gameObject.SetActive(false);
                return;
            }

            string name = string.IsNullOrEmpty(a.label) ? a.kind.ToString() : a.label;
            int amt = Mathf.Max(0, shownAmount);
            intentText.text = a.kind switch
            {
                EnemyActionKind.Attack => a.hits > 1 ? $"{name}  {amt}×{a.hits}" : $"{name}  {amt}",
                EnemyActionKind.Block => $"{name}  블록 {amt}",
                EnemyActionKind.Debuff => $"{name}  {CardText.Kor(a.status)} {a.amount}",
                EnemyActionKind.Buff => $"{name}  {CardText.Kor(a.status)} +{a.amount}",
                _ => name,
            };
            intentText.color = a.kind == EnemyActionKind.Attack ? new Color(1f, 0.72f, 0.66f) : Color.white;

            // 아이콘·글 어디에 올려도 같은 설명 — 실제 들어올 수치로
            string tipTitle = CardText.IntentTitle(a), tipBody = CardText.IntentInfo(a, amt);
            TooltipTrigger.On(intentText, tipTitle, tipBody);

            if (intentIcon == null) return;
            TooltipTrigger.On(intentIcon, tipTitle, tipBody);
            var sp = theme != null ? theme.IntentFor(a.kind) : null;
            intentIcon.gameObject.SetActive(true);
            UiKit.Apply(intentIcon, sp, a.kind switch
            {
                EnemyActionKind.Attack => new Color(0.9f, 0.35f, 0.3f),
                EnemyActionKind.Block => new Color(0.35f, 0.62f, 0.85f),
                EnemyActionKind.Debuff => new Color(0.7f, 0.45f, 0.85f),
                _ => new Color(0.9f, 0.75f, 0.3f),
            });
            intentIcon.preserveAspect = true;
        }

        // ══════════════════════════════════════════════ 피격 연출 ══════════════
        /// <summary>
        /// 초상화 위로 떠오르는 숫자. slot으로 좌우를 벌려 여러 개가 동시에 떠도 겹치지 않게 한다
        /// (0 = 가운데 체력, 1 = 왼쪽 블록, 2 = 오른쪽 효과 방어).
        /// </summary>
        public void Float(string msg, Color color, int slot = 0)
        {
            if (portrait == null) return;
            var prt = portrait.rectTransform;
            var rt = (RectTransform)transform;
            // 떠오르는 글자는 패널 가운데 기준으로 놓이므로, 초상화 중심을 패널 가운데 기준 좌표로 바꾼다
            Vector2 local = rt.InverseTransformPoint(prt.TransformPoint(prt.rect.center));
            Vector2 center = local - rt.rect.center;
            float dx = slot switch { 1 => -prt.rect.width * 0.3f, 2 => prt.rect.width * 0.3f, _ => 0f };
            FloatText.Spawn(transform, font, msg, color, center + new Vector2(dx, slot == 0 ? 10f : -30f),
                            slot == 0 ? 34 : 24, slot * 0.12f);
        }

        /// <summary>맞았을 때 초상화가 붉게 번쩍이며 흔들린다.</summary>
        public void Flash()
        {
            if (portrait == null || !isActiveAndEnabled) return;
            if (flashing != null) StopCoroutine(flashing);
            flashing = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            var prt = portrait.rectTransform;
            const float dur = 0.32f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                portrait.color = Color.Lerp(new Color(1f, 0.45f, 0.42f, portraitBase.a), portraitBase, k);
                float shake = (1f - k) * 7f;
                prt.anchoredPosition = portraitHome + new Vector2(Random.Range(-shake, shake), Random.Range(-shake * 0.4f, shake * 0.4f));
                yield return null;
            }
            portrait.color = portraitBase;
            prt.anchoredPosition = portraitHome;
            flashing = null;
        }

        void OnDisable()
        {
            // 흔들리던 중에 꺼지면 자리가 어긋난 채로 남는다
            if (portrait != null) { portrait.color = portraitBase; portrait.rectTransform.anchoredPosition = portraitHome; }
            flashing = null;
        }
    }
}
