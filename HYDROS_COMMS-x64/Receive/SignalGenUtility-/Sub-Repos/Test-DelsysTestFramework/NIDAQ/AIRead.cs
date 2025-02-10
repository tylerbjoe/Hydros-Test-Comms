using DelsysNICommon;
using DelsysTestLib.NIDAQ;
using DelsysTestLib.Util;
using System.Collections.Concurrent;
using System.Diagnostics;
using System;


namespace DelsysTestFramework.NIDAQ
{
    public class AIRead
    {
        NIDAQController DAQ => FixtureFramework.Instance.DAQ;

        public int SAMPLE_RATE { get; set; } = 50_000;
        public int INPUT_BUFFER { get; set; } = 10_000;
        private string[] CHANNELS;
        public int[] CHANNELS_NUMBER;
        public double[,] samplesBuffer;

        public event EventHandler<GotInputDataEventArgs>? GotInputData;


        public AIRead()
        {
            samplesBuffer = new double[1, INPUT_BUFFER];
            CHANNELS = new string[] { "" };
            CHANNELS_NUMBER = new int[] { 0 };
        }
        public double ReadVoltageAI(string pin)
        {
            double[] voltage_Dut_V = DAQ.AnalogReadVoltageSync(INPUT_BUFFER, pin, SAMPLE_RATE); // samples in 0.05 seconds
            return voltage_Dut_V.Average();
        }
        public double TakeSampleAI(string pin)
        {
            return DAQ.ReadOneVoltage(pin);
        }
        public bool ReadAnalogInputAsDigital(string pin)
        {
            return TakeSampleAI(pin) > 2.5;
        }
        public string[] GetAIPinNames()
        {
            var states = new List<string>();
            foreach (var pin in DAQ.Part.AIPins)
            {
                states.Add(DAQ.Part.GetNameByPin(pin));
            }
            return states.ToArray();
        }
        public void StopAnalogRead()
        {
            DAQ.AnalogEndReadVoltage(currentAnalogTaskName);
            currentAnalogTaskName = "";
        }
        public void StopAnalogRead(double range)
        {
            DAQ.AnalogEndReadVoltage(currentAnalogTaskName);
            currentAnalogTaskName = "";
        }


        private string currentAnalogTaskName = "";
        private AsyncCallback analogCallback;
        public void StartAnalogRead(string[] CHANNELS, string taskName = "MonitorTask10")
        {
            this.CHANNELS = CHANNELS;
            for (int i = 0; i < CHANNELS.Length; i++)
            {
                CHANNELS_NUMBER[i] = int.Parse(CHANNELS[i].Substring(2));
            }

            samplesBuffer = new double[1, INPUT_BUFFER];
            analogCallback = new AsyncCallback(OnInputData);


            DAQ.AnalogBeginReadVoltage(taskName, CHANNELS, INPUT_BUFFER, SAMPLE_RATE, analogCallback);
            currentAnalogTaskName = taskName;
        }

        public void StartAnalogRead(double range, string[] CHANNELS, string taskName = "MonitorTask")
        {
            if (currentAnalogTaskName != "")
                DAQ.AnalogEndReadVoltage(currentAnalogTaskName);

            this.CHANNELS = CHANNELS;
            samplesBuffer = new double[1, INPUT_BUFFER];
            analogCallback = new AsyncCallback(OnInputData);
            currentAnalogTaskName = $"{taskName}{range}";
            DAQ.AnalogBeginReadVoltage($"{taskName}{range}", CHANNELS, INPUT_BUFFER, SAMPLE_RATE, analogCallback, range);
            Producer();
        }
        public void StartPsigAnalogRead(double range)
        {
            if (currentAnalogTaskName != "")
                DAQ.AnalogEndReadVoltage(currentAnalogTaskName);

            samplesBuffer = new double[1, INPUT_BUFFER];
            analogCallback = new AsyncCallback(OnInputData);
            currentAnalogTaskName = $"Psig{range}";
            this.CHANNELS = new string[] { "AI30", "AI31" };
            DAQ.AnalogBeginReadVoltage($"Psig{range}", CHANNELS, INPUT_BUFFER, SAMPLE_RATE, analogCallback, range);
        }
        //public static double[] ApplyBandpassFilter(double[] input, double lowCutoff, double highCutoff, int sampleRate, int filterLength)
        //{
        //    // Generate the FIR filter coefficients using a simple windowed-sinc function
        //    var coefficients = DesignBandpassFilter(lowCutoff, highCutoff, sampleRate, filterLength);

        //    // Apply the filter using convolution
        //    double[] output = new double[input.Length];

        //    for (int i = 0; i < input.Length; i++)
        //    {
        //        double sum = 0;
        //        for (int j = 0; j < coefficients.Length; j++)
        //        {
        //            if (i - j >= 0)
        //            {
        //                sum += coefficients[j] * input[i - j];
        //            }
        //        }
        //        output[i] = sum;
        //    }

        //    return output;
        //}

        //private static double[] DesignBandpassFilter(double lowCutoff, double highCutoff, int sampleRate, int filterLength)
        //{
        //    // Normalize the frequencies by the Nyquist frequency
        //    double nyquist = sampleRate / 2.0;
        //    double lowNorm = lowCutoff / nyquist;
        //    double highNorm = highCutoff / nyquist;

        //    // Create a sinc function for the bandpass filter
        //    double[] filter = new double[filterLength];
        //    for (int i = 0; i < filterLength; i++)
        //    {
        //        if (i == filterLength / 2) // the central value should be the difference of the frequencies
        //        {
        //            filter[i] = highNorm - lowNorm;
        //        }
        //        else
        //        {
        //            double n = i - filterLength / 2;
        //            filter[i] = (Math.Sin(2 * Math.PI * highNorm * n) - Math.Sin(2 * Math.PI * lowNorm * n)) / (Math.PI * n);
        //        }
        //    }

        //    // Apply a window function (Hamming Window here)
        //    for (int i = 0; i < filter.Length; i++)
        //    {
        //        filter[i] *= 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (filter.Length - 1)); // Hamming window
        //    }

        //    return filter;
        //}

        public bool NiFailed = false;
        public BlockingCollection<double[,]> DataBuffer = new BlockingCollection<double[,]>();
        public object LockQueue = new object();
        public void OnInputData(IAsyncResult result)
        {
            Task.Run(() =>
            {
            //    try
            //    {
            //        if (DAQ.RequestedStop) // if none are running then don't continue
            //            return;

            //        samplesBuffer = DAQ.AnalogContinueReadVoltage(currentAnalogTaskName, result, analogCallback);
            //        DataBuffer.TryAdd(samplesBuffer, 100);
            //        //GotInputData?.Invoke(this, new GotInputDataEventArgs(CHANNELS_NUMBER[0], samplesBuffer, SAMPLE_RATE, INPUT_BUFFER));
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine("OnInputData:" + ex);
            //    }
            });
        }
        
        public void Producer()
        {
            //String mode = "DAQ";
            String mode = "File";
            double[] yValues = Array.Empty<double>(); ;
            if (mode == "File")
            {

                //string filePath = "U:\\Users Common\\TJoe\\Brandeis Pool Tests\\1.16.25\\trial_23.bin";
                //string filePath = "C:\\Users\\TJoe\\OneDrive - Delsys Inc\\TJ HYDROS\\HYDROS comms\\laptop demod\\t23bp.bin";
                string filePath = "U:\\Users Common\\TJoe\\Brandeis Pool Tests\\2.6.25\\trial_13_packets\\packet_5.bin";

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    try
                    {
                        // Calculate the total number of floats in the file
                        long floatCount = fs.Length / sizeof(float);

                        // Initialize the double array with the appropriate size
                        yValues = new double[floatCount];

                        for (long i = 0; i < floatCount; i++)
                        {
                            // Read a 32-bit float from the file and convert it to double
                            float value = reader.ReadSingle();
                            yValues[i] = (double)value;
                        }

                        Debug.WriteLine($"Successfully read {yValues.Length} values as double[].");
                    }
                    catch (EndOfStreamException)
                    {
                        Debug.WriteLine("Reached the end of the file unexpectedly.");
                        yValues = Array.Empty<double>();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"An error occurred while reading the file: {ex.Message}");
                        yValues = Array.Empty<double>();
                    }
                }
            }
            Task.Run(() =>
            {
                int currentIndex = 0;

                while (!DAQ.RequestedStop && mode=="DAQ")
                {
                    double[,] data = DAQ.AnalogContinueReadVoltageProducer(INPUT_BUFFER);
                    DataBuffer.TryAdd(data, 100);
                }
                while (currentIndex < yValues.Length && mode == "File")
                {
                    try
                    {
                        if (yValues != null && currentIndex < yValues.Length)
                        {
                            // Determine the actual size of the current batch
                            int remaining = yValues.Length - currentIndex;
                            int currentBatchSize = Math.Min(INPUT_BUFFER, remaining);

                            // Create a new 2D array with the batch size
                            double[,] dataBatch = new double[1, currentBatchSize];
                            for (int i = 0; i < currentBatchSize; i++)
                            {
                                dataBatch[0, i] = yValues[currentIndex + i];
                            }

                            // Add the batch to the DataBuffer
                            DataBuffer.TryAdd(dataBatch, 100);

                            // Update the index to process the next batch
                            currentIndex += currentBatchSize;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in File mode loop: {ex.Message}");
                    }
                }
            });
        }
    }
}
