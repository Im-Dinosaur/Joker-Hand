using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JokerHand
{
    // This is the entire AI input contract. It cannot carry an opponent hand or deck order.
    public sealed class Observation
    {
        public ReadOnlyCollection<Card> Hole { get; }
        public ReadOnlyCollection<Card> Board { get; }
        public ReadOnlyCollection<Card> OwnDiscards { get; }
        public ReadOnlyCollection<Card> OpponentDiscards { get; }
        public ReadOnlyCollection<Card> Replacements { get; }
        public ReadOnlyCollection<Joker> Candidates { get; }
        public ReadOnlyCollection<Joker> OpponentCandidates { get; }
        public Observation(IEnumerable<Card> hole, IEnumerable<Card> board, IEnumerable<Card> ownDiscards,
            IEnumerable<Card> opponentDiscards, IEnumerable<Card> replacements, IEnumerable<Joker> candidates,
            IEnumerable<Joker> opponentCandidates)
        {
            Hole = Array.AsReadOnly(hole.ToArray()); Board = Array.AsReadOnly(board.ToArray());
            OwnDiscards = Array.AsReadOnly(ownDiscards.ToArray()); OpponentDiscards = Array.AsReadOnly(opponentDiscards.ToArray());
            Replacements = Array.AsReadOnly(replacements.ToArray()); Candidates = Array.AsReadOnly(candidates.ToArray());
            OpponentCandidates = Array.AsReadOnly(opponentCandidates.ToArray());
            if (Hole.Count != 2 || (Board.Count != 3 && Board.Count != 4)) throw new ArgumentException("Decision observations need two private cards and three or four board cards.");
            ValidateLoadout(Candidates); ValidateLoadout(OpponentCandidates);
            Card.ValidateDistinct(Hole.Concat(Board).Concat(OwnDiscards).Concat(OpponentDiscards));
            if (OwnDiscards.Count > 2 || OpponentDiscards.Count > 2 || OwnDiscards.Count != Replacements.Count ||
                Replacements.Distinct().Count() != Replacements.Count || Replacements.Any(c => !Hole.Contains(c)))
                throw new ArgumentException("Invalid exchange history.");
            if (Board.Count == 3 && (OwnDiscards.Count != 0 || OpponentDiscards.Count != 0))
                throw new ArgumentException("Discards remain private until both exchanges lock.");
        }
        public static void ValidateLoadout(IEnumerable<Joker> candidates)
        {
            var array = candidates.ToArray();
            if (array.Length != 3 || array.Distinct().Count() != 3 || array.Any(j => (int)j < 0 || (int)j > 5))
                throw new ArgumentException("Exactly three different joker types required.");
        }
        public Card[] UnseenCards()
        {
            var known = new HashSet<Card>(Hole.Concat(Board).Concat(OwnDiscards).Concat(OpponentDiscards));
            return Card.Deck().Where(c => !known.Contains(c)).ToArray();
        }
        public ScoreContext CurrentScore() => new ScoreContext(Hole, Board, OwnDiscards, Replacements, OpponentDiscards.Count);
    }

    public sealed class JokerDecision
    {
        public Joker Choice { get; internal set; }
        public Card? Prediction { get; internal set; }
        public double ExpectedScore { get; internal set; }
    }

    public sealed class ExchangeDecision
    {
        // Bit zero selects private card zero; bit one selects private card one.
        public int Mask { get; internal set; }
        public double[] ExpectedScores { get; internal set; }
    }

    public sealed class MediumAi
    {
        public int ExchangeSamples { get; }
        public int RiverSamples { get; }
        public MediumAi(int exchangeSamples = 48, int riverSamples = 12)
        {
            if (exchangeSamples < 1 || riverSamples < 1) throw new ArgumentOutOfRangeException();
            ExchangeSamples = exchangeSamples; RiverSamples = riverSamples;
        }

        public JokerDecision ChooseJoker(Observation observation)
        {
            if (observation.Board.Count != 4) throw new ArgumentException("Choose a joker after the fourth board card.");
            var pool = observation.UnseenCards();
            return EvaluateRivers(observation.Hole.ToArray(), observation.Board.ToArray(), observation.OwnDiscards.ToArray(),
                observation.Replacements.ToArray(), observation.OpponentDiscards.Count, observation.Candidates, pool, pool);
        }

        private static Card Predict(IReadOnlyList<Card> pool)
        {
            // Exact-card chance is equal for every unseen card; maximize rank-hit chance first.
            int rank = pool.GroupBy(c => c.Rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
            return pool.Where(c => c.Rank == rank).OrderBy(c => c.Id).First();
        }

        private static JokerDecision EvaluateRivers(Card[] hole, Card[] turnBoard, Card[] discards, Card[] replacements,
            int opponentCount, IReadOnlyList<Joker> candidates, Card[] unseen, Card[] rivers)
        {
            var totals = new double[candidates.Count];
            Card prediction = Predict(unseen); // Fixed before evaluating any possible fifth card.
            foreach (var river in rivers)
            {
                var context = new ScoreContext(hole, turnBoard.Concat(new[] { river }), discards, replacements, opponentCount);
                var hands = Poker.BestSelections(context.Hole.Concat(context.Board).ToArray());
                for (int j = 0; j < candidates.Count; j++)
                    totals[j] += Scoring.Calculate(context, candidates[j], candidates[j] == Joker.Prediction ? prediction : (Card?)null, hands).Total;
            }
            int best = 0;
            for (int j = 1; j < totals.Length; j++) if (totals[j] > totals[best]) best = j;
            return new JokerDecision { Choice = candidates[best], Prediction = candidates[best] == Joker.Prediction ? prediction : (Card?)null,
                ExpectedScore = totals[best] / rivers.Length };
        }

        public ExchangeDecision ChooseExchange(Observation observation, int seed)
        {
            if (observation.Board.Count != 3) throw new ArgumentException("Exchange follows the third board card.");
            var values = new double[4];
            var unseen = observation.UnseenCards();
            for (int mask = 0; mask < 4; mask++)
            {
                var random = new Random(seed); // Common random samples reduce noise between choices.
                for (int sample = 0; sample < ExchangeSamples; sample++)
                {
                    var shuffled = (Card[])unseen.Clone();
                    Shuffle(shuffled, random);
                    var hole = observation.Hole.ToArray();
                    var discards = new List<Card>();
                    var replacements = new List<Card>();
                    int cursor = 0;
                    for (int slot = 0; slot < 2; slot++)
                    {
                        if ((mask & (1 << slot)) == 0) continue;
                        discards.Add(hole[slot]); hole[slot] = shuffled[cursor++]; replacements.Add(hole[slot]);
                    }
                    var turnBoard = observation.Board.Concat(new[] { shuffled[cursor++] }).ToArray();
                    var remaining = shuffled.Skip(cursor).ToArray();
                    // Opponent's simultaneous choice is unknown: a simple 0/1/2 prior, not their pending choice.
                    int opponentCount = random.Next(3);
                    var decision = EvaluateRivers(hole, turnBoard, discards.ToArray(), replacements.ToArray(), opponentCount,
                        observation.Candidates, remaining, remaining.Take(Math.Min(RiverSamples, remaining.Length)).ToArray());
                    values[mask] += decision.ExpectedScore;
                }
                values[mask] /= ExchangeSamples;
            }
            int bestMask = 0;
            for (int mask = 1; mask < 4; mask++) if (values[mask] > values[bestMask]) bestMask = mask;
            return new ExchangeDecision { Mask = bestMask, ExpectedScores = values };
        }

        internal static void Shuffle(Card[] cards, Random random)
        {
            for (int i = cards.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Card saved = cards[i]; cards[i] = cards[j]; cards[j] = saved;
            }
        }
    }
}
