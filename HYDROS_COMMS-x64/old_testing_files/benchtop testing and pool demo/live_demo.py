''' 
Live plotting script for Delsys/Altec demonstration of popoto modem communication. This script pulls data from transmitter and receiver log files
that are updated live, and plots the values in the log file.

Written by Jacob Sage Vietorisz for Delsys/Altec on 11/16/2023
'''


import numpy as np
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
from itertools import count
from icecream import ic
import os
import shutil


# delete existing log files !!! DO THIS BEFORE PLOTTING
folder = './demo'
for filename in os.listdir(folder):
    file_path = os.path.join(folder, filename)
    try:
        if os.path.isfile(file_path) or os.path.islink(file_path):
            os.unlink(file_path)
        elif os.path.isdir(file_path):
            shutil.rmtree(file_path)
    except Exception as e:
        print('Failed to delete %s. Reason: %s' % (file_path, e))


# wait for input to start
start = input('Press <ENTER> to begin live plotting.')

fig, axs = plt.subplots(nrows=5, ncols=1, sharex=True, figsize=(10, 18)) 
plt.style.use('fivethirtyeight')

index = count()

ys = {i:[] for i in range(5)}
# x = []

def animate(i):

    plt.pause(0.01)

    # initialize empty lists

    # create y lists by extracting data from receiver log
    Rx_log = open('./demo/testRx.log')

    # new_length = len(Rx_log) - len(Rx_log_old)
    # Rx_log = Rx_log[new_length:]

    for line in (Rx_log.readlines() [-1:]):  # only reads the last line in log
        data_string = line[155:174]
        ic(data_string)
        
        # fields = line.split('\t')
        # for field in fields:
        #     _,_,val = field.partition(':')
        #     data_string = val                  # 109:128
        #     # ic(data_string)

        # catch strings that do not contain data
        if data_string=='':
            continue   
        try:
            integer = int(data_string[0])
        except:
            Exception
            integer = False
        if integer == False:
            continue

        # separate values to populate lists
        values = data_string.split(',')
        for j, value in enumerate(values):
            if j<5:
                ys[j].append(int(value))

    Rx_log.close()

    # create x axis of right length
    x = np.arange(0, len(ys[0]), 1)
    # counter = 0

    # x.append(counter)
    # fs = np.arange(1, 11, 2)
    # for key in ys:
    #     ys[key].append(200+int(50*np.sin(2*np.pi*fs[key]*counter)))
    
    # clear axes
    for j in ys:
        axs[j].cla()

        # plot data
        colors = ['tab:blue', 'tab:orange', 'tab:green', 'tab:red', 'tab:purple']
        axs[j].plot(x, ys[j], color=colors[j])
        axs[j].set_xlabel('Sample index')
        axs[j].set_ylabel('Signal{0}'.format(j))

        axs[0].set_title('Real-Time Data Received')

    plt.tight_layout()
    # counter +=np.pi/64


ani = FuncAnimation(plt.gcf(), animate, interval=1000, cache_frame_data=False)
plt.tight_layout()
plt.show()

# # read log files
# # extract data from transmitter log
# with open('./demo/testTx.log') as Tx_log:
#     for line in Tx_log:
#         fields = line.split('\t')
#         for field in fields:
#             key,_,val = field.partition(':')
#             data_string = val[54:73]
#             values = data_string.split(',')
#             for i, value in enumerate(values):
#                 if value == '':
#                     continue
#                 ys[i].append(int(value))

