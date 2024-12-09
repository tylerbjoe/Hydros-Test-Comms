''' 
This is a script containing methods for performing a sine (frequency) sweep in a pool to measure
the impulse response. 
'''

from socket_connection import connect_modem
import sys
import logging
import time


if  __name__ == '__main__':
    # connect modem
    Rx, socket = connect_modem('10.0.0.231')

    streamHandler = logging.StreamHandler(stream=sys.stdout)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console and to a log file
                        handlers=[streamHandler],                               # WARNING -- When the logger writes to the command console, it can interfere with
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )                                                      
    
    Rx.verbose = 1                                                              # set the verbosity of the modem
    Rx.startRx()                                                                # set the modem to receive mode

    
    # parameters of the chirp .wav file (these must match a file on the SD card)
    DATE = 231116               # the current date in YYMMDD
    f0 = 15e3                   # the lower frequency bound of the chirp
    f1 = 45e3                   # the upper frequency bound of the chirp
    T=15                        # the total duration of the chirp in seconds
    method = 'logarithmic'      # the method used for ramping up the frequency
    pos_index = 0               # index corresponding to where in the pool the transmitting transducer is positioned for this recording
    trial_num = 1               # the number of recordings from this position

    FILE_NAME='./chirps/{0}_{1}-{2}kHz_{3}_pos{4}_trial{5}.wav'.format(DATE, int(f0/1e3), int(f1/1e3), method, pos_index, trial_num)

    # record the chirp    
    Rx.setValueI('RecordMode', 1)             # set the modem to record in baseband, so there is no shift. Frequencies are encoded in signal
    
    start = input('Press <ENTER> to begin recording...')
    Rx.logger.info('Recording chirp...')
    time.sleep(2)
    Rx.recordStartTargetStartTarget(FILE_NAME, duration=60)
    end = input('Press <ENTER> to stop recording...')
    Rx.recordStopTarget()

    Rx.logger.info('Recording complete. ')
    Rx.tearDownPopoto()
    

