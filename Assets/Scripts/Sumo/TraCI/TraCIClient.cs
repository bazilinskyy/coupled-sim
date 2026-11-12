using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using static CoupledSim.TraCI.TraCIConstants;

namespace CoupledSim.TraCI
{
    /// <summary>State of one SUMO vehicle as received through a variable subscription.</summary>
    public sealed class SumoVehicleState
    {
        public string Id;
        /// <summary>Front bumper centre in SUMO network coordinates (m).</summary>
        public TraCIPosition Position;
        /// <summary>Heading in degrees, 0 = north (+y), clockwise.</summary>
        public double Angle;
        public double Speed;
        public double Length = 5;
        public double Width = 1.8;
        public double Slope;
        public string TypeId = "";
        public string VehicleClass = "passenger";
        public int Signals;
        public TraCIColor Color = new TraCIColor(255, 255, 0, 255);
        /// <summary>SUMO time of the last update (s).</summary>
        public double Time;
    }

    /// <summary>
    /// Minimal, dependency-free TraCI client (the SUMO TCP control protocol).
    /// Supports what the coupled simulator needs: stepping, vehicle subscriptions,
    /// injecting externally controlled vehicles (moveToXY) and coordinate conversion.
    /// Not thread-safe: use from one thread.
    /// </summary>
    public sealed class TraCIClient : IDisposable
    {
        TcpClient _tcp;
        NetworkStream _stream;

        public int ApiVersion { get; private set; }
        public string SumoVersion { get; private set; }
        public double SimTime { get; private set; }

        /// <summary>Vehicles currently in the SUMO network, by ID (updated in <see cref="SimulationStep"/>).</summary>
        public readonly Dictionary<string, SumoVehicleState> Vehicles = new Dictionary<string, SumoVehicleState>();
        /// <summary>IDs that entered/left the network during the last step.</summary>
        public readonly List<string> Departed = new List<string>();
        public readonly List<string> Arrived = new List<string>();
        /// <summary>Vehicles that are not mirrored in Vehicles (e.g. the ego vehicles we drive ourselves).</summary>
        public readonly HashSet<string> IgnoredVehicles = new HashSet<string>();

        static readonly byte[] VehicleVars =
        {
            VAR_POSITION3D, VAR_ANGLE, VAR_SPEED, VAR_LENGTH, VAR_WIDTH, VAR_SLOPE,
            VAR_TYPE, VAR_VEHICLECLASS, VAR_SIGNALS, VAR_COLOR,
        };

        public bool Connected => _tcp != null && _tcp.Connected;

        /// <summary>Connects to a running SUMO started with --remote-port. Retries until the timeout.</summary>
        public void Connect(string host, int port, int timeoutMs = 10000, int receiveTimeoutMs = 30000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            Exception last = null;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    _tcp = new TcpClient { NoDelay = true, ReceiveTimeout = receiveTimeoutMs, SendTimeout = receiveTimeoutMs };
                    _tcp.Connect(host, port);
                    _stream = _tcp.GetStream();
                    break;
                }
                catch (SocketException e)
                {
                    last = e;
                    _tcp?.Dispose();
                    _tcp = null;
                    Thread.Sleep(200);
                }
            }
            if (_tcp == null)
                throw new TraCIException($"Could not connect to SUMO at {host}:{port}: {last?.Message}");

            var r = SendCommand(CMD_GETVERSION, new TraCIWriter());
            r.CommandLength();
            r.UByte(); // CMD_GETVERSION
            ApiVersion = r.Int();
            SumoVersion = r.String();
        }

        /// <summary>Sets the client order (only needed when several clients share one SUMO).</summary>
        public void SetOrder(int order) => SendCommand(CMD_SETORDER, new TraCIWriter().Int(order));

        /// <summary>Subscribes to departed/arrived vehicle lists so new vehicles are picked up automatically.</summary>
        public void SubscribeSimulation()
        {
            var w = new TraCIWriter()
                .Double(INVALID_DOUBLE_VALUE).Double(MAX_TIME)
                .String("")
                .UByte(3).UByte(VAR_DEPARTED_VEHICLES_IDS).UByte(VAR_ARRIVED_VEHICLES_IDS).UByte(VAR_TIME);
            var r = SendCommand(CMD_SUBSCRIBE_SIM_VARIABLE, w);
            ReadSubscriptionResponse(r);
            Departed.Clear();
            Arrived.Clear();
            // Vehicles already in the network when we connect
            foreach (var id in GetStringList(CMD_GET_VEHICLE_VARIABLE, TRACI_ID_LIST, ""))
                if (!IgnoredVehicles.Contains(id)) SubscribeVehicle(id);
        }

        public void SubscribeVehicle(string id)
        {
            var w = new TraCIWriter().Double(INVALID_DOUBLE_VALUE).Double(MAX_TIME).String(id).UByte((byte)VehicleVars.Length);
            foreach (var v in VehicleVars) w.UByte(v);
            if (!Vehicles.ContainsKey(id)) Vehicles[id] = new SumoVehicleState { Id = id };
            var r = SendCommand(CMD_SUBSCRIBE_VEHICLE_VARIABLE, w);
            ReadSubscriptionResponse(r);
        }

        /// <summary>Advances SUMO to targetTime (s); 0 = one step. Updates Vehicles, Departed and Arrived.</summary>
        public void SimulationStep(double targetTime = 0)
        {
            Departed.Clear();
            Arrived.Clear();
            var r = SendCommand(CMD_SIMSTEP, new TraCIWriter().Double(targetTime));
            int n = r.Int();
            for (int i = 0; i < n; i++) ReadSubscription(r);

            foreach (var id in Arrived) Vehicles.Remove(id);
            foreach (var id in Departed)
                if (!IgnoredVehicles.Contains(id)) SubscribeVehicle(id);
        }

        void ReadSubscriptionResponse(TraCIReader r)
        {
            if (!r.AtEnd) ReadSubscription(r);
        }

        void ReadSubscription(TraCIReader r)
        {
            int len = r.CommandLength();
            int end = r.Position + len;
            byte response = r.UByte();
            string objectId = r.String();
            int numVars = r.UByte();
            bool known = response == RESPONSE_SUBSCRIBE_VEHICLE_VARIABLE || response == RESPONSE_SUBSCRIBE_SIM_VARIABLE;
            if (!known)
            {
                r.Position = end; // skip subscriptions we did not ask for
                return;
            }
            SumoVehicleState veh = null;
            if (response == RESPONSE_SUBSCRIBE_VEHICLE_VARIABLE && !Vehicles.TryGetValue(objectId, out veh))
            {
                veh = new SumoVehicleState { Id = objectId };
                Vehicles[objectId] = veh;
            }
            for (int i = 0; i < numVars; i++)
            {
                byte varId = r.UByte();
                byte status = r.UByte();
                object value = r.Typed();
                if (status != RTYPE_OK) continue; // value is an error string
                if (response == RESPONSE_SUBSCRIBE_SIM_VARIABLE)
                {
                    switch (varId)
                    {
                        case VAR_DEPARTED_VEHICLES_IDS: Departed.AddRange((List<string>)value); break;
                        case VAR_ARRIVED_VEHICLES_IDS: Arrived.AddRange((List<string>)value); break;
                        case VAR_TIME: SimTime = (double)value; break;
                    }
                }
                else
                {
                    ApplyVehicleVar(veh, varId, value);
                }
            }
            if (veh != null) veh.Time = SimTime;
            r.Position = end;
        }

        static void ApplyVehicleVar(SumoVehicleState v, byte varId, object value)
        {
            switch (varId)
            {
                case VAR_POSITION3D:
                case VAR_POSITION: v.Position = (TraCIPosition)value; break;
                case VAR_ANGLE: v.Angle = (double)value; break;
                case VAR_SPEED: v.Speed = (double)value; break;
                case VAR_LENGTH: v.Length = (double)value; break;
                case VAR_WIDTH: v.Width = (double)value; break;
                case VAR_SLOPE: v.Slope = (double)value; break;
                case VAR_TYPE: v.TypeId = (string)value; break;
                case VAR_VEHICLECLASS: v.VehicleClass = (string)value; break;
                case VAR_SIGNALS: v.Signals = (int)value; break;
                case VAR_COLOR: v.Color = (TraCIColor)value; break;
            }
        }

        // ------------------------------------------------------------------ queries

        public double GetDeltaT() => (double)GetVariable(CMD_GET_SIM_VARIABLE, VAR_DELTA_T, "");
        public int GetMinExpectedVehicles() => (int)GetVariable(CMD_GET_SIM_VARIABLE, VAR_MIN_EXPECTED_VEHICLES, "");

        /// <summary>Network bounding box: (xmin, ymin, xmax, ymax).</summary>
        public double[] GetNetBoundary()
        {
            var v = GetVariable(CMD_GET_SIM_VARIABLE, VAR_NET_BOUNDING_BOX, "");
            if (v is double[] d) return d;
            var p = (TraCIPosition[])v;
            return new[] { p[0].X, p[0].Y, p[1].X, p[1].Y };
        }

        public List<string> GetEdgeIds() => GetStringList(CMD_GET_EDGE_VARIABLE, TRACI_ID_LIST, "");

        /// <summary>Converts longitude/latitude (degrees) to network x/y. Needs a geo-referenced network.</summary>
        public TraCIPosition LonLatToXY(double lon, double lat)
        {
            var param = new TraCIWriter().Compound(2)
                .UByte(POSITION_LON_LAT).Double(lon).Double(lat)
                .TypedUByte(POSITION_2D);
            return (TraCIPosition)GetVariable(CMD_GET_SIM_VARIABLE, POSITION_CONVERSION, "", param);
        }

        /// <summary>Converts network x/y to longitude/latitude (degrees).</summary>
        public TraCIPosition XYToLonLat(double x, double y)
        {
            var param = new TraCIWriter().Compound(2)
                .UByte(POSITION_2D).Double(x).Double(y)
                .TypedUByte(POSITION_LON_LAT);
            return (TraCIPosition)GetVariable(CMD_GET_SIM_VARIABLE, POSITION_CONVERSION, "", param);
        }

        List<string> GetStringList(byte cmd, byte var, string id) => (List<string>)GetVariable(cmd, var, id);

        object GetVariable(byte cmd, byte var, string id, TraCIWriter param = null)
        {
            var w = new TraCIWriter().UByte(var).String(id);
            if (param != null) w.Bytes(param.ToArray());
            var r = SendCommand(cmd, w);
            r.CommandLength();
            r.UByte(); // response id
            r.UByte(); // variable id
            r.String(); // object id
            return r.Typed();
        }

        // ------------------------------------------------------------------ external (ego) vehicles

        /// <summary>
        /// Adds a vehicle that is driven from Unity (a participant's car or the AV) so SUMO traffic reacts to it.
        /// It is placed with <see cref="MoveToXY"/> every step and ignored in <see cref="Vehicles"/>.
        /// </summary>
        public void AddExternalVehicle(string id, string typeId = "DEFAULT_VEHTYPE", string routeEdge = null)
        {
            IgnoredVehicles.Add(id);
            if (string.IsNullOrEmpty(routeEdge))
            {
                foreach (var e in GetEdgeIds())
                    if (!e.StartsWith(":")) { routeEdge = e; break; }
            }
            string routeId = "coupledsim_route_" + id;
            Set(CMD_SET_ROUTE_VARIABLE, ADD, routeId, new TraCIWriter().TypedStringList(new[] { routeEdge }));
            var w = new TraCIWriter().Compound(14)
                .TypedString(routeId).TypedString(typeId).TypedString("now")
                .TypedString("first").TypedString("base").TypedString("0")
                .TypedString("current").TypedString("max").TypedString("current")
                .TypedString("").TypedString("").TypedString("")
                .TypedInt(0).TypedInt(0);
            Set(CMD_SET_VEHICLE_VARIABLE, ADD_FULL, id, w);
            // Ignore SUMO's own safety checks and lane changing: Unity is in control.
            Set(CMD_SET_VEHICLE_VARIABLE, VAR_SPEEDSETMODE, id, new TraCIWriter().TypedInt(0));
            Set(CMD_SET_VEHICLE_VARIABLE, VAR_LANECHANGE_MODE, id, new TraCIWriter().TypedInt(0));
        }

        /// <summary>
        /// Places a vehicle at network position x/y (front bumper) with heading angle (deg, 0 = north, clockwise).
        /// keepRoute: 0 = stay on route, 1 = any edge, 2 = free (may leave the road).
        /// </summary>
        public void MoveToXY(string id, double x, double y, double angle, int keepRoute = 2, string edgeId = "", int lane = -1)
        {
            var w = new TraCIWriter().Compound(6)
                .TypedString(edgeId).TypedInt(lane).TypedDouble(x).TypedDouble(y).TypedDouble(angle).TypedByte((sbyte)keepRoute);
            Set(CMD_SET_VEHICLE_VARIABLE, MOVE_TO_XY, id, w);
        }

        public void SetSpeed(string id, double speed) =>
            Set(CMD_SET_VEHICLE_VARIABLE, VAR_SPEED, id, new TraCIWriter().TypedDouble(speed));

        public void SetColor(string id, byte r, byte g, byte b, byte a = 255) =>
            Set(CMD_SET_VEHICLE_VARIABLE, VAR_COLOR, id, new TraCIWriter().TypedColor(r, g, b, a));

        public void RemoveVehicle(string id)
        {
            Set(CMD_SET_VEHICLE_VARIABLE, REMOVE, id, new TraCIWriter().TypedByte(2));
            IgnoredVehicles.Remove(id);
            Vehicles.Remove(id);
        }

        void Set(byte cmd, byte var, string id, TraCIWriter value)
        {
            var w = new TraCIWriter().UByte(var).String(id).Bytes(value.ToArray());
            SendCommand(cmd, w);
        }

        // ------------------------------------------------------------------ transport

        /// <summary>Sends one command and returns a reader positioned after its status response.</summary>
        TraCIReader SendCommand(byte cmdId, TraCIWriter content)
        {
            if (_stream == null) throw new TraCIException("Not connected to SUMO");
            var body = content.ToArray();
            var cmd = new TraCIWriter();
            int len = body.Length + 2;
            if (len <= 255) cmd.UByte((byte)len).UByte(cmdId);
            else cmd.UByte(0).Int(body.Length + 6).UByte(cmdId);
            cmd.Bytes(body);
            var cmdBytes = cmd.ToArray();
            var msg = new TraCIWriter().Int(cmdBytes.Length + 4).Bytes(cmdBytes).ToArray();
            _stream.Write(msg, 0, msg.Length);

            var header = ReadExact(4);
            int total = new TraCIReader(header).Int();
            var data = ReadExact(total - 4);
            var r = new TraCIReader(data);
            // status response: length, command id, result, description
            r.CommandLength();
            byte respCmd = r.UByte();
            byte result = r.UByte();
            string description = r.String();
            if (respCmd != cmdId)
                throw new TraCIException($"TraCI: status for 0x{respCmd:x2}, expected 0x{cmdId:x2}");
            if (result != RTYPE_OK)
                throw new TraCIException($"SUMO error on command 0x{cmdId:x2}: {description}");
            return r;
        }

        byte[] ReadExact(int n)
        {
            var buf = new byte[n];
            int read = 0;
            while (read < n)
            {
                int k = _stream.Read(buf, read, n - read);
                if (k <= 0) throw new IOException("SUMO closed the TraCI connection");
                read += k;
            }
            return buf;
        }

        /// <summary>Ends the simulation and closes the connection.</summary>
        public void Close()
        {
            if (_stream != null)
            {
                try { SendCommand(CMD_CLOSE, new TraCIWriter()); } catch (Exception) { }
            }
            Dispose();
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _tcp?.Dispose();
            _stream = null;
            _tcp = null;
        }
    }
}
