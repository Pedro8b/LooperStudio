using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LooperStudio
{
    
    /// Движок для воспроизведения и микширования нескольких семплов одновременно
    
    public class MixerEngine : IDisposable
    {
        private WaveOutEvent outputDevice;
        private MixingSampleProvider mixer;
        private List<CachedSoundSampleProvider> activeSounds;
        private bool isPlaying;
        private double currentTime;
        private System.Timers.Timer playbackTimer;
        private List<PausableSampleProvider> pausableSamples;
        private List<OffsetSampleProvider> offsetProviders;
        private double projectDuration;

        public bool IsPlaying => isPlaying;
        public double CurrentTime => currentTime;
        public double ProjectDuration => projectDuration;
        public int OutputDeviceNumber { get; set; } = 0;
        public PlaybackState CurrentPlaybackState => outputDevice?.PlaybackState ?? PlaybackState.Stopped;

        public event EventHandler PlaybackStopped;
        public event EventHandler<double> PlaybackPositionChanged;

        public MixerEngine()
        {
            activeSounds = new List<CachedSoundSampleProvider>();
            pausableSamples = new List<PausableSampleProvider>();
            offsetProviders = new List<OffsetSampleProvider>();
            InitializeOutput();
        }

        private void InitializeOutput()
        {
            outputDevice = new WaveOutEvent();
            outputDevice.DeviceNumber = OutputDeviceNumber;

            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            mixer.ReadFully = true;

            outputDevice.Init(mixer);

            playbackTimer = new System.Timers.Timer(50); // Обновляем каждые 50мс для плавности
            playbackTimer.Elapsed += (s, e) =>
            {
                if (isPlaying)
                {
                    currentTime += 0.05;
                    PlaybackPositionChanged?.Invoke(this, currentTime);
                }
            };

            outputDevice.PlaybackStopped += (s, e) =>
            {
                isPlaying = false;
                playbackTimer.Stop();
                PlaybackStopped?.Invoke(this, EventArgs.Empty);
            };
        }

        public void SetOutputDevice(int deviceNumber)
        {
            Stop();
            OutputDeviceNumber = deviceNumber;

            outputDevice?.Dispose();
            InitializeOutput();
        }

        
        /// Воспроизвести проект с указанной позиции
        
        public void Play(Project project, double startTime = 0)
        {
            Stop();
            currentTime = startTime;

            Debug.WriteLine($"=== НАЧАЛО ВОСПРОИЗВЕДЕНИЯ С ПОЗИЦИИ {startTime}s ===");

            pausableSamples.Clear();
            offsetProviders.Clear();

            // Вычисляем длительность проекта
            projectDuration = 0;
            foreach (var sample in project.Samples)
            {
                double endTime = sample.StartTime + sample.Duration;
                if (endTime > projectDuration)
                    projectDuration = endTime;
            }

            foreach (var sample in project.Samples)
            {
                try
                {
                    // Пропускаем семплы, которые уже закончились
                    if (sample.StartTime + sample.Duration <= startTime)
                    {
                        Debug.WriteLine($"Пропускаем семпл {sample.Name} (уже закончился)");
                        continue;
                    }

                    Debug.WriteLine($"Загрузка семпла: {sample.Name}");
                    Debug.WriteLine($"  StartTime: {sample.StartTime}s, Duration: {sample.Duration}s");

                    ScheduleSample(sample, startTime);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка загрузки семпла {sample.Name}: {ex.Message}");
                }
            }

            isPlaying = true;
            playbackTimer.Start();
            outputDevice.Play();
        }

        
        /// Запланировать воспроизведение семпла с учётом стартовой позиции
        
        private void ScheduleSample(AudioSample sample, double playbackStartTime)
        {
            try
            {
                var audioFile = new AudioFileReader(sample.FilePath);
                var resampler = new MediaFoundationResampler(audioFile, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));

                double sampleStartInPlayback = sample.StartTime;
                double sampleEndInPlayback = sample.StartTime + sample.Duration;

                // Если семпл должен начаться после текущей позиции
                if (sampleStartInPlayback >= playbackStartTime)
                {
                    // Воспроизводим с начала семпла, но с задержкой
                    double delay = sampleStartInPlayback - playbackStartTime;

                    var skipTakeProvider = new SkipTakeSampleProvider(
                        resampler.ToSampleProvider(),
                        TimeSpan.FromSeconds(sample.FileOffset),
                        TimeSpan.FromSeconds(sample.Duration));

                    var volumeProvider = new VolumeSampleProvider(skipTakeProvider)
                    {
                        Volume = sample.Volume
                    };

                    var pausableProvider = new PausableSampleProvider(volumeProvider);
                    pausableSamples.Add(pausableProvider);

                    var offsetProvider = new OffsetSampleProvider(pausableProvider)
                    {
                        DelayBy = TimeSpan.FromSeconds(delay)
                    };
                    offsetProviders.Add(offsetProvider);

                    mixer.AddMixerInput(offsetProvider);

                    Debug.WriteLine($"Семпл {sample.Name} запланирован с задержкой {delay}s");
                }
                else
                {
                    // Семпл уже должен играть - начинаем с середины
                    double timeIntoSample = playbackStartTime - sampleStartInPlayback;
                    double remainingDuration = sample.Duration - timeIntoSample;

                    if (remainingDuration > 0)
                    {
                        var skipTakeProvider = new SkipTakeSampleProvider(
                            resampler.ToSampleProvider(),
                            TimeSpan.FromSeconds(sample.FileOffset + timeIntoSample),
                            TimeSpan.FromSeconds(remainingDuration));

                        var volumeProvider = new VolumeSampleProvider(skipTakeProvider)
                        {
                            Volume = sample.Volume
                        };

                        var pausableProvider = new PausableSampleProvider(volumeProvider);
                        pausableSamples.Add(pausableProvider);

                        var offsetProvider = new OffsetSampleProvider(pausableProvider)
                        {
                            DelayBy = TimeSpan.Zero
                        };
                        offsetProviders.Add(offsetProvider);

                        mixer.AddMixerInput(offsetProvider);

                        Debug.WriteLine($"Семпл {sample.Name} начинается с позиции {timeIntoSample}s");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки семпла {sample.Name}: {ex.Message}");
            }
        }

        
        /// Перемотать на указанную позицию
        
        public void Seek(Project project, double time)
        {
            bool wasPlaying = isPlaying;
            Play(project, time);

            if (!wasPlaying)
            {
                // Если не играло, сразу ставим на паузу
                Pause();
            }
        }

        
        /// Остановить воспроизведение
        
        public void Stop()
        {
            if (outputDevice != null)
            {
                outputDevice.Stop();
            }

            isPlaying = false;
            currentTime = 0;
            playbackTimer.Stop();

            mixer.RemoveAllMixerInputs();
            activeSounds.Clear();
            pausableSamples.Clear();
            offsetProviders.Clear();
        }

        
        /// Пауза
        
        public void Pause()
        {
            if (outputDevice != null && isPlaying)
            {
                Debug.WriteLine("=== ПАУЗА ===");

                foreach (var pausable in pausableSamples)
                {
                    pausable.Pause();
                }

                outputDevice.Pause();
                isPlaying = false;
                playbackTimer.Stop();

                Debug.WriteLine($"Пауза на позиции: {currentTime}s");
            }
        }

        
        /// Продолжить воспроизведение <summary>
        /// Продолжить воспроизведение
                

        public void Resume()
        {
            if (outputDevice != null && !isPlaying && outputDevice.PlaybackState == PlaybackState.Paused)
            {
                Debug.WriteLine("=== ПРОДОЛЖЕНИЕ ===");

                foreach (var pausable in pausableSamples)
                {
                    pausable.Resume();
                }

                outputDevice.Play();
                isPlaying = true;
                playbackTimer.Start();

                Debug.WriteLine($"Продолжение с позиции: {currentTime}s");
            }
        }

        public void Dispose()
        {
            playbackTimer?.Dispose();
            outputDevice?.Dispose();
            activeSounds.Clear();
            pausableSamples.Clear();
            offsetProviders.Clear();
        }
    }

    



    /// Sample provider с поддержкой паузы
    
    public class PausableSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private bool isPaused;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public PausableSampleProvider(ISampleProvider source)
        {
            sourceProvider = source;
            isPaused = false;
        }

        public void Pause()
        {
            isPaused = true;
        }

        public void Resume()
        {
            isPaused = false;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (isPaused)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            return sourceProvider.Read(buffer, offset, count);
        }
    }

    
    /// Sample provider с пропуском начала и ограничением длительности
    
    public class SkipTakeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private long samplesToSkip;
        private long samplesRemaining;
        private bool skipped = false;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public SkipTakeSampleProvider(ISampleProvider source, TimeSpan skipDuration, TimeSpan takeDuration)
        {
            sourceProvider = source;
            samplesToSkip = (long)(skipDuration.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
            samplesRemaining = (long)(takeDuration.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (!skipped && samplesToSkip > 0)
            {
                float[] skipBuffer = new float[WaveFormat.SampleRate * WaveFormat.Channels];
                long remaining = samplesToSkip;

                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(skipBuffer.Length, remaining);
                    int read = sourceProvider.Read(skipBuffer, 0, toRead);
                    if (read == 0) break;
                    remaining -= read;
                }

                skipped = true;
            }

            if (samplesRemaining <= 0)
            {
                return 0;
            }

            int samplesToRead = (int)Math.Min(count, samplesRemaining);
            int samplesRead = sourceProvider.Read(buffer, offset, samplesToRead);

            samplesRemaining -= samplesRead;

            return samplesRead;
        }
    }

    
    /// Sample provider с задержкой старта
    
    public class OffsetSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private int delayBySamples;
        private int delayRemaining;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public TimeSpan DelayBy
        {
            get => TimeSpan.FromSeconds((double)delayBySamples / WaveFormat.SampleRate / WaveFormat.Channels);
            set
            {
                delayBySamples = (int)(value.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
                delayRemaining = delayBySamples;
            }
        }

        public OffsetSampleProvider(ISampleProvider source)
        {
            sourceProvider = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int written = 0;

            if (delayRemaining > 0)
            {
                int delaySamplesToWrite = Math.Min(delayRemaining, count);
                for (int i = 0; i < delaySamplesToWrite; i++)
                {
                    buffer[offset + i] = 0;
                }
                delayRemaining -= delaySamplesToWrite;
                written += delaySamplesToWrite;
                offset += delaySamplesToWrite;
                count -= delaySamplesToWrite;
            }

            if (count > 0)
            {
                written += sourceProvider.Read(buffer, offset, count);
            }

            return written;
        }
    }

    
    /// Кэшированный звук для эффективного воспроизведения
    
    public class CachedSoundSampleProvider : ISampleProvider
    {
        private readonly float[] audioData;
        private long position;

        public WaveFormat WaveFormat { get; private set; }

        public CachedSoundSampleProvider(AudioFileReader audioFile)
        {
            WaveFormat = audioFile.WaveFormat;

            var wholeFile = new List<float>();
            var readBuffer = new float[audioFile.WaveFormat.SampleRate * audioFile.WaveFormat.Channels];
            int samplesRead;
            while ((samplesRead = audioFile.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                wholeFile.AddRange(readBuffer.Take(samplesRead));
            }
            audioData = wholeFile.ToArray();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var availableSamples = audioData.Length - position;
            var samplesToCopy = Math.Min(availableSamples, count);
            Array.Copy(audioData, position, buffer, offset, samplesToCopy);
            position += samplesToCopy;
            return (int)samplesToCopy;
        }

        public void Reset()
        {
            position = 0;
        }
    }
}