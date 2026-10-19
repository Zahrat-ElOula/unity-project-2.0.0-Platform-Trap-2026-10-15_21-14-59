using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;
namespace Timbeaux.SFXPack
{
    public class SFXBatchCleaner
    {
        private const string FfmpegPath = "C:\\ffmpeg\\bin\\ffmpeg.exe";

        [MenuItem("Assets/Clean SFX/Light Cleanup", false, 2000)]
        private static void CleanSelectedSFXLight()
        {
            CleanSelectedSFX("Light Cleanup", "afftdn=nf=-20,silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05:stop_periods=1:stop_threshold=-45dB:stop_silence=0.1,loudnorm=I=-16:TP=-1.5:LRA=11", "_clean_light");
        }

        [MenuItem("Assets/Clean SFX/UI Crisp", false, 2001)]
        private static void CleanSelectedSFXUI()
        {
            CleanSelectedSFX("UI Crisp", "afftdn=nf=-18,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.02:stop_periods=1:stop_threshold=-50dB:stop_silence=0.04,loudnorm=I=-14:TP=-1:LRA=8", "_clean_ui");
        }

        [MenuItem("Assets/Clean SFX/Ambient Soft", false, 2002)]
        private static void CleanSelectedSFXAmbient()
        {
            CleanSelectedSFX("Ambient Soft", "afftdn=nf=-16,loudnorm=I=-18:TP=-2:LRA=14", "_clean_ambient");
        }

        [MenuItem("Assets/Clean SFX/Heavy Denoise", false, 2003)]
        private static void CleanSelectedSFXHeavy()
        {
            CleanSelectedSFX("Heavy Denoise", "afftdn=nf=-30,silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05:stop_periods=1:stop_threshold=-45dB:stop_silence=0.1,loudnorm=I=-16:TP=-1.5:LRA=11", "_clean_heavy");
        }

        private static void CleanSelectedSFX(string presetName, string audioFilter, string suffix)
        {
            if (!File.Exists(FfmpegPath))
            {
                UnityEngine.Debug.LogError("FFmpeg was not found at: " + FfmpegPath);
                return;
            }

            int cleanedCount = 0;

            foreach (Object obj in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);

                if (string.IsNullOrEmpty(assetPath) || !assetPath.ToLowerInvariant().EndsWith(".wav"))
                {
                    continue;
                }

                string fullInputPath = Path.GetFullPath(assetPath);
                string outputPath = Path.Combine(
                    Path.GetDirectoryName(fullInputPath),
                    Path.GetFileNameWithoutExtension(fullInputPath) + suffix + ".wav"
                );

                UnityEngine.Debug.Log("Cleaning SFX using " + presetName + ": " + fullInputPath);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = FfmpegPath,
                    Arguments = "-y -i \"" + fullInputPath + "\" -af \"" + audioFilter + "\" -ar 44100 -ac 2 -sample_fmt s16 \"" + outputPath + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                Process process = Process.Start(psi);

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError("FFmpeg failed on: " + fullInputPath + "\n" + error);
                    continue;
                }

                cleanedCount++;
                UnityEngine.Debug.Log("Saved cleaned SFX: " + outputPath);
            }

            AssetDatabase.Refresh();

            UnityEngine.Debug.Log("SFX cleaning complete. Preset: " + presetName + ". Files cleaned: " + cleanedCount);
        }
    }
}