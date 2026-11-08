using NUnit.Framework;
using UnityEngine;

namespace Blackout.Experience.Tests
{
    public class CaptionTableTests
    {
        [Test]
        public void Parse_ReadsRowsAndSkipsHeaderCommentsAndBlanks()
        {
            var csv = "id,speaker,text,seconds\n# comment\n\nvendor_01,Vendor,\"Fresh bread, fresh bread!\",3.5\nradio_01,Radio,Static,\n";
            var table = CaptionTable.Parse(csv);
            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("Fresh bread, fresh bread!", table["vendor_01"].Text);
            Assert.AreEqual(3.5f, table["vendor_01"].Seconds, 0.001f);
            Assert.GreaterOrEqual(table["radio_01"].Seconds, 2f);
        }

        [Test]
        public void Parse_HandlesEscapedQuotesAndWindowsLineEndings()
        {
            var table = CaptionTable.Parse("a,B,\"She said \"\"hello\"\"\",2\r\nc,D,Plain,2\r\n");
            Assert.AreEqual("She said \"hello\"", table["a"].Text);
            Assert.AreEqual(2, table.Count);
        }

        [Test]
        public void Parse_EmptyInput_ReturnsEmptyTable() => Assert.AreEqual(0, CaptionTable.Parse(null).Count);
    }

    public class GameSettingsTests
    {
        [SetUp] public void SetUp() => GameSettings.ResetToDefaults();
        [TearDown] public void TearDown() => GameSettings.ResetToDefaults();

        [Test]
        public void Defaults_AreSafe()
        {
            Assert.IsTrue(GameSettings.CaptionsOn);
            Assert.IsTrue(GameSettings.ReduceFlashing, "Flashing must be reduced by default.");
            Assert.IsTrue(GameSettings.SnapTurn, "Snap turn is the comfort default.");
            Assert.AreEqual(1f, GameSettings.Brightness, 0.001f);
        }

        [Test]
        public void Values_AreClampedAndPersisted()
        {
            GameSettings.Brightness = 99f;
            Assert.AreEqual(GameSettings.MaxBrightness, GameSettings.Brightness, 0.001f);
            GameSettings.CaptionScale = -5f;
            Assert.AreEqual(GameSettings.MinCaptionScale, GameSettings.CaptionScale, 0.001f);
            GameSettings.HighContrast = true;
            GameSettings.Reload();
            Assert.IsTrue(GameSettings.HighContrast, "Saved value must survive a reload.");
        }

        [Test]
        public void Changed_FiresOnlyWhenValueChanges()
        {
            int count = 0;
            void Handler() => count++;
            GameSettings.Changed += Handler;
            GameSettings.Seated = true;
            GameSettings.Seated = true;
            GameSettings.Changed -= Handler;
            Assert.AreEqual(1, count);
        }
    }

    public class GameFlowTests
    {
        [SetUp]
        public void SetUp()
        {
            GameFlow.Begin();                  // ensure we start from a known state
            GameFlow.AcceptHealthNotice();
        }

        [TearDown] public void TearDown() => Time.timeScale = 1f;

        [Test]
        public void FullRun_PlayPauseDeliver()
        {
            Assert.AreEqual(GameState.MainMenu, GameFlow.State);
            GameFlow.StartRun();
            Assert.AreEqual(GameState.Playing, GameFlow.State);
            GameFlow.Pause();
            Assert.AreEqual(GameState.Paused, GameFlow.State);
            Assert.AreEqual(0f, Time.timeScale);
            GameFlow.Resume();
            Assert.AreEqual(1f, Time.timeScale);
            bool delivered = false;
            void OnDelivered() => delivered = true;
            GameFlow.Delivered += OnDelivered;
            GameFlow.OnDelivered();
            GameFlow.Delivered -= OnDelivered;
            Assert.IsTrue(delivered);
            Assert.AreEqual(GameState.Ended, GameFlow.State);
            GameFlow.ReturnToMenu();
            Assert.AreEqual(GameState.MainMenu, GameFlow.State);
        }

        [Test]
        public void OnDelivered_IgnoredUnlessPlaying()
        {
            GameFlow.OnDelivered();
            Assert.AreEqual(GameState.MainMenu, GameFlow.State);
        }
    }

    public class UIStyleTests
    {
        [Test]
        public void TextOnBackground_MeetsWcagAA_InBothModes()
        {
            GameSettings.HighContrast = false;
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(UIStyle.Text, UIStyle.Background), 7f);
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(UIStyle.Text, UIStyle.SurfaceHover), 4.5f);
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(UIStyle.Accent, UIStyle.Background), 7f);
            GameSettings.HighContrast = true;
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(UIStyle.Text, UIStyle.Background), 15f);
            GameSettings.ResetToDefaults();
        }

        [Test]
        public void MinimumSizes_MatchTheStandard()
        {
            Assert.GreaterOrEqual(UIStyle.MinFontSize, 36f);
            Assert.GreaterOrEqual(UIStyle.ButtonHeight, 80f);
            Assert.That(UIStyle.PanelDistance, Is.InRange(1.5f, 2f));
        }
    }
}
