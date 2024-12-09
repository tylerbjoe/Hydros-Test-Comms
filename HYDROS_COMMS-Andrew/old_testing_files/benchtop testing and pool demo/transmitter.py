'''  
This file contains code to control the transmitting Popoto modem during initial benchtop/pool tests. 
Written by Jacob Sage Vietorisz on 10/27/2023 for Delsys Inc./Altec Inc.
'''
import time
from socket_connection import connect_modem
import logging
import sys
import msvcrt
from numpy import random
import numpy as np

def send_byte_data(modem, data):
    ''' 
    A method to make it easier to send byte data for testing the modems.

    :params:
    modem: [object] The transmitter modem object, Tx.
    data: [list] A list of 5 decimal bytes (these are base-10 integers).

    :returns:
    None
    '''
    # convert list of bytes into a string as part of the json message
    data_string = ''
    for ind, byte in enumerate(data):
        data_string += str(byte)
        if ind != len(data)-1:
            data_string += ','
    json_message = ' { "Payload": {"Data": [' + data_string + '] } } '

    # transmit the message
    modem.transmitJSON(json_message)
    return


############################################################################################################################


if __name__ == '__main__':


    Tx, socket = connect_modem('10.0.0.230')                                                # connect to modem via the socket
    streamHandler = logging.StreamHandler(stream=sys.stdout)
    LOG_PATH = './demo/testTx.log'
    fileHandler = logging.FileHandler(filename = LOG_PATH)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console
                        handlers=[streamHandler, fileHandler],                  # WARNING -- When the logger writes to the command console, it can interfere with
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )                                                      
    
    Tx.verbose = 1                              # set the verbosity of the modem

    # adjust the transmitter parameters
    print('Calibrating the transmitter...')
    Tx.calibrateTransmit()                      # calibrate modem to specify transmission power
    Tx.setValueF('TxPowerWatts', 1)           # Set the transmitter power to 4 W (safe for benchtop air test)
    
    PAYLOAD_MODE = 0
    Tx.setValueI('PayloadMode', PAYLOAD_MODE)   # set the payload mode to 0 -> 80 bps with frequency hopping
    Tx.setValueF('UPCONVERT_Carrier', 20000)
    Tx.setValueF('DOWNCONVERT_Carrier', 20000)

    # transmit data packets on loop awaiting keyboard interrupt
    Tx_start = input('Press <ENTER> to begin transmission.')

    i = 0
    while True:
        data = [200+int(50*np.sin(2*np.pi*f*i)) for f in np.arange(1, 11, 2)]
        # data = random.randint(100, 255, 5)
        send_byte_data(Tx, data)            # send data to modem to transmit
        time.sleep(2)                   # pause the thread between sending packets
        
        # break loop when any key is pressed
        if msvcrt.kbhit():  
            break
        i+=np.pi/64
        continue

    # gracefully terminate connection to modem
    Tx.tearDownPopoto()
