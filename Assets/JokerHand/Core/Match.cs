using System;
using System.Collections.Generic;
using System.Linq;

namespace JokerHand
{
    public enum MatchStage { Exchange, JokerChoice, Reveal, Complete }

    public sealed class Match
    {
        private readonly Card[] deck;
        private int cursor;
        private readonly Card[][] holes = { new Card[2], new Card[2] };
        private readonly List<Card>[] discards = { new List<Card>(), new List<Card>() };
        private readonly List<Card>[] replacements = { new List<Card>(), new List<Card>() };
        private readonly List<Card> board = new List<Card>();
        private readonly Joker[][] candidates;
        private readonly bool[] ready = new bool[2];
        private readonly int[] exchangeMasks = new int[2];
        private readonly Joker?[] selected = new Joker?[2];
        private readonly Card?[] predictions = new Card?[2];
        public MatchStage Stage { get; private set; } = MatchStage.Exchange;
        public double SecondsLeft { get; private set; } = 15;
        public double RevealElapsed { get; private set; }
        public bool Forfeited { get; private set; }
        public ScoreResult[] Results { get; private set; }
        public int Winner { get; private set; } = -1;
        public Card[] Board => Stage == MatchStage.Reveal && RevealElapsed < .5 ? board.Take(4).ToArray() : board.ToArray();
        public Joker[] Candidates(int player) => (Joker[])candidates[player].Clone();
        public bool Ready(int player) => ready[player];
        public int ExchangeMask(int player) => exchangeMasks[player];
        public Joker? OwnSelection(int player) => selected[player];
        public Card? OwnPrediction(int player) => predictions[player];
        public Joker? RevealedJoker(int player) => Stage >= MatchStage.Reveal && !Forfeited ? selected[player] : null;
        public Card[] PublicDiscards(int player) => Stage == MatchStage.Exchange ? Array.Empty<Card>() : discards[player].ToArray();
        public Card[] VisibleHole(int viewer, int player) => viewer == player || (!Forfeited && (Stage == MatchStage.Complete || Stage == MatchStage.Reveal && RevealElapsed >= 1))
            ? (Card[])holes[player].Clone() : Array.Empty<Card>();
        public Card? RevealedPrediction(int player) => !Forfeited && (Stage == MatchStage.Complete || RevealElapsed >= 2.5) ? predictions[player] : null;

        public Match(IEnumerable<Joker> playerCandidates, IEnumerable<Joker> aiCandidates, int shuffleSeed)
        {
            candidates = new[] { playerCandidates.ToArray(), aiCandidates.ToArray() };
            foreach (var loadout in candidates) Observation.ValidateLoadout(loadout);
            deck = Card.Deck(); MediumAi.Shuffle(deck, new Random(shuffleSeed));
            for (int slot = 0; slot < 2; slot++) for (int p = 0; p < 2; p++) holes[p][slot] = Draw();
            for (int i = 0; i < 3; i++) board.Add(Draw());
        }
        private Card Draw() => deck[cursor++];
        public Observation Observe(int player)
        {
            if (Stage != MatchStage.Exchange && Stage != MatchStage.JokerChoice) throw new InvalidOperationException("No decision is pending.");
            return new Observation(holes[player], board, PublicDiscards(player), PublicDiscards(1 - player), replacements[player], candidates[player], candidates[1 - player]);
        }
        public ScoreContext CurrentContext(int player) => new ScoreContext(holes[player], board, discards[player], replacements[player], discards[1 - player].Count);
        public void SelectExchange(int player, int mask)
        {
            if (Stage != MatchStage.Exchange || ready[player]) return;
            if (mask < 0 || mask > 3) throw new ArgumentOutOfRangeException(nameof(mask));
            exchangeMasks[player] = mask;
        }
        public void SelectJoker(int player, Joker joker)
        {
            if (Stage != MatchStage.JokerChoice || ready[player]) return;
            if (!candidates[player].Contains(joker)) throw new ArgumentException("Joker is not in the loadout.");
            selected[player] = joker;
        }
        public void SelectPrediction(int player, Card? card)
        {
            if (Stage != MatchStage.JokerChoice || ready[player]) return;
            if (card.HasValue && !Observe(player).UnseenCards().Contains(card.Value)) throw new ArgumentException("Known cards cannot be predicted.");
            predictions[player] = card;
        }
        public void Confirm(int player)
        {
            if ((Stage != MatchStage.Exchange && Stage != MatchStage.JokerChoice) || ready[player]) return;
            if (Stage == MatchStage.JokerChoice && !selected[player].HasValue)
                selected[player] = Scoring.TimeoutChoice(CurrentContext(player), candidates[player]);
            ready[player] = true;
            if (ready.All(value => value)) AdvanceStage();
        }
        private void AdvanceStage()
        {
            if (Stage == MatchStage.Exchange)
            {
                // No discards or replacements are exposed until both selections are locked.
                for (int p = 0; p < 2; p++)
                for (int slot = 0; slot < 2; slot++)
                {
                    if ((exchangeMasks[p] & (1 << slot)) == 0) continue;
                    discards[p].Add(holes[p][slot]); holes[p][slot] = Draw(); replacements[p].Add(holes[p][slot]);
                }
                board.Add(Draw()); Stage = MatchStage.JokerChoice; SecondsLeft = 30;
                ready[0] = ready[1] = false;
            }
            else
            {
                board.Add(Draw()); Stage = MatchStage.Reveal; SecondsLeft = 0;
                Results = Enumerable.Range(0, 2).Select(p => Scoring.Calculate(CurrentContext(p), selected[p].Value,
                    selected[p] == Joker.Prediction ? predictions[p] : null)).ToArray();
            }
        }
        public void Tick(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (Stage == MatchStage.Complete) return;
            if (Stage == MatchStage.Reveal)
            {
                RevealElapsed += seconds;
                if (RevealElapsed >= 5)
                {
                    Winner = Results[0].Total == Results[1].Total ? -1 : Results[0].Total > Results[1].Total ? 0 : 1;
                    Stage = MatchStage.Complete;
                }
                return;
            }
            SecondsLeft = Math.Max(0, SecondsLeft - seconds);
            if (SecondsLeft <= 0)
            {
                // Snapshot pending players, so advancing a phase cannot auto-confirm the next one.
                var pending = Enumerable.Range(0, 2).Where(p => !ready[p]).ToArray();
                foreach (int player in pending) Confirm(player);
            }
        }
        public void Forfeit(int player)
        {
            if (Stage != MatchStage.Exchange && Stage != MatchStage.JokerChoice) return;
            Forfeited = true; Winner = 1 - player; Stage = MatchStage.Complete;
        }
    }
}
