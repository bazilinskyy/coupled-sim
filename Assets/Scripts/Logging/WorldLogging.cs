using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class WorldLogger
{
    StreetLightsSystem _lights;
    PlayerSystem _playerSystem;
    BinaryWriter _fileWriter;
    float _startTime;

    public WorldLogger(PlayerSystem playerSys)
    {
        _playerSystem = playerSys;
    }

    List<PlayerAvatar> _driverBuffer = new List<PlayerAvatar>();
    public void BeginLog(string fileName, ExperimentDefinition experiment, StreetLightsSystem lights)
    {
        _lights = lights;
        if (!Directory.Exists("ExperimentLogs"))
        {
            Directory.CreateDirectory("ExperimentLogs");
        }
        _fileWriter = new BinaryWriter(File.Create("ExperimentLogs/" + fileName + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".binLog"));
        _startTime = Time.realtimeSinceStartup;
        _fileWriter.Write(DateTime.Now.ToBinary());
        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Drivers);
        _driverBuffer.AddRange(_playerSystem.Passengers);
        _fileWriter.Write(_driverBuffer.Count);
        _fileWriter.Write(_playerSystem.Pedestrians.Count);
        _fileWriter.Write(experiment.PointsOfInterest.Length);
        foreach (var poi in experiment.PointsOfInterest)
        {
            _fileWriter.Write(poi.name);
            _fileWriter.Write(poi.position);
            _fileWriter.Write(poi.rotation);
        }
        if (_lights != null)
        {
            _fileWriter.Write(_lights.CarLights.Length);
            foreach (var light in _lights.CarLights)
            {
                _fileWriter.Write(GetHierarchyString(light.transform));
            }
            _fileWriter.Write(_lights.PedestrianLights.Length);
            foreach (var light in _lights.PedestrianLights)
            {
                _fileWriter.Write(GetHierarchyString(light.transform));
            }
        }
        else
        {
            _fileWriter.Write(0);
            _fileWriter.Write(0);
        }
    }

    string GetHierarchyString(Transform trans)
    {
        List<string> names = new List<string>();
        while (trans != null)
        {
            names.Add(trans.name);
            trans = trans.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }


    public void LogFrame()
    {
        // We write time directly first, then all other values are written as ",val" by the LogWriter
        _fileWriter.Write(Time.realtimeSinceStartup - _startTime);

        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Drivers);
        _driverBuffer.AddRange(_playerSystem.Passengers);
        foreach (var driver in _driverBuffer)
        {
            _fileWriter.Write(driver.transform.position);
            _fileWriter.Write(driver.transform.rotation);
            _fileWriter.Write((int)driver._carBlinkers.State);
        }
        foreach (var pedestrian in _playerSystem.Pedestrians)
        {
            pedestrian.GetPose().SerializeTo(_fileWriter);
        }
        if (_lights != null)
        {
            foreach (var light in _lights.CarLights)
            {
                _fileWriter.Write((byte)light.State);
            }
            foreach (var light in _lights.PedestrianLights)
            {
                _fileWriter.Write((byte)light.State);
            }
        }
    }
    public void EndLog()
    {
        // TODO(jacek): for now we just call EndLog in NetworkingManager.OnDestroy, so we close the log when the game finishes
        // Remove that call and this null check when we have proper "End experiment" handling
        if (_fileWriter != null)
        {
            _fileWriter.Dispose();
        }
    }
}

public class LogConverter
{
    public struct SerializedPOI
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;

        public override string ToString()
        {
            var rot = Rotation.eulerAngles;
            return $"{Name};{Position.x};{Position.y};{Position.z};{rot.x};{rot.y};{rot.z}";
        }
    }
    static SerializedPOI[] ParsePOI(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var pois = new SerializedPOI[count];
        for (int i = 0; i < count; i++)
        {
            pois[i].Name = reader.ReadString();
            pois[i].Position = reader.ReadVector3();
            pois[i].Rotation = reader.ReadQuaternion();
        }
        return pois;
    }
    public static SerializedPOI[] GetPOIs(string sourceFile)
    {
        using (var reader = new BinaryReader(File.OpenRead(sourceFile)))
        {
            var unusedTimestamp = reader.ReadInt64();
            var unusedDriverCount = reader.ReadInt32();
            var unusedPedestrianCount = reader.ReadInt32();
            return ParsePOI(reader);
        }
    }

    public static void TranslateBinaryLogToCsv(string sourceFile, string dstFile, string[] pedestrianSkeletonNames, string referenceName, Vector3 referencePos, Quaternion referenceRot)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        const string separator = ";";
        const int columnsPerDriver = 7;
        const int columnsPerBone = 6;
        int columnsPerPedestrian = pedestrianSkeletonNames.Length * columnsPerBone + columnsPerBone; // + columnsPerBone is for the root transform;
        var toRefRot = Quaternion.Inverse(referenceRot);

        var srcFile = File.OpenRead(sourceFile);
        using (var reader = new BinaryReader(srcFile))
        using (var writer = new StreamWriter(File.Create(dstFile)))
        {
            writer.WriteLine($"Root;{referenceName}");
            var startTime = DateTime.FromBinary(reader.ReadInt64());
            var startString = startTime.ToString("HH:mm:ss") + ":" + startTime.Millisecond.ToString();
            writer.WriteLine($"Start Time;{startString}");
            var numDrivers = reader.ReadInt32();
            var numPedestrians = reader.ReadInt32();

            // POI
            var pois = new List<SerializedPOI>(ParsePOI(reader));
            pois.Add(new SerializedPOI()
            {
                Name = "World Root",
                Position = Vector3.zero,
                Rotation = Quaternion.identity
            });
            Vector3 PosToRefPoint(Vector3 p) => toRefRot * (p - referencePos);
            Quaternion RotToRefPoint(Quaternion q) => toRefRot * q;
            for (int i = 0; i < pois.Count; i++)
            {
                var poi = pois[i];
                poi.Position = PosToRefPoint(poi.Position);
                poi.Rotation = RotToRefPoint(poi.Rotation);
                writer.WriteLine(poi);
            }

            int numCarLights = reader.ReadInt32();
            List<string> carLights = new List<string>();
            for (int i = 0; i < numCarLights; i++)
            {
                carLights.Add(reader.ReadString());
            }
            int numPedestrianLights = reader.ReadInt32();
            List<string> pedestrianLights = new List<string>();
            for (int i = 0; i < numPedestrianLights; i++)
            {
                pedestrianLights.Add(reader.ReadString());
            }

            //****************
            // HEADER ROW
            //****************

            writer.Write("Timestamp;");

            // Drivers header
            for (int i = 0; i < numDrivers; i++)
            {
                writer.Write(string.Join(separator, Enumerable.Repeat($"Driver{i}", columnsPerDriver)));
                if (i < numDrivers - 1)
                {
                    writer.Write(separator);
                }
            }
            if (numPedestrians > 0)
            {
                writer.Write(separator);
            }
            for (int i = 0; i < numPedestrians; i++)
            {
                var pedestrianName = $"Pedestrian{i}";
                writer.Write(string.Join(separator, Enumerable.Repeat(pedestrianName, columnsPerPedestrian)));
            }
            if (numCarLights > 0)
            {
                writer.Write(separator);
                writer.Write(string.Join(separator, carLights));
            }
            if (numPedestrianLights > 0)
            {
                writer.Write(separator);
                writer.Write(string.Join(separator, pedestrianLights));
            }
            writer.Write("\n");

            // No bone names for drivers
            writer.Write(separator); // for the Timestamp column
            writer.Write(string.Join(separator, new string(';', numDrivers * columnsPerDriver)));
            var sb = new StringBuilder();
            sb.Append(string.Join(separator, Enumerable.Repeat("Root", columnsPerBone)));
            sb.Append(";");
            for (int i = 0; i < pedestrianSkeletonNames.Length; i++)
            {
                sb.Append(string.Join(separator, Enumerable.Repeat(pedestrianSkeletonNames[i], columnsPerBone)));
                if (i < pedestrianSkeletonNames.Length - 1)
                {
                    sb.Append(separator);
                }
            }
            for (int i = 0; i < numPedestrians; i++)
            {
                writer.Write(sb.ToString());
            }

            writer.Write("\n");

            writer.Write(separator); // for the Timestamp column

            const string driverTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;blinkers";
            writer.Write(string.Join(separator, Enumerable.Repeat(driverTransformHeader, numDrivers)));
            if (numPedestrians > 0)
            {
                writer.Write(separator);
            }
            const string boneTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z";
            writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians * (pedestrianSkeletonNames.Length + 1))));

            writer.Write("\n");
            List<string> line = new List<string>();
            while (srcFile.Position < srcFile.Length)
            {
                line.Clear();
                line.Add(reader.ReadSingle().ToString());
                for (int i = 0; i < numDrivers; i++)
                {
                    var pos = PosToRefPoint(reader.ReadVector3());
                    var rot = RotToRefPoint(reader.ReadQuaternion()).eulerAngles;
                    var blinkers = reader.ReadInt32();
                    line.Add($"{pos.x};{pos.y};{pos.z};{rot.x};{rot.y};{rot.z};{(BlinkerState)blinkers}");
                }
                for (int i = 0; i < numPedestrians; i++)
                {
                    var pos = reader.ReadListVector3();
                    var rot = reader.ReadListQuaternion();
                    var unused = reader.ReadInt32();
                    for (int j = 0; j < pos.Count; j++)
                    {
                        var p = PosToRefPoint(pos[j]);
                        var r = RotToRefPoint(rot[j]).eulerAngles;
                        line.Add($"{p.x};{p.y};{p.z};{r.x};{r.y};{r.z}");
                    }
                }
                for (int i = 0; i < numCarLights; i++)
                {
                    line.Add(((LightState)reader.ReadByte()).ToString());
                }
                for (int i = 0; i < numPedestrianLights; i++)
                {
                    line.Add(((LightState)reader.ReadByte()).ToString());
                }
                writer.Write(string.Join(separator, line));
                writer.Write("\n");
            }
        }
    }

    public LogConverter(PlayerAvatar pedestrianPrefab)
    {
        var transforms = pedestrianPrefab.SyncTransforms;
        _pedestrianSkeletonNames = new string[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            _pedestrianSkeletonNames[i] = transforms[i].name;
        }
    }

    string[] _pedestrianSkeletonNames;
    bool _open;
    List<string> _fileNames = new List<string>();
    string _selectedFileName = "";
    SerializedPOI[] _pois;

    public void OnGUI()
    {
        if (!_open && GUILayout.Button("Convert log to csv"))
        {
            var files = Directory.GetFiles("ExperimentLogs/");
            foreach (var file in files)
            {
                if (file.EndsWith("binLog"))
                {
                    _fileNames.Add(file.Split('/')[1]);
                }
            }
            _open = true;
        }
        if (_open)
        {
            if (GUILayout.Button("Close"))
            {
                _open = false;
            }
            foreach (var name in _fileNames)
            {
                if (GUILayout.Button(name))
                {
                    _selectedFileName = name;
                    _pois = GetPOIs("ExperimentLogs/" + name);
                }
                if (_selectedFileName == name)
                {
                    GUILayout.Label("Reference Point:");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("World root"))
                    {
                        var fullName = "ExperimentLogs/" + name;
                        var csvName = "ExperimentLogs/" + "World Root-" + name.Replace("binLog", "csv");
                        TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, "World Root", default, Quaternion.identity);
                    }
                    foreach (var poi in _pois)
                    {
                        if (GUILayout.Button(poi.Name))
                        {
                            var fullName = "ExperimentLogs/" + name;
                            var csvName = "ExperimentLogs/" + poi.Name + "-" + name.Replace("binLog", "csv");
                            TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, poi.Name, poi.Position, poi.Rotation);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}
