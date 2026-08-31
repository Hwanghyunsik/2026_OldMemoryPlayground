using UnityEngine;

namespace Shinmyeong.Tracking
{
    /// One Euro Filter — 느린 움직임에선 강하게, 빠른 움직임에선 약하게 스무딩해
    /// 지연과 떨림을 동시에 억제한다. 커서 안정화 표준 기법.
    public class OneEuroFilter
    {
        readonly float _minCutoff;
        readonly float _beta;
        readonly float _dCutoff;

        bool _initialized;
        float _prev;
        float _prevDeriv;

        public OneEuroFilter(float minCutoff = 1.0f, float beta = 0.02f, float dCutoff = 1.0f)
        {
            _minCutoff = minCutoff;
            _beta = beta;
            _dCutoff = dCutoff;
        }

        static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / Mathf.Max(dt, 1e-5f));
        }

        public float Filter(float value, float dt)
        {
            if (!_initialized)
            {
                _initialized = true;
                _prev = value;
                _prevDeriv = 0f;
                return value;
            }

            float deriv = (value - _prev) / Mathf.Max(dt, 1e-5f);
            float aD = Alpha(_dCutoff, dt);
            _prevDeriv = Mathf.Lerp(_prevDeriv, deriv, aD);

            float cutoff = _minCutoff + _beta * Mathf.Abs(_prevDeriv);
            float a = Alpha(cutoff, dt);
            _prev = Mathf.Lerp(_prev, value, a);
            return _prev;
        }

        public void Reset() => _initialized = false;
    }

    public class OneEuroFilter2
    {
        readonly OneEuroFilter _x;
        readonly OneEuroFilter _y;

        public OneEuroFilter2(float minCutoff = 1.0f, float beta = 0.02f, float dCutoff = 1.0f)
        {
            _x = new OneEuroFilter(minCutoff, beta, dCutoff);
            _y = new OneEuroFilter(minCutoff, beta, dCutoff);
        }

        public Vector2 Filter(Vector2 value, float dt) =>
            new Vector2(_x.Filter(value.x, dt), _y.Filter(value.y, dt));

        public void Reset()
        {
            _x.Reset();
            _y.Reset();
        }
    }
}
