'''  
This file contains code to control the socket connections to the Popoto modems during initial benchtop/pool tests. 
Written by Jacob Sage Vietorisz on 10/27/2023 for Delsys Inc./Altec Inc.

Edit History:
- 11/09/2023 - Jacob Vietorisz - added send_byte_data method. connect_modem method now is desgined to connect to a specific modem
                                ip for controlling multiple modems simultaneously via network switch

'''

def connect_modem(ip):
    ''' 
    This method takes care of establishing a socket connection to a modem that is directly 
    wired by ethernet to the PC via a properly configured ethernet port (refer to email from 
    Aaron from Popoto for configurations). It does not matter which modem is used.

    :params:
    ip: [string] The modem's IP address, in the form '10.0.0.23x' where x is 0 or 1

    :returns:
    Rx: [object] The receiver modem object with all popoto Python API methods
    socket: [object] The socket connection to the modem via the ethernet port
    '''
    from popoto import popoto
    from socket import socket, timeout as SocketTimeout, AF_INET, SOCK_STREAM

    # try_other_ip = False
    try:                                                  # try to connect to the modem twice using the other valid modem
        Modem = popoto(ip, 17000, 'root')       # IP address if the first attempt fails.
    except:
        Exception
    # The section below is for accessing whichever modem is connected to the PC. Won't work if both modems are connected
    #     try_other_ip = True
        
    # if try_other_ip:
    #     try:
    #         Modem = popoto('10.0.0.230', 17000, 'root')
    #     except:
    #         Exception

    socket = socket(AF_INET, SOCK_STREAM)
    socket.connect( (Modem.ip, Modem.cmdport) )
    
    print('Trying to establish transmitter connection...')
    socket.settimeout(3)            # Set a timeout of 3 seconds on the socket to prevent indefinite hanging
    print('You are connected to the modem!!')
    
    return Modem, socket


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

