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

        /// 개발용: 실행 인자 `-screen SCR_006`으로 시작 화면을 바로 지정한다(빌드에서 영상·화면 단독 확인용).
        /// 인자가 없으면 정상 흐름으로 시작한다 — 기기가 연결되지 않은 PC는 운영자 로그인(LOGIN) → 기기 선택,
        /// 연결된 PC는 SCR-001. `-relogin`은 기기 연결을 지우고 로그인 화면부터 시작한다.
        void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-relogin") >= 0)
                DeviceAuth.Clear();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-screen" && System.Enum.TryParse(args[i + 1], out ScreenId start))
                {
                    Go(start);
                    return;
                }
            Go(DeviceAuth.IsRegistered ? ScreenId.SCR_001 : ScreenId.LOGIN);
        }

        void BuildScreens()
        {
            Add<LoginScreen>(ScreenId.LOGIN);
            Add<DeviceSelectScreen>(ScreenId.DEVICE_SELECT);
            Add<IdleScreen>(ScreenId.SCR_001);
            Add<CalibrationScreen>(ScreenId.SCR_002);
            Add<UserSelectScreen>(ScreenId.SCR_003);
            Add<ModeSelectScreen>(ScreenId.SCR_004);
            Add<LobbyScreen>(ScreenId.SCR_005);
            Add<StoryVideoScreen>(ScreenId.SCR_006).Setup("SCR-006-Title", new Rect(805, 88, 310, 65), "1.오프닝", ScreenId.SCR_007);
            // 튜토리얼 단계 문구·삽화는 시안(SCR-007/012/017) 기준 — 「양발 고정 · 체중 싣기」류 표현 금지(설계서)
            Add<GameTutorialScreen>(ScreenId.SCR_007).Setup("SCR-007-Title", 192, "SCR-007-Bg", new[]
            {
                new TutorialStep("손을 올립니다", "거두고 싶은 것 위에", "SCR-007-Sequence-01"),
                new TutorialStep("아래로 당깁니다", "천천히 내리면 됩니다", "SCR-007-Sequence-02"),
                new TutorialStep("바구니에 담깁니다", "소리로 알려 드립니다", "SCR-007-Sequence-03"),
            }, "튜토리얼게임1", ScreenId.SCR_008);
            Add<HarvestPlayScreen>(ScreenId.SCR_008);
            Add<ResultScreen>(ScreenId.SCR_009);
            Add<StoryVideoScreen>(ScreenId.SCR_011).Setup("SCR-011_Title", new Rect(828, 88, 265, 65), "2.연결연출1", ScreenId.SCR_012);
            Add<GameTutorialScreen>(ScreenId.SCR_012).Setup("SCR-012-Title", 143, "SCR-013-Bg", new[]
            {
                new TutorialStep("빛나는 발판을 봅니다", "위쪽 재료와 같은 자리입니다", "Icon-Step"),
                new TutorialStep("그쪽으로 옮겨 갑니다", "지나가는 자리는 괜찮습니다", "Icon-GranMa"),
                new TutorialStep("그 자리에서 잠시 멈춥니다", "재료를 담아 드립니다", "Icon-GranMa-02"),
                new TutorialStep("가운데로 돌아옵니다", "장바구니에 담깁니다", "Icon-GranMa"),
            }, "튜토리얼게임2", ScreenId.SCR_013);
            Add<ShoppingPlayScreen>(ScreenId.SCR_013);
            Add<ShoppingResultScreen>(ScreenId.SCR_014);
            Add<StoryVideoScreen>(ScreenId.SCR_016).Setup("SCR-016-Title", new Rect(740, 90, 441, 85), "3.연결연출2", ScreenId.SCR_017);
            // 「외우지 않아도 괜찮아요」를 1단계에 — 기억 부담 불안을 먼저 낮춘다 (설계서)
            Add<GameTutorialScreen>(ScreenId.SCR_017).Setup("SCR-017-title", 192, "SCR-017-Bg", new[]
            {
                new TutorialStep("레시피를 잠깐 봅니다", "외우지 않아도 괜찮아요"),
                new TutorialStep("재료 위에 손을 올립니다", "여섯 가지 중에서 고릅니다"),
                new TutorialStep("동그라미가 다 차면 담깁니다", "소리로 알려 드립니다"),
                new TutorialStep("다시 보기를 쓸 수 있습니다", "차림표를 다시 보여 드립니다"),
            }, "튜토리얼게임3", ScreenId.SCR_018);
            Add<CookingPlayScreen>(ScreenId.SCR_018);
            Add<CookingResultScreen>(ScreenId.SCR_019);
            Add<StoryVideoScreen>(ScreenId.SCR_021).Setup("SCR-021-Title", new Rect(780, 88, 361, 66), "4.엔딩", ScreenId.SCR_022);

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
            // 마우스는 관리 화면(로그인·기기 선택 — 키보드·마우스 입력)에서만 보인다 — 이후 게임은 손 커서만 쓴다
            Cursor.visible = id == ScreenId.LOGIN || id == ScreenId.DEVICE_SELECT;
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
