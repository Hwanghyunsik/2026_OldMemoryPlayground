using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shinmyeong.Flow;
using Shinmyeong.Interaction;
using Shinmyeong.Save;
using Shinmyeong.UI;

namespace Shinmyeong.Games.Cooking
{
    /// 요리하기 정식 게임 루프 (무대 그림은 시안 SCR-018: 재료 카드 3×2 · 담은 재료 칸 · 레시피 기억 팝업).
    /// 힌트 유효 행동(C4): 재료 선택 확정만 타이머를 리셋한다(3-2 현재 기준) — 손 이동·호버는 리셋 없음.
    /// 04 게임구성표 확정 레시피 10종 고정 순서 · 재료 풀 22종.
    /// 규칙(07 문서 6장):
    ///   레시피 기억(SCR-018-1): 재료 칸을 덮는 팝업 · 공개 게이지(1개 3.5s/2개 5s/3개 6.5s) ·
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
            public RectTransform Root;
            public Image Face;
            public Image Border;
            public Image HintBorder;
            public DwellTarget Target;
        }

        RectTransform _stageRoot;
        PlayHud _hud;
        GameObject _candRoot;
        GameObject _slotRoot;
        readonly List<Candidate> _cands = new List<Candidate>();
        readonly List<Image> _slotFaces = new List<Image>();
        readonly List<Image> _slotIcons = new List<Image>();
        readonly List<Text> _slotTexts = new List<Text>();
        GameObject _reviewButton;

        GameObject _popup;
        Image _popupFood;
        Text _popupFoodName;
        Text _popupHeading;
        RectTransform _popupItems;
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

        // 카드 상태 색 (색상 계열로만 구분 · 6-8)
        static readonly Color FaceNormal = Skin.Face;
        static readonly Color BorderNormal = Skin.Hex("c2b286", 0.5f);
        static readonly Color FacePicked = Skin.Hex("e9e3d5");
        static readonly Color BorderPicked = Skin.Hex("b5a67d");

        public void Begin(RectTransform host)
        {
            End();

            var rootGo = new GameObject("CookingStage");
            rootGo.transform.SetParent(host, false);
            _stageRoot = UiKit.Stretch(rootGo);

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
            _slotFaces.Clear();
            _slotIcons.Clear();
            _slotTexts.Clear();
            if (_stageRoot != null)
            {
                Destroy(_stageRoot.gameObject);
                _stageRoot = null;
            }
        }

        void BuildStage()
        {
            _candRoot = UiKit.Group(_stageRoot, "IngredientCards").gameObject;

            // 담은 재료 칸 — 하단 초록 알약 (x656 y814 610×138)
            var slots = UiKit.Node(_stageRoot, "CollectedIngredients", 656, 814, 610, 138);
            UiKit.Img(slots, "Pill", "Box-Round-38", 0, 0, 610, 138, Skin.Hex("2f5d49"), sliced: true);
            UiKit.Img(slots, "LabelBackground", "Box-Round-15", 52, 38, 157, 62, Skin.Outline2, sliced: true);
            UiKit.Txt(slots, "Label", 52, 38, 157, 62, "담은 재료", 30, 7, Skin.Green);
            _slotRoot = slots.gameObject;

            // 차림표 다시 보기 — 왼쪽 받침 아래 칸 (x63 y589 160×157 · 초록) · dwell 3초(UI 버튼)
            var review = UiKit.Node(_stageRoot, "RecipeButton", 63, 589, 160, 157);
            UiKit.Img(review, "Background", "Bt-Round-Green-02", 0, 0, 157, 157, Color.white, sliced: true);
            UiKit.ImgFit(review, "Icon", "Icon-Record", 60, 25, 36, 38, Color.white);
            UiKit.Txt(review, "Label", 5, 70, 150, 72, "차림표\n다시 보기", 23, 7, Color.white);
            UiKit.Dwell(review, 160, 157, 3f, () => _reviewRequested = true, withText: false)
                .gameObject.AddComponent<SafeAreaExempt>().Reason = "디자인 시안 배치(좌측) — 2-4 안전 영역 밖 · 기획 확인 대기(Q5)";
            _reviewButton = review.gameObject;

            // 공통 HUD — 목표 패널(음식 이름만 · 완성 음식 이미지는 플레이 중 금지 6-7)·경과 시간·진행 레일
            _hud = PlayHud.Create(_stageRoot);

            BuildPopup(); // 팝업이 마지막에 조립되어 재료 칸·버튼 위를 덮는다
        }

        void BuildPopup()
        {
            var popup = UiKit.Node(_stageRoot, "RecipePopup", 343, 237, 1146, 756);
            _popup = popup.gameObject;
            UiKit.Img(popup, "Background", "SCR-018-Popup-Bg", -2, 11, 1150, 733, Color.white, sliced: true);

            // 이번에 준비할 음식 — 음식 그림(팝업에서는 허용 · 6-7) + 이름
            UiKit.Img(popup, "FoodPanel", "Box-Round-23", 70, 121, 660, 198, Skin.Hex("fff8e9"), sliced: true);
            UiKit.ImgFit(popup, "Title", "SCR-018-Popup-Title", 203, 76, 395, 78);
            _popupFood = UiKit.ImgFit(popup, "Food", null, 309, 143, 183, 138);
            UiKit.Img(popup, "FoodNameBackground", "Box-Round-20", 334, 258, 132, 40, Skin.Hex("38210c"), sliced: true);
            _popupFoodName = UiKit.Txt(popup, "FoodName", 334, 257, 132, 42, "", 27, 7, Color.white);

            // 「다 외웠어요」 — dwell 2초 (확정) + 공개 시간 게이지 (끝나도 닫히지 않음)
            var btn = UiKit.Node(popup, "MemorizedButton", 777, 113, 291, 137);
            UiKit.Img(btn, "Background", "Bt-Round-Green-02", 0, 0, 291, 137, Color.white, sliced: true);
            UiKit.ImgFit(btn, "Check", "Icon-Check-02", 118, 24, 54, 44, Color.white);
            UiKit.Txt(btn, "Label", 20, 66, 251, 53, "다 외웠어요", 30, 7, Color.white);
            UiKit.Dwell(btn, 291, 137, 2f, () => _memorized = true, withText: false);
            _memorizedButton = btn.gameObject;
            _gaugeFill = UiKit.Bar(popup, "Progress", 779, 279, 290, 18, Skin.Track3, Skin.Hex("365e49"), "TimeBar", "TimeBar");

            // 필요한 재료 N개 + 점선 상자 안 재료 카드
            var leafL = UiKit.ImgFit(popup, "LeftLeaf", "Icon-Leaf", 244, 392, 42, 40);
            leafL.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // 중심 피벗이라 제자리 뒤집기
            _popupHeading = UiKit.Txt(popup, "IngredientHeading", 286, 386, 243, 55, "", 31, 6, Skin.Hex("39230f"));
            UiKit.ImgFit(popup, "RightLeaf", "Icon-Leaf", 529, 392, 42, 40);
            UiKit.ImgFit(popup, "DashedBorder", "Dot-Line-3-02", 58, 451, 685, 226);
            _popupItems = UiKit.Node(popup, "Foods", 58, 473, 685, 183);

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
                cand.Face.color = cand.IsAnswer ? Skin.ProperCard : Skin.OtherCard;
                cand.Border.color = cand.IsAnswer ? Skin.ProperOutline : Skin.OtherOutline;
                parts.Add($"{cand.Item}({(cand.IsAnswer ? "맞음" : "다름")})");
            }
            for (int i = 0; i < _picks.Count; i++)
            {
                bool isAnswer = System.Array.IndexOf(recipe.Answers, _picks[i]) >= 0;
                _slotFaces[i].color = isAnswer ? Skin.ProperCard : Skin.OtherCard;
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
            _candRoot.SetActive(false); // 차폐 뒤 재료는 보이지도, 선택되지도 않는다
            _reviewButton.SetActive(false);
            _popup.SetActive(true);
            _hud.SetGoal("필요한 재료를 기억해 주세요");

            _popupFoodName.text = recipe.Food;
            var foodArt = ArtCatalog.Get(ArtCatalog.Food, recipe.Food);
            _popupFood.gameObject.SetActive(foodArt != null);
            if (foodArt != null)
                _popupFood.sprite = foodArt;
            _popupHeading.text = $"필요한 재료 {recipe.Answers.Length}개";
            BuildPopupItems(recipe.Answers);
            _memorizedButton.SetActive(firstTime);
            _memorized = false;

            // 공개 시간: 재료 수 비례 (1개 3.5 / 2개 5 / 3개 6.5) · 다시 보기는 5초 자동 복귀
            float duration = firstTime
                ? recipe.Answers.Length == 1 ? 3.5f : recipe.Answers.Length == 2 ? 5f : 6.5f
                : 5f;
            float start = Time.time;
            while (firstTime ? !_memorized : Time.time - start < duration)
            {
                UiKit.SetBar(_gaugeFill, Mathf.Clamp01(1f - (Time.time - start) / duration));
                yield return null;
            }

            _popup.SetActive(false);
            _candRoot.SetActive(true);
            _reviewButton.SetActive(true);
            _popupOpen = false;
            _hud.SetGoal($"「<color=#{ColorUtility.ToHtmlStringRGB(Skin.Green)}>{recipe.Food}</color>」에 넣을 재료를 골라 주세요"); // 8-6-1 확정 문구
        }

        void BuildPopupItems(string[] answers)
        {
            for (int i = _popupItems.childCount - 1; i >= 0; i--)
                Destroy(_popupItems.GetChild(i).gameObject);
            int n = answers.Length;
            float total = n * 190 + (n - 1) * 30;
            float start = (685 - total) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var card = UiKit.Node(_popupItems, $"Food{i + 1}", start + i * 220, 0, 190, 183);
                UiKit.Img(card, "Face", "Box-Round-23", -4, -1, 198, 186, Skin.Cream, sliced: true);
                UiKit.Img(card, "Outline", "Box-Round-23-Outline", -4, -1, 198, 186, Skin.Hex("decba7"), sliced: true);
                var art = ArtCatalog.Get(ArtCatalog.Ingredient, answers[i]);
                if (art != null)
                    UiKit.ImgFit(card, "Food", null, 42, 22, 107, 95).sprite = art;
                UiKit.Txt(card, "Label", 10, 127, 170, 46, answers[i], 30, 7, Skin.Hex("39230f"));
            }
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

            // 3×2 카드 243×243 (시안 IngredientCards · x 560/838/1117 · y 275/534)
            float[] xs = { 560, 838, 1117 };
            float[] ys = { 275, 534 };
            for (int i = 0; i < 6; i++)
            {
                var card = UiKit.Node(_candRoot.transform, $"Cand_{chosen[i]}", xs[i % 3], ys[i / 3], 243, 243);
                UiKit.Img(card, "Frame", "Box-Round-28", 0, 0, 243, 243, Color.white, sliced: true);
                var face = UiKit.Img(card, "Face", "Box-Round-23", 7, 8, 228, 228, FaceNormal, sliced: true);
                var border = UiKit.Img(card, "Border", "Box-Round-23-Outline", 7, 8, 228, 228, BorderNormal, sliced: true);
                var hint = UiKit.Img(card, "HintBorder", "Box-Round-23-Outline-8", 0, 0, 243, 243, Skin.Gold, sliced: true);
                hint.gameObject.SetActive(false);
                var art = ArtCatalog.Get(ArtCatalog.Ingredient, chosen[i]);
                if (art != null)
                    UiKit.ImgFit(card, "Food", null, 45, 27, 153, 150).sprite = art;
                else
                    UiKit.Txt(card, "Placeholder", 20, 40, 203, 130, chosen[i], 34, 6, Skin.Muted);
                UiKit.Txt(card, "Label", 7, 183, 228, 48, chosen[i], 30, 6, Skin.BrownDark);

                var target = UiKit.Dwell(card, 243, 243, _pickDwellSeconds, null); // 요리 재료 1~2초 기준안 (C3)
                var cand = new Candidate
                {
                    Item = chosen[i],
                    IsAnswer = System.Array.IndexOf(recipe.Answers, chosen[i]) >= 0,
                    Root = card,
                    Face = face,
                    Border = border,
                    HintBorder = hint,
                    Target = target,
                };
                target.Selected += _ => OnPick(cand);
                _cands.Add(cand);
            }
        }

        void BuildSlots(int count)
        {
            // 담은 재료 칸: 흰 원(채움) / 점선 원(빈 칸) · 3칸 자리 x 244·367·489
            for (int i = 0; i < count; i++)
            {
                var face = UiKit.Img(_slotRoot.transform, $"Slot{i + 1}", "Dot-Line-Circle", 244 + i * 123, 20, 96, 96, Color.white);
                var icon = UiKit.ImgFit(face.transform, "Food", null, 14, 18, 69, 60);
                icon.gameObject.SetActive(false);
                var text = UiKit.Txt(face.transform, "Label", 0, 0, 96, 96, "", 22, 6, Skin.BrownDark);
                // 담김 반짝임 이펙트는 칸(흰 원) 위 · 재료 그림 뒤 (튜토리얼 영상)
                EffectKit.KeepInFront(icon.gameObject);
                EffectKit.KeepInFront(text.gameObject);
                _slotFaces.Add(face);
                _slotIcons.Add(icon);
                _slotTexts.Add(text);
            }
        }

        void OnPick(Candidate cand)
        {
            if (_popupOpen || cand.Picked || _picks.Count >= _slotFaces.Count)
                return;

            // 담기는 순간에는 정답 여부와 무관하게 담긴다 (확정 6-8-1) · 취소·교체 없음
            cand.Picked = true;
            cand.Face.color = FacePicked;
            cand.Border.color = BorderPicked;
            cand.Target.enabled = false;
            int slot = _picks.Count;
            UiKit.Apply(_slotFaces[slot], "Circle-125");
            var art = ArtCatalog.Get(ArtCatalog.Ingredient, cand.Item);
            if (art != null)
            {
                _slotIcons[slot].sprite = art;
                _slotIcons[slot].gameObject.SetActive(true);
            }
            else
                _slotTexts[slot].text = cand.Item;
            _picks.Add(cand.Item);
            EffectKit.Play(EffectKit.CookingGet, _slotFaces[slot].rectTransform); // 담김 반짝임 (정답 여부 무관 · 확정 6-8-1)
            if (_firstPickSec < 0f)
                _firstPickSec = Time.time - _roundT0; // 반응 시간 — 정답 여부 무관, 첫 선택 확정 시점(18-4)

            // 힌트 해제: 힌트 재료면 종료 · 다른 재료면 즉시 취소 후 타이머 재시작 (확정 6-5)
            if (_hinted != null && _hinted != cand)
                ClearHint();
            else if (_hinted == cand)
            {
                cand.HintBorder.gameObject.SetActive(false);
                _hinted = null;
            }
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
                        cand.HintBorder.gameObject.SetActive(true); // 지속 유지 — 점멸 후 사라지지 않는다
                        return;
                    }
                }
            }
        }

        void ClearHint()
        {
            if (_hinted != null && _hinted.HintBorder != null)
                _hinted.HintBorder.gameObject.SetActive(false);
            _hinted = null;
        }

        void ClearRound()
        {
            foreach (var cand in _cands)
                if (cand.Root != null)
                    Destroy(cand.Root.gameObject);
            _cands.Clear();
            foreach (var slot in _slotFaces)
                if (slot != null)
                    Destroy(slot.gameObject);
            _slotFaces.Clear();
            _slotIcons.Clear();
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
    }
}
