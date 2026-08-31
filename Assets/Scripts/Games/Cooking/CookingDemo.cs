using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Interaction;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Games.Cooking
{
    /// 요리하기 정식 게임 루프 (그래픽은 X-Box 플레이스홀더 — 자산 도입 시 교체).
    /// 힌트 유효 행동(C4): 재료 선택 확정만 타이머를 리셋한다(3-2 현재 기준) — 손 이동·호버는 리셋 없음.
    /// 04 게임구성표 확정 레시피 10종 고정 순서 · 재료 풀 22종.
    /// 규칙(07 문서 6장):
    ///   레시피 기억(SCR-018-1): 불투명 차폐 · 공개 게이지(1개 3.5s/2개 5s/3개 6.5s) ·
    ///     자동으로 닫히지 않고 「다 외웠어요」(dwell 2초)로만 닫힘
    ///   재료 선택(SCR-018): 후보 6칸(3×2) · dwell 1~2초(기준안 1.5) · 취소·교체 없음 ·
    ///     슬롯이 다 차면 일괄 판정 · 색상 계열로만 구분(부정 기호 금지)
    ///   차림표 다시 보기: 같은 팝업 재사용 · 5초 자동 복귀 · 담은 재료 유지 · 횟수 무제한(기록만)
    ///   자동 힌트: 10초 무선택 → 미담은 첫 정답 재료 테두리 지속 강조 · 다른 재료 선택 시 취소+재시작
    public class CookingDemo : MonoBehaviour
    {
        [Header("판정 기준값 (C3 기준안 · 실환경 테스트로 확정)")]
        [SerializeField] float _pickDwellSeconds = 1.5f;
        [SerializeField] float _hintIntervalSeconds = 10f;

        /// 10라운드 완주 시에만 발생 — 중도 종료는 기록을 남기지 않는다(6-2)
        public event System.Action<PlayRecord, IReadOnlyList<string>> Finished;

        /// 레시피 기억 팝업(SCR-018-1) 표시 중 — 이때는 일시정지를 호출할 수 없다(설계서 확정)
        public bool RecipePopupOpen => _popupOpen;

        class Recipe
        {
            public string Food;
            public string[] Answers;
        }

        // 04 게임구성표 확정 — 라운드 순서 고정 · 음식은 한 판 1회
        static readonly Recipe[] Recipes =
        {
            new Recipe { Food = "두부조림", Answers = new[] { "두부" } },
            new Recipe { Food = "수정과", Answers = new[] { "곶감" } },
            new Recipe { Food = "김밥", Answers = new[] { "김", "밥" } },
            new Recipe { Food = "호박죽", Answers = new[] { "호박", "팥" } },
            new Recipe { Food = "미역국", Answers = new[] { "미역", "소고기" } },
            new Recipe { Food = "김치전", Answers = new[] { "김치", "밀가루", "달걀" } },
            new Recipe { Food = "잡채", Answers = new[] { "당면", "시금치", "당근" } },
            new Recipe { Food = "산적", Answers = new[] { "소고기", "파", "버섯" } },
            new Recipe { Food = "송편", Answers = new[] { "쌀가루", "깨", "솔잎" } },
            new Recipe { Food = "갈비찜", Answers = new[] { "갈비", "간장", "밤" } },
        };

        static readonly string[] ItemPool =
        {
            "밥", "쌀가루", "밀가루", "호박", "시금치", "당근", "파", "버섯", "김치",
            "팥", "깨", "밤", "미역", "김", "소고기", "갈비", "두부", "당면", "곶감", "달걀", "간장", "솔잎",
        };

        class Candidate
        {
            public string Item;
            public bool IsAnswer;
            public bool Picked;
            public Image Panel;
            public DwellTarget Target;
            public Color BaseColor;
        }

        RectTransform _stageRoot;
        PlayHud _hud;
        GameObject _candRoot;
        GameObject _slotRoot;
        readonly List<Candidate> _cands = new List<Candidate>();
        readonly List<Image> _slotPanels = new List<Image>();
        readonly List<Text> _slotTexts = new List<Text>();
        GameObject _reviewButton;

        GameObject _popup;
        Text _popupFood;
        Text _popupItems;
        Image _gaugeFill;
        GameObject _memorizedButton;

        readonly List<string> _picks = new List<string>();
        readonly List<string> _records = new List<string>();
        readonly System.Random _rng = new System.Random();
        Candidate _hinted;
        bool _memorized;
        bool _reviewRequested;
        bool _popupOpen;
        float _nextHintAt;
        float _roundT0;       // 최초 팝업 닫힘 시점 — 반응·라운드 시간의 기준점
        float _firstPickSec;  // 첫 재료 선택까지 (반응 시간 · 18-4 끝점) · 미선택 -1
        float _startTime;
        PlayRecord _play;

        static readonly Color CandNormal = new Color(0.32f, 0.36f, 0.42f);
        static readonly Color CandPicked = new Color(0.5f, 0.5f, 0.55f);
        static readonly Color CandHint = new Color(0.85f, 0.75f, 0.3f);
        static readonly Color JudgeCorrect = new Color(0.32f, 0.62f, 0.38f);
        static readonly Color JudgeOther = new Color(0.78f, 0.52f, 0.28f);

        public void Begin(RectTransform host)
        {
            End();

            var rootGo = new GameObject("CookingStage");
            rootGo.transform.SetParent(host, false);
            var rect = rootGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            _stageRoot = rect;

            BuildStage();
            _records.Clear();
            _play = PlayRecord.Start("Cooking");
            GamePause.ResetAccumulated();
            _startTime = Time.time;
            StartCoroutine(GameLoop());
        }

        public void End()
        {
            StopAllCoroutines();
            _cands.Clear();
            _slotPanels.Clear();
            _slotTexts.Clear();
            if (_stageRoot != null)
            {
                Destroy(_stageRoot.gameObject);
                _stageRoot = null;
            }
        }

        void BuildStage()
        {
            // 공통 HUD — 목표 패널(음식 이름만 · 완성 음식 이미지는 플레이 중 금지 6-7)·경과 시간·진행 레일.
            // 레시피 팝업(불투명 차폐)이 나중에 조립되어 HUD 위를 덮는다
            _hud = PlayHud.Create(_stageRoot);

            _slotRoot = new GameObject("Slots");
            _slotRoot.transform.SetParent(_stageRoot, false);
            StretchRect(_slotRoot);

            _candRoot = new GameObject("Candidates");
            _candRoot.transform.SetParent(_stageRoot, false);
            StretchRect(_candRoot);

            // 차림표 다시 보기 — dwell 3초(UI 버튼)
            var review = CreatePanel(_stageRoot, "ReviewButton", new Color(0.35f, 0.42f, 0.55f));
            var reviewRect = review.rectTransform;
            // 안전 영역(2-4 · x420~) 안으로 — 하단 좌측, 일시정지(중앙)와 나란히
            reviewRect.anchorMin = reviewRect.anchorMax = new Vector2(0.28f, 0.15f);
            reviewRect.sizeDelta = new Vector2(210, 100);
            CreateText(review.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(200, 90), 28, "차림표\n다시 보기");
            var reviewTarget = review.gameObject.AddComponent<DwellTarget>();
            reviewTarget.Selected += _ => _reviewRequested = true;
            _reviewButton = review.gameObject;

            BuildPopup();
        }

        void BuildPopup()
        {
            var popup = CreatePanel(_stageRoot, "RecipePopup", new Color(0.13f, 0.12f, 0.16f, 1f)); // 불투명 차폐 (필수)
            StretchRect(popup.gameObject);
            _popup = popup.gameObject;

            CreateText(_popup.transform, "Caption", new Vector2(0.5f, 0.82f), new Vector2(800, 50), 30, "만들 음식과 재료를 봐 주세요");
            _popupFood = CreateText(_popup.transform, "Food", new Vector2(0.5f, 0.68f), new Vector2(800, 90), 60, "");
            _popupItems = CreateText(_popup.transform, "Items", new Vector2(0.5f, 0.5f), new Vector2(900, 100), 44, "");

            // 공개 시간 게이지 — 버튼 바로 위 (남은 시간 표시 · 끝나도 닫히지 않음)
            var gaugeBg = CreatePanel(_popup.transform, "GaugeBg", new Color(0.3f, 0.3f, 0.35f));
            var gaugeBgRect = gaugeBg.rectTransform;
            gaugeBgRect.anchorMin = gaugeBgRect.anchorMax = new Vector2(0.5f, 0.3f);
            gaugeBgRect.sizeDelta = new Vector2(640, 26);
            var fill = CreatePanel(gaugeBg.transform, "Fill", new Color(0.95f, 0.8f, 0.35f));
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _gaugeFill = fill;

            // 「다 외웠어요」 — 팝업 중앙 하단 · dwell 2초 (확정)
            var btn = CreatePanel(_popup.transform, "Memorized", new Color(0.3f, 0.6f, 0.4f));
            var btnRect = btn.rectTransform;
            btnRect.anchorMin = btnRect.anchorMax = new Vector2(0.5f, 0.18f);
            btnRect.sizeDelta = new Vector2(440, 96);
            CreateText(btn.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(420, 80), 36, "다 외웠어요");
            var btnTarget = btn.gameObject.AddComponent<DwellTarget>();
            btnTarget.SetDwellSeconds(2f);
            btnTarget.Selected += _ => _memorized = true;
            _memorizedButton = btn.gameObject;

            _popup.SetActive(false);
        }

        IEnumerator GameLoop()
        {
            for (int round = 1; round <= 10; round++)
                yield return RunRound(round);

            Debug.Log("[Cooking] ===== 10라운드 종료 =====\n" + string.Join("\n", _records));
            _play.DurationSec = Time.time - _startTime; // timeScale=0 정지로 일시정지 시간은 이미 제외됨
            _play.PausedSec = GamePause.AccumulatedSec;
            _hud.SetGoal("요리를 마쳤어요");
            yield return new WaitForSeconds(1.2f);
            Finished?.Invoke(_play, _records);
        }

        IEnumerator RunRound(int round)
        {
            var recipe = Recipes[round - 1];
            _hud.SetRound(round); // 라운드 표시는 HUD 진행 레일 하나뿐 (중복 배치 금지)
            _hud.SetGoal($"「{recipe.Food}」에 넣을 재료를 골라 주세요"); // 8-6-1 확정 문구

            BuildCandidates(recipe);
            BuildSlots(recipe.Answers.Length);
            _picks.Clear();
            _hinted = null;
            int reviewCount = 0;
            int hintCount = 0;

            // ① 최초 레시피 기억 — 「다 외웠어요」로만 닫힘
            yield return ShowPopup(recipe, firstTime: true);
            float t0 = Time.time;
            _roundT0 = t0;
            _firstPickSec = -1f;
            _nextHintAt = t0 + _hintIntervalSeconds;

            // ② 재료 선택 — 슬롯이 다 차면 종료
            while (_picks.Count < recipe.Answers.Length)
            {
                if (_reviewRequested)
                {
                    _reviewRequested = false;
                    reviewCount++;
                    yield return ShowPopup(recipe, firstTime: false); // 5초 자동 복귀 · 담은 재료 유지
                    _nextHintAt = Time.time + _hintIntervalSeconds;
                }

                if (Time.time >= _nextHintAt)
                {
                    // 미담은 정답 중 첫 번째를 지속 강조 (TTS 없음 · 확정 6-5)
                    ActivateHint(recipe);
                    hintCount++;
                    _nextHintAt = Time.time + _hintIntervalSeconds;
                }
                yield return null;
            }

            float roundTime = Time.time - t0;

            // ③ 일괄 판정 — 색상 계열로만 구분 (부정 기호 금지 · 확정 6-8)
            var parts = new List<string>();
            foreach (var cand in _cands)
            {
                if (!cand.Picked)
                    continue;
                cand.Panel.color = cand.IsAnswer ? JudgeCorrect : JudgeOther;
                parts.Add($"{cand.Item}({(cand.IsAnswer ? "맞음" : "다름")})");
            }
            for (int i = 0; i < _picks.Count; i++)
            {
                bool isAnswer = System.Array.IndexOf(recipe.Answers, _picks[i]) >= 0;
                _slotPanels[i].color = isAnswer ? JudgeCorrect : JudgeOther;
            }

            _records.Add($"R{round}: {recipe.Food} · [{string.Join(", ", parts)}] · {roundTime:F1}s · 다시보기 {reviewCount}회 · 힌트 {hintCount}회");
            Debug.Log($"[Cooking] {_records[_records.Count - 1]}");

            var roundRec = new CookingRoundRecord
            {
                Round = round,
                Food = recipe.Food,
                RoundSec = roundTime,
                ReactionSec = Mathf.Max(0f, _firstPickSec),
                ReviewCount = reviewCount,
                HintCount = hintCount,
            };
            bool allCorrect = true;
            foreach (var pick in _picks)
            {
                bool isAnswer = System.Array.IndexOf(recipe.Answers, pick) >= 0;
                roundRec.Picks.Add(new CookingPick { Item = pick, Correct = isAnswer });
                allCorrect &= isAnswer;
            }
            _play.CookingRounds.Add(roundRec);
            _play.HintCount += hintCount;
            if (allCorrect)
                _play.SuccessCount++; // 모든 재료를 맞게 담은 라운드 (9-1 확정값으로 재검토)

            yield return new WaitForSeconds(1.6f);
            ClearRound();
        }

        IEnumerator ShowPopup(Recipe recipe, bool firstTime)
        {
            _popupOpen = true;
            _candRoot.SetActive(false); // 불투명 차폐 뒤 재료는 보이지도, 선택되지도 않는다
            _reviewButton.SetActive(false);
            _popup.SetActive(true);
            _popupFood.text = recipe.Food;
            _popupItems.text = string.Join("   ", recipe.Answers);
            _memorizedButton.SetActive(firstTime);
            _memorized = false;

            // 공개 시간: 재료 수 비례 (1개 3.5 / 2개 5 / 3개 6.5) · 다시 보기는 5초 자동 복귀
            float duration = firstTime
                ? recipe.Answers.Length == 1 ? 3.5f : recipe.Answers.Length == 2 ? 5f : 6.5f
                : 5f;
            float start = Time.time;
            while (firstTime ? !_memorized : Time.time - start < duration)
            {
                _gaugeFill.fillAmount = Mathf.Clamp01(1f - (Time.time - start) / duration);
                yield return null;
            }

            _popup.SetActive(false);
            _candRoot.SetActive(true);
            _reviewButton.SetActive(true);
            _popupOpen = false;
        }

        void BuildCandidates(Recipe recipe)
        {
            var chosen = new List<string>(recipe.Answers);
            var remaining = new List<string>();
            foreach (var item in ItemPool)
                if (!chosen.Contains(item))
                    remaining.Add(item);

            while (chosen.Count < 6 && remaining.Count > 0)
            {
                int idx = _rng.Next(remaining.Count);
                string item = remaining[idx];
                remaining.RemoveAt(idx);
                // 파와 시금치를 같은 라운드에 함께 배치하지 않는다 (확정)
                if ((item == "파" && chosen.Contains("시금치")) || (item == "시금치" && chosen.Contains("파")))
                    continue;
                chosen.Add(item);
            }
            Shuffle(chosen);

            float[] xs = { 0.36f, 0.5f, 0.64f };
            float[] ys = { 0.52f, 0.29f };
            for (int i = 0; i < 6; i++)
            {
                var panel = CreatePanel(_candRoot.transform, $"Cand_{chosen[i]}", CandNormal);
                var rect = panel.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(xs[i % 3], ys[i / 3]);
                rect.sizeDelta = new Vector2(180, 150);
                // 재료 그림이 있으면 아이콘으로 — 칸 색은 뒤에 남아 판정·힌트 색 표현 유지 (Docs/92)
                if (!ArtCatalog.TryAddIcon(panel, ArtCatalog.Ingredient, chosen[i]))
                    CreateText(panel.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(170, 130), 34, chosen[i]);

                var target = panel.gameObject.AddComponent<DwellTarget>();
                target.SetDwellSeconds(_pickDwellSeconds); // 요리 재료 1~2초 기준안 (C3)
                var cand = new Candidate
                {
                    Item = chosen[i],
                    IsAnswer = System.Array.IndexOf(recipe.Answers, chosen[i]) >= 0,
                    Panel = panel,
                    Target = target,
                    BaseColor = CandNormal,
                };
                target.Selected += _ => OnPick(cand);
                _cands.Add(cand);
            }
        }

        void BuildSlots(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = 0.5f + (i - (count - 1) * 0.5f) * 0.1f;
                var slot = CreatePanel(_slotRoot.transform, $"Slot_{i}", new Color(0.22f, 0.24f, 0.28f));
                var rect = slot.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(x, 0.76f);
                rect.sizeDelta = new Vector2(130, 100);
                _slotPanels.Add(slot);
                _slotTexts.Add(CreateText(slot.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(120, 90), 28, ""));
            }
        }

        void OnPick(Candidate cand)
        {
            if (_popupOpen || cand.Picked || _picks.Count >= _slotPanels.Count)
                return;

            // 담기는 순간에는 정답 여부와 무관하게 담긴다 (확정 6-8-1) · 취소·교체 없음
            cand.Picked = true;
            cand.Panel.color = CandPicked;
            cand.Target.enabled = false;
            if (!ArtCatalog.TryAddIcon(_slotPanels[_picks.Count], ArtCatalog.Ingredient, cand.Item))
                _slotTexts[_picks.Count].text = cand.Item;
            _picks.Add(cand.Item);
            if (_firstPickSec < 0f)
                _firstPickSec = Time.time - _roundT0; // 반응 시간 — 정답 여부 무관, 첫 선택 확정 시점(18-4)

            // 힌트 해제: 힌트 재료면 종료 · 다른 재료면 즉시 취소 후 타이머 재시작 (확정 6-5)
            if (_hinted != null && _hinted != cand)
                ClearHint();
            else if (_hinted == cand)
                _hinted = null;
            _nextHintAt = Time.time + _hintIntervalSeconds;
        }

        void ActivateHint(Recipe recipe)
        {
            ClearHint();
            foreach (var answer in recipe.Answers)
            {
                if (_picks.Contains(answer))
                    continue;
                foreach (var cand in _cands)
                {
                    if (cand.Item == answer && !cand.Picked)
                    {
                        _hinted = cand;
                        cand.Panel.color = CandHint; // 지속 유지 — 점멸 후 사라지지 않는다
                        return;
                    }
                }
            }
        }

        void ClearHint()
        {
            if (_hinted != null && !_hinted.Picked)
                _hinted.Panel.color = _hinted.BaseColor;
            _hinted = null;
        }

        void ClearRound()
        {
            foreach (var cand in _cands)
                if (cand.Panel != null)
                    Destroy(cand.Panel.gameObject);
            _cands.Clear();
            foreach (var slot in _slotPanels)
                if (slot != null)
                    Destroy(slot.gameObject);
            _slotPanels.Clear();
            _slotTexts.Clear();
            _hinted = null;
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ---- UI 헬퍼 ----

        static void StretchRect(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
                rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }

        Image CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize, string content)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = content;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return text;
        }
    }
}
