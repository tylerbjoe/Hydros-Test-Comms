'''
Contains a method that is used for executing a chirp test. 
The file designed to be imported as a module in master_scripy.py

Written by Jacob Sage Vietorisz on 12/01/2023 for Delsys/Altec
'''

import time
from socket_connection import send_byte_data


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

        self.Rx.startRx()                            # set the modem to receive mode
        self.Rx.setValueI("RecordMode", 1)             # set the modem to record in baseband, so there is no shift. Frequencies are encoded in signal
        self.Rx.logger.info("Recording chirp...")
        
        # begin recording
        self.Rx.recordStartTarget(record_path, duration=60)
        time.sleep(2)
        self.Tx.setValueI("PlayMode", 1)             # set the modem to play in baseband, so there is no shift. Frequencies are encoded in signal
        self.Tx.logger.info("Playing chirp...")
        
        # begin playing chirp
        try:
            self.Tx.playStartTarget(filename=self.config.chirp_path, scale=self.config.play_scale)
            self.Tx.playStopTarget()
            self.Tx.logger.info("Finished playing chirp.")
            self.time.sleep(20)          # build in buffer time for reflections

        except:
            Exception
            self.Tx.logger.error(" !! ERROR !! No file found at specified path...")
        
        # stop recording
        self.Rx.recordStopTarget()
        self.Rx.logger.info("Recording complete.")


    def parallel(self, *, freqs, record_path_root):
        '''
        This method individually sends ten data packets on the specified carrier frequencies and invidualy records
        on each carrier frequency. 

        :params:
        freqs: [list or array] The list of carrier frequencies to be used, in kHz
        record_path_root: [str] The absolute path on Rx's SD card to give to the new recording. 
                                Does not including the frequency-specific labels that are added.
        '''

        self.Rx.logger.info("Preparing the receiver to receive data...")
        # WARNING -- you need to check with popoto whether this is the right record mode
        self.Rx.setValueI("RecordMode", 1)   # set Rx to record at passband (record the wave with all its modulations and frequencies)
        self.Tx.setValueI("PayloadMode", 0)   # set the payload mode to 0 -> 80 bps with frequency hopping

        # set the transmit power
        self.Tx.setValueF("TxPowerWatts", self.config.transmit_power)

        # main transmission and recording loop
        for f_kHz in freqs:
            self.Rx.drainReplyQquiet()           # drain reply queue at the receiver

            f_Hz = f_kHz*1e3 # convert from kHz to Hz
            
            # set carrier frequency
            self.Rx.setValueF("UPCONVERT_Carrier", f_Hz)
            self.Rx.setValueF("DOWNCONVERT_Carrier", f_Hz)
            self.Tx.setValueF("UPCONVERT_Carrier", f_Hz)
            self.Tx.setValueF("DOWNCONVERT_Carrier", f_Hz)
                
            # create actual file path for recording
            RECORD_PATH = record_path_root + "_{0}kHz.wav".format(f_kHz)
    
            # start recording
            self.Rx.logger.info(f"Recording at {f_kHz} kHz...")
            self.Rx.recordStartTarget(RECORD_PATH, duration=60)
            time.sleep(2)
            
            self.Tx.logger.info(f"Transmitting at {f_kHz} kHz...")
            for packet in range(10):        # send 10 data packets
                send_byte_data(self.Tx, [0,1,2,3,4]) 
                time.sleep(0.495)            # pause the thread between sending packets
            self.Tx.logger.info("Done transmitting.")

            time.sleep(5)       # allow time for reflections to get recorded
            
            self.Rx.recordStopTarget()
            self.Rx.logger.info("Done recording.")
            continue
    

        self.Rx.logger.info("Done recording all frequencies.")