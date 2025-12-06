using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LooperStudio
{
    public partial class MainForm : Form
    {
        private Recorder recordInstance;
        private MixerEngine mixerEngine;
        private Project currentProject;
        private TimelineControl timeline;
        private string lastRecordedFile;
        private bool Recording = false;

        public MainForm()
        {
            InitializeComponent();
            InitializeDAW();
        }

        private void InitializeDAW()
        {
            // Создаем новый проект
            currentProject = new Project();

            // Инициализируем компоненты
            recordInstance = new Recorder();

            // Устанавливаем папку проекта для записей
            string projectFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LooperStudio Projects");

            if (!Directory.Exists(projectFolder))
            {
                Directory.CreateDirectory(projectFolder);
            }

            // Устанавливаем папку семплов
            if (string.IsNullOrEmpty(currentProject.SamplesFolder))
            {
                currentProject.SamplesFolder = Path.Combine(projectFolder, "Samples");
            }

            if (!Directory.Exists(currentProject.SamplesFolder))
            {
                Directory.CreateDirectory(currentProject.SamplesFolder);
            }

            recordInstance.ProjectFolder = currentProject.SamplesFolder;

            mixerEngine = new MixerEngine();

            // Настраиваем форму
            this.Text = "Looper Studio - " + currentProject.Name;

            // Создаем таймлайн
            timeline = new TimelineControl
            {
                Location = new System.Drawing.Point(0, 0)
            };
            timeline.SetProject(currentProject);
            timeline.SampleSelected += (s, sample) =>
            {
                Console.WriteLine($"Selected: {sample.Name}");
            };
            timelinePanel.Controls.Add(timeline);

            // Устанавливаем начальные значения в UI
            bpmNumeric.Value = currentProject.BPM;
            snapToGridCheckbox.Checked = currentProject.SnapToGrid;
            UpdateGridDivisionFromProject();

            // Загружаем семплы из папки
            LoadSamplesFromFolder();

            // Добавляем горячие клавиши
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
        }

        private void LoadSamplesFromFolder()
        {
            if (string.IsNullOrEmpty(currentProject.SamplesFolder) || !Directory.Exists(currentProject.SamplesFolder))
                return;

            try
            {
                // Поддерживаемые форматы
                string[] supportedExtensions = { "*.wav", "*.mp3", "*.aiff", "*.flac", "*.ogg" };

                var audioFiles = new List<string>();

                // Ищем файлы с поддерживаемыми расширениями
                foreach (var extension in supportedExtensions)
                {
                    audioFiles.AddRange(Directory.GetFiles(currentProject.SamplesFolder, extension, SearchOption.AllDirectories));
                }

                // Добавляем в библиотеку
                foreach (var filePath in audioFiles.OrderBy(f => Path.GetFileName(f)))
                {
                    if (!sampleLibrary.Items.Contains(filePath))
                    {
                        sampleLibrary.Items.Add(filePath);
                    }
                }

                sampleLibrary.Refresh();

                Debug.WriteLine($"Загружено {audioFiles.Count} семплов из папки {currentProject.SamplesFolder}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки семплов из папки: {ex.Message}");
            }
        }

        private void UpdateGridDivisionFromProject()
        {
            switch (currentProject.GridDivision)
            {
                case 4: gridDivisionCombo.SelectedIndex = 0; break;
                case 8: gridDivisionCombo.SelectedIndex = 1; break;
                case 16: gridDivisionCombo.SelectedIndex = 2; break;
                case 32: gridDivisionCombo.SelectedIndex = 3; break;
                default: gridDivisionCombo.SelectedIndex = 0; break;
            }
        }

        // Обработчики кнопок
        private void PlayButton_Click(object sender, EventArgs e)
        {
            PlayProject();
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            StopProject();
        }

        private void RecordButton_Click(object sender, EventArgs e)
        {
            if (!Recording)
            {
                Recording = true;
                MessageBox.Show("Идет запись... Нажмите Record ещё раз для завершения.", "Запись");
                recordInstance.Record();
                recordButton.ForeColor = System.Drawing.Color.Red;
            }
            else
            {
                recordInstance.StopRecording();

                // Получаем путь к последнему записанному файлу
                lastRecordedFile = recordInstance.LastRecordedFile;

                if (!string.IsNullOrEmpty(lastRecordedFile) && File.Exists(lastRecordedFile))
                {
                    Recording = false;
                    recordButton.ForeColor = System.Drawing.Color.White;
                    var result = MessageBox.Show(
                        "Запись завершена! Добавить в библиотеку семплов?",
                        "Запись завершена",
                        MessageBoxButtons.YesNo);

                    if (result == DialogResult.Yes)
                    {
                        AddRecordedSampleToLibrary(lastRecordedFile);
                    }
                }
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Looper Studio Project|*.lsp|All Files|*.*";
                saveFileDialog.DefaultExt = "lsp";
                saveFileDialog.FileName = currentProject.Name + ".lsp";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Сохраняем настройки устройств в проект
                        currentProject.InputDeviceNumber = recordInstance.InputDeviceNumber;
                        currentProject.OutputDeviceNumber = mixerEngine.OutputDeviceNumber;

                        currentProject.Save(saveFileDialog.FileName);
                        MessageBox.Show("Проект успешно сохранен!", "Сохранение");
                        this.Text = "Looper Studio - " + Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка");
                    }
                }
            }
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Looper Studio Project|*.lsp|All Files|*.*";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        currentProject = Project.Load(openFileDialog.FileName);

                        // Восстанавливаем настройки устройств
                        recordInstance.InputDeviceNumber = currentProject.InputDeviceNumber;
                        mixerEngine.SetOutputDevice(currentProject.OutputDeviceNumber);

                        // Восстанавливаем папку семплов
                        if (!string.IsNullOrEmpty(currentProject.SamplesFolder))
                        {
                            recordInstance.ProjectFolder = currentProject.SamplesFolder;
                            if (!Directory.Exists(currentProject.SamplesFolder))
                            {
                                Directory.CreateDirectory(currentProject.SamplesFolder);
                            }
                        }

                        // Обновляем UI
                        bpmNumeric.Value = currentProject.BPM;
                        snapToGridCheckbox.Checked = currentProject.SnapToGrid;
                        UpdateGridDivisionFromProject();

                        // Очищаем и перезагружаем библиотеку семплов
                        sampleLibrary.Items.Clear();
                        LoadSamplesFromFolder();

                        timeline.SetProject(currentProject);
                        this.Text = "Looper Studio - " + Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                        MessageBox.Show("Проект успешно загружен!", "Загрузка");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка");
                    }
                }
            }
        }

        private void AddSampleButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.flac;*.ogg|All Files|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        if (!sampleLibrary.Items.Contains(filePath))
                        {
                            sampleLibrary.Items.Add(filePath);
                        }
                    }
                    sampleLibrary.Refresh();
                }
            }
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new SettingsForm(
                recordInstance.InputDeviceNumber,
                mixerEngine.OutputDeviceNumber,
                currentProject.SamplesFolder))
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    // Сохраняем выбранные устройства
                    recordInstance.InputDeviceNumber = settingsForm.SelectedInputDevice;
                    mixerEngine.SetOutputDevice(settingsForm.SelectedOutputDevice);

                    // Проверяем изменилась ли папка семплов
                    bool folderChanged = currentProject.SamplesFolder != settingsForm.SamplesFolder;

                    // Сохраняем папку семплов
                    currentProject.SamplesFolder = settingsForm.SamplesFolder;
                    recordInstance.ProjectFolder = currentProject.SamplesFolder;

                    // Создаём папку если её нет
                    if (!Directory.Exists(currentProject.SamplesFolder))
                    {
                        Directory.CreateDirectory(currentProject.SamplesFolder);
                    }

                    // Если папка изменилась, перезагружаем семплы
                    if (folderChanged)
                    {
                        sampleLibrary.Items.Clear();
                        LoadSamplesFromFolder();
                    }

                    MessageBox.Show("Настройки сохранены!", "Настройки");
                }
            }
        }

        // Обработчики настроек сетки и BPM
        private void BpmNumeric_ValueChanged(object sender, EventArgs e)
        {
            currentProject.BPM = (int)bpmNumeric.Value;
            timeline.Invalidate();
        }

        private void SnapToGridCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            currentProject.SnapToGrid = snapToGridCheckbox.Checked;
            timeline.Invalidate();
        }

        private void GridDivisionCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (gridDivisionCombo.SelectedIndex)
            {
                case 0: currentProject.GridDivision = 4; break;
                case 1: currentProject.GridDivision = 8; break;
                case 2: currentProject.GridDivision = 16; break;
                case 3: currentProject.GridDivision = 32; break;
            }
            timeline.Invalidate();
        }

        // Обработчики ListBox
        private void SampleLibrary_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            string fullPath = sampleLibrary.Items[e.Index].ToString();
            string fileName = Path.GetFileName(fullPath);

            using (var brush = new System.Drawing.SolidBrush(e.ForeColor))
            {
                e.Graphics.DrawString(fileName, e.Font, brush, e.Bounds);
            }

            e.DrawFocusRectangle();
        }

        private void SampleLibrary_DoubleClick(object sender, EventArgs e)
        {
            if (sampleLibrary.SelectedItem != null)
            {
                string filePath = sampleLibrary.SelectedItem.ToString();
                AddSampleToTimeline(filePath);
            }
        }

        private void SampleLibrary_MouseDown(object sender, MouseEventArgs e)
        {
            int index = sampleLibrary.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                string filePath = sampleLibrary.Items[index].ToString();
                sampleLibrary.DoDragDrop(filePath, DragDropEffects.Copy);
            }
        }

        // Вспомогательные методы
        private void AddRecordedSampleToLibrary(string filePath)
        {
            if (!sampleLibrary.Items.Contains(filePath))
            {
                sampleLibrary.Items.Add(filePath);
                sampleLibrary.Refresh();
            }
        }

        private void AddSampleToTimeline(string filePath)
        {
            try
            {
                using (var audioFile = new AudioFileReader(filePath))
                {
                    double duration = audioFile.TotalTime.TotalSeconds;

                    var sample = new AudioSample
                    {
                        FilePath = filePath,
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        StartTime = 0,
                        TrackNumber = 0,
                        Duration = duration
                    };

                    currentProject.Samples.Add(sample);
                    timeline.SetProject(currentProject);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления семпла: {ex.Message}", "Ошибка");
            }
        }

        private void PlayProject()
        {
            try
            {
                mixerEngine.Play(currentProject);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка воспроизведения: {ex.Message}", "Ошибка");
            }
        }

        private void StopProject()
        {
            mixerEngine.Stop();
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "WAV Audio|*.wav|MP3 Audio|*.mp3";
                saveFileDialog.DefaultExt = "wav";
                saveFileDialog.FileName = currentProject.Name + "_export";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string extension = Path.GetExtension(saveFileDialog.FileName).ToLower();

                        if (extension == ".wav")
                        {
                            ExportToWav(saveFileDialog.FileName);
                        }
                        else if (extension == ".mp3")
                        {
                            MessageBox.Show("MP3 экспорт требует дополнительных кодеков (LAME).\nПока доступен только WAV экспорт.", "Информация");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка");
                    }
                }
            }
        }

        private void ExportToWav(string filePath)
        {
            if (currentProject.Samples.Count == 0)
            {
                MessageBox.Show("Нет семплов для экспорта!", "Ошибка");
                return;
            }

            // Находим максимальное время
            double maxTime = 0;
            foreach (var sample in currentProject.Samples)
            {
                double endTime = sample.StartTime + sample.Duration;
                if (endTime > maxTime)
                    maxTime = endTime;
            }

            // Создаем микшер для рендеринга
            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            mixer.ReadFully = true;

            // Загружаем все семплы
            foreach (var sample in currentProject.Samples)
            {
                try
                {
                    var audioFile = new AudioFileReader(sample.FilePath);
                    var resampler = new MediaFoundationResampler(audioFile,
                        WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));

                    var volumeProvider = new VolumeSampleProvider(resampler.ToSampleProvider())
                    {
                        Volume = sample.Volume
                    };

                    var offsetProvider = new OffsetSampleProvider(volumeProvider)
                    {
                        DelayBy = TimeSpan.FromSeconds(sample.StartTime)
                    };

                    mixer.AddMixerInput(offsetProvider);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка загрузки семпла {sample.Name}: {ex.Message}");
                }
            }

            // Записываем в файл
            int totalSamples = (int)(maxTime * 44100 * 2); // stereo

            using (var writer = new WaveFileWriter(filePath, mixer.WaveFormat))
            {
                float[] buffer = new float[44100 * 2]; // 1 секунда буфер
                int samplesRead;
                int totalRead = 0;

                while (totalRead < totalSamples && (samplesRead = mixer.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.WriteSamples(buffer, 0, samplesRead);
                    totalRead += samplesRead;
                }
            }

            MessageBox.Show($"Проект успешно экспортирован!\n{filePath}", "Экспорт завершен");
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                if (mixerEngine.IsPlaying)
                    mixerEngine.Pause();
                else
                    PlayProject();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                timeline.DeleteSelectedSample();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                timeline.CopySelectedSample();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                timeline.PasteSample();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveButton_Click(sender, e);
                e.Handled = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                mixerEngine?.Dispose();
                recordInstance = null;
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}