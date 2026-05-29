using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Sounds;

namespace IAmYourTranslator.Audio
{
    public static class AudioClipReplacer
    {
        private static readonly string[] ReplacementExtensions = { ".wav", ".ogg" };
        private static readonly Dictionary<string, Dictionary<string, string>> ReplacementIndexByDirectory =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static bool TryFindReplacementAudioFile(string audioDir, string clipName, out string filePath)
        {
            filePath = null;

            if (string.IsNullOrWhiteSpace(audioDir) || string.IsNullOrWhiteSpace(clipName))
                return false;
            if (!Directory.Exists(audioDir))
                return false;

            string safeName = SanitizeFileNameForPath(clipName);
            if (TryFindByBaseName(audioDir, clipName, out filePath))
                return true;
            if (!string.Equals(safeName, clipName, StringComparison.Ordinal) &&
                TryFindByBaseName(audioDir, safeName, out filePath))
            {
                return true;
            }

            var index = GetOrBuildReplacementIndex(audioDir);
            string normalized = NormalizeAudioKey(clipName);
            if (!string.IsNullOrEmpty(normalized) && index.TryGetValue(normalized, out filePath))
                return true;

            if (!string.Equals(safeName, clipName, StringComparison.Ordinal))
            {
                string normalizedSafe = NormalizeAudioKey(safeName);
                if (!string.IsNullOrEmpty(normalizedSafe) && index.TryGetValue(normalizedSafe, out filePath))
                    return true;
            }

            return false;
        }

        private static bool TryFindByBaseName(string audioDir, string baseName, out string filePath)
        {
            filePath = null;
            if (string.IsNullOrWhiteSpace(baseName))
                return false;

            foreach (var ext in ReplacementExtensions)
            {
                string candidate = Path.Combine(audioDir, baseName + ext);
                if (File.Exists(candidate))
                {
                    filePath = candidate;
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, string> GetOrBuildReplacementIndex(string audioDir)
        {
            if (ReplacementIndexByDirectory.TryGetValue(audioDir, out var cached))
                return cached;

            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var file in Directory.EnumerateFiles(audioDir, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file);
                    if (!ext.Equals(".wav", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string key = NormalizeAudioKey(name);
                    if (string.IsNullOrEmpty(key) || index.ContainsKey(key))
                        continue;

                    index[key] = file;
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[AudioClipReplacer] Failed to build replacement index for '{audioDir}': {e.Message}");
            }

            ReplacementIndexByDirectory[audioDir] = index;
            return index;
        }

        private static string NormalizeAudioKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char ch in input)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToUpperInvariant(ch));
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        private static string SanitizeFileNameForPath(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name;
        }

        public static AudioClip CreateDecompressedCopy(AudioClip original)
        {
            if (original == null) return null;

            if (original.loadType == AudioClipLoadType.DecompressOnLoad)
                return original;

            float[] samples = new float[original.samples * original.channels];
            original.GetData(samples, 0);

            AudioClip decompressed = AudioClip.Create(
                original.name + "_decompressed",
                original.samples,
                original.channels,
                original.frequency,
                false
            );
            decompressed.SetData(samples, 0);
            return decompressed;
        }

        public static void ExportAudioClipToWav(AudioClip clip, string filePath)
        {
            if (clip == null) return;

            try
            {
                clip = CreateDecompressedCopy(clip);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    int headerSize = 44;
                    fs.Seek(headerSize, SeekOrigin.Begin);

                    float[] samples = new float[clip.samples * clip.channels];
                    clip.GetData(samples, 0);

                    short[] intData = new short[samples.Length];
                    byte[] bytesData = new byte[samples.Length * 2];

                    for (int i = 0; i < samples.Length; i++)
                    {
                        intData[i] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);
                        BitConverter.GetBytes(intData[i]).CopyTo(bytesData, i * 2);
                    }

                    fs.Write(bytesData, 0, bytesData.Length);

                    fs.Seek(0, SeekOrigin.Begin);
                    byte[] header = CreateWavHeader(clip, bytesData.Length);
                    fs.Write(header, 0, header.Length);
                }

                Debug.Log("[AudioClipReplacer] WAV exported: " + filePath);
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioClipReplacer] Failed to export WAV to {filePath}: {e}");
            }
        }

        public static void ExportAudioClipToOgg(AudioClip clip, string filePath)
        {
            if (clip == null) return;

            string tempWav = null;
            try
            {
                clip = CreateDecompressedCopy(clip);
                tempWav = Path.Combine(Path.GetTempPath(), clip.name + ".wav");
                ExportAudioClipToWav(clip, tempWav);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                var ffmpeg = new System.Diagnostics.Process();
                ffmpeg.StartInfo.FileName = "ffmpeg";
                ffmpeg.StartInfo.Arguments = $"-y -i \"{tempWav}\" -c:a libvorbis \"{filePath}\"";
                ffmpeg.StartInfo.CreateNoWindow = true;
                ffmpeg.StartInfo.UseShellExecute = false;
                ffmpeg.Start();
                ffmpeg.WaitForExit();

                Debug.Log("[AudioClipReplacer] OGG exported: " + filePath);
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioClipReplacer] Error exporting OGG: {e}");
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(tempWav) && File.Exists(tempWav))
                        File.Delete(tempWav);
                }
                catch { }
            }
        }

        public static AudioClip LoadAudioClip(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning("[AudioClipReplacer] File not found: " + filePath);
                return null;
            }

            AudioType type = filePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                ? AudioType.OGGVORBIS
                : AudioType.WAV;

            using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, type))
            {
                var request = www.SendWebRequest();
                while (!request.isDone) { }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[AudioClipReplacer] Loading error: " + www.error);
                    return null;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                clip.name = Path.GetFileNameWithoutExtension(filePath);
                return clip;
            }
        }

        public static void ReplaceAudioClip(UnityEngine.AudioSource source, string filePath)
        {
            if (source == null) return;
            AudioClip clip = LoadAudioClip(filePath);
            if (clip == null) return;

            source.clip = clip;
            source.Play();
            Debug.Log("[AudioClipReplacer] AudioSource playing: " + clip.name);
        }

        public static void ReplaceSoundObjectClip(SoundObject soundObj, string filePath, string name)
        {
            if (soundObj == null)
            {
                Debug.LogWarning("[AudioClipReplacer] SoundObject " + name + " == null");
                return;
            }

            AudioClip clip = LoadAudioClip(filePath);
            if (clip == null) return;

            soundObj.SetClip(clip);
            Debug.Log("[AudioClipReplacer] " + name + " replaced with: " + clip.name);
        }

        private static byte[] CreateWavHeader(AudioClip clip, int dataLength)
        {
            int hz = clip.frequency;
            int channels = clip.channels;
            int byteRate = hz * channels * 2;
            byte[] header = new byte[44];

            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
            BitConverter.GetBytes(dataLength + 36).CopyTo(header, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(header, 8);
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(header, 12);
            BitConverter.GetBytes(16).CopyTo(header, 16);
            BitConverter.GetBytes((short)1).CopyTo(header, 20);
            BitConverter.GetBytes((short)channels).CopyTo(header, 22);
            BitConverter.GetBytes(hz).CopyTo(header, 24);
            BitConverter.GetBytes(byteRate).CopyTo(header, 28);
            BitConverter.GetBytes((short)(channels * 2)).CopyTo(header, 32);
            BitConverter.GetBytes((short)16).CopyTo(header, 34);
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(header, 36);
            BitConverter.GetBytes(dataLength).CopyTo(header, 40);

            return header;
        }
    }
}
