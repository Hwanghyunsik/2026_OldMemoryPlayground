using UnityEngine;

namespace Shinmyeong.Flow
{
    /// 모든 화면의 기반. FlowManager가 생성·등록·전환한다.
    /// UI는 첫 표시 때 한 번만 조립(BuildUi)하고 이후에는 활성/비활성만 바뀐다.
    public abstract class ScreenBase : MonoBehaviour
    {
        public ScreenId Id { get; set; }

        bool _built;

        protected FlowManager Flow => FlowManager.Instance;
        protected RectTransform Rect => (RectTransform)transform;

        public void Show()
        {
            if (!_built)
            {
                BuildUi();
                _built = true;
            }
            gameObject.SetActive(true);
            OnEnter();
        }

        public void Hide()
        {
            OnExit();
            gameObject.SetActive(false);
        }

        protected abstract void BuildUi();

        protected virtual void OnEnter() { }

        protected virtual void OnExit() { }
    }
}
