using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shinmyeong.Interaction
{
    [Serializable]
    public class ArrivalZone
    {
        public int Id;
        [Tooltip("몸 중심 x 정규화 좌표 (0~1)")]
        public float Center;
        [Tooltip("진입 판정 반폭 — 이탈 반폭보다 좁게 (히스테리시스)")]
        public float EnterHalfWidth = 0.06f;
        public float ExitHalfWidth = 0.09f;
    }

    /// 장보기 도착 판정 (07 문서 5-8 · 개발 판단 C1).
    /// 순간 속도 대신 「시간 창 내 변위 폭」으로 정지를 판정한다 —
    /// 고령 사용자의 느린 이동이 정지로 오판정되는 것을 막기 위함.
    ///
    ///   통과: 영역 안에 있어도 창 내 변위가 크다 → 판정 없음
    ///   도착: 창(기본 0.7초) 내내 같은 영역 + 변위 폭이 작다 → Arrived 1회
    ///
    /// 도착 후에는 그 영역을 벗어나기 전까지 재판정하지 않는다(재판정 잠금 · 확정).
    /// MonoBehaviour가 아닌 순수 클래스 — 합성 데이터로 단독 검증 가능.
    public class ArrivalJudge
    {
        readonly List<ArrivalZone> _zones;
        readonly float _windowSeconds;
        readonly float _maxDriftInWindow;
        readonly Queue<(float time, float x)> _samples = new Queue<(float, float)>();

        public int CurrentZoneId { get; private set; } = -1;
        public bool Locked { get; private set; }

        /// 도착이 확정된 영역 Id를 1회 통지
        public event Action<int> Arrived;

        public ArrivalJudge(List<ArrivalZone> zones, float windowSeconds = 0.7f, float maxDriftInWindow = 0.035f)
        {
            _zones = zones;
            _windowSeconds = windowSeconds;
            _maxDriftInWindow = maxDriftInWindow;
        }

        /// 트래킹이 끊기면 호출 — 창을 비워 이전 데이터로 판정하지 않는다
        public void Reset()
        {
            _samples.Clear();
            CurrentZoneId = -1;
            Locked = false;
        }

        public void Tick(float x, float time)
        {
            _samples.Enqueue((time, x));
            while (_samples.Count > 0 && time - _samples.Peek().time > _windowSeconds)
                _samples.Dequeue();

            UpdateZoneMembership(x);

            if (CurrentZoneId < 0 || Locked)
                return;
            if (_samples.Count < 2)
                return;

            // 창이 충분히 채워졌는가 (진입 직후 판정 방지)
            if (time - _samples.Peek().time < _windowSeconds * 0.95f)
                return;

            var zone = FindZone(CurrentZoneId);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var (_, sx) in _samples)
            {
                // 창 전체가 현재 영역 안이어야 한다 — 방금 걸어 들어온 경우 배제
                if (Mathf.Abs(sx - zone.Center) > zone.ExitHalfWidth)
                    return;
                min = Mathf.Min(min, sx);
                max = Mathf.Max(max, sx);
            }

            if (max - min > _maxDriftInWindow)
                return;

            Locked = true;
            Arrived?.Invoke(CurrentZoneId);
        }

        void UpdateZoneMembership(float x)
        {
            if (CurrentZoneId >= 0)
            {
                var current = FindZone(CurrentZoneId);
                if (Mathf.Abs(x - current.Center) > current.ExitHalfWidth)
                {
                    CurrentZoneId = -1;
                    Locked = false; // 영역 이탈 = 재판정 잠금 해제
                }
                else
                    return;
            }

            foreach (var zone in _zones)
            {
                if (Mathf.Abs(x - zone.Center) <= zone.EnterHalfWidth)
                {
                    CurrentZoneId = zone.Id;
                    Locked = false;
                    return;
                }
            }
        }

        ArrivalZone FindZone(int id)
        {
            foreach (var zone in _zones)
                if (zone.Id == id)
                    return zone;
            return null;
        }
    }
}
