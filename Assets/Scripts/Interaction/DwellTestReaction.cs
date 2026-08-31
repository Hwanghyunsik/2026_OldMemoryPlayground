using UnityEngine;
using UnityEngine.UI;

namespace Shinmyeong.Interaction
{
    /// dwell 판정 확인용 임시 컴포넌트 — 선택되면 색이 바뀌고 로그를 남긴다. 실제 화면 구현 시 제거.
    [RequireComponent(typeof(DwellTarget))]
    public class DwellTestReaction : MonoBehaviour
    {
        [SerializeField] Image _image;

        static readonly Color[] Palette =
        {
            new Color(0.35f, 0.55f, 0.9f),
            new Color(0.35f, 0.8f, 0.5f),
            new Color(0.9f, 0.55f, 0.35f),
            new Color(0.75f, 0.45f, 0.85f),
        };
        int _index;

        void Awake()
        {
            if (_image == null)
                _image = GetComponent<Image>();
            GetComponent<DwellTarget>().Selected += OnSelected;
        }

        void OnSelected(DwellTarget target)
        {
            _index = (_index + 1) % Palette.Length;
            if (_image != null)
                _image.color = Palette[_index];
            Debug.Log($"[DwellTest] 선택 완료: {name}");
        }
    }
}
