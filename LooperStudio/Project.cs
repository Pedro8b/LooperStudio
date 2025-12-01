using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LooperStudio
{
    /// <summary>
    /// Представляет проект DAW
    /// </summary>
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

        public Project()
        {
            Name = "Новый проект";
        }

        /// <summary>
        /// Сохранить проект в JSON файл
        /// </summary>
        public void Save(string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Загрузить проект из JSON файла
        /// </summary>
        public static Project Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Project>(json);
        }
    }
}