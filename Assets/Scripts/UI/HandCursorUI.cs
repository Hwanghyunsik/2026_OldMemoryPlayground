using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Tracking;

namespace Shinmyeong.UI
{
    /// 캔버스 위에서 트래킹 손 위치를 따라다니는 커서 (한 손용 · 좌우 각 1개 배치).
    /// 모양은 디자이너 손 그림(`Prefabs/Hand.prefab` · 튜토리얼 영상과 같은 커서) — 기준점(트래킹 위치)은 손바닥이고
    /// 게이지 링도 손바닥 위에 그린다. 오른손은 프리팹 방향 그대로, 왼손은 좌우 반전.
    /// 프리팹이 없으면 예전 모양(흰 원 + 파란 테 + 옅은 그림자)으로 그린다.
    public class HandCursorUI : MonoBehaviour
    {
        public HandSide Side;

        /// 손 조작이 없는 화면(SCR-002 사용자 감지·보정)에서 켠다 — 커서를 숨긴다
        public static bool Hidden;
        [SerializeField] Image _image;
        [SerializeField] GameObject _handPrefab;
        [Tooltip("손 그림 중심을 기준점(손바닥)에서 위로 올리는 거리 px")]
        [SerializeField] float _handLift = 35f;

        RectTransform _rect;
        GameObject _shadow;
        GameObject _outline;
        GameObject _hand;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (_image == null)
                _image = GetComponent<Image>();
            ApplySkin();
        }

        void ApplySkin()
        {
            if (_handPrefab != null && _image != null)
            {
                _hand = Instantiate(_handPrefab, transform, false);
                _hand.name = "Hand";
                _hand.transform.SetAsFirstSibling(); // 게이지 링이 손 그림 위에 그려지도록
                var r = (RectTransform)_hand.transform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(0f, _handLift);
                var s = r.localScale;
                float sx = Mathf.Abs(s.x) * (Side == HandSide.Right ? Mathf.Sign(s.x) : -Mathf.Sign(s.x));
                r.localScale = new Vector3(sx, s.y, s.z);
                // 프리팹의 정렬 캔버스(order 3)가 커서 캔버스 순서를 벗어나 팝업 아래로 깔리지 않게 부모 순서를 따른다
                var canvas = _hand.GetComponent<Canvas>();
                if (canvas != null)
                    canvas.overrideSorting = false;
                foreach (var g in _hand.GetComponentsInChildren<Graphic>(true))
                    g.raycastTarget = false;
                _image.color = Color.clear; // 기준점 원은 숨기고 표시/숨김 판단에만 쓴다
                return;
            }

            var circle = Skin.Sprite("Circle-125");
            var ring = Skin.Sprite("Circle-outline-15");
            if (circle == null || ring == null || _image == null)
                return; // 카탈로그 미생성 시 기존 모양 유지

            _image.sprite = circle;
            _image.color = Color.white;
            _image.type = Image.Type.Simple;

            _shadow = Child("Shadow", circle, Skin.Hex("1d1513", 0.28f), 1.12f, new Vector2(3f, -5f));
            _shadow.transform.SetAsFirstSibling();
            _outline = Child("Outline", ring, Skin.Blue, 1.0f, Vector2.zero);
            // 게이지 링(자식)이 테 위에 그려지도록 테는 게이지 앞에 둔다
            _outline.transform.SetSiblingIndex(1);
        }

        GameObject Child(string name, Sprite sprite, Color color, float scale, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            r.anchoredPosition = offset;
            r.localScale = Vector3.one * scale;
            return go;
        }

        void LateUpdate()
        {
            var svc = BodyTrackingService.Instance;
            bool show = !Hidden && svc != null && svc.GetHandValid(Side);
            if (_image != null && _image.enabled != show)
            {
                _image.enabled = show;
                if (_shadow != null) _shadow.SetActive(show);
                if (_outline != null) _outline.SetActive(show);
                if (_hand != null) _hand.SetActive(show);
                // 게이지 트랙(형제 오브젝트)은 게이지가 꺼져 있으면 함께 숨긴다 — 손이 없을 때 링만 남지 않게
                var track = transform.Find("DwellGaugeTrack");
                if (track != null && !show)
                    track.GetComponent<Image>().enabled = false;
            }
            if (!show)
                return;

            // 앵커를 뷰포트 좌표에 직접 두면 해상도와 무관하게 같은 지점을 가리킨다
            _rect.anchorMin = _rect.anchorMax = svc.GetHandPos(Side);
            _rect.anchoredPosition = Vector2.zero;
        }
    }
}
