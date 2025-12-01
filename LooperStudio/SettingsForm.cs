using NAudio.Wave;
using System;
using System.Windows.Forms;

namespace LooperStudio
{
    public partial class SettingsForm : Form
    {
        public int SelectedInputDevice { get; private set; }
        public int SelectedOutputDevice { get; private set; }

        public SettingsForm(int currentInputDevice, int currentOutputDevice)
        {
            InitializeComponent();
            SelectedInputDevice = currentInputDevice;
            SelectedOutputDevice = currentOutputDevice;
            LoadAudioDevices();
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

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void InitializeComponent()
        {
            this.Text = "Settings";
            this.Size = new System.Drawing.Size(450, 250);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Метка для устройства ввода
            var inputLabel = new Label
            {
                Text = "Input Device (Recording):",
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
                Text = "Output Device (Playback):",
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

            // Кнопка Save
            saveButton = new Button
            {
                Text = "Save",
                Location = new System.Drawing.Point(245, 170),
                Size = new System.Drawing.Size(80, 30)
            };
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Кнопка Cancel
            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(340, 170),
                Size = new System.Drawing.Size(80, 30)
            };
            cancelButton.Click += CancelButton_Click;
            this.Controls.Add(cancelButton);
        }

        private ComboBox inputDeviceCombo;
        private ComboBox outputDeviceCombo;
        private Button saveButton;
        private Button cancelButton;
    }
}