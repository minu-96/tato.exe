using System.Collections.Generic;
using UnityEngine;

namespace TatoGames.Launcher
{
    /// <summary>화면 설정을 따로 갖는 단위. 게임마다 아트 비율이 달라 목록도 다르다.</summary>
    public enum DisplayTarget
    {
        Launcher,        // 런처 셸 — 아트가 1280×900 (64:45)
        MainGame,        // 카드 로그라이크(Battle) — 16:9, 유일하게 전체화면 토글이 있다
        Neulteona,       // 늘어나라 pooo-tato — 화면 아트가 1024×768 (4:3)
        MoaMoa,          // 모아모아 10tato — 16:9
        FieldSurvivor,   // 밭의 생존자 — 16:9
    }

    /// <summary>
    /// 화면 해상도 설정 (§3·v0.6 §5 해상도 정책).
    ///
    /// <b>게임마다 그린 아트의 비율이 다르다.</b> 하나의 해상도를 전부에 강요하면
    /// 늘어나라(4:3)나 런처(64:45)가 반드시 어긋난다. 그래서 대상별로 자기 비율의
    /// 목록을 갖고 따로 기억한다.
    ///
    /// - 미니게임: 각 타이틀 화면에서 ◀▶ 로만 바꾼다 (런처 설정에 노출하지 않음)
    /// - 런처 · 메인 게임: 런처 설정 탭의 드롭다운
    /// - 전체화면 토글: 메인 게임에만 있다 (런처는 창 셸이라는 컨셉이라 항상 창)
    ///
    /// 씬 배선은 필요 없다 — <see cref="DisplayBootstrap"/>이 씬이 바뀔 때마다 알아서 적용한다.
    /// </summary>
    public static class DisplaySettings
    {
        class Profile
        {
            public string key;
            public Vector2Int[] sizes;
            public int defaultIndex;
            public bool canFullscreen;
        }

        static readonly Dictionary<DisplayTarget, Profile> Profiles = new()
        {
            // 런처 아트는 1280×900 (64:45) — 같은 비율로만 키운다
            [DisplayTarget.Launcher] = new Profile
            {
                key = "launcher", defaultIndex = 1, canFullscreen = false,
                sizes = new[] { new Vector2Int(1024, 720), new Vector2Int(1280, 900), new Vector2Int(1536, 1080) },
            },
            [DisplayTarget.MainGame] = new Profile
            {
                key = "maingame", defaultIndex = 0, canFullscreen = true,
                sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) },
            },
            // 늘어나라는 화면 아트가 1024×768 (4:3)
            [DisplayTarget.Neulteona] = new Profile
            {
                key = "snake", defaultIndex = 1, canFullscreen = false,
                sizes = new[] { new Vector2Int(800, 600), new Vector2Int(1024, 768), new Vector2Int(1280, 960) },
            },
            [DisplayTarget.MoaMoa] = new Profile
            {
                key = "moamoa", defaultIndex = 0, canFullscreen = false,
                sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) },
            },
            [DisplayTarget.FieldSurvivor] = new Profile
            {
                key = "field", defaultIndex = 2, canFullscreen = false,
                sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) },
            },
        };

        const string FullscreenKey = "tato_maingame_fullscreen";

        /// <summary>설정이 바뀌면 발생 (UI 갱신용).</summary>
        public static event System.Action Changed;

        static Profile P(DisplayTarget t) => Profiles[t];
        static string SizeKey(DisplayTarget t) => "tato_res_" + P(t).key;

        public static Vector2Int[] SizesFor(DisplayTarget t) => P(t).sizes;
        public static bool CanFullscreen(DisplayTarget t) => P(t).canFullscreen;

        public static int IndexOf(DisplayTarget t)
        {
            var p = P(t);
            return Mathf.Clamp(PlayerPrefs.GetInt(SizeKey(t), p.defaultIndex), 0, p.sizes.Length - 1);
        }

        public static Vector2Int SizeOf(DisplayTarget t) => P(t).sizes[IndexOf(t)];

        public static string Describe(DisplayTarget t, int index)
        {
            var p = P(t);
            var s = p.sizes[Mathf.Clamp(index, 0, p.sizes.Length - 1)];
            return $"{s.x}×{s.y}";
        }

        /// <summary>메인 게임 전체화면 여부. 기본값 = 전체화면.</summary>
        public static bool MainGameFullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static void SetIndex(DisplayTarget t, int index)
        {
            var p = P(t);
            PlayerPrefs.SetInt(SizeKey(t), Mathf.Clamp(index, 0, p.sizes.Length - 1));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>미니게임 타이틀의 ◀▶ — 목록 안에서만 움직이고 양끝에서 멈춘다.</summary>
        public static void Step(DisplayTarget t, int delta)
        {
            SetIndex(t, IndexOf(t) + delta);
            ApplyFor(t);
        }

        /// <summary>이 대상의 설정을 실제 화면에 적용한다.</summary>
        public static void ApplyFor(DisplayTarget t)
        {
#if UNITY_EDITOR
            // 에디터 Game뷰는 진짜 창이 아니라 SetResolution이 의미가 없다 — 빌드에서만 적용
            return;
#else
            if (t == DisplayTarget.MainGame && MainGameFullscreen)
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight,
                                     FullScreenMode.FullScreenWindow);
                return;
            }
            var s = FitToScreen(t, SizeOf(t));
            Screen.SetResolution(s.x, s.y, FullScreenMode.Windowed);
#endif
        }

        /// <summary>창 테두리·메뉴바·작업표시줄 몫으로 남겨두는 높이.</summary>
        const int ScreenMarginY = 80;

        /// <summary>
        /// 창 모드인데 모니터보다 크면(예: 1080p 모니터에 1920×1080 창) 창이 화면 밖으로 잘린다.
        /// 그 대상의 목록에서 화면에 들어가는 가장 큰 크기로 낮춘다. 저장된 선택은 그대로 둔다
        /// (큰 모니터로 옮기면 다시 원래 크기로 뜬다). 들어가는 게 없으면 가장 작은 크기.
        /// </summary>
        public static Vector2Int FitToScreen(DisplayTarget t, Vector2Int wanted)
        {
            int maxW = Display.main.systemWidth, maxH = Display.main.systemHeight - ScreenMarginY;
            if (maxW <= 0 || maxH <= 0) return wanted;
            if (wanted.x <= maxW && wanted.y <= maxH) return wanted;

            var sizes = P(t).sizes;
            Vector2Int best = sizes[0];
            foreach (var s in sizes)
                if (s.x <= maxW && s.y <= maxH && s.x * s.y > best.x * best.y) best = s;
            return best;
        }
    }
}
