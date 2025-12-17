using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LooperStudio
{
    

    /// Представляет проект DAW
    
    [Serializable]
    public class Project
    {
        public string Name { get; set; }
        public int BPM { get; set; } = 120;
        public List<AudioSample> Samples { get; set; } = new List<AudioSample>();
        public int TrackCount { get; set; } = 8;

        // Настройки аудиоустройств
        public int InputDeviceNumber { get; set; } = 0;
        public int OutputDeviceNumber { get; set; } = 0;

        // Папка семплов
        public string SamplesFolder { get; set; } = "";

        // Настройки сетки
        public bool SnapToGrid { get; set; } = false;
        public int GridDivision { get; set; } = 4; // 1/4 ноты (четвертные)

        public Project()
        {
            Name = "Новый проект";
        }

        
        /// Получить размер одной доли сетки в секундах
        
        public double GetGridSize()
        {
            // Длительность одного бита в секундах
            double beatDuration = 60.0 / BPM;
            // Размер сетки зависит от деления (1/4, 1/8, 1/16)
            return beatDuration / (GridDivision / 4.0);
        }

        
        /// Сохранить проект в JSON файл
        
        public void Save(string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }

        
        /// Загрузить проект из JSON файла
        
        public static Project Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Project>(json);
        }
    }
}