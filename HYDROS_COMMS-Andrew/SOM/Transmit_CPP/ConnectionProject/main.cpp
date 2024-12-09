#include <iostream>
#include <string>
#include <sstream>
#include <boost/asio.hpp>
#include <omp.h>
#include <thread>
#include <mutex>
#include <fstream>
#include <tuple>
#include <vector>
#include <atomic>
#include <chrono>
#include <filesystem>
#include <cstring>
#include <algorithm>
#include <iterator>
#include <numeric>
#include <iomanip>
#include <stdexcept>
#include <complex>
#include <cmath>
#include <functional>
#include <Eigen/Dense>
#include <boost/math/interpolators/cardinal_cubic_b_spline.hpp>
#include <cstdlib>
#include <sys/types.h>
#include <fftw3.h>
#include <nlohmann/json.hpp>
#include <unistd.h>
#include <sys/wait.h>

using boost::asio::ip::tcp;
using json = nlohmann::json;

// std::mutex consoleLock;
std::atomic<bool> globalStopped(false);

void startrx(tcp::socket& socket);
void SendJsonCommand(const std::string& jsonCommand, int numRec, tcp::socket& socket);
void GetRecMessages(tcp::socket& socket);
void SetValue(const std::string& element, double value, int numRec, tcp::socket& socket);
void GetVals(tcp::socket& socket, tcp::socket& pcmSocket);
std::tuple<int, int> getNextVals(int calls);
void RunModemProgram(int hr, int spo2, tcp::socket& socket, tcp::socket& pcmSocket, int calls);
std::tuple<int, int> GetIdxs(const std::vector<float>& data);
void sendData(int hr, int spo2, int numRec, tcp::socket& socket);
void sendBytes(tcp::socket& pcmSocket);
void UpsampleUpshift(std::vector<float> cutArray, int calls);
std::vector<double> DesignLowPassFIRFilter(double normalizedCutoff, int filterOrder, const std::vector<double>& window);
std::vector<std::complex<double>> Filfilt(const std::vector<double>& firCoefficients, const std::vector<std::complex<double>>& signal);
std::vector<std::complex<double>> Resample(const std::vector<std::complex<double>>& input, double fs_next, double fs_og);
std::vector<double> WindowHamming(int size);
float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax);
int NextPowerOfTwo(int n);
std::vector<std::complex<double>> Filfilt(const std::vector<double>& b, const std::vector<std::complex<double>>& x);
std::vector<std::complex<double>> FftConvolve(const std::vector<std::complex<double>>& x, const std::vector<double>& b, int fftSize);
void SaveToCSV(const std::vector<float>& scaledValues, const std::string& filename);
void terminateExistingPopotoApp();

int main()
{
    #pragma omp parallel
    {
        // int threadID = omp_get_thread_num();
        // std::cout << "Hello from thread " << threadID << std::endl;

        #pragma omp single
        {
            int numThreads = omp_get_num_threads();
            std::cout << "Total number of OpenMP threads: " << numThreads << std::endl;
        }
    }

    // Make sure no other instances are running -- otherwise will run into "Binding Error"
    terminateExistingPopotoApp();
    terminateExistingPopotoApp();

    pid_t pid = fork(); // Two different processes
    
    if (pid == -1) {
        std::cerr << "Failed to fork" << std::endl;
        return 1;
    } else if (pid == 0) {
        // Child process
        std::string popotoAppDir = "/mnt/c/Users/AEngel/Desktop/git_Modem/HYDROS";
        std::string popotoAppPath = popotoAppDir + "/popoto_app";

        // Change to the directory and execute popoto_app
        if (chdir(popotoAppDir.c_str()) != 0) {
            std::cerr << "Failed to change directory to " << popotoAppDir << std::endl;
            exit(1);
        }

        execl(popotoAppPath.c_str(), popotoAppPath.c_str(), (char *)NULL);

        // If execl returns, there was an error
        std::cerr << "Failed to start popoto_app" << std::endl;
        exit(1);
    } else {
        // Parent process
        // Allow some time for popoto_app to start up
        std::this_thread::sleep_for(std::chrono::seconds(2));

        std::string ipAddress = "localhost";
        int port = 17000;
        int pcmPort = 17003;

        try
        {
            boost::asio::io_context ioContext;

            tcp::socket client(ioContext);
            tcp::socket pcmClient(ioContext);

            tcp::resolver resolver(ioContext);
            auto endpoints = resolver.resolve(ipAddress, std::to_string(port));
            auto pcmEndpoints = resolver.resolve(ipAddress, std::to_string(pcmPort));

            boost::asio::connect(client, endpoints);
            boost::asio::connect(pcmClient, pcmEndpoints);

            GetRecMessages(client);
            startrx(client);
            double frequency = 35000;
            SetValue("Carrier", frequency, 1, client);
            SetValue("PayloadMode", 0, 1, client);
            SetValue("UPCONVERT_OutputScale", 10, 1, client);

            // Start GetVals function in a separate thread if needed
            GetVals(client, pcmClient);
        }
        catch (std::exception& e)
        {
            std::cerr << "Exception: " << e.what() << "\n";
        }

    }

    // Make sure no other instances are running -- otherwise will run into "Binding Error"
    terminateExistingPopotoApp();
    terminateExistingPopotoApp();
    return 0;
}

void startrx(tcp::socket& socket) {
    json rxStart = {
        {"Command", "Event_StartRx"},
        {"Arguments", "Unused Arguments"}
    };
    std::string jsonString = rxStart.dump();
    SendJsonCommand(jsonString, 2, socket);
}

void SendJsonCommand(const std::string& jsonCommand, int numRec, tcp::socket& socket) {
    // std::lock_guard<std::mutex> lock(consoleLock);
    std::string data = jsonCommand + "\n";
    boost::asio::write(socket, boost::asio::buffer(data));

    std::cout << "Sent: " << jsonCommand << std::endl;

    for (int i = 0; i < numRec; ++i) {
        GetRecMessages(socket);
    }
}

void GetRecMessages(tcp::socket& socket) {
    // std::cout << "Waiting... " << std::endl;
    char responseData[256];
    boost::system::error_code error;

    size_t len = socket.read_some(boost::asio::buffer(responseData), error);
    if (error == boost::asio::error::eof) {
        return; // Connection closed cleanly by someone else
    }
    else if (error) {
        throw boost::system::system_error(error); // Some other error
    }

    std::string response(responseData, len);
    // std::lock_guard<std::mutex> lock(consoleLock);
    std::cout << "Received: " << response << std::endl;
}

void SetValue(const std::string& element, double value, int numRec, tcp::socket& socket) 
{
    std::ostringstream oss;
    oss << element << ' ' << value << " 0";
    std::string input = oss.str();
    // std::string input = std::format("{0} {1} 0", element, value);
    json setValCmd = {
        {"Command", "SetValue"},
        {"Arguments", input}
    };
    std::string jsonString = setValCmd.dump();
    SendJsonCommand(jsonString, numRec, socket);
}

void sendData(int hr, int spo2, int numRec, tcp::socket& socket) {
    std::string jsonString = "{"
        "\"Command\":\"TransmitJSON\","
        "\"Arguments\":{"
            "\"Payload\":{"
                "\"Data\":[" + std::to_string(hr) + "," + std::to_string(spo2) + "]"
            "}"
        "}"
    "}";
    SendJsonCommand(jsonString, numRec, socket);
}

void sendBytes(tcp::socket& pcmSocket) {
    std::vector<char> emptyWav(2048000); // 2,048,000 bytes
    boost::system::error_code error;
    std::size_t bytesSent = boost::asio::write(pcmSocket, boost::asio::buffer(emptyWav), error);
}


void GetVals(tcp::socket& socket, tcp::socket& pcmSocket)
{
    int calls = 0;
    while (!globalStopped.load())
    {
        // std::this_thread::sleep_for(std::chrono::milliseconds(1500)); // for debugging, should pause a bit
        auto [hr, spo2] = getNextVals(calls);
        std::cout << "Hr: " << hr << ", Spo2: " << spo2 << std::endl;
        RunModemProgram(hr, spo2, socket, pcmSocket, calls);
        calls++;

        if (calls > 4) {
            globalStopped.store(true);
        }
    }
}

std::tuple<int, int> getNextVals(int calls)
{
    int hr = 0;
    int spo2 = 0;
    std::string filePath = "/mnt/c/Users/AEngel/Desktop/test_vals/vals_" + std::to_string(calls) + ".csv";

    while (true)
    {
        if (std::filesystem::exists(filePath))
        {
            std::ifstream file(filePath);
            std::string line;
            if (std::getline(file, line))
            {
                std::istringstream lineStream(line);
                std::string hrStr, spo2Str;

                if (std::getline(lineStream, hrStr, ',') && std::getline(lineStream, spo2Str, ','))
                {
                    hr = std::stoi(hrStr);
                    spo2 = std::stoi(spo2Str);
                    return std::make_tuple(hr, spo2);
                }
            }
        }
    }
}

void RunModemProgram(int hr, int spo2, tcp::socket& socket, tcp::socket& pcmSocket, int calls)
{
    // Send data and bytes
    sendData(hr, spo2, 0, socket);
    sendBytes(pcmSocket);

    // Get messages -- put this on a separate thread
    GetRecMessages(socket);
    GetRecMessages(socket);
    GetRecMessages(socket);
    GetRecMessages(socket);

    // Prepare buffers
    std::vector<char> responseData(5120); // 5120 bytes, 2560 floats (4 bytes each)
    std::vector<float> resArray(102400 * 5);

    int nbytes = 0;
    int resI = 0;
    int nbytesTot = 0;
    
    // Read data from the socket
    while (nbytesTot < 2048000) // can we do this in a single read?? might be faster
    {
        std::size_t bytesRead = boost::asio::read(pcmSocket, boost::asio::buffer(responseData), boost::asio::transfer_at_least(1));
        nbytes = static_cast<int>(bytesRead);
        nbytesTot += nbytes;
        int floatCount = nbytes / 4;

        // Convert bytes to floats
        for (int i = 0; i < floatCount; ++i)
        {
            float value;
            std::memcpy(&value, &responseData[i * 4], sizeof(float));
            if (resI < resArray.size())
            {
                resArray[resI] = value;
            }
            resI++;
        }
    }

    // Process and cut array
    std::vector<float> cutArray;
    if (calls == 0)
    { // keep the first zeros
        auto [_, lastIdx] = GetIdxs(resArray);
        cutArray.assign(resArray.begin(), resArray.begin() + std::min(lastIdx + 10000, static_cast<int>(resArray.size())));
    }
    else
    { // cut on both sides
        auto [firstIdx, lastIdx] = GetIdxs(resArray);
        cutArray.assign(resArray.begin() + std::max(firstIdx - 10000, 0), resArray.begin() + std::min(lastIdx + 10000, static_cast<int>(resArray.size())));
    }

    // Upsample and upshift
    UpsampleUpshift(cutArray, calls);
}

void UpsampleUpshift(std::vector<float> sigin, int calls)
{
    const float fs_og = 102400.0f;
    const float fc_og = 35000.0f;
    const float fs_up = 1000000.0f;
    const float fc_up = 100000.0f; // Define or calculate this value

    // Modulate to baseband
    int sigLen = sigin.size();
    std::vector<std::complex<double>> sigComplex(sigLen);
    for (int i = 0; i < sigLen; ++i) {
        double phase = -2 * M_PI * fc_og * (i + 1) / fs_og; // +1 to match Python's 1-based indexing in arange
        sigComplex[i] = std::complex<double>(sigin[i], 0) * std::exp(std::complex<double>(0, phase));
    }

    // FIR filter with Hamming window
    int filterOrder = 1024;
    double cutoffFreq = 2 * 5120 / fs_og;
    std::vector<double> hammingWindow = WindowHamming(filterOrder);
    std::vector<double> firCoefficients = DesignLowPassFIRFilter(cutoffFreq, filterOrder, hammingWindow);
    std::vector<std::complex<double>> bb_filtered = Filfilt(firCoefficients, sigComplex);

    // Resample
    int up = static_cast<int>(fs_up);
    int down = static_cast<int>(fs_og);
    std::vector<std::complex<double>> resampled = Resample(bb_filtered, up, down);

    // Modulate to passband
    int resampledLen = resampled.size();
    std::vector<std::complex<double>> pb(resampledLen);
    for (int i = 0; i < resampledLen; ++i) {
        double phase = 2 * M_PI * fc_up * (i + 1) / fs_up; // +1 to match np.arange start @ 1
        pb[i] = resampled[i] * std::exp(std::complex<double>(0, phase));
    }

    std::vector<float> sigout(pb.size());
    for (int i = 0; i < pb.size(); ++i) {
        sigout[i] = static_cast<float>(pb[i].real() - pb[i].imag());
    }

    // Define input range
    float inputMin = *std::min_element(sigout.begin(), sigout.end());
    float inputMax = *std::max_element(sigout.begin(), sigout.end());

    // Define output range (+5V to -5V)
    float outputMin = -5.0f;
    float outputMax = 5.0f;

    // Linearly scale the values
    std::vector<float> scaledValues(sigout.size());
    std::transform(sigout.begin(), sigout.end(), scaledValues.begin(),
        [inputMin, inputMax, outputMin, outputMax](float value) {
            return Map(value, inputMin, inputMax, outputMin, outputMax);
        });

    // if (calls == 0)
    // {
    //     SaveToCSV(scaledValues, "/mnt/c/Users/AEngel/Desktop/testOut0.csv");
    // }
    // if (calls == 1)
    // {
    //     SaveToCSV(scaledValues, "/mnt/c/Users/AEngel/Desktop/testOut1.csv");
    // }

    // SEND TO TRANSDUCER / PORT HERE
}

void SaveToCSV(const std::vector<float>& scaledValues, const std::string& filename) {
    // Create an output file stream
    std::ofstream outFile(filename);

    // Check if the file stream was created successfully
    if (!outFile.is_open()) {
        std::cerr << "Error: Could not open file " << filename << " for writing." << std::endl;
        return;
    }

    // Write each value from the vector to the file, each on a new line
    for (const auto& value : scaledValues) {
        outFile << value << '\n';
    }

    // Close the file stream
    outFile.close();

    std::cout << "Data successfully saved to " << filename << std::endl;
}

std::tuple<int, int> GetIdxs(const std::vector<float>& data) {
    int first = 0;
    int last = 0;

    for (int i = 0; i < data.size(); ++i) {
        if (data[i] != 0) {
            first = i;
            break;
        }
    }
    int conseq = 0;
    for (int j = first; j < data.size(); ++j) {
        if (data[j] == 0) {
            conseq++;
        } else {
            conseq = 0;
        }
        if (conseq > 5) {
            last = j;
            break;
        }
    }
    std::cout << "FIRST & LAST: " << first << " & " << last << std::endl;
    return std::make_tuple(first, last);
}

// Utility function to convert std::vector to Eigen::VectorXd
Eigen::VectorXd toEigenVector(const std::vector<double>& vec) {
    Eigen::VectorXd eigenVec(vec.size());
    for (size_t i = 0; i < vec.size(); ++i) {
        eigenVec[i] = vec[i];
    }
    return eigenVec;
}

template<typename T>
const T* toRawPointer(const std::vector<T>& vec) {
    return vec.data();
}

std::vector<double> DesignLowPassFIRFilter(double normalizedCutoff, int filterOrder, const std::vector<double>& window)
{
    int halfOrder = filterOrder / 2;
    std::vector<double> h(filterOrder);

    for (int i = 0; i < filterOrder; ++i) {
        if (i == halfOrder) {
            h[i] = normalizedCutoff;
        } else {
            double numerator = std::sin(M_PI * normalizedCutoff * (i - halfOrder));
            double denominator = M_PI * (i - halfOrder);
            h[i] = numerator / denominator;
        }

        h[i] *= window[i];
    }

    return h;
}

int NextPowerOfTwo(int n) {
    int p = 1;
    while (p < n) {
        p <<= 1;
    }
    return p;
}

std::vector<std::complex<double>> Filfilt(const std::vector<double>& b, const std::vector<std::complex<double>>& x) {
    int len = x.size();
    int n = b.size();

    // Pad the filter and input arrays to the next power of two for efficient FFT computation
    int fftSize = NextPowerOfTwo(len + n - 1);

    // Apply the forward filter using FFT convolution
    std::vector<std::complex<double>> y = FftConvolve(x, b, fftSize);

    // Apply the backward filter using FFT convolution
    std::reverse(y.begin(), y.end());
    std::vector<std::complex<double>> z = FftConvolve(y, b, fftSize);
    std::reverse(z.begin(), z.end());

    return z;
}

std::vector<std::complex<double>> FftConvolve(const std::vector<std::complex<double>>& x, const std::vector<double>& b, int fftSize) {
    int xSize = x.size();
    int bSize = b.size();

    // Convert filter coefficients to std::complex<double>
    std::vector<std::complex<double>> bComplex(bSize);
    for (int i = 0; i < bSize; ++i) {
        bComplex[i] = std::complex<double>(b[i], 0);
    }

    // Pad the input and filter arrays
    std::vector<std::complex<double>> xPadded(fftSize, std::complex<double>(0, 0));
    std::vector<std::complex<double>> bPadded(fftSize, std::complex<double>(0, 0));
    std::copy(x.begin(), x.end(), xPadded.begin());
    std::copy(bComplex.begin(), bComplex.end(), bPadded.begin());

    // Create FFTW plans
    fftw_plan pX = fftw_plan_dft_1d(fftSize, reinterpret_cast<fftw_complex*>(xPadded.data()), reinterpret_cast<fftw_complex*>(xPadded.data()), FFTW_FORWARD, FFTW_ESTIMATE);
    fftw_plan pB = fftw_plan_dft_1d(fftSize, reinterpret_cast<fftw_complex*>(bPadded.data()), reinterpret_cast<fftw_complex*>(bPadded.data()), FFTW_FORWARD, FFTW_ESTIMATE);
    fftw_plan pInv = fftw_plan_dft_1d(fftSize, reinterpret_cast<fftw_complex*>(xPadded.data()), reinterpret_cast<fftw_complex*>(xPadded.data()), FFTW_BACKWARD, FFTW_ESTIMATE);

    // Perform FFT on both padded arrays
    fftw_execute(pX);
    fftw_execute(pB);

    // Element-wise multiplication in the frequency domain
    for (int i = 0; i < fftSize; ++i) {
        xPadded[i] *= bPadded[i];
    }

    // Inverse FFT to get the time domain result
    fftw_execute(pInv);

    // Normalize the result
    for (auto& val : xPadded) {
        val /= fftSize;
    }

    // Truncate the result to the original signal length
    std::vector<std::complex<double>> result(xSize);
    std::copy(xPadded.begin(), xPadded.begin() + xSize, result.begin());

    // Clean up
    fftw_destroy_plan(pX);
    fftw_destroy_plan(pB);
    fftw_destroy_plan(pInv);

    return result;
}

std::vector<std::complex<double>> Resample(const std::vector<std::complex<double>>& input, double fs_next, double fs_og)
{
    int inputLen = input.size();
    int outputLen = static_cast<int>(std::ceil(inputLen * fs_next / fs_og));

    std::vector<double> xOrig(inputLen);
    std::vector<double> xResampled(outputLen);
    std::vector<double> realOrig(inputLen);
    std::vector<double> imagOrig(inputLen);

    for (int i = 0; i < inputLen; ++i) {
        xOrig[i] = static_cast<double>(i) / fs_og;
        realOrig[i] = input[i].real();
        imagOrig[i] = input[i].imag();
    }

    for (int i = 0; i < outputLen; ++i) {
        xResampled[i] = static_cast<double>(i) / fs_next;
    }

    // Create Cardinal Cubic B-Spline interpolators
    double left_endpoint = xOrig.front();
    double step_size = xOrig[1] - xOrig[0];
    auto realSpline = boost::math::interpolators::cardinal_cubic_b_spline<double>(
        toRawPointer(realOrig), inputLen, left_endpoint, step_size, step_size, step_size
    );

    auto imagSpline = boost::math::interpolators::cardinal_cubic_b_spline<double>(
        toRawPointer(imagOrig), inputLen, left_endpoint, step_size, step_size, step_size
    );

    // Interpolate
    std::vector<std::complex<double>> output(outputLen);

    #pragma omp parallel for // Parallelize!
    for (int i = 0; i < outputLen; ++i) {
        double realResampled = realSpline(xResampled[i]);
        double imagResampled = imagSpline(xResampled[i]);
        output[i] = std::complex<double>(realResampled, imagResampled);
    }

    return output;
}

std::vector<double> WindowHamming(int filterOrder)
{
    std::vector<double> window(filterOrder);
    for (int n = 0; n < filterOrder; ++n)
    {
        window[n] = 0.54 - 0.46 * std::cos(2.0 * M_PI * n / (filterOrder - 1));
    }
    return window;
}

float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax)
{
    return outputMin + (outputMax - outputMin) * ((value - inputMin) / (inputMax - inputMin));
}

void terminateExistingPopotoApp() {
    // Find the process ID of the running popoto_app and terminate it
    std::string command = "pkill -f popoto_app";
    std::system(command.c_str());
}