// Decompiled with JetBrains decompiler
// Type: TSolesPlugin.Traceability
// Assembly: TSolesPlugin, Version=3.4.1.0, Culture=neutral, PublicKeyToken=null
// MVID: 943EFD72-12B6-4D53-BB0D-FD8C4C742C73
// Assembly location: D:\opt directory (T-Soles)\dsf\bin\TSolesPlugin.dll

using DuetAPIClient;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TSolesPlugin.IPC;

#nullable enable
namespace TSolesPlugin
{
    public static class Traceability
    {
        private const string FilamentSerialFile_Deprecated = "/opt/dsf/sd/sys/filamentSerials.json";
        private const string VariableStorageFile = "/opt/dsf/sd/sys/variables.json";
        private static readonly AsyncLock _lock = new AsyncLock();
        private const string PrintsCsvFile = "/opt/dsf/sd/sys/prints.csv";
        private const string PrintsCsvHeader = "\"datetime\",\"filename\",\"filament\",\"filamentSerial\"";

        public static bool EnableBroadcast { get; set; }

        public static bool EnableFilamentTracking { get; set; }

        public static string LeftFilamentSerial { get; set; } = string.Empty;

        public static string RightFilamentSerial { get; set; } = string.Empty;

        public static float Babystep { get; set; } = 1f;

        public static string MachineSerial { get; set; } = string.Empty;

        public static async Task Init()
        {
            if (!File.Exists("/opt/dsf/sd/sys/variables.json") && File.Exists("/opt/dsf/sd/sys/filamentSerials.json"))
                File.Move("/opt/dsf/sd/sys/filamentSerials.json", "/opt/dsf/sd/sys/variables.json");
            if (!File.Exists("/opt/dsf/sd/sys/variables.json"))
                return;
            try
            {
                Traceability.VariableStorage storage;
                await using (FileStream filamentSerialFile = new FileStream("/opt/dsf/sd/sys/variables.json", FileMode.Open, FileAccess.Read))
                {
                    storage = await JsonSerializer.DeserializeAsync<Traceability.VariableStorage>((Stream)filamentSerialFile);
                    if (storage != null)
                    {
                        using (await Traceability._lock.LockAsync(Program.CancelSource.Token))
                        {
                            Traceability.EnableFilamentTracking = storage.EnableFilamentTracking;
                            Traceability.LeftFilamentSerial = storage.LeftFilamentSerial;
                            Traceability.RightFilamentSerial = storage.RightFilamentSerial;
                            Traceability.Babystep = storage.Babystep;
                            Traceability.MachineSerial = storage.MachineSerial;
                            Console.WriteLine("[info] Variables loaded, enabled = {0}, left = '{1}', right = '{2}', babystep = {3}, machineSerial = '{4}'", (object)Traceability.EnableFilamentTracking, (object)Traceability.LeftFilamentSerial, (object)Traceability.RightFilamentSerial, (object)Traceability.Babystep, (object)Traceability.MachineSerial);
                        }
                    }
                }
                storage = (Traceability.VariableStorage)null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[warn] Failed to load serial numbers from storage file: {0}", (object)ex.Message);
            }
        }

        public static async Task InitVariables()
        {
            do
            {
                try
                {
                    using (CommandConnection connection = new CommandConnection())
                    {
                        await connection.Connect(Program.SocketPath, Program.CancelSource.Token);
                        using (await Traceability._lock.LockAsync(Program.CancelSource.Token))
                        {
                            string str1 = await connection.PerformSimpleCode("global enableBroadcast=true");
                            string str2 = await connection.PerformSimpleCode("global enableFilamentTracking=" + (Traceability.EnableFilamentTracking ? "true" : "false"));
                            string str3 = await connection.PerformSimpleCode("global leftFilamentSerial=\"" + Traceability.LeftFilamentSerial + "\"");
                            string str4 = await connection.PerformSimpleCode("global rightFilamentSerial=\"" + Traceability.RightFilamentSerial + "\"");
                            CommandConnection commandConnection = connection;
                            //DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(9, 1);
                            //interpolatedStringHandler.AppendLiteral("M290 R0 Y");
                            //interpolatedStringHandler.AppendFormatted<float>(Traceability.Babystep);
                            //string stringAndClear = interpolatedStringHandler.ToStringAndClear();

                            string stringAndClear = "M290 R0 Y" + Traceability.Babystep.ToString("0.000");
                            CancellationToken cancellationToken = new CancellationToken();
                            string str5 = await commandConnection.PerformSimpleCode(stringAndClear, cancellationToken: cancellationToken);
                            string str6 = await connection.PerformSimpleCode("global machineSerial=\"" + Traceability.MachineSerial + "\"");
                            Console.WriteLine("[info] Variables initialized, enabled = {0}, left = '{1}', right = '{2}', babystep = {3}, serial = '{4}'", (object)Traceability.EnableFilamentTracking, (object)Traceability.LeftFilamentSerial, (object)Traceability.RightFilamentSerial, (object)Traceability.Babystep, (object)Traceability.MachineSerial);
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[warn] Failed to assign filament serial numbers: {0}", (object)ex.Message);
                    await Task.Delay(2000, Program.CancelSource.Token);
                }
            }
            while (!Program.CancelSource.IsCancellationRequested);
        }

        public static async Task<bool> MaintainVariables(
          bool enableBroadcast,
          bool enableFilamentTracking,
          string leftFilamentSerial,
          string rightFilamentSerial,
          float babystep,
          string machineSerial)
        {
            bool filamentSerialChanged = false;
            bool saveChanges = false;
            using (await Traceability._lock.LockAsync(Program.CancelSource.Token))
            {
                if (Traceability.EnableBroadcast != enableBroadcast)
                {
                    Console.WriteLine("[info] UDP broadcast {0} ", enableBroadcast ? (object)"enabled" : (object)"disabled");
                    Traceability.EnableBroadcast = enableBroadcast;
                }
                if (Traceability.EnableFilamentTracking != enableFilamentTracking)
                {
                    Console.WriteLine("[info] Filament tracking {0}", enableFilamentTracking ? (object)"enabled" : (object)"disabled");
                    Traceability.EnableFilamentTracking = enableFilamentTracking;
                    saveChanges = true;
                }
                if (Traceability.LeftFilamentSerial != leftFilamentSerial || Traceability.RightFilamentSerial != rightFilamentSerial)
                {
                    Console.WriteLine("[info] Serial numbers updated, left = '{0}', right = '{1}'", (object)leftFilamentSerial, (object)rightFilamentSerial);
                    filamentSerialChanged = Traceability.EnableFilamentTracking;
                    Traceability.LeftFilamentSerial = leftFilamentSerial;
                    Traceability.RightFilamentSerial = rightFilamentSerial;
                    saveChanges = true;
                }
                if ((double)Traceability.Babystep != (double)babystep)
                {
                    Console.WriteLine("[info] Babystepping changed to {0} mm", (object)babystep);
                    Traceability.Babystep = babystep;
                    saveChanges = true;
                }
                if (Traceability.MachineSerial != machineSerial)
                {
                    Console.WriteLine("[info] Machine serial number changed to '{0}'", (object)machineSerial);
                    Traceability.MachineSerial = machineSerial;
                    saveChanges = true;
                }
                if (saveChanges)
                {
                    Traceability.VariableStorage storage = new Traceability.VariableStorage()
                    {
                        EnableFilamentTracking = enableFilamentTracking,
                        LeftFilamentSerial = leftFilamentSerial,
                        RightFilamentSerial = rightFilamentSerial,
                        Babystep = babystep,
                        MachineSerial = machineSerial
                    };
                    try
                    {
                        await using (FileStream filamentSerialFile = new FileStream("/opt/dsf/sd/sys/variables.json", FileMode.Create, FileAccess.Write))
                            await JsonSerializer.SerializeAsync<Traceability.VariableStorage>((Stream)filamentSerialFile, storage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[warn] Failed to save variables to file: {0}", (object)ex.Message);
                    }
                    storage = (Traceability.VariableStorage)null;
                }
            }
            if (!filamentSerialChanged)
                return false;
            await Traceability.RecordPrintFile();
            return true;
        }

        public static async Task RecordPrintFile()
        {
            using (await Traceability._lock.LockAsync(Program.CancelSource.Token))
            {
                string filament = ModelObserver.CurrentFilament;
                if (!(filament == "Left") && !(filament == "Right"))
                    return;
                string filamentSerial = filament == "Left" ? Traceability.LeftFilamentSerial : Traceability.RightFilamentSerial;
                if (string.IsNullOrEmpty(filamentSerial))
                    filamentSerial = "n/a";
                string jobFile = await JobQueue.GetCurrentJobFile();
                if (string.IsNullOrEmpty(jobFile))
                    return;
                await using (FileStream printsFile = new FileStream("/opt/dsf/sd/sys/prints.csv", FileMode.Append, FileAccess.Write))
                {
                    await using (StreamWriter writer = new StreamWriter((Stream)printsFile))
                    {
                        if (printsFile.Length == 0L)
                            await writer.WriteLineAsync("\"datetime\",\"filename\",\"filament\",\"filamentSerial\"");
                        StreamWriter streamWriter = writer;
                        //DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(11, 4);
                        //interpolatedStringHandler.AppendLiteral("\"");
                        //interpolatedStringHandler.AppendFormatted<DateTime>(DateTime.Now, "s");
                        //interpolatedStringHandler.AppendLiteral("\",\"");
                        //interpolatedStringHandler.AppendFormatted(Path.GetFileName(jobFile));
                        //interpolatedStringHandler.AppendLiteral("\",\"");
                        //interpolatedStringHandler.AppendFormatted(filament);
                        //interpolatedStringHandler.AppendLiteral("\",\"");
                        //interpolatedStringHandler.AppendFormatted(filamentSerial);
                        //interpolatedStringHandler.AppendLiteral("\"");
                        //string stringAndClear = interpolatedStringHandler.ToStringAndClear();

                        string stringAndClear = "\"" + DateTime.Now.ToString("s") + "\",\"" + Path.GetFileName(jobFile) + "\",\"" + filament + "\",\"" + filamentSerial + "\"";
                        await streamWriter.WriteLineAsync(stringAndClear);
                    }
                }
                filament = (string)null;
                filamentSerial = (string)null;
                jobFile = (string)null;
                //printsFile = (FileStream)null;
                //writer = (StreamWriter)null;
            }
        }

        public static async Task<string> FilterLog(string filterString)
        {
            if (!File.Exists("/opt/dsf/sd/sys/prints.csv"))
                return "\"datetime\",\"filename\",\"filament\",\"filamentSerial\"";
            StreamReader reader;
            StringBuilder builder;
            await using (FileStream fs = new FileStream("/opt/dsf/sd/sys/prints.csv", FileMode.Open, FileAccess.Read))
            {
                reader = new StreamReader((Stream)fs);
                try
                {
                    builder = new StringBuilder();
                    StringBuilder stringBuilder = builder;
                    string str = await reader.ReadLineAsync();
                    stringBuilder.AppendLine(str);
                    stringBuilder = (StringBuilder)null;
                    str = (string)null;
                    do
                    {
                        string line = await reader.ReadLineAsync();
                        if (line != null)
                        {
                            if (line.Contains(filterString))
                                builder.AppendLine(line);
                            line = (string)null;
                        }
                        else
                            break;
                    }
                    while (!Program.CancelSource.IsCancellationRequested);
                    return builder.ToString();
                }
                finally
                {
                    reader?.Dispose();
                }
            }
            reader = (StreamReader)null;
            builder = (StringBuilder)null;
            string str1;
            return str1;
        }

        public class VariableStorage
        {
            public bool EnableFilamentTracking { get; set; }

            public string LeftFilamentSerial { get; set; } = string.Empty;

            public string RightFilamentSerial { get; set; } = string.Empty;

            public float Babystep { get; set; }

            public string MachineSerial { get; set; } = string.Empty;
        }
    }
}