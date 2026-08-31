using System.Collections.Generic;
using UnityEngine;
using Shinmyeong.Tracking;

namespace Shinmyeong.Interaction
{
    /// 장보기 도착 판정 개발용 데모. 발판 5개 배치(L2·L1·C·R1·R2)를 흉내 내고
    /// 화면 하단에 영역·몸 중심·판정 상태를 그린다. 실제 게임 구현 시 교체.
    public class ArrivalDemoView : MonoBehaviour
    {
        [Header("판정 파라미터 (C1 기준안 · 실환경 테스트로 확정)")]
        [SerializeField] float _windowSeconds = 0.7f;
        [SerializeField] float _maxDriftInWindow = 0.035f;
        [SerializeField] float _enterHalfWidth = 0.06f;
        [SerializeField] float _exitHalfWidth = 0.09f;

        [Header("발판 배치 (정규화 x)")]
        [SerializeField] float[] _zoneCenters = { 0.15f, 0.325f, 0.5f, 0.675f, 0.85f };

        static readonly string[] ZoneNames = { "L2", "L1", "C", "R1", "R2" };

        ArrivalJudge _judge;
        readonly List<string> _log = new List<string>();
        float _flashUntil;
        int _flashZone = -1;

        void OnEnable() => BuildJudge();

        void OnValidate()
        {
            if (Application.isPlaying && _judge != null)
                BuildJudge();
        }

        void BuildJudge()
        {
            var zones = new List<ArrivalZone>();
            for (int i = 0; i < _zoneCenters.Length; i++)
                zones.Add(new ArrivalZone
                {
                    Id = i,
                    Center = _zoneCenters[i],
                    EnterHalfWidth = _enterHalfWidth,
                    ExitHalfWidth = _exitHalfWidth,
                });
            _judge = new ArrivalJudge(zones, _windowSeconds, _maxDriftInWindow);
            _judge.Arrived += OnArrived;
        }

        void OnArrived(int zoneId)
        {
            _flashZone = zoneId;
            _flashUntil = Time.time + 0.6f;
            string name = zoneId < ZoneNames.Length ? ZoneNames[zoneId] : zoneId.ToString();
            _log.Insert(0, $"[{Time.time:F1}s] 도착: {name}");
            if (_log.Count > 5)
                _log.RemoveAt(_log.Count - 1);
            Debug.Log($"[ArrivalDemo] 도착 판정: {name}");
        }

        void Update()
        {
            var svc = BodyTrackingService.Instance;
            if (svc == null || !svc.BodyValid)
            {
                _judge.Reset();
                return;
            }
            _judge.Tick(svc.BodyCenterX01, Time.time);
        }

        void OnGUI()
        {
            var svc = BodyTrackingService.Instance;
            if (svc == null)
                return;

            float stripY = Screen.height - 120;
            float stripH = 60;

            for (int i = 0; i < _zoneCenters.Length; i++)
            {
                DrawZone(_zoneCenters[i], _exitHalfWidth, stripY, stripH, new Color(1f, 1f, 1f, 0.12f));
                bool isCurrent = _judge.CurrentZoneId == i;
                bool flashing = _flashZone == i && Time.time < _flashUntil;
                Color c = flashing ? new Color(0.3f, 1f, 0.4f, 0.85f)
                    : isCurrent && _judge.Locked ? new Color(0.3f, 0.8f, 0.5f, 0.5f)
                    : isCurrent ? new Color(1f, 0.85f, 0.2f, 0.4f)
                    : new Color(1f, 1f, 1f, 0.22f);
                DrawZone(_zoneCenters[i], _enterHalfWidth, stripY, stripH, c);

                var labelPos = new Rect(_zoneCenters[i] * Screen.width - 20, stripY + stripH + 4, 40, 20);
                GUI.Label(labelPos, i < ZoneNames.Length ? ZoneNames[i] : i.ToString());
            }

            if (svc.BodyValid)
            {
                var prev = GUI.color;
                GUI.color = new Color(0.2f, 0.9f, 1f);
                GUI.DrawTexture(new Rect(svc.BodyCenterX01 * Screen.width - 2, stripY - 8, 4, stripH + 16), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            for (int i = 0; i < _log.Count; i++)
                GUI.Label(new Rect(Screen.width - 220, stripY - 110 + i * 20, 210, 20), _log[i]);
        }

        static void DrawZone(float center, float halfWidth, float y, float h, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            float x = (center - halfWidth) * Screen.width;
            float w = halfWidth * 2f * Screen.width;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
