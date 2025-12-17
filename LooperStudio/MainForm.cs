using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;

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
        private double splitNum = 0;

        // Для слайдера
        private bool isUserDraggingSlider = false;
        private System.Windows.Forms.Timer uiUpdateTimer;

        public MainForm()
        {
            InitializeComponent();
            InitializeDAW();
            SetupUIUpdateTimer();
        }

        private void SetupUIUpdateTimer()
        {
            // Таймер для обновления UI (слайдер и метки времени)
            uiUpdateTimer = new System.Windows.Forms.Timer();
            uiUpdateTimer.Interval = 50; // Обновляем каждые 50мс
            uiUpdateTimer.Tick += UIUpdateTimer_Tick;
            uiUpdateTimer.Start();
        }

        private void UIUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (mixerEngine.IsPlaying && !isUserDraggingSlider)
            {
                UpdatePlaybackUI();
            }
        }

        private void UpdatePlaybackUI()
        {
            double currentTime = mixerEngine.CurrentTime;
            double totalTime = mixerEngine.ProjectDuration;

            // Обновляем метки времени
            currentTimeLabel.Text = FormatTime(currentTime);
            totalTimeLabel.Text = FormatTime(totalTime);

            // Обновляем слайдер
            if (totalTime > 0)
            {
                int sliderValue = (int)((currentTime / totalTime) * playbackSlider.Maximum);
                sliderValue = Math.Max(playbackSlider.Minimum, Math.Min(playbackSlider.Maximum, sliderValue));
                playbackSlider.Value = sliderValue;
            }

            // Обновляем курсор на таймлайне
            timeline.SetPlaybackPosition(currentTime);
        }

        private string FormatTime(double seconds)
        {
            int minutes = (int)(seconds / 60);
            double secs = seconds % 60;
            return $"{minutes}:{secs:00.0}";
        }

        private void InitializeDAW()
        {
            currentProject = new Project();

            recordInstance = new Recorder();

            string projectFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LooperStudio Projects");

            if (!Directory.Exists(projectFolder))
            {
                Directory.CreateDirectory(projectFolder);
            }

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

            // Подписываемся на события движка
            mixerEngine.PlaybackPositionChanged += MixerEngine_PlaybackPositionChanged;
            mixerEngine.PlaybackStopped += MixerEngine_PlaybackStopped;

            this.Text = "Looper Studio - " + currentProject.Name;

            timeline = new TimelineControl
            {
                Location = new System.Drawing.Point(0, 0)
            };
            timeline.SetProject(currentProject);
            timeline.SampleSelected += (s, sample) =>
            {
                Console.WriteLine($"Selected: {sample.Name}");
            };
            timeline.PlayheadMoved += Timeline_PlayheadMoved;
            timelinePanel.Controls.Add(timeline);

            bpmNumeric.Value = currentProject.BPM;
            snapToGridCheckbox.Checked = currentProject.SnapToGrid;
            UpdateGridDivisionFromProject();

            LoadSamplesFromFolder();

            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            UpdateProjectDuration();
        }

        private void Timeline_PlayheadMoved(object sender, double newPosition)
        {
            // Пользователь перетащил курсор на таймлайне
            mixerEngine.Seek(currentProject, newPosition);
            UpdatePlaybackUI();
        }

        private void MixerEngine_PlaybackPositionChanged(object sender, double position)
        {
            // Обновление происходит через UIUpdateTimer
        }

        private void MixerEngine_PlaybackStopped(object sender, EventArgs e)
        {
            this.Invoke(new Action(() =>
            {
                playButton.Text = "▶ Играть";
            }));
        }

        private void UpdateProjectDuration()
        {
            if (currentProject.Samples.Count == 0)
            {
                totalTimeLabel.Text = "0:00.0";
                return;
            }

            double maxTime = 0;
            foreach (var sample in currentProject.Samples)
            {
                double endTime = sample.StartTime + sample.Duration;
                if (endTime > maxTime)
                    maxTime = endTime;
            }

            totalTimeLabel.Text = FormatTime(maxTime);
        }

        private void PlaybackSlider_MouseDown(object sender, MouseEventArgs e)
        {
            isUserDraggingSlider = true;
        }

        private void PlaybackSlider_MouseUp(object sender, MouseEventArgs e)
        {
            isUserDraggingSlider = false;

            // Применяем новую позицию
            double totalTime = mixerEngine.ProjectDuration;
            if (totalTime > 0)
            {
                double newPosition = ((double)playbackSlider.Value / playbackSlider.Maximum) * totalTime;
                mixerEngine.Seek(currentProject, newPosition);
                timeline.SetPlaybackPosition(newPosition);
            }
        }

        private void PlaybackSlider_Scroll(object sender, EventArgs e)
        {
            if (isUserDraggingSlider)
            {
                // Показываем предпросмотр позиции
                double totalTime = mixerEngine.ProjectDuration;
                if (totalTime > 0)
                {
                    double previewPosition = ((double)playbackSlider.Value / playbackSlider.Maximum) * totalTime;
                    currentTimeLabel.Text = FormatTime(previewPosition);
                    timeline.SetPlaybackPosition(previewPosition);
                }
            }
        }

        private void LoadSamplesFromFolder()
        {
            if (string.IsNullOrEmpty(currentProject.SamplesFolder) || !Directory.Exists(currentProject.SamplesFolder))
                return;

            try
            {
                string[] supportedExtensions = { "*.wav", "*.mp3", "*.aiff", "*.flac", "*.ogg" };

                var audioFiles = new List<string>();

                foreach (var extension in supportedExtensions)
                {
                    audioFiles.AddRange(Directory.GetFiles(currentProject.SamplesFolder, extension, SearchOption.AllDirectories));
                }

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
            if (mixerEngine.IsPlaying)
            {
                mixerEngine.Pause();
                playButton.Text = "▶ Играть";
            }
            else if (mixerEngine.CurrentPlaybackState == PlaybackState.Paused)
            {
                mixerEngine.Resume();
                playButton.Text = "⏸ Пауза";
            }
            else
            {
                PlayProject();
                playButton.Text = "⏸ Пауза";
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            StopProject();
            playButton.Text = "▶ Играть";
        }

        private void RecordButton_Click(object sender, EventArgs e)
        {
            if (!Recording)
            {
                Recording = true;
                MessageBox.Show("Идет запись... Нажмите кнопку записи ещё раз для завершения.", "Запись");
                recordInstance.Record();
                recordButton.ForeColor = System.Drawing.Color.Red;
            }
            else
            {
                recordInstance.StopRecording();

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

                        recordInstance.InputDeviceNumber = currentProject.InputDeviceNumber;
                        mixerEngine.SetOutputDevice(currentProject.OutputDeviceNumber);

                        if (!string.IsNullOrEmpty(currentProject.SamplesFolder))
                        {
                            recordInstance.ProjectFolder = currentProject.SamplesFolder;
                            if (!Directory.Exists(currentProject.SamplesFolder))
                            {
                                Directory.CreateDirectory(currentProject.SamplesFolder);
                            }
                        }

                        bpmNumeric.Value = currentProject.BPM;
                        snapToGridCheckbox.Checked = currentProject.SnapToGrid;
                        UpdateGridDivisionFromProject();

                        sampleLibrary.Items.Clear();
                        LoadSamplesFromFolder();

                        timeline.SetProject(currentProject);
                        UpdateProjectDuration();

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

        private void SplitButton_Click(object sender, EventArgs e)
        {
            using (var SplitForm = new SplitForm())
            {
                if (SplitForm.ShowDialog() == DialogResult.OK && timeline.selectedSample != null)
                {
                    splitNum = SplitForm.split;
                    SplitSample(timeline.selectedSample, splitNum);
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
                    recordInstance.InputDeviceNumber = settingsForm.SelectedInputDevice;
                    mixerEngine.SetOutputDevice(settingsForm.SelectedOutputDevice);

                    bool folderChanged = currentProject.SamplesFolder != settingsForm.SamplesFolder;

                    currentProject.SamplesFolder = settingsForm.SamplesFolder;
                    recordInstance.ProjectFolder = currentProject.SamplesFolder;

                    if (!Directory.Exists(currentProject.SamplesFolder))
                    {
                        Directory.CreateDirectory(currentProject.SamplesFolder);
                    }

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
                UpdateProjectDuration();
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

        private void SplitSample(AudioSample original, double splitTime)
        {
            Debug.WriteLine($"=== РАЗДЕЛЕНИЕ СЕМПЛА ===");
            Debug.WriteLine($"Оригинальный семпл: {original.Name}");
            Debug.WriteLine($"FileOffset: {original.FileOffset}");
            Debug.WriteLine($"Duration: {original.Duration}");
            Debug.WriteLine($"Split position: {splitTime}");

            if (splitTime <= 0 || splitTime >= 1)
            {
                MessageBox.Show("Выберите значение между 1 и 7", "Ошибка");
                return;
            }

            double splitPointInSample = splitTime * original.Duration;

            Debug.WriteLine($"Split point in sample: {splitPointInSample}s");

            double duration1 = splitPointInSample;
            double fileOffset1 = original.FileOffset;

            double duration2 = original.Duration - splitPointInSample;
            double fileOffset2 = original.FileOffset + splitPointInSample;

            Debug.WriteLine($"Часть 1: FileOffset={fileOffset1}s, Duration={duration1}s");
            Debug.WriteLine($"Часть 2: FileOffset={fileOffset2}s, Duration={duration2}s");

            var sample1 = new AudioSample
            {
                FilePath = original.FilePath,
                Name = original.Name + "_Part1",
                StartTime = original.StartTime,
                TrackNumber = original.TrackNumber,
                Duration = duration1,
                Volume = original.Volume,
                FileOffset = fileOffset1
            };

            var sample2 = new AudioSample
            {
                FilePath = original.FilePath,
                Name = original.Name + "_Part2",
                StartTime = original.StartTime + duration1,
                TrackNumber = original.TrackNumber,
                Duration = duration2,
                Volume = original.Volume,
                FileOffset = fileOffset2
            };

            timeline.DeleteSample(original);
            timeline.AddSample(sample1);
            timeline.AddSample(sample2);

            Debug.WriteLine("=== РАЗДЕЛЕНИЕ ЗАВЕРШЕНО ===");
        }

        private void PlayProject()
        {
            try
            {
                UpdateProjectDuration();
                mixerEngine.Play(currentProject, mixerEngine.CurrentTime);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка воспроизведения: {ex.Message}", "Ошибка");
            }
        }

        private void StopProject()
        {
            mixerEngine.Stop();
            timeline.SetPlaybackPosition(0);
            currentTimeLabel.Text = "0:00.0";
            playbackSlider.Value = 0;
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

            double maxTime = 0;
            foreach (var sample in currentProject.Samples)
            {
                double endTime = sample.StartTime + sample.Duration;
                if (endTime > maxTime)
                    maxTime = endTime;
            }

            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            mixer.ReadFully = true;

            foreach (var sample in currentProject.Samples)
            {
                try
                {
                    var audioFile = new AudioFileReader(sample.FilePath);
                    var resampler = new MediaFoundationResampler(audioFile,
                        WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));

                    var skipTakeProvider = new SkipTakeSampleProvider(
                        resampler.ToSampleProvider(),
                        TimeSpan.FromSeconds(sample.FileOffset),
                        TimeSpan.FromSeconds(sample.Duration));

                    var volumeProvider = new VolumeSampleProvider(skipTakeProvider)
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

            int totalSamples = (int)(maxTime * 44100 * 2);

            using (var writer = new WaveFileWriter(filePath, mixer.WaveFormat))
            {
                float[] buffer = new float[44100 * 2];
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
                {
                    mixerEngine.Pause();
                    playButton.Text = "▶ Играть";
                }
                else if (mixerEngine.CurrentPlaybackState == PlaybackState.Paused)
                {
                    mixerEngine.Resume();
                    playButton.Text = "⏸ Пауза";
                }
                else
                {
                    PlayProject();
                    playButton.Text = "⏸ Пауза";
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                timeline.DeleteSelectedSample();
                UpdateProjectDuration();
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
                UpdateProjectDuration();
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