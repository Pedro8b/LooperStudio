using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LooperStudio
{
    public partial class SplitForm : Form
    {
        public double split = 0;
        public SplitForm()
        {
            InitializeComponent();
        }
        private void Slider_ValueChanged(object sender, EventArgs e)
        {
            DenumLabel.Text = (Slider.Value / 8.0).ToString();
        }
        private void SubmitSlider_Click(object sender, EventArgs e)
        {
            split = Slider.Value / 8.0;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
