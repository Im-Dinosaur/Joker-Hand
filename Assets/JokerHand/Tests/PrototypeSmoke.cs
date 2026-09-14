using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JokerHand.Tests
{
    public sealed class PrototypeSmoke
    {
        [UnityTest]
        public IEnumerator MediumAiCompletesAPlayableMatch()
        {
            var root = new GameObject("Prototype smoke test");
            try
            {
                var component = root.AddComponent<Prototype>();
                yield return null; // Initialize the component and draw preparation UI where available.
                component.SendMessage("StartMatch");
                var field = typeof(Prototype).GetField("match", BindingFlags.NonPublic | BindingFlags.Instance);
                var match = (Match)field.GetValue(component);
                Assert.NotNull(match);
                match.SelectExchange(0, 1);
                match.Confirm(0);
                float deadline = Time.realtimeSinceStartup + 12;
                while (match.Stage == MatchStage.Exchange && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(MatchStage.JokerChoice, match.Stage, "AI must return an exchange decision before timeout.");
                Assert.Less(match.SecondsLeft, 30.001);
                Assert.Greater(match.SecondsLeft, 25);
                yield return null; // Draw and schedule the new phase before the player confirms.
                match.SelectJoker(0, Joker.Hold);
                match.Confirm(0);
                deadline = Time.realtimeSinceStartup + 8;
                while (match.Stage == MatchStage.JokerChoice && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(MatchStage.Reveal, match.Stage, "AI must choose a joker before timeout.");
                deadline = Time.realtimeSinceStartup + 8;
                while (match.Stage == MatchStage.Reveal && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(MatchStage.Complete, match.Stage);
                Assert.AreEqual(2, match.Results.Length);
                Assert.Greater(match.Results[0].Total, 0);
                Assert.Greater(match.Results[1].Total, 0);
                yield return null; // Exercise the result screen too.
            }
            finally { Object.Destroy(root); }
        }
    }
}
