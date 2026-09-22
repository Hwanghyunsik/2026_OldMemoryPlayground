using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Tracking;

namespace Shinmyeong.UI
{
    /// 캔버스 위에서 트래킹 손 위치를 따라다니는 커서 (한 손용 · 좌우 각 1개 배치).
    /// 모양은 시안 선택 표시와 같은 계열 — 흰 원 + 파란 테 + 옅은 그림자 (Awake에서 스프라이트 적용 · 씬 수정 불필요).
    public class HandCursorUI : MonoBehaviour
    {
        public HandSide Side;
        [SerializeField] Image _image;

        RectTransform _rect;
        GameObject _shadow;
        GameObject _outline;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (_image == null)
                _image = GetComponent<Image>();
            ApplySkin();
        }

        void ApplySkin()
        {
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
            bool show = svc != null && svc.GetHandValid(Side);
            if (_image != null && _image.enabled != show)
            {
                _image.enabled = show;
                if (_shadow != null) _shadow.SetActive(show);
                if (_outline != null) _outline.SetActive(show);
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
