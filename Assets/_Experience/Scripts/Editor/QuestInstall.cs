using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;

namespace Blackout.Experience.Editor
{
    /// <summary>Installs the built APK on a USB-connected Quest and launches it, using the adb that ships with Unity.</summary>
    public static class QuestInstall
    {
        static string Adb => Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK", "platform-tools", "adb.exe");

        static string Run(string args, int timeoutMs = 120000)
        {
            var psi = new ProcessStartInfo(Adb, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(timeoutMs);
                return o.Trim();
            }
        }

        [MenuItem("Blackout/Quest 2/Install APK On Headset")]
        public static void Install()
        {
            if (!File.Exists(Adb)) { UnityEngine.Debug.LogError("[Blackout] adb not found at " + Adb); return; }
            string apk = Path.GetFullPath(QuestBuild.OutputPath);
            string pkg = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            UnityEngine.Debug.Log("[Blackout] Devices: " + Run("devices -l"));
            UnityEngine.Debug.Log("[Blackout] Install: " + Run("install -r -d \"" + apk + "\""));
            UnityEngine.Debug.Log("[Blackout] Launch: " + Run("shell monkey -p " + pkg + " -c android.intent.category.LAUNCHER 1"));
        }

        [MenuItem("Blackout/Quest 2/Launch And Capture Log")]
        public static void CaptureLog()
        {
            if (!File.Exists(Adb)) { UnityEngine.Debug.LogError("[Blackout] adb not found"); return; }
            string pkg = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            Run("logcat -c");
            Run("shell am force-stop " + pkg);
            Run("shell monkey -p " + pkg + " -c android.intent.category.LAUNCHER 1");
            System.Threading.Thread.Sleep(30000);
            string log = Run("logcat -d -t 6000", 60000);
            string path = Path.GetFullPath("Logs/quest-logcat.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, log);
            UnityEngine.Debug.Log("[Blackout] Log saved: " + path + " (" + log.Length + " chars)");
        }

        [MenuItem("Blackout/Quest 2/Clean Reinstall And Capture Log")]
        public static void CleanReinstall()
        {
            if (!File.Exists(Adb)) { UnityEngine.Debug.LogError("[Blackout] adb not found"); return; }
            string apk = Path.GetFullPath(QuestBuild.OutputPath);
            string pkg = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            UnityEngine.Debug.Log("[Blackout] Uninstall: " + Run("uninstall " + pkg));
            UnityEngine.Debug.Log("[Blackout] Install: " + Run("install -r \"" + apk + "\"", 300000));
            CaptureLog();
        }
    }
}
