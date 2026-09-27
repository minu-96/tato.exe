using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 상단 HUD 칩에 수치를 표시한다(§3). 게임에서 번 토인·모은 카드가 그대로 이어진다.
    /// 칩 종류만 바꿔 같은 컴포넌트를 재사용.
    ///
    /// 칩은 아이콘뿐이라 처음 보면 무엇의 숫자인지 모른다 — 마우스를 올리면 설명이 뜬다.
    /// 글자가 칸보다 길면(닉네임 등) 줄여서 칸 안에 맞춘다(예전엔 아이콘 위로 넘쳤다).
    /// </summary>
    public class HudStat : MonoBehaviour
    {
        public enum Stat
        {
            Toin,             // 토인 보유량
            DeckCount,        // 런 덱 장수
            CollectionCount,  // 감자창고 보유 장수
            BestStage,        // 최고 도달 스테이지
            Nickname,         // 플레이어 이름
        }

        /// <summary>닉네임 — 아직 입력 시스템이 없어 고정값. 나중에 설정에서 바꾸게 한다.</summary>
        public const string DefaultNickname = "멀쩡한 감자농부";

        public Stat stat = Stat.Toin;
        public Text label;

        bool prepared;

        void OnEnable()
        {
            Prepare();
            PlayerData.Changed += Refresh;   // 덱 토글·보상 등으로 값이 바뀌면 즉시 갱신
            Refresh();
        }

        void OnDisable() => PlayerData.Changed -= Refresh;

        void Prepare()
        {
            if (prepared) return;
            prepared = true;

            if (label != null)
            {
                // 아이콘 오른쪽부터만 쓴다. 칸보다 길면 한 줄을 유지한 채 글자 크기를 줄인다(FitWidth)
                // — 자동 맞춤(best fit)을 쓰면 두 줄로 접혀 버렸다
                var rt = label.rectTransform;
                rt.offsetMin = new Vector2(Mathf.Max(58f, rt.offsetMin.x), rt.offsetMin.y);
                baseSize = label.fontSize;
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            var (title, body) = Tip(stat);
            if (TryGetComponent(out Image chip)) TooltipTrigger.On(chip, title, body);
        }

        static (string, string) Tip(Stat s) => s switch
        {
            Stat.DeckCount => ("런 덱", "전투에 가져가는 카드 수예요 (최소 8 · 최대 30).\n감자창고에서 편성해요. 전투에서 지면 덱에 든 카드가 사라져요."),
            Stat.CollectionCount => ("감자창고", "가지고 있는 카드 전체 수예요 (덱에 넣은 카드 포함)."),
            Stat.BestStage => ("최고 도달 스테이지", $"지금까지 가장 멀리 간 스테이지예요 (전체 {RunState.StageCount}단계)."),
            Stat.Nickname => ("프로필", "닉네임 바꾸기는 준비 중이에요."),
            _ => ("토인", "게임의 돈이에요. 전투에서 벌고,\n미니게임 구입과 대장간 강화·제거에 써요."),
        };

        public void Refresh()
        {
            if (label == null) return;
            label.text = stat switch
            {
                Stat.DeckCount => PlayerData.DeckSize().ToString(),
                Stat.CollectionCount => PlayerData.Instances().Count.ToString(),
                Stat.BestStage => $"{RunState.BestStageReached} / {RunState.StageCount}",
                Stat.Nickname => DefaultNickname,
                _ => PlayerData.Toin.ToString(),
            };
            FitWidth();
        }

        int baseSize;

        /// <summary>한 줄이 칸보다 길면 칸에 들어가는 크기까지 줄인다.</summary>
        void FitWidth()
        {
            if (label == null || baseSize <= 0) return;
            label.fontSize = baseSize;
            float w = label.rectTransform.rect.width;
            float need = label.preferredWidth;
            if (w > 0f && need > w)
                label.fontSize = Mathf.Max(12, Mathf.FloorToInt(baseSize * w / need));
        }
    }
}
