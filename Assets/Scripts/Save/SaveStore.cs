using System;
using System.IO;
using UnityEngine;

namespace Shinmyeong.Save
{
    /// 로컬 파일 저장 도우미 — 2026-10-19 서버 연동 뒤에는 **캐시 전용**이다.
    /// 사용자 목록은 서버(A1)가 원본이고 `ParticipantStore` 가 여기에 캐시를 둔다. 참여 기록은 `Net/Outbox` 가 보낼 때까지만 파일로 둔다.
    /// (이전의 users.json · records/ 로컬 완결 저장은 기획 03 문서 6-1 「서버 없음」 기준이었고, 2026-10-07 외부 서버 결정으로 폐기 — 기획 개정 Q1 대기)
    /// 쓰기는 임시 파일에 쓴 뒤 교체해 중단(전원 차단 등)에도 기존 파일이 살아남게 한다.
    public static class SaveStore
    {
        public static string Root => Path.Combine(Application.persistentDataPath, "SaveData");

        public static T LoadJson<T>(string path) where T : class
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

        public static void WriteJson(string path, object data)
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
