using System.Drawing;
using System.Windows.Forms;

namespace LooperStudio
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();

            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 700);
            Name = "MainForm";
            Text = "Looper Studio";

            // Создаем все UI элементы
            InitializeToolbar();
            InitializeTimelinePanel();
            InitializeSampleLibrary();

            ResumeLayout(false);
        }

        private void InitializeToolbar()
        {
            toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(37, 37, 38)
            };

            // Кнопка воспроизведения
            playButton = new Button
            {
                Text = "▶ Play",
                Location = new Point(10, 10),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            playButton.Click += PlayButton_Click;
            toolbar.Controls.Add(playButton);

            // Кнопка остановки
            stopButton = new Button
            {
                Text = "⬛ Stop",
                Location = new Point(95, 10),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            stopButton.Click += StopButton_Click;
            toolbar.Controls.Add(stopButton);

            // Кнопка записи
            recordButton = new Button
            {
                Text = "⏺ Record",
                Location = new Point(180, 10),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Red
            };
            recordButton.Click += RecordButton_Click;
            toolbar.Controls.Add(recordButton);

            // Кнопка сохранения проекта
            saveButton = new Button
            {
                Text = "💾 Save",
                Location = new Point(300, 10),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            saveButton.Click += SaveButton_Click;
            toolbar.Controls.Add(saveButton);

            // Кнопка загрузки проекта
            loadButton = new Button
            {
                Text = "📁 Load",
                Location = new Point(385, 10),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            loadButton.Click += LoadButton_Click;
            toolbar.Controls.Add(loadButton);

            // Кнопка добавления семпла
            addSampleButton = new Button
            {
                Text = "+ Add Sample",
                Location = new Point(500, 10),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            addSampleButton.Click += AddSampleButton_Click;
            toolbar.Controls.Add(addSampleButton);

            // Кнопка настроек
            settingsButton = new Button
            {
                Text = "⚙ Settings",
                Location = new Point(620, 10),
                Size = new Size(90, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            settingsButton.Click += SettingsButton_Click;
            toolbar.Controls.Add(settingsButton);

            // === НАСТРОЙКИ СЕТКИ И BPM ===

            // Label для BPM
            var bpmLabel = new Label
            {
                Text = "BPM:",
                Location = new Point(750, 15),
                Size = new Size(40, 20),
                ForeColor = Color.White
            };
            toolbar.Controls.Add(bpmLabel);

            // NumericUpDown для BPM
            bpmNumeric = new NumericUpDown
            {
                Location = new Point(790, 12),
                Size = new Size(60, 25),
                Minimum = 40,
                Maximum = 300,
                Value = 120,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            bpmNumeric.ValueChanged += BpmNumeric_ValueChanged;
            toolbar.Controls.Add(bpmNumeric);

            // CheckBox для Snap to Grid
            snapToGridCheckbox = new CheckBox
            {
                Text = "Snap to Grid",
                Location = new Point(870, 15),
                Size = new Size(100, 20),
                ForeColor = Color.White,
                Checked = false
            };
            snapToGridCheckbox.CheckedChanged += SnapToGridCheckbox_CheckedChanged;
            toolbar.Controls.Add(snapToGridCheckbox);

            // ComboBox для Grid Division
            gridDivisionCombo = new ComboBox
            {
                Location = new Point(980, 12),
                Size = new Size(70, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            gridDivisionCombo.Items.AddRange(new object[] { "1/4", "1/8", "1/16", "1/32" });
            gridDivisionCombo.SelectedIndex = 0;
            gridDivisionCombo.SelectedIndexChanged += GridDivisionCombo_SelectedIndexChanged;
            toolbar.Controls.Add(gridDivisionCombo);

            this.Controls.Add(toolbar);
        }

        private void InitializeTimelinePanel()
        {
            timelinePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(0, 0, 0, 0) // Убираем все отступы
            };

            this.Controls.Add(timelinePanel);
        }

        private void InitializeSampleLibrary()
        {
            libraryPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = Color.FromArgb(45, 45, 48),
                BorderStyle = BorderStyle.FixedSingle
            };

            libraryLabel = new Label
            {
                Text = "Sample Library",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(37, 37, 38),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            sampleLibrary = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9),
                ItemHeight = 20,
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ScrollAlwaysVisible = false, // Скролл появляется автоматически
                HorizontalScrollbar = false
            };

            sampleLibrary.DrawItem += SampleLibrary_DrawItem;
            sampleLibrary.DoubleClick += SampleLibrary_DoubleClick;
            sampleLibrary.MouseDown += SampleLibrary_MouseDown;

            libraryPanel.Controls.Add(sampleLibrary);
            libraryPanel.Controls.Add(libraryLabel);

            this.Controls.Add(libraryPanel);
        }

        #endregion

        // UI элементы
        private Panel toolbar;
        private Button playButton;
        private Button stopButton;
        private Button recordButton;
        private Button saveButton;
        private Button loadButton;
        private Button addSampleButton;
        private Button settingsButton;
        private NumericUpDown bpmNumeric;
        private CheckBox snapToGridCheckbox;
        private ComboBox gridDivisionCombo;

        private Panel timelinePanel;

        private Panel libraryPanel;
        private Label libraryLabel;
        private ListBox sampleLibrary;

        // Старые кнопки для совместимости (не используются)
        private Button Record = new Button();
        private Button Stop = new Button();
        private Button Play = new Button();
    }
}