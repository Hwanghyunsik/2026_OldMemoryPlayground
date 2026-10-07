using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Shinmyeong.UI
{
    /// 화면 안 영상 창 — VideoPlayer → RenderTexture → RawImage.
    /// 부모(둥근 모양 Image)를 Mask로 삼아 창을 꽉 채우고(16:9 비율 유지 · 넘치는 가장자리는 잘림) 소리는 영상 트랙 그대로 낸다.
    /// 영상은 `UiCatalog.FindVideo(파일명)`로 찾는다 — 없으면 Play가 false를 돌려 호출 쪽이 자리 표시로 대체한다.
    public class VideoView : MonoBehaviour
    {
        VideoPlayer _player;
        RawImage _image;
        RenderTexture _texture;

        /// 반복 재생이 아닐 때 끝까지 재생되면 한 번 호출
        public event Action Finished;

        /// 재생 진행 비율 0~1 (준비 전·영상 없음 = 0)
        public float Progress
        {
            get
            {
                if (_player == null || _player.clip == null || _player.length <= 0) return 0f;
                return Mathf.Clamp01((float)(_player.time / _player.length));
            }
        }

        public bool HasClip => _player != null && _player.clip != null;

        /// 개발용: 실행 인자 `-videolog`면 재생 진행(프레임·재생 위치·시계)을 0.5초마다 로그로 남긴다 — 밀림·멈춤 진단
        static readonly bool LogProgress = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-videolog") >= 0;
        float _nextLogAt;

        /// mask = 영상이 보일 둥근 모양 Image (Mask를 붙인다) · 그 크기에 맞춰 영상을 꽉 채운다
        public static VideoView Create(Image mask, float aspect = 16f / 9f)
        {
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var go = new GameObject("Video");
            go.transform.SetParent(mask.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var size = mask.rectTransform.sizeDelta;
            rect.sizeDelta = size.x / size.y > aspect
                ? new Vector2(size.x, size.x / aspect)
                : new Vector2(size.y * aspect, size.y);
            var view = go.AddComponent<VideoView>();
            view._image = go.AddComponent<RawImage>();
            view._image.raycastTarget = false;
            view._image.enabled = false;
            return view;
        }

        /// 영상 재생 시작 — 처음부터. 영상이 카탈로그에 없으면 false
        public bool Play(string clipName, bool loop)
        {
            var clip = UiCatalog.Instance != null ? UiCatalog.Instance.FindVideo(clipName) : null;
            if (clip == null)
            {
                Debug.LogWarning($"[VideoView] 영상 없음: {clipName} — 메뉴 「신명/UI 카탈로그 재생성」 확인");
                Stop();
                return false;
            }
            EnsurePlayer();
            // 처음부터 재생 — Stop이 위치를 0으로 되돌린다
            _player.Stop();
            _player.clip = clip;
            _player.isLooping = loop;
            ClearTexture(); // 이전 영상의 마지막 장면이 비치지 않게 창 색으로 지운다
            _image.enabled = true;
            _player.Play(); // 준비가 안 됐으면 준비 후 첫 프레임이 나오면 시작 (waitForFirstFrame)
            return true;
        }

        public void Stop()
        {
            if (_player != null)
                _player.Stop();
            if (_image != null)
                _image.enabled = false;
        }

        void EnsurePlayer()
        {
            if (_player != null) return;
            _texture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) { name = "VideoView" };
            _texture.Create();
            _image.texture = _texture;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.waitForFirstFrame = true;
            // 프레임 건너뛰기는 끈다 — waitForFirstFrame과 함께 켜면 첫 재생이 0프레임에 멈추고 시계만 흐른다
            // (빌드 실측 · 반복 2회차부터는 정상이라 「영상이 밀린다」로 보였음). 끄면 화면과 소리가 정확히 맞는다
            _player.skipOnDrop = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _texture;
            _player.aspectRatio = VideoAspectRatio.FitOutside;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            // 화면을 소리(오디오 DSP 시계)에 맞춘다 — 게임 시간 기준이면 프레임이 끊길 때 화면만 늦어져 소리와 밀린다
            _player.timeUpdateMode = VideoTimeUpdateMode.DSPTime;
            _player.loopPointReached += _ =>
            {
                if (!_player.isLooping)
                    Finished?.Invoke();
            };
            _player.errorReceived += (_, message) => Debug.LogWarning($"[VideoView] 재생 오류: {message}");
        }

        void Update()
        {
            if (!LogProgress || _player == null || _player.clip == null || Time.unscaledTime < _nextLogAt)
                return;
            _nextLogAt = Time.unscaledTime + 0.5f;
            Debug.Log($"[VideoLog] {_player.clip.name} frame={_player.frame}/{_player.frameCount} time={_player.time:F2} clock={_player.clockTime:F2} playing={_player.isPlaying} fps={1f / Time.unscaledDeltaTime:F0}");
        }

        void ClearTexture()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(false, true, Skin.Hex("2d2926"));
            RenderTexture.active = prev;
        }

        void OnDisable() => Stop();

        void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }
    }
}
