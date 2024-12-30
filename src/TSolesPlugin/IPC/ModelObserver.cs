// Decompiled with JetBrains decompiler
// Type: TSolesPlugin.IPC.ModelObserver
// Assembly: TSolesPlugin, Version=3.4.1.0, Culture=neutral, PublicKeyToken=null
// MVID: 943EFD72-12B6-4D53-BB0D-FD8C4C742C73
// Assembly location: C:\Users\user\Downloads\Telegram Desktop\opt directory (T-Soles)\bin\TSolesPlugin.dll

using DuetAPI.Connection;
using DuetAPI.ObjectModel;
using DuetAPIClient;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


#nullable enable
namespace TSolesPlugin.IPC
{
  public static class ModelObserver
  {
    private static readonly string[] Filters = new string[10]
    {
      "move/axes/**",
      "move/extruders/**",
      "global/**",
      "job/lastFileName",
      "job/lastFileAborted",
      "job/file/fileName",
      "messages/**",
      "state/displayMessage",
      "state/messageBox",
      "state/status"
    };
    private static readonly AsyncMonitor _monitor = new AsyncMonitor();
    private static bool _isIdle;
    private static bool _hadEmergencyStop;

    public static AwaitableDisposable<IDisposable> LockAsync() => ModelObserver._monitor.EnterAsync();

    public static string? CurrentFilament { get; private set; }

    public static bool IsJobComplete { get; private set; }

    public static async Task WaitUntilReady(CancellationToken cancellationToken)
    {
      using (await ModelObserver._monitor.EnterAsync(cancellationToken))
      {
        if (!ModelObserver._isIdle || ModelObserver._hadEmergencyStop)
          await ModelObserver._monitor.WaitAsync(cancellationToken);
      }
    }

    public static async Task Resume()
    {
      using (await ModelObserver._monitor.EnterAsync())
      {
        if (ModelObserver._hadEmergencyStop)
        {
          ModelObserver._hadEmergencyStop = false;
          ModelObserver._monitor.PulseAll();
        }
      }
    }

    public static async Task Run()
    {
      // ISSUE: reference to a compiler-generated field
      //int num = this.\u003C\u003E1__state;
      while (true)
      {
        bool wasConnected = false;
        try
        {
          DuetAPI.ObjectModel.ObjectModel model;
          using (SubscribeConnection subscribeConnection = new SubscribeConnection())
          {
            await subscribeConnection.Connect(SubscriptionMode.Patch, (IEnumerable<string>) ModelObserver.Filters, Program.SocketPath, Program.CancelSource.Token);
            Console.WriteLine("[info] Model observer connected");
            wasConnected = true;
            model = await subscribeConnection.GetObjectModel(Program.CancelSource.Token);
            ModelObserver.CurrentFilament = model.Move.Extruders.Count > 0 ? model.Move.Extruders[0].Filament : (string) null;
            bool checkVariables = true;
            bool wasPrinting = false;
            bool wasBusyOrUpdating = false;
            do
            {
              using (JsonDocument diff = await subscribeConnection.GetObjectModelPatch(Program.CancelSource.Token))
              {
                model.UpdateFromJson(diff.RootElement);
                ModelObserver.CurrentFilament = model.Move.Extruders.Count > 0 ? model.Move.Extruders[0].Filament : (string) null;
                bool wakeUp = !wasBusyOrUpdating && (model.State.Status == MachineStatus.Busy || model.State.Status == MachineStatus.Updating);
                bool variablesInitialized = model.Global.ContainsKey("enableBroadcast") && model.Global.ContainsKey("enableFilamentTracking") && model.Global.ContainsKey("leftFilamentSerial") && model.Global.ContainsKey("rightFilamentSerial") && model.Global.ContainsKey("machineSerial");
                if (checkVariables && !variablesInitialized)
                {
                  await Traceability.InitVariables();
                  checkVariables = false;
                }
                using (await ModelObserver._monitor.EnterAsync())
                {
                  if (model.State.Status == MachineStatus.Halted)
                  {
                    ModelObserver._hadEmergencyStop = true;
                    variablesInitialized = false;
                    CodeInterceptor.InIntermediateSegment = false;
                    JobQueue.HadEmergencyStop = true;
                    checkVariables = wakeUp = true;
                  }
                  ModelObserver.IsJobComplete = !string.IsNullOrEmpty(model.Job.LastFileName) && !model.Job.LastFileAborted;
                  ModelObserver._isIdle = model.State.Status == MachineStatus.Idle && !checkVariables | variablesInitialized;
                  if (ModelObserver._isIdle && !ModelObserver._hadEmergencyStop)
                    ModelObserver._monitor.PulseAll();
                }
                if (variablesInitialized && model.Move.Axes.Count > 0)
                {
                  bool enableBroadcast = model.Global["enableBroadcast"].GetBoolean();
                  JsonElement jsonElement = model.Global["enableFilamentTracking"];
                  bool enableFilamentTracking = jsonElement.GetBoolean();
                  jsonElement = model.Global["leftFilamentSerial"];
                  string leftFilamentSerial = jsonElement.GetString() ?? string.Empty;
                  jsonElement = model.Global["rightFilamentSerial"];
                  string rightFilamentSerial = jsonElement.GetString() ?? string.Empty;
                  float babystep = model.Move.Axes[1].Babystep;
                  jsonElement = model.Global["machineSerial"];
                  string machineSerial = jsonElement.GetString() ?? string.Empty;
                  if (await Traceability.MaintainVariables(enableBroadcast, enableFilamentTracking, leftFilamentSerial, rightFilamentSerial, babystep, machineSerial))
                    wakeUp = true;
                  checkVariables = true;
                  leftFilamentSerial = (string) null;
                  rightFilamentSerial = (string) null;
                  machineSerial = (string) null;
                }
                if (string.IsNullOrEmpty(model.Job.File.FileName))
                  wasPrinting = false;
                else if (!wasPrinting)
                {
                  wasPrinting = true;
                  await JobQueue.PrintStarted(model.Job.File.FileName);
                }
                if (wakeUp)
                  Process.Start("/usr/bin/xscreensaver-command", "-deactivate");
                wasBusyOrUpdating = model.State.Status == MachineStatus.Busy || model.State.Status == MachineStatus.Updating;
              }
            }
            while (subscribeConnection.IsConnected);
          }
          model = (DuetAPI.ObjectModel.ObjectModel) null;
        }
        catch (Exception ex) when (ex is IOException || ex is SocketException)
        {
          if (wasConnected)
            Console.WriteLine("[warn] Model observer lost connection to DCS");
          else if (!(ex is SocketException))
            Console.WriteLine("[warn] Failed to set up model observer");
        }
        await Task.Delay(2000, Program.CancelSource.Token);
      }
    }
  }
}
