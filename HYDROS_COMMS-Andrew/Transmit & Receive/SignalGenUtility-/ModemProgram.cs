using Org.BouncyCastle.Asn1.Ocsp; // why is this randomly spawning in
using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;
using MathNet.Numerics.Interpolation;
using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Globalization;
using MathNet.Numerics.LinearAlgebra;
using NAudio.Wave.Compression;
using Aero.PipeLine;
using SciChart.Data.Model;
using DelsysSigNIalGen.Utilities;
using NAudio.Wave;
using Microsoft.VisualBasic.ApplicationServices;
using ControlzEx.Standard;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Net.Mime.MediaTypeNames;
using DelsysSigNIalGen.ViewModel;

namespace DelsysSigNIalGen
{
    class ModemProgram
    {
        private static readonly object consoleLock = new object();
        private static bool initialized = false;

        public static bool globalStopped = false;
        public static int trialNum = 7; // Changes names of files
        public static int secretCarrierFrequency = 100_000;

        static void GetRecMessages(NetworkStream stream)
        {
            byte[] responseData = new byte[256];

            int bytes = stream.Read(responseData, 0, responseData.Length); // .Result
            string response = Encoding.UTF8.GetString(responseData, 0, bytes);
            lock (consoleLock)
            {
                //Trace.WriteLine($"Received: {response}");
            }
        }

        static List<int> GetRecMessagesData(NetworkStream stream)
        {
            byte[] responseData = new byte[256];

            int bytes = stream.Read(responseData, 0, responseData.Length); // .Result
            string response = Encoding.UTF8.GetString(responseData, 0, bytes);

            lock (consoleLock)
            {
                Trace.WriteLine($"Received: {response}");
            }

            if (response.Contains("Data"))
            {
                // Find the index of the start
                int startIndex = response.IndexOf('[');
                if (startIndex != -1)
                {
                    // Find end index
                    int endIndex = response.LastIndexOf(']');
                    if (endIndex != -1)
                    {
                        // Extract the substring containing the "Data" array
                        string dataSubstring = response.Substring(startIndex, endIndex - startIndex + 1); // second arg is length

                        // Parse the substring into a list of integers
                        List<int> dataList = ParseDataList(dataSubstring);
                        return dataList;
                    }
                }
            }

            // "Data" keyword not found or invalid format, return null
            return null;
        }

        static List<int> GetRecMessagesDataDecode(NetworkStream stream)
        {
            byte[] responseData = new byte[256];

            int bytes = stream.Read(responseData, 0, responseData.Length); // .Result
            string response = Encoding.UTF8.GetString(responseData, 0, bytes);

            //lock (consoleLock)
            //{
            //    Trace.WriteLine($"Received: {response}");
            //}

            // add to log
            using (StreamWriter sw = new StreamWriter($"..\\outputLogs\\outputLog_{trialNum}.txt", true))
            {
                sw.WriteLine(response);
            }

            if (response.Contains("Data"))
            {
                // Find the index of the start of the "Data" array
                int startIndex = response.IndexOf('[');
                if (startIndex != -1)
                {
                    // Find the end index of the "Data" array
                    int endIndex = response.LastIndexOf(']');
                    if (endIndex != -1)
                    {
                        // Extract the substring containing the "Data" array
                        string dataSubstring = response.Substring(startIndex, endIndex - startIndex + 1);

                        // Parse the substring into a list of integers
                        List<int> dataList = ParseDataList(dataSubstring);
                        return dataList;
                    }
                }
            }

            // "Data" keyword not found or invalid format, return null
            return null;
        }

        private static List<int> ParseDataList(string dataSubstring)
        {
            // Parse the substring into a list of integers
            List<int> dataList = new List<int>();
            string[] parts = dataSubstring.Split(new[] { ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (int.TryParse(part, out int number))
                {
                    dataList.Add(number);
                }
            }
            return dataList;
        }


        // Send JSON string & get numRec responses back
        static void SendJsonCommand(string jsonCommand, int numRec, NetworkStream stream)
        {
            lock (consoleLock)
            {
                // Translate passed message into UTF-8 and store it as a byte array
                byte[] data = Encoding.UTF8.GetBytes(jsonCommand + "\n"); // Newline for encoding

                // Send JSON command to connected TcpServer
                stream.Write(data, 0, data.Length);
                //lock (consoleLock)
                {
                    Trace.WriteLine($"Sent: {jsonCommand}");
                }
                // Read response bytes
                for (int i = 0; i < numRec; i++) // Could switch this to timing out
                {
                    GetRecMessages(stream);
                }
            }
        }

        static void SetValue(string element, double value, int numRec, NetworkStream stream)
        {
            //lock (consoleLock)
            {
                var setVal = new
                {
                    Command = "SetValue",
                    Arguments = $"{element} {value} 0"
                };
                string jsonString = JsonSerializer.Serialize(setVal);
                SendJsonCommand(jsonString, numRec, stream);
            }
        }

        static void startrx(NetworkStream stream)
        {
            var rxStart = new
            {
                Command = "Event_StartRx",
                Arguments = "Unused Arguments"
            };
            string jsonString = JsonSerializer.Serialize(rxStart);
            SendJsonCommand(jsonString, 2, stream);
        }

        static void sendData(int hr, int spo2, int numRec, NetworkStream stream)
        {
            var data = new
            {
                Command = "TransmitJSON",
                Arguments = new
                {
                    Payload = new
                    {
                        Data = new int[] { hr, spo2 } // change number of bits // 48 bits! // 32 bits!
                    }
                }
            };
            string jsonString = JsonSerializer.Serialize(data);
            SendJsonCommand(jsonString, numRec, stream);
        }

        //public static (TcpClient, TcpClient) ModemInit(BlockingCollection<float[]> waveBuff, BlockingCollection<float[]> spectBuff)
        public static (NetworkStream, NetworkStream, TcpClient, TcpClient, NetworkStream, NetworkStream, TcpClient, TcpClient) ModemInit(BlockingCollection<float[]> waveBuff)
        {
            if (initialized == true)
            {
                return (null, null, null, null, null, null, null, null);
            }
            initialized = true;

            Trace.Listeners.Clear(); // Clear existing listeners (if needed) -- there are multiple
            Trace.Listeners.Add(new DefaultTraceListener()); // ensures exactly 1 thread through here

            foreach (TraceListener listener in Trace.Listeners)
            {
                Debug.WriteLine($"Listener: {listener.GetType().FullName}");
            }

            // Check/print out the count of listeners
            Debug.WriteLine($"Total listeners: {Trace.Listeners.Count}");

            // Create TCP/IP socket, using this source:https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient?view=net-8.0
            string ipAddress = "127.0.0.1";
            int port = 17000;
            int pcmPort = 17003;

            TcpClient client = new TcpClient(ipAddress, port);
            TcpClient pcmClient = new TcpClient(ipAddress, pcmPort);

            NetworkStream stream = client.GetStream();
            NetworkStream pcmStream = pcmClient.GetStream();

            int portDec = 17100;
            int pcmPortDec = 17103;

            TcpClient clientDec = new TcpClient(ipAddress, portDec);
            TcpClient pcmClientDec = new TcpClient(ipAddress, pcmPortDec);

            NetworkStream streamDec = clientDec.GetStream();
            NetworkStream pcmStreamDec = pcmClientDec.GetStream();

            GetRecMessages(stream); // get num connections -- if it says 2 it's probably the python connection

            startrx(stream);
            double frequency = 35000; // var
            SetValue("Carrier", frequency, 1, stream);
            SetValue("PayloadMode", 0, 1, stream);
            SetValue("UPCONVERT_OutputScale", 10, 1, stream);

            Trace.WriteLine("Now from Decode port: ");

            GetRecMessages(streamDec); // get num connections

            startrx(streamDec);
            double frequencyDec = 35000; // var
            SetValue("Carrier", frequencyDec, 1, streamDec);
            SetValue("PayloadMode", 0, 1, streamDec);

            //sendCSV(stream, pcmStream, 0, new float[1]);
            //sendCSV(streamDec, pcmStreamDec, 0, new float[1]);

            // Possibly necessary control commands
            //await SetValue("UPCONVERT_Carrier", frequency + 100, 1, mainStream);
            //await SetValue("DOWNCONVERT_Carrier", frequency - 100, 1, mainStream);
            //await SetValue("UPCONVERT_OutputScale", 5, 1, mainStream); // min: 0, max: 10
            //await SetValue("TxPower", 13, 1, mainStream); // min: 0, max: 100
            //await SetValue("TxPowerWatts", 600, 1, mainStream); // min: 0, max: 1000
            //await SetValue("StreamingTxLen", 2560, 1, mainStream); // sending 2 bytes
            //await SetValue("PayloadMode", 0, 1, mainStream); /// check this one
            // frequency hopped

            return (stream, pcmStream, client, pcmClient, streamDec, pcmStreamDec, clientDec, pcmClientDec);
        }

        public static async Task decodeControls(NetworkStream streamDec)
        {
            int calls = 0;
            while (true)
            {
                List<int> values = null;
                values = GetRecMessagesDataDecode(streamDec);
                if (values != null)
                {
                    Trace.WriteLine($"hr: {values[0]}, spo2: {values[1]}");
                    ValuePlot.AppendData((calls, values[0]), (calls, values[1]));
                    calls++;
                }
            }
        }

        public static void decodeMyWav(NetworkStream pcmStreamDec, float[] givenData)
        {
            Trace.WriteLine($"fed: {givenData.Length}");
            byte[] byteArray = new byte[givenData.Length * sizeof(float)];
            Buffer.BlockCopy(givenData, 0, byteArray, 0, byteArray.Length);

            pcmStreamDec.Write(byteArray);

            readMyWavSize(pcmStreamDec, givenData.Length * 4);
        }

        // Use this logic when sending signals BACK, just with loaded arrays
        public static void sendCSV(NetworkStream stream, NetworkStream pcmStream, int calls, float[] givenData)
        {
            string filePath = $"U:\\Users Common\\AnnaE\\csvsUW\\DownOut_RT_0.csv"; // DownOut_RT_5 // cutOffWav33kHz
            List<float> floatList = new List<float>();
            Trace.WriteLine("loaded csv");

            // Read the CSV file
            using (StreamReader sr = new StreamReader(filePath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    // Assuming each line contains a single float value
                    if (float.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    {
                        floatList.Add(value);
                    }
                    else
                    {
                        Console.WriteLine($"Unable to parse float from line: {line}");
                    }
                }
            }

            Trace.WriteLine($"floatList len: {floatList.Count}");

            //for csv use

            byte[] byteArray = new byte[floatList.Count * sizeof(float)];
            Buffer.BlockCopy(floatList.ToArray(), 0, byteArray, 0, byteArray.Length);

            // for non-csv use
            //byte[] byteArray = new byte[givenData.Length * sizeof(float)];
            //Buffer.BlockCopy(givenData, 0, byteArray, 0, byteArray.Length);

            int FrameSize = 640;
            int nbytesFrame = FrameSize * 4; // sizeof(float) is 4 bytes
            byte[] zeroFrame = new byte[nbytesFrame];

            int idxSend = 0;
            while (idxSend + nbytesFrame <= byteArray.Length)
            {
                byte[] txout = new byte[nbytesFrame];
                Array.Copy(byteArray, idxSend, txout, 0, nbytesFrame);

                // Send the byte array
                pcmStream.Write(txout);
                idxSend += nbytesFrame;
            }

            // Send the zeroFrame at the end
            pcmStream.Write(zeroFrame);
            Trace.WriteLine("Waiting for messages:");

            List<int> values = null;
            lock (consoleLock)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (values == null)
                    {
                        values = GetRecMessagesData(stream);
                    }
                    else
                    {
                        GetRecMessages(stream);
                    }
                }
            }
            if (values != null)
            {
                Trace.WriteLine($"hr: {values[0]}, spo2: {values[1]}");
            }

            readMyWav(pcmStream);
            Trace.WriteLine("done getting messages");
        }

        private static void readMyWav(NetworkStream pcmStream)
        {
            int lenIdx = 800;
            byte[] responseData = new byte[2560];
            float[] resArray = new float[102400 * 5];

            int nbytes;
            int idx = 0;
            while (idx < lenIdx && (nbytes = pcmStream.Read(responseData, 0, responseData.Length)) > 0) // 400 is hardcoded here; for length of file; can do math on this once returned sample rate is known
            {
                idx++;
            }
            Trace.WriteLine("DONE READING WAV");
        }

        // all zeros pumping
        static void sendBytes(NetworkStream pcmStream)
        {
            byte[] emptyWav = new byte[2_048_000];
            SendJsonWav(emptyWav, 0, pcmStream);
        }

        private static void readMyWavSize(NetworkStream pcmStream, int nbytesTot)
        {
            byte[] responseData = new byte[nbytesTot];
            int nbytes = pcmStream.Read(responseData, 0, responseData.Length);
        }

        static void SendJsonWav(byte[] wav, int numRec, NetworkStream pcmStream)
        {
            pcmStream.Write(wav, 0, wav.Length);
        }

        public static async Task GetVals(NetworkStream stream, NetworkStream pcmStream, BlockingCollection<float[]> waveBuff)
        {
            int calls = 0;
            while (globalStopped == false)
            {
                Thread.Sleep(800); // for debugging non-real time, should pause a bit
                (int hr, int spo2) = getNextVals(calls);
                RunModemProgram(hr, spo2, stream, pcmStream, calls, waveBuff);
                calls++;
            }
        }

        private static (int, int) getNextVals(int calls)
        {
            int hr = 0;
            int spo2 = 0;
            string filePath = ($"..\\test_vals\\vals_{calls}.csv");
            while (true)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        string[] lines = File.ReadAllLines(filePath);

                        if (lines.Length > 0)
                        {
                            string[] values = lines[0].Split(',');

                            if (values.Length == 2 && int.TryParse(values[0], out hr) && int.TryParse(values[1], out spo2))
                            {
                                //Trace.WriteLine($"Algorithm Vals: {hr}, {spo2}");
                                return (hr, spo2);
                            }
                            else
                            {
                                //Console.WriteLine("File format is incorrect.");
                            }
                        }
                        else
                        {
                            //Console.WriteLine("File is empty.");
                        }
                    }
                    else
                    {
                        //Console.WriteLine("File does not exist.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }            
        }

        public static void RunModemProgram(int heart_rate, int spo2, NetworkStream stream, NetworkStream pcmStream, int calls, BlockingCollection<float[]> waveBuff) // called after algs
        {
            sendData(heart_rate, spo2, 0, stream);
            sendBytes(pcmStream);

            GetRecMessages(stream);
            GetRecMessages(stream);
            GetRecMessages(stream);
            GetRecMessages(stream);

            byte[] responseData = new byte[5120]; // 5120 // 2560 floats (4 bytes each)
            float[] resArray = new float[102400 * 5];

            int nbytes;
            int resI = 0;
            int nbytesTot = 0;

            while (nbytesTot < 2_048_000)
            {
                nbytes = pcmStream.Read(responseData, 0, responseData.Length);
                nbytesTot += nbytes;

                int floatCount = nbytes / 4;

                // Convert bytes to floats
                for (int i = 0; i < floatCount; i++)
                {
                    // Convert 4 bytes to a float
                    float value = BitConverter.ToSingle(responseData, i * 4);
                    if (resI < resArray.Length)
                    {
                        resArray[resI] = value;
                    }
                    resI++;
                }
            }

            float[] cutArray;
            if (calls == 0) // keep the first zeros
            {
                (int _, int lastIdx) = GetIdxs(resArray);
                cutArray = resArray[..(lastIdx + 10_000)];
            }
            else // cut on both sides
            {
                (int firstIdx, int lastIdx) = GetIdxs(resArray);
                cutArray = resArray[(firstIdx - 10_000)..(lastIdx + 10_000)];
            }

            UpsampleUpshift(cutArray, waveBuff, calls);
            Trace.WriteLine($"Added waveform #{calls}!");
        }

        private static (int, int) GetIdxs(float[] data)
        {
            int first = 0;
            int last = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    first = i;
                    break;
                }
            }
            int conseq = 0;
            for (int j = first; j < data.Length ; j++)
            {
                if (data[j] == 0)
                {
                    conseq++;
                }
                else
                {
                    conseq = 0;
                }
                if (conseq > 5)
                {
                    last = j;
                    break;
                }
            }
            Trace.WriteLine($"FIRST & LAST: {first} & {last}");
            return (first, last);
        }

        static double[] DesignLowPassFIRFilter(double normalizedCutoff, int filterOrder, double[] window)
        {
            int halfOrder = filterOrder / 2;
            double[] h = new double[filterOrder];

            for (int i = 0; i < filterOrder; i++)
            {
                if (i == halfOrder)
                {
                    h[i] = normalizedCutoff;
                }
                else
                {
                    double numerator = Math.Sin(Math.PI * normalizedCutoff * (i - halfOrder));
                    double denominator = Math.PI * (i - halfOrder);
                    h[i] = numerator / denominator;
                }

                h[i] *= window[i];
            }

            return h;
        }

        static int NextPowerOfTwo(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        static Complex[] Filfilt(double[] b, Complex[] x)
        {
            int len = x.Length;
            int n = b.Length;

            // Pad the filter and input arrays to the next power of two for efficient FFT computation
            int fftSize = NextPowerOfTwo(len + n - 1);

            // Apply the forward filter using FFT convolution
            Complex[] y = FftConvolve(x, b, fftSize);

            // Apply the backward filter using FFT convolution
            Array.Reverse(y);
            Complex[] z = FftConvolve(y, b, fftSize);
            Array.Reverse(z);

            return z;
        }

        static Complex[] FftConvolve(Complex[] x, double[] b, int fftSize)
        {
            // Convert filter coefficients to Complex
            Complex[] bComplex = Array.ConvertAll(b, item => new Complex(item, 0));

            // Pad the input and filter arrays
            Complex[] xPadded = new Complex[fftSize];
            Complex[] bPadded = new Complex[fftSize];
            Array.Copy(x, xPadded, x.Length);
            Array.Copy(bComplex, bPadded, bComplex.Length);

            // Perform FFT on both padded arrays
            Fourier.Forward(xPadded, FourierOptions.Matlab);
            Fourier.Forward(bPadded, FourierOptions.Matlab);

            // Element-wise multiplication in the frequency domain
            for (int i = 0; i < fftSize; i++)
            {
                xPadded[i] *= bPadded[i];
            }
            // Inverse FFT to get the time domain result
            Fourier.Inverse(xPadded, FourierOptions.Matlab);

            // Truncate the result to the original signal length
            Complex[] result = new Complex[x.Length];
            Array.Copy(xPadded, result, x.Length);

            return result;
        }

        public static void UpsampleUpshift(float[] sigin, BlockingCollection<float[]> waveBuff, int calls, bool down_next = false)
        {
            float fs_og = 102_400;
            float fc_og = 35_000;
            float fs_up = 1_000_000;
            float fc_up = secretCarrierFrequency;

            // Modulate to baseband
            int sigLen = sigin.Length;
            Complex[] sigComplex = new Complex[sigLen];
            for (int i = 0; i < sigLen; i++)
            {
                double phase = -2 * Math.PI * fc_og * (i + 1) / fs_og; // +1 to match Python's 1-based indexing in arange
                sigComplex[i] = new Complex(sigin[i], 0) * Complex.Exp(new Complex(0, phase));
            }

            // FIR filter with Hamming window
            int filterOrder = 1024;
            double cutoffFreq = 2 * 5120 / fs_og;

            double[] hammingWindow = Window.Hamming(filterOrder);
            // Design the FIR filter coefficients
            double[] firCoefficients = DesignLowPassFIRFilter(cutoffFreq, filterOrder, hammingWindow);
            // Apply filtfilt
            Complex[] bb_filtered = Filfilt(firCoefficients, sigComplex);

            // Resample
            int up = (int)fs_up;
            int down = (int)fs_og;
            var resampled = Resample(bb_filtered, up, down);

            // Modulate to passband
            int resampledLen = resampled.Length;
            Complex[] pb = new Complex[resampledLen];
            for (int i = 0; i < resampledLen; i++)
            {
                double phase = 2 * Math.PI * fc_up * (i + 1) / fs_up; // +1 to match np.arange start @ 1
                pb[i] = resampled[i] * Complex.Exp(new Complex(0, phase));
            }
            if (down_next)
            {
                // not implementing this rn
                //return pb.Select(c => (float)c.Magnitude).ToArray();
            }
            else
            {
                float[] sigout = new float[pb.Length];
                for (int i = 0; i < pb.Length; i++)
                {
                    sigout[i] = (float)(pb[i].Real - pb[i].Imaginary);
                }
                // Define input range
                float inputMin = sigout.Min();
                float inputMax = sigout.Max();

                // Define output range (+5V to -5V)
                float outputMin = -5f; // -5f
                float outputMax = 5f; // 5f

                // Linearly scale the values
                float[] scaledValues = new float[sigout.Length];
                for (int i = 0; i < sigout.Length; i++)
                {
                    scaledValues[i] = Map(sigout[i], inputMin, inputMax, outputMin, outputMax);
                }
                waveBuff.Add(scaledValues);
            }
        }

        public static Complex[] ResComp(Complex[] input, int newLength)
        {
            int origLength = input.Length;

            if (newLength == origLength)
                return input; // No change needed

            // Perform FFT on input signal
            Complex[] forwardOut = new Complex[origLength];
            fftDLLLayer.fftwTransform(input, forwardOut, origLength); // WAY FASTER!
            //Fourier.Forward(input, FourierOptions.Matlab);
            input = forwardOut;

            // Resampling using Fourier method
            Complex[] output = new Complex[newLength];

            int N = Math.Min(newLength, origLength);
            int halfN = (N + 1) / 2;
            int offset = (newLength - origLength) / 2;

            // Copy positive frequency components
            for (int i = 0; i < halfN; i++)
            {
                output[i] = input[i];
            }

            // Copy negative frequency components
            for (int i = 0; i < (N - 1) / 2; i++)
            {
                output[newLength - (i + 1)] = input[origLength - (i + 1)];
            }

            // Handle special case for even N
            if (N % 2 == 0)
            {
                if (N < origLength)  // downsampling
                {
                    output[N / 2] += input[N / 2];
                }
                else if (N < newLength)  // upsampling
                {
                    output[newLength - N / 2] /= 2;
                    output[N / 2] = output[newLength - N / 2];
                }
            }

            // Perform inverse FFT to get resampled signal
            Fourier.Inverse(output, FourierOptions.Matlab);

            // Scale the result due to FFT scaling
            double scale = (double)newLength / origLength;
            for (int i = 0; i < output.Length; i++)
            {
                output[i] *= scale;
            }

            return output;
        }

        public static float[] DownsampleDownshiftNew(double[] sigin, int calls)
        {
            float fs_down = 102_400; // changed 102_400
            float fc_down = 35_000;
            float fs_bef = 1_000_000;
            float fc_bef = secretCarrierFrequency; // Changes based on signal though...

            // Undo modulate to passband
            int sigLen = sigin.Length;
            Complex[] sigComplex = new Complex[sigLen];
            for (int i = 0; i < sigLen; i++)
            {
                double pb_factor = 2 * Math.PI * fc_bef * (i + 1) / fs_bef; // +1 to match Python's 1-based indexing in arange
                sigComplex[i] = Complex.Divide(new Complex(sigin[i], 0), Complex.Exp(new Complex(0, pb_factor)));
            }

            int resLen = (int)Math.Round(sigin.Length * 102_400.0 / 1_000_000.0);
            var resampled = ResComp(sigComplex, resLen); // 512000

            // Undo modulate to baseband
            int resampledLen = resampled.Length;
            Complex[] no_bb = new Complex[resampledLen];
            for (int i = 0; i < resampledLen; i++)
            {
                double bb_factor = -2 * Math.PI * fc_down * (i + 1) / fs_down; // +1 to match np.arange start @ 1
                no_bb[i] = Complex.Divide(resampled[i], Complex.Exp(new Complex(0, bb_factor)));
            }

            float[] sigout = new float[no_bb.Length];
            for (int i = 0; i < no_bb.Length; i++)
            {
                sigout[i] = (float)(no_bb[i].Real - no_bb[i].Imaginary);
            }

            return sigout;

            // skip the scaling -- use raw signal data
            // Define input range
            float inputMin = sigout.Min();
            float inputMax = sigout.Max();

            // Define output range (+5V to -5V)
            float outputMin = -5f; // -5f
            float outputMax = 5f; // 5f

            // Linearly scale the values
            float[] scaledValues = new float[sigout.Length];
            for (int i = 0; i < sigout.Length; i++)
            {
                scaledValues[i] = Map(sigout[i], inputMin, inputMax, outputMin, outputMax);
            }

            return scaledValues;
        }


        private static Complex[] Resample(Complex[] input, double fs_next, double fs_og) // cubic spline interp
        {
            int inputLen = input.Length;
            int outputLen = (int)Math.Ceiling(inputLen * fs_next / fs_og);
            double[] xOrig = Enumerable.Range(0, inputLen).Select(i => (double)i / fs_og).ToArray();
            double[] xResampled = Enumerable.Range(0, outputLen).Select(i => (double)i / fs_next).ToArray();

            // Interpolate real and imaginary parts separately
            double[] realOrig = input.Select(c => c.Real).ToArray();
            double[] imagOrig = input.Select(c => c.Imaginary).ToArray();

            var realSpline = CubicSpline.InterpolateNatural(xOrig, realOrig);
            var imagSpline = CubicSpline.InterpolateNatural(xOrig, imagOrig);

            Complex[] output = new Complex[outputLen];
            for (int i = 0; i < outputLen; i++)
            {
                double realResampled = realSpline.Interpolate(xResampled[i]);
                double imagResampled = imagSpline.Interpolate(xResampled[i]);
                output[i] = new Complex(realResampled, imagResampled);
            }
            return output;
        }

        public static void CloseModem(TcpClient client, TcpClient pcmClient)
        {
            client.GetStream().Close();
            pcmClient.GetStream().Close();
            client.Close();
            pcmClient.Close();
            //initialized = false; -- WHILE DEBUGGING COMMENT OUT
            Debug.WriteLine("Modem Closed");
        }

        static float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax)
        {
            return outputMin + (outputMax - outputMin) * ((value - inputMin) / (inputMax - inputMin));
        }
    }
}