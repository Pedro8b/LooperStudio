using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;

namespace LooperStudio
{
    /// <summary>
    /// Кастомный контрол для отображения таймлайна с семплами
    /// </summary>
    public class TimelineControl : Control
    {
        private Project project;
        private const int TrackHeight = 60;
        private const int TimelineHeaderHeight = 60;
        private const double PixelsPerSecond = 100;
        public AudioSample selectedSample = null;
        private bool isDragging = false;
        private Point dragStartPoint;
        private double sampleStartTimeBeforeDrag;
        private int sampleTrackBeforeDrag;
        private AudioSample copiedSample = null; // Для Ctrl+C / Ctrl+V

        public event EventHandler<AudioSample> SampleSelected;
        public event EventHandler<AudioSample> SampleDoubleClicked;

        public TimelineControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(45, 45, 48);
            AllowDrop = true;

            MouseDown += TimelineControl_MouseDown;
            MouseMove += TimelineControl_MouseMove;
            MouseUp += TimelineControl_MouseUp;
            MouseDoubleClick += TimelineControl_MouseDoubleClick;

            DragEnter += TimelineControl_DragEnter;
            DragDrop += TimelineControl_DragDrop;
        }

        private void TimelineControl_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text) ||
                e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void TimelineControl_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                string filePath = null;

                if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    filePath = e.Data.GetData(DataFormats.Text) as string;
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Length > 0)
                    {
                        filePath = files[0];
                    }
                }

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    Point clientPoint = PointToClient(new Point(e.X, e.Y));

                    double startTime = Math.Max(0, clientPoint.X / PixelsPerSecond);

                    if (project.SnapToGrid)
                    {
                        double gridSize = project.GetGridSize();
                        startTime = Math.Round(startTime / gridSize) * gridSize;
                    }

                    int trackNumber = Math.Max(0, Math.Min(project.TrackCount - 1,
                        (clientPoint.Y - TimelineHeaderHeight) / TrackHeight));

                    using (var audioFile = new AudioFileReader(filePath))
                    {
                        double duration = audioFile.TotalTime.TotalSeconds;

                        var sample = new AudioSample
                        {
                            FilePath = filePath,
                            Name = Path.GetFileNameWithoutExtension(filePath),
                            StartTime = startTime,
                            TrackNumber = trackNumber,
                            Duration = duration
                        };

                        project.Samples.Add(sample);
                        UpdateSize();
                        Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления семпла: {ex.Message}", "Ошибка");
            }
        }

        public void SetProject(Project proj)
        {
            project = proj;
            UpdateSize();
            Invalidate();
        }

        private void UpdateSize()
        {
            if (project == null) return;

            // Вычисляем минимальную ширину на основе самого длинного семпла
            double maxTime = 120; // Минимум 2 минуты (было 30 секунд)
            foreach (var sample in project.Samples)
            {
                double endTime = sample.StartTime + sample.Duration;
                if (endTime > maxTime)
                    maxTime = endTime;
            }

            // Добавляем 20% запаса для удобства
            maxTime *= 1.2;

            Width = (int)(maxTime * PixelsPerSecond) + 100;
            Height = TimelineHeaderHeight + (project.TrackCount * TrackHeight) + 80; // +80 для кнопки добавления трека
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (project == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            DrawTimelineHeader(g);
            DrawTracks(g);

            if (project.SnapToGrid)
            {
                DrawGrid(g);
            }

            DrawSamples(g);
            DrawAddTrackButton(g);
        }

        private void DrawAddTrackButton(Graphics g)
        {
            int y = TimelineHeaderHeight + (project.TrackCount * TrackHeight) + 10;
            int buttonWidth = 150;
            int buttonHeight = 40;
            int x = 20;

            Rectangle buttonRect = new Rectangle(x, y, buttonWidth, buttonHeight);

            // Проверяем наведение мыши
            Point mousePos = PointToClient(MousePosition);
            bool isHovered = buttonRect.Contains(mousePos);

            // Рисуем кнопку
            Color buttonColor = isHovered ? Color.FromArgb(70, 130, 180) : Color.FromArgb(60, 60, 62);
            g.FillRectangle(new SolidBrush(buttonColor), buttonRect);
            g.DrawRectangle(new Pen(Color.FromArgb(100, 100, 100), 2), buttonRect);

            // Рисуем текст
            using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("+ Add Track", font, Brushes.White, buttonRect, sf);
            }
        }

        private void DrawTimelineHeader(Graphics g)
        {
            g.FillRectangle(new SolidBrush(Color.FromArgb(37, 37, 38)),
                0, 0, Width, TimelineHeaderHeight);

            using (Font font = new Font("Segoe UI", 8))
            {
                int secondsToShow = (int)(Width / PixelsPerSecond) + 1;
                for (int i = 0; i < secondsToShow; i++)
                {
                    int x = (int)(i * PixelsPerSecond);

                    g.DrawLine(Pens.LightGray, x, TimelineHeaderHeight - 8, x, TimelineHeaderHeight);
                    g.DrawString($"{i}s", font, Brushes.LightGray, x + 2, 5);

                    if (i < secondsToShow - 1)
                    {
                        int halfX = (int)((i + 0.5) * PixelsPerSecond);
                        g.DrawLine(Pens.Gray, halfX, TimelineHeaderHeight - 4, halfX, TimelineHeaderHeight);
                    }
                }
            }
        }

        private void DrawTracks(Graphics g)
        {
            for (int i = 0; i < project.TrackCount; i++)
            {
                int y = TimelineHeaderHeight + (i * TrackHeight);

                Color trackColor = i % 2 == 0
                    ? Color.FromArgb(50, 50, 52)
                    : Color.FromArgb(45, 45, 48);

                g.FillRectangle(new SolidBrush(trackColor), 0, y, Width, TrackHeight);

                g.DrawLine(new Pen(Color.FromArgb(60, 60, 60)),
                    0, y, Width, y);

                using (Font font = new Font("Segoe UI", 9))
                {
                    g.DrawString($"Track {i + 1}", font, Brushes.Gray, 5, y + 5);
                }
            }
        }

        private void DrawGrid(Graphics g)
        {
            double gridSize = project.GetGridSize();
            int gridPixels = (int)(gridSize * PixelsPerSecond);

            if (gridPixels < 5) return;

            using (Pen gridPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
            {
                gridPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

                int maxX = Width;
                for (int x = 0; x < maxX; x += gridPixels)
                {
                    g.DrawLine(gridPen, x, TimelineHeaderHeight, x, Height);
                }
            }

            double barSize = (60.0 / project.BPM) * 4;
            int barPixels = (int)(barSize * PixelsPerSecond);

            if (barPixels >= 10)
            {
                using (Pen barPen = new Pen(Color.FromArgb(120, 255, 255, 255), 2))
                {
                    int maxX = Width;
                    for (int x = 0; x < maxX; x += barPixels)
                    {
                        g.DrawLine(barPen, x, TimelineHeaderHeight, x, Height);
                    }
                }
            }
        }

        private void DrawSamples(Graphics g)
        {
            foreach (var sample in project.Samples)
            {
                DrawSample(g, sample, sample == selectedSample);
            }
        }

        private void DrawSample(Graphics g, AudioSample sample, bool isSelected)
        {
            int x = (int)(sample.StartTime * PixelsPerSecond);
            int y = TimelineHeaderHeight + (sample.TrackNumber * TrackHeight) + 5;
            int width = (int)(sample.Duration * PixelsPerSecond);
            int height = TrackHeight - 10;

            Color sampleColor = isSelected
                ? Color.FromArgb(0, 122, 204)
                : Color.FromArgb(70, 130, 180);

            g.FillRectangle(new SolidBrush(sampleColor), x, y, width, height);
            g.DrawRectangle(new Pen(Color.White, isSelected ? 2 : 1), x, y, width, height);

            using (Font font = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                g.DrawString(sample.Name, font, Brushes.White, x + 5, y + 5);
            }

            using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
            {
                string durationText = $"{sample.Duration:F2}s";
                var textSize = g.MeasureString(durationText, font);
                g.DrawString(durationText, font, Brushes.Yellow,
                    x + (width / 2) - (textSize.Width / 2), y + (height / 2) - (textSize.Height / 2));
            }

            using (Font font = new Font("Segoe UI", 7))
            {
                g.DrawString($"Vol: {(int)(sample.Volume * 100)}%", font,
                    Brushes.LightGray, x + 5, y + height - 15);
            }
        }

        private void TimelineControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Проверяем клик по кнопке "Add Track"
                int addButtonY = TimelineHeaderHeight + (project.TrackCount * TrackHeight) + 10;
                Rectangle addButtonRect = new Rectangle(20, addButtonY, 150, 40);

                if (addButtonRect.Contains(e.Location))
                {
                    // Добавляем новый трек
                    project.TrackCount++;
                    UpdateSize();
                    Invalidate();
                    return;
                }

                AudioSample clickedSample = GetSampleAtPoint(e.Location);

                if (clickedSample != null)
                {
                    selectedSample = clickedSample;
                    isDragging = true;
                    dragStartPoint = e.Location;

                    sampleStartTimeBeforeDrag = selectedSample.StartTime;
                    sampleTrackBeforeDrag = selectedSample.TrackNumber;

                    SampleSelected?.Invoke(this, selectedSample);
                    Invalidate();
                }
                else
                {
                    selectedSample = null;
                    Invalidate();
                }
            }
        }

        private void TimelineControl_MouseMove(object sender, MouseEventArgs e)
        {
            // Проверяем наведение на кнопку "Add Track"
            int addButtonY = TimelineHeaderHeight + (project.TrackCount * TrackHeight) + 10;
            Rectangle addButtonRect = new Rectangle(20, addButtonY, 150, 40);

            if (addButtonRect.Contains(e.Location))
            {
                Cursor = Cursors.Hand;
                if (!isDragging) Invalidate(); // Перерисовываем для эффекта hover
            }
            else
            {
                Cursor = Cursors.Default;
            }

            if (isDragging && selectedSample != null)
            {
                int totalDeltaX = e.X - dragStartPoint.X;
                int totalDeltaY = e.Y - dragStartPoint.Y;

                double newStartTime = sampleStartTimeBeforeDrag + (totalDeltaX / PixelsPerSecond);

                if (project.SnapToGrid)
                {
                    double gridSize = project.GetGridSize();
                    newStartTime = Math.Round(newStartTime / gridSize) * gridSize;
                }

                if (newStartTime >= 0)
                {
                    selectedSample.StartTime = newStartTime;
                }

                int trackDelta = totalDeltaY / TrackHeight;
                int newTrack = sampleTrackBeforeDrag + trackDelta;
                if (newTrack >= 0 && newTrack < project.TrackCount)
                {
                    selectedSample.TrackNumber = newTrack;
                }

                Invalidate();
            }
        }

        private void TimelineControl_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void TimelineControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            AudioSample clickedSample = GetSampleAtPoint(e.Location);
            if (clickedSample != null)
            {
                SampleDoubleClicked?.Invoke(this, clickedSample);
            }
        }

        private AudioSample GetSampleAtPoint(Point point)
        {
            if (project == null) return null;

            for (int i = project.Samples.Count - 1; i >= 0; i--)
            {
                var sample = project.Samples[i];
                int x = (int)(sample.StartTime * PixelsPerSecond);
                int y = TimelineHeaderHeight + (sample.TrackNumber * TrackHeight) + 5;
                int width = (int)(sample.Duration * PixelsPerSecond);
                int height = TrackHeight - 10;

                Rectangle rect = new Rectangle(x, y, width, height);
                if (rect.Contains(point))
                {
                    return sample;
                }
            }

            return null;
        }

        public void DeleteSelectedSample()
        {
            if (selectedSample != null && project != null)
            {
                project.Samples.Remove(selectedSample);
                selectedSample = null;
                Invalidate();
            }
        }

        public void DeleteSample(AudioSample sample)
        {
            if (selectedSample != null && project != null)
            {
                project.Samples.Remove(sample);
                sample = null;
                Invalidate();
            }
        }

        public void CopySelectedSample()
        {
            if (selectedSample != null)
            {
                copiedSample = new AudioSample
                {
                    FilePath = selectedSample.FilePath,
                    Name = selectedSample.Name,
                    Duration = selectedSample.Duration,
                    Volume = selectedSample.Volume,
                    StartTime = selectedSample.StartTime,
                    TrackNumber = selectedSample.TrackNumber
                };
            }
        }

        public void PasteSample()
        {
            if (copiedSample != null && project != null)
            {
                var newSample = new AudioSample
                {
                    FilePath = copiedSample.FilePath,
                    Name = copiedSample.Name,
                    Duration = copiedSample.Duration,
                    Volume = copiedSample.Volume,
                    StartTime = copiedSample.StartTime + 1.0,
                    TrackNumber = copiedSample.TrackNumber
                };

                project.Samples.Add(newSample);
                selectedSample = newSample;
                UpdateSize();
                Invalidate();
            }
        }
        public void AddSample(AudioSample sample)
        {
            project.Samples.Add(sample);
            selectedSample = sample;
            UpdateSize(); 
            Invalidate();
        }
    }
}