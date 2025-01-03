namespace DelsysTestLib.Util
{
    public class GotInputDataEventArgs : EventArgs
    {
        public int ChannelNum { get; set; }
        public double[,] SamplesBuffer { get; set; }
        public double SampleRate { get; set; }
        public int BufferSize { get; set; }

        public GotInputDataEventArgs(int a, double[,] b, double c, int d)
        {
            this.ChannelNum = a;
            this.SamplesBuffer = b;
            this.SampleRate = c;
            this.BufferSize = d;
        }

    }

}
