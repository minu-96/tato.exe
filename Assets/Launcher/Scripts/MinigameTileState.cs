using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 미니게임 탭 타일 — 설치 여부에 따라 버튼 글자와 타일 밝기를 바꾼다.
    /// 미설치면 실행 버튼을 잠그고 "상점에서 설치"로 안내한다 (§4).
    /// </summary>
    public class MinigameTileState : MonoBehaviour
    {
        public string gameId;
        public Button launchButton;
        public Text caption;
        public Image tileImage;

        void OnEnable() { StorageData.Changed += Refresh; Refresh(); }
        void OnDisable() => StorageData.Changed -= Refresh;

        public void Refresh()
        {
            bool installed = StorageData.IsInstalled(gameId);
            if (launchButton != null) launchButton.interactable = installed;
            if (caption != null) caption.text = installed ? "" : "상점에서 설치";
            if (tileImage != null)
                tileImage.color = installed ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f);
        }
    }
}
