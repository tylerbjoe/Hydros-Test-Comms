'''  
This file contains code to control the transmitting Popoto modem for benchtop demo. 
Written by Jacob Sage Vietorisz on 11/16/2023 for Delsys Inc./Altec Inc.
'''
import time
from socket_connection import connect_modem
import logging
import sys
import msvcrt
from numpy import random


if __name__ == '__main__':
    
    Rx, socket = connect_modem('10.0.0.231')                                                # connect to modem via the socket
    streamHandler = logging.StreamHandler(stream=sys.stdout)
    LOG_PATH = './demo/testRx.log'
    fileHandler = logging.FileHandler(filename = LOG_PATH)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console
                        handlers=[streamHandler, fileHandler],                  # WARNING -- When the logger writes to the command console, it can interfere with
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )                                                      
    
    Rx.verbose = 1                              # set the verbosity of the modem

    print('Preparing the receiver to receive data...')
    Rx.drainReplyQquiet()           # drain reply queue
    Rx.startRx()                    # put the modem in receive mode
    Rx.setValueF('UPCONVERT_Carrier', 20000)
    Rx.setValueF('DOWNCONVERT_Carrier', 20000)

    # transmit data packets on loop awaiting keyboard interrupt
    Tx_start = input('Press <ENTER> to begin reception.')
    while True:

        try:
            message = socket.recv(4096)           # read from the command socket
    
        except:
            Exception
            message = 'Error Reading Data'
        
        Rx.logger.info(message)
        
        time.sleep(2)

        # break loop when any key is pressed
        if msvcrt.kbhit():  
            break
        continue

    # gracefully terminate connection to modem
    Rx.tearDownPopoto()
