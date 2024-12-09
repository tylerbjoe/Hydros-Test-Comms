'''  
This file contains code to control the receiver Popoto modem to record transmitted modulated signals at passband. 
Written by Jacob Sage Vietorisz on 11/03/2023 for Delsys Inc./Altec Inc.

Edit History:
<MM/DD/YYYY> -- <name of contributor> -- <brief description>
-11/09/2023 --  Jacob Vietorisz -- updated file naming to better organize recorded files on the SD card of the modem
-11/20/2023 -- Jacob Vietorisz --  updated file naming to better organize recorded files on the SD card of the modem


'''
import time
from socket_connection import connect_modem
import logging
import sys

if __name__ == '__main__':

    Rx, cmdsocket = connect_modem('10.0.0.231')                                             # connect to modem via the socket
    streamHandler = logging.StreamHandler(stream=sys.stdout)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console and to a log file
                        handlers=[streamHandler],                               # WARNING -- When the logger writes to the command console, it can interfere with 
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )
    cmdsocket.settimeout(0.2)
    
    Rx.verbose = 1                  # set the verbosity of the modem

    # adjust the receiver parameters
    Rx.logger.info('Preparing the receiver to receive data...')
    Rx.drainReplyQquiet()           # drain reply queue
    Rx.setValueI('RecordMode', 0)   # put the modem out of receive mode so it will record at passband

    # declare list of the carrier frequencies on which we will record
    FREQUENCIES = [20, 25, 30, 35, 40]

    time.sleep(1)
    # loop over each frequency to make a recording

    # TODO - ACTION REQUIRED - change the file path to save the recording to
    DATE = 231109
    pos_index = 1
    TRIAL_NUM = 1

    for freq in FREQUENCIES:    
        # declare file name for recording
        FILE_NAME = './captures/{0}_pos{1}_{2}kHz_recording_trial{3}'.format(DATE, pos_index, freq, TRIAL_NUM)

        # set carrier frequency
        Rx.setValueF('UPCONVERT_Carrier', freq*1e3)
        Rx.setValueF('DOWNCONVERT_Carrier', freq*1e3)
        
        Rx_start = input(f'Press <ENTER> to begin recording at {freq} kHz')
        Rx.logger.info('Recording the signal at passband...')
        Rx.recordStartTarget(FILE_NAME, duration=60)     # start recording passband signal. Will automatically create new file after 60 seconds
        Rx_stop = input('Press <ENTER> to stop recording')
        time.sleep(2.0)
        continue

    Rx.logger.info('All recordings finished. Shutting down modem...')

    # gracefully terminate connection to modem
    Rx.tearDownPopoto()



