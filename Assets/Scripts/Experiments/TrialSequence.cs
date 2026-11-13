using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CoupledSim.Experiments;
using UnityEngine;

/// <summary>
/// Trial sequences from files (CSV or JSON) instead of the list in StartScene, ordered per
/// participant (counterbalancing). CSV columns, one row per trial:
///   experiment  index, ShortName, Name or prefab name of a registered experiment (required)
///   role        index or role name (default 0)
///   input       Flat / VR / Suite (default: PlayerSystem's)
///   block       counterbalancing block; rows without a block before the first block run first
///   recordingStart, recordingDuration   seconds, for video recording
///   any other column: an experiment parameter (weather, time, map, animals, traffic, ...);
///   empty cells are left out.
/// JSON: {"order":"latin","trials":[{"experiment":"Base","role":"Driver","block":"A",
///   "parameters":[{"name":"weather","value":"LightRain"}]}]}
/// </summary>
public static class TrialSequence
{
    static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "experiment", "role", "input", "block", "recordingStart", "recordingDuration", "comment", "notes" };

    [Serializable]
    class JsonFile
    {
        public string order;
        public JsonTrial[] trials;
    }

    [Serializable]
    class JsonTrial
    {
        public string experiment, role, input, block;
        public float recordingStart, recordingDuration;
        public NetworkingManager.ExperimentParameter[] parameters;
    }

    /// <summary>Path as given, else relative to StreamingAssets/Trials or StreamingAssets.</summary>
    public static string Resolve(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var p in new[] { path, Path.Combine(Application.streamingAssetsPath, "Trials", path), Path.Combine(Application.streamingAssetsPath, path) })
            if (File.Exists(p)) return Path.GetFullPath(p);
        return null;
    }

    /// <summary>Reads a trial file into a table (JSON is converted to the CSV columns).</summary>
    public static TrialTable ReadTable(string path)
    {
        var text = File.ReadAllText(path);
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return TrialTable.ParseCsv(text);
        var json = JsonUtility.FromJson<JsonFile>(text);
        var table = new TrialTable();
        if (json.order != null && TrialTable.TryParseOrder(json.order, out var o)) table.Order = o;
        table.Columns.AddRange(new[] { "experiment", "role", "input", "block", "recordingStart", "recordingDuration" });
        foreach (var t in json.trials ?? new JsonTrial[0])
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["experiment"] = t.experiment ?? "", ["role"] = t.role ?? "", ["input"] = t.input ?? "", ["block"] = t.block ?? "",
                ["recordingStart"] = t.recordingStart.ToString(CultureInfo.InvariantCulture),
                ["recordingDuration"] = t.recordingDuration.ToString(CultureInfo.InvariantCulture),
            };
            foreach (var p in t.parameters ?? new NetworkingManager.ExperimentParameter[0])
            {
                if (!table.Columns.Contains(p.name)) table.Columns.Add(p.name);
                row[p.name] = p.value;
            }
            table.Rows.Add(row);
        }
        return table;
    }

    public static int FindExperiment(ExperimentDefinition[] experiments, string key)
    {
        if (experiments == null || string.IsNullOrEmpty(key)) return -1;
        if (int.TryParse(key, out var i)) return i >= 0 && i < experiments.Length ? i : -1;
        for (int k = 0; k < experiments.Length; k++)
        {
            var e = experiments[k];
            if (e == null) continue;
            if (string.Equals(e.ShortName, key, StringComparison.OrdinalIgnoreCase) || string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.gameObject.name, key, StringComparison.OrdinalIgnoreCase)) return k;
        }
        return -1;
    }

    public static int FindRole(ExperimentDefinition exp, string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        var roles = exp.Roles ?? new ExperimentRoleDefinition[0];
        if (int.TryParse(key, out var i)) return i >= 0 && i < roles.Length ? i : -1;
        for (int k = 0; k < roles.Length; k++)
            if (string.Equals(roles[k].Name, key, StringComparison.OrdinalIgnoreCase)) return k;
        return -1;
    }

    /// <summary>Converts the table to trials in the participant's order; problems go to <paramref name="errors"/>.</summary>
    public static NetworkingManager.Trial[] ToTrials(TrialTable table, ExperimentDefinition[] experiments, int participant, TrialOrder order,
        PlayerSystem.InputMode defaultInput, List<string> errors)
    {
        var trials = new List<NetworkingManager.Trial>();
        foreach (var r in table.OrderFor(participant, order))
        {
            int line = r + 1;
            var expKey = table.Get(r, "experiment");
            int ei = FindExperiment(experiments, expKey);
            if (ei < 0) { errors.Add($"trial {line}: unknown experiment '{expKey}'"); continue; }
            int ri = FindRole(experiments[ei], table.Get(r, "role"));
            if (ri < 0) { errors.Add($"trial {line}: experiment '{expKey}' has no role '{table.Get(r, "role")}'"); continue; }
            var input = defaultInput;
            var inputKey = table.Get(r, "input");
            if (inputKey.Length > 0 && !Enum.TryParse(inputKey, true, out input)) { errors.Add($"trial {line}: unknown input '{inputKey}' (Flat, VR, Suite)"); input = defaultInput; }
            var parameters = new List<NetworkingManager.ExperimentParameter>();
            foreach (var c in table.Columns)
            {
                if (Reserved.Contains(c)) continue;
                var v = table.Get(r, c);
                if (v.Length > 0) parameters.Add(new NetworkingManager.ExperimentParameter { name = c, value = v });
            }
            trials.Add(new NetworkingManager.Trial
            {
                experimentIndex = ei,
                roleIndex = ri,
                InputMode = input,
                experimentParameters = parameters.ToArray(),
                recordingStartTime = TrialTable.ParseFloat(table.Get(r, "recordingStart"), 0f),
                recordingDuration = TrialTable.ParseFloat(table.Get(r, "recordingDuration"), 0f),
            });
        }
        return trials.ToArray();
    }

    /// <summary>Loads a file: trials in the participant's order (<paramref name="order"/>, else the file's "#order", else <paramref name="fallback"/>).</summary>
    public static NetworkingManager.Trial[] Load(string path, ExperimentDefinition[] experiments, int participant, TrialOrder? order, TrialOrder fallback,
        PlayerSystem.InputMode defaultInput, List<string> errors)
    {
        var full = Resolve(path);
        if (full == null) { errors.Add($"trial file '{path}' not found (also looked in StreamingAssets/Trials)"); return null; }
        try
        {
            var table = ReadTable(full);
            return ToTrials(table, experiments, participant, order ?? table.Order ?? fallback, defaultInput, errors);
        }
        catch (Exception e)
        {
            errors.Add($"trial file '{path}': {e.Message}");
            return null;
        }
    }

    public static string Describe(NetworkingManager.Trial t, ExperimentDefinition[] experiments)
    {
        var e = t.experimentIndex >= 0 && experiments != null && t.experimentIndex < experiments.Length ? experiments[t.experimentIndex] : null;
        var role = e != null && e.Roles != null && t.roleIndex >= 0 && t.roleIndex < e.Roles.Length ? e.Roles[t.roleIndex].Name : t.roleIndex.ToString();
        var ps = t.experimentParameters == null ? "" : string.Join(" ", t.experimentParameters.Select(p => p.name + "=" + p.value));
        return $"{(e != null ? e.ShortName : "?")} / {role} {ps}".Trim();
    }
}
