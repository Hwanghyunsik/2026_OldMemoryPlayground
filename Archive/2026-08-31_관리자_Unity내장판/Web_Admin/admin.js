// 신명놀이터 관리자 화면 — 외부 라이브러리 없음 (localhost 오프라인 동작)
"use strict";

const $ = (id) => document.getElementById(id);

// Unity 쪽 CardColors 팔레트와 동일 (사용자 카드 배경색)
const CARD_COLORS = ["#408073", "#806640", "#4D668C", "#804D66", "#59804D", "#735980", "#80734D", "#4D7380"];
const GENDER_LABEL = { "남": "남", "여": "여" };

async function api(path, body) {
  const res = await fetch(path, body === undefined
    ? undefined
    : { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
  if (res.status === 403) { location.reload(); throw new Error("locked"); }
  return res.json();
}

function toast(text, isError) {
  const el = $("toast");
  el.textContent = text;
  el.classList.toggle("error", !!isError);
  el.hidden = false;
  clearTimeout(el._timer);
  el._timer = setTimeout(() => { el.hidden = true; }, 2400);
}

function fmtDate(s) { // "yyyy-MM-dd HH:mm:ss" → "yyyy.MM.dd"
  return s ? s.slice(0, 10).replaceAll("-", ".") : "없음";
}

// ---- 숫자 키패드 (POP-A-001 · POP-A-004 공용 · 마우스 + 키보드 숫자키) ----

function buildKeypad(root, onDigit, onErase) {
  root.innerHTML = "";
  const keys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "", "0", "←"];
  for (const k of keys) {
    const b = document.createElement("button");
    if (k === "") { b.style.visibility = "hidden"; root.appendChild(b); continue; }
    b.textContent = k;
    if (k === "←") { b.className = "key-erase"; b.onclick = onErase; }
    else b.onclick = () => onDigit(k);
    root.appendChild(b);
  }
}

function setDots(root, count, error) {
  root.classList.toggle("error", !!error);
  [...root.children].forEach((dot, i) => dot.classList.toggle("on", !error && i < count));
}

// ---- POP-A-001 잠금 화면 ----

const lockState = { pin: "" };

function lockDigit(d) {
  if (lockState.pin.length >= 4) return;
  lockState.pin += d;
  setDots($("lock-dots"), lockState.pin.length);
  if (lockState.pin.length === 4) submitLockPin(); // 4자리 입력 시 즉시 확인 · 확인 버튼 없음
}

function lockErase() {
  lockState.pin = lockState.pin.slice(0, -1);
  setDots($("lock-dots"), lockState.pin.length);
}

async function submitLockPin() {
  const res = await api("/api/auth", { pin: lockState.pin });
  if (res.ok) { enterApp(); return; }
  // 오류: 점 4개 빨강 → 비워짐 · 같은 화면에서 재입력
  setDots($("lock-dots"), 4, true);
  setTimeout(() => { lockState.pin = ""; setDots($("lock-dots"), 0); }, 700);
}

// ---- 화면 전환 ----

let currentPage = "check";

function showPage(page) {
  currentPage = page;
  for (const b of $("nav").children) b.classList.toggle("active", b.dataset.page === page);
  for (const sec of document.querySelectorAll(".page")) sec.hidden = sec.id !== "page-" + page;
  if (page === "users") loadUsers();
  if (page === "check") refreshStatus();
}

function enterApp() {
  $("lock").hidden = true;
  $("app").hidden = false;
  lockState.pin = "";
  setDots($("lock-dots"), 0);
  showPage(currentPage);
}

function lockScreen() { // 화면 잠그기 — 사용자 화면은 그대로 (확정)
  api("/api/lock", {}).then(() => {
    $("app").hidden = true;
    $("lock").hidden = false;
  });
}

// ---- ADM-001 점검 ----

let statusTimer = null;

async function refreshStatus() {
  if ($("page-check").hidden) return;
  try {
    const s = await api("/api/status");
    $("check-now").textContent = s.now;
    setLight("camera", s.camera, s.cameraText);
    setLight("storage", s.storage, s.storageText);
    setLight("clock", s.clock, s.clockText);
    const advice = $("check-advice");
    advice.hidden = !s.advice;
    advice.textContent = s.advice || "";
    $("camera-person").textContent = s.camera !== "ok" ? "카메라가 보이지 않아요"
      : s.personPresent ? "사람을 인식하고 있어요" : "화면 앞에 사람이 없어요";
  } catch (e) { /* 서버 미응답 시 다음 주기에 재시도 */ }
}

function setLight(key, level, text) {
  $("light-" + key).className = "light " + level;
  $("text-" + key).textContent = text;
}

// ---- ADM-002 사용자 관리 ----

const userState = { all: [], page: 1, perPage: 10 }; // 한 쪽 10명 — D2 협의 전 기준안

async function loadUsers() {
  const res = await api("/api/users");
  userState.all = res.users || [];
  renderUsers();
}

function visibleUsers() {
  const q = $("user-search").value.trim();
  let list = userState.all.filter((u) => !q || u.name.includes(q));
  if ($("user-sort").value === "name")
    list.sort((a, b) => a.name.localeCompare(b.name, "ko"));
  else
    list.sort((a, b) => (b.lastPlayedAt || "").localeCompare(a.lastPlayedAt || ""));
  return list;
}

function renderUsers() {
  const list = visibleUsers();
  $("users-total").textContent = userState.all.length;

  const pages = Math.max(1, Math.ceil(list.length / userState.perPage));
  if (userState.page > pages) userState.page = pages;
  const pageList = list.slice((userState.page - 1) * userState.perPage, userState.page * userState.perPage);

  const tbody = $("user-rows");
  tbody.innerHTML = "";
  for (const u of pageList) {
    const tr = document.createElement("tr");
    const color = CARD_COLORS[Math.abs(u.cardColorIndex) % 8];
    tr.innerHTML =
      `<td><div class="avatar-chip" style="background:${color}">그림${u.avatarIndex + 1}</div></td>` +
      `<td><b></b></td><td></td><td></td><td></td><td></td>` +
      `<td><div class="row-actions">` +
      `<button class="btn" data-act="edit">수정</button>` +
      `<button class="btn" data-act="delete">삭제</button></div></td>`;
    tr.children[1].firstChild.textContent = u.name;
    tr.children[2].textContent = GENDER_LABEL[u.gender] || u.gender;
    tr.children[3].textContent = fmtDate(u.createdAt);
    tr.children[4].textContent = fmtDate(u.lastPlayedAt);
    tr.children[5].textContent = u.playCount + "번";
    tr.querySelector('[data-act="edit"]').onclick = () => openUserModal(u);
    tr.querySelector('[data-act="delete"]').onclick = () => confirmDeleteUser(u);
    tbody.appendChild(tr);
  }

  // 빈 상태 2종 (REF-A-01) — 등록이 없는 것과 검색이 안 맞은 것을 구분한다
  const empty = $("users-empty");
  const q = $("user-search").value.trim();
  $("user-table").hidden = list.length === 0;
  empty.hidden = list.length > 0;
  if (list.length === 0) {
    empty.innerHTML = userState.all.length === 0
      ? `<div class="empty-icon">✕</div><p>등록된 사용자가 없습니다</p><p>오른쪽 위 [사용자 등록]으로 첫 사용자를 등록해 주세요</p>`
      : `<div class="empty-icon">🔍</div><p>「<b></b>」에 맞는 사용자가 없습니다</p>`;
    if (userState.all.length > 0) empty.querySelector("b").textContent = q;
  }

  // 쪽 이동
  const paging = $("user-paging");
  paging.innerHTML = "";
  paging.hidden = pages <= 1;
  for (let p = 1; p <= pages; p++) {
    const b = document.createElement("button");
    b.textContent = p;
    b.classList.toggle("active", p === userState.page);
    b.onclick = () => { userState.page = p; renderUsers(); };
    paging.appendChild(b);
  }
}

// ---- POP-A-002 사용자 등록·수정 (같은 팝업 · 제목과 초기값만 다르다) ----

const userModal = { id: "", gender: "", avatarIndex: -1 };

function openUserModal(user) {
  userModal.id = user ? user.id : "";
  userModal.gender = user ? user.gender : "";
  userModal.avatarIndex = user ? user.avatarIndex : -1;
  $("user-modal-title").textContent = user ? "사용자 수정" : "사용자 등록";
  $("user-name").value = user ? user.name : "";
  for (const b of document.querySelectorAll("#modal-user .pick"))
    b.classList.toggle("selected", b.dataset.gender === userModal.gender);
  renderAvatarGrid();
  updateUserSave();
  $("modal-user").hidden = false;
  $("user-name").focus();
}

function renderAvatarGrid() {
  const grid = $("avatar-grid");
  grid.innerHTML = "";
  for (let i = 0; i < 8; i++) {
    const cell = document.createElement("div");
    cell.className = "avatar-cell" + (i === userModal.avatarIndex ? " selected" : "");
    cell.textContent = `그림 ${i + 1}`; // X-Box 자리 표시 — 실제 소재는 확정 후 교체
    cell.onclick = () => { userModal.avatarIndex = i; renderAvatarGrid(); updateUserSave(); };
    grid.appendChild(cell);
  }
}

function updateUserSave() {
  $("user-save").disabled =
    !$("user-name").value.trim() || !userModal.gender || userModal.avatarIndex < 0; // 미입력 시 비활성
}

async function saveUser() {
  const res = await api("/api/user/save", {
    id: userModal.id,
    name: $("user-name").value.trim(),
    gender: userModal.gender,
    avatarIndex: userModal.avatarIndex,
  });
  if (!res.ok) { toast(res.message || "저장하지 못했습니다", true); return; }
  $("modal-user").hidden = true;
  toast(userModal.id ? "사용자 정보를 고쳤습니다" : "사용자를 등록했습니다");
  loadUsers();
}

// ---- POP-A-003 공용 확인 팝업 (삭제 · 잠그기 · 프로그램 종료) ----

function openConfirm(opts) { // {title, text, runLabel, danger, onRun}
  $("confirm-icon").textContent = opts.danger ? "!" : "?";
  $("confirm-icon").className = "confirm-icon" + (opts.danger ? " danger" : "");
  $("confirm-title").textContent = opts.title;
  $("confirm-text").textContent = opts.text;
  const run = $("confirm-run");
  run.textContent = opts.runLabel;
  run.className = "btn " + (opts.danger ? "btn-danger" : "btn-dark");
  run.onclick = () => { $("modal-confirm").hidden = true; opts.onRun(); };
  $("modal-confirm").hidden = false;
}

function confirmDeleteUser(u) {
  openConfirm({
    title: `「${u.name}」 사용자를 삭제합니다`,
    // 결과를 숫자로 적는다 — 무엇이 사라지는지 감추지 않는다 (확정)
    text: `참여 기록 ${u.playCount}번이 함께 삭제됩니다. 삭제한 뒤에는 되돌릴 수 없습니다.`,
    runLabel: "삭제",
    danger: true,
    onRun: async () => {
      const res = await api("/api/user/delete", { id: u.id });
      if (res.ok) { toast("사용자를 삭제했습니다"); loadUsers(); }
      else toast("삭제하지 못했습니다", true);
    },
  });
}

// ---- POP-A-004 번호 변경 (3단계 · 오류 시 같은 단계 재입력) ----

const pinModal = { step: 1, input: "", next: "" };
const PIN_STEP_LABEL = [
  "3단계 중 1단계",
  "3단계 중 2단계",
  "3단계 중 3단계",
];
const PIN_STEP_GUIDE = [
  "지금 쓰는 번호를 눌러 주세요",
  "새로 쓸 번호를 눌러 주세요",
  "새 번호를 다시 한번 눌러 주세요",
];

function openPinModal() {
  pinModal.step = 1;
  pinModal.input = "";
  pinModal.next = "";
  renderPinStep();
  $("modal-pin").hidden = false;
}

function renderPinStep() {
  [...$("pin-steps").children].forEach((bar, i) => bar.classList.toggle("done", i < pinModal.step));
  $("pin-step-label").textContent = PIN_STEP_LABEL[pinModal.step - 1];
  $("pin-guide").textContent = PIN_STEP_GUIDE[pinModal.step - 1];
  setDots($("pin-dots"), pinModal.input.length);
}

function pinDigit(d) {
  if ($("modal-pin").hidden || pinModal.input.length >= 4) return;
  pinModal.input += d;
  setDots($("pin-dots"), pinModal.input.length);
  if (pinModal.input.length === 4) submitPinStep();
}

function pinErase() {
  pinModal.input = pinModal.input.slice(0, -1);
  setDots($("pin-dots"), pinModal.input.length);
}

function pinStepError() { // 틀려도 1단계로 되돌리지 않는다 — 같은 단계에서 다시 (확정)
  setDots($("pin-dots"), 4, true);
  setTimeout(() => { pinModal.input = ""; setDots($("pin-dots"), 0); }, 700);
}

async function submitPinStep() {
  const value = pinModal.input;
  if (pinModal.step === 1) {
    const res = await api("/api/pin/check", { pin: value });
    if (!res.ok) { pinStepError(); return; }
    pinModal.current = value;
    pinModal.step = 2;
  } else if (pinModal.step === 2) {
    if (value === "0000") { toast("0000은 쓸 수 없는 번호입니다", true); pinStepError(); return; }
    pinModal.next = value;
    pinModal.step = 3;
  } else {
    if (value !== pinModal.next) { pinStepError(); return; }
    const res = await api("/api/pin", { current: pinModal.current, next: pinModal.next });
    if (!res.ok) { toast(res.message || "바꾸지 못했습니다", true); pinStepError(); return; }
    $("modal-pin").hidden = true;
    toast("관리자 번호를 바꿨습니다"); // C10 변경 완료 안내 — 기준안
    return;
  }
  pinModal.input = "";
  renderPinStep();
}

// ---- 초기화 ----

async function init() {
  buildKeypad($("lock-keypad"), lockDigit, lockErase);
  buildKeypad($("pin-keypad"), pinDigit, pinErase);

  // 키보드 숫자키 병행 (설계서 POP-A-001)
  document.addEventListener("keydown", (e) => {
    if (/^[0-9]$/.test(e.key)) {
      if (!$("modal-pin").hidden) pinDigit(e.key);
      else if (!$("lock").hidden) lockDigit(e.key);
    } else if (e.key === "Backspace") {
      if (!$("modal-pin").hidden) pinErase();
      else if (!$("lock").hidden) lockErase();
    }
  });

  for (const b of $("nav").children) b.onclick = () => showPage(b.dataset.page);

  $("btn-add-user").onclick = () => openUserModal(null);
  $("user-search").oninput = () => { userState.page = 1; renderUsers(); };
  $("user-sort").onchange = () => { userState.page = 1; renderUsers(); };
  $("user-name").oninput = updateUserSave;
  for (const b of document.querySelectorAll("#modal-user .pick"))
    b.onclick = () => {
      userModal.gender = b.dataset.gender;
      for (const x of document.querySelectorAll("#modal-user .pick"))
        x.classList.toggle("selected", x === b);
      updateUserSave();
    };
  $("user-cancel").onclick = () => { $("modal-user").hidden = true; }; // 입력값 폐기
  $("user-save").onclick = saveUser;
  $("confirm-cancel").onclick = () => { $("modal-confirm").hidden = true; };
  $("pin-cancel").onclick = () => { $("modal-pin").hidden = true; }; // 어느 단계에서든 취소

  $("btn-pin").onclick = openPinModal;
  $("btn-lock").onclick = () => openConfirm({
    title: "화면을 잠급니다",
    text: "번호 입력 화면으로 돌아갑니다. 사용자 화면은 그대로 돌아갑니다.",
    runLabel: "잠그기",
    danger: false,
    onRun: lockScreen,
  });
  $("btn-close").onclick = () => window.close();
  $("btn-quit").onclick = () => openConfirm({
    title: "프로그램을 종료합니다",
    text: "사용자 화면(TV)까지 모두 종료됩니다.",
    runLabel: "종료",
    danger: true,
    onRun: async () => { await api("/api/quit", {}); document.body.innerHTML = ""; },
  });

  statusTimer = setInterval(refreshStatus, 2000);

  const state = await api("/api/state");
  $("app-version").textContent = state.version || "-";
  if (state.unlocked) enterApp(); // 인증은 프로그램 실행 시 1회 — 새로고침에 다시 묻지 않는다
}

init();
