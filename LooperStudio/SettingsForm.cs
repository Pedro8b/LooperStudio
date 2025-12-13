using NAudio.Wave;
using System;
using System.Windows.Forms;

namespace LooperStudio
{
    public partial class SettingsForm : Form
    {
        public int SelectedInputDevice { get; private set; }
        public int SelectedOutputDevice { get; private set; }
        public string SamplesFolder { get; private set; }

        public SettingsForm(int currentInputDevice, int currentOutputDevice, string currentSamplesFolder)
        {
            InitializeComponent();
            SelectedInputDevice = currentInputDevice;
            SelectedOutputDevice = currentOutputDevice;
            SamplesFolder = currentSamplesFolder;
            LoadAudioDevices();
            samplesFolderTextBox.Text = SamplesFolder;
        }

        private void LoadAudioDevices()
        {
            // Загружаем устройства ввода (микрофоны)
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                inputDeviceCombo.Items.Add($"{i}: {caps.ProductName}");
            }

            if (inputDeviceCombo.Items.Count > 0)
            {
                inputDeviceCombo.SelectedIndex = Math.Min(SelectedInputDevice, inputDeviceCombo.Items.Count - 1);
            }

            // Загружаем устройства вывода (динамики/наушники)
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                outputDeviceCombo.Items.Add($"{i}: {caps.ProductName}");
            }

            if (outputDeviceCombo.Items.Count > 0)
            {
                outputDeviceCombo.SelectedIndex = Math.Min(SelectedOutputDevice, outputDeviceCombo.Items.Count - 1);
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (inputDeviceCombo.SelectedIndex >= 0)
            {
                SelectedInputDevice = inputDeviceCombo.SelectedIndex;
            }

            if (outputDeviceCombo.SelectedIndex >= 0)
            {
                SelectedOutputDevice = outputDeviceCombo.SelectedIndex;
            }

            SamplesFolder = samplesFolderTextBox.Text;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BrowseFolderButton_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Выберите папку для семплов";
                folderDialog.SelectedPath = SamplesFolder;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    samplesFolderTextBox.Text = folderDialog.SelectedPath;
                }
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void InitializeComponent()
        {
            this.Text = "Настройки";
            this.Size = new System.Drawing.Size(450, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Метка для устройства ввода
            var inputLabel = new Label
            {
                Text = "Устройство ввода (запись):",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(200, 20)
            };
            this.Controls.Add(inputLabel);

            // ComboBox для устройства ввода
            inputDeviceCombo = new ComboBox
            {
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(400, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(inputDeviceCombo);

            // Метка для устройства вывода
            var outputLabel = new Label
            {
                Text = "Устройство вывода (воспроизведение):",
                Location = new System.Drawing.Point(20, 85),
                Size = new System.Drawing.Size(200, 20)
            };
            this.Controls.Add(outputLabel);

            // ComboBox для устройства вывода
            outputDeviceCombo = new ComboBox
            {
                Location = new System.Drawing.Point(20, 110),
                Size = new System.Drawing.Size(400, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(outputDeviceCombo);

            // Метка для папки семплов
            var samplesFolderLabel = new Label
            {
                Text = "Папка семплов:",
                Location = new System.Drawing.Point(20, 150),
                Size = new System.Drawing.Size(200, 20)
            };
            this.Controls.Add(samplesFolderLabel);

            // TextBox для папки семплов
            samplesFolderTextBox = new TextBox
            {
                Location = new System.Drawing.Point(20, 175),
                Size = new System.Drawing.Size(315, 25)
            };
            this.Controls.Add(samplesFolderTextBox);

            // Кнопка Browse для выбора папки
            browseFolderButton = new Button
            {
                Text = "Выбрать",
                Location = new System.Drawing.Point(340, 173),
                Size = new System.Drawing.Size(80, 30)
            };
            browseFolderButton.Click += BrowseFolderButton_Click;
            this.Controls.Add(browseFolderButton);

            // Кнопка Save
            saveButton = new Button
            {
                Text = "Сохранить",
                Location = new System.Drawing.Point(235, 220),
                Size = new System.Drawing.Size(100, 30)
            };
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Кнопка Cancel
            cancelButton = new Button
            {
                Text = "Отмена",
                Location = new System.Drawing.Point(340, 220),
                Size = new System.Drawing.Size(80, 30)
            };
            cancelButton.Click += CancelButton_Click;
            this.Controls.Add(cancelButton);
        }

        private ComboBox inputDeviceCombo;
        private ComboBox outputDeviceCombo;
        private TextBox samplesFolderTextBox;
        private Button browseFolderButton;
        private Button saveButton;
        private Button cancelButton;
    }
}