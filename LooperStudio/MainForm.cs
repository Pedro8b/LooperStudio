using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
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

            recordInstance.ProjectFolder = projectFolder;

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

            // Добавляем горячие клавиши
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
        }

        // Обработчики кнопок
        private void PlayButton_Click(object sender, EventArgs e)
        {
            PlayProject();
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            StopProject();
            recordInstance.StopRecording();

            // Получаем путь к последнему записанному файлу
            lastRecordedFile = recordInstance.LastRecordedFile;

            if (!string.IsNullOrEmpty(lastRecordedFile) && File.Exists(lastRecordedFile))
            {
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

        private void RecordButton_Click(object sender, EventArgs e)
        {
            recordInstance.Record();
            MessageBox.Show("Идет запись... Нажмите Stop для завершения.", "Запись");
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
                mixerEngine.OutputDeviceNumber))
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    // Сохраняем выбранные устройства
                    recordInstance.InputDeviceNumber = settingsForm.SelectedInputDevice;
                    mixerEngine.SetOutputDevice(settingsForm.SelectedOutputDevice);

                    MessageBox.Show("Настройки аудиоустройств сохранены!", "Настройки");
                }
            }
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
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveButton_Click(sender, e);
                e.Handled = true;
            }
        }

    }
}