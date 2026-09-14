using System;
using System.Collections.Generic;
using System.Linq;

namespace JokerHand
{
    public enum Suit { Clubs, Diamonds, Hearts, Spades }
    public enum HandCategory { HighCard, Pair, TwoPair, Trips, Straight, Flush, FullHouse, Quads, StraightFlush, RoyalFlush }
    public enum Joker { Hold, Prediction, Read, Resolve, Flush, Pair }

    public readonly struct Card : IEquatable<Card>
    {
        public readonly int Rank;
        public readonly Suit Suit;
        public int Id => (int)Suit * 13 + Rank - 2;
        public Card(int rank, Suit suit)
        {
            if (rank < 2 || rank > 14 || (int)suit < 0 || (int)suit > 3) throw new ArgumentOutOfRangeException();
            Rank = rank; Suit = suit;
        }
        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => RankText(Rank) + new[] { "♣", "♦", "♥", "♠" }[(int)Suit];
        public static string RankText(int rank) => rank <= 10 ? rank.ToString() : new[] { "J", "Q", "K", "A" }[rank - 11];
        public static Card[] Deck() => Enumerable.Range(0, 52).Select(id => new Card(id % 13 + 2, (Suit)(id / 13))).ToArray();
        public static void ValidateDistinct(IEnumerable<Card> cards)
        {
            var seen = new HashSet<int>();
            foreach (var card in cards)
                if (card.Rank < 2 || card.Rank > 14 || (int)card.Suit < 0 || (int)card.Suit > 3 || !seen.Add(card.Id))
                    throw new ArgumentException("Cards must be valid and distinct.");
        }
    }

    public sealed class Hand
    {
        public readonly HandCategory Category;
        public readonly int RankKey;
        public readonly int BasePoints;
        public readonly int RankBonus;
        public readonly Card[] Cards;
        public int Points => BasePoints + RankBonus;
        internal Hand(HandCategory category, int key, int bonus, Card[] cards)
        { Category = category; RankKey = key; RankBonus = bonus; Cards = cards; BasePoints = Poker.BasePoints[(int)category]; }
    }

    public static class Labels
    {
        public static readonly string[] Hands = { "하이카드", "원페어", "투페어", "트리플", "스트레이트", "플러시", "풀하우스", "포카드", "스트레이트 플러시", "로열 플러시" };
        public static readonly string[] Jokers = { "유지·활용형", "예측형", "간파형", "결단형", "플러시 특화형", "페어형" };
        public static readonly string[] Effects = {
            "개인 카드 2장 활용 +3,000 / 교체 0장 추가 +5,000",
            "기본 +2,000 / 숫자 적중 추가 +10,000 / 완전 적중 추가 +100,000",
            "상대보다 적게 교체 +4,000 / 투페어 이상 추가 +5,000",
            "J·Q·K·A 버림 +2,000 / 새 카드 활용 추가 +5,000",
            "플러시 계열 ×1.5 / 마지막 카드로 완성 ×2.0",
            "개인 페어 ×1.3 / 그 페어로 상위 조합 완성 ×1.8"
        };
    }
}
