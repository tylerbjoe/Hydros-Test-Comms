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
    Tx, socket = connect_modem('10.0.0.230')

    streamHandler = logging.StreamHandler(stream=sys.stdout)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console
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
    

    # play the chirp
    input('Press <ENTER> to begin transmitting chirp.')
    Tx.logger.info('Playing chirp...')
    Tx.setValueI('PlayMode', 1)             # set the modem to play in baseband, so there is no shift. Frequencies are encoded in signal
    Tx.playStartTarget(FILE_NAME, scale=1)
    time.sleep(20)                          # build in buffer time 
    Tx.playStopTarget()


    Tx.tearDownPopoto()
    

