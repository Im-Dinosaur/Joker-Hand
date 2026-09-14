using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace JokerHand
{
    public sealed class Prototype : MonoBehaviour
    {
        private readonly List<Joker> loadout = new List<Joker> { Joker.Hold, Joker.Resolve, Joker.Pair };
        private readonly System.Random seeds = new System.Random();
        private Match match;
        private Task<ExchangeDecision> exchangeTask;
        private Task<JokerDecision> jokerTask;
        private MatchStage scheduledStage;
        private float phaseAge;
        private int pendingRank = 14;
        private int pendingSuit;
        private Vector2 scroll;
        private Font font;
        private GUIStyle title, heading, body, card, button;
        private string aiStatus = "";
        private string lastError = "";
        private bool predictionOpen, resultsOpen;
        private static readonly Rect ConfirmRect = new Rect(790, 820, 220, 80);
        public string ConfirmationLabel => match == null || match.Ready(0) ? null
            : match.Stage == MatchStage.Exchange ? "교체 확정"
            : match.Stage == MatchStage.JokerChoice ? "조커 확정" : null;
        private static readonly Color Green = new Color(.3f, .95f, .6f);
        private static readonly Color Red = new Color(1f, .35f, .4f);

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
            if (Camera.main != null) { Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = new Color(.04f, .08f, .12f); }
        }
        private void StartMatch()
        {
            // Both loadouts are visible. The test opponent cycles through varied, distinct types.
            var aiLoadout = Enum.GetValues(typeof(Joker)).Cast<Joker>().OrderBy(_ => seeds.Next()).Take(3).ToArray();
            match = new Match(loadout, aiLoadout, seeds.Next());
            lastError = ""; predictionOpen = resultsOpen = false; ScheduleAi();
        }
        private void ScheduleAi()
        {
            scheduledStage = match.Stage; phaseAge = 0;
            exchangeTask = null; jokerTask = null;
            var observation = match.Observe(1); // Immutable snapshot containing allowed information only.
            var ai = new MediumAi();
            if (match.Stage == MatchStage.Exchange)
            {
                int seed = seeds.Next();
                exchangeTask = Task.Run(() => ai.ChooseExchange(observation, seed));
            }
            else jokerTask = Task.Run(() => ai.ChooseJoker(observation));
            aiStatus = "중급 AI 판단 중";
        }
        private void Update()
        {
            if (match == null || match.Stage == MatchStage.Complete) return;
            phaseAge += Time.unscaledDeltaTime;
            if ((match.Stage == MatchStage.Exchange || match.Stage == MatchStage.JokerChoice) && match.Stage != scheduledStage) ScheduleAi();
            if (phaseAge >= .8f && !match.Ready(1))
            {
                if (match.Stage == MatchStage.Exchange && exchangeTask != null && exchangeTask.IsCompleted)
                {
                    if (exchangeTask.IsFaulted) ReportError(exchangeTask.Exception);
                    else match.SelectExchange(1, exchangeTask.Result.Mask);
                    match.Confirm(1); aiStatus = "중급 AI 선택 완료"; exchangeTask = null;
                }
                else if (match.Stage == MatchStage.JokerChoice && jokerTask != null && jokerTask.IsCompleted)
                {
                    if (jokerTask.IsFaulted) ReportError(jokerTask.Exception);
                    else
                    {
                        match.SelectJoker(1, jokerTask.Result.Choice);
                        match.SelectPrediction(1, jokerTask.Result.Prediction);
                    }
                    match.Confirm(1); aiStatus = "중급 AI 선택 완료"; jokerTask = null;
                }
            }
            match.Tick(Time.unscaledDeltaTime);
        }
        private void ReportError(Exception error)
        {
            lastError = "AI 계산 오류: 자동 확정 규칙으로 진행했어.";
            Debug.LogException(error);
        }
        private void InitStyles()
        {
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true };
            body.normal.textColor = new Color(.87f, .92f, .96f);
            title = new GUIStyle(body) { fontSize = 30, fontStyle = FontStyle.Bold };
            heading = new GUIStyle(body) { fontSize = 21, fontStyle = FontStyle.Bold };
            card = new GUIStyle(GUI.skin.box) { font = font, fontSize = 26, alignment = TextAnchor.MiddleCenter };
            card.normal.textColor = Color.white;
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17, wordWrap = true, padding = new RectOffset(12, 12, 10, 10) };
        }
        private bool Button(string label, params GUILayoutOption[] options) => GUILayout.Button(label, button, options);
        private void Text(string text) => GUILayout.Label(text, body);
        private static string CardsText(IEnumerable<Card> cards) => string.Join("  ", cards.Select(c => c.ToString()));
        private static string JokerName(Joker joker) => Labels.Jokers[(int)joker];
        private void OnGUI()
        {
            InitStyles();
            if (match != null)
            {
                var previousMatrix = GUI.matrix;
                float fit = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
                GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920 * fit) / 2,
                    (Screen.height - 1080 * fit) / 2, 0), Quaternion.identity, new Vector3(fit, fit, 1));
                DrawTable();
                GUI.matrix = previousMatrix;
                return;
            }
            float scale = Mathf.Max(.3f, Mathf.Min(Screen.width / 1180f, Screen.height / 840f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(20, 12, Screen.width / scale - 40, Screen.height / scale - 24));
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("JOKER HAND  ·  중급 AI 프로토타입", title);
            Text("PC 테스트 · 개인 카드 2장 + 공용 카드 5장 · 조커 후보 3장 중 1장 선택");
            GUILayout.Space(8);
            DrawPreparation();
            if (lastError.Length > 0) Text(lastError);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawPreparation()
        {
            GUILayout.Label("출전 후보 3장을 선택해줘", heading);
            Text("초기 선택은 신규 무료 지급 세트야. 여기서는 테스트 조커 6종을 모두 사용할 수 있어.");
            foreach (Joker joker in Enum.GetValues(typeof(Joker)))
            {
                GUI.backgroundColor = loadout.Contains(joker) ? new Color(.25f, .7f, .5f) : Color.white;
                if (Button((loadout.Contains(joker) ? "✓ " : "") + JokerName(joker) + "  |  " + Labels.Effects[(int)joker], GUILayout.Height(55)))
                {
                    if (loadout.Contains(joker)) loadout.Remove(joker);
                    else if (loadout.Count < 3) loadout.Add(joker);
                }
            }
            GUI.backgroundColor = Color.white;
            Text($"선택 {loadout.Count}/3 · 선택한 순서가 동점 자동 선택의 우선순위야.");
            GUI.enabled = loadout.Count == 3;
            if (Button("중급 AI와 대결 시작", GUILayout.Height(55))) StartMatch();
            GUI.enabled = true;
        }
        private bool DecisionStage => match.Stage == MatchStage.Exchange || match.Stage == MatchStage.JokerChoice;

        private void Panel(Rect rect, string label)
        {
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 14, rect.y + 10, rect.width - 28, 32), label, heading);
        }
        private void ReadyAt(int player, Rect rect)
        {
            if (!DecisionStage) return;
            GUI.contentColor = match.Ready(player) ? Green : Red;
            GUI.Label(rect, match.Ready(player) ? "READY" : "NOT READY", heading);
            GUI.contentColor = Color.white;
        }
        private Card[] HighlightedCards()
        {
            if (match.Forfeited) return Array.Empty<Card>();
            if (DecisionStage)
            {
                // The selected joker can choose a different five-card selection among equally strong poker hands.
                if (match.OwnSelection(0).HasValue)
                    return Scoring.Calculate(match.CurrentContext(0), match.OwnSelection(0).Value).Hand.Cards;
                return Poker.BestSelections(match.VisibleHole(0, 0).Concat(match.Board).ToArray())[0].Cards;
            }
            return match.RevealElapsed >= 1 ? match.Results[0].Hand.Cards : Array.Empty<Card>();
        }
        private void CardAt(Rect rect, Card? value, bool selected, bool highlighted, int exchangeSlot = -1)
        {
            GUI.backgroundColor = selected ? new Color(.95f, .58f, .2f) : highlighted ? new Color(.3f, .8f, .6f) : Color.white;
            string label = value.HasValue ? value.Value.ToString() : "뒷면";
            if (selected) label += "\n교체";
            if (exchangeSlot >= 0 && match.Stage == MatchStage.Exchange)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && !match.Ready(0);
                if (GUI.Button(rect, label, button)) match.SelectExchange(0, match.ExchangeMask(0) ^ (1 << exchangeSlot));
                GUI.enabled = wasEnabled;
            }
            else GUI.Box(rect, label, card);
            GUI.backgroundColor = Color.white;
            if (selected || highlighted)
            {
                Color previous = GUI.color;
                GUI.color = selected ? new Color(1f, .62f, .2f) : Green;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 4, rect.width, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 4, rect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax - 4, rect.y, 4, rect.height), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }
        private void DrawTable()
        {
            // One letterboxed 16:9 canvas follows the user's sketch at every window size.
            bool decision = DecisionStage;
            if (!decision || match.Ready(0)) predictionOpen = false;
            bool modal = predictionOpen || resultsOpen;
            GUI.enabled = !modal;
            GUI.Label(new Rect(30, 12, 600, 48), "JOKER HAND", title);
            GUI.Label(new Rect(680, 20, 900, 36), match.Stage == MatchStage.Exchange ? "1. 개인 카드 교체"
                : match.Stage == MatchStage.JokerChoice ? "2. 조커 선택" : "3. 결과 공개", heading);
            if (decision && GUI.Button(new Rect(1780, 15, 110, 42), "기권", button)) match.Forfeit(0);

            DrawCandidates(1, new Rect(320, 75, 770, 210));
            Panel(new Rect(1140, 75, 420, 210), "상대 개인 카드");
            ReadyAt(1, new Rect(1390, 85, 160, 32));
            var other = match.VisibleHole(0, 1);
            for (int i = 0; i < 2; i++)
                CardAt(new Rect(1200 + i * 165, 122, 140, 150), other.Length == 2 ? other[i] : (Card?)null, false, false);

            var highlighted = HighlightedCards();
            Panel(new Rect(320, 355, 1240, 310), "공용 카드");
            var board = match.Board;
            for (int i = 0; i < 5; i++)
            {
                var rect = new Rect(355 + i * 237, 407, 218, 235);
                if (i < board.Length) CardAt(rect, board[i], false, highlighted.Contains(board[i]));
                else GUI.Box(rect, (i + 1) + "\n공개 대기", card);
            }
            DrawStatus();
            Panel(new Rect(1620, 355, 270, 310), "버린 패");
            GUI.Label(new Rect(1640, 415, 230, 80), match.Stage == MatchStage.Exchange
                ? "양쪽 교체 확정 후\n동시에 공개" : "나\n" + DiscardsText(0), body);
            if (match.Stage != MatchStage.Exchange)
                GUI.Label(new Rect(1640, 520, 230, 100), "상대\n" + DiscardsText(1), body);

            Panel(new Rect(240, 710, 510, 290), "내 개인 카드");
            ReadyAt(0, new Rect(565, 722, 170, 32));
            var own = match.VisibleHole(0, 0);
            for (int i = 0; i < own.Length; i++)
                CardAt(new Rect(300 + i * 210, 762, 175, 220), own[i],
                    match.Stage == MatchStage.Exchange && (match.ExchangeMask(0) & (1 << i)) != 0,
                    highlighted.Contains(own[i]), i);
            DrawCandidates(0, new Rect(1040, 710, 850, 290));

            // Confirmation disappears as soon as the local selection locks, even while the opponent is choosing.
            string confirm = ConfirmationLabel;
            if (confirm != null)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && (match.Stage == MatchStage.Exchange || match.OwnSelection(0).HasValue);
                if (GUI.Button(ConfirmRect, confirm, button)) match.Confirm(0);
                GUI.enabled = wasEnabled;
            }
            else if (DecisionStage)
                GUI.Label(new Rect(790, 828, 220, 80), "상대 선택을\n기다리는 중", body);

            if (match.Stage == MatchStage.JokerChoice && match.OwnSelection(0) == Joker.Prediction)
            {
                string prediction = "예측: " + (match.OwnPrediction(0)?.ToString() ?? "없음");
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && !match.Ready(0);
                if (GUI.Button(new Rect(1130, 668, 650, 38), prediction + " · 숫자 / 무늬 지정", button)) predictionOpen = true;
                GUI.enabled = wasEnabled;
            }
            if (!DecisionStage && !match.Forfeited)
            {
                DrawScoreRibbon(1, new Rect(320, 289, 1240, 62));
                DrawScoreRibbon(0, new Rect(240, 1008, 1650, 64));
            }
            else if (DecisionStage)
                GUI.Label(new Rect(240, 1014, 1650, 50), match.Stage == MatchStage.Exchange
                    ? "교체할 카드를 탭해 선택 / 해제 · 0~2장 한 번 교체 · 주황색: 교체 대상 · 초록색: 현재 족보 구성"
                    : "현재 점수는 마지막 카드에 따라 달라져. 선택하지 않고 시간이 끝나면 현재 점수가 가장 높은 조커로 자동 확정돼.", body);

            if (match.Stage == MatchStage.Complete)
            {
                if (!match.Forfeited && GUI.Button(new Rect(790, 710, 220, 56), "점수 상세", button)) resultsOpen = true;
                if (GUI.Button(new Rect(790, 930, 220, 60), "준비 화면으로", button)) match = null;
            }
            GUI.enabled = true;
            if (match == null) return;
            if (predictionOpen || resultsOpen) DrawModal();
            if (lastError.Length > 0) GUI.Label(new Rect(30, 62, 1800, 34), lastError, body);
        }
        private string DiscardsText(int player)
        {
            var cards = match.PublicDiscards(player);
            return cards.Length == 0 ? "없음" : CardsText(cards);
        }
        private void DrawStatus()
        {
            Panel(new Rect(20, 355, 270, 310), "현재 족보 · 점수");
            if (match.Forfeited)
            {
                GUI.Label(new Rect(38, 410, 235, 225), "기권 패배\n보상 없음", heading);
                return;
            }
            if (DecisionStage)
            {
                var context = match.CurrentContext(0);
                var hand = Poker.BestSelections(match.VisibleHole(0, 0).Concat(match.Board).ToArray())[0];
                string score = hand.Points.ToString("N0") + "점\n조커 적용 전";
                if (match.OwnSelection(0).HasValue)
                    score = Scoring.Calculate(context, match.OwnSelection(0).Value).Total.ToString("N0") + "점\n선택 조커 적용";
                GUI.Label(new Rect(38, 410, 235, 120), Labels.Hands[(int)hand.Category] + "\n" + score, heading);
                GUI.Label(new Rect(38, 555, 235, 40), Math.Ceiling(match.SecondsLeft) + "초 남음", heading);
                GUI.Label(new Rect(38, 608, 235, 46), aiStatus, body);
            }
            else
            {
                // Do not expose the final score before its animation stage.
                string text = match.RevealElapsed < 1 ? "쇼다운 진행 중"
                    : Labels.Hands[(int)match.Results[0].Hand.Category];
                if (match.RevealElapsed >= 3.5) text += "\n" + (match.Results[0].ExactPrediction && match.RevealElapsed < 4.2
                    ? (match.Results[0].Hand.Points + 12000).ToString("N0") : match.Results[0].Total.ToString("N0")) + "점";
                if (match.Stage == MatchStage.Complete) text += "\n\n" + (match.Winner < 0 ? "무승부" : match.Winner == 0 ? "승리!" : "패배");
                GUI.Label(new Rect(38, 413, 235, 225), text, heading);
            }
        }
        private void DrawCandidates(int player, Rect area)
        {
            Panel(area, player == 0 ? "내 조커 후보" : "상대 조커 후보");
            var candidates = match.Candidates(player);
            var revealed = match.RevealedJoker(player);
            for (int i = 0; i < candidates.Length; i++)
            {
                var joker = candidates[i];
                bool selected = player == 0 ? match.OwnSelection(0) == joker : revealed == joker;
                float width = (area.width - 48) / 3;
                var rect = new Rect(area.x + 12 + i * (width + 12), area.y + 48, width, area.height - 60);
                GUI.backgroundColor = selected ? new Color(.25f, .7f, .5f) : Color.white;
                string label = (selected ? "✓ " : "") + JokerName(joker);
                bool preview = player == 0 && match.Stage == MatchStage.JokerChoice;
                if (preview)
                {
                    GUI.Label(new Rect(rect.x, rect.y, rect.width, 38),
                        Scoring.Calculate(match.CurrentContext(0), joker).Total.ToString("N0") + "점", heading);
                    rect.y += 42; rect.height -= 42;
                }
                label += "\n" + Labels.Effects[(int)joker];
                bool canChoose = player == 0 && match.Stage == MatchStage.JokerChoice && !match.Ready(0);
                GUI.Box(rect, label, new GUIStyle(button) { alignment = TextAnchor.MiddleCenter });
                if (preview && !selected)
                {
                    Color old = GUI.color;
                    GUI.color = new Color(.45f, .45f, .45f, .52f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = old;
                }
                if (canChoose && GUI.Button(rect, GUIContent.none, GUIStyle.none)) match.SelectJoker(0, joker);
                GUI.backgroundColor = Color.white;
            }
        }
        private void DrawScoreRibbon(int player, Rect rect)
        {
            double t = match.RevealElapsed;
            var r = match.Results[player];
            string text = (player == 0 ? "나" : "상대") + " · " + JokerName(match.RevealedJoker(player).Value);
            if (t < .5) text += " · 마지막 공용 카드 공개 대기";
            else if (t < 1) text += " · 개인 카드 동시 공개 대기";
            else
            {
                text += " · " + Labels.Hands[(int)r.Hand.Category];
                if (t >= 1.5) text += $" · 족보 {r.Hand.BasePoints:N0} + 숫자 {r.Hand.RankBonus:N0} = {r.Hand.Points:N0}";
                if (t >= 2.5) text += $"\n기본 효과 +{r.BaseFlatBonus:N0} / ×{r.BaseMultiplier} → {r.AfterBaseEffect:N0}점";
                if (t >= 2.5 && match.RevealedJoker(player) == Joker.Prediction)
                    text += " · 예측 " + (match.RevealedPrediction(player)?.ToString() ?? "없음") + " / 실제 " + match.Board[4];
                if (t >= 3.5) text += r.ExactPrediction && t < 4.2
                    ? $" · 숫자 적중 +10,000 → {r.Hand.Points + 12000:N0}점"
                    : $" · 총 가산 +{r.FlatBonus:N0} / ×{r.Multiplier} → {r.Total:N0}점" + (r.ExactPrediction ? " 완전 적중!" : "");
            }
            GUI.Label(rect, text, body);
        }
        private void DrawModal()
        {
            GUI.Box(new Rect(0, 0, 1920, 1080), GUIContent.none);
            var rect = resultsOpen ? new Rect(260, 190, 1400, 700) : new Rect(360, 300, 1200, 390);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 25, rect.y + 20, rect.width - 50, rect.height - 40));
            GUILayout.Label(resultsOpen ? "최종 점수 상세" : "마지막 공용 카드 예측", heading);
            if (Button("닫기", GUILayout.Width(100))) { predictionOpen = resultsOpen = false; }
            if (resultsOpen)
            {
                scroll = GUILayout.BeginScrollView(scroll);
                DrawResults();
                GUILayout.EndScrollView();
            }
            else if (predictionOpen) DrawPrediction();
            GUILayout.EndArea();
        }
        private void DrawPrediction()
        {
            GUI.enabled = !match.Ready(0);
            pendingRank = GUILayout.SelectionGrid(pendingRank - 2, Enumerable.Range(2, 13).Select(Card.RankText).ToArray(), 13, button) + 2;
            pendingSuit = GUILayout.SelectionGrid(pendingSuit, new[] { "♣", "♦", "♥", "♠" }, 4, button);
            var proposed = new Card(pendingRank, (Suit)pendingSuit);
            bool legal = match.Observe(0).UnseenCards().Contains(proposed);
            GUILayout.BeginHorizontal();
            GUI.enabled = !match.Ready(0) && legal;
            if (Button(proposed + " 예측으로 지정")) match.SelectPrediction(0, proposed);
            GUI.enabled = !match.Ready(0);
            if (Button("예측 안 함")) match.SelectPrediction(0, null);
            GUI.enabled = true; GUILayout.EndHorizontal();
            Text("지정한 예측: " + (match.OwnPrediction(0)?.ToString() ?? "없음 — 기본 보너스만 적용") + (legal ? "" : " · 이미 알려진 카드는 지정할 수 없어"));
        }
        private void DrawResults()
        {
            double t = match.RevealElapsed;
            if (t < 1)
            {
                Text("선택 조커 동시 공개  ·  나: " + JokerName(match.RevealedJoker(0).Value) + " / 상대: " + JokerName(match.RevealedJoker(1).Value));
                Text(t < .5 ? "마지막 공용 카드를 공개할게…" : "이제 두 플레이어의 패를 공개할게…");
                return;
            }
            for (int p = 0; p < 2; p++)
            {
                var r = match.Results[p];
                GUILayout.Label((p == 0 ? "나" : "상대") + " · " + Labels.Hands[(int)r.Hand.Category] + " · " + JokerName(match.RevealedJoker(p).Value), heading);
                Text("최종 5장: " + CardsText(r.Hand.Cards));
                if (t >= 1.5) Text($"족보 {r.Hand.BasePoints:N0} + 숫자 보정 {r.Hand.RankBonus:N0} = {r.Hand.Points:N0}");
                if (t >= 2.5)
                {
                    var j = match.RevealedJoker(p).Value;
                    Text("조커 기본 조건: " + (r.BaseTriggered ? "충족" : "미충족") + $" · +{r.BaseFlatBonus:N0} / ×{r.BaseMultiplier} → {r.AfterBaseEffect:N0}점");
                    if (j == Joker.Prediction) Text("예측: " + (match.RevealedPrediction(p)?.ToString() ?? "없음") + " / 실제: " + match.Board[4]);
                }
                if (t >= 3.5)
                {
                    if (r.ExactPrediction && t < 4.2) Text($"숫자 적중! +10,000 → {r.Hand.Points + 12000:N0}점");
                    else Text($"추가 조건: {(r.ExtraTriggered ? "충족" : "미충족")} · 조커 총 가산 +{r.FlatBonus:N0} · 최종 배율 ×{r.Multiplier} → {r.Total:N0}점" + (r.ExactPrediction ? "  완전 적중!" : ""));
                }
            }
            if (match.Stage == MatchStage.Complete)
            {
                GUILayout.Label(match.Winner == -1 ? "무승부" : match.Winner == 0 ? "승리!" : "패배", title);
                Text("프로토타입에서는 재화·강화·랭킹을 처리하지 않아.");
                if (Button("준비 화면으로", GUILayout.Height(48))) match = null;
            }
        }
    }
}
