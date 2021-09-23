import socket
import struct

HOST, PORT = "localhost", 40131

# SOCK_DGRAM is the socket type to use for UDP sockets
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((HOST, PORT))

while True:
    received = sock.recv(1024)
    offset = 0

### this is almost one to one translation of relevant part of WorldLogging
    packetType = struct.unpack("i", received[offset:offset+4])[0]
    offset = offset + 4

    if (packetType == 1) : # Log Begin
        numAICars = 0
        #int numAICars = 0;
        aiCarIndexes = []
        #List<int> aiCarIndexes = new List<int>();
        LocalDriver = struct.unpack("i", received[offset:offset+4])[0]
        offset = offset + 4
        #log.LocalDriver = reader.ReadInt32();
        numPersistentDrivers = struct.unpack("i", received[offset:offset+4])[0]
        offset = offset + 4
        #int numPersistentDrivers = reader.ReadInt32();    
        numPedestrians = struct.unpack("i", received[offset:offset+4])[0]
        offset = offset + 4
        #int numPedestrians = reader.ReadInt32();    
        numCarLights = struct.unpack("i", received[offset:offset+4])[0]
        offset = offset + 4
        #int numCarLights = reader.ReadInt32();    
        numPedestrianLights = struct.unpack("i", received[offset:offset+4])[0]
        offset = offset + 4
        #int numPedestrianLights = reader.ReadInt32();
        print("begin")
    elif (packetType == 2) : # Log Frame
        eventType = struct.unpack("i", received[offset:offset+4])[0]
        offset = offset + 4
        #var eventType = (LogFrameType)reader.ReadInt32();
        while (eventType == 1) :
        #if (eventType == LogFrameType.AICarSpawn)
        #{
            aiCarIndexes.append(numAICars + numPersistentDrivers)
            #aiCarIndexes.Add(numAICars + numPersistentDrivers);
            numAICars = numAICars + 1
            #numAICars++;
            #continue;
            eventType = struct.unpack("i", received[offset:offset+4])[0]
            offset = offset + 4
        #}
        ###Assert.AreEqual(LogFrameType.PositionsUpdate, eventType);
        frame = {}
        #var frame = new SerializedFrame();
        ###log.Frames.Add(frame);
        frame["timestamp"] = struct.unpack("f", received[offset:offset+4])[0]
        offset = offset + 4
        #frame.Timestamp = reader.ReadSingle();
        frame["roundtrip"] = struct.unpack("f", received[offset:offset+4])[0]
        offset = offset + 4
        #frame.RoundtripTime = reader.ReadSingle();

        numDriversThisFrame = numAICars + numPersistentDrivers

        #int numDriversThisFrame = numAICars + numPersistentDrivers;
        for i in range(numDriversThisFrame):
        #for (int i = 0; i < numDriversThisFrame; i++)
        #{
            key = "Driver " + str(i)
            frame[key] = {}

            frame[key]["position"] = struct.unpack("{}f".format(3), received[offset:offset+4*3])
            offset = offset + 4*3  # 3 floats, 4 bytes each
            #frame.DriverPositions.Add(reader.ReadVector3());
            frame[key]["rotation"] = struct.unpack("{}f".format(3), received[offset:offset+4*3])
            offset = offset + 4*3  # 3 floats, 4 bytes each
            #frame.DriverRotations.Add(reader.ReadQuaternion());
            frame[key]["blinker"] = struct.unpack("i", received[offset:offset+4])[0]
            offset = offset + 4
            #frame.BlinkerStates.Add((BlinkerState)reader.ReadInt32());
            if (i == LocalDriver) :
            #if (i == log.LocalDriver)
            #{
                frame[key]["rigidbody"] = struct.unpack("{}f".format(3), received[offset:offset+4*3]) # meters per second (not in Km per hour)
                offset = offset + 4*3  # 3 floats, 4 bytes each
                #frame.LocalDriverRbVelocity = reader.ReadVector3() * SpeedConvertion.Mps2Kmph;
            elif (aiCarIndexes.count(i) > 0) :
            #} else if (IsAICar(i))
            #{
                frame[key]["rigidbody"] = struct.unpack("{}f".format(3), received[offset:offset+4*3]) # meters per second (not in Km per hour)
                offset = offset + 4*3  # 3 floats, 4 bytes each
                #frame.AICarRbVelocities.Add(i, reader.ReadVector3() * SpeedConvertion.Mps2Kmph);
                frame[key]["speed"] = struct.unpack("f", received[offset:offset+4])[0]
                offset = offset + 4
                #frame.AICarSpeeds.Add(i, reader.ReadSingle());
                frame[key]["braking"] = struct.unpack("?", received[offset:offset+1])[0]
                offset = offset + 1
                #frame.braking.Add(i, reader.ReadBoolean());
                frame[key]["stopped"] = struct.unpack("?", received[offset:offset+1])[0]
                offset = offset + 1
                #frame.stopped.Add(i, reader.ReadBoolean());
                frame[key]["takeoff"] = struct.unpack("?", received[offset:offset+1])[0]
                offset = offset + 1
                #frame.takeoff.Add(i, reader.ReadBoolean());
                frame[key]["eyecontact"] = struct.unpack("?", received[offset:offset+1])[0]
                offset = offset + 1
                #frame.eyecontact.Add(i, reader.ReadBoolean());
            #}
        #}

        for i in range(numPedestrians):
        #for (int i = 0; i < numPedestrians; i++)
        #{
            key = "Pedestrian " + str(i)
            frame[key] = {}
            num_bones = struct.unpack("i", received[offset:offset+4])[0]
            offset = offset + 4
            for j in range(num_bones):
                bone_key = "bone " + str(j)
                frame[key][bone_key] = {}
                frame[key][bone_key]["position"] = struct.unpack("{}f".format(3), received[offset:offset+4*3])
                offset = offset + 4*3  # 3 floats, 4 bytes each
                #frame.PedestrianPositions.Add(reader.ReadListVector3());

            num_bones = struct.unpack("i", received[offset:offset+4])[0]
            offset = offset + 4
            for j in range(num_bones):
                bone_key = "bone " + str(j)
                frame[key][bone_key]["rotation"] = struct.unpack("{}f".format(3), received[offset:offset+4*3])
                offset = offset + 4*3  # 3 floats, 4 bytes each
                #frame.PedestrianRotations.Add(reader.ReadListQuaternion());

            offset = offset + 4
            #_ = reader.ReadInt32(); // Blinkers, unused
        #}
        for i in range(numCarLights):
        #for (int i = 0; i < numCarLights; i++)
        #{
            key = "CarLight " + str(i)
            frame[key] = {}

            frame[key]["state"] = struct.unpack("c", received[offset:offset+1])[0]
            offset = offset + 1
            #frame.CarLightStates.Add((LightState)reader.ReadByte());
        #}
        for i in range(numPedestrianLights):
        #for (int i = 0; i < numPedestrianLights; i++)
        #{
            key = "PedestrianLight " + str(i)
            frame[key] = {}

            frame[key]["state"] = struct.unpack("c", received[offset:offset+1])[0]
            offset = offset + 1
            #frame.PedestrianLightStates.Add((LightState)reader.ReadByte());
        #}
###
        print(frame)