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
using MathNet.Filtering.Butterworth;
using static qxz;
using System.Collections;

namespace DelsysSigNIalGen
{
    class ModemProgram
    {
        private static readonly object consoleLock = new object();
        private static bool initialized = false;

        // These get accessed from MainViewModel.cs, so they are public
        public static bool globalStopped = false;
        public static int numTransducers = 2; // number of transducers to transmit
        public static int trialNum = 0; // Changes names of files
        public static int secretCarrierFrequency = -1; // Carrier frequency in Hz, for across the water // Will be set by on-screen input
        public static int secretCarrierFrequency2 = -1; // Carrier frequency in Hz, for across the water // Will be set by on-screen input
        public static int numPackets; // Change number of packets being sent
        public static float voltageAmplitude; // +/- voltageAmplitude is max/min voltage waves are sent at
        public static float voltageAmplitude2; // +/- voltageAmplitude is max/min voltage waves are sent at
        public static int sine = 0; // set true to transmit sine wave instead of packets


        // Receive control messages from modem
        static void GetRecMessages(NetworkStream stream)
        {
            byte[] responseData = new byte[256];

            int bytes = stream.Read(responseData, 0, responseData.Length); // Will wait here if there is nothing to receive
            string response = Encoding.UTF8.GetString(responseData, 0, bytes);
            lock (consoleLock)
            {
                //Trace.WriteLine($"Received: {response}"); // You can print these out for debugging
            }
        }

        // Receive control messages from modem and return and Data that is extracted
        static List<int> GetRecMessagesDataDecode(NetworkStream stream)
        {
            byte[] responseData = new byte[256];

            int bytes = stream.Read(responseData, 0, responseData.Length);
            string response = Encoding.UTF8.GetString(responseData, 0, bytes);

            //lock (consoleLock)
            //{
            //    Trace.WriteLine($"Received: {response}"); // Print out if you want to debug
            //}

            // Add to log
            using (StreamWriter sw = new StreamWriter($"C:\\Users\\TJoe\\Documents\\1_8_pooltest\\txoutputLog_{trialNum}.txt", true)) // Saves incoming messages from modem to txt file
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

        // Parse the substring into a list of integers
        private static List<int> ParseDataList(string dataSubstring)
        {
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
                Trace.WriteLine($"Sent: {jsonCommand}");

                // Read response
                for (int i = 0; i < numRec; i++)
                {
                    GetRecMessages(stream);
                }
            }
        }

        // Create & send the json string for any parameter that uses SetValue command
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

        // Construct and send proper startrx json string
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

        // Construct and send proper TransmitJSON json string with hr and spo2 values
        static void sendData(int hr, int spo2, int time_stamp, int numRec, NetworkStream stream)
        {
            var data = new
            {
                Command = "TransmitJSON",
                Arguments = new
                {
                    Payload = new
                    {
                        Data = new int[] { hr, spo2, time_stamp }
                    }
                }
            };
            string jsonString = JsonSerializer.Serialize(data);
            SendJsonCommand(jsonString, numRec, stream);
        }

        // Initialize modem connection! Return control and pcm port connections, so MainViewModel.cs can use them
        public static (NetworkStream, NetworkStream, TcpClient, TcpClient) ModemInit()
        {
            if (initialized == true)
            {
                return (null, null, null, null);
            }
            initialized = true;

            Trace.Listeners.Clear(); // Clear existing listeners (if needed) -- there are multiple
            Trace.Listeners.Add(new DefaultTraceListener()); // ensures exactly 1 thread through here

            // Check/print out the count of listeners
            Debug.WriteLine($"Total listeners: {Trace.Listeners.Count}");

            // Create TCP/IP socket, using this source:https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient?view=net-8.0
            string ipAddress = "localhost";
            int port = 17000;
            int pcmPort = 17003;

            TcpClient client = new TcpClient(ipAddress, port);
            TcpClient pcmClient = new TcpClient(ipAddress, pcmPort);

            NetworkStream stream = client.GetStream();
            NetworkStream pcmStream = pcmClient.GetStream();

            GetRecMessages(stream); // Just connecting to the modem, gives us a response (It tells us # of connections)

            // Send all initializing commands:
            startrx(stream);
            double frequency = 35000; // var
            SetValue("Carrier", frequency, 1, stream);
            SetValue("PayloadMode", 0, 1, stream);
            SetValue("UPCONVERT_OutputScale", 10, 1, stream);

            // For debugging, send a csv to make sure modem connection works
            // sendCSV(stream, pcmStream, 0);

            return (stream, pcmStream, client, pcmClient);
        }

        // Sends 5 seconds of zeros at 102.4kHz frequency. Note: 4 bytes in a float
        static void sendBytes(NetworkStream pcmStream)
        {
            byte[] emptyWav = new byte[2_048_000];
            pcmStream.Write(emptyWav, 0, emptyWav.Length);
        }

        // Read the exact amount of bytes from the pcm port, that were sent to the pcm port
        private static void readMyWavSize(NetworkStream pcmStream, int nbytesTot)
        {
            byte[] responseData = new byte[nbytesTot];
            int nbytes = pcmStream.Read(responseData, 0, responseData.Length);
        }

        // Get the hr&spo2 values from CSVs
        public static async Task GetVals(NetworkStream stream, NetworkStream pcmStream, BlockingCollection<float[]> waveBuff, CancellationTokenSource cts)
        {
            //int calls = ModemProgram.secretCarrierFrequency / 1000;
            int calls = 1;  // init calls to 0 so we begin at the 0th packet


            while (!cts.Token.IsCancellationRequested)  // Check for cancellation request
            {
                //Thread.Sleep(200); // for debugging non-real time, should pause a bit
                (int hr, int spo2, int time_stamp) = getNextVals(calls);
                RunModemProgram(hr, spo2, time_stamp, stream, pcmStream, calls, waveBuff, ModemProgram.secretCarrierFrequency, ModemProgram.voltageAmplitude);
                calls++;


                // There are currently csvs for 0-1000 values. Reset after reaching the end.
                if (calls == (ModemProgram.secretCarrierFrequency / 1000) + ModemProgram.numPackets)
                {
                    cts.Cancel(); // Cancel the task gracefully after 50 calls
                    break;
                }
            }

            Console.WriteLine("Task has been canceled or completed.");
        }

        // Get the hr&spo2 values from CSVs overloaded for 2 transducers
        public static async Task GetVals(NetworkStream stream, NetworkStream pcmStream, BlockingCollection<float[]> waveBuff, BlockingCollection<float[]> waveBuff2, CancellationTokenSource cts)
        {
            //int start = ModemProgram.secretCarrierFrequency / 1000;
            //int start2 = ModemProgram.secretCarrierFrequency2 / 1000;

            int start = 1;
            int start2 = 1;

            // init to zero on both channels. (set up right now for only 1 channel, if second is active, it will have the same data)
            int calls = start;
            int calls2 = start2;

            while (!cts.Token.IsCancellationRequested)  // Check for cancellation request
            {
                //Thread.Sleep(200); // for debugging non-real time, should pause a bit
                (int hr, int spo2, int time_stamp) = getNextVals(calls);
                RunModemProgram(hr, spo2, time_stamp, stream, pcmStream, calls, waveBuff, ModemProgram.secretCarrierFrequency, ModemProgram.voltageAmplitude);
                (int hr2, int spo22, int time_stamp2) = getNextVals(calls2);
                RunModemProgram(hr2, spo22, time_stamp2, stream, pcmStream, calls, waveBuff2, ModemProgram.secretCarrierFrequency2, ModemProgram.voltageAmplitude2);
                calls++;
                calls2++;
                

                // There are currently csvs for 0-1000 values. Reset after reaching the end.
                if (calls == start + ModemProgram.numPackets)
                {
                    cts.Cancel(); // Cancel the task gracefully after 50 calls
                    break;
                }
            }

            Console.WriteLine("Task has been canceled or completed.");
        }

        // Loop until we find the next csv call
        private static (int, int, int) getNextVals(int calls)
        {
            int hr = 0;
            int spo2 = 0;
            int time_stamp = 0;
            // Change this path for your personal computer
            //string filePath = ($"C:\\Users\\TJoe\\Documents\\test_vals_DEMO_250218\\vals_{calls}.csv");  // modified this path to be where new files get creates
            string filePath = ($"U:\\Users Common\\Ashwin\\hydros csv dump\\vals_{calls}.csv");  // modified this path to be where new files get creates

            while (true)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        // init var for lines read from csv file
                        string[] lines;

                        // build in loop to handle if file is open in another process (been created but yet not closed)
                        while (true) 
                        {
                            try
                            {
                                lines = File.ReadAllLines(filePath);
                                break;
                            }
                            catch (Exception ex) 
                            {
                                Console.WriteLine("Reached the file open but used by another process exception..."); 
                            };
                        };
                       
                        if (lines.Length > 0)
                        {
                            string[] values = lines[0].Split(',');

                            if (values.Length == 3 && int.TryParse(values[0], out hr) && int.TryParse(values[1], out spo2) && int.TryParse(values[2], out time_stamp))
                            {
                                Trace.WriteLine($"Algorithm Vals: {hr}, {spo2} at timestamp {time_stamp}");
                                return (hr, spo2, time_stamp);
                            }
                            else
                            {
                                Console.WriteLine("File format is incorrect.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("File is empty.");
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

        // Send the data and get the encoded waveform
        public static void RunModemProgram(int heart_rate, int spo2, int time_stamp, NetworkStream stream, NetworkStream pcmStream, int calls, BlockingCollection<float[]> waveBuff, int scf, float voltAmp)
        {
            // Send TransmitJSON message and pump 5s zero waveform
            sendData(heart_rate, spo2, time_stamp, 0, stream);
            sendBytes(pcmStream);

            // Control messages
            GetRecMessages(stream);
            GetRecMessages(stream);
            GetRecMessages(stream);
            GetRecMessages(stream);

            byte[] responseData = new byte[5120];
            float[] resArray = new float[102400 * 5]; // will get exactly 5s in return

            int nbytes;
            int resI = 0;
            int nbytesTot = 0;

            // Store encoded waveform (in float type) in resArray
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

            // Only keep the packet, not the extra padding
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

            // Save cutArray to CSV
            //SaveArrayToCsv("C:/Users/TJoe/Documents/Comms Intermmediate Outputs/txencoded.csv", cutArray);

            UpsampleUpshift(cutArray, waveBuff, calls, scf, voltAmp);
        }


        // Identify the start and end of the packets
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
            //Trace.WriteLine($"FIRST & LAST: {first} & {last}");
            return (first, last);
        }

        // Design the FIR filter coefficients -- similar to scipy
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

        // For most efficient fft
        static int NextPowerOfTwo(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        // Replacing the scipy implementation
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

        // Filter, fft convolution
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

        public static void UpsampleUpshift(float[] sigin, BlockingCollection<float[]> waveBuff, int calls, int scf, float voltAmp, bool down_next = false)
        {
            float fs_og = 102_400;
            float fc_og = 35_000;
            float fs_up = 1_000_000;
            float fc_up = scf;

            // Modulate to baseband
            int sigLen = sigin.Length;
            Complex[] sigComplex = new Complex[sigLen];
            for (int i = 0; i < sigLen; i++)
            {
                double phase = -2 * Math.PI * fc_og * (i + 1) / fs_og; // +1 to match Python's 1-based indexing in arange  // equiv to -iwt
                sigComplex[i] = new Complex(sigin[i], 0) * Complex.Exp(new Complex(0, phase));  // sigComplex = s(t) * e^{-iwt}
            }

            // FIR filter with Hamming window  // lowpass filter to remove high-freq. terms
            int filterOrder = 1024;
            double cutoffFreq = 2 * 5120 / fs_og;

            double[] hammingWindow = Window.Hamming(filterOrder);
            // Design the FIR filter coefficients
            double[] firCoefficients = DesignLowPassFIRFilter(cutoffFreq, filterOrder, hammingWindow);
            // Apply filtfilt
            Complex[] bb_filtered = Filfilt(firCoefficients, sigComplex); // bb_filtered = m(t)

            // Resample
            int up = (int)fs_up;
            int down = (int)fs_og;
            var resampled = Resample(bb_filtered, up, down);

            double[] realComponents = new double[resampled.Length];
            for (int i = 0; i < resampled.Length; i++)
            {
                realComponents[i] = resampled[i].Real;  // m(t) at 1 MHz sample rate
            }

            //SaveArrayToCsv("C:/Users/TJoe/Documents/Comms Intermmediate Outputs/txus.csv", realComponents);

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
                // We never reach this case, but the python version has it. Uncomment below to implement
                //return pb.Select(c => (float)c.Magnitude).ToArray();
            }
            else
            {
                float[] sigout = new float[pb.Length];
                for (int i = 0; i < pb.Length; i++)
                {
                    sigout[i] = (float)(pb[i].Real - pb[i].Imaginary);  // new s(t) at the HYDROS pass band
                }
                // Define input range
                float inputMin = sigout.Min();
                float inputMax = sigout.Max();

                // Define output range (+5V to -5V)
                float outputMax = voltAmp; // 5f
                float outputMin = -outputMax; // -5f
                

                // Linearly scale the values
                float[] scaledValues = new float[sigout.Length];
                for (int i = 0; i < sigout.Length; i++)
                {
                    scaledValues[i] = Map(sigout[i], inputMin, inputMax, outputMin, outputMax);  // s(t) rescaled to DAC voltages
                }
                // Save scaledValues as a binary file
                //string binaryPath = "C:\\Users\\TJoe\\OneDrive - Delsys Inc\\TJ HYDROS\\HYDROS comms\\laptop demod\\scaledValues300.bin";
                //SaveArrayToBinary(binaryPath, scaledValues);


                waveBuff.Add(scaledValues);
            }

        }

        private static void SaveArrayToBinary(string binaryPath, float[] array)
        {
            // Open the file in append mode
            using (var writer = new BinaryWriter(File.Open(binaryPath, FileMode.Append)))
            {
                foreach (var value in array)
                {
                    writer.Write(value); // Append each 32-bit float value to the file
                }
            }
        }


        private static void SaveArrayToCsv(string csvPath, double[] cutArray)
        {
            // Check if the file exists
            if (!File.Exists(csvPath))
            {
                // Create CSV and add the first column
                using (var writer = new StreamWriter(csvPath))
                {
                    // Write header for the first column
                    writer.WriteLine("Column_1");

                    // Write data for the first column
                    foreach (var value in cutArray)
                    {
                        writer.WriteLine(value);
                    }
                }
            }
            else
            {
                // Append new column to the existing CSV
                var allLines = File.ReadAllLines(csvPath).ToList();

                // Split headers and data
                var headers = allLines[0].Split(',');
                var dataRows = allLines.Skip(1).ToList();

                // Add new column header
                string newHeader = $"Column_{headers.Length + 1}";
                headers = headers.Append(newHeader).ToArray();

                // Ensure enough rows to accommodate the new column
                int maxRows = Math.Max(dataRows.Count, cutArray.Length);
                while (dataRows.Count < maxRows)
                {
                    dataRows.Add(string.Empty);
                }

                // Append the new column's data
                for (int i = 0; i < maxRows; i++)
                {
                    string newValue = i < cutArray.Length ? cutArray[i].ToString() : string.Empty;
                    if (i < dataRows.Count && !string.IsNullOrEmpty(dataRows[i]))
                    {
                        dataRows[i] += $",{newValue}";
                    }
                    else
                    {
                        dataRows[i] = newValue;
                    }
                }

                // Write updated CSV
                using (var writer = new StreamWriter(csvPath))
                {
                    // Write headers
                    writer.WriteLine(string.Join(",", headers));

                    // Write rows
                    foreach (var row in dataRows)
                    {
                        writer.WriteLine(row);
                    }
                }
            }
        }
        private static void SaveArrayToCsv(string csvPath, float[] cutArray)
        {
            // Check if the file existssecret
            if (!File.Exists(csvPath))
            {
                // Create CSV and add the first column
                using (var writer = new StreamWriter(csvPath))
                {
                    // Write header for the first column
                    writer.WriteLine("Column_1");

                    // Write data for the first column
                    foreach (var value in cutArray)
                    {
                        writer.WriteLine(value);
                    }
                }
            }
            else
            {
                // Append new column to the existing CSV
                var allLines = File.ReadAllLines(csvPath).ToList();

                // Split headers and data
                var headers = allLines[0].Split(',');
                var dataRows = allLines.Skip(1).ToList();

                // Add new column header
                string newHeader = $"Column_{headers.Length + 1}";
                headers = headers.Append(newHeader).ToArray();

                // Ensure enough rows to accommodate the new column
                int maxRows = Math.Max(dataRows.Count, cutArray.Length);
                while (dataRows.Count < maxRows)
                {
                    dataRows.Add(string.Empty);
                }

                // Append the new column's data
                for (int i = 0; i < maxRows; i++)
                {
                    string newValue = i < cutArray.Length ? cutArray[i].ToString() : string.Empty;
                    if (i < dataRows.Count && !string.IsNullOrEmpty(dataRows[i]))
                    {
                        dataRows[i] += $",{newValue}";
                    }
                    else
                    {
                        dataRows[i] = newValue;
                    }
                }

                // Write updated CSV
                using (var writer = new StreamWriter(csvPath))
                {
                    // Write headers
                    writer.WriteLine(string.Join(",", headers));

                    // Write rows
                    foreach (var row in dataRows)
                    {
                        writer.WriteLine(row);
                    }
                }
            }
        }

        // Cubic spline interpolation to change sampling frequency from fs_og to fs_next. Frequencies in Hz
        private static Complex[] Resample(Complex[] input, double fs_next, double fs_og)
        {
            int inputLen = input.Length;
            int outputLen = (int)Math.Ceiling(inputLen * fs_next / fs_og);
            double[] xOrig = Enumerable.Range(0, inputLen).Select(i => (double)i / fs_og).ToArray();
            double[] xResampled = Enumerable.Range(0, outputLen).Select(i => (double)i / fs_next).ToArray();

            // Interpolate real and imaginary parts separately
            double[] realOrig = input.Select(c => c.Real).ToArray();
            double[] imagOrig = input.Select(c => c.Imaginary).ToArray();

            // Methods from MathNet.Numerics.Interpolation
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

        // Close Modem stream & client connections
        // If you want to explicitly close modem, call this in the StopNICard method
        // Note that this will require the entire modem to be restarted before encoding again
        public static void CloseModem(TcpClient client, TcpClient pcmClient)
        {
            client.GetStream().Close();
            pcmClient.GetStream().Close();
            client.Close();
            pcmClient.Close();
            initialized = false;
            Debug.WriteLine("Modem Closed");
        }

        // Scale the signal based on the desired mins/maxs. Note this can slightly shift the zero point, if the signal was not symmetrical about the x-axis
        static float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax)
        {
            return outputMin + (outputMax - outputMin) * ((value - inputMin) / (inputMax - inputMin));

            //return outputMax * (2 * ((value - inputMin) / (inputMax - inputMin)) - 1);


        }
    }
}