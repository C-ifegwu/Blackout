using NUnit.Framework;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World.Tests
{
    /// <summary>EditMode tests for the monsters' fairness rules, perception and road navigation.</summary>
    public class MonsterTests
    {
        [Test]
        public void Difficulty_Levels_Are_Ordered_And_Off_Has_No_Monsters()
        {
            var off = MonsterTuning.For(0);
            var gentle = MonsterTuning.For(1);
            var normal = MonsterTuning.For(2);
            Assert.AreEqual(0, off.count);
            Assert.Less(gentle.count, normal.count);
            Assert.Less(gentle.chaseSpeed, normal.chaseSpeed);
            Assert.Less(gentle.sightRange, normal.sightRange);
            Assert.Greater(gentle.graceSeconds, normal.graceSeconds);
        }

        [Test]
        public void Gentle_Monsters_Can_Be_Outrun_By_Walking()
        {
            // Player walking speed in the XR rig is 2.5 m/s.
            Assert.Less(MonsterTuning.For(1).chaseSpeed, 2.5f);
        }

        [Test]
        public void Normal_Chase_Is_Escapable_Because_It_Ends()
        {
            var t = MonsterTuning.For(2);
            Assert.Greater(t.maxChaseSeconds, 0f);
            Assert.LessOrEqual(t.maxChaseSeconds, 20f, "A monster must tire so players can escape.");
            Assert.Greater(t.graceSeconds, 10f, "Players need time to listen before any monster can notice them.");
        }

        [Test]
        public void Holding_Still_Shortens_Sight_Range()
        {
            var t = MonsterTuning.For(2);
            Assert.Less(MonsterPerception.SightRange(t, true), MonsterPerception.SightRange(t, false));
        }

        [Test]
        public void Sight_Needs_Range_Cone_And_Clear_Line()
        {
            Vector3 eye = Vector3.up * 1.7f, fwd = Vector3.forward;
            Assert.IsTrue(MonsterPerception.CanSee(eye, fwd, new Vector3(0, 1.5f, 10f), 15f, 110f, 3.5f, true), "in front");
            Assert.IsFalse(MonsterPerception.CanSee(eye, fwd, new Vector3(0, 1.5f, 20f), 15f, 110f, 3.5f, true), "too far");
            Assert.IsFalse(MonsterPerception.CanSee(eye, fwd, new Vector3(0, 1.5f, -10f), 15f, 110f, 3.5f, true), "behind");
            Assert.IsFalse(MonsterPerception.CanSee(eye, fwd, new Vector3(0, 1.5f, 10f), 15f, 110f, 3.5f, false), "wall in the way");
            Assert.IsTrue(MonsterPerception.CanSee(eye, fwd, new Vector3(0, 1.5f, -2f), 15f, 110f, 3.5f, true), "right behind but very close");
        }

        [Test]
        public void Hearing_Scales_With_Difficulty()
        {
            Vector3 m = Vector3.zero, noise = new Vector3(25f, 0f, 0f);
            Assert.IsTrue(MonsterPerception.CanHear(m, noise, 30f, MonsterTuning.For(2).hearingScale));
            Assert.IsFalse(MonsterPerception.CanHear(m, noise, 30f, MonsterTuning.For(1).hearingScale));
        }

        [Test]
        public void Noticing_Takes_Time_And_Is_Faster_When_Close()
        {
            float near = MonsterPerception.NoticeSeconds(2f, 15f);
            float far = MonsterPerception.NoticeSeconds(14f, 15f);
            Assert.Greater(near, 0.3f, "Never instant.");
            Assert.Less(near, far);
        }

        [Test]
        public void RoadGraph_Snaps_Steps_And_Stays_In_Bounds()
        {
            var g = new RoadGraph(new Vector2(-168f, -168f), 48f, 8);
            Assert.AreEqual(new Vector2Int(3, 6), g.Nearest(new Vector3(-24f, 0f, 120f)));
            Assert.AreEqual(new Vector2Int(0, 0), g.Nearest(new Vector3(-999f, 0f, -999f)));
            Assert.AreEqual(new Vector2Int(7, 7), g.Nearest(new Vector3(999f, 0f, 999f)));
            Assert.AreEqual(new Vector3(-168f, 0f, -168f), g.WorldPos(Vector2Int.zero));

            var step = g.StepToward(new Vector2Int(1, 1), new Vector2Int(4, 2));
            Assert.AreEqual(new Vector2Int(2, 1), step);
            Assert.AreEqual(new Vector2Int(2, 2), g.StepToward(new Vector2Int(2, 2), new Vector2Int(2, 2)));

            var rng = new System.Random(5);
            var cur = new Vector2Int(0, 0);
            var prev = cur;
            for (int i = 0; i < 200; i++)
            {
                var next = g.RandomNeighbour(cur, prev, rng);
                Assert.AreEqual(1, Mathf.Abs(next.x - cur.x) + Mathf.Abs(next.y - cur.y), "one road segment at a time");
                Assert.That(next.x, Is.InRange(0, 7));
                Assert.That(next.y, Is.InRange(0, 7));
                prev = cur; cur = next;
            }
        }

        [Test]
        public void Monster_Settings_Default_To_Normal_And_Clamp()
        {
            GameSettings.ResetToDefaults();
            Assert.AreEqual(GameSettings.MonstersNormal, GameSettings.MonsterLevel);
            GameSettings.MonsterLevel = 9;
            Assert.AreEqual(2, GameSettings.MonsterLevel);
            GameSettings.MonsterLevel = GameSettings.MonstersOff;
            Assert.AreEqual("Off", GameSettings.MonsterLevelName);
            GameSettings.ResetToDefaults();
        }
    }
}
