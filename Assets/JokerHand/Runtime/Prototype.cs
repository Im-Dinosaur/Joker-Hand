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
            lastError = ""; ScheduleAi();
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
            float scale = Mathf.Max(.3f, Mathf.Min(Screen.width / 1180f, Screen.height / 840f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(20, 12, Screen.width / scale - 40, Screen.height / scale - 24));
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("JOKER HAND  ·  중급 AI 프로토타입", title);
            Text("PC 테스트 · 개인 카드 2장 + 공용 카드 5장 · 조커 후보 3장 중 1장 선택");
            GUILayout.Space(8);
            if (match == null) DrawPreparation(); else DrawMatch();
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
        private void DrawReady(int player, string name)
        {
            GUI.contentColor = match.Ready(player) ? Green : Red;
            GUILayout.Label(name + (match.Ready(player) ? "  READY" : "  NOT READY"), heading);
            GUI.contentColor = Color.white;
        }
        private void DrawMatch()
        {
            bool decision = match.Stage == MatchStage.Exchange || match.Stage == MatchStage.JokerChoice;
            GUILayout.BeginHorizontal();
            GUILayout.Label(match.Stage == MatchStage.Exchange ? "1. 개인 카드 교체" : match.Stage == MatchStage.JokerChoice ? "2. 조커 선택" : "3. 결과 공개", heading);
            if (decision)
            {
                GUILayout.Label($"남은 시간 {Math.Ceiling(match.SecondsLeft)}초", heading);
                if (Button("기권", GUILayout.Width(90))) match.Forfeit(0);
            }
            GUILayout.EndHorizontal();
            Text("AI 후보: " + string.Join(" / ", match.Candidates(1).Select(JokerName)));
            foreach (var j in match.Candidates(1)) Text("  " + JokerName(j) + ": " + Labels.Effects[(int)j]);
            if (decision) { DrawReady(1, "상대"); Text(aiStatus); }
            DrawCards(match.VisibleHole(0, 1), "상대 개인 카드", true);
            DrawCards(match.Board, "공용 카드", false);
            if (match.Stage != MatchStage.Exchange)
                Text("버린 카드  ·  나: " + CardsText(match.PublicDiscards(0)) + "  /  상대: " + CardsText(match.PublicDiscards(1)));
            if (match.Forfeited)
            {
                Text("기권 처리 · 보상 없음");
                if (Button("준비 화면으로")) match = null;
                return;
            }
            if (decision) DrawReady(0, "나");
            DrawCards(match.VisibleHole(0, 0), "내 개인 카드", false, match.Stage == MatchStage.Exchange);
            if (match.Stage == MatchStage.Exchange) DrawExchange();
            else if (match.Stage == MatchStage.JokerChoice) DrawJokerChoice();
            else DrawResults();
        }
        private void DrawCards(Card[] cards, string label, bool hidden, bool selectable = false)
        {
            Card[] highlighted = Array.Empty<Card>();
            if (!hidden && !match.Forfeited)
            {
                if (match.Stage == MatchStage.Exchange || match.Stage == MatchStage.JokerChoice)
                    highlighted = Scoring.Calculate(match.CurrentContext(0), match.OwnSelection(0) ?? match.Candidates(0)[0]).Hand.Cards;
                else if (match.RevealElapsed >= 1) highlighted = match.Results[0].Hand.Cards;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, body, GUILayout.Width(140));
            if (hidden && cards.Length == 0) GUILayout.Box("?     ?", card, GUILayout.Width(200), GUILayout.Height(54));
            for (int i = 0; i < cards.Length; i++)
            {
                bool chosen = selectable && (match.ExchangeMask(0) & (1 << i)) != 0;
                GUI.backgroundColor = chosen ? new Color(.9f, .55f, .2f) : highlighted.Contains(cards[i]) ? new Color(.3f, .8f, .6f) : Color.white;
                if (selectable)
                {
                    GUI.enabled = !match.Ready(0);
                    if (Button(cards[i] + (chosen ? " 교체" : ""), GUILayout.Width(125), GUILayout.Height(54)))
                        match.SelectExchange(0, match.ExchangeMask(0) ^ (1 << i));
                    GUI.enabled = true;
                }
                else GUILayout.Box(cards[i].ToString(), card, GUILayout.Width(90), GUILayout.Height(54));
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }
        private void DrawExchange()
        {
            var hand = Poker.BestSelections(match.VisibleHole(0, 0).Concat(match.Board).ToArray())[0];
            Text($"현재 족보: {Labels.Hands[(int)hand.Category]}  ·  {CardsText(hand.Cards)}  ·  {hand.Points:N0}점 (조커 적용 전)");
            Text("초록색은 현재 족보에 쓰이는 카드야. 교체할 카드를 탭해줘. 0~2장, 한 번만 교체하며 시간이 끝나면 현재 선택대로 확정돼.");
            Text("내 후보: " + string.Join(" / ", match.Candidates(0).Select(JokerName)));
            GUI.enabled = !match.Ready(0);
            if (Button("교체 선택 확정", GUILayout.Height(48))) match.Confirm(0);
            GUI.enabled = true;
        }
        private void DrawJokerChoice()
        {
            var context = match.CurrentContext(0);
            Text("현재 기준 점수야. 5번째 카드에 따라 족보와 조건이 달라질 수 있어.");
            foreach (var joker in match.Candidates(0))
            {
                var score = Scoring.Calculate(context, joker);
                bool selected = match.OwnSelection(0) == joker;
                GUI.backgroundColor = selected ? new Color(.25f, .7f, .5f) : Color.white;
                GUI.enabled = !match.Ready(0);
                string future = joker == Joker.Prediction || joker == Joker.Flush ? " · 마지막 카드 조건 미확정" : " · 최종 패에서 재판정";
                if (Button((selected ? "✓ " : "") + JokerName(joker) + $"  {score.Total:N0}점" + future + "\n" + Labels.Effects[(int)joker], GUILayout.Height(58)))
                    match.SelectJoker(0, joker);
                GUI.enabled = true;
            }
            GUI.backgroundColor = Color.white;
            var current = Scoring.Calculate(context, match.OwnSelection(0) ?? match.Candidates(0)[0]);
            Text("현재 족보: " + Labels.Hands[(int)current.Hand.Category] + "  ·  " + CardsText(current.Hand.Cards));
            if (match.OwnSelection(0) == Joker.Prediction) DrawPrediction();
            Text("선택하지 않고 시간이 끝나면 현재 점수가 가장 높은 후보가 자동 확정돼. 동점이면 후보 순서대로 선택해.");
            GUI.enabled = !match.Ready(0) && match.OwnSelection(0).HasValue;
            if (Button("조커 선택 확정", GUILayout.Height(48))) match.Confirm(0);
            GUI.enabled = true;
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
