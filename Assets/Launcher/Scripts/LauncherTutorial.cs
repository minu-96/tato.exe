using System.Collections;
using System.Collections.Generic;
using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;
using Keys = TatoGames.CardGame.TutorialOverlay.Keys;
using Step = TatoGames.CardGame.TutorialOverlay.Step;
using Side = TatoGames.CardGame.TutorialOverlay.Side;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 런처 튜토리얼 — 처음 켰을 때 전체 흐름, 탭을 처음 열 때 그 탭 설명.
    /// LauncherNavigation이 실행 중에 붙인다(씬 배선 없음).
    ///
    /// 함께 하는 일:
    ///   · 설정 탭에 `튜토리얼 다시 보기` 버튼을 만든다
    ///   · 홈 GAME START 아래에 "진행 중인 런 / 새 런" 한 줄을 띄운다
    ///     (예전엔 런이 진행 중인지, 누르면 이어서 하는지 새로 시작하는지 알 수 없었다)
    /// </summary>
    public class LauncherTutorial : MonoBehaviour
    {
        LauncherNavigation nav;
        Font font;
        Text homeStatus;

        void Awake()
        {
            nav = GetComponent<LauncherNavigation>();
            font = nav != null ? nav.UiFont() : null;
        }

        void Start()
        {
            if (nav == null) return;
            nav.TabShown += OnTab;
            PlayerData.Changed += RefreshHome;
            SetupSettingsButton();
            SetupHomeStatus();
            StartCoroutine(IntroWhenReady());
        }

        void OnDestroy()
        {
            if (nav != null) nav.TabShown -= OnTab;
            PlayerData.Changed -= RefreshHome;
        }

        // ══════════════════════════════════════════════ 흐름 ══════════════════
        /// <summary>전환 연출과 미니게임 보상 팝업이 걷힌 뒤에 띄운다 — 가려진 채로 지나가지 않게.</summary>
        IEnumerator IntroWhenReady()
        {
            yield return null;
            while (LauncherTransition.Busy || FindAnyObjectByType<RewardSummaryPopup>() != null)
                yield return null;
            yield return new WaitForSecondsRealtime(0.2f);

            if (!TutorialOverlay.Seen(Keys.LauncherIntro)) RunIntro(force: false);
            else OnTab(nav.CurrentPanel);
        }

        void RunIntro(bool force)
        {
            TutorialOverlay.Run(transform, font, Keys.LauncherIntro, IntroSteps(), force,
                                onClosed: () => OnTab(nav.CurrentPanel));
        }

        /// <summary>탭을 처음 열면 그 탭 안내 — 첫 안내(전체 흐름)를 본 뒤부터.</summary>
        void OnTab(string panel)
        {
            if (panel == "Panel_Home") RefreshHome();
            if (!TutorialOverlay.Seen(Keys.LauncherIntro)) return;
            switch (panel)
            {
                case "Panel_Storage": Later(Keys.LauncherStorage, StorageSteps); break;
                case "Panel_Minigame": Later(Keys.LauncherMinigame, MinigameSteps); break;
                case "Panel_Shop": Later(Keys.LauncherShop, ShopSteps); break;
            }
        }

        /// <summary>한 프레임 뒤에 — 방금 켠 탭의 카드·타일이 자리를 잡은 뒤여야 정확히 가리킨다.</summary>
        void Later(string key, System.Func<List<Step>> steps)
        {
            if (TutorialOverlay.Seen(key) || !isActiveAndEnabled) return;
            StartCoroutine(RunLater(key, steps));
        }

        IEnumerator RunLater(string key, System.Func<List<Step>> steps)
        {
            yield return null;
            TutorialOverlay.Run(transform, font, key, steps());
        }

        /// <summary>설정 탭 `다시 보기` — 모든 안내를 처음으로 되돌리고 전체 흐름부터 다시.</summary>
        void ReplayAll()
        {
            TutorialOverlay.CloseAll();
            TutorialOverlay.ResetAll();
            nav.ShowByName("Panel_Home");
            RunIntro(force: true);
        }

        // ══════════════════════════════════════════════ 대상 찾기 ═════════════
        RectTransform Panel(string name)
        {
            foreach (var p in nav.panels)
                if (p != null && p.name == name) return p.transform as RectTransform;
            return null;
        }

        RectTransform Find(string name) => TutorialOverlay.FindDeep(transform, name);
        RectTransform FindIn(string panel, string name) => TutorialOverlay.FindDeep(Panel(panel), name);

        RectTransform NavButton(string name)
        {
            foreach (var b in nav.navButtons)
                if (b != null && b.name == name) return b.transform as RectTransform;
            return null;
        }

        /// <summary>감자창고 카드 칸 (보이는 순서대로).</summary>
        IEnumerable<Transform> StorageCells()
        {
            var content = TutorialOverlay.FindDeep(FindIn("Panel_Storage", "Scroll_Cards"), "Content");
            if (content == null) yield break;
            for (int i = 0; i < content.childCount; i++) yield return content.GetChild(i);
        }

        RectTransform FirstCellPart(params string[] names)
        {
            foreach (var cell in StorageCells())
                foreach (var n in names)
                {
                    var t = cell.Find(n);
                    if (t != null && t.gameObject.activeInHierarchy) return t as RectTransform;
                }
            return null;
        }

        RectTransform FirstCell()
        {
            foreach (var cell in StorageCells()) return cell as RectTransform;
            return null;
        }

        RectTransform FirstGameTile()
        {
            var tiles = FindIn("Panel_Minigame", "Tiles");
            if (tiles == null) return null;
            for (int i = 0; i < tiles.childCount; i++)
                if (tiles.GetChild(i).name.StartsWith("Game_")) return tiles.GetChild(i) as RectTransform;
            return null;
        }

        // ══════════════════════════════════════════════ 안내 문구 ═════════════
        List<Step> IntroSteps() => new()
        {
            new("tato.exe에 오신 걸 환영해요!",
                "감자 카드로 싸우는 로그라이크예요.\n" +
                "<b>미니게임</b>으로 카드를 모으고 → <b>감자창고</b>에서 덱을 짜고 → <b>GAME START</b>로 본편에 도전해요.\n" +
                "화면을 짧게 둘러볼게요.",
                null, Side.Auto, () => nav.ShowByName("Panel_Home")),
            new("토인", "게임의 돈이에요. 전투에서 벌고, 미니게임 구입과 카드 강화·제거에 써요.",
                () => Find("Chip_TOIN"), Side.Below),
            new("상단 표시", "왼쪽부터 <b>최고 도달 스테이지</b> · <b>런 덱 카드 수</b> · <b>감자창고 카드 수</b>예요.\n" +
                            "마우스를 올리면 설명이 나와요.",
                () => Find("Header_Left"), Side.Below),
            new("미니게임", "판이 끝나면 성적에 따라 카드를 받아요. 잘할수록 많이, 희귀하게!\n" +
                           "<b>늘어나라</b>는 공격 · <b>모아모아</b>는 방어 · <b>밭의 생존자</b>는 중독 카드를 줘요.",
                () => NavButton("Btn_Minigame"), Side.Right),
            new("감자창고", "모은 카드를 보고 <b>덱을 짜는 곳</b>이에요.\n" +
                           "미니게임 카드는 창고로 들어오니, <b>덱 +</b>를 눌러야 전투에 나와요.",
                () => NavButton("Btn_Storage"), Side.Right),
            new("상점", "토인으로 새 미니게임을 사요.", () => NavButton("Btn_Shop"), Side.Right),
            new("시작하기", "준비되면 <b>GAME START</b>! 처음엔 시작 카드 8장으로 싸워요.\n" +
                           "<color=#FF9A8A>전투에서 지면 덱에 넣은 카드가 사라져요</color> (시작 카드는 남아요).\n" +
                           "안내는 설정 탭에서 다시 볼 수 있어요.",
                () => FindIn("Panel_Home", "Btn_GameStart"), Side.Below, () => nav.ShowByName("Panel_Home")),
        };

        List<Step> StorageSteps() => new()
        {
            new("감자창고", "가진 카드가 한 장씩 따로 보여요. 같은 카드라도 수명이 제각각이에요.\n" +
                           "카드를 <b>우클릭</b>하면 등급과 용어 설명을 자세히 볼 수 있어요.",
                FirstCell, Side.Right),
            new("카드 수명", "새 런을 시작할 때마다 모든 카드가 한 살 먹어요.\n" +
                            "<b>n런 남음</b> → 마지막 런엔 <b>싹</b>이 나서 더 강해지고 → 그 다음엔 <b>썩어서</b> 못 써요.\n" +
                            "시작 카드(<b>귀속</b>)는 썩지 않아요.",
                () => FirstCellPart("State", "Bound"), Side.Right),
            new("덱 편성", "<b>덱 +</b>를 누르면 이 카드가 런 덱에 들어가요. 다시 누르면 빠져요.\n" +
                          "덱은 <b>최소 8장 · 최대 30장</b>. 런이 진행 중일 때는 바꿀 수 없어요.",
                () => FirstCellPart("DeckToggle"), Side.Right),
            new("판매", "필요 없는 카드는 토인을 받고 팔 수 있어요 (한 번 더 눌러 확인).\n썩은 카드는 3토인. 시작 카드는 팔 수 없어요.",
                () => FirstCellPart("Sell"), Side.Right),
            new("찾기", "등급·종류 필터, 이름 검색, 정렬로 원하는 카드를 찾아요.",
                () => FindIn("Panel_Storage", "FilterBar"), Side.Below),
            new("주의", "여기에 런 덱 장수가 보여요.\n<color=#FF9A8A>전투에서 지면 덱에 넣은 카드가 사라져요</color> (시작 카드 제외). 아끼는 카드는 신중하게!",
                () => FindIn("Panel_Storage", "DeckCount"), Side.Below),
        };

        List<Step> MinigameSteps() => new()
        {
            new("미니게임", "<b>GAME START</b>로 실행해요. 판이 끝나면 성적만큼 카드를 받고,\n런처로 돌아오면 받은 카드를 알려줘요 (감자창고에 빨간 점).",
                FirstGameTile, Side.Right),
            new("저장공간", "게임은 설치해야 실행돼요. 설치하면 저장공간을 차지해요.\n<b>삭제</b>는 설치만 내리는 거라 카드는 그대로 남아요.",
                () => FindIn("Panel_Minigame", "Label_Storage"), Side.Above),
            new("새 게임", "<b>+ PURCHASE</b>를 누르면 상점으로 가요.",
                () => FindIn("Panel_Minigame", "Purchase"), Side.Left),
        };

        List<Step> ShopSteps() => new()
        {
            new("상점", "토인으로 미니게임을 사요. 저장공간이 남으면 바로 설치돼요.",
                () => FindIn("Panel_Shop", "Tiles"), Side.Below),
            new("판매", "산 게임은 되팔 수 있어요 — 구매가의 30%를 돌려받지만\n<color=#FF9A8A>그 게임에서 얻은 카드가 모두 사라져요.</color> 한 번 더 눌러야 팔려요.",
                () => FindIn("Panel_Shop", "Tiles"), Side.Below),
        };

        // ══════════════════════════════════════════════ 설정 · 홈 ═════════════
        /// <summary>설정 탭에 `튜토리얼 다시 보기` — 다른 설정 버튼과 같은 모양으로.</summary>
        void SetupSettingsButton()
        {
            var panel = Panel("Panel_Setting");
            if (panel == null) return;
            var existing = panel.Find("Btn_Tutorial");
            if (existing != null)
            {
                if (existing.TryGetComponent(out Button eb)) { eb.onClick.RemoveAllListeners(); eb.onClick.AddListener(ReplayAll); }
                return;
            }

            var likeLabel = TutorialOverlay.FindDeep(panel, "Label_Fullscreen");
            var likeLabelText = likeLabel != null ? likeLabel.GetComponent<Text>() : null;

            // 라벨
            var lrt = NewRect("Label_Tutorial", panel);
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(360, 44);
            lrt.anchoredPosition = new Vector2(-280, -190);
            var lt = lrt.gameObject.AddComponent<Text>();
            lt.text = "튜토리얼";
            lt.font = likeLabelText != null ? likeLabelText.font : font;
            lt.fontSize = likeLabelText != null ? likeLabelText.fontSize : 22;
            lt.color = likeLabelText != null ? likeLabelText.color : Color.white;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.raycastTarget = false;
            lt.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 버튼 — 전체화면 토글과 같은 모양의 글자 없는 알약 (DisplaySettingsView가 토글도 같은 모양으로 바꾼다)
            var btn = UiKit.PillButton(panel, "Btn_Tutorial", lt.font, "다시 보기", new Vector2(200, 48),
                                       UiKit.FieldBg, UiKit.FieldText, 20);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(60, -190);
            UiKit.Outline(btn.targetGraphic, 1f, new Color(1f, 1f, 1f, 0.2f));
            btn.onClick.AddListener(ReplayAll);

            TooltipTrigger.On(btn.targetGraphic, "튜토리얼 다시 보기", "모든 안내를 처음부터 다시 보여줘요.\n탭을 처음 열 때 나오던 안내도 다시 나와요.");
        }

        /// <summary>홈 GAME START 아래 — 누르면 무엇이 시작되는지.</summary>
        void SetupHomeStatus()
        {
            var home = Panel("Panel_Home");
            var start = TutorialOverlay.FindDeep(home, "Btn_GameStart");
            if (home == null || start == null) return;

            var rt = NewRect("Label_RunStatus", home);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760, 56);
            rt.anchoredPosition = start.anchoredPosition + new Vector2(0f, -(start.sizeDelta.y * 0.5f + 40f));
            homeStatus = rt.gameObject.AddComponent<Text>();
            homeStatus.font = font; homeStatus.fontSize = 18;
            homeStatus.color = new Color(0.82f, 0.84f, 0.9f);
            homeStatus.alignment = TextAnchor.MiddleCenter;
            homeStatus.supportRichText = true;
            homeStatus.raycastTarget = false;
            homeStatus.horizontalOverflow = HorizontalWrapMode.Overflow;
            homeStatus.verticalOverflow = VerticalWrapMode.Overflow;
            homeStatus.lineSpacing = 1.15f;
            RefreshHome();
        }

        void RefreshHome()
        {
            if (homeStatus == null) return;
            if (RunState.Active)
            {
                int cols = RunState.Columns.Count;
                string where = RunState.StageIntro ? "새 스테이지 시작"
                             : $"노드 {Mathf.Min(RunState.Col + 1, cols)}/{cols}";
                homeStatus.text =
                    $"<color=#FFC845>진행 중인 런</color>   {RunState.StageName} · {where} · 체력 {RunState.PlayerHp}\n" +
                    "<color=#A0A4AE>GAME START를 누르면 이어서 해요 · 런이 끝날 때까지 덱을 바꿀 수 없어요</color>";
            }
            else
            {
                homeStatus.text =
                    $"런 덱 {PlayerData.DeckSize()}장으로 새 런을 시작해요\n" +
                    "<color=#A0A4AE>새 런을 시작하면 모든 카드가 한 살 먹어요</color>";
            }
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
