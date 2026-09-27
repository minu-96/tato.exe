using System.Collections.Generic;
using System.Linq;
using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 런처 설정 탭의 화면 설정 UI.
    ///
    /// 여기서 다루는 건 <b>런처와 메인 게임뿐</b>이다 — 미니게임 해상도는 각 타이틀 화면의
    /// ◀▶ 위젯에서만 바꾼다(<see cref="MinigameDisplayStepper"/>).
    /// 전체화면 토글도 메인 게임에만 있다. 런처는 "창 셸" 컨셉이라 항상 창이다.
    /// </summary>
    public class DisplaySettingsView : MonoBehaviour
    {
        [Tooltip("런처 해상도")]
        public Dropdown launcherDropdown;

        [Tooltip("메인 게임(카드 로그라이크) 해상도")]
        public Dropdown mainGameDropdown;

        [Tooltip("메인 게임 전체화면 토글 — 현재 상태가 글자로 보인다")]
        public Button fullscreenToggle;
        public Text fullscreenToggleLabel;

        bool building;   // 드롭다운을 코드로 채우는 중엔 콜백을 무시

        void Start()
        {
            Bind(launcherDropdown, DisplayTarget.Launcher);
            Bind(mainGameDropdown, DisplayTarget.MainGame);

            // 설정 탭 전용 그림이 없어 감자창고 필터 그림('전체' 글자가 박힌 알약)을 빌려 쓰고 있었다 —
            // 드롭다운·버튼마다 '전체'가 겹쳐 보였다. 글자 없는 어두운 알약으로 바꾼다 (항목을 채운 뒤에 — 목록 높이를 맞추려고)
            UiKit.StyleDarkDropdown(launcherDropdown, 18, replaceBackground: true);
            UiKit.StyleDarkDropdown(mainGameDropdown, 18, replaceBackground: true);
            if (fullscreenToggle != null)
            {
                UiKit.StylePillButton(fullscreenToggle, UiKit.FieldBg, UiKit.FieldText);
                if (fullscreenToggle.targetGraphic != null)
                    UiKit.Outline(fullscreenToggle.targetGraphic, 1f, new Color(1f, 1f, 1f, 0.2f));
            }

            if (fullscreenToggle != null)
                fullscreenToggle.onClick.AddListener(() =>
                {
                    DisplaySettings.MainGameFullscreen = !DisplaySettings.MainGameFullscreen;
                    Refresh();
                });

            Refresh();
        }

        void OnEnable() { DisplaySettings.Changed += Refresh; Refresh(); }
        void OnDisable() => DisplaySettings.Changed -= Refresh;

        void Bind(Dropdown dd, DisplayTarget target)
        {
            if (dd == null) return;

            building = true;
            dd.ClearOptions();
            dd.AddOptions(Enumerable.Range(0, DisplaySettings.SizesFor(target).Length)
                                    .Select(i => DisplaySettings.Describe(target, i))
                                    .ToList());
            dd.value = DisplaySettings.IndexOf(target);
            dd.RefreshShownValue();
            building = false;

            dd.onValueChanged.AddListener(v =>
            {
                if (building) return;
                DisplaySettings.SetIndex(target, v);
                // 런처 해상도는 지금 보고 있는 화면이라 즉시 반영한다
                if (target == DisplayTarget.Launcher) DisplaySettings.ApplyFor(target);
            });
        }

        public void Refresh()
        {
            building = true;
            if (launcherDropdown != null)
            {
                launcherDropdown.value = DisplaySettings.IndexOf(DisplayTarget.Launcher);
                launcherDropdown.RefreshShownValue();
            }
            if (mainGameDropdown != null)
            {
                mainGameDropdown.value = DisplaySettings.IndexOf(DisplayTarget.MainGame);
                mainGameDropdown.RefreshShownValue();
                // 전체화면이면 해상도 선택은 의미가 없다
                mainGameDropdown.interactable = !DisplaySettings.MainGameFullscreen;
            }
            building = false;

            if (fullscreenToggleLabel != null)
                fullscreenToggleLabel.text = DisplaySettings.MainGameFullscreen ? "전체화면" : "창 모드";
        }
    }
}
