using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LooperStudio
{
    internal class Playback
    {
        public Playback()
        {
            filePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/ExampleRecording.wav";
        }
        public string filePath;
        WaveOut waveOut;
        AudioFileReader audioFileReader;
        public void Play()
        {
            
            try
            {
                var audioFileReader = new AudioFileReader(filePath);
                waveOut = new WaveOut();
                waveOut.DeviceNumber = 0;
                waveOut.Init(audioFileReader);
                waveOut.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public void Stop()
        {
            if (waveOut != null)
            {
                waveOut.Stop();
            }
        }
        private void Wave_PlayvStopped(object sender, StoppedEventArgs e)
        {
            waveOut.Dispose();
        }
    }
}
