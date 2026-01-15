using System;
using System.IO;
using UnityEngine;

namespace IAmYourTranslator.Audio
{
    public static class OggEncoder
    {
        public static void SaveToOgg(string path, float[] samples, int channels, int sampleRate)
        {
            // Forced conversion to 44100 Hz
            if (sampleRate != 44100)
            {
                samples = Resample(samples, sampleRate, 44100, channels);
                sampleRate = 44100;
            }

            // Creating an OGG container (but with a PCM 32-bit float)
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(System.Text.Encoding.ASCII.GetBytes("OggS")); // magic
                bw.Write((byte)0); // version
                bw.Write((byte)0);
                bw.Write((long)0); // granule pos
                bw.Write((int)0); // stream serial
                bw.Write((int)0); // page seq
                bw.Write((int)0); // checksum placeholder
                bw.Write((byte)1); // segments
                bw.Write((byte)255); // dummy segment size

                // Writing 32-bit PCM (IEEE float)
                foreach (float f in samples)
                {
                    bw.Write(f);
                }
            }
        }

        private static float[] Resample(float[] input, int srcRate, int dstRate, int channels)
        {
            if (srcRate == dstRate) return input;

            int srcSamples = input.Length / channels;
            int dstSamples = (int)((long)srcSamples * dstRate / srcRate);
            float[] output = new float[dstSamples * channels];

            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < dstSamples; i++)
                {
                    float t = (float)i * srcRate / dstRate;
                    int idx = (int)t;
                    float frac = t - idx;

                    if (idx < srcSamples - 1)
                        output[i * channels + ch] = Mathf.Lerp(input[idx * channels + ch], input[(idx + 1) * channels + ch], frac);
                    else
                        output[i * channels + ch] = input[idx * channels + ch];
                }
            }

            return output;
        }
    }
}
