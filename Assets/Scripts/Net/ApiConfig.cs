using System;
using System.IO;
using UnityEngine;

namespace Shinmyeong.Net
{
    /// 서버 API 접속 설정 (Docs/신명놀이터_API.md 「공통」).
    /// 기본 주소는 개발 서버. 바꾸려면 실행 인자 `-api http://localhost:3000/api` 또는
    /// persistentDataPath/api_config.json `{ "base_url": "..." }` (인자가 파일보다 우선).
    /// 모든 경로가 `/api/…` 이므로 기본 주소에 `/api` 까지 넣어 두고, 호출부는 `auth/login` 처럼 그 뒤만 적는다.
    public static class ApiConfig
    {
        public const string DefaultBaseUrl = "https://dev.joylab.kr:33780/api";
        public const int TimeoutSec = 5;

        // 호출 주기 — 명세 1장 표 · 4-8 요청 1(상태 보고 30초 이하 · 권장 5초)
        public const float StatusIntervalSec = 5f;    // B1 PUT devices/me/status
        public const float CommandIntervalSec = 5f;   // GET devices/me/commands
        public const float SettingsIntervalSec = 60f; // B2 GET devices/me/settings
        public const float QueueRetrySec = 30f;       // A2 재전송 (3-3)

        [Serializable]
        class FileConfig
        {
            public string base_url;
        }

        static string _baseUrl;

        public static string BaseUrl
        {
            get
            {
                if (_baseUrl == null)
                    _baseUrl = Resolve().TrimEnd('/');
                return _baseUrl;
            }
        }

        public static string ConfigPath => Path.Combine(Application.persistentDataPath, "api_config.json");

        /// 관리자 웹 주소 — API 주소에서 `/api` 를 뗀 것 (아이디·비밀번호 찾기 등 웹으로 넘길 때)
        public static string WebUrl
        {
            get
            {
                string b = BaseUrl;
                return b.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? b.Substring(0, b.Length - 4) : b;
            }
        }

        /// B1 · A2 의 `app_version` — Player Settings 의 Version (ProjectSettings bundleVersion)
        public static string AppVersion => Application.version;

        public static string Url(string path) => BaseUrl + "/" + path.TrimStart('/');

        /// 시간대가 붙은 ISO 8601 (명세 공통 「시각」 — 시간대 없으면 400). 예: 2026-10-19T11:54:17.000+09:00
        public static string ToIso(DateTimeOffset t) => t.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz");
        public static string NowIso() => ToIso(DateTimeOffset.Now);

        static string Resolve()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-api" && !string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    Debug.Log($"[Api] 주소(실행 인자): {args[i + 1]}");
                    return args[i + 1];
                }
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var cfg = JsonUtility.FromJson<FileConfig>(File.ReadAllText(ConfigPath));
                    if (cfg != null && !string.IsNullOrWhiteSpace(cfg.base_url))
                    {
                        Debug.Log($"[Api] 주소(설정 파일): {cfg.base_url}");
                        return cfg.base_url;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Api] 설정 파일 읽기 실패({ConfigPath}): {e.Message} — 기본 주소 사용");
            }
            return DefaultBaseUrl;
        }
    }
}
