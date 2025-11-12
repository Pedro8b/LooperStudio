namespace LooperStudio
{
    public partial class MainForm : Form
    {
        Recorder RecordInstance = new Recorder();
        Playback PlaybackInstance = new Playback();
        public MainForm()
        {
            InitializeComponent();
        }
        private void Record_Click(object sender, EventArgs e)
        {
            RecordInstance.Record();
        }
        private void Play_Click(object sender, EventArgs e)
        {
            PlaybackInstance.Play();
        }
        private void Stop_Click(object sender, EventArgs e)
        {
            PlaybackInstance.Stop();
            RecordInstance.StopRecording();

        }
    }
}
