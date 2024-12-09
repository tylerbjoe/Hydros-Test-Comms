import pandas as pd
import soundfile as sf
import numpy as np
import os
import matplotlib.pyplot as plt
from scipy.signal import firwin, filtfilt, resample_poly, spectrogram, sosfilt, butter, chirp, welch
from fractions import Fraction


def upsample_upshift(sigin, fs_og, fc_og, fs_up, fc_up, down_next=False):
    """
    Upshifts signal to a new carrier frequency AND upsamples to a new sampling frequency.

    Only upshift: fs_up = fs_og, fc_up > fc_og
    Only upsample: fc_up = fc_og, fs_up > fs_og
    Both upshift and upsample: fs_up > fs_og, fc_up > fc_og

    :param sigin: original signal to be manipulated
    :param fs_og: sampling frequency of original signal
    :param fc_og: carrier frequency of original signal
    :param fs_up: sampling frequency of signal to be transmitted
    :param fc_up: carrier frequency of signal to be transmitted
    :param down_next: bool to determine if a downsample will directly follow to up (whether we convert to real output)
    :return sigout: manipulated signal
    """

    # modulate to baseband
    sig_arr = np.arange(1, len(sigin) + 1)
    bb_factor = np.exp(np.emath.sqrt(-1) * 2 * np.pi * -fc_og * sig_arr / fs_og)
    bb = np.multiply(sigin, bb_factor)

    # filtering with FIR filter
    b = firwin(1024, 2 * 5120 / fs_og, window='hamming', pass_zero='lowpass')
    bb_filtered = filtfilt(b, 1, bb)

    # resample to a higher rate
    rate = Fraction(int(fs_up), int(fs_og))
    bbew = resample_poly(bb_filtered, rate.numerator, rate.denominator)

    # modulate to passband
    sig_arr_new = np.arange(1, len(bbew) + 1)
    pb_factor = np.exp(np.emath.sqrt(-1) * 2 * np.pi * fc_up * sig_arr_new / fs_up)
    pb = np.multiply(bbew, pb_factor)

    # convert to real output
    if down_next:
        return pb
    else:
        real = []
        imag = []
        for num in pb:
            real.append(num.real)
            imag.append(num.imag)

        sigout = np.subtract(real, imag)

        return sigout


def downsample_downshift(sigin, fc_up, fs_up, fs_down, fc_down):
    """
    Downshifts signal to a new carrier frequency AND downsamples to a new sampling frequency.
    Mirrors upsample_upshift()

    Only downshift: fs_up = fs_down, fc_up > fc_down
    Only downsample: fc_up = fc_down, fs_up > fs_down
    Both downshift and downsample: fs_up > fs_down, fc_up > fc_do wn

    :param sigin: signal to be manipulated, the RxSignal
    :param fc_up: carrier frequency of sigin
    :param fs_up: sampling frequency of signin
    :param fs_down: sampling frequency of the original frequency, the fs downsampling to
    :param fc_down: carrier frequency of the transmitted signal, the fc downshifting to
    :return sigout: the manipulated signal
    """

    if sigin.ndim > 1:
        sigin = sigin.flatten()

    # undo modulate to passband
    sig_arr = np.arange(1, len(sigin) + 1)
    pb_factor = np.exp(np.emath.sqrt(-1) * 2 * np.pi * fc_up * sig_arr / fs_up)
    no_pb = np.divide(sigin, pb_factor)

    # resample to lower rate
    rate = Fraction(int(fs_up), int(fs_down))
    down_sig = resample_poly(no_pb, rate.denominator, rate.numerator)

    # undo modulate to baseband
    sig_arr_new = np.arange(1, len(down_sig) + 1)
    bb_factor = np.exp(np.emath.sqrt(-1) * 2 * np.pi * -fc_down * sig_arr_new / fs_down)
    no_bb = np.divide(down_sig, bb_factor)

    # convert to real output
    real = []
    imag = []
    for num in no_bb:
        real.append(num.real)
        imag.append(num.imag)

    sigout = np.subtract(real, imag)

    return sigout


def plot_spectrogram(x, fs):
    """
    Plots the spectrogram of a given signal using the scipy implementation.
    :param x: signal
    :param fs: sampling frequency
    :return: None
    """
    f, t, Sxx = spectrogram(x, fs)
    plt.figure(figsize=(16, 10))
    plt.pcolormesh(t, f, Sxx, shading='gouraud')
    plt.ylabel('Frequency (Hz)')
    plt.xlabel('Time (sec)')
    plt.show()


def csv_to_wav(top_dir, samplerate, csv_read_dir, wav_write_dir):
    """
    reads in .csv file outputted from hydrophone. the high pass filter helps eliminate any signal drift.
    then gets saved as a .wav file
    :param top_dir: directory of the project, used when specifying the save path of the .wav files
    :param samplerate: fs of the .wav file to be saved, should be the same as the signal acquired and saved to the .csv
    :param csv_read_dir: directory to the .csv files, the output of the hydrophone
    :param wav_write_dir: directory to write the new .wav file to
    """

    for file in os.listdir(csv_read_dir):
        df = pd.read_csv(os.path.join(csv_read_dir, file))

        # times points for each data point (about 21.2 seconds)
        times = np.arange(0, len(df) / samplerate, 1 / samplerate)

        # high pass the signal to get rid of drift
        df_high_passed = butter_highpass_filter(df, 10e3, samplerate)

        # plotting signals
        # plt.plot(times, df)
        # plt.scatter(times, df)
        # plt.plot(times, df_high_passed)
        # plt.legend(['original', 'high pass'])
        # plt.show()

        # save as .wav files
        sf.write(os.path.join(top_dir, wav_write_dir, 'high_passed',
                              file[:-4] + '_high_passed.wav'), df_high_passed, samplerate=int(samplerate))
        sf.write(os.path.join(top_dir, wav_write_dir, file[:-4] + '.wav'),
                 df, samplerate=int(samplerate))

        return os.path.join(top_dir, wav_write_dir, 'high_passed', file[:-4] + '_high_passed.wav')


def butter_highpass_filter(data, cutoff, fs, order=2):
    """
    Applies scipy's implementation of butterworth high-pass filter. Used on data acquired from the hydrophone to remove
    signal drift
    :param data: signal to be high-passed
    :param cutoff: frequency to start passing
    :param fs: sampling frequency of data
    :param order: order of the filter (default = 2)
    """
    sos = butter(order, cutoff, fs=fs, btype='highpass', output='sos')
    y = sosfilt(sos, data, axis=0)
    return y


def generate_chirp(fs, f0, chirp_dur, f1):
    """
    Generates a linear chirp. Used to characterize the frequency band of the transducers.

    :param fs: sampling frequency of the chirp
    :param f0: frequency the chirp starts with
    :param chirp_dur: duration of the chirp
    :param f1: frequency the chirp ends with
    """
    time_arr = np.linspace(start=0, stop=chirp_dur, num=fs * chirp_dur)
    sigout = chirp(t=time_arr, f0=f0, t1=10, f1=f1, method='linear')
    return sigout


def plot_psd(signal, fs):
    """
    Plots scipy's implementation of welch's periodogram to visualize the power spectral density of a signal.
    Used to characterize the transducer's bandwidth. The PSD is scaled to the units dB/Hz

    :param signal: signal to calculate the PSD from
    :param fs: sampling frequency of signal
    """
    if signal.ndim > 1:
        signal = signal.to_numpy().flatten()
    f, Pxx = welch(signal, fs)

    plt.plot(f, 10*np.log10(Pxx/10))
    plt.xlabel('Frequency [Hz]', fontdict={'fontsize': 25})
    plt.ylabel('PSD [dB/Hz]', fontdict={'fontsize': 25})
    # plt.show()
