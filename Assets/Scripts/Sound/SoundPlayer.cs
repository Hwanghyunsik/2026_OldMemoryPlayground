using System.Collections.Generic;
using UnityEngine;
using Shinmyeong.Interaction;
using Shinmyeong.Net;
using Shinmyeong.UI;

namespace Shinmyeong.Sound
{
    /// 소리 코드 — `Docs/08_사운드시나리오` 01 파일목록의 코드 그대로 (파일은 UiCatalog 가 `Assets/Audio`에서 모은다)
    public static class Snd
    {
        public const string BgmMain = "01_BGM";
        public const string BgmHarvest = "02_BGM";
        public const string BgmMarket = "03_BGM";
        public const string BgmCook = "04_BGM";
        public const string BgmTutorial = "05_BGM";

        public const string Select = "01_SE";     // 선택 완료 (모든 버튼)
        public const string Detect = "02_SE";     // 감지(보정) 완료
        public const string Count = "03_SE";      // 카운트 3-2-1
        public const string Start = "04_SE";      // 시작 (카운트 0)
        public const string Hint = "05_SE";       // 힌트
        public const string Result = "06_SE";     // 결과 등장
        public const string Target = "07_SE";     // 목표 강조
        public const string Harvest = "08_SE";    // 수확
        public const string Whoosh = "09_SE";     // 휙 날아감
        public const string PadGlow = "10_SE";    // 발판 빛남
        public const string Buy = "11_SE";        // 구매 · 목표 점포
        public const string BuyOther = "12_SE";   // 구매 · 다른 점포
        public const string Basket = "13_SE";     // 장바구니 복귀
        public const string Recipe = "14_SE";     // 차림표
        public const string Tok = "15_SE";        // 톡 (재료 담김)
        public const string CookDone = "16_SE";   // 요리 완성
        public const string CookOther = "17_SE";  // 다른 재료

        /// 음성 1~14 → `01_VO`~`14_VO`
        public static string Vo(int number) => $"{number:00}_VO";
    }

    /// 소리 재생 창구 (08 사운드 시나리오 03 공통규칙).
    ///   BGM: 한 화면 묶음에 1곡 · 첫 시작 1초 페이드 인 · 곡 전환 1.5초 교차 · 정지 1.5초 페이드 아웃 · 같은 곡이면 끊지 않는다
    ///   효과음·음성: 한 번에 하나 — 새 소리가 오면 앞 소리를 끊는다. 순서가 정해진 소리만 앞 소리가 끝난 뒤(PlayAfter)
    ///   화면이 바뀌면 그 화면의 효과음·음성은 멈춘다 (전환을 일으킨 그 순간의 소리는 남긴다 — 버튼 완료음 · 감지 완료음)
    ///   일시정지 중에는 BGM은 그대로, 게임 효과음은 내지 않는다 (팝업 버튼 완료음만)
    ///   볼륨은 전부 100 — 종류별 크기는 파일에 들어 있고, 전체 크기는 DeviceLink 가 AudioListener 로 조절한다(B2 3단계). 덕킹 없음
    /// 시간은 전부 unscaled — timeScale=0(일시정지)에도 페이드·이어 재생이 멈추지 않는다.
    public class SoundPlayer : MonoBehaviour
    {
        const float FadeInSeconds = 1f;
        const float CrossSeconds = 1.5f;
        const float FadeOutSeconds = 1.5f;

        static SoundPlayer _instance;
        static bool _quitting;

        readonly AudioSource[] _bgm = new AudioSource[2];
        readonly float[] _bgmTarget = new float[2];
        readonly float[] _bgmRate = new float[2];
        int _bgmActive;
        string _bgmCode;

        AudioSource _one;       // 효과음·음성 공용 — 한 번에 하나
        int _oneStartFrame = -1;
        readonly Queue<(string code, float gap)> _queue = new Queue<(string, float)>();
        float _gapTimer;

        static SoundPlayer I
        {
            get
            {
                if (_instance == null && !_quitting && Application.isPlaying)
                {
                    var go = new GameObject("SoundPlayer");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SoundPlayer>();
                }
                return _instance != null ? _instance : null; // 파괴된 객체는 진짜 null 로 (?. 가 걸러내게)
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _quitting = false;
        }

        void Awake()
        {
            Application.quitting += () => _quitting = true;
            for (int i = 0; i < 2; i++)
            {
                _bgm[i] = NewSource();
                _bgm[i].loop = true; // BGM 파일은 끝과 처음이 이어지게 만들어져 있다
                _bgm[i].volume = 0f;
            }
            _one = NewSource();

            // 임포트 설정이 「미리 읽기 끔」이어도 첫 재생이 늦지 않게 시작할 때 한 번에 읽어 둔다
            var cat = UiCatalog.Instance;
            if (cat != null)
                foreach (var e in cat.Sounds)
                    if (e != null && e.Clip != null && e.Clip.loadState == AudioDataLoadState.Unloaded)
                        e.Clip.LoadAudioData();
        }

        AudioSource NewSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        static AudioClip Find(string code)
        {
            var cat = UiCatalog.Instance;
            var clip = cat != null ? cat.FindSound(code) : null;
            if (clip == null)
                Debug.LogWarning($"[Sound] 소리 없음: {code} — 메뉴 「신명/UI 카탈로그 재생성」 확인");
            return clip;
        }

        // ---------------------------------------------------------------- BGM

        /// 이 곡을 튼다. 이미 같은 곡이면 그대로 이어 간다 · 아무 곡도 없으면 1초 페이드 인 · 다른 곡이면 1.5초 교차
        public static void Bgm(string code)
        {
            var p = I;
            if (p == null || p._bgmCode == code)
                return;
            var clip = Find(code);
            if (clip == null)
                return;
            bool wasAudible = BgmAudible;
            p.FadeTo(p._bgmActive, 0f, CrossSeconds);
            p._bgmActive = 1 - p._bgmActive;
            var src = p._bgm[p._bgmActive];
            src.Stop();
            src.clip = clip;
            src.volume = 0f;
            src.Play();
            p.FadeTo(p._bgmActive, 1f, wasAudible ? CrossSeconds : FadeInSeconds);
            p._bgmCode = code;
        }

        /// 1.5초 페이드 아웃 (영상 구간 직전 등)
        public static void StopBgm()
        {
            var p = I;
            if (p == null)
                return;
            p.FadeTo(0, 0f, FadeOutSeconds);
            p.FadeTo(1, 0f, FadeOutSeconds);
            p._bgmCode = null;
        }

        /// BGM이 아직 들리는가 (페이드 아웃이 끝나기 전 포함) — 영상 화면이 「다 꺼진 뒤 시작」을 맞추는 데 쓴다
        public static bool BgmAudible
        {
            get
            {
                var p = _instance;
                return p != null && (p._bgm[0].isPlaying || p._bgm[1].isPlaying);
            }
        }

        void FadeTo(int index, float target, float seconds)
        {
            _bgmTarget[index] = target;
            _bgmRate[index] = 1f / Mathf.Max(0.01f, seconds);
        }

        // ---------------------------------------------------------------- 효과음 · 음성

        /// 효과음·음성 1회 — 앞 소리를 끊고 바로 낸다 (소리가 밀려 늦게 나지 않게). 일시정지 중에는 내지 않는다
        public static void Play(string code)
        {
            if (GamePause.IsPaused)
                return;
            I?.PlayNow(code);
        }

        /// 버튼 머무르기 완료음(01_SE) — 일시정지 팝업 버튼에서도 난다
        public static void Select() => I?.PlayNow(Snd.Select);

        /// 순서가 정해진 소리 — 앞 소리(와 먼저 줄 선 소리)가 끝난 뒤 gap 초 쉬고 낸다. 나는 소리가 없으면 바로
        public static void PlayAfter(string code, float gap = 0f)
        {
            var p = I;
            if (p == null || GamePause.IsPaused)
                return;
            if (!p._one.isPlaying && p._queue.Count == 0)
            {
                p.PlayNow(code);
                return;
            }
            p._queue.Enqueue((code, gap));
        }

        /// 화면이 바뀔 때 — 앞 화면의 효과음·음성을 멈춘다. 이 프레임에 시작된 소리(전환을 일으킨 버튼 완료음 등)는 남긴다
        public static void StopScreenSounds()
        {
            var p = _instance;
            if (p == null)
                return;
            p._queue.Clear();
            p._gapTimer = 0f;
            if (p._one.isPlaying && p._oneStartFrame != Time.frameCount)
                p._one.Stop();
        }

        void PlayNow(string code)
        {
            _queue.Clear();
            _gapTimer = 0f;
            StartClip(code);
        }

        void StartClip(string code)
        {
            // 음성 안내 끔(관리자 설정 B2) — 음성만 내지 않는다
            if (code.EndsWith("_VO") && !DeviceLink.VoiceEnabled)
                return;
            var clip = Find(code);
            if (clip == null)
                return;
            _one.Stop();
            _one.clip = clip;
            _one.Play();
            _oneStartFrame = Time.frameCount;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < 2; i++)
            {
                var src = _bgm[i];
                if (!src.isPlaying)
                    continue;
                src.volume = Mathf.MoveTowards(src.volume, _bgmTarget[i], _bgmRate[i] * dt);
                if (_bgmTarget[i] <= 0f && src.volume <= 0f)
                    src.Stop();
            }

            if (_queue.Count == 0 || _one.isPlaying)
                return;
            _gapTimer += dt;
            if (_gapTimer < _queue.Peek().gap)
                return;
            _gapTimer = 0f;
            StartClip(_queue.Dequeue().code);
        }
    }
}
