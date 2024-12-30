// Decompiled with JetBrains decompiler
// Type: TSolesPlugin.Program
// Assembly: TSolesPlugin, Version=3.4.1.0, Culture=neutral, PublicKeyToken=null
// MVID: 943EFD72-12B6-4D53-BB0D-FD8C4C742C73
// Assembly location: D:\opt directory (T-Soles)\dsf\bin\TSolesPlugin.dll

using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using TSolesPlugin.IPC;

#nullable enable
namespace TSolesPlugin
{
    public static class Program
    {
        private static readonly ManualResetEvent _programTerminated = new ManualResetEvent(false);

        public static string Version { get; } = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        public static CancellationTokenSource CancelSource { get; } = new CancellationTokenSource();

        public static string SocketPath { get; set; } = "/run/dsf/dcs.sock";

        public static async Task Main(string[] args)
        {
            Console.WriteLine("T-Soles Plugin v" + Program.Version);
            Console.WriteLine("Written by Elaheh");
            string lastArg = string.Empty;
            string[] strArray = args;
            for (int index = 0; index < strArray.Length; ++index)
            {
                string arg = strArray[index];
                if (lastArg == "-s" || lastArg == "--socket-file")
                    Program.SocketPath = arg;
                lastArg = arg;
                arg = (string)null;
            }
            strArray = (string[])null;
            AssemblyLoadContext.Default.Unloading += (Action<AssemblyLoadContext>)(_ =>
            {
                if (Program.CancelSource.IsCancellationRequested)
                    return;
                Console.WriteLine("[warn] Received SIGTERM, shutting down...");
                Program.CancelSource.Cancel();
                Program._programTerminated.WaitOne();
            });
            Console.CancelKeyPress += (ConsoleCancelEventHandler)((_, e) =>
            {
                if (Program.CancelSource.IsCancellationRequested)
                    return;
                Console.WriteLine("[warn] Received SIGINT, shutting down...");
                e.Cancel = true;
                Program.CancelSource.Cancel();
            });
            await Traceability.Init();
            Task jobQueueTask = Task.Factory.StartNew<Task>(new Func<Task>(JobQueue.Run), TaskCreationOptions.LongRunning).Unwrap();
            Task codeInterceptorTask = Task.Factory.StartNew<Task>(new Func<Task>(CodeInterceptor.Run), TaskCreationOptions.LongRunning).Unwrap();
            Task httpEndpointTask = Task.Factory.StartNew<Task>(new Func<Task>(HttpEndpoints.Run), TaskCreationOptions.LongRunning).Unwrap();
            Task modelObserverTask = Task.Factory.StartNew<Task>(new Func<Task>(ModelObserver.Run), TaskCreationOptions.LongRunning).Unwrap();
            Task updaterTask = Task.Factory.StartNew<Task>(new Func<Task>(Updater.Run), TaskCreationOptions.LongRunning).Unwrap();
            Task terminatedTask = await Task.WhenAny(jobQueueTask, codeInterceptorTask, httpEndpointTask, modelObserverTask, updaterTask);
            if (terminatedTask.IsFaulted && !Program.CancelSource.IsCancellationRequested)
                Console.WriteLine("[err] Unhandled exception: {0}", (object)terminatedTask.Exception);
            Program.CancelSource.Cancel();
            try
            {
                await Task.WhenAll(jobQueueTask, codeInterceptorTask, httpEndpointTask, modelObserverTask, updaterTask);
            }
            catch
            {
            }
            Program._programTerminated.Set();
            lastArg = (string)null;
            jobQueueTask = (Task)null;
            codeInterceptorTask = (Task)null;
            httpEndpointTask = (Task)null;
            modelObserverTask = (Task)null;
            updaterTask = (Task)null;
            terminatedTask = (Task)null;
        }
    }
}