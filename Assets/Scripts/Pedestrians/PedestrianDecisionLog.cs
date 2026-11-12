using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CoupledSim.Pedestrians;
using UnityEngine;

/// <summary>
/// Writes crossing decisions of AI pedestrians to ExperimentLogs/PedestrianDecisions-&lt;date&gt;.csv
/// (one file per run): when a pedestrian reaches the kerb, starts crossing and has crossed,
/// with the waiting time and the smallest time-to-arrival of the approaching vehicles.
/// </summary>
public static class PedestrianDecisionLog
{
    static StreamWriter _writer;

    public static void Write(AIPedestrian pedestrian, string evt, GapAcceptanceModel model, List<ApproachingVehicle> vehicles)
    {
        try
        {
            if (_writer == null)
            {
                Directory.CreateDirectory("ExperimentLogs");
                var path = "ExperimentLogs/PedestrianDecisions-" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv";
                _writer = new StreamWriter(path) { AutoFlush = true };
                _writer.WriteLine("time,pedestrian,event,phase,waiting_time,vehicles,min_tta,yielding_vehicles,critical_gap,walking_speed,reason");
                Application.quitting += Close;
            }
            float minTta = float.PositiveInfinity;
            int yielding = 0;
            foreach (var v in vehicles)
            {
                var a = model.Assess(v, model.WaitingTime);
                minTta = Mathf.Min(minTta, a.TimeToArrival);
                if (a.Yielding) yielding++;
            }
            var ci = CultureInfo.InvariantCulture;
            _writer.WriteLine(string.Join(",",
                Time.time.ToString("F3", ci),
                Escape(pedestrian.name),
                evt,
                model.Phase,
                model.WaitingTime.ToString("F2", ci),
                vehicles.Count,
                float.IsInfinity(minTta) ? "" : minTta.ToString("F2", ci),
                yielding,
                model.Profile.CriticalGap.ToString("F2", ci),
                model.Profile.WalkingSpeed.ToString("F2", ci),
                Escape(model.Reason)));
        }
        catch (IOException e)
        {
            Debug.LogWarning("PedestrianDecisionLog: " + e.Message);
        }
    }

    static string Escape(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";

    static void Close()
    {
        _writer?.Dispose();
        _writer = null;
    }
}
