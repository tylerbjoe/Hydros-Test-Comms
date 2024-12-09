'''  
This file contains code to control the transmitting Popoto modem during initial benchtop/pool tests. 
Created by Jacob Sage Vietorisz on 10/27/2023 for Delsys Inc./Altec Inc.

edited: <MM/DD/YYYY> -- <name of contributor> -- <description of edits>
11/03/2023 -- jvietorisz -- updated title in github repository
'''
import time
from socket_connection import connect_modem
import logging
import sys
import msvcrt

def send_byte_data(modem, data):
    ''' 
    A method to make it easier to send byte data for testing the modems.

    :params:
    modem: [object] The transmitter modem object, Tx
    data: [list] A list of 5 decimal bytes (these are actually integers...)

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
    
    FILE_NAME = '231103_payload[0]_trial[1]' # TODO: ACTION REQUIRED -- set the log file name
    
    Tx, socket = connect_modem()                                                # connect to modem via the socket
    fileHandler = logging.FileHandler("{0}/{1}.log".format('./TxLogs', FILE_NAME))
    streamHandler = logging.StreamHandler(stream=sys.stdout)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console and to a log file
                        handlers=[fileHandler, streamHandler],                  # WARNING -- When the logger writes to the command console, it can interfere with
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )                                                      
    
    Tx.verbose = 1                              # set the verbosity of the modem

    # adjust the transmitter parameters
    print('Calibrating the transmitter...')
    Tx.calibrateTransmit()                      # calibrate modem to specify transmission power
    Tx.setValueF('TxPowerWatts', 4.0)           # Set the transmitter power to 4 W (safe for benchtop air test)
    
    PAYLOAD_MODE = 0
    Tx.setValueI('PayloadMode', PAYLOAD_MODE)   # set the payload mode to 0 -> 80 bps with frequency hopping

    # transmit data packets on loop awaiting keyboard interrupt
    Tx_start = input('Press <ENTER> to begin transmission.')

    i = 0
    while i<256:
        loop_start = time.time_ns()            # keep track of the beginning of each loop iteration
        
        Tx.logger.info(f'Packet #: {i}')

        loop_start_str = str(loop_start)
        data = [int(dig) for dig in loop_start_str]
        data.insert(0, i)
        
        t_start = time.time_ns()               # start time of packet transmission
        send_byte_data(Tx, data)                      # send data to modem to transmit
        t_end = time.time_ns()                 # end time of packet transmission

        Dt = t_end - t_start
        
        Tx.logger.info(f't_start: {t_start}')
        Tx.logger.info(f't_end:   {t_end}')
        Tx.logger.info(f'Dt:      {Dt}')

        time.sleep(0.495)                   # pause the thread between sending packets
        
        # break loop when any key is pressed
        if msvcrt.kbhit():  
            break
        i+=1
        loop_end = time.time_ns()               # keep track of the end of each loop iteration
        Dt_loop = loop_end - loop_start
        Tx.logger.info(f'Dt_loop: {Dt_loop}')
        Tx.logger.info('\n')
        continue

    # gracefully terminate connection to modem
    Tx.tearDownPopoto()
