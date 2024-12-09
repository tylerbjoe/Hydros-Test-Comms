#define FFTW_DLL
#define FIND_DLL __declspec(dllexport) // import or export?
#include <fftw3.h>
#include <complex>

using namespace std;
extern "C" FIND_DLL int fftwFunc();
extern "C" FIND_DLL void fftwTransform(std::complex<double>* input, std::complex<double>* output, int N);