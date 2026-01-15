using System;
using System.IO;
using UnityEngine;
using System.Reflection;

namespace IAmYourTranslator
{
    public static class AudioClipDumper
    {
        public static bool TryDumpRawData(AudioClip clip, string outputPath, out string message)
        {
            message = "";
            if (clip == null)
            {
                message = "AudioClip == null";
                return false;
            }

            try
            {
                string nameSafe = string.Join("_", clip.name.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(outputPath, nameSafe + ".raw");
                Directory.CreateDirectory(outputPath);

                float[] data = new float[clip.samples * clip.channels];

                try
                {
                    // Attempt standard method
                    if (clip.GetData(data, 0))
                    {
                        File.WriteAllBytes(filePath, FloatsToBytes(data));
                        message = $"✅ Raw PCM data saved: {filePath}";
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AudioClipDumper] GetData() is not available for '{clip.name}', trying to extract binary data directly: {e.Message}");
                }

                // Trying through reflection (access to internal Unity fields)
                byte[] rawData = TryExtractRawAudioData(clip);
                if (rawData != null && rawData.Length > 0)
                {
                    File.WriteAllBytes(filePath, rawData);
                    message = $"⚙️ Extracted raw bytes of compressed audio: {filePath} ({rawData.Length} bytes)";
                    return true;
                }

                message = $"❌ Failed to get audio data: {clip.name}";
                return false;
            }
            catch (Exception ex)
            {
                message = $"[AudioClipDumper] Error saving {clip.name}: {ex}";
                return false;
            }
        }

        private static byte[] TryExtractRawAudioData(AudioClip clip)
        {
            try
            {
                // Unity 2020+ stores a reference to native data in the private field m_AudioClip
                var field = typeof(AudioClip).GetField("m_AudioClip", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var nativeRef = field.GetValue(clip);
                    if (nativeRef != null)
                    {
                        // Sometimes AudioClip stores a reference to FMOD handle or UnityEngine.Object with "m_AudioData"
                        var dataField = nativeRef.GetType().GetField("m_AudioData", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (dataField != null)
                        {
                            byte[] raw = dataField.GetValue(nativeRef) as byte[];
                            if (raw != null && raw.Length > 0)
                                return raw;
                        }
                    }
                }

                // Let's try to extract through Unity API (rarely works, but maybe)
                var method = typeof(AudioClip).GetMethod("GetNativeAudioData", BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    object result = method.Invoke(clip, null);
                    if (result is byte[] bytes && bytes.Length > 0)
                        return bytes;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioClipDumper] Failed to extract internal data: {e.Message}");
                return null;
            }
        }

        private static byte[] FloatsToBytes(float[] samples)
        {
            var bytes = new byte[samples.Length * 4];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
