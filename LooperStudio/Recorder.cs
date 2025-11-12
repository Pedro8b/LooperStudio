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
        public Recorder()
        { }
        WaveFileWriter writer;
        WaveIn wave;
        public void Record()
        {
            try
            {
                wave = new WaveIn();
                wave.DeviceNumber = 0;
                wave.WaveFormat = new WaveFormat(44100, 16, 1);
                wave.DataAvailable += Wave_DataAvailable;
                wave.RecordingStopped += Wave_RecordingStopped;
                string pathToDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = pathToDesktop + "/ExampleRecording.wav";

                // Проверяем, не используется ли файл другим процессом
                if (File.Exists(filePath))
                {
                    try
                    {
                        using (var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            fileStream.Close();
                        }
                        // Если файл не заблокирован, удаляем его
                        File.Delete(filePath);
                    }
                    catch (IOException)
                    {
                        // Файл занят другим процессом - генерируем уникальное имя
                        filePath = GetUniqueFilePath(filePath);
                    }
                }

                writer = new WaveFileWriter(filePath, wave.WaveFormat);
                wave.StartRecording();
            }
            catch (Exception ex)
            {
                // Логирование или обработка ошибки инициализации записи
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
                if (wave != null)
                {
                    wave.StopRecording();
                }
            }
            catch (Exception ex)
            {
                // Логирование или обработка ошибки остановки записи
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
                }
            }
            catch (Exception ex)
            {
                // Логирование или обработка ошибки записи данных
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
                // Логирование или обработка ошибки освобождения ресурсов
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