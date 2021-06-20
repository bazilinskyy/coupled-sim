import socket
import struct

HOST, PORT = "localhost", 40131

# SOCK_DGRAM is the socket type to use for UDP sockets
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((HOST, PORT))

while True:
    received = sock.recv(1024)
    offset = 0
    num_cars = struct.unpack("i", received[0:4])[0]
    offset = offset + 4

    cars_pos = struct.unpack("{}f".format(num_cars*3),
                             received[offset:offset+num_cars*4*3])
    offset = offset + num_cars*4*3  # 3 floats, 4 bytes each

    num_ped = struct.unpack("i", received[offset:offset+4])[0]
    offset = offset + 4

    ped_pos = struct.unpack("{}f".format(num_ped*3),
                            received[offset:offset+num_ped*4*3])

    car_str = ", ".join(str(x) for x in cars_pos)
    ped_str = ", ".join(str(x) for x in ped_pos)

    print("Received:\nCars:{} - {}\nPedestrians:{} - {}"
          .format(num_cars, car_str, num_ped, ped_str))
