using System;
using System.Collections.Generic;
using System.Linq;

namespace JokerHand
{
    public static class Poker
    {
        public static readonly int[] BasePoints = { 1000, 3000, 7000, 13000, 21000, 33000, 50000, 75000, 110000, 160000 };
        private static readonly Dictionary<int, int>[] RankBonuses = BuildRankBonuses();
        public static int RankCount(HandCategory category) => RankBonuses[(int)category].Count;

        // The key compares ranks lexicographically: pair/trips ranks first, then kickers.
        private static int Key(IEnumerable<int> ranks)
        {
            int result = 0, length = 0;
            foreach (int rank in ranks) { result = result * 15 + rank; length++; }
            while (length++ < 5) result *= 15;
            return result;
        }

        private static void Classify(int[] ranks, bool flush, out HandCategory category, out int key)
        {
            var counts = new int[15];
            foreach (int rank in ranks) counts[rank]++;
            var singles = new List<int>(5);
            var pairs = new List<int>(2);
            int trips = 0, quads = 0;
            for (int r = 14; r >= 2; r--)
            {
                if (counts[r] == 1) singles.Add(r);
                if (counts[r] == 2) pairs.Add(r);
                if (counts[r] == 3) trips = r;
                if (counts[r] == 4) quads = r;
            }
            int straight = 0;
            if (singles.Count == 5)
            {
                if (singles[0] - singles[4] == 4) straight = singles[0];
                else if (singles[0] == 14 && singles[1] == 5 && singles[4] == 2) straight = 5;
            }
            if (flush && straight > 0)
            { category = straight == 14 ? HandCategory.RoyalFlush : HandCategory.StraightFlush; key = Key(new[] { straight }); }
            else if (quads > 0) { category = HandCategory.Quads; key = Key(new[] { quads, singles[0] }); }
            else if (trips > 0 && pairs.Count == 1) { category = HandCategory.FullHouse; key = Key(new[] { trips, pairs[0] }); }
            else if (flush) { category = HandCategory.Flush; key = Key(singles); }
            else if (straight > 0) { category = HandCategory.Straight; key = Key(new[] { straight }); }
            else if (trips > 0) { category = HandCategory.Trips; key = Key(new[] { trips }.Concat(singles)); }
            else if (pairs.Count == 2) { category = HandCategory.TwoPair; key = Key(pairs.Concat(singles)); }
            else if (pairs.Count == 1) { category = HandCategory.Pair; key = Key(pairs.Concat(singles)); }
            else { category = HandCategory.HighCard; key = Key(singles); }
        }

        private static Dictionary<int, int>[] BuildRankBonuses()
        {
            var keys = Enumerable.Range(0, 10).Select(_ => new SortedSet<int>()).ToArray();
            for (int a = 2; a <= 14; a++)
            for (int b = a; b <= 14; b++)
            for (int c = b; c <= 14; c++)
            for (int d = c; d <= 14; d++)
            for (int e = d; e <= 14; e++)
            {
                if (a == e) continue; // Five copies of one rank cannot exist in a 52-card deck.
                var ranks = new[] { a, b, c, d, e };
                Classify(ranks, false, out var category, out int key);
                keys[(int)category].Add(key);
                if (a < b && b < c && c < d && d < e)
                {
                    Classify(ranks, true, out category, out key);
                    keys[(int)category].Add(key);
                }
            }
            return keys.Select(set => set.Select((key, index) => new { key, index }).ToDictionary(x => x.key, x => x.index)).ToArray();
        }

        public static Hand EvaluateFive(IReadOnlyList<Card> cards)
        {
            if (cards.Count != 5) throw new ArgumentException("Exactly five cards required.");
            Card.ValidateDistinct(cards);
            return EvaluateUnchecked(cards.ToArray());
        }

        private static Hand EvaluateUnchecked(Card[] cards)
        {
            Classify(cards.Select(c => c.Rank).ToArray(), cards.All(c => c.Suit == cards[0].Suit), out var category, out int key);
            return new Hand(category, key, RankBonuses[(int)category][key], cards);
        }

        // Retain every tied strongest selection so joker scoring can break selection ties only.
        public static List<Hand> BestSelections(IReadOnlyList<Card> cards)
        {
            if (cards.Count < 5 || cards.Count > 7) throw new ArgumentException("Five to seven cards required.");
            Card.ValidateDistinct(cards);
            var best = new List<Hand>();
            int bestPoints = -1;
            for (int a = 0; a < cards.Count - 4; a++)
            for (int b = a + 1; b < cards.Count - 3; b++)
            for (int c = b + 1; c < cards.Count - 2; c++)
            for (int d = c + 1; d < cards.Count - 1; d++)
            for (int e = d + 1; e < cards.Count; e++)
            {
                var hand = EvaluateUnchecked(new[] { cards[a], cards[b], cards[c], cards[d], cards[e] });
                if (hand.Points < bestPoints) continue;
                if (hand.Points > bestPoints) { best.Clear(); bestPoints = hand.Points; }
                best.Add(hand);
            }
            return best;
        }

        public static bool HasFlush(IReadOnlyList<Card> cards)
        {
            var count = new int[4];
            foreach (var card in cards) if (++count[(int)card.Suit] >= 5) return true;
            return false;
        }
    }
}
