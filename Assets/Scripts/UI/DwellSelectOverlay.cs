using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;

namespace Shinmyeong.UI
{
    /// 선택 대상 위 dwell 진행 표시 (시안 Select-Card): 어둡게 덮는 면 + 파란 테두리 + 중앙 진행 링 + 「선택하는 중…」.
    /// 손 커서의 게이지 링(DwellGaugeUI)과 별개로, 어느 요소가 선택되는 중인지 대상 자체에 보여 준다.
    /// UI 버튼·카드(dwell 3초)와 요리 재료(1~2초)에 붙는다 — 수확·장보기 판정 대상에는 쓰지 않는다(2-1).
    public class DwellSelectOverlay : MonoBehaviour
    {
        DwellTarget _target;
        GameObject _root;
        Image _ring;

        public static DwellSelectOverlay Attach(DwellTarget target, float width, float height, bool withText = true)
        {
            var rect = (RectTransform)target.transform;
            var rootGo = new GameObject("SelectOverlay");
            rootGo.transform.SetParent(rect, false);
            var rootRect = rootGo.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            // 덮는 면 + 파란 테두리 (Box-Round-Select · 9분할)
            var shade = new GameObject("Shade").AddComponent<Image>();
            shade.transform.SetParent(rootRect, false);
            shade.sprite = Skin.Sprite("Box-Round-Select");
            shade.type = Image.Type.Sliced;
            shade.raycastTarget = false;
            var shadeRect = shade.rectTransform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.sizeDelta = Vector2.zero;

            float ring = Mathf.Clamp(Mathf.Min(width, height) * 0.46f, 60f, 108f);
            float ringY = withText ? height * 0.06f : 0f;
            Circle(rootRect, "RingTrack", ring, ringY, Skin.Hex("cccccc"));
            var fill = Circle(rootRect, "RingFill", ring, ringY, Skin.Blue);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;

            if (withText)
            {
                var text = new GameObject("SelectingText").AddComponent<Image>();
                text.transform.SetParent(rootRect, false);
                text.sprite = Skin.Sprite("Text-Select");
                text.preserveAspect = true;
                text.raycastTarget = false;
                var tr = text.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
                float tw = Mathf.Min(165f, width * 0.85f);
                tr.sizeDelta = new Vector2(tw, tw * 52f / 165f);
                tr.anchoredPosition = new Vector2(0f, height * 0.2f);
            }

            var overlay = rootGo.AddComponent<DwellSelectOverlay>();
            overlay._target = target;
            overlay._root = rootGo;
            overlay._ring = fill;
            rootGo.transform.SetAsLastSibling();
            overlay.SetVisible(false);
            return overlay;
        }

        static Image Circle(Transform parent, string name, float size, float y, Color color)
        {
            var img = new GameObject(name).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = Skin.Sprite("Circle-outline-15");
            img.color = color;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = new Vector2(0f, y);
            return img;
        }

        void OnEnable()
        {
            if (_root != null)
                SetVisible(false);
        }

        void LateUpdate()
        {
            var judge = DwellJudge.Instance;
            bool show = judge != null && _target != null && judge.CurrentTarget == _target && _target.IsArmed;
            SetVisible(show);
            if (show)
                _ring.fillAmount = judge.Progress01;
        }

        void SetVisible(bool on)
        {
            // 루트 자체는 살아 있어야 LateUpdate가 돈다 — 자식만 켜고 끈다
            for (int i = 0; i < _root.transform.childCount; i++)
            {
                var child = _root.transform.GetChild(i).gameObject;
                if (child.activeSelf != on)
                    child.SetActive(on);
            }
        }
    }
}
