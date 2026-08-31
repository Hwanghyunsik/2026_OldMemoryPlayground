using System.Collections.Generic;
using UnityEngine;
using Shinmyeong.Flow.Screens;
using Shinmyeong.Save;

namespace Shinmyeong.Flow
{
    /// 화면 전환 관리자. 화면설계서 이동 맵(p2~3)이 이 클래스와 각 화면의 이동 호출로 표현된다.
    /// 세션 컨텍스트(모드·사용자)를 함께 들고 있어 -S 분기(스토리/개별 이동처 차이)와 기록 저장 여부를 결정한다.
    public class FlowManager : MonoBehaviour
    {
        public static FlowManager Instance { get; private set; }

        // ---- 세션 컨텍스트 ----
        public GameMode Mode = GameMode.Free;
        public string UserId = "";
        public string UserName = "";
        public bool IsGuest = true;

        /// 스토리 모드 게임별 성공 횟수 — SCR-022 잔치상 판정 입력(9-1). 비회원도 판정은 필요하다
        public readonly Dictionary<string, int> StorySuccess = new Dictionary<string, int>();

        /// 직전 이동이 GoBack이었는가 — SCR-022가 「기록 보기에서 복귀 시 타이머 미재개」(확정)를 구분하는 데 쓴다
        public bool LastMoveWasBack { get; private set; }

        readonly Dictionary<ScreenId, ScreenBase> _screens = new Dictionary<ScreenId, ScreenBase>();
        readonly Stack<ScreenId> _history = new Stack<ScreenId>();
        ScreenBase _current;

        public ScreenId CurrentId => _current != null ? _current.Id : ScreenId.SCR_001;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildScreens();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start() => Go(ScreenId.SCR_001);

        void BuildScreens()
        {
            Add<IdleScreen>(ScreenId.SCR_001);
            Add<CalibrationScreen>(ScreenId.SCR_002);
            Add<UserSelectScreen>(ScreenId.SCR_003);
            Add<ModeSelectScreen>(ScreenId.SCR_004);
            Add<LobbyScreen>(ScreenId.SCR_005);
            Add<VideoPlaceholderScreen>(ScreenId.SCR_006).Setup("오프닝 영상 (자리)\n대본 미확정 · B2", ScreenId.SCR_007);
            Add<GameTutorialScreen>(ScreenId.SCR_007).Setup("수확하기", new[]
            {
                "반짝이는 작물을 찾아요",
                "작물 위에 손을 올려요",
                "아래로 당겨 주세요",
            }, ScreenId.SCR_008);
            Add<HarvestPlayScreen>(ScreenId.SCR_008);
            Add<ResultScreen>(ScreenId.SCR_009);
            Add<VideoPlaceholderScreen>(ScreenId.SCR_011).Setup("연결 연출 1 (자리)\n수확 → 장보기", ScreenId.SCR_012);
            // 「양발 고정 · 체중 싣기」류 표현 금지 · 지나가는 자리는 괜찮다는 점 명시 (설계서)
            Add<GameTutorialScreen>(ScreenId.SCR_012).Setup("장보기", new[]
            {
                "빛나는 발판을 찾아요",
                "그쪽으로 걸어가요 (지나가는 자리는 괜찮아요)",
                "발판 위에서 잠깐 멈춰요",
                "가운데로 돌아와요",
            }, ScreenId.SCR_013);
            Add<ShoppingPlayScreen>(ScreenId.SCR_013);
            Add<ShoppingResultScreen>(ScreenId.SCR_014);
            Add<VideoPlaceholderScreen>(ScreenId.SCR_016).Setup("연결 연출 2 (자리)\n장보기 → 요리", ScreenId.SCR_017);
            // 「외우지 않아도 괜찮아요」를 1단계에 — 기억 부담 불안을 먼저 낮춘다 (설계서)
            Add<GameTutorialScreen>(ScreenId.SCR_017).Setup("요리하기", new[]
            {
                "음식과 재료를 봐요 (외우지 않아도 괜찮아요)",
                "「다 외웠어요」에 손을 올려요",
                "기억나는 재료에 손을 올려요",
                "차림표는 언제든 다시 볼 수 있어요",
            }, ScreenId.SCR_018);
            Add<CookingPlayScreen>(ScreenId.SCR_018);
            Add<CookingResultScreen>(ScreenId.SCR_019);
            Add<VideoPlaceholderScreen>(ScreenId.SCR_021).Setup("엔딩 영상 (자리)\n잔치 준비 끝", ScreenId.SCR_022);

            Add<HarvestStoryResultScreen>(ScreenId.SCR_010);
            Add<ShoppingStoryResultScreen>(ScreenId.SCR_015);
            Add<CookingStoryResultScreen>(ScreenId.SCR_020);
            Add<FeastScreen>(ScreenId.SCR_022);

            Add<RecordsScreen>(ScreenId.SCR_023);
        }

        T Add<T>(ScreenId id) where T : ScreenBase
        {
            var go = new GameObject(id.ToString());
            go.transform.SetParent(transform, false);
            UiKit.Stretch(go);
            var screen = go.AddComponent<T>();
            screen.Id = id;
            _screens[id] = screen;
            go.SetActive(false);
            return screen;
        }

        public void Go(ScreenId id)
        {
            if (!_screens.TryGetValue(id, out var next))
            {
                Debug.LogWarning($"[Flow] 미등록 화면: {id} — 이동 무시");
                return;
            }

            PausePopup.CloseIfOpen(); // 화면이 바뀌면 일시정지 상태를 남기지 않는다
            LastMoveWasBack = false;

            if (id == ScreenId.SCR_001)
                ResetSession();
            else if (_current != null)
                _history.Push(_current.Id);

            if (_current != null)
                _current.Hide();
            _current = next;
            _current.Show();
            Debug.Log($"[Flow] → {id}");
        }

        /// 호출 화면 복귀 (SCR-023 「이전으로」 등)
        public void GoBack()
        {
            if (_history.Count == 0)
            {
                Go(ScreenId.SCR_004);
                return;
            }
            LastMoveWasBack = true;
            var id = _history.Pop();
            if (_current != null)
                _current.Hide();
            _current = _screens[id];
            _current.Show();
            Debug.Log($"[Flow] ← {id}");
        }

        void ResetSession()
        {
            Mode = GameMode.Free;
            UserId = "";
            UserName = "";
            IsGuest = true;
            StorySuccess.Clear();
            _history.Clear();
        }

        /// 게임 10라운드 완주 시 참여 기록 저장 창구.
        /// 비회원은 저장하지 않는다(03 문서 6-1 확정) · 중도 종료는 게임이 커밋 자체를 하지 않는다(6-2)
        public void CommitPlay(PlayRecord record)
        {
            record.Mode = Mode.ToString();
            if (Mode == GameMode.Story)
                StorySuccess[record.Activity] = record.SuccessCount; // 잔치상 판정용 — 저장 여부와 무관
            if (IsGuest || string.IsNullOrEmpty(UserId))
            {
                Debug.Log("[Save] 비회원 — 참여 기록 저장 안 함 (확정)");
                return;
            }
            SaveStore.AppendRecord(UserId, record);
        }
    }
}
