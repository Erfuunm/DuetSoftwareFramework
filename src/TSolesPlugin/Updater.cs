// Decompiled with JetBrains decompiler
// Type: TSolesPlugin.Updater
// Assembly: TSolesPlugin, Version=3.4.1.0, Culture=neutral, PublicKeyToken=null
// MVID: 943EFD72-12B6-4D53-BB0D-FD8C4C742C73
// Assembly location: D:\opt directory (T-Soles)\dsf\bin\TSolesPlugin.dll

using DuetAPIClient;
using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace TSolesPlugin
{
    public static class Updater
    {
        public static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(6.0);
        private static readonly AsyncManualResetEvent _updateState = new AsyncManualResetEvent();
        private static CancellationTokenSource _delayCts = CancellationTokenSource.CreateLinkedTokenSource(Program.CancelSource.Token);

        public static Task WaitForFinish()
        {
            return Updater._updateState.WaitAsync(Program.CancelSource.Token);
        }

        private static bool IsNetworkOnline()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback && networkInterface.OperationalStatus == OperationalStatus.Up && networkInterface.GetIPProperties().UnicastAddresses.Where<UnicastIPAddressInformation>((Func<UnicastIPAddressInformation, bool>)(unicastAddress => unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)).Select<UnicastIPAddressInformation, IPAddress>((Func<UnicastIPAddressInformation, IPAddress>)(unicastAddress => unicastAddress.Address)).FirstOrDefault<IPAddress>() != null)
                    return true;
            }
            return false;
        }

        public static async Task Run()
        {
            for (int i = 0; i < 120; ++i)
            {
                if (Updater.IsNetworkOnline() && DateTime.Now.Year >= 2023)
                {
                    Console.WriteLine("[info] Network is online");
                    break;
                }
                await Task.Delay(1000, Program.CancelSource.Token);
            }
            do
            {
                if (Updater.IsNetworkOnline())
                {
                    Updater._updateState.Reset();
                    await Updater.Update();
                }
                else
                    Console.WriteLine("[warn] Skipping update because network is offline");
                string gcodeDirectory = (string)null;
                do
                {
                    try
                    {
                        using (CommandConnection commandConnection = new CommandConnection())
                        {
                            await commandConnection.Connect(Program.SocketPath, Program.CancelSource.Token);
                            gcodeDirectory = await commandConnection.ResolvePath("0:/gcodes");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[error] Failed to resolve 0:/gcodes: {0}", (object)ex);
                        await Task.Delay(2000, Program.CancelSource.Token);
                    }
                }
                while (!Program.CancelSource.IsCancellationRequested);
                if (!string.IsNullOrEmpty(gcodeDirectory))
                {
                    foreach (string file in Directory.EnumerateFiles(gcodeDirectory))
                    {
                        if ((DateTime.Now - File.GetLastWriteTime(file)).Duration() > TimeSpan.FromDays(7.0))
                            File.Delete(file);
                    }
                }
                Updater._updateState.Set();
                try
                {
                    await Task.Delay(Updater.UpdateInterval, Updater._delayCts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    if (Program.CancelSource.IsCancellationRequested)
                        throw;
                }
                bool isIdle;
                do
                {
                    using (await JobQueue.LockAsync())
                    {
                        if (JobQueue.Jobs.Any<Job>((Func<Job, bool>)(job => job.Completed < job.Total)) && !Program.CancelSource.IsCancellationRequested)
                        {
                            isIdle = false;
                            await JobQueue.WaitForUpdate();
                        }
                        else
                            isIdle = true;
                    }
                }
                while (!isIdle);
                gcodeDirectory = (string)null;
            }
            while (!Program.CancelSource.IsCancellationRequested);
        }

        private static async Task Update()
        {
            do
            {
                bool wasConnected = false;
                try
                {
                    using (CommandConnection commandConnection = new CommandConnection())
                    {
                        await commandConnection.Connect(Program.SocketPath, Program.CancelSource.Token);
                        Console.WriteLine("[info] Updater connected");
                        wasConnected = true;
                        Exception obj = null;
                        int num = 0;
                        try
                        {
                            Console.WriteLine("[info] Starting update");
                            await commandConnection.SetUpdateStatus(true);
                            Console.WriteLine("[info] Updating package lists");
                            using (Process updateProcess = Process.Start("/usr/bin/apt-get", "-y update"))
                                await updateProcess.WaitForExitAsync(Program.CancelSource.Token);
                            Console.WriteLine("[info] Performing upgrade");
                            using (Process upgradeProcess = Process.Start("/usr/bin/unattended-upgrade"))
                                await upgradeProcess.WaitForExitAsync(Program.CancelSource.Token);
                            if (File.Exists("/usr/bin/sole-update"))
                            {
                                Console.WriteLine("[info] Running post-upgrade script");
                                using (Process postUpgradeProcess = Process.Start("/usr/bin/sole-update"))
                                    await postUpgradeProcess.WaitForExitAsync(Program.CancelSource.Token);
                            }
                            Console.WriteLine("[info] Done!");
                            num = 1;
                        }
                        catch (Exception ex)
                        {
                            obj = ex;
                        }
                        await commandConnection.SetUpdateStatus(false);
                        var obj1 = obj;
                        if (obj1 != null)
                        {
                            if (!(obj1 is Exception source))
                                throw obj1;
                            ExceptionDispatchInfo.Capture(source).Throw();
                        }
                        if (num == 1)
                            break;
                        obj = null;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is SocketException)
                {
                    if (wasConnected)
                        Console.WriteLine("[warn] Updater lost connection to DCS");
                    else if (!(ex is SocketException))
                        Console.WriteLine("[warn] Failed to run update");
                    await Task.Delay(2000, Program.CancelSource.Token);
                }
            }
            while (!Program.CancelSource.IsCancellationRequested);
        }

        public static void StartUpdate()
        {
            Updater._delayCts.Cancel();
            Updater._delayCts.Dispose();
            Updater._delayCts = CancellationTokenSource.CreateLinkedTokenSource(Program.CancelSource.Token);
        }
    }
}