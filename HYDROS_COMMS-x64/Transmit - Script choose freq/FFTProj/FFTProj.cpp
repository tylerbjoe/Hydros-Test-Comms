#define FFTW_DLL
#include <fftw3.h>
#include "FFTProj.h"
#include <iostream>
#include <cmath>
#include <complex>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

using namespace std;
int fftwFunc()
{
    return 2;
}

extern "C" FIND_DLL void fftwTransform(std::complex<double>* input, std::complex<double>* output, int N)
{
    // Allocate memory for FFTW input and output arrays
    fftw_complex* in = (fftw_complex*)fftw_malloc(sizeof(fftw_complex) * N);
    fftw_complex* out = (fftw_complex*)fftw_malloc(sizeof(fftw_complex) * N);

    // Copy input data to FFTW input array
    for (int i = 0; i < N; ++i) {
        in[i][0] = input[i].real(); // Real part
        in[i][1] = input[i].imag(); // Imaginary part
    }

    // Create a plan for the forward FFT
    fftw_plan plan = fftw_plan_dft_1d(N, in, out, FFTW_FORWARD, FFTW_ESTIMATE);

    // Execute the FFT
    fftw_execute(plan);

    // Copy FFTW output data to output array
    for (int i = 0; i < N; ++i) {
        output[i] = std::complex<double>(out[i][0], out[i][1]);
    }

    // Clean up
    fftw_destroy_plan(plan);
    fftw_free(in);
    fftw_free(out);
}
