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
        private const int TimelineHeaderHeight = 30;
        private const double PixelsPerSecond = 100; // Увеличиваем для точности (было 50)
        private AudioSample selectedSample = null;
        private bool isDragging = false;
        private Point dragStartPoint;

        public event EventHandler<AudioSample> SampleSelected;
        public event EventHandler<AudioSample> SampleDoubleClicked;

        public TimelineControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(45, 45, 48);
            AllowDrop = true; // Включаем поддержку drag & drop

            MouseDown += TimelineControl_MouseDown;
            MouseMove += TimelineControl_MouseMove;
            MouseUp += TimelineControl_MouseUp;
            MouseDoubleClick += TimelineControl_MouseDoubleClick;

            // Drag & Drop события
            DragEnter += TimelineControl_DragEnter;
            DragDrop += TimelineControl_DragDrop;
        }

        private void TimelineControl_DragEnter(object sender, DragEventArgs e)
        {
            // Проверяем, что перетаскивается текст (путь к файлу)
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

                // Получаем путь к файлу
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
                    // Конвертируем координаты экрана в координаты контрола
                    Point clientPoint = PointToClient(new Point(e.X, e.Y));

                    // Вычисляем позицию на таймлайне
                    double startTime = Math.Max(0, clientPoint.X / PixelsPerSecond);

                    // Привязка к сетке
                    if (project.SnapToGrid)
                    {
                        double gridSize = project.GetGridSize();
                        startTime = Math.Round(startTime / gridSize) * gridSize;
                    }

                    int trackNumber = Math.Max(0, Math.Min(project.TrackCount - 1,
                        (clientPoint.Y - TimelineHeaderHeight) / TrackHeight));

                    // Получаем длительность файла
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
            double maxTime = 30; // Минимум 30 секунд
            foreach (var sample in project.Samples)
            {
                double endTime = sample.StartTime + sample.Duration;
                if (endTime > maxTime)
                    maxTime = endTime;
            }

            Width = (int)(maxTime * PixelsPerSecond) + 100;
            Height = TimelineHeaderHeight + (project.TrackCount * TrackHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (project == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Рисуем заголовок временной шкалы
            DrawTimelineHeader(g);

            // Рисуем треки
            DrawTracks(g);

            // Рисуем сетку (если включена)
            if (project.SnapToGrid)
            {
                DrawGrid(g);
            }

            // Рисуем семплы
            DrawSamples(g);
        }

        private void DrawTimelineHeader(Graphics g)
        {
            // Фон заголовка
            g.FillRectangle(new SolidBrush(Color.FromArgb(37, 37, 38)),
                0, 0, Width, TimelineHeaderHeight);

            // Временные метки (каждую секунду)
            using (Font font = new Font("Segoe UI", 8))
            {
                int secondsToShow = (int)(Width / PixelsPerSecond) + 1;
                for (int i = 0; i < secondsToShow; i++)
                {
                    int x = (int)(i * PixelsPerSecond);

                    // Длинная линия каждую секунду
                    g.DrawLine(Pens.LightGray, x, TimelineHeaderHeight - 8, x, TimelineHeaderHeight);
                    g.DrawString($"{i}s", font, Brushes.LightGray, x + 2, 5);

                    // Короткие линии каждые 0.5 секунды
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

                // Чередующиеся цвета треков
                Color trackColor = i % 2 == 0
                    ? Color.FromArgb(50, 50, 52)
                    : Color.FromArgb(45, 45, 48);

                g.FillRectangle(new SolidBrush(trackColor), 0, y, Width, TrackHeight);

                // Линия разделения
                g.DrawLine(new Pen(Color.FromArgb(60, 60, 60)),
                    0, y, Width, y);

                // Номер трека
                using (Font font = new Font("Segoe UI", 9))
                {
                    g.DrawString($"Track {i + 1}", font, Brushes.Gray, 5, y + 5);
                }
            }
        }

        private void DrawGrid(Graphics g)
        {
            double gridSize = project.GetGridSize(); // Размер одной клетки сетки в секундах
            int gridPixels = (int)(gridSize * PixelsPerSecond);

            if (gridPixels < 5) return; // Не рисуем слишком мелкую сетку

            using (Pen gridPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
            {
                gridPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

                int maxX = Width;
                for (int x = 0; x < maxX; x += gridPixels)
                {
                    // Рисуем вертикальную линию
                    g.DrawLine(gridPen, x, TimelineHeaderHeight, x, Height);
                }
            }

            // Рисуем линии тактов (каждые 4 бита) более жирными
            double barSize = (60.0 / project.BPM) * 4; // Длительность такта в секундах
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

            // Цвет семпла
            Color sampleColor = isSelected
                ? Color.FromArgb(0, 122, 204)
                : Color.FromArgb(70, 130, 180);

            // Рисуем прямоугольник семпла
            g.FillRectangle(new SolidBrush(sampleColor), x, y, width, height);
            g.DrawRectangle(new Pen(Color.White, isSelected ? 2 : 1), x, y, width, height);

            // Название семпла
            using (Font font = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                g.DrawString(sample.Name, font, Brushes.White, x + 5, y + 5);
            }

            // Показываем длительность крупно для понимания масштаба
            using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
            {
                string durationText = $"{sample.Duration:F2}s";
                var textSize = g.MeasureString(durationText, font);
                g.DrawString(durationText, font, Brushes.Yellow,
                    x + (width / 2) - (textSize.Width / 2), y + (height / 2) - (textSize.Height / 2));
            }

            // Индикатор громкости
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
                AudioSample clickedSample = GetSampleAtPoint(e.Location);

                if (clickedSample != null)
                {
                    selectedSample = clickedSample;
                    isDragging = true;
                    dragStartPoint = e.Location;
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
            if (isDragging && selectedSample != null)
            {
                // Перемещение семпла
                int deltaX = e.X - dragStartPoint.X;
                int deltaY = e.Y - dragStartPoint.Y;

                // Изменяем позицию по времени
                double newStartTime = selectedSample.StartTime + (deltaX / PixelsPerSecond);

                // Привязка к сетке
                if (project.SnapToGrid)
                {
                    double gridSize = project.GetGridSize();
                    newStartTime = Math.Round(newStartTime / gridSize) * gridSize;
                }

                if (newStartTime >= 0)
                {
                    selectedSample.StartTime = newStartTime;
                }

                // Изменяем трек
                int trackDelta = deltaY / TrackHeight;
                int newTrack = selectedSample.TrackNumber + trackDelta;
                if (newTrack >= 0 && newTrack < project.TrackCount)
                {
                    selectedSample.TrackNumber = newTrack;
                }

                dragStartPoint = e.Location;
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

            // Проверяем в обратном порядке (последние нарисованные сверху)
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
    }
}