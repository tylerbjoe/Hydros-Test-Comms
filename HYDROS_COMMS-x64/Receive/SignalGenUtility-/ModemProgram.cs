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
using System.Windows.Markup;

namespace DelsysSigNIalGen
{
    class ModemProgram
    {
        private static readonly object consoleLock = new object();
        private static bool initialized = false;

        // These get accessed from MainViewModel.cs, so they are public
        public static bool globalStopped = false;
        public static bool messagesStopped = false;
        public static bool decodeThread = false;
        public static int trialNum = 0; // Changes names of files
        public static int secretCarrierFrequency = -1; // Carrier frequency in Hz, for across the water // Will be set by on-screen input
        public static string logPath = string.Empty;

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

            int bytes = stream.Read(responseData, 0, responseData.Length); // .Result

            string response = Encoding.UTF8.GetString(responseData, 0, bytes);

            //Debug.WriteLine("Response:", response);

            // Add to log
            using (StreamWriter sw = new StreamWriter($"C:\\Users\\TJoe\\Documents\\1_8_pooltest\\outputLog_{trialNum}.txt", true))
            //using (StreamWriter sw = new StreamWriter(logPath, true)) // appends = true here UNCOMMENT FOR LINE BEFORE PREVIOUSLY
            {
                sw.WriteLine(response);
            }

            if (response.Contains("Data"))
            {

                //Debug.WriteLine("Line with data:", response);

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

                        //Debug.WriteLine("substring", dataSubstring);


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
            var setVal = new
            {
                Command = "SetValue",
                Arguments = $"{element} {value} 0"
            };
            string jsonString = JsonSerializer.Serialize(setVal);
            SendJsonCommand(jsonString, numRec, stream);
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

        // Initialize modem connection! Return control and pcm port connections, so MainViewModel.cs can use them
        public static (NetworkStream, NetworkStream, TcpClient, TcpClient) ModemInit(BlockingCollection<float[]> waveBuff)
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

            int portDec = 17100;
            int pcmPortDec = 17103;

            TcpClient clientDec = new TcpClient(ipAddress, portDec);
            TcpClient pcmClientDec = new TcpClient(ipAddress, pcmPortDec);

            NetworkStream streamDec = clientDec.GetStream();
            NetworkStream pcmStreamDec = pcmClientDec.GetStream();

            GetRecMessages(streamDec); // Just connecting to the modem, gives us a response (It tells us # of connections)

            // Send all initializing commands:
            startrx(streamDec);
            double frequencyDec = 35000; // var
            SetValue("Carrier", frequencyDec, 1, streamDec);
            SetValue("PayloadMode", 0, 1, streamDec);

            // For debugging, send a csv to make sure modem connection works
            //sendCSV(streamDec, pcmStreamDec, 0);

            return (streamDec, pcmStreamDec, clientDec, pcmClientDec);
        }

        // Continuously reads messages from control port, looking for Data messages
        public static async Task decodeControls(NetworkStream streamDec)
        {
            decodeThread = true;
            int calls = 0;
            while (!globalStopped)
            {
                List<int> values = null;
                values = GetRecMessagesDataDecode(streamDec);
                // Handle the stopping and starting of the program (where another thread could be introduced)
                if (messagesStopped)
                {
                    if (globalStopped)
                    {
                        break;
                    }
                    else
                    {
                        messagesStopped = false;
                        calls = 0;
                        continue;
                    }
                }
                if (values != null)
                {
                    Trace.WriteLine($"hr: {values[0]}, spo2: {values[1]}");
                    // Adds to Plot
                    ValuePlot.AppendData((calls, values[0]), (calls, values[1]));
                    calls++;
                }
            }
            decodeThread = false;
        }

        // Send received waveform to the modem and read the same number of bytes back from pcm port
        public static void decodeMyWav(NetworkStream pcmStreamDec, float[] givenData)
        {
            Trace.WriteLine($"fed: {givenData.Length}");
            byte[] byteArray = new byte[givenData.Length * sizeof(float)];
            Buffer.BlockCopy(givenData, 0, byteArray, 0, byteArray.Length);

            pcmStreamDec.Write(byteArray);
            readMyWavSize(pcmStreamDec, givenData.Length * 4);
        }

        // For debugging and making sure the modem connection works, can send a csv to the modem
        // Should decode the values [60, 95]
        public static void sendCSV(NetworkStream stream, NetworkStream pcmStream, int calls)
        {
            string filePath = $"U:\\Users Common\\AnnaE\\csvsUW\\DownOut_RT_0.csv";
            List<float> floatList = new List<float>();

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
            Trace.WriteLine($"CSV len: {floatList.Count}");

            byte[] byteArray = new byte[floatList.Count * sizeof(float)];
            Buffer.BlockCopy(floatList.ToArray(), 0, byteArray, 0, byteArray.Length);

            // Have essentially translated the popoto pumpAudio python code below:
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
                        values = GetRecMessagesDataDecode(stream);
                    }
                    else
                    {
                        GetRecMessages(stream);
                    }
                }
            }
            if (values != null)
            {
                // If we are able to decode data
                Trace.WriteLine($"hr: {values[0]}, spo2: {values[1]}");
            }

            // Read the zeros that are returned by the pcm socket
            readMyWavSize(pcmStream, byteArray.Length);
            Trace.WriteLine("Done getting csv messages");
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

            // call to parsing method
        }

        // Resample with ffts -- Note the complex signal
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

        // Downsample sigin from fs_bef to fs_down. And downshift from fc_bef to fc_down
        public static float[] DownsampleDownshift(double[] sigin, int calls)
        {
            //SAVE INPUT
            //SaveArrayToCsv("C:/Users/TJoe/Documents/1_8_pooltest/rx.csv", sigin);
            float fs_down = 102_400; // changed 102_400
            float fc_down = 35_000;
            float fs_bef = 1_000_000;
            float fc_bef = (float)secretCarrierFrequency;

            // Undo modulate to passband  sigin = s(t) at HYDROS passband
            int sigLen = sigin.Length;
            Complex[] sigComplex = new Complex[sigLen];
            for (int i = 0; i < sigLen; i++)
            {
                double pb_factor = -2 * Math.PI * fc_bef * (i + 1) / fs_bef; // +1 to match Python's 1-based indexing in arange
                sigComplex[i] = new Complex(sigin[i], 0) * Complex.Exp(new Complex(0, pb_factor));
            }

            int resLen = (int)Math.Round(sigin.Length * 102_400.0 / 1_000_000.0);
            var resampled = ResComp(sigComplex, resLen); // 512000
            // SAVE DOWNSHIFT REAL COMPONENTS
            //double[] realComponents = new double[resampled.Length];
            //for (int i = 0; i < resampled.Length; i++)
            //{
            //    realComponents[i] = resampled[i].Real;
            //}
            //SaveArrayToCsv("C:/Users/TJoe/Documents/1_8_pooltest/rxds.csv", realComponents);

            // Undo modulate to baseband (go back to Popoto passband)
            int resampledLen = resampled.Length;
            Complex[] no_bb = new Complex[resampledLen];
            for (int i = 0; i < resampledLen; i++)
            {
                double bb_factor = 2 * Math.PI * fc_down * (i + 1) / fs_down; // +1 to match np.arange start @ 1
                no_bb[i] = resampled[i] * Complex.Exp(new Complex(0, bb_factor));
            }
            // SAVE downsampled REAL COMPONENTS
            //double[] realComponentss = new double[no_bb.Length];
            //for (int i = 0; i < no_bb.Length; i++)
            //{
            //    realComponentss[i] = no_bb[i].Real;
            //}
            //SaveArrayToCsv("C:/Users/TJoe/Documents/1_8_pooltest/rxdsdsimag.csv", realComponentss);

            // Subtract imag component from real component
            float[] sigout = new float[no_bb.Length];
            for (int i = 0; i < no_bb.Length; i++)
            {
                sigout[i] = (float)(no_bb[i].Real - no_bb[i].Imaginary);
            }
            // SAVE DOWNSHIFT
            SaveArrayToCsv("C:/Users/TJoe/Documents/1_8_pooltest/rxdsds.csv", sigout);

            return sigout; // s(t) in popoto passband
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

        // Close Modem stream & client connections
        // If you want to explicitly close modem, call this in the StopNICard method
        // Note that this will require the entire modem to be restarted before encoding again
        public static void CloseModem(TcpClient client, TcpClient pcmClient)
        {
            client.GetStream().Close();
            pcmClient.GetStream().Close();
            client.Close();
            pcmClient.Close();
            //initialized = false; -- WHILE DEBUGGING COMMENT OUT
            Debug.WriteLine("Modem Closed");
        }

        // Scale the signal based on the desired mins/maxs. Note this can slightly shift the zero point, if the signal was not symmetrical about the x-axis
        static float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax)
        {
            return outputMin + (outputMax - outputMin) * ((value - inputMin) / (inputMax - inputMin));
        }
    }
}