import csv
import numpy as np
import matplotlib.pyplot as plt
import csv
import struct
from scipy.signal import stft, butter, filtfilt
from scipy.signal import find_peaks
from scipy.stats import linregress

#%% LOAD DATA

def load_signals_from_binary(file_path):
    """
    Load floating-point signals from a binary file.
    Assumes the file contains consecutive float32 values.
    
    :param file_path: Path to the binary file
    :return: List of float values
    """
    float_list = []
    
    with open(file_path, 'rb') as file:
        while chunk := file.read(4):  # Read 4 bytes at a time (size of float32)
            # Unpack the binary data to a float and append to the list
            float_list.append(struct.unpack('f', chunk)[0])
    
    return float_list

def get_fft(signal, fs):
    # Create an FFTW object
    fft_result = np.fft.fft(signal)
    fft_vals = np.abs(fft_result) / len(signal)
    
    freqs = np.fft.fftfreq(len(signal), 1 / fs)
    return freqs[:len(signal) // 2], fft_vals[:len(signal) // 2]

def integrate_fft_power(freqs, power_spectrum, center_freq=300000, bandwidth=2500):
    """
    Integrate the power spectral density in a given frequency band.
    
    :param freqs: Frequency array from FFT
    :param power_spectrum: Squared magnitude of FFT
    :param center_freq: Center frequency for integration (default 100 kHz)
    :param bandwidth: Half-bandwidth for integration (default ±2.5 kHz)
    :return: Integrated power over the specified frequency range
    """
    lower_bound = center_freq - bandwidth
    upper_bound = center_freq + bandwidth

    # Find indices within the frequency range
    indices = np.where((freqs >= lower_bound) & (freqs <= upper_bound))

    # Integrate (sum) power in the frequency range
    integrated_power = np.sum(power_spectrum[indices])
    return integrated_power
def bandpass_filter(signal, lowcut, highcut, fs, order=5):
    nyquist = 0.5 * fs  # Nyquist frequency
    low = lowcut / nyquist
    high = highcut / nyquist
    
    # Design the bandpass filter
    b, a = butter(order, [low, high], btype='band')
    
    # Apply the filter to the signal
    filtered_signal = filtfilt(b, a, signal)
    
    return filtered_signal

#%% Processing multiple packets
fs = 1_000_000  # Sampling frequency in Hz
index = [1,10,20]#np.arange(1, 21)  # Packet tx voltages
packet=[]

trial = "t2 300 1"
# for ind in index:
file_path = fr"C:\Users\TJoe\OneDrive - Delsys Inc\TJ HYDROS\HYDROS comms\Transducer Port Comparisons\{trial}.bin"

# Load and normalize signal
out1 = load_signals_from_binary(file_path)
out1 = bandpass_filter(out1, 250000, 350000, 1000000)
normalized_out1 = out1 - np.mean(out1)
# out1=out1[:1000000*3]
# Compute FFT
out1_freqs, out1_power_spectrum = get_fft(normalized_out1, fs)

# Integrate power around 100 kHz ± 2.5 kHz
power_integral = integrate_fft_power(out1_freqs, out1_power_spectrum, center_freq=300000, bandwidth=2500)

print(f"{trial}: Integrated power in {300000} ± {2500} Hz = {power_integral}")

# packet.append(power_integral)

#%% Plot FFT
# Plot the frequency spectrum
plt.figure(figsize=(10, 6))
plt.plot(out1_freqs, out1_power_spectrum)
plt.title(f"{trial} 30")
plt.xlabel("Frequency (Hz)")
plt.ylabel("Magnitude")
plt.grid()
plt.show()

#%%
# plt.figure()
# plt.plot(out1)
#%% Plot sft
# Zxx = zxx[::2, ::2]
# f = out1_sfreqs[::2]
# t = out1_stimes[::2]

# Zxx = zxx
# f = out1_sfreqs
# t = out1_stimes


# plt.figure(figsize=(10, 6))
# plt.pcolormesh(t, f, np.abs(Zxx), shading='gouraud')
# plt.title("10v packet 2v sine")
# plt.ylabel("Frequency [Hz]")
# plt.xlabel("Time [s]")
# plt.colorbar(label="Magnitude")
# plt.tight_layout()
# plt.show()

# # #%%

#%%
# #plot rx psd and linear model
# # Perform linear regression
# index = np.array(index)
# plt.figure()
# plt.title('Power Spectral Density at Test Position')

# plt.plot(index,packet,label='Packet')
# plt.plot(index,index*linregress(index,packet).slope,label=f'slope={linregress(index,packet).slope:.2f}', linestyle='--', color='grey')

# plt.xlabel('Tx Voltage')
# plt.ylabel('Rx Packet PSD (Voltage^2)')
    
# plt.legend()
# plt.show()
    
#%% Packet Saver 
# import numpy as np

# # Specify the input and output file names
# input_file = r"U:\Users Common\TJoe\Brandeis Pool Tests\2.6.25\trial_13.bin"
# output_file = r"U:\Users Common\TJoe\Brandeis Pool Tests\2.6.25\trial_13_packets\start_noise.bin"

# # Define the data type; adjust as needed (e.g., np.float32 or np.float64)
# dtype = np.float32

# # Read the entire binary file into a NumPy array
# data = np.fromfile(input_file, dtype=dtype)
# print("Original data:", data)

# # Slice the array as needed; for example, take elements from index 10 to 20
# splice = data[int(0.01e7):int(0.26e7)]
# print("Spliced data:", splice)

# # Save the spliced array to a new binary file
# splice.tofile(output_file)
# print(f"Spliced data saved to {output_file}")

vs = [1,10,20]

plt.figure()
plt.title("Tx 1 vs Tx 2 300kHz")
plt.xlabel('Tx Voltage (V)')
plt.ylabel('Rx Power Spectral Density at 300kHz (V^2)')
plt.plot(vs,[1.946,19.859,39.583],label="Tx1")
plt.plot(vs,[1.848,18.548,37.201],label="Tx2")
plt.legend()
plt.show()



























