using System;
using System.Collections.Generic;
using System.Linq;

namespace JokerHand
{
    public sealed class ScoreContext
    {
        public readonly Card[] Hole;
        public readonly Card[] Board;
        public readonly Card[] Discards;
        public readonly Card[] Replacements;
        public readonly int OpponentExchangeCount;
        public ScoreContext(IEnumerable<Card> hole, IEnumerable<Card> board, IEnumerable<Card> discards,
            IEnumerable<Card> replacements, int opponentExchangeCount)
        {
            Hole = hole.ToArray(); Board = board.ToArray(); Discards = discards.ToArray(); Replacements = replacements.ToArray();
            OpponentExchangeCount = opponentExchangeCount;
            if (Hole.Length != 2 || Board.Length < 3 || Board.Length > 5 || Discards.Length > 2 ||
                Replacements.Length != Discards.Length || opponentExchangeCount < 0 || opponentExchangeCount > 2)
                throw new ArgumentException("Invalid score context.");
            Card.ValidateDistinct(Hole.Concat(Board).Concat(Discards));
            if (Replacements.Distinct().Count() != Replacements.Length || Replacements.Any(c => !Hole.Contains(c)))
                throw new ArgumentException("Replacements must be distinct cards in the current hand.");
        }
    }

    public sealed class ScoreResult
    {
        public Hand Hand { get; internal set; }
        public bool BaseTriggered { get; internal set; }
        public bool ExtraTriggered { get; internal set; }
        public bool ExactPrediction { get; internal set; }
        public int FlatBonus { get; internal set; }
        public int BaseFlatBonus { get; internal set; }
        public decimal BaseMultiplier { get; internal set; } = 1m;
        public decimal Multiplier { get; internal set; } = 1m;
        public int AfterBaseEffect => (int)decimal.Floor((Hand.Points + BaseFlatBonus) * BaseMultiplier);
        public int Total => (int)decimal.Floor((Hand.Points + FlatBonus) * Multiplier);
    }

    public static class Scoring
    {
        public static ScoreResult Calculate(ScoreContext context, Joker joker, Card? prediction = null)
            => Calculate(context, joker, prediction, Poker.BestSelections(context.Hole.Concat(context.Board).ToArray()));

        internal static ScoreResult Calculate(ScoreContext c, Joker joker, Card? prediction, List<Hand> selections)
        {
            ScoreResult best = null;
            foreach (var hand in selections)
            {
                var r = new ScoreResult { Hand = hand };
                bool both = c.Hole.All(card => hand.Cards.Contains(card));
                switch (joker)
                {
                    case Joker.Hold:
                        r.BaseTriggered = both; r.ExtraTriggered = both && c.Discards.Length == 0;
                        r.BaseFlatBonus = r.BaseTriggered ? 3000 : 0;
                        r.FlatBonus = r.BaseFlatBonus + (r.ExtraTriggered ? 5000 : 0); break;
                    case Joker.Prediction:
                        r.BaseTriggered = true;
                        r.ExtraTriggered = prediction.HasValue && c.Board.Length == 5 && prediction.Value.Rank == c.Board[4].Rank;
                        r.ExactPrediction = r.ExtraTriggered && prediction.Value.Equals(c.Board[4]);
                        r.BaseFlatBonus = 2000;
                        r.FlatBonus = r.BaseFlatBonus + (r.ExtraTriggered ? 10000 : 0) + (r.ExactPrediction ? 100000 : 0); break;
                    case Joker.Read:
                        r.BaseTriggered = c.Discards.Length < c.OpponentExchangeCount;
                        r.ExtraTriggered = r.BaseTriggered && hand.Category >= HandCategory.TwoPair;
                        r.BaseFlatBonus = r.BaseTriggered ? 4000 : 0;
                        r.FlatBonus = r.BaseFlatBonus + (r.ExtraTriggered ? 5000 : 0); break;
                    case Joker.Resolve:
                        r.BaseTriggered = c.Discards.Any(card => card.Rank >= 11);
                        r.ExtraTriggered = r.BaseTriggered && c.Replacements.Any(card => hand.Cards.Contains(card));
                        r.BaseFlatBonus = r.BaseTriggered ? 2000 : 0;
                        r.FlatBonus = r.BaseFlatBonus + (r.ExtraTriggered ? 5000 : 0); break;
                    case Joker.Flush:
                        r.BaseTriggered = hand.Category == HandCategory.Flush || hand.Category >= HandCategory.StraightFlush;
                        r.ExtraTriggered = r.BaseTriggered && c.Board.Length == 5 && !Poker.HasFlush(c.Hole.Concat(c.Board.Take(4)).ToArray());
                        r.BaseMultiplier = r.BaseTriggered ? 1.5m : 1m;
                        r.Multiplier = r.ExtraTriggered ? 2m : r.BaseMultiplier; break;
                    case Joker.Pair:
                        r.BaseTriggered = c.Hole[0].Rank == c.Hole[1].Rank;
                        r.ExtraTriggered = r.BaseTriggered && both && (hand.Category == HandCategory.TwoPair ||
                            hand.Category == HandCategory.Trips || hand.Category == HandCategory.FullHouse || hand.Category == HandCategory.Quads);
                        r.BaseMultiplier = r.BaseTriggered ? 1.3m : 1m;
                        r.Multiplier = r.ExtraTriggered ? 1.8m : r.BaseMultiplier; break;
                    default: throw new ArgumentOutOfRangeException(nameof(joker));
                }
                if (best == null || r.Total > best.Total) best = r;
            }
            return best;
        }

        // Timeout intentionally considers the current board only, unlike the medium AI.
        public static Joker TimeoutChoice(ScoreContext context, IReadOnlyList<Joker> candidates)
        {
            if (candidates.Count == 0) throw new ArgumentException("Candidates required.");
            return candidates.OrderByDescending(j => Calculate(context, j).Total).First();
        }
    }
}
