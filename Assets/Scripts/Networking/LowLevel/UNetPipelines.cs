using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

/// <summary>
/// The network driver and pipelines of host and client (both must create them in the same order).
/// Messages larger than one packet (about 1.4 KB, e.g. the skeleton pose of a pedestrian) are
/// fragmented; without that, Unity Transport drops them silently.
/// </summary>
public static class UNetPipelines
{
    /// <summary>Largest message (bytes).</summary>
    public const int MaxMessageSize = 64 * 1024;

    public static NetworkDriver CreateDriver(out NetworkPipeline unreliable, out NetworkPipeline reliable)
    {
        var settings = new NetworkSettings();
        settings.WithFragmentationStageParameters(payloadCapacity: MaxMessageSize);
        settings.WithReliableStageParameters(windowSize: 64);
        var driver = NetworkDriver.Create(settings);
        unreliable = driver.CreatePipeline(typeof(FragmentationPipelineStage));
        reliable = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        return driver;
    }

    static int _reported;

    /// <summary>Logs failed sends (a few times), e.g. a message larger than MaxMessageSize.</summary>
    public static void Check(int status, int size)
    {
        if (status >= 0 || _reported >= 5) return;
        _reported++;
        Debug.LogWarning($"Network: sending a message of {size} bytes failed ({(Unity.Networking.Transport.Error.StatusCode)status})");
    }
}
