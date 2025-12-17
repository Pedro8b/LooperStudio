using System;

namespace LooperStudio
{
    


    /// Представляет аудио-семпл на таймлайне
    
    [Serializable]
    public class AudioSample
    {
        public string FilePath { get; set; }
        public string Name { get; set; }
        public double StartTime { get; set; } // Позиция на таймлайне в секундах
        public int TrackNumber { get; set; } // Номер трека (0, 1, 2...)
        public double Duration { get; set; } // Длительность в секундах
        public float Volume { get; set; } = 1.0f; // Громкость 0.0 - 1.0
        public double FileOffset { get; set; } = 0.0; // Смещение от начала файла в секундах (для нарезки)
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
            FileOffset = 0.0;
        }
    }
}