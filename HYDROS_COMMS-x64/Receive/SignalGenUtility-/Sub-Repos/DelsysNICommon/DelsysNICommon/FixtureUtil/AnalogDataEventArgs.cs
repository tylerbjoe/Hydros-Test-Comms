using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DelsysNICommon.Util
{
    public class AnalogDataEventArgs : EventArgs
    {
        public int ChannelNum { get; set; }
        public double[,] SamplesBuffer { get; set; }
        public double SampleRate { get; set; }
        public int BufferSize { get; set; }

        public AnalogDataEventArgs(int a, double[,] b, double c, int d)
        {
            ChannelNum = a;
            SamplesBuffer = b;
            SampleRate = c;
            BufferSize = d;
        }
    }
}
