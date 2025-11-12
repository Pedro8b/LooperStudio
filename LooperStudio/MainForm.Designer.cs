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
            Record = new Button();
            Stop = new Button();
            Play = new Button();
            SuspendLayout();
            // 
            // Record
            // 
            Record.Location = new Point(359, 47);
            Record.Name = "Record";
            Record.Size = new Size(75, 23);
            Record.TabIndex = 0;
            Record.Text = "Rec";
            Record.UseVisualStyleBackColor = true;
            Record.Click += Record_Click;
            // 
            // Stop
            // 
            Stop.Location = new Point(475, 47);
            Stop.Name = "Stop";
            Stop.Size = new Size(75, 23);
            Stop.TabIndex = 1;
            Stop.Text = "Stop";
            Stop.UseVisualStyleBackColor = true;
            Stop.Click += Stop_Click;
            // 
            // Play
            // 
            Play.Location = new Point(237, 47);
            Play.Name = "Play";
            Play.Size = new Size(75, 23);
            Play.TabIndex = 2;
            Play.Text = "Play";
            Play.UseVisualStyleBackColor = true;
            Play.Click += Play_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(Play);
            Controls.Add(Stop);
            Controls.Add(Record);
            Name = "MainForm";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private Button Record;
        private Button Stop;
        private Button Play;
    }
}
