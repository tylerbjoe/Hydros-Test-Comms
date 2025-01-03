using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Numerics; // Registers Complex type

namespace DelsysSigNIalGen.Utilities
{
    public class fftDLLLayer
    {
        [DllImport("FFTProj.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int fftwFunc();

        [DllImport("FFTProj.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void fftwTransform([In, Out] Complex[] input, [In, Out] Complex[] output, int N);
    }
}