using UnityEngine;

namespace Shinmyeong.UI
{
    /// 캔버스가 화면 좌표 변환에 쓰는 카메라 — Overlay면 null, Screen Space Camera면 그 카메라
    public static class UiCamera
    {
        public static Camera Of(Canvas canvas)
        {
            var root = canvas.rootCanvas;
            return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }
    }

    /// 디자이너 파티클 이펙트(`Assets/Prefabs/P_*` · 튜토리얼 영상과 같은 연출)를 UI 위에 띄운다.
    /// 이펙트는 시안 씬처럼 Screen Space Camera 캔버스(직교 카메라 size 5 → 1유닛 = 108px) 기준으로 만들어져 있어
    /// UICanvas도 같은 방식이어야 크기·앞뒤 순서가 맞는다(파티클 sortingOrder 1~3 > 캔버스 0).
    /// 이펙트는 배경·틀 위, 물건 그림(작물·재료) 뒤에 그려야 한다 — 물건 그림은 KeepInFront로 이펙트 앞에 올린다.
    ///  · P_TargetPoint_Game1/2 — 목표 강조(빛 테 + 퍼지는 빛) · 3초 주기
    ///  · P_Get_Game1/2/3 — 담김 순간 반짝임(빛 테 + 별 8개) · 1초 이내
    public static class EffectKit
    {
        public const string HarvestTarget = "P_TargetPoint_Game1";
        public const string ShoppingTarget = "P_TargetPoint_Game2";
        public const string HarvestGet = "P_Get_Game1";
        public const string ShoppingGet = "P_Get_Game2";
        public const string CookingGet = "P_Get_Game3";

        /// parent 중심(+offset px)에 이펙트를 띄운다. loop=false면 한 주기 재생 후 스스로 지워진다(중간에 끊으려면 Stop).
        /// loop=true면 하위 파티클을 반복으로 바꿔 계속 재생 — 끌 때는 Stop으로 지운다.
        public static GameObject Play(string name, RectTransform parent, Vector2 offset = default, bool loop = false)
        {
            var prefab = UiCatalog.Instance != null ? UiCatalog.Instance.FindEffect(name) : null;
            if (prefab == null || parent == null)
            {
                if (prefab == null)
                    Debug.LogWarning($"[EffectKit] 이펙트 없음: {name} — 메뉴 「신명/UI 카탈로그 재생성」 확인");
                return null;
            }

            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            go.layer = parent.gameObject.layer;
            var rect = go.transform as RectTransform;
            if (rect != null)
            {
                // 부모 사각형 중심 기준 — 프리팹에 남은 시안 씬 좌표는 버린다
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition3D = new Vector3(offset.x, -offset.y, 0f);
            }
            else
                go.transform.localPosition = new Vector3(offset.x, -offset.y, 0f);

            float lifetime = 0f;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.gameObject.layer = go.layer;
                if (ps.emission.enabled)
                    lifetime = Mathf.Max(lifetime, ps.main.startDelay.constantMax + ps.main.duration + ps.main.startLifetime.constantMax);
                if (loop)
                {
                    var main = ps.main;
                    main.loop = true;
                }
            }
            go.GetComponent<ParticleSystem>().Play(true);
            if (!loop)
                Object.Destroy(go, lifetime + 0.1f);
            return go;
        }

        /// 이펙트(파티클 sortingOrder 1~3)보다 앞에 그릴 물건 그림 — 자체 정렬 캔버스로 순서를 올린다
        public static void KeepInFront(GameObject go)
        {
            if (go.GetComponent<InFrontOfEffects>() == null)
                go.AddComponent<InFrontOfEffects>();
        }

        public static void Stop(ref GameObject effect)
        {
            if (effect != null)
                Object.Destroy(effect);
            effect = null;
        }
    }
}
