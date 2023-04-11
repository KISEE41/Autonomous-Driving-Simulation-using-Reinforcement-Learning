import sys

import socket
import ujson
from base64 import b64decode

import cv2
import numpy as np


class Socket:
    def __init__(self, host, port, timeout=15.0):
        self.host = host
        self.port = port
        self.connection = None
        self._last_image = None #for sending an image in case of error

        #creating the socket
        self.soc = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        print(f"Socket created at {self.host} with port {self.port}")

        self.soc.settimeout(timeout)
        self.connect()


    def connect(self):
        try:
            self.soc.bind((self.host, self.port))
        except socket.error as error_msg:
            self.connect()

        print("Listening to the client connection...")
        #start listening on socket
        self.soc.listen(0)

        try:
            self.connection, addr = self.soc.accept()  #return a object with list of ipaddress and port
            print(f"Connected with  the client with ipaddress: {addr[0]} and at port: {addr[1]}")
        except socket.timeout:
            print("Socket connection time out")
            sys.exit()


    def disconnect(self):
        print("The connection was closed. So closing the Socket.")
        self.connection.close()
        self.soc.close()


    def send_reset(self):
        self._write_message(
            '{"action":' + str(0) + ',"resetGame":' + str(1) + ',"quit":' + str(0) +'}')


    def send_state(self, action, reset=0, quit=0):
        """ Send an action command to the game """
        self._write_message(
            '{"action":' + str(int(action)) + ',"resetGame":' + str(reset) + ',"quit":' + str(quit) +'}')


    def _write_message(self, msg):
        self.connection.sendall(bytearray(msg, 'utf-8'))


    def get_state(self):
        """ Returns a picture of the current game state, and the state of the game """
        data = self._read_message()
        #first the message received have to be decoded
        
        try:
            image = self._decode_image(data["encodedImage"])
            # Record the previous image to send in case of image decoding errors
            self._last_image = image

            return image

        except:
            self.disconnect()
            sys.exit()
    

    def _read_message(self):
        main_data = ""
        while True:
            #decoding the bytes format
            data = self.connection.recv(1000000).decode('utf-8')

            if "QUITING!" in data:
                print("Unity closing! Trying to reconnect...")
                return 0

            main_data += data
            if "}" in data: break

        # BUG FIX: If there was extra data, strip it out
        # main_data[0] != '{' and main_data[-1] != '}'
        
            

        if main_data.count('{') > 1:
            first_open = main_data.index('{')
            last_open = main_data.rindex('{')
            first_close = main_data.index('}')
            last_close = main_data.rindex('}')

            if last_open < last_close:
                fixed_data = main_data[last_open:last_close + 1]
            elif first_open < first_close:
                fixed_data = main_data[first_open:first_close + 1]
        else:
            fixed_data = main_data

        # Get rid of any other data there
        try:
            #"Converting in json
            decoded = ujson.loads(fixed_data)
        except Exception as e:
            print("ERROR while decoding\n", main_data)
            return self._last_image

        return decoded


    def _decode_image(self, base64_img):
        a = b64decode(base64_img)
        np_arr = np.frombuffer(a, np.uint8)
        img_np = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)

        return img_np






   
        