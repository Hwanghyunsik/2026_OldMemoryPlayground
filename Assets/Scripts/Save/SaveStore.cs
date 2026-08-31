using System;
using System.IO;
using UnityEngine;

namespace Shinmyeong.Save
{
    /// 로컬 저장소 — 엣지 PC 로컬 · 서버 없음 (03 문서 6-1).
    /// persistentDataPath/SaveData/users.json + records/<사용자Id>.json.
    /// 쓰기는 임시 파일에 쓴 뒤 교체해 중단(전원 차단 등)에도 기존 파일이 살아남게 한다.
    public static class SaveStore
    {
        static string Root => Path.Combine(Application.persistentDataPath, "SaveData");
        static string UsersPath => Path.Combine(Root, "users.json");
        static string RecordsDir => Path.Combine(Root, "records");
        static string RecordsPath(string userId) => Path.Combine(RecordsDir, userId + ".json");

        // ---- 사용자 ----

        public static UserDatabase LoadUsers() => LoadJson<UserDatabase>(UsersPath) ?? new UserDatabase();

        public static UserProfile AddUser(string name, string gender, int avatarIndex, int cardColorIndex)
        {
            var db = LoadUsers();
            var user = new UserProfile
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Gender = gender,
                AvatarIndex = avatarIndex,
                CardColorIndex = cardColorIndex,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            db.Users.Add(user);
            WriteJson(UsersPath, db);
            return user;
        }

        public static void UpdateUser(UserProfile user)
        {
            var db = LoadUsers();
            int idx = db.Users.FindIndex(u => u.Id == user.Id);
            if (idx < 0)
            {
                Debug.LogWarning($"[Save] 수정할 사용자를 찾지 못함: {user.Id}");
                return;
            }
            db.Users[idx] = user;
            WriteJson(UsersPath, db);
        }

        /// 사용자 삭제 시 참여 기록도 함께 지운다 (FN-20 확정)
        public static void DeleteUser(string userId)
        {
            var db = LoadUsers();
            db.Users.RemoveAll(u => u.Id == userId);
            WriteJson(UsersPath, db);
            try
            {
                if (File.Exists(RecordsPath(userId)))
                    File.Delete(RecordsPath(userId));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 참여 기록 삭제 실패({userId}): {e.Message}");
            }
        }

        // ---- 참여 기록 ----

        public static RecordDatabase LoadRecords(string userId)
        {
            var db = LoadJson<RecordDatabase>(RecordsPath(userId));
            return db ?? new RecordDatabase { UserId = userId };
        }

        public static void AppendRecord(string userId, PlayRecord record)
        {
            var db = LoadRecords(userId);
            db.Records.Add(record);
            WriteJson(RecordsPath(userId), db);
            Debug.Log($"[Save] 참여 기록 저장: {record.Activity}/{record.Mode} · {record.DurationSec:F0}s"
                + $" · 성공 {record.SuccessCount} · 누적 {db.Records.Count}건\n→ {RecordsPath(userId)}");
        }

        // ---- 파일 IO ----

        static T LoadJson<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var obj = JsonUtility.FromJson<T>(File.ReadAllText(path));
                if (obj == null)
                    throw new Exception("빈 JSON");
                return obj;
            }
            catch (Exception e)
            {
                // 손상 파일은 지우지 않고 .corrupt로 보존해 두고 새로 시작 (복구 여지)
                Debug.LogError($"[Save] 읽기 실패({path}): {e.Message} — .corrupt로 보존 후 새로 시작");
                try { File.Copy(path, path + ".corrupt", true); } catch { }
                return null;
            }
        }

        static void WriteJson(string path, object data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 쓰기 실패({path}): {e.Message}");
            }
        }
    }
}
