'''  
This file contains code to control the transmitter Popoto modem to transmit modulated signals at various 
passband frequencies for recording. These recordings can be used to synthesize layered signals to simulate N divers 
Created by Jacob Sage Vietorisz on 11/03/2023 for Delsys Inc./Altec Inc.

Edit History: 
<MM/DD/YYYY> -- <name of contributor> -- <description of edits>
-11/09/2023 -- Jacob Vietorisz -- removed send_byte_data from file, since it is now located in the socket_connection.py file

'''
import time
from socket_connection import connect_modem, send_byte_data
import logging
import sys

if __name__ == '__main__':
    
    Tx, socket = connect_modem()                                                # connect to modem via the socket
    streamHandler = logging.StreamHandler(stream=sys.stdout)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console and to a log file
                        handlers=[streamHandler],                               # WARNING -- When the logger writes to the command console, it can interfere with
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )                                                      
    
    Tx.verbose = 1                              # set the verbosity of the modem

    # adjust the transmitter parameters
    print('Calibrating the transmitter...')
    Tx.calibrateTransmit()                      # calibrate modem to specify transmission power

    # declare list of the carrier frequencies on which we will transmit
    FREQUENCIES = [20, 25, 30, 35, 40]
    powers = [3.7, 16.1, 1.6, 0.8, 6.8]

    PAYLOAD_MODE = 0
    Tx.setValueI('PayloadMode', PAYLOAD_MODE)   # set the payload mode to 0 -> 80 bps with frequency hopping

    for i, freq in enumerate(FREQUENCIES):
        # set the carrier frequency
        Tx.setValueF('UPCONVERT_Carrier', freq)
        Tx.setValueF('DOWNCONVERT_Carrier', freq)

        # set the transmit power
        Tx.setValueF('TxPowerWatts', powers[i])

        Tx_start = input(f'Press <ENTER> to begin transmitting at {freq} kHz')
        Tx.logger.info('Transmitting...')
        for packet in range(10):        # send 10 data packets
            send_byte_data(Tx, [0,1,2,3,4]) 
            time.sleep(0.495)            # pause the thread between sending packets
        Tx.logger.info('Done transmitting ')
        time.sleep(1.0)
    Tx.logger.info('All transmissions complete. Shutting down modem...')

    # gracefully terminate connection to modem
    Tx.tearDownPopoto()
