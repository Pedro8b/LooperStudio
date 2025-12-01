using System;

namespace LooperStudio
{
    /// <summary>
    /// Представляет аудио-семпл на таймлайне
    /// </summary>
    [Serializable]
    public class AudioSample
    {
        public string FilePath { get; set; }
        public string Name { get; set; }
        public double StartTime { get; set; } // Позиция на таймлайне в секундах
        public int TrackNumber { get; set; } // Номер трека (0, 1, 2...)
        public double Duration { get; set; } // Длительность в секундах
        public float Volume { get; set; } = 1.0f; // Громкость 0.0 - 1.0
        public Guid Id { get; set; }

        public AudioSample()
        {
            Id = Guid.NewGuid();
        }

        public AudioSample(string filePath, string name, double startTime, int trackNumber)
        {
            Id = Guid.NewGuid();
            FilePath = filePath;
            Name = name;
            StartTime = startTime;
            TrackNumber = trackNumber;
            Volume = 1.0f;
        }
    }
}