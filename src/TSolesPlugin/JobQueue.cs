// Decompiled with JetBrains decompiler
// Type: TSolesPlugin.JobQueue
// Assembly: TSolesPlugin, Version=3.4.1.0, Culture=neutral, PublicKeyToken=null
// MVID: 943EFD72-12B6-4D53-BB0D-FD8C4C742C73
// Assembly location: D:\opt directory (T-Soles)\dsf\bin\TSolesPlugin.dll

using DuetAPI.ObjectModel;
using DuetAPIClient;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TSolesPlugin.IPC;

#nullable enable
namespace TSolesPlugin
{
    public static class JobQueue
    {
        private static readonly List<Job> _jobs = new List<Job>();
        private static readonly AsyncMonitor _monitor = new AsyncMonitor();
        public static readonly ReadOnlyCollection<Job> Jobs = new ReadOnlyCollection<Job>((IList<Job>)JobQueue._jobs);
        public const string JobQueueFile = "/opt/dsf/sd/sys/jobs.json";

        public static AwaitableDisposable<IDisposable> LockAsync() => JobQueue._monitor.EnterAsync();

        public static async Task<string?> GetCurrentJobFile()
        {
            using (await JobQueue.LockAsync())
            {
                foreach (Job job in JobQueue._jobs)
                {
                    if (job.Active)
                        return job.Filename;
                }
            }
            return (string)null;
        }

        public static bool StartingPrint { get; set; }

        public static bool ProcessingLastJob { get; private set; }

        public static bool HadEmergencyStop { get; set; }

        public static Task WaitForUpdate() => JobQueue._monitor.WaitAsync(Program.CancelSource.Token);

        private static async Task JobsUpdated()
        {
            Process.Start("/usr/bin/xscreensaver-command", "-deactivate");
            await using (FileStream fs = new FileStream("/opt/dsf/sd/sys/jobs.json", FileMode.Create, FileAccess.Write))
                await JsonSerializer.SerializeAsync<List<Job>>((Stream)fs, JobQueue._jobs);
            if (JobQueue._jobs.Count > 0)
            {
                int remainingJobs = 0;
                foreach (Job job in JobQueue._jobs)
                    remainingJobs += job.Total - job.Completed;
                JobQueue.ProcessingLastJob = remainingJobs == 1;
            }
            else
                JobQueue.ProcessingLastJob = false;
            JobQueue._monitor.PulseAll();
        }

        public static async Task PrintStarted(string filename)
        {
            if (!JobQueue.StartingPrint)
                Console.WriteLine("[info] Started next job {0} from an external source", (object)filename);
            await JobQueue.Add(filename, JobQueue.StartingPrint ? 0 : 1, !JobQueue.StartingPrint);
            await Traceability.RecordPrintFile();
            JobQueue.StartingPrint = false;
        }

        public static async Task Add(string filename, int total, bool manuallyStarted = false)
        {
            foreach (Job job in JobQueue._jobs)
            {
                Job item = job;
                if (item.Filename == filename)
                {
                    item.Total += total;
                    item.ExternallyStarted = manuallyStarted;
                    await JobQueue.JobsUpdated();
                    return;
                }
                item = (Job)null;
            }
            long? printTime = new long?();
            bool wasConnected = false;
            do
            {
                try
                {
                    string physicalFile;
                    GCodeFileInfo fileInfo;
                    using (CommandConnection commandConnection = new CommandConnection())
                    {
                        await commandConnection.Connect(Program.SocketPath, Program.CancelSource.Token);
                        wasConnected = true;
                        physicalFile = await commandConnection.ResolvePath(filename);
                        fileInfo = await commandConnection.GetFileInfo(physicalFile, Program.CancelSource.Token);
                        printTime = fileInfo.PrintTime ?? fileInfo.SimulatedTime;
                    }
                    physicalFile = (string)null;
                    fileInfo = (GCodeFileInfo)null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[error] Failed to parse file {0}: {1}", (object)filename, (object)ex);
                    if (!wasConnected)
                        await Task.Delay(2000, Program.CancelSource.Token);
                }
            }
            while (!wasConnected && !Program.CancelSource.IsCancellationRequested);
            JobQueue._jobs.Add(new Job()
            {
                Filename = filename,
                Total = total,
                ExternallyStarted = manuallyStarted,
                PrintTime = printTime
            });
            await JobQueue.JobsUpdated();
        }

        public static async Task Update(int index, int timesToPrint)
        {
            JobQueue._jobs[index].Total = timesToPrint;
            await JobQueue.JobsUpdated();
        }

        public static async Task Move(int from, int to)
        {
            Job job = JobQueue._jobs[from];
            JobQueue._jobs.RemoveAt(from);
            if (to >= JobQueue._jobs.Count)
                JobQueue._jobs.Add(job);
            else
                JobQueue._jobs.Insert(to, job);
            await JobQueue.JobsUpdated();
            job = (Job)null;
        }

        public static async Task Delete(int index)
        {
            JobQueue._jobs.RemoveAt(index);
            await JobQueue.JobsUpdated();
        }

        public static async Task DeleteAll()
        {
            JobQueue._jobs.Clear();
            await JobQueue.JobsUpdated();
        }

        public static async Task Run()
        {
            if (File.Exists("/opt/dsf/sd/sys/jobs.json"))
            {
                try
                {
                    await using (FileStream fs = new FileStream("/opt/dsf/sd/sys/jobs.json", FileMode.Open, FileAccess.Read))
                    {
                        List<Job> savedJobs = await JsonSerializer.DeserializeAsync<List<Job>>((Stream)fs);
                        if (savedJobs != null)
                        {
                            foreach (Job job in savedJobs)
                            {
                                if (job.Completed < job.Total && !job.ExternallyStarted)
                                {
                                    job.Active = false;
                                    JobQueue._jobs.Add(job);
                                }
                            }
                        }
                        savedJobs = (List<Job>)null;
                    }
                    await JobQueue.JobsUpdated();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[warn] Failed to load saved jobs: {0}", (object)ex.Message);
                }
            }
            do
            {
                CodeInterceptor.InIntermediateSegment = false;
                bool startingPrintQueue = true;
                bool jobExternallyStarted = false;
                bool printCancelled = false;
                while (true)
                {
                    Job job = (Job)null;
                    using (await JobQueue._monitor.EnterAsync())
                    {
                        foreach (Job job1 in JobQueue._jobs)
                        {
                            Job item = job1;
                            if (item.Completed < item.Total)
                            {
                                job = item;
                                break;
                            }
                            item = (Job)null;
                        }
                        if (job == null)
                        {
                            if (startingPrintQueue)
                            {
                                await JobQueue._monitor.WaitAsync(Program.CancelSource.Token);
                                goto label_109;
                            }
                            else
                                goto label_109;
                        }
                        else
                        {
                            job.Active = true;
                            await JobQueue.JobsUpdated();
                        }
                    }
                    jobExternallyStarted = job.ExternallyStarted;
                    if (!jobExternallyStarted)
                    {
                        await ModelObserver.WaitUntilReady(Program.CancelSource.Token);
                        if (startingPrintQueue)
                            Console.WriteLine("[info] Start processing the print queue");
                        Console.WriteLine("[info] Starting next job {0} ({1} of {2} printed)", (object)job.Filename, (object)job.Completed, (object)job.Total);
                        bool printStarted = false;
                        do
                        {
                            try
                            {
                                string macroFile;
                                string response;
                                using (CommandConnection commandConnection1 = new CommandConnection())
                                {
                                    await commandConnection1.Connect(Program.SocketPath, Program.CancelSource.Token);
                                    macroFile = startingPrintQueue || JobQueue.HadEmergencyStop ? "queue-start.g" : "queue-intermediate.g";
                                    response = await commandConnection1.PerformSimpleCode("M98 P\"" + macroFile + "\"");
                                    if (response.StartsWith("Error", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        CommandConnection commandConnection2 = commandConnection1;
                                        //DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(32, 2);
                                        //interpolatedStringHandler.AppendLiteral("M118 S\"Failed to ");
                                        //interpolatedStringHandler.AppendFormatted(startingPrintQueue || JobQueue.HadEmergencyStop ? "start" : "continue");
                                        //interpolatedStringHandler.AppendLiteral(" print queue: ");
                                        //interpolatedStringHandler.AppendFormatted(response);
                                        //interpolatedStringHandler.AppendLiteral("\"");
                                        //string stringAndClear = interpolatedStringHandler.ToStringAndClear();

                                        string stringAndClear = "M118 S\"Failed to " + (startingPrintQueue || JobQueue.HadEmergencyStop ? "start" : "continue") + "print queue:" + response + "\"";

                                        CancellationToken cancellationToken = new CancellationToken();
                                        string str = await commandConnection2.PerformSimpleCode(stringAndClear, cancellationToken: cancellationToken);
                                    }
                                    using (await JobQueue.LockAsync())
                                    {
                                        if (!JobQueue.Jobs.Contains(job))
                                        {
                                            response = await commandConnection1.PerformSimpleCode("M98 P\"cancel.g\"");
                                            if (response.StartsWith("Error", StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                CommandConnection commandConnection3 = commandConnection1;
                                                //DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(32, 2);
                                                //interpolatedStringHandler.AppendLiteral("M118 S\"Failed to ");
                                                //interpolatedStringHandler.AppendFormatted(startingPrintQueue || JobQueue.HadEmergencyStop ? "start" : "continue");
                                                //interpolatedStringHandler.AppendLiteral(" print queue: ");
                                                //interpolatedStringHandler.AppendFormatted(response);
                                                //interpolatedStringHandler.AppendLiteral("\"");
                                                //string stringAndClear = interpolatedStringHandler.ToStringAndClear();
                                                string stringAndClear = "M118 S\"Failed to " + (startingPrintQueue || JobQueue.HadEmergencyStop ? "start" : "continue") + " print queue: " + response + "\"";

                                                CancellationToken cancellationToken = new CancellationToken();
                                                string str = await commandConnection3.PerformSimpleCode(stringAndClear, cancellationToken: cancellationToken);
                                                break;
                                            }
                                            break;
                                        }
                                    }
                                    JobQueue.StartingPrint = true;
                                    response = await commandConnection1.PerformSimpleCode("M32 \"" + job.Filename.Replace("'", "''") + "\"");
                                    if (response.StartsWith("Error", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        JobQueue.StartingPrint = false;
                                        string str = await commandConnection1.PerformSimpleCode("M118 S\"Failed to start file print: " + response + "\"");
                                    }
                                    printStarted = true;
                                    await commandConnection1.SyncObjectModel(Program.CancelSource.Token);
                                }
                                macroFile = (string)null;
                                response = (string)null;
                            }
                            catch (Exception ex) when (ex is IOException || ex is SocketException)
                            {
                                Console.WriteLine("[warn] Trying once more to start the file print due to this error: {0}", (object)ex.Message);
                                await Task.Delay(2000);
                            }
                        }
                        while (!printStarted);
                        if (!printStarted)
                            break;
                    }
                    startingPrintQueue = JobQueue.HadEmergencyStop = false;
                    await Task.Delay(1000, Program.CancelSource.Token);
                    await ModelObserver.WaitUntilReady(Program.CancelSource.Token);
                    await Task.Delay(1000, Program.CancelSource.Token);
                    using (await JobQueue._monitor.EnterAsync(Program.CancelSource.Token))
                    {
                        if (ModelObserver.IsJobComplete)
                            ++job.Completed;
                        else
                            startingPrintQueue = true;
                        job.Active = job.ExternallyStarted = false;
                        int i = JobQueue._jobs.Count - 1;
                        int jobCount = 0;
                        for (; i >= 0; --i)
                        {
                            if (++jobCount > 50 && JobQueue._jobs[i].Completed >= JobQueue._jobs[i].Total)
                            {
                                try
                                {
                                    if (Path.GetDirectoryName(JobQueue._jobs[i].Filename).EndsWith("gcodes"))
                                    {
                                        string physicalFile;
                                        using (CommandConnection commandConnection = new CommandConnection())
                                        {
                                            await commandConnection.Connect(Program.SocketPath, Program.CancelSource.Token);
                                            physicalFile = await commandConnection.ResolvePath(JobQueue._jobs[i].Filename);
                                            if (File.Exists(physicalFile))
                                                File.Delete(physicalFile);
                                        }
                                        physicalFile = (string)null;
                                    }
                                }
                                finally
                                {
                                    JobQueue._jobs.RemoveAt(i);
                                    Console.WriteLine("[info] Deleted job {0}", (object)job.Filename);
                                }
                            }
                        }
                        await JobQueue.JobsUpdated();
                        Console.WriteLine(jobExternallyStarted ? "[info] Finished external job {0}, printed {1} of {2} times" : "[info] Finished job {0}, printed {1} of {2} times", (object)job.Filename, (object)job.Completed, (object)job.Total);
                    }
                    job = (Job)null;
                }
                Console.WriteLine("[info] Print cancelled while running the queue macros");
                printCancelled = true;
            label_109:
                if (!startingPrintQueue && !jobExternallyStarted && !printCancelled)
                {
                    try
                    {
                        string response;
                        using (CommandConnection commandConnection = new CommandConnection())
                        {
                            await commandConnection.Connect(Program.SocketPath, Program.CancelSource.Token);
                            response = await commandConnection.PerformSimpleCode("M98 P\"queue-end.g\"");
                            if (response.StartsWith("Error", StringComparison.InvariantCultureIgnoreCase))
                            {
                                string str = await commandConnection.PerformSimpleCode("M118 S\"Failed to end print queue: " + response + "\"");
                                throw new Exception("Received error response for M98: " + response);
                            }
                        }
                        response = (string)null;
                    }
                    catch (Exception ex) when (ex is IOException || ex is SocketException)
                    {
                        Console.WriteLine("[warn] Trying once more to run the job queue end macro due to this error: {0}", (object)ex.Message);
                        await Task.Delay(2000);
                    }
                    Console.WriteLine("[info] Finished processing the print queue");
                }
            }
            while (!Program.CancelSource.IsCancellationRequested);
        }
    }
}