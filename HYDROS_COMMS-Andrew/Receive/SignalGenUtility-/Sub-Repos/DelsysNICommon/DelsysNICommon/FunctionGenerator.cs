using System;
using System.Collections.Generic;

namespace DelsysNICommon
{
    public enum WaveformType
    {
        SineWave = 0,
        SAWTOOTH = 1,
        SQUARE = 3,
        TRIANGLE = 4,
    }

    public static class FunctionGenerator
    {

        public static double[] GenerateSineWave(
            double frequency,
            double amplitude,
            double clockRate,
            double dc_offset)
        {
            int samplesPerPeriod = (int)(clockRate / frequency);
            int numberOfPeriods = (samplesPerPeriod > 5000) ?  4 : (5000 / samplesPerPeriod);

            double[] sineWave = new double[samplesPerPeriod * numberOfPeriods];

            for (int j = 0; j < numberOfPeriods; j++)
            {
                for (int i = 0; i < samplesPerPeriod; i++)
                {
                    double time = i / clockRate;
                    sineWave[i + (samplesPerPeriod * j)] = (amplitude * Math.Sin(2 * Math.PI * frequency * time)) + dc_offset;
                }
            }

            return sineWave;
        }
        public static double[] GenertateSinglePeriodSineWave(double frequency, double amplitude, double sampleClockRate, double samplesPerBuffer, double dc_offset)
        {
            int samplesPerPeriod = (int)(sampleClockRate / frequency);
            double[] sineWave = new double[samplesPerPeriod];

            for (int i = 0; i < samplesPerPeriod; i++)
            {
                double time = i / sampleClockRate;
                sineWave[i] = (amplitude * Math.Sin(2 * Math.PI * frequency * time)) + dc_offset;
            }

            return sineWave;
        }

        public static double[] GenerateSawWave(double frequency, double amplitude, double sampleClockRate, double samplesPerBuffer, double dc_offset)
        {
            int bufferSize = (int)samplesPerBuffer;
            double[] buffer = new double[bufferSize];

            double period = sampleClockRate / frequency;
            double increment = 2.0 * amplitude / period;

            for (int i = 0; i < bufferSize; i++)
            {
                buffer[i] = (-amplitude + (i % period) * increment) + dc_offset;
            }

            return buffer;
        }
        public static double[] GenerateSquareWave(double freq, double amp, double clkRate, int samplesPerBuffer, double dc_offset)
        {
            // Calculate the time period of one cycle
            double T = 1 / freq;

            // Calculate the number of samples per cycle
            int samplesPerCycle = (int)(clkRate / freq);

            // Generate the time array
            double[] t = new double[samplesPerCycle];
            for (int i = 0; i < samplesPerCycle; i++)
            {
                t[i] = (i * T / samplesPerCycle) + dc_offset;
            }

            // Generate the square wave signal
            double[] squareWave = new double[samplesPerCycle];
            for (int i = 0; i < samplesPerCycle; i++)
            {
                squareWave[i] = Math.Sign(Math.Sin(2 * Math.PI * freq * t[i])) * amp;
            }

            return squareWave;
        }

        public static double[] GenerateTriangleWaveform(double frequency, double amplitude, double sampleClockRate, double samplesPerBuffer, double dc_offset)
        {
            int bufferSize = (int)samplesPerBuffer;
            double[] buffer = new double[bufferSize];

            double period = sampleClockRate / frequency;
            double increment = 2.0 * amplitude / period;

            for (int i = 0; i < bufferSize; i++)
            {
                buffer[i] = (-amplitude + Math.Abs((i % period) * increment - amplitude)) + dc_offset;
            }

            return buffer;
        }

        internal static double[] GenerateSweep(double amp, double startFreq, double stopFreq, double step)
        {
            if (startFreq <= 0 || stopFreq <= 0 || step <= 0)
            {
                throw new ArgumentException("Frequency and rate must be positive numbers.");
            }

            if (startFreq >= stopFreq)
            {
                throw new ArgumentException("Start frequency must be less than end frequency.");
            }

            List<double> buffer = new List<double>();

            for (int i = (int)startFreq; i < stopFreq; i++)
            {
                var wave = GenertateSinglePeriodSineWave(i, amp, 2_000_000, 2_000_000, 0);
                foreach (double point in wave)
                {
                    buffer.Add(point);
                }
                i += (int)step;
            }
            buffer.Reverse();
            return buffer.ToArray();
        }
    }
}