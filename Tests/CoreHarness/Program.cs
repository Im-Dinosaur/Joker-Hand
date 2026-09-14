using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JokerHand;

internal static class Program
{
    private static int checks;
    private static readonly Joker[] Starter = { Joker.Hold, Joker.Resolve, Joker.Pair };
    private static Card C(int rank, Suit suit = Suit.Clubs) => new Card(rank, suit);
    private static Card[] Parse(string text) => text.Split(' ').Select(s =>
        new Card("23456789TJQKA".IndexOf(s[0]) + 2, (Suit)"cdhs".IndexOf(s[1]))).ToArray();
    private static void Check(bool value, string message)
    {
        checks++; if (!value) throw new Exception(message);
    }
    private static void Throws(Action action, string message)
    {
        try { action(); } catch (ArgumentException) { checks++; return; }
        throw new Exception(message);
    }
    private static ScoreContext Context(string hole, string board, string discard = "", string replacement = "", int opponent = 0)
        => new ScoreContext(Parse(hole), Parse(board), discard.Length == 0 ? Array.Empty<Card>() : Parse(discard),
            replacement.Length == 0 ? Array.Empty<Card>() : Parse(replacement), opponent);

    private static void PokerChecks()
    {
        int[] expectedKeys = { 1277, 2860, 858, 858, 10, 1277, 156, 156, 9, 1 };
        for (int i = 0; i < 10; i++)
        {
            Check(Poker.RankCount((HandCategory)i) == expectedKeys[i], "Rank count " + i);
            if (i < 9) Check(Poker.BasePoints[i] + expectedKeys[i] - 1 < Poker.BasePoints[i + 1], "Category boundaries " + i);
        }
        Check(Poker.EvaluateFive(Parse("Ac 2d 3h 4s 5c")).Category == HandCategory.Straight, "Wheel");
        Check(Poker.EvaluateFive(Parse("Ac 2d 3h 4s 5c")).Points < Poker.EvaluateFive(Parse("2c 3d 4h 5s 6c")).Points, "Wheel is low");
        Check(Poker.EvaluateFive(Parse("Ac Kd Qh Js 2c")).Category == HandCategory.HighCard, "No wrapping straight");
        Check(Poker.EvaluateFive(Parse("Ac Ad 2h 2s 2c")).Points < Poker.EvaluateFive(Parse("Kc Kd Kh 3s 3c")).Points, "Full house trips first");
        Check(Poker.EvaluateFive(Parse("Kc Kd Ah Qs Jc")).Points > Poker.EvaluateFive(Parse("Qc Qd Ah Ks Jc")).Points, "Pair rank before kickers");
        Check(Poker.EvaluateFive(Parse("Kc Kd Ah 8s 2c")).Points > Poker.EvaluateFive(Parse("Kc Kd Qh Js Tc")).Points, "First kicker before lower kickers");
        Check(Poker.EvaluateFive(Parse("Ac Ad 2h 2s 3c")).Points > Poker.EvaluateFive(Parse("Kc Kd Qh Qs Ac")).Points, "Two pair upper pair first");
        Check(Poker.EvaluateFive(Parse("Ac Kc Qc Jc Tc")).Category == HandCategory.RoyalFlush, "Royal separate");
        Check(Poker.EvaluateFive(Parse("Ac Kd Qh Js 9c")).Points == Poker.EvaluateFive(Parse("Ah Ks Qd Jc 9s")).Points, "No suit hierarchy");
        Check(Poker.BestSelections(Parse("Ac Ad Ah As Kc Kd Kh"))[0].Category == HandCategory.Quads, "Seven card best");
        Throws(() => Poker.EvaluateFive(Parse("Ac Ac 2h 3s 4c")), "Duplicate allowed");
        Throws(() => Observation.ValidateLoadout(new[] { Joker.Hold, Joker.Hold, Joker.Pair }), "Duplicate joker allowed");
        // Every physical five-card hand: independently known standard category frequencies.
        var frequency = new int[10];
        var deck = Card.Deck();
        for (int a = 0; a < 48; a++)
        for (int b = a + 1; b < 49; b++)
        for (int c = b + 1; c < 50; c++)
        for (int d = c + 1; d < 51; d++)
        for (int e = d + 1; e < 52; e++)
            frequency[(int)Poker.EvaluateFive(new[] { deck[a], deck[b], deck[c], deck[d], deck[e] }).Category]++;
        Check(frequency.SequenceEqual(new[] { 1302540, 1098240, 123552, 54912, 10200, 5108, 3744, 624, 36, 4 }),
            "Physical hand category counts: " + string.Join(",", frequency));
        Console.WriteLine("PASS: all 2,598,960 physical hands; 7,462 rank keys; poker ordering and boundaries.");
    }
    private static void JokerChecks()
    {
        var held = Context("Ah Kc", "As Kh Qd Jc Ts");
        var hold = Scoring.Calculate(held, Joker.Hold);
        Check(hold.FlatBonus == 8000 && held.Hole.All(c => hold.Hand.Cards.Contains(c)), "Tied best selection must use both private cards");
        Check(Scoring.Calculate(Context("Ah Kc", "As Kh Qd Jc Ts", "2h", "Ah"), Joker.Hold).FlatBonus == 3000, "Hold after exchange");
        var stronger = Scoring.Calculate(Context("2h 2d", "Ac Kc Qc Jc Tc"), Joker.Pair);
        Check(stronger.Hand.Category == HandCategory.RoyalFlush && stronger.Multiplier == 1.3m, "Cannot sacrifice royal for private pair");
        var predicting = Context("2c 3d", "4h 6s 8c Td Ah");
        Check(Scoring.Calculate(predicting, Joker.Prediction).FlatBonus == 2000, "No prediction");
        Check(Scoring.Calculate(predicting, Joker.Prediction, C(14, Suit.Spades)).FlatBonus == 12000, "Rank only");
        Check(Scoring.Calculate(predicting, Joker.Prediction, C(14, Suit.Hearts)).FlatBonus == 112000, "Exact cumulative bonus");
        Check(Scoring.Calculate(predicting, Joker.Prediction, C(13, Suit.Hearts)).FlatBonus == 2000, "Suit only");
        var reading = Context("2c 2d", "4h 4s 8c Td Ah", "3h", "2c", 2);
        Check(Scoring.Calculate(reading, Joker.Read).FlatBonus == 9000, "Read two pair");
        Check(Scoring.Calculate(Context("2c 2d", "4h 6s 8c Td Ah", opponent: 1), Joker.Read).FlatBonus == 4000, "Read pair base");
        Check(Scoring.Calculate(Context("2c 2d", "4h 4s 8c Td Ah", opponent: 0), Joker.Read).FlatBonus == 0, "Read tied count");
        Check(Scoring.Calculate(Context("2c 2d", "4h 4s 8c Td Ah", "Ac Kd", "2c 2d"), Joker.Resolve).FlatBonus == 7000, "Resolve two high discards applies once");
        Check(Scoring.Calculate(Context("2h 3d", "Ac Kc Qc Jc Tc", "Kh", "2h"), Joker.Resolve).FlatBonus == 2000, "Resolve replacement not used");
        Check(Scoring.Calculate(Context("2h 3d", "Ac Kc Qc Jc Tc", "4h", "2h"), Joker.Resolve).FlatBonus == 0, "Resolve low discard");
        var lateFlush = Context("2h 5h", "8h Jh 3c 4d Kh");
        Check(Scoring.Calculate(lateFlush, Joker.Flush).Multiplier == 2m, "River flush");
        Check(Scoring.Calculate(Context("2h 5h", "8h Jh Kh 4d Ac"), Joker.Flush).Multiplier == 1.5m, "Already flush on turn");
        Check(Scoring.Calculate(Context("Ah Kh", "Qh Jh 3c 4d Th"), Joker.Flush).Multiplier == 2m, "River royal flush");
        Check(Scoring.Calculate(Context("Ah Kh", "Qh Jh 3c 4d"), Joker.Flush).Multiplier == 1m, "Unconfirmed future flush excluded from current score");
        var pair = Scoring.Calculate(Context("2c 2d", "4h 4s 8c Td Ah"), Joker.Pair);
        Check(pair.Multiplier == 1.8m, "Pair development");
        Check(pair.Total == (int)decimal.Floor(pair.Hand.Points * 1.8m), "One final floor");
        Check(Scoring.Calculate(Context("2c 3d", "4h 4s 8c Td Ah"), Joker.Pair).Multiplier == 1m, "No private pair");
        var automatic = Context("2h 3d", "Ac Kc Qc Jc");
        Check(Scoring.TimeoutChoice(automatic, new[] { Joker.Pair, Joker.Flush, Joker.Resolve }) == Joker.Pair, "Timeout tie follows candidate order");
        Console.WriteLine("PASS: six jokers, hidden prediction, tied selection, no weaker hand substitution, decimal flooring.");
    }
    private static void MatchChecks()
    {
        var match = new Match(Starter, Starter, 47);
        var original = match.VisibleHole(0, 0);
        match.SelectExchange(0, 1); match.Confirm(1);
        Check(match.PublicDiscards(0).Length == 0 && match.VisibleHole(0, 1).Length == 0, "Private exchange leaked");
        match.Tick(15);
        Check(match.Stage == MatchStage.JokerChoice && !match.Ready(0) && !match.Ready(1), "Timeout leaked into next phase");
        Check(match.PublicDiscards(0).SequenceEqual(new[] { original[0] }), "Selected card timeout exchange");
        Check(match.VisibleHole(0, 0)[1].Equals(original[1]), "Unselected card changed");
        match.SelectJoker(0, Joker.Hold); match.Confirm(0);
        Check(match.RevealedJoker(0) == null && match.VisibleHole(1, 0).Length == 0, "Early joker/hand leak");
        match.SelectJoker(0, Joker.Pair);
        Check(match.OwnSelection(0) == Joker.Hold, "Confirmed choice changed");
        match.Tick(30);
        Check(match.Stage == MatchStage.Reveal && match.Board.Length == 4 && match.VisibleHole(0, 1).Length == 0, "Jokers must precede river and private cards");
        match.Tick(.5); Check(match.Board.Length == 5 && match.VisibleHole(0, 1).Length == 0, "River before private cards");
        match.Tick(.5); Check(match.VisibleHole(0, 1).Length == 2, "Showdown");
        match.Tick(3.9); Check(match.Stage == MatchStage.Reveal, "Winner revealed too early");
        match.Tick(.1); Check(match.Stage == MatchStage.Complete, "Five second reveal");
        var predictionMatch = new Match(new[] { Joker.Prediction, Joker.Flush, Joker.Pair }, Starter, 50);
        predictionMatch.Confirm(0); predictionMatch.Confirm(1);
        var predicted = predictionMatch.Observe(0).UnseenCards()[0];
        predictionMatch.SelectJoker(0, Joker.Prediction); predictionMatch.SelectPrediction(0, predicted); predictionMatch.Confirm(0);
        predictionMatch.Confirm(1);
        Check(predictionMatch.RevealedPrediction(0) == null, "Prediction leaked before scoring");
        predictionMatch.Tick(2.5); Check(predictionMatch.RevealedPrediction(0).Equals(predicted), "Prediction reveal missing");
        for (int seed = 0; seed < 100; seed++)
        {
            var game = new Match(Starter, Starter, seed);
            game.SelectExchange(0, seed % 4); game.SelectExchange(1, (seed / 4) % 4);
            game.Tick(15); game.Tick(30); game.Tick(5);
            var exposed = game.VisibleHole(0, 0).Concat(game.VisibleHole(0, 1)).Concat(game.Board).Concat(game.PublicDiscards(0)).Concat(game.PublicDiscards(1)).ToArray();
            Check(exposed.Distinct().Count() == exposed.Length, "Discard reused or card duplicated, seed " + seed);
            Check(game.Stage == MatchStage.Complete && game.Results.Length == 2, "Full timeout match failed");
        }
        var forfeit = new Match(Starter, Starter, 1); forfeit.Forfeit(0);
        Check(forfeit.Forfeited && forfeit.Winner == 1 && forfeit.Stage == MatchStage.Complete && forfeit.VisibleHole(0, 1).Length == 0, "Forfeit");
        Console.WriteLine("PASS: phase locks, simultaneous reveals, timeouts, forfeit, 100 full matches with no reused discards.");
    }
    private static void AiChecks()
    {
        var ai = new MediumAi();
        var game = new Match(Starter, new[] { Joker.Prediction, Joker.Flush, Joker.Pair }, 88);
        var observation = game.Observe(1);
        var timer = Stopwatch.StartNew();
        var exchange = ai.ChooseExchange(observation, 45);
        long exchangeMs = timer.ElapsedMilliseconds;
        game.SelectExchange(0, 3); game.Confirm(0); // Opponent pending selections must not affect AI inputs.
        var otherView = game.Observe(1);
        Check(otherView.Hole.SequenceEqual(observation.Hole) && otherView.Board.SequenceEqual(observation.Board) && otherView.OpponentDiscards.Count == 0, "Observation leaked pending exchange");
        var repeated = ai.ChooseExchange(otherView, 45);
        Check(exchange.Mask == repeated.Mask && exchange.ExpectedScores.SequenceEqual(repeated.ExpectedScores), "Hidden actions affect AI");
        game.SelectExchange(1, exchange.Mask); game.Confirm(1);
        timer.Restart();
        var choice = ai.ChooseJoker(game.Observe(1));
        long jokerMs = timer.ElapsedMilliseconds;
        Check(game.Candidates(1).Contains(choice.Choice), "AI chose unavailable joker");
        if (choice.Prediction.HasValue) Check(game.Observe(1).UnseenCards().Contains(choice.Prediction.Value), "AI predicted unavailable card");
        game.SelectJoker(1, choice.Choice); game.SelectPrediction(1, choice.Prediction); game.Confirm(1);
        game.Confirm(0); game.Tick(5);
        Check(game.Stage == MatchStage.Complete, "AI-driven match incomplete");
        // Distinguish expected future reward from the current-board timeout policy.
        var draw = new Observation(Parse("Ah Kh"), Parse("Qh Jh 3c 4d"), Array.Empty<Card>(), Array.Empty<Card>(), Array.Empty<Card>(),
            new[] { Joker.Flush, Joker.Hold, Joker.Pair }, Starter);
        var drawChoice = ai.ChooseJoker(draw);
        Check(drawChoice.Choice == Joker.Flush, "Medium AI ignores future flush/royal payoff");
        Check(Scoring.TimeoutChoice(draw.CurrentScore(), draw.Candidates) == Joker.Hold, "Fixture must distinguish AI expectation from timeout");
        var known = new Observation(Parse("Ah Kh"), Parse("Qh Jh 3c 4d"), Parse("2c"), Parse("Th"), Parse("Ah"),
            new[] { Joker.Prediction, Joker.Flush, Joker.Pair }, Starter);
        Check(!known.UnseenCards().Contains(C(10, Suit.Hearts)) && !known.UnseenCards().Contains(C(2)), "AI reuses public discards");
        Console.WriteLine($"PASS: medium AI uses public information, considers future outcomes, and completes a match. Exchange {exchangeMs} ms; joker {jokerMs} ms.");
    }
    private static int Main()
    {
        try
        {
            var timer = Stopwatch.StartNew(); PokerChecks(); JokerChecks(); MatchChecks(); AiChecks();
            Console.WriteLine($"PASS: {checks} assertions in {timer.Elapsed.TotalSeconds:F2}s."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
