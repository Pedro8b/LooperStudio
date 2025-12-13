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
        private const int EdgeGrabWidth = 8; // Ширина зоны для захвата края семпла

        // Выделение
        public AudioSample selectedSample = null;
        private List<AudioSample> selectedSamples = new List<AudioSample>();

        // Перетаскивание
        private bool isDragging = false;
        private Point dragStartPoint;
        private double sampleStartTimeBeforeDrag;
        private int sampleTrackBeforeDrag;
        private Dictionary<AudioSample, (double startTime, int track)> groupDragStartPositions = new Dictionary<AudioSample, (double, int)>();

        // Изменение размера
        private bool isResizing = false;
        private AudioSample resizingSample = null;
        private bool resizingRightEdge = false; // true = правый край, false = левый край
        private double originalDuration;
        private double originalFileOffset;
        private double originalStartTime;

        // Рамка выделения
        private bool isSelecting = false;
        private Point selectionStartPoint;
        private Rectangle selectionRectangle;

        private List<AudioSample> copiedSamples = new List<AudioSample>();

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
            MouseWheel += TimelineControl_MouseWheel;

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

            double maxTime = 120;
            foreach (var sample in project.Samples)
            {
                double endTime = sample.StartTime + sample.Duration;
                if (endTime > maxTime)
                    maxTime = endTime;
            }

            maxTime *= 1.2;

            Width = (int)(maxTime * PixelsPerSecond) + 100;
            Height = TimelineHeaderHeight + (project.TrackCount * TrackHeight) + 80;
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

            // Рисуем рамку выделения
            if (isSelecting)
            {
                using (Pen selectionPen = new Pen(Color.FromArgb(150, 100, 150, 255), 2))
                {
                    selectionPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawRectangle(selectionPen, selectionRectangle);
                }
                using (Brush selectionBrush = new SolidBrush(Color.FromArgb(30, 100, 150, 255)))
                {
                    g.FillRectangle(selectionBrush, selectionRectangle);
                }
            }
        }

        private void DrawAddTrackButton(Graphics g)
        {
            int y = TimelineHeaderHeight + (project.TrackCount * TrackHeight) + 10;
            int buttonWidth = 150;
            int buttonHeight = 40;
            int x = 20;

            Rectangle buttonRect = new Rectangle(x, y, buttonWidth, buttonHeight);

            Point mousePos = PointToClient(MousePosition);
            bool isHovered = buttonRect.Contains(mousePos);

            Color buttonColor = isHovered ? Color.FromArgb(70, 130, 180) : Color.FromArgb(60, 60, 62);
            g.FillRectangle(new SolidBrush(buttonColor), buttonRect);
            g.DrawRectangle(new Pen(Color.FromArgb(100, 100, 100), 2), buttonRect);

            using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("+ Добавить дорожку", font, Brushes.White, buttonRect, sf);
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

            // Показываем информацию о сетке
            if (project.SnapToGrid)
            {
                using (Font infoFont = new Font("Segoe UI", 9, FontStyle.Bold))
                {
                    string gridInfo = $"Сетка: 1/{project.GridDivision} | Темп: {project.BPM}";
                    var size = g.MeasureString(gridInfo, infoFont);

                    // Рисуем в правом верхнем углу с фоном
                    Rectangle infoBg = new Rectangle(
                        Width - (int)size.Width - 20,
                        5,
                        (int)size.Width + 10,
                        (int)size.Height + 4);

                    g.FillRectangle(new SolidBrush(Color.FromArgb(180, 50, 50, 50)), infoBg);
                    g.DrawString(gridInfo, infoFont, Brushes.LimeGreen, Width - size.Width - 15, 7);
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
                    g.DrawString($"Дорожка {i + 1}", font, Brushes.Gray, 5, y + 5);
                }
            }
        }

        private void DrawGrid(Graphics g)
        {
            double gridSize = project.GetGridSize();
            double gridPixels = gridSize * PixelsPerSecond;

            if (gridPixels < 5) return;

            // Рисуем линии сетки (биты)
            using (Pen gridPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
            {
                gridPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

                double maxTime = Width / PixelsPerSecond;
                int gridCount = (int)(maxTime / gridSize) + 1;

                for (int i = 0; i < gridCount; i++)
                {
                    int x = (int)(i * gridPixels);
                    if (x >= 0 && x < Width)
                    {
                        g.DrawLine(gridPen, x, TimelineHeaderHeight, x, Height);
                    }
                }
            }

            // Рисуем линии тактов (bars - 4 бита)
            double barSize = (60.0 / project.BPM) * 4; // 4 четверти = 1 такт
            double barPixels = barSize * PixelsPerSecond;

            if (barPixels >= 10)
            {
                using (Pen barPen = new Pen(Color.FromArgb(120, 255, 255, 255), 2))
                {
                    double maxTime = Width / PixelsPerSecond;
                    int barCount = (int)(maxTime / barSize) + 1;

                    for (int i = 0; i < barCount; i++)
                    {
                        int x = (int)(i * barPixels);
                        if (x >= 0 && x < Width)
                        {
                            g.DrawLine(barPen, x, TimelineHeaderHeight, x, Height);
                        }
                    }
                }
            }
        }

        private void DrawSamples(Graphics g)
        {
            foreach (var sample in project.Samples)
            {
                bool isSelected = selectedSamples.Contains(sample) || sample == selectedSample;
                DrawSample(g, sample, isSelected);
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

            // Рисуем индикатор громкости (зелёная/жёлтая/красная полоска)
            int volumeBarHeight = 4;
            int volumeBarWidth = (int)(width * Math.Min(1.0f, sample.Volume));
            Color volumeColor;

            if (sample.Volume <= 1.0f)
                volumeColor = Color.FromArgb(100, 255, 100); // Зелёный - нормальная громкость
            else if (sample.Volume <= 1.5f)
                volumeColor = Color.FromArgb(255, 200, 0); // Жёлтый - повышенная громкость
            else
                volumeColor = Color.FromArgb(255, 50, 50); // Красный - опасная громкость

            g.FillRectangle(new SolidBrush(volumeColor), x, y + height - volumeBarHeight, volumeBarWidth, volumeBarHeight);

            // Рисуем индикаторы для изменения размера
            if (isSelected)
            {
                g.FillRectangle(Brushes.Yellow, x, y, EdgeGrabWidth, height);
                g.FillRectangle(Brushes.Yellow, x + width - EdgeGrabWidth, y, EdgeGrabWidth, height);
            }

            using (Font font = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                g.DrawString(sample.Name, font, Brushes.White, x + 5, y + 5);
            }

            using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
            {
                string durationText = $"{sample.Duration:F2}сек.";
                var textSize = g.MeasureString(durationText, font);
                g.DrawString(durationText, font, Brushes.Yellow,
                    x + (width / 2) - (textSize.Width / 2), y + (height / 2) - (textSize.Height / 2));
            }

            using (Font font = new Font("Segoe UI", 7, FontStyle.Bold))
            {
                // Цвет текста громкости зависит от уровня
                Brush volumeBrush = sample.Volume > 1.0f ? Brushes.Yellow : Brushes.LightGray;
                string volumeText = $"Громк.: {(int)(sample.Volume * 100)}%";

                if (sample.Volume > 1.0f)
                    volumeText += " ⚠";

                g.DrawString(volumeText, font, volumeBrush, x + 5, y + height - 15);
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
                    project.TrackCount++;
                    UpdateSize();
                    Invalidate();
                    return;
                }

                // Проверяем, не кликнули ли по краю семпла для изменения размера
                var (sample, edge) = GetSampleEdgeAtPoint(e.Location);
                if (sample != null && (selectedSamples.Contains(sample) || sample == selectedSample))
                {
                    isResizing = true;
                    resizingSample = sample;
                    resizingRightEdge = (edge == EdgeType.Right);
                    originalDuration = sample.Duration;
                    originalFileOffset = sample.FileOffset;
                    originalStartTime = sample.StartTime;
                    Cursor = Cursors.SizeWE;
                    return;
                }

                AudioSample clickedSample = GetSampleAtPoint(e.Location);

                if (clickedSample != null)
                {
                    // Ctrl - добавляем к выделению
                    if (ModifierKeys.HasFlag(Keys.Control))
                    {
                        if (selectedSamples.Contains(clickedSample))
                        {
                            selectedSamples.Remove(clickedSample);
                        }
                        else
                        {
                            selectedSamples.Add(clickedSample);
                        }
                    }
                    else if (!selectedSamples.Contains(clickedSample))
                    {
                        // Кликнули по невыделенному семплу - выделяем только его
                        selectedSamples.Clear();
                        selectedSamples.Add(clickedSample);
                    }

                    selectedSample = clickedSample;
                    isDragging = true;
                    dragStartPoint = e.Location;

                    // Запоминаем начальные позиции всех выделенных семплов
                    groupDragStartPositions.Clear();
                    foreach (var s in selectedSamples)
                    {
                        groupDragStartPositions[s] = (s.StartTime, s.TrackNumber);
                    }

                    SampleSelected?.Invoke(this, selectedSample);
                    Invalidate();
                }
                else
                {
                    // Начинаем выделение рамкой
                    isSelecting = true;
                    selectionStartPoint = e.Location;
                    selectionRectangle = new Rectangle(e.Location, Size.Empty);

                    // Если НЕ зажат Ctrl - очищаем выделение
                    if (!ModifierKeys.HasFlag(Keys.Control))
                    {
                        selectedSamples.Clear();
                        selectedSample = null;
                    }
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
            }
            else if (!isResizing && !isDragging)
            {
                // Проверяем наведение на края семплов
                var (sample, edge) = GetSampleEdgeAtPoint(e.Location);
                if (sample != null && (selectedSamples.Contains(sample) || sample == selectedSample))
                {
                    Cursor = Cursors.SizeWE;
                }
                else
                {
                    Cursor = Cursors.Default;
                }
            }

            // Изменение размера семпла
            if (isResizing && resizingSample != null)
            {
                int deltaX = e.X - dragStartPoint.X;
                double deltaTime = deltaX / PixelsPerSecond;

                if (resizingRightEdge)
                {
                    // Изменяем правый край - меняем Duration
                    double newDuration = originalDuration + deltaTime;

                    // Получаем реальную длительность файла
                    try
                    {
                        using (var audioFile = new AudioFileReader(resizingSample.FilePath))
                        {
                            double maxDuration = audioFile.TotalTime.TotalSeconds - resizingSample.FileOffset;
                            newDuration = Math.Max(0.1, Math.Min(newDuration, maxDuration));
                        }
                    }
                    catch { }

                    resizingSample.Duration = newDuration;
                }
                else
                {
                    // Изменяем левый край - меняем FileOffset и StartTime
                    double newFileOffset = originalFileOffset + deltaTime;

                    if (newFileOffset >= 0)
                    {
                        try
                        {
                            using (var audioFile = new AudioFileReader(resizingSample.FilePath))
                            {
                                double maxOffset = audioFile.TotalTime.TotalSeconds - 0.1;
                                newFileOffset = Math.Max(0, Math.Min(newFileOffset, maxOffset));

                                double actualDelta = newFileOffset - originalFileOffset;
                                resizingSample.FileOffset = newFileOffset;
                                resizingSample.StartTime = originalStartTime + actualDelta;
                                resizingSample.Duration = originalDuration - actualDelta;
                            }
                        }
                        catch { }
                    }
                }

                Invalidate();
                return;
            }

            // Выделение рамкой
            if (isSelecting)
            {
                int x = Math.Min(selectionStartPoint.X, e.X);
                int y = Math.Min(selectionStartPoint.Y, e.Y);
                int width = Math.Abs(e.X - selectionStartPoint.X);
                int height = Math.Abs(e.Y - selectionStartPoint.Y);

                selectionRectangle = new Rectangle(x, y, width, height);

                // Обновляем выделение
                var samplesInRect = GetSamplesInRectangle(selectionRectangle);
                if (!ModifierKeys.HasFlag(Keys.Control))
                {
                    selectedSamples.Clear();
                }
                foreach (var s in samplesInRect)
                {
                    if (!selectedSamples.Contains(s))
                    {
                        selectedSamples.Add(s);
                    }
                }

                Invalidate();
                return;
            }

            // Перетаскивание семплов
            if (isDragging && selectedSamples.Count > 0)
            {
                int totalDeltaX = e.X - dragStartPoint.X;
                int totalDeltaY = e.Y - dragStartPoint.Y;

                double deltaTime = totalDeltaX / PixelsPerSecond;
                int trackDelta = totalDeltaY / TrackHeight;

                foreach (var sample in selectedSamples)
                {
                    if (groupDragStartPositions.TryGetValue(sample, out var startPos))
                    {
                        double newStartTime = startPos.startTime + deltaTime;

                        if (project.SnapToGrid)
                        {
                            double gridSize = project.GetGridSize();
                            newStartTime = Math.Round(newStartTime / gridSize) * gridSize;
                        }

                        if (newStartTime >= 0)
                        {
                            sample.StartTime = newStartTime;
                        }

                        int newTrack = startPos.track + trackDelta;
                        if (newTrack >= 0 && newTrack < project.TrackCount)
                        {
                            sample.TrackNumber = newTrack;
                        }
                    }
                }

                Invalidate();
            }
        }

        private void TimelineControl_MouseUp(object sender, MouseEventArgs e)
        {
            // Если это был простой клик (не перетаскивание) по пустому месту
            if (isSelecting && !ModifierKeys.HasFlag(Keys.Control))
            {
                // Проверяем, было ли реальное перетаскивание
                int dragDistance = Math.Abs(e.X - selectionStartPoint.X) + Math.Abs(e.Y - selectionStartPoint.Y);

                if (dragDistance < 5) // Если движение меньше 5 пикселей - это клик, а не drag
                {
                    // Простой клик по пустому месту - снимаем выделение
                    selectedSamples.Clear();
                    selectedSample = null;
                }
            }

            isDragging = false;
            isResizing = false;
            resizingSample = null;
            Cursor = Cursors.Default;

            // Завершаем выделение рамкой
            if (isSelecting)
            {
                isSelecting = false;
                Invalidate();
            }
        }

        private void TimelineControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            AudioSample clickedSample = GetSampleAtPoint(e.Location);
            if (clickedSample != null)
            {
                SampleDoubleClicked?.Invoke(this, clickedSample);
            }
        }

        private void TimelineControl_MouseWheel(object sender, MouseEventArgs e)
        {
            // Ctrl + колёсико = регулировка громкости
            if (ModifierKeys.HasFlag(Keys.Control))
            {
                AudioSample hoveredSample = GetSampleAtPoint(e.Location);

                if (hoveredSample != null)
                {
                    // Изменяем громкость
                    float volumeChange = e.Delta > 0 ? 0.05f : -0.05f;
                    hoveredSample.Volume = Math.Max(0.0f, Math.Min(2.0f, hoveredSample.Volume + volumeChange));

                    // Если семпл не выделен, выделяем его для визуальной обратной связи
                    if (!selectedSamples.Contains(hoveredSample))
                    {
                        selectedSamples.Clear();
                        selectedSamples.Add(hoveredSample);
                        selectedSample = hoveredSample;
                    }

                    Invalidate();

                    // Показываем временное сообщение с громкостью
                    ShowVolumeTooltip(hoveredSample, e.Location);
                }

                // ВАЖНО: Предотвращаем прокрутку таймлайна
                ((HandledMouseEventArgs)e).Handled = true;
            }
        }

        private void ShowVolumeTooltip(AudioSample sample, Point location)
        {
            // Создаём временную подсказку
            var tooltip = new ToolTip();
            tooltip.IsBalloon = false;
            tooltip.UseAnimation = true;
            tooltip.UseFading = true;
            tooltip.InitialDelay = 0;
            tooltip.AutoPopDelay = 1000;

            string volumeText = $"{sample.Name}\nVolume: {(int)(sample.Volume * 100)}%";
            tooltip.Show(volumeText, this, location.X + 10, location.Y - 30, 1000);
        }

        private enum EdgeType { None, Left, Right }

        private (AudioSample sample, EdgeType edge) GetSampleEdgeAtPoint(Point point)
        {
            if (project == null) return (null, EdgeType.None);

            for (int i = project.Samples.Count - 1; i >= 0; i--)
            {
                var sample = project.Samples[i];
                int x = (int)(sample.StartTime * PixelsPerSecond);
                int y = TimelineHeaderHeight + (sample.TrackNumber * TrackHeight) + 5;
                int width = (int)(sample.Duration * PixelsPerSecond);
                int height = TrackHeight - 10;

                Rectangle leftEdge = new Rectangle(x, y, EdgeGrabWidth, height);
                Rectangle rightEdge = new Rectangle(x + width - EdgeGrabWidth, y, EdgeGrabWidth, height);

                if (leftEdge.Contains(point))
                    return (sample, EdgeType.Left);
                if (rightEdge.Contains(point))
                    return (sample, EdgeType.Right);
            }

            return (null, EdgeType.None);
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

        private List<AudioSample> GetSamplesInRectangle(Rectangle rect)
        {
            var result = new List<AudioSample>();
            if (project == null) return result;

            foreach (var sample in project.Samples)
            {
                int x = (int)(sample.StartTime * PixelsPerSecond);
                int y = TimelineHeaderHeight + (sample.TrackNumber * TrackHeight) + 5;
                int width = (int)(sample.Duration * PixelsPerSecond);
                int height = TrackHeight - 10;

                Rectangle sampleRect = new Rectangle(x, y, width, height);
                if (rect.IntersectsWith(sampleRect))
                {
                    result.Add(sample);
                }
            }

            return result;
        }

        public void DeleteSelectedSample()
        {
            if (project != null)
            {
                foreach (var sample in selectedSamples.ToList())
                {
                    project.Samples.Remove(sample);
                }
                selectedSamples.Clear();
                selectedSample = null;
                Invalidate();
            }
        }

        public void DeleteSample(AudioSample sample)
        {
            if (project != null)
            {
                project.Samples.Remove(sample);
                selectedSamples.Remove(sample);
                if (selectedSample == sample)
                    selectedSample = null;
                Invalidate();
            }
        }

        public void CopySelectedSample()
        {
            copiedSamples.Clear();
            foreach (var sample in selectedSamples)
            {
                copiedSamples.Add(new AudioSample
                {
                    FilePath = sample.FilePath,
                    Name = sample.Name,
                    Duration = sample.Duration,
                    Volume = sample.Volume,
                    StartTime = sample.StartTime,
                    TrackNumber = sample.TrackNumber,
                    FileOffset = sample.FileOffset
                });
            }
        }

        public void PasteSample()
        {
            if (copiedSamples.Count > 0 && project != null)
            {
                selectedSamples.Clear();

                foreach (var copiedSample in copiedSamples)
                {
                    var newSample = new AudioSample
                    {
                        FilePath = copiedSample.FilePath,
                        Name = copiedSample.Name,
                        Duration = copiedSample.Duration,
                        Volume = copiedSample.Volume,
                        StartTime = copiedSample.StartTime + 1.0,
                        TrackNumber = copiedSample.TrackNumber,
                        FileOffset = copiedSample.FileOffset
                    };

                    project.Samples.Add(newSample);
                    selectedSamples.Add(newSample);
                }

                if (selectedSamples.Count > 0)
                    selectedSample = selectedSamples[0];

                UpdateSize();
                Invalidate();
            }
        }

        public void AddSample(AudioSample sample)
        {
            project.Samples.Add(sample);
            selectedSample = sample;
            selectedSamples.Clear();
            selectedSamples.Add(sample);
            UpdateSize();
            Invalidate();
        }
    }
}