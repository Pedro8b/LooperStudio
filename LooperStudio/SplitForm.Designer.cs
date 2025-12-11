using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Drawing;
using System.Windows.Forms;

namespace LooperStudio
{
    partial class SplitForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // SplitForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(184, 110);
            BackColor = Color.FromArgb(37, 37, 38);
            Name = "SplitForm";
            Text = "Form1";
            ResumeLayout(false);
            InitializeSlider();
        }

        private void InitializeSlider()
        {
            DenumLabel = new Label
            {
                Text = "1",
                Location = new Point(50, 50),
                Size = new Size(80, 20),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(DenumLabel);

            Slider = new System.Windows.Forms.TrackBar
            {
                Size = new System.Drawing.Size(180, 40),
                Location = new System.Drawing.Point(0, 5),
                Maximum = 8,
                Minimum = 0,
                TickFrequency = 1,
                LargeChange = 2,
                SmallChange = 1,
            };
            this.Controls.Add(Slider);
            Slider.ValueChanged += Slider_ValueChanged;

            SubmitSlider = new System.Windows.Forms.Button
            {
                Text = "Разделить",
                Location = new Point(50, 75),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            this.Controls.Add(SubmitSlider);
            SubmitSlider.Click += SubmitSlider_Click;
        }

        #endregion
        private System.Windows.Forms.TrackBar Slider;
        private Label DenumLabel;
        private System.Windows.Forms.Button SubmitSlider;
    }
}