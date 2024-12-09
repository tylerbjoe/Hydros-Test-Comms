'''  
This file contains code to control the receiver Popoto modem during initial benchtop/pool tests. 
Written by Jacob Sage Vietorisz on 10/27/2023 for Delsys Inc./Altec Inc.
'''
import time
from socket_connection import connect_modem
import socket
import logging
import sys
import msvcrt

if __name__ == '__main__':
    
    FILE_NAME = '231030_payload[0]_trial[5]'  # TODO: ACTION REQUIRED -- set the log file name

    Rx, cmdsocket = connect_modem()                                                # connect to modem via the socket
    fileHandler = logging.FileHandler("{0}/{1}.log".format('./RxLogs', FILE_NAME))
    streamHandler = logging.StreamHandler(stream=sys.stdout)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console and to a log file
                        handlers=[fileHandler, streamHandler],                  # WARNING -- When the logger writes to the command console, it can interfere with 
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )
    cmdsocket.settimeout(0.2)
    
    Rx.verbose = 1                  # set the verbosity of the modem

    # adjust the transmitter parameters
    print('Preparing the receiver to receive data...')
    Rx.drainReplyQquiet()           # drain reply queue
    Rx.startRx()                    # put the modem in receive mode


    # Receive data packets by emptying the reply queue with each iteration of while loop
    Rx_start = input('Press <ENTER> to begin reception')
    i=0
    while True:    
        loop_start = time.time_ns()
        # clear the socket each iteration
        
        try:
            t_start = time.time_ns()              # start time of reading socket
            message = cmdsocket.recv(4096)        # read from the command socket
            t_end = time.time_ns()                # end time of reading socket
    
        except socket.error as e:
            Rx.logger.error(e)
            message = 'Error Reading Data'
        
        time.sleep(0.1)
        
        # log times and received socket data
        Rx.logger.info(f'Iter #:  {i}')
        Rx.logger.info(f't_start: {t_start}')
        Rx.logger.info(f'Received socket data: {message}')
        Rx.logger.info(f't_end:   {t_end}') 

        Dt = t_end - t_start
        Rx.logger.info(f'Dt:      {Dt}')

        # break loop when any key is pressed
        if msvcrt.kbhit():  
            break
        i+=1
        loop_end = time.time_ns()
        Dt_loop = loop_end - loop_start
        Rx.logger.info(f'Dt_loop  {Dt_loop}')
        Rx.logger.info('\n')
        continue

    # gracefully terminate connection to modem
    Rx.tearDownPopoto()

