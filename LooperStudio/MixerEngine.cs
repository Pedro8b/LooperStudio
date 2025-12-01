using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LooperStudio
{
    /// <summary>
    /// Движок для воспроизведения и микширования нескольких семплов одновременно
    /// </summary>
    public class MixerEngine : IDisposable
    {
        private WaveOutEvent outputDevice;
        private MixingSampleProvider mixer;
        private List<CachedSoundSampleProvider> activeSounds;
        private bool isPlaying;
        private double currentTime;
        private System.Timers.Timer playbackTimer;

        public bool IsPlaying => isPlaying;
        public double CurrentTime => currentTime;
        public int OutputDeviceNumber { get; set; } = 0;

        public event EventHandler PlaybackStopped;

        public MixerEngine()
        {
            activeSounds = new List<CachedSoundSampleProvider>();
            InitializeOutput();
        }

        private void InitializeOutput()
        {
            // Создаем устройство вывода
            outputDevice = new WaveOutEvent();
            outputDevice.DeviceNumber = OutputDeviceNumber;

            // Создаем микшер (44.1kHz, stereo)
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            mixer.ReadFully = true;

            outputDevice.Init(mixer);

            // Таймер для отслеживания времени воспроизведения
            playbackTimer = new System.Timers.Timer(100); // Обновление каждые 100мс
            playbackTimer.Elapsed += (s, e) =>
            {
                if (isPlaying)
                {
                    currentTime += 0.1;
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

            // Пересоздаём устройство вывода
            outputDevice?.Dispose();
            InitializeOutput();
        }

        /// <summary>
        /// Воспроизвести проект с начала
        /// </summary>
        public void Play(Project project)
        {
            Stop();
            currentTime = 0;

            Debug.WriteLine("=== НАЧАЛО ВОСПРОИЗВЕДЕНИЯ ===");

            // Загружаем все семплы
            foreach (var sample in project.Samples)
            {
                try
                {
                    Debug.WriteLine($"Загрузка семпла: {sample.Name}");
                    Debug.WriteLine($"  StartTime: {sample.StartTime}s");
                    Debug.WriteLine($"  Duration: {sample.Duration}s");
                    Debug.WriteLine($"  Должен закончиться в: {sample.StartTime + sample.Duration}s");

                    ScheduleSample(sample);
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

        /// <summary>
        /// Запланировать воспроизведение семпла
        /// </summary>
        private void ScheduleSample(AudioSample sample)
        {
            try
            {
                Debug.WriteLine($"=== ПЛАНИРОВАНИЕ СЕМПЛА ===");
                Debug.WriteLine($"Имя: {sample.Name}");
                Debug.WriteLine($"Должен начаться в: {sample.StartTime}s");
                Debug.WriteLine($"Длительность: {sample.Duration}s");

                // Загружаем аудиофайл
                var audioFile = new AudioFileReader(sample.FilePath);

                Debug.WriteLine($"Реальная длительность файла: {audioFile.TotalTime.TotalSeconds}s");

                // Конвертируем в нужный формат (44.1kHz stereo)
                var resampler = new MediaFoundationResampler(audioFile, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));

                // Применяем громкость
                var volumeProvider = new VolumeSampleProvider(resampler.ToSampleProvider())
                {
                    Volume = sample.Volume
                };

                // Создаем offset provider для задержки старта
                var offsetProvider = new OffsetSampleProvider(volumeProvider)
                {
                    DelayBy = TimeSpan.FromSeconds(sample.StartTime)
                };

                Debug.WriteLine($"Установлена задержка: {sample.StartTime}s ({TimeSpan.FromSeconds(sample.StartTime)})");

                // Добавляем в микшер
                mixer.AddMixerInput(offsetProvider);

                Debug.WriteLine($"Семпл добавлен в микшер");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки семпла {sample.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Остановить воспроизведение
        /// </summary>
        public void Stop()
        {
            if (outputDevice != null)
            {
                outputDevice.Stop();
            }

            isPlaying = false;
            currentTime = 0;
            playbackTimer.Stop();

            // Очищаем микшер
            mixer.RemoveAllMixerInputs();
            activeSounds.Clear();
        }

        /// <summary>
        /// Пауза
        /// </summary>
        public void Pause()
        {
            if (outputDevice != null && isPlaying)
            {
                outputDevice.Pause();
                isPlaying = false;
                playbackTimer.Stop();
            }
        }

        /// <summary>
        /// Продолжить воспроизведение
        /// </summary>
        public void Resume()
        {
            if (outputDevice != null && !isPlaying)
            {
                outputDevice.Play();
                isPlaying = true;
                playbackTimer.Start();
            }
        }

        public void Dispose()
        {
            playbackTimer?.Dispose();
            outputDevice?.Dispose();
            activeSounds.Clear();
        }
    }

    /// <summary>
    /// Sample provider с задержкой старта
    /// </summary>
    public class OffsetSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private int delayBySamples;
        private int delayRemaining;
        private bool debugLogged = false;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public TimeSpan DelayBy
        {
            get => TimeSpan.FromSeconds((double)delayBySamples / WaveFormat.SampleRate / WaveFormat.Channels);
            set
            {
                delayBySamples = (int)(value.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
                delayRemaining = delayBySamples;

                Debug.WriteLine($"OffsetSampleProvider: Установлена задержка {value.TotalSeconds}s = {delayBySamples} семплов");
            }
        }

        public OffsetSampleProvider(ISampleProvider source)
        {
            sourceProvider = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int written = 0;

            // Если еще есть задержка, заполняем тишиной
            if (delayRemaining > 0)
            {
                if (!debugLogged)
                {
                    Debug.WriteLine($"OffsetSampleProvider: Начинаем задержку, осталось {delayRemaining} семплов");
                    debugLogged = true;
                }

                int delaySamplesToWrite = Math.Min(delayRemaining, count);
                for (int i = 0; i < delaySamplesToWrite; i++)
                {
                    buffer[offset + i] = 0;
                }
                delayRemaining -= delaySamplesToWrite;
                written += delaySamplesToWrite;
                offset += delaySamplesToWrite;
                count -= delaySamplesToWrite;

                if (delayRemaining == 0)
                {
                    Debug.WriteLine($"OffsetSampleProvider: Задержка закончилась, начинаем воспроизведение");
                }
            }

            // Читаем из источника
            if (count > 0)
            {
                written += sourceProvider.Read(buffer, offset, count);
            }

            return written;
        }
    }

    /// <summary>
    /// Кэшированный звук для эффективного воспроизведения
    /// </summary>
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