using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using Shinmyeong.Save;

namespace Shinmyeong.Admin
{
    /// 관리자 로컬 웹서버 (18-1 확정: 엣지 PC 내부 로컬 웹서버 · localhost 접속 · 외부 서버 없음).
    /// Unity 프로세스 내장 방식(개발 판단): 저장 파일을 한 프로세스만 만져 동시 접근 문제가 없고,
    /// 카메라·추적 상태 점검과 프로그램 종료(끝내기 3종)를 같은 자리에서 처리할 수 있다.
    /// 정적 파일: StreamingAssets/Admin · API: /api/* — 요청 처리는 전부 메인 스레드에서 한다.
    /// 인증(18-2): 프로그램 실행 시 1회 · 화면 간 이동 시 다시 묻지 않음 · 잠그기 시에만 재인증.
    public class AdminServer : MonoBehaviour
    {
        public const int Port = 8975;

        static AdminServer _instance;

        HttpListener _listener;
        Thread _acceptThread;
        volatile bool _running;
        readonly ConcurrentQueue<HttpListenerContext> _pending = new ConcurrentQueue<HttpListenerContext>();

        string _staticRoot;
        bool _unlocked; // 프로그램 실행 단위 세션 — 잠그기 전까지 유지 (확정)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null)
                return;
            var go = new GameObject("AdminServer");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AdminServer>();
        }

        void Start()
        {
            _staticRoot = Path.Combine(Application.streamingAssetsPath, "Admin");
            try
            {
                _listener = new HttpListener();
                // localhost 계열 프리픽스는 관리자 권한(URL ACL 예약) 없이 열 수 있다
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                _listener.Start();
                _running = true;
                _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "AdminServer" };
                _acceptThread.Start();
                Debug.Log($"[Admin] 관리자 화면: http://localhost:{Port}/ (2번 모니터 브라우저로 접속)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Admin] 서버 시작 실패(포트 {Port}): {e.Message}");
            }
        }

        void AcceptLoop()
        {
            while (_running)
            {
                try { _pending.Enqueue(_listener.GetContext()); }
                catch { if (_running) Thread.Sleep(100); } // Stop() 호출 시 예외로 빠져나온다
            }
        }

        void Update()
        {
            // Unity API(카메라·종료 등)와 저장 파일 접근을 한 스레드로 모으기 위해 메인 스레드에서 처리
            while (_pending.TryDequeue(out var ctx))
            {
                try { Handle(ctx); }
                catch (Exception e)
                {
                    Debug.LogError($"[Admin] 요청 처리 실패: {e.Message}");
                    try { WriteJson(ctx, 500, new OkRes { ok = false, message = "server error" }); } catch { }
                }
            }
        }

        void OnDestroy() => Shutdown();
        void OnApplicationQuit() => Shutdown();

        void Shutdown()
        {
            _running = false;
            try { _listener?.Stop(); _listener?.Close(); } catch { }
        }

        // ---- 라우팅 ----

        void Handle(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath;
            string method = ctx.Request.HttpMethod;

            if (path.StartsWith("/api/"))
            {
                HandleApi(ctx, method, path);
                return;
            }
            ServeStatic(ctx, path);
        }

        void HandleApi(HttpListenerContext ctx, string method, string path)
        {
            // 인증 전에 허용되는 것: 상태 조회 · 인증 시도
            if (path == "/api/state" && method == "GET")
            {
                WriteJson(ctx, 200, new StateRes { unlocked = _unlocked, version = Application.version });
                return;
            }
            if (path == "/api/auth" && method == "POST")
            {
                var req = ReadBody<PinReq>(ctx);
                if (req != null && PinValid(req.pin))
                {
                    _unlocked = true;
                    WriteJson(ctx, 200, new OkRes { ok = true });
                }
                else
                    WriteJson(ctx, 200, new OkRes { ok = false });
                return;
            }
            if (!_unlocked)
            {
                WriteJson(ctx, 403, new OkRes { ok = false, message = "locked" });
                return;
            }

            switch (path)
            {
                case "/api/lock" when method == "POST":       // 화면 잠그기 — 사용자 화면은 그대로 (확정)
                    _unlocked = false;
                    WriteJson(ctx, 200, new OkRes { ok = true });
                    break;
                case "/api/users" when method == "GET":
                    WriteJson(ctx, 200, BuildUserList());
                    break;
                case "/api/user/save" when method == "POST":
                    HandleUserSave(ctx);
                    break;
                case "/api/user/delete" when method == "POST":
                    HandleUserDelete(ctx);
                    break;
                case "/api/status" when method == "GET":
                    WriteJson(ctx, 200, BuildStatus());
                    break;
                case "/api/pin/check" when method == "POST":  // POP-A-004 1단계 — 현재 번호 확인
                {
                    var req = ReadBody<PinReq>(ctx);
                    WriteJson(ctx, 200, new OkRes { ok = req != null && PinValid(req.pin) });
                    break;
                }
                case "/api/pin" when method == "POST":        // POP-A-004 3단계 완료 — 번호 변경
                    HandlePinChange(ctx);
                    break;
                case "/api/quit" when method == "POST":       // 프로그램 종료 — 사용자 화면까지 모두 (확정)
                    WriteJson(ctx, 200, new OkRes { ok = true });
                    StartCoroutine(QuitNextFrame());
                    break;
                default:
                    WriteJson(ctx, 404, new OkRes { ok = false, message = "not found" });
                    break;
            }
        }

        bool PinValid(string pin)
        {
            if (string.IsNullOrEmpty(pin))
                return false;
            if (pin == "0000") // 마스터 번호 — 번호를 바꿔도 상시 유효 (확정)
                return true;
            var saved = SaveStore.LoadAdmin().Pin;
            return !string.IsNullOrEmpty(saved) && pin == saved;
        }

        // ---- 사용자 관리 (ADM-002 · POP-A-002 · FN-20) ----

        UserListRes BuildUserList()
        {
            var res = new UserListRes();
            foreach (var u in SaveStore.LoadUsers().Users)
            {
                var records = SaveStore.LoadRecords(u.Id).Records;
                res.users.Add(new UserRow
                {
                    id = u.Id,
                    name = u.Name,
                    gender = u.Gender,
                    avatarIndex = u.AvatarIndex,
                    cardColorIndex = u.CardColorIndex,
                    createdAt = u.CreatedAt,
                    playCount = records.Count,
                    lastPlayedAt = records.Count > 0 ? records[records.Count - 1].StartedAt : "",
                });
            }
            return res;
        }

        void HandleUserSave(HttpListenerContext ctx)
        {
            var req = ReadBody<UserSaveReq>(ctx);
            if (req == null || string.IsNullOrWhiteSpace(req.name) ||
                req.name.Trim().Length > 5 || // 이름 최대 5글자 (설계서 POP-A-002)
                (req.gender != "남" && req.gender != "여") ||
                req.avatarIndex < 0 || req.avatarIndex > 7)
            {
                WriteJson(ctx, 200, new OkRes { ok = false, message = "잘못된 입력" });
                return;
            }

            string name = req.name.Trim();
            if (string.IsNullOrEmpty(req.id))
            {
                // 카드 배경색은 저장할 때 자동 배정 (확정 18-5) — 가장 적게 쓰인 색을 준다
                SaveStore.AddUser(name, req.gender, req.avatarIndex, LeastUsedCardColor());
            }
            else
            {
                var user = SaveStore.LoadUsers().Users.Find(u => u.Id == req.id);
                if (user == null)
                {
                    WriteJson(ctx, 200, new OkRes { ok = false, message = "사용자를 찾을 수 없음" });
                    return;
                }
                user.Name = name;
                user.Gender = req.gender;
                user.AvatarIndex = req.avatarIndex;
                SaveStore.UpdateUser(user);
            }
            WriteJson(ctx, 200, new OkRes { ok = true });
        }

        static int LeastUsedCardColor()
        {
            var counts = new int[8];
            foreach (var u in SaveStore.LoadUsers().Users)
                counts[Mathf.Abs(u.CardColorIndex) % 8]++;
            int best = 0;
            for (int i = 1; i < 8; i++)
                if (counts[i] < counts[best])
                    best = i;
            return best;
        }

        void HandleUserDelete(HttpListenerContext ctx)
        {
            var req = ReadBody<UserIdReq>(ctx);
            if (req == null || string.IsNullOrEmpty(req.id))
            {
                WriteJson(ctx, 200, new OkRes { ok = false, message = "잘못된 입력" });
                return;
            }
            // D1 협의(기록 익명 보존) 회신 전 기본 동작: 참여 기록 동반 삭제 (FN-20 · SaveStore 구현)
            SaveStore.DeleteUser(req.id);
            WriteJson(ctx, 200, new OkRes { ok = true });
        }

        // ---- 점검 (ADM-001 · REF-A-02 확정 3종: 카메라 연결 · 저장 공간 · 날짜 시각) ----

        StatusRes BuildStatus()
        {
            var res = new StatusRes { now = DateTime.Now.ToString("yyyy년 M월 d일 HH:mm") };

            bool cameraOk = WebCamTexture.devices.Length > 0;
            res.camera = cameraOk ? "ok" : "bad";
            res.cameraText = cameraOk ? "카메라가 연결되어 있어요" : "카메라가 보이지 않아요";
            if (!cameraOk)
                res.advice = "카메라 선이 빠지지 않았는지 확인하고, 프로그램을 다시 시작해 주세요";

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Application.persistentDataPath));
                long free = drive.AvailableFreeSpace;
                // 경고 임계값은 설치 시 결정(18-7) — 기준안: 2GB 주의 · 500MB 문제
                res.storage = free < 500_000_000L ? "bad" : free < 2_000_000_000L ? "warn" : "ok";
                res.storageText = res.storage == "ok" ? "저장 공간이 넉넉해요"
                    : res.storage == "warn" ? "저장 공간이 줄고 있어요" : "저장 공간이 거의 없어요";
                if (res.storage != "ok" && string.IsNullOrEmpty(res.advice))
                    res.advice = "쓰지 않는 파일을 정리하거나 관리 담당자에게 알려 주세요";
            }
            catch
            {
                res.storage = "warn";
                res.storageText = "저장 공간을 확인하지 못했어요";
            }

            res.clock = "ok"; // 상태 표시만 하고 이 화면에서 고치지 않는다 (확정)
            res.clockText = $"기기 시계 {DateTime.Now:HH:mm}";

            var tracking = Shinmyeong.Tracking.BodyTrackingService.Instance;
            res.personPresent = tracking != null && tracking.PersonPresent;
            return res;
        }

        // ---- 설정 (ADM-008 · POP-A-004) ----

        void HandlePinChange(HttpListenerContext ctx)
        {
            var req = ReadBody<PinChangeReq>(ctx);
            if (req == null || !PinValid(req.current))
            {
                WriteJson(ctx, 200, new OkRes { ok = false, message = "현재 번호가 달라요" });
                return;
            }
            if (string.IsNullOrEmpty(req.next) || req.next.Length != 4 || req.next == "0000")
            {
                // 0000은 마스터 번호라 새 번호로 쓸 수 없다 (확정)
                WriteJson(ctx, 200, new OkRes { ok = false, message = "쓸 수 없는 번호예요" });
                return;
            }
            var config = SaveStore.LoadAdmin();
            config.Pin = req.next;
            SaveStore.SaveAdmin(config);
            WriteJson(ctx, 200, new OkRes { ok = true });
        }

        IEnumerator QuitNextFrame()
        {
            yield return null; // 응답이 브라우저에 닿을 시간을 준다
            Debug.Log("[Admin] 프로그램 종료 (관리자 화면 요청)");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ---- 정적 파일 ----

        void ServeStatic(HttpListenerContext ctx, string path)
        {
            if (path == "/")
                path = "/index.html";
            // 경로 탈출 방지
            string full = Path.GetFullPath(Path.Combine(_staticRoot, path.TrimStart('/')));
            if (!full.StartsWith(Path.GetFullPath(_staticRoot)) || !File.Exists(full))
            {
                WriteText(ctx, 404, "text/plain; charset=utf-8", "not found");
                return;
            }
            var bytes = File.ReadAllBytes(full);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = MimeOf(Path.GetExtension(full));
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        static string MimeOf(string ext)
        {
            switch (ext.ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".js": return "text/javascript; charset=utf-8";
                case ".png": return "image/png";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
                default: return "application/octet-stream";
            }
        }

        // ---- IO 헬퍼 ----

        static T ReadBody<T>(HttpListenerContext ctx) where T : class
        {
            try
            {
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    return JsonUtility.FromJson<T>(reader.ReadToEnd());
            }
            catch { return null; }
        }

        static void WriteJson(HttpListenerContext ctx, int status, object data) =>
            WriteText(ctx, status, "application/json; charset=utf-8", JsonUtility.ToJson(data));

        static void WriteText(HttpListenerContext ctx, int status, string contentType, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        // ---- API 모델 (JsonUtility · 소문자 필드 = JS 쪽 이름) ----

        [Serializable] class OkRes { public bool ok; public string message; }
        [Serializable] class StateRes { public bool unlocked; public string version; }
        [Serializable] class PinReq { public string pin; }
        [Serializable] class PinChangeReq { public string current; public string next; }
        [Serializable] class UserIdReq { public string id; }
        [Serializable] class UserSaveReq { public string id; public string name; public string gender; public int avatarIndex; }

        [Serializable]
        class UserRow
        {
            public string id, name, gender, createdAt, lastPlayedAt;
            public int avatarIndex, cardColorIndex, playCount;
        }

        [Serializable] class UserListRes { public List<UserRow> users = new List<UserRow>(); }

        [Serializable]
        class StatusRes
        {
            public string now;
            public string camera, cameraText, storage, storageText, clock, clockText;
            public string advice;
            public bool personPresent;
        }
    }
}
