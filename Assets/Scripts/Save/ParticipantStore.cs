using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Shinmyeong.Net;

namespace Shinmyeong.Save
{
    /// 사용자 목록 — 서버 A1 `GET participants`(기관 전체) + 로컬 캐시 (Docs/94 3장 3단계).
    /// SCR-003 은 캐시로 먼저 그리고, 서버 응답이 오면 다시 그린다. 서버에 닿지 못하면 마지막 캐시를 그대로 쓴다.
    /// 등록·수정·삭제는 관리자 웹 몫(FN-20) — 게임은 읽기만 한다.
    public static class ParticipantStore
    {
        static string CachePath => Path.Combine(SaveStore.Root, "participants_cache.json");
        static List<UserProfile> _cached;

        /// 마지막으로 받은 목록 (없으면 빈 목록)
        public static List<UserProfile> Cached
        {
            get
            {
                if (_cached == null)
                    _cached = (SaveStore.LoadJson<UserDatabase>(CachePath) ?? new UserDatabase()).Users;
                return _cached;
            }
        }

        public static bool HasCache => Cached.Count > 0;

        public static UserProfile Find(string id) => string.IsNullOrEmpty(id) ? null : Cached.Find(u => u.Id == id);

        /// 서버에서 다시 받아 캐시를 갱신한다. 실패해도 캐시는 유지
        public static async Awaitable<ApiResult<ParticipantsResponse>> Refresh()
        {
            var res = await ApiClient.Get<ParticipantsResponse>("participants", DeviceAuth.Token);
            if (!res.Ok) return res;
            var list = new List<UserProfile>();
            foreach (var p in res.Data.participants)
                list.Add(new UserProfile
                {
                    Id = p.id,
                    Name = p.name,
                    Gender = p.gender,
                    AvatarIndex = p.avatar_index,
                    CardColorIndex = p.card_color_index,
                });
            _cached = list;
            SaveStore.WriteJson(CachePath, new UserDatabase { Users = list });
            return res;
        }
    }
}
