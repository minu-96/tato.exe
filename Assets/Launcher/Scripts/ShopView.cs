using System.Collections.Generic;
using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 상점 — 프로그램 구매(설치)와 삭제. 저장공간(§4)이 실제로 모자라므로
    /// 미니게임을 전부 깔 수는 없고, 무엇을 설치할지가 카드 축 선택이 된다.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [System.Serializable]
        public class Tile
        {
            public string gameId;
            public Button button;
            public Text caption;     // 버튼 글자 (구매 / 보유중 / 삭제 …)
            public Text info;        // 이름 · 가격 · 용량
        }

        public List<Tile> tiles = new();

        [Tooltip("상단 저장공간 표시")]
        public Text storageLabel;

        [Tooltip("안내·경고 한 줄")]
        public Text noticeLabel;

        /// <summary>삭제는 카드가 사라지므로 두 번 눌러야 실행된다.</summary>
        string pendingDelete;

        void Start()
        {
            foreach (var t in tiles)
            {
                if (t?.button == null) continue;
                var local = t;
                t.button.onClick.AddListener(() => OnClick(local));
            }
            Refresh();
        }

        void OnEnable()
        {
            StorageData.Changed += Refresh;
            PlayerData.Changed += Refresh;
            pendingDelete = null;
            Refresh();
        }

        void OnDisable()
        {
            StorageData.Changed -= Refresh;
            PlayerData.Changed -= Refresh;
        }

        void OnClick(Tile t)
        {
            var g = StorageData.Find(t.gameId);
            if (g == null || g.core) return;   // 본편은 건드릴 수 없다

            if (StorageData.IsInstalled(t.gameId))
            {
                // 삭제 — 카드가 사라지므로 한 번 더 확인
                if (pendingDelete != t.gameId)
                {
                    pendingDelete = t.gameId;
                    int risk = StorageData.CardsAtRisk(t.gameId);
                    Notice(risk > 0
                        ? $"정말 삭제할까요? 이 게임에서 얻은 카드 {risk}장이 사라집니다 — 한 번 더 누르세요"
                        : "정말 삭제할까요? 한 번 더 누르세요", warn: true);
                    Refresh();
                    return;
                }
                int lost = StorageData.Uninstall(t.gameId);
                pendingDelete = null;
                Notice($"{g.displayName} 삭제 — 저장공간 {g.sizeMb}MB 확보" +
                       (lost > 0 ? $", 카드 {lost}장 소멸" : ""), warn: true);
            }
            else
            {
                pendingDelete = null;
                if (StorageData.TryInstall(t.gameId, out string reason))
                    Notice($"{g.displayName} 설치 완료" + (g.price > 0 ? $" (−{g.price} 토인)" : ""));
                else
                    Notice(reason, warn: true);
            }
            Refresh();
        }

        void Notice(string msg, bool warn = false)
        {
            if (noticeLabel == null) return;
            noticeLabel.text = msg;
            noticeLabel.color = warn ? new Color(1f, 0.55f, 0.45f) : new Color(0.7f, 1f, 0.75f);
        }

        public void Refresh()
        {
            if (storageLabel != null)
                storageLabel.text = $"저장공간  {StorageData.UsedMb} / {StorageData.TotalMb} MB" +
                                    $"   (남은 공간 {StorageData.FreeMb}MB)";

            foreach (var t in tiles)
            {
                var g = StorageData.Find(t?.gameId);
                if (g == null) continue;

                if (t.info != null)
                    t.info.text = g.core
                        ? $"{g.displayName}\n{g.sizeMb}MB · 본편"
                        : $"{g.displayName}\n{g.sizeMb}MB · {(g.price > 0 ? g.price + " 토인" : "무료")}";

                bool installed = StorageData.IsInstalled(g.id);
                if (t.caption != null)
                    t.caption.text = g.core ? "보유중"
                        : pendingDelete == g.id ? "정말 삭제?"
                        : installed ? "삭제"
                        : g.sizeMb > StorageData.FreeMb ? "공간 부족"
                        : PlayerData.Toin < g.price ? "토인 부족"
                        : "구매";

                if (t.button != null)
                    t.button.interactable = !g.core &&
                        (installed || (g.sizeMb <= StorageData.FreeMb && PlayerData.Toin >= g.price));
            }
        }
    }
}
