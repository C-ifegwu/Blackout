using NUnit.Framework;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World.Tests
{
    /// <summary>EditMode tests for the safety rules and tuning of the sonar and objective (Tracks 2 and 3).</summary>
    public class WorldTests
    {
        GameObject _go;

        [TearDown]
        public void TearDown() { if (_go != null) Object.DestroyImmediate(_go); }

        [Test]
        public void Sonar_Cooldown_Is_Above_One_Second()
        {
            _go = new GameObject("sonar");
            var s = _go.AddComponent<SonarPing>();
            Assert.Greater(s.cooldown, 1.0f, "Safety rule: pings must be more than 1 second apart.");
        }

        [Test]
        public void Sonar_Has_Enough_Rays_And_Sane_Range()
        {
            _go = new GameObject("sonar");
            var s = _go.AddComponent<SonarPing>();
            Assert.GreaterOrEqual(s.azimuths * s.elevationsDeg.Length, 600, "Too few rays to reveal the streets.");
            Assert.That(s.range, Is.InRange(20f, 80f));
            Assert.Greater(s.waveSpeed, 0f);
        }

        [Test]
        public void Gameplay_Hint_Interval_And_Dwell_Are_Reasonable()
        {
            _go = new GameObject("gameplay");
            var g = _go.AddComponent<SonarGameplay>();
            Assert.That(g.dwellSeconds, Is.InRange(1.5f, 8f), "Dwell fallback must be reachable but not instant.");
            Assert.GreaterOrEqual(g.hintInterval, 10f);
            Assert.GreaterOrEqual(g.zoneRadius, 3f);
            Assert.AreEqual(0, g.Stage);
        }

        [Test]
        public void CityPower_Relight_Is_Gradual()
        {
            _go = new GameObject("power");
            var p = _go.AddComponent<CityPower>();
            Assert.GreaterOrEqual(p.RestoreSeconds, 3f, "Relight must take at least 3 seconds (no hard flash).");
            Assert.Less(p.darkAmbient.maxColorComponent, p.litAmbient.maxColorComponent);
            Assert.Less(p.moonDark, p.moonLit);
        }
    
        [Test]
        public void Challenge_Mode_Is_Optional_And_Fair()
        {
            _go = new GameObject("gameplay");
            var g = _go.AddComponent<SonarGameplay>();
            Assert.GreaterOrEqual(g.challengeSeconds, 240f, "Beat the dawn must leave time to walk the route.");
            Assert.AreEqual(-1f, g.TimeLeft, "Story mode has no countdown.");
            GameSettings.ResetToDefaults();
            Assert.IsFalse(GameSettings.Challenge, "Challenge mode must be off by default.");
        }

        [Test]
        public void Time_Formatting_Is_Minutes_And_Seconds()
        {
            Assert.AreEqual("0:00", GameFlow.FormatTime(0f));
            Assert.AreEqual("1:05", GameFlow.FormatTime(65f));
            Assert.AreEqual("12:30", GameFlow.FormatTime(750f));
        }
    }
}
