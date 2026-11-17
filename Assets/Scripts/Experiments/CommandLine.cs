using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// The agent's command-line options, plus the options of the current trial on the host (an experiment file's first
/// participant, e.g. "-driverCar ToyotaCorolla -autopilot"; trial parameter "args"). The trial's options come first,
/// so they win over the same option on the real command line. Options read when a trial's level loads (vehicle,
/// autopilot, in-car displays, eHMI, sound, ...) follow the trial; options read once at start (-quality, -trials) do not.
/// </summary>
public static class CommandLine
{
    static string[] _trial = new string[0];
    static string[] _cached;

    public static string[] Args
    {
        get
        {
            if (_cached != null) return _cached;
            var real = Environment.GetCommandLineArgs();
            if (_trial.Length == 0) return _cached = real;
            var all = new List<string>(real.Length + _trial.Length);
            if (real.Length > 0) all.Add(real[0]);
            all.AddRange(_trial);
            for (int i = 1; i < real.Length; i++) all.Add(real[i]);
            return _cached = all.ToArray();
        }
    }

    /// <summary>The current trial's options (null or empty: none).</summary>
    public static void SetTrialOptions(string options)
    {
        _trial = Split(options);
        _cached = null;
    }

    public static string TrialOptions => string.Join(" ", _trial);

    /// <summary>Splits like a shell: spaces separate, "double quotes" group.</summary>
    public static string[] Split(string text)
    {
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return parts.ToArray();
        var sb = new StringBuilder(); bool quoted = false, any = false;
        foreach (var c in text)
        {
            if (c == '"') { quoted = !quoted; any = true; continue; }
            if (char.IsWhiteSpace(c) && !quoted) { if (any) parts.Add(sb.ToString()); sb.Clear(); any = false; continue; }
            sb.Append(c); any = true;
        }
        if (any) parts.Add(sb.ToString());
        return parts.ToArray();
    }
}
