# HYRDOS COMMS

## Modem Instructions

To run the Popoto Modem, you need a linux os to run the popoto_app. (1) Run the app on a computer
with linux or (2) use a WSL. 

### Downloading, Setting up and Running WSL
In the Windows PowerShell type the comment `wsl --install`
Once installed, you will need to create a user account and password for the newly installed Linux distribution.

To run the Linux distribution, open Windows Powershell. Click the down arrow next to the new tab button
and you should see Ubuntu. Click on that to open a Linux console. 

To access the C: drive on your Windows hard drive type in the command: `cd /mnt/c`

### Running the Modem
In one Linux terminal, run popoto_app with the following command: `./ popoto_app`

In a separate Linux terminal, you can set up the connection to the modem and send through a .wav file.
If Python is already installed on the Windows hard drive, you don't need to install it again. 
If you need to set up pip, enter the following commend: `sudo apt install python3-pip`

Make the connection by running `python3 default_shell.py`. Once running, first enter the command `connect localhost 17000`.
Next, set the carrier frequency of the signal you are sending through the modem. For example, if the carrier frequency
is 35kHz, you would type the command: `set Carrier 35000`. Next, put the modem in receive mode by typing: `startrx`.
Now the connection is made, and you can pump a .wav file through. To do this, enter the following command: 
`ff packet.wav`. For example, to run one of the pure packets, the command would be 
`ff wav_files/pure_packets/35kHz_multi_packet.wav`. If all is set up correctly, the data in the file should be 
decoded and displayed on the console. 

To disconnect from the modem enter: `exit`.

## Resampling and Modulating Packets

In the directory Popoto - resample code python version, you can find the code for upsampling, downsampling, upshifting,
and downshifting signals. This a python version of the code that was translated from the MATLAB util that Popoto sent us.

These files allow you to alter the sampling frequency and carrier frequency of a signal. See examples of how to use the 
utils in transducer_utils.py in transducer_main.py. Once you have the combination of modulations set up, run them by 
running transducer_main.py
