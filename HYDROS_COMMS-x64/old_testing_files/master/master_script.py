'''
This is the master script for running a battery of acoustic pool tests with the PMM3511 modems from Popoto Modem. 
Written by Jacob Sage Vietorisz on 12/01/2023 for Delsys/Altec.
'''
from socket_connection import connect_modem, send_byte_data
import sys
import logging
import time
import yaml
from easydict import EasyDict

# Load config file
config_path = "./config.yaml"
with open(config_path, 'r') as f:
    config = yaml.safe_load(f)
config = EasyDict(config)

class Experiments():
    '''
    This class contains a method for each pool test. An instance of Experiments will take the modem 
    class instances as arguments so that they are accessible by the methods in Experiments
    '''
    def __init__(self, tx_instance, rx_instance, config):
        self.Tx = tx_instance
        self.Rx = rx_instance
        self.config = config

    def chirp(self, *, record_path):
        '''
        Plays a chirp signal from a file on Tx's SD card, and records the signal as a file on Rx's SD card.

        :params:
        play_path: [str] The absolute path on Tx's SD card to the chirp signal to play.
        record_path: [str] The absolute path on Rx's SD card to give to the new recording.

        :returns: 
        None
        '''

        Rx.startRx()                            # set the modem to receive mode
        Rx.setValueI("RecordMode", 1)             # set the modem to record in baseband, so there is no shift. Frequencies are encoded in signal
        Rx.logger.info("Recording chirp...")
        
        # begin recording
        Rx.recordStartTarget(record_path, duration=60)
        time.sleep(2)
        Tx.setValueI("PlayMode", 1)             # set the modem to play in baseband, so there is no shift. Frequencies are encoded in signal
        Tx.logger.info("Playing chirp...")
        
        # begin playing chirp
        try:
            Tx.playStartTarget(filename=config.chirp_path, scale=config.play_scale)
            Tx.playStopTarget()
            Tx.logger.info("Finished playing chirp.")
            time.sleep(20)          # build in buffer time for reflections

        except:
            Exception
            Tx.logger.error(" !! ERROR !! No file found at specified path...")
        
        # stop recording
        Rx.recordStopTarget()
        Rx.logger.info("Recording complete.")


    def parallel(self, *, freqs, record_path_root):
        '''
        This method individually sends ten data packets on the specified carrier frequencies and invidualy records
        on each carrier frequency. 

        :params:
        freqs: [list or array] The list of carrier frequencies to be used, in kHz
        record_path_root: [str] The absolute path on Rx's SD card to give to the new recording. 
                                Does not including the frequency-specific labels that are added.
        '''

        Rx.logger.info("Preparing the receiver to receive data...")
        # WARNING -- you need to check with popoto whether this is the right record mode
        Rx.setValueI("RecordMode", 1)   # set Rx to record at passband (record the wave with all its modulations and frequencies)
        Tx.setValueI("PayloadMode", 0)   # set the payload mode to 0 -> 80 bps with frequency hopping

        # set the transmit power
        Tx.setValueF("TxPowerWatts", config.transmit_power)

        # main transmission and recording loop
        for f_kHz in freqs:
            Rx.drainReplyQquiet()           # drain reply queue at the receiver

            f_Hz = f_kHz*1e3 # convert from kHz to Hz
            
            # set carrier frequency
            Rx.setValueF("UPCONVERT_Carrier", f_Hz)
            Rx.setValueF("DOWNCONVERT_Carrier", f_Hz)
            Tx.setValueF("UPCONVERT_Carrier", f_Hz)
            Tx.setValueF("DOWNCONVERT_Carrier", f_Hz)
                
            # create actual file path for recording
            RECORD_PATH = record_path_root + "_{0}kHz.wav".format(f_kHz)
    
            # start recording
            Rx.logger.info(f"Recording at {f_kHz} kHz...")
            Rx.recordStartTarget(RECORD_PATH, duration=60)
            time.sleep(2)
            
            Tx.logger.info(f"Transmitting at {f_kHz} kHz...")
            for packet in range(10):        # send 10 data packets
                send_byte_data(Tx, [0,1,2,3,4]) 
                time.sleep(0.495)            # pause the thread between sending packets
            Tx.logger.info("Done transmitting.")

            time.sleep(5)       # allow time for reflections to get recorded
            
            Rx.recordStopTarget()
            Rx.logger.info("Done recording.")
            continue
    

        Rx.logger.info("Done recording all frequencies.")


#############################################################################################


if __name__ == "__main__":
    
    

    # Connect transmitting modem
    try:
        Tx, Tx_socket = connect_modem('10.0.0.230')
        Tx.logger.info('Tx connected!')
    except Exception:
        raise BufferError('Could not connect to Tx...')

    # Connect receiving modem
    try: 
        Rx, Rx_socket = connect_modem('10.0.0.231')
        Rx.logger.info('Rx connected!')
    except Exception:
        raise BufferError('Could not conect to Rx...')


    # set the logging stream to log to console
    streamHandler = logging.StreamHandler(stream=sys.stdout)
    logging.basicConfig(level=logging.INFO,                                     # edit the logger configurations to write to console and to a log file
                        handlers=[streamHandler],                               # WARNING -- When the logger writes to the command console, it can interfere with
                        format = '%(message)s'                                  # the python input() method. You can adjust where to log to, or adjust the verbosity
                        )                                                      

    # set the modems' logging level
    Tx.verbose = 1                                                              
    Rx.verbose = 1


    Exp  = Experiments(Tx, Rx, config)

    # test 1
    RECORD_PATH = "{0}_chirp_position{1}_trial{2}.wav".format(config.date, config.position, config.trial)
    Exp.chirp(record_path=RECORD_PATH)

    # test 2

    RECORD_ROOT = "{0}_parallel_position{1}_test1_trial{2}".format(config.date, config.position, config.trial)
    Exp.parallel(freqs=[20, 25, 30, 35, 40], record_path_root=RECORD_ROOT)

    # test 3

    RECORD_ROOT = "{0}_parallel_position{1}_test2_trial{2}".format(config.date, config.position, config.trial)
    Exp.parallel(freqs=[20, 24, 28, 32, 36, 40], record_path_root=RECORD_ROOT)

    # disconnect both modems
    Rx.logger.info("All tests completed. Shutting down...")
    Tx.tearDownPopoto()
    Rx.tearDownPopoto()






