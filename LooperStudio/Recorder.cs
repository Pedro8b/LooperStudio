using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio;
using NAudio.Wave;
using System.IO;

namespace LooperStudio
{
    public class Recorder
    {
        private WaveFileWriter writer;
        private WaveIn wave;
        private string currentRecordingPath;

        public string ProjectFolder { get; set; }
        public string LastRecordedFile => currentRecordingPath;
        public int InputDeviceNumber { get; set; } = 0;

        public Recorder()
        {
            // По умолчанию используем рабочий стол
            ProjectFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        public void Record()
        {
            try
            {
                wave = new WaveIn();
                wave.DeviceNumber = InputDeviceNumber; // Используем выбранное устройство
                wave.WaveFormat = new WaveFormat(44100, 16, 2); // Stereo для совместимости
                wave.DataAvailable += Wave_DataAvailable;
                wave.RecordingStopped += Wave_RecordingStopped;

                // Создаем папку Recordings если её нет
                string recordingsFolder = Path.Combine(ProjectFolder, "Recordings");
                if (!Directory.Exists(recordingsFolder))
                {
                    Directory.CreateDirectory(recordingsFolder);
                }

                // Генерируем уникальное имя файла с датой и временем
                string fileName = $"Recording_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.wav";
                currentRecordingPath = Path.Combine(recordingsFolder, fileName);

                // Если файл существует, делаем уникальное имя
                if (File.Exists(currentRecordingPath))
                {
                    currentRecordingPath = GetUniqueFilePath(currentRecordingPath);
                }

                writer = new WaveFileWriter(currentRecordingPath, wave.WaveFormat);
                wave.StartRecording();

                Console.WriteLine($"Запись начата: {currentRecordingPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при запуске записи: {ex.Message}");
                CleanupResources();
                throw;
            }
        }

        private string GetUniqueFilePath(string originalPath)
        {
            string directory = Path.GetDirectoryName(originalPath);
            string fileName = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);
            int counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileName}_{counter}{extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        public void StopRecording()
        {
            try
            {
                if (wave != null && wave.WaveFormat != null)
                {
                    wave.StopRecording();
                    Console.WriteLine($"Запись остановлена: {currentRecordingPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при остановке записи: {ex.Message}");
                throw;
            }
        }

        private void Wave_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (writer != null)
                {
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при записи аудиоданных: {ex.Message}");
            }
        }

        private void Wave_RecordingStopped(object sender, StoppedEventArgs e)
        {
            try
            {
                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при освобождении ресурсов: {ex.Message}");
            }
            finally
            {
                CleanupResources();
            }
        }

        private void CleanupResources()
        {
            try
            {
                if (wave != null)
                {
                    wave.Dispose();
                    wave = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при очистке ресурсов: {ex.Message}");
            }
        }
    }
}