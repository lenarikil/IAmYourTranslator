using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using UnityEngine;
using BepInEx;

namespace IAmYourTranslator
{
    // Add component to the same GameObject as AudioSource.
    public class AudioCapture : MonoBehaviour
    {
        // Settings
        public string outputFilePath;
        public int targetRate = 44100; // final frequency (ffmpeg resamples)
        public int ffmpegQuality = 10; // for libvorbis (0..10 -> -q:a)

        AudioSource audioSource;
        ConcurrentQueue<byte[]> queue = new ConcurrentQueue<byte[]>();
        Thread writerThread;
        Process ffmpegProc;
        volatile bool writerRunning;
        volatile bool requestedStop;
        private readonly object ffmpegLock = new object();

        int captureChannels;
        int inputSampleRate;

        // Start capture
        public void StartCapture(string outPath, int quality = 10)
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Logging.Error("[AudioCapture] No AudioSource found.");
                return;
            }

            outputFilePath = outPath;
            ffmpegQuality = quality;
            captureChannels = audioSource.clip != null ? audioSource.clip.channels : AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            inputSampleRate = AudioSettings.outputSampleRate;

            StartFfmpegWriter();
        }

        void StartFfmpegWriter()
        {
            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
            {
                Logging.Warn("[AudioCapture] ffmpeg not found. Capture disabled.");
                return;
            }

            // ffmpeg accepts float32 little-endian raw input (-f f32le)
            // input frequency = inputSampleRate, channels = captureChannels
            // output — ogg vorbis, resamples to targetRate
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-f f32le -ar {inputSampleRate} -ac {captureChannels} -i - -c:a libvorbis -q:a {ffmpegQuality} -ar {targetRate} \"{outputFilePath}\" -y",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                ffmpegProc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                ffmpegProc.Start();

                writerRunning = true;
                requestedStop = false;
                writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "IAYT-AudioWriter" };
                writerThread.Start();

                // Do not block; read stderr in background and log
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        string err = ffmpegProc.StandardError.ReadToEnd();
                        if (!string.IsNullOrEmpty(err))
                            Logging.Warn($"[AudioCapture][ffmpeg] stderr: {err}");
                    }
                    catch { }
                });

                Logging.Warn($"[AudioCapture] ffmpeg started, writing to {outputFilePath}");
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioCapture] Failed to start ffmpeg: {e}");
                writerRunning = false;
                requestedStop = true;
            }
        }

        void WriterLoop()
        {
            Process localFfmpegProc = null;
            lock (ffmpegLock)
            {
                localFfmpegProc = ffmpegProc;
            }

            if (localFfmpegProc == null)
            {
                writerRunning = false;
                return;
            }

            try
            {
                using (var stdin = localFfmpegProc.StandardInput.BaseStream)
                {
                    while (!requestedStop || !queue.IsEmpty)
                    {
                        if (queue.TryDequeue(out var chunk))
                        {
                            try
                            {
                                stdin.Write(chunk, 0, chunk.Length);
                            }
                            catch (Exception e)
                            {
                                Logging.Error($"[AudioCapture] Write to ffmpeg failed: {e}");
                                break;
                            }
                        }
                        else
                        {
                            Thread.Sleep(2);
                        }
                    }

                    // make sure everything is sent
                    try
                    {
                        stdin.Flush();
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioCapture] WriterLoop exception: {e}");
            }
            finally
            {
                // close stdin (if process is alive)
                try
                {
                    lock (ffmpegLock)
                    {
                        if (ffmpegProc != null && !ffmpegProc.HasExited)
                        {
                            // close standard input so ffmpeg finishes encoding
                            try { ffmpegProc.StandardInput.Close(); } catch { }
                            // wait for a reasonable time
                            ffmpegProc.WaitForExit(5000);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.Warn($"[AudioCapture] Error while finalizing ffmpeg: {e}");
                }

                writerRunning = false;
            }
        }

        // Stop capture (can be called externally, but the component will stop itself when the source stops playing)
        public void StopCapture()
        {
            requestedStop = true;
            // wait for the thread
            int wait = 0;
            while (writerRunning && wait < 200) // ~200*50ms = 10s max
            {
                Thread.Sleep(50);
                wait++;
            }

            // If the process is still alive -  wait a little longer.
            lock (ffmpegLock)
            {
                try
                {
                    if (ffmpegProc != null && !ffmpegProc.HasExited)
                    {
                        ffmpegProc.WaitForExit(2000);
                    }
                }
                catch { }

                // clear
                try { ffmpegProc?.Dispose(); } catch { }
                ffmpegProc = null;
            }
            writerThread = null;
            Logging.Warn("[AudioCapture] Stopped capture.");
        }

        // Copy the float[] block from the audio and put it in the queue as a f32le
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!writerRunning) return;
            
            Process localFfmpegProc;
            lock (ffmpegLock)
            {
                localFfmpegProc = ffmpegProc;
            }
            
            if (localFfmpegProc == null || localFfmpegProc.HasExited)
            {
                requestedStop = true;
                return;
            }

            try
            {
                // copying the array (so as not to depend on overused buffers)
                var local = new float[data.Length];
                Array.Copy(data, local, data.Length);

                // converting float[] -> bytes (f32 little-endian)
                byte[] bytes = new byte[local.Length * 4];
                Buffer.BlockCopy(local, 0, bytes, 0, bytes.Length);

                queue.Enqueue(bytes);
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioCapture] OnAudioFilterRead error: {e}");
                requestedStop = true;
            }
        }

        void Update()
        {
            // Auto-stop: if the source has stopped playing, we end
            if (!writerRunning) return;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null && !audioSource.isPlaying && !requestedStop)
            {
                // we give another 100-200 ms to finish the queue.
                requestedStop = true;
                Logging.Warn("[AudioCapture] AudioSource stopped — finishing capture.");
            }
        }

        void OnDestroy()
        {
            requestedStop = true;
            // Let's wait short
            try
            {
                writerThread?.Join(500);
            }
            catch { }
            
            lock (ffmpegLock)
            {
                try
                {
                    if (ffmpegProc != null && !ffmpegProc.HasExited)
                    {
                        try { ffmpegProc.Kill(); } catch { }
                    }
                }
                catch { }
            }
        }

        private string FindFfmpeg()
        {
            // search for ffmpeg in several places
            string basePath = Application.dataPath;
            string[] candidates = new[]
            {
                Path.Combine(basePath, "StreamingAssets", "PlatformSpecific_Win", "Support", "ThirdParty", "ffmpeg.exe"),
                Path.Combine(basePath, "..", "ffmpeg.exe"),
                Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe"),
                Path.Combine(Application.dataPath, "..", "ffmpeg.exe")
            };
            foreach (var p in candidates)
            {
                try
                {
                    if (File.Exists(p)) return Path.GetFullPath(p);
                }
                catch { }
            }

            // If haven't found it - try PATH.
            try
            {
                var proc = new ProcessStartInfo { FileName = "ffmpeg", Arguments = "-version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using var p = Process.Start(proc);
                if (p != null) { p.Kill(); return "ffmpeg"; }
            }
            catch { }

            return null;
        }
    }
}
