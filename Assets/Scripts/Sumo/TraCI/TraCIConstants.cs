// TraCI protocol constants used by the coupled simulator.
// Values from SUMO's src/libsumo/TraCIConstants.h (see https://sumo.dlr.de/docs/TraCI/Protocol.html).
namespace CoupledSim.TraCI
{
    public static class TraCIConstants
    {
        // Control commands
        public const byte CMD_GETVERSION = 0x00;
        public const byte CMD_SIMSTEP = 0x02;
        public const byte CMD_SETORDER = 0x03;
        public const byte CMD_CLOSE = 0x7F;

        // Get / set / subscribe commands per domain
        public const byte CMD_GET_VEHICLE_VARIABLE = 0xa4;
        public const byte RESPONSE_GET_VEHICLE_VARIABLE = 0xb4;
        public const byte CMD_SET_VEHICLE_VARIABLE = 0xc4;
        public const byte CMD_SUBSCRIBE_VEHICLE_VARIABLE = 0xd4;
        public const byte RESPONSE_SUBSCRIBE_VEHICLE_VARIABLE = 0xe4;

        public const byte CMD_GET_EDGE_VARIABLE = 0xaa;
        public const byte RESPONSE_GET_EDGE_VARIABLE = 0xba;

        public const byte CMD_GET_SIM_VARIABLE = 0xab;
        public const byte RESPONSE_GET_SIM_VARIABLE = 0xbb;
        public const byte CMD_SUBSCRIBE_SIM_VARIABLE = 0xdb;
        public const byte RESPONSE_SUBSCRIBE_SIM_VARIABLE = 0xeb;

        public const byte CMD_SET_ROUTE_VARIABLE = 0xc6;

        public const byte CMD_GET_PERSON_VARIABLE = 0xae;
        public const byte CMD_SET_PERSON_VARIABLE = 0xce;
        public const byte CMD_SUBSCRIBE_PERSON_VARIABLE = 0xde;
        public const byte RESPONSE_SUBSCRIBE_PERSON_VARIABLE = 0xee;

        // Result codes
        public const byte RTYPE_OK = 0x00;
        public const byte RTYPE_NOTIMPLEMENTED = 0x01;
        public const byte RTYPE_ERR = 0xFF;

        // Data types
        public const byte POSITION_LON_LAT = 0x00;
        public const byte POSITION_2D = 0x01;
        public const byte POSITION_LON_LAT_ALT = 0x02;
        public const byte POSITION_3D = 0x03;
        public const byte POSITION_ROADMAP = 0x04;
        public const byte TYPE_BOUNDINGBOX = 0x05;
        public const byte TYPE_POLYGON = 0x06;
        public const byte TYPE_UBYTE = 0x07;
        public const byte TYPE_BYTE = 0x08;
        public const byte TYPE_INTEGER = 0x09;
        public const byte TYPE_DOUBLE = 0x0B;
        public const byte TYPE_STRING = 0x0C;
        public const byte TYPE_STRINGLIST = 0x0E;
        public const byte TYPE_COMPOUND = 0x0F;
        public const byte TYPE_DOUBLELIST = 0x10;
        public const byte TYPE_COLOR = 0x11;

        // Variables
        public const byte TRACI_ID_LIST = 0x00;
        public const byte VAR_SLOPE = 0x36;
        public const byte VAR_POSITION3D = 0x39;
        public const byte VAR_SPEED = 0x40;
        public const byte VAR_POSITION = 0x42;
        public const byte VAR_ANGLE = 0x43;
        public const byte VAR_LENGTH = 0x44;
        public const byte VAR_COLOR = 0x45;
        public const byte VAR_VEHICLECLASS = 0x49;
        public const byte VAR_WIDTH = 0x4d;
        public const byte VAR_TYPE = 0x4f;
        public const byte VAR_SIGNALS = 0x5b;
        public const byte VAR_TIME = 0x66;
        public const byte VAR_DEPARTED_VEHICLES_IDS = 0x74;
        public const byte VAR_ARRIVED_VEHICLES_IDS = 0x7a;
        public const byte VAR_DELTA_T = 0x7b;
        public const byte VAR_NET_BOUNDING_BOX = 0x7c;
        public const byte VAR_MIN_EXPECTED_VEHICLES = 0x7d;
        public const byte ADD = 0x80;
        public const byte REMOVE = 0x81;
        public const byte POSITION_CONVERSION = 0x82;
        public const byte ADD_FULL = 0x85;
        public const byte VAR_SPEEDSETMODE = 0xb3;
        public const byte MOVE_TO_XY = 0xb4;
        public const byte VAR_LANECHANGE_MODE = 0xb6;

        // Subscription begin/end meaning "whole simulation"
        public const double INVALID_DOUBLE_VALUE = -1073741824.0;
        public const double MAX_TIME = 1.7976931348623157e308;
    }
}
