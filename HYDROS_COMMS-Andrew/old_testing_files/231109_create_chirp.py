''' 
This file contains functions for creating chirp signals. The created signals are saved as .wav files
and need to be manually loaded onto the modem SD card

Written by Jacob Sage Vietorisz on 11/09/2023 for Delsys/Altec Inc.

Edit History:
<MM/DD/YYYY -- <contributor> -- <comment>
'''
import numpy as np
from socket_connection import connect_modem
import sys
import logging
import scipy as sc


def get_chirp(f0, f1, T, sample_rate, method):
    ''' 
    Produces a chirp signal with increasing frequency used for determining the impulse response of the pool.

    :params:
    f0: [float] The frequency to start the chirp, in Hz.
    f1: [float] The frequency to end the chirp, in Hz. Note f1 > f0
    T: [float] The duration of the chirp, in seconds.
    sample_rate: [int] The sampling rate of the signal in samples/second
    method: [str] The method used for producing the chirp. Valid values are 'linear' and 'logarithmic'.
            Note that if 'logarithmic', the amplitude modulated log chirp is returned (with flat PSD)
    
    :returns:
    chirp: [1D np.array] The chirp signal array to be transmitted
    '''

    N = int(T*sample_rate)

    time = np.linspace(0, T, N)

    if method=='linear':
        chirp = sc.signal.chirp(t=time, f0=f0, t1=T, f1=f1)

    elif method=='logarithmic':
        # define constants from meng2008
        L = T/np.log(f1/f0)
        K = (2*np.pi*f0)*L
        w = (K/L)*np.exp(time/L)        # the anglular frequency as a function of time
        
        A = np.sqrt(w/(2*np.pi*f0))     # amplitude modulation necessary for logarithmic chirp to have flat spectrum (constant energy per freq)
        chirp = A*sc.signal.chirp(t=time, f0=f0, t1=T, f1=f1, method='logarithmic')

    else:
        raise ValueError(f' "{method}" is not a valid method')

    return chirp


###################################################################################################################


if  __name__ == '__main__':
    # connect modem
    Tx, socket = connect_modem('10.0.0.230')

    streamHandler = logging.StreamHandler(stream=sys.stdout)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console and to a log file
                        handlers=[streamHandler],                               # WARNING -- When the logger writes to the command console, it can interfere with
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )                                                      
    
    Tx.verbose = 1                                                              # set the verbosity of the modem
    Tx.setValueF('TxPowerWatts', 1)                                             # set the initial transmit power

    
    # parameters of the chirp .wav file
    DATE = 231109
    f0 = 15e3
    f1 = 45e3
    T=15
    method = 'logarithmic'
    FILE_NAME='./chirps/{0}_{1}-{2}kHz_{3}.wav'.format(DATE, int(f0/1e3), int(f1/1e3), method)
    
    
    # TODO -- ACTION REQUIRED !! Create wave file
    create_wav = True # <---------- !!!!!
    if create_wav == True:
        # make a .wav file to play chirp
        sample_rate = Tx.SampFreq
        chirp = get_chirp(f0=f0, f1=f1, T=T, sample_rate=sample_rate, method=method)
        chirp = 10000*chirp/np.max(chirp)
        sc.io.wavfile.write(filename=FILE_NAME, rate=sample_rate, data=chirp.astype(np.int32)) # must use this dtype to create PCM file

    Tx.tearDownPopoto()
