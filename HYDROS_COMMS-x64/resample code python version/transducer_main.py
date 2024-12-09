"""
This file runs the methods in transducer_utils.py in a desired combination to
 - upsample and/or upshift a signal
 - downsample and/or downshift a signal
 - convert a .csv file to .wav and high pass the signal
 - save modified signals to .wav files
 - plot spectrograms
 - generate a linear chirp
 - plot PSD

 Set up the carrier frequencies, sampling frequencies, and read and write paths before running code

 Set action_run = True if you want to use one of the examples of how to use the utils. Then set action to be which
 example you wish to use.
"""
import os

import matplotlib.pyplot as plt
from scipy.io import wavfile
import time
from transducer_utils import *


top_directory = r"R:\Active Research\Projects\HYDROS\Dry-land experiments\240529 - New Transducer and Hydrophone " \
                r"feasibility tests"

action_run = False
action = 'down'

# carrier frequencies
fc_og = int(35e3)  # carrier frequency of signal before upshifting
fc_up = int(130e3)  # carrier frequency of upshifted signal to be transmitted through a transducer

# sampling frequencies
fs_up = int(1e6)  # sampling frequency of upsampled signal to be transmitted through a transducer
fs_og = 102400  # sampling frequency of original .wav files

# reading .wav file
wav_read_path = os.path.join(top_directory, "transmit_signals\\pure packets\\35kHz_multi_packet.wav")
fs_sigin, signal = wavfile.read(wav_read_path)  # read signal (if this is a pure packet, fs_og = fs_sigin)

# saving to .wav parameters
csv_read_path = os.path.join(top_directory, 'hydrophone_output\\Hydrophone - TC4033\\transducer_5')
wav_write_path = 'emulator_input\\transducer_5\\TC4033_to_35kHz\\'

if action_run:
    '''
    examples of how to use the utils
    '''
    if action == 'up':
        plot_spectrogram(signal, fs_sigin)
        tx_signal = upsample_upshift(signal, fs_og, fc_og, fs_up, fc_up)
        plot_spectrogram(tx_signal, fs_up)
    elif action == 'to wav':
        csv_to_wav(top_directory, fs_og, csv_read_path, wav_write_path)
    elif action == 'down':
        plot_spectrogram(signal, fs_sigin)
        down_signal = downsample_downshift(signal, fc_up, fs_sigin, fc_og, fs_og)
        plot_spectrogram(down_signal, fs_og)
    elif action == 'up down':
        tx_signal = upsample_upshift(signal, fs_og, fc_og, fs_up, fc_up)
        down_signal = downsample_downshift(tx_signal, fc_up, fs_up, fs_og, fc_og)
    elif action == 'chirp':
        fs = int(600e3)
        sig = generate_chirp(fs=fs, f0=int(20e3), chirp_dur=10, f1=int(300e3))
        plot_spectrogram(sig, fs)
        sf.write(os.path.join(top_directory, 'chirp_signal\\20_to_300kHz.wav'), sig, fs)
    elif action == 'psd':
        dir = os.path.join(top_directory, 'chirp_signal\\hydrophone_out')
        plt.figure(figsize=(12, 5))
        for file in os.listdir(dir):
            df = pd.read_csv(os.path.join(dir, file))
            plot_psd(df, int(600e3))

        plt.legend(['Transducer 3', 'Transducer 5'])
        plt.title('Linear Chirp (20kHz to 300kHz) with Hydrophone TC4033', fontdict={'fontsize': 25})
        ax = plt.gca()
        ax.tick_params(axis='both', labelsize=18)
        plt.tight_layout()
        fig = ax.get_figure()
        fig.savefig('linear_chirp_20_to_300_hydrophone_tc4033_both_transducers.svg', dpi=500)

else:
    '''
    custom combination of utils
    '''
    fc_ups = [100000, 125000, 150000, 175000, 200000, 225000, 250000, 300000]
    i = 0
    for file in os.listdir(csv_read_path):
        start = time.time()
        df = pd.read_csv(os.path.join(csv_read_path, file))

        # high pass the signal to get rid of drift
        df_high_passed = butter_highpass_filter(df, 10e3, fs_up)

        down_signal = downsample_downshift(df_high_passed, fc_ups[i], fs_up, fs_og, fc_og)
        plot_spectrogram(down_signal, fs_og)
        sf.write(os.path.join(top_directory, wav_write_path, str(fc_ups[i]) + 'kHz.wav'), down_signal, samplerate=fs_og)
        print(time.time() - start)
        i += 1



