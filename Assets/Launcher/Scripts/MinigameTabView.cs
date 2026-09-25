using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 미니게임 탭 — <b>보유 중인 게임만</b> 타일로 보여준다.
    /// 여기서 하는 일은 실행 · 설치 · 삭제. 구매·판매는 상점 담당이다.
    /// 맨 오른쪽 `+ Purchase` 타일을 누르면 상점 탭으로 넘어간다.
    ///
    /// 보유 목록이 바뀌면 타일 수도 달라지므로 런타임에 그린다.
    /// </summary>
    public class MinigameTabView : MonoBehaviour
    {
        /// <summary>게임 id → 타일 아트. 에디터 빌더가 채운다(스프라이트가 Resources에 없어서 전달 필요).</summary>
        [System.Serializable]
        public class TileArt { public string gameId; public Sprite sprite; }

        [Header("타일 아트")]
        public System.Collections.Generic.List<TileArt> tileArts = new();
        public Sprite purchaseTile;
        public Sprite startIdle, startHover, startPressed;
        public Sprite purchaseIdle, purchaseHover, purchasePressed;
        public Font labelFont;

        [Header("배치 (원본 씬 좌표)")]
        public float tileY = 91f;
        public float firstX = -377f;
        public float stepX = 240.5f;
        public Vector2 tileSize = new(234, 294);
        public Vector2 buttonSize = new(171, 61);
        public float buttonY = -76.5f;

        [Tooltip("타일만 들어가는 컨테이너 — 다시 그릴 때 여기 안쪽만 지운다")]
        public RectTransform tileRoot;

        [Tooltip("저장공간 현황 — 항상 떠 있다")]
        public Text storageLabel;

        [Tooltip("저장공간 사용량 막대 (배경 / 채움)")]
        public Image storageBarBg;
        public Image storageBarFill;

        [Tooltip("설치·삭제 결과 안내 (일시)")]
        public Text noticeLabel;

        LauncherNavigation nav;

        void Awake() => nav = GetComponentInParent<LauncherNavigation>();

        void OnEnable() { StorageData.Changed += Rebuild; Rebuild(); }
        void OnDisable() => StorageData.Changed -= Rebuild;

        public void Rebuild()
        {
            if (tileRoot == null)
            {
                Debug.LogWarning("[TatoGames] MinigameTabView.tileRoot 미설정 — Fill Tab Panels 를 실행하세요");
                return;
            }

            // 타일 컨테이너 안쪽만 지운다.
            // (예전엔 패널의 모든 자식을 지워서 저장공간 라벨·막대까지 같이 날아갔다)
            for (int i = tileRoot.childCount - 1; i >= 0; i--)
            {
                var c = tileRoot.GetChild(i);
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }

            int slot = 0;
            foreach (var g in StorageData.MiniGames)
            {
                if (!StorageData.IsOwned(g.id)) continue;     // 미보유는 아예 안 보인다
                BuildGameTile(g, firstX + slot * stepX);
                slot++;
            }

            BuildPurchaseTile(firstX + slot * stepX);
            UpdateStorage();
        }

        void BuildGameTile(GameEntry g, float x)
        {
            var tile = Tile("Game_" + g.id, ArtFor(g.id), x);
            bool installed = StorageData.IsInstalled(g.id);

            // 설치돼 있으면 실행 버튼, 아니면 설치 버튼
            var main = Button(tile.transform, "Btn_GameStart", startIdle, startHover, startPressed,
                              0f, buttonY, buttonSize);
            var caption = Caption(main.transform, installed ? "" : "설치");

            if (installed)
            {
                // 실행은 GameLaunchButton 이 담당 (전환 연출·해상도까지 처리)
                var gl = main.gameObject.AddComponent<GameLaunchButton>();
                gl.sceneName = g.sceneName;
                gl.exeLabel = g.exeLabel;
            }
            else
            {
                tile.color = new Color(0.45f, 0.45f, 0.5f, 1f);   // 미설치는 어둡게
                bool fits = g.sizeMb <= StorageData.FreeMb;
                caption.text = fits ? $"설치 ({g.sizeMb}MB)" : "공간 부족";
                main.interactable = fits;
                main.onClick.AddListener(() =>
                {
                    int before = StorageData.UsedMb;
                    Notice(StorageData.TryInstall(g.id, out string why)
                        ? $"{g.displayName} 설치 — 저장공간 {before} → {StorageData.UsedMb}MB"
                        : why, warn: why != null);
                });
            }

            // 용량 + 삭제
            var info = Text(tile.transform, "Info",
                            $"{g.sizeMb}MB · {(installed ? "설치됨" : "미설치")}",
                            17, TextAnchor.LowerCenter, new Vector2(0, 10), new Vector2(220, 26));
            info.color = installed ? new Color(0.85f, 0.9f, 0.95f) : new Color(1f, 0.7f, 0.55f);

            if (installed)
            {
                var del = Button(tile.transform, "Btn_Uninstall", null, null, null,
                                 0f, -132f, new Vector2(120, 34));
                var delImg = del.GetComponent<Image>();
                delImg.color = new Color(0.32f, 0.22f, 0.18f, 0.95f);
                Caption(del.transform, "삭제", 17).color = new Color(1f, 0.75f, 0.65f);
                del.onClick.AddListener(() =>
                {
                    int before = StorageData.UsedMb;
                    Notice(StorageData.TryUninstall(g.id, out string why)
                        ? $"{g.displayName} 삭제 — 저장공간 {before} → {StorageData.UsedMb}MB (카드는 그대로)"
                        : why, warn: why != null);
                });
            }
        }

        void BuildPurchaseTile(float x)
        {
            var tile = Tile("Purchase", purchaseTile, x);
            var btn = Button(tile.transform, "Btn_Purchase",
                             purchaseIdle, purchaseHover, purchasePressed, 0f, buttonY, buttonSize);
            btn.onClick.AddListener(() =>
            {
                if (nav == null) nav = GetComponentInParent<LauncherNavigation>();
                if (nav != null) nav.ShowByName("Panel_Shop");
            });
        }

        /// <summary>저장공간은 이 탭에서만 보여준다 — 설치·삭제가 여기서 일어나므로.</summary>
        void UpdateStorage()
        {
            int used = StorageData.UsedMb, total = StorageData.TotalMb;
            if (storageLabel != null)
            {
                storageLabel.text = $"저장공간   {used} / {total} MB   (남은 공간 {StorageData.FreeMb}MB)";
                storageLabel.color = StorageData.FreeMb <= 0
                    ? new Color(1f, 0.55f, 0.45f) : new Color(0.85f, 0.9f, 0.95f);
            }
            if (storageBarFill != null && storageBarBg != null)
            {
                float ratio = total > 0 ? Mathf.Clamp01((float)used / total) : 0f;
                storageBarFill.rectTransform.anchorMin = Vector2.zero;
                storageBarFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                storageBarFill.rectTransform.offsetMin = Vector2.zero;
                storageBarFill.rectTransform.offsetMax = Vector2.zero;
                storageBarFill.color = ratio > 0.95f
                    ? new Color(0.75f, 0.35f, 0.3f) : new Color(0.35f, 0.62f, 0.85f);
            }
        }

        void Notice(string msg, bool warn = false)
        {
            if (noticeLabel == null || msg == null) return;
            noticeLabel.text = msg;
            noticeLabel.color = warn ? new Color(1f, 0.55f, 0.45f) : new Color(0.8f, 0.85f, 0.9f);
        }

        // ── 조립 헬퍼 ──
        Sprite ArtFor(string gameId)
        {
            var a = tileArts.Find(x => x != null && x.gameId == gameId);
            return a != null ? a.sprite : null;
        }

        Image Tile(string name, Sprite sp, float x)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(tileRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = tileSize;
            rt.anchoredPosition = new Vector2(x, tileY);
            var img = go.GetComponent<Image>();
            img.sprite = sp; img.raycastTarget = false;
            return img;
        }

        Button Button(Transform parent, string name, Sprite idle, Sprite hover, Sprite pressed,
                      float x, float y, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, y);
            var img = go.GetComponent<Image>();
            img.sprite = idle;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (hover != null || pressed != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var ss = btn.spriteState;
                ss.highlightedSprite = hover != null ? hover : idle;
                ss.pressedSprite = pressed != null ? pressed : idle;
                ss.selectedSprite = idle;
                btn.spriteState = ss;
            }
            return btn;
        }

        Text Caption(Transform parent, string text, int size = 20)
        {
            var t = Text(parent, "Text", text, size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        Text Text(Transform parent, string name, string text, int size, TextAnchor anchor,
                  Vector2 pos, Vector2 size2)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            bool bottom = anchor == TextAnchor.LowerCenter;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, bottom ? 0f : 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size2;
            var t = go.GetComponent<Text>();
            t.text = text; t.font = labelFont; t.fontSize = size;
            t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
    }
}
