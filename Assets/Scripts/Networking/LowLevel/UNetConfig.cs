public class UNetConfig
{
    /// <summary>The UDP port of host and clients: 7770, or -netPort N on the command line (smoke tests of two
    /// project copies run side by side on one machine).</summary>
    public static ushort Port => _port ??= ReadPort();
    static ushort? _port;
    static ushort ReadPort()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-netPort");
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var p) && p > 0 && p < 65536 ? (ushort)p : (ushort)7770;
    }
    public const int MaxPlayers = 8;
    public const int MaxHostConnections = MaxPlayers-1;
    public const int SendBufferSize = 256 * 1024;
}
