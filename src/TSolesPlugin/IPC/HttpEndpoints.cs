// Decompiled with JetBrains decompiler
// Type: TSolesPlugin.IPC.HttpEndpoints
// Assembly: TSolesPlugin, Version=3.4.1.0, Culture=neutral, PublicKeyToken=null
// MVID: 943EFD72-12B6-4D53-BB0D-FD8C4C742C73
// Assembly location: C:\Users\user\Downloads\Telegram Desktop\opt directory (T-Soles)\bin\TSolesPlugin.dll

using DuetAPI.Commands;
using DuetAPI.ObjectModel;
using DuetAPI.Utility;
using DuetAPIClient;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;


#nullable enable
namespace TSolesPlugin.IPC
{
  public static class HttpEndpoints
  {
    public static async Task Run()
    {
      // ISSUE: reference to a compiler-generated field
      //int num = this.\u003C\u003E1__state;
      while (true)
      {
        bool wasConnected = false;
        HttpEndpointUnixSocket wsTSolesQueue = (HttpEndpointUnixSocket) null;
        HttpEndpointUnixSocket getTSolesQueue = (HttpEndpointUnixSocket) null;
        HttpEndpointUnixSocket putTSolesQueue = (HttpEndpointUnixSocket) null;
        HttpEndpointUnixSocket patchTSolesQueue = (HttpEndpointUnixSocket) null;
        HttpEndpointUnixSocket deleteTSolesQueue = (HttpEndpointUnixSocket) null;
        HttpEndpointUnixSocket getTSolesUpdate = (HttpEndpointUnixSocket) null;
        HttpEndpointUnixSocket getFilterLog = (HttpEndpointUnixSocket) null;
        try
        {
          using (CommandConnection commandConnection = new CommandConnection())
          {
            await commandConnection.Connect(Program.SocketPath, Program.CancelSource.Token);
            Console.WriteLine("[info] Registering HTTP endpoints");
            wasConnected = true;
            wsTSolesQueue = await commandConnection.AddHttpEndpoint(HttpEndpointType.WebSocket, "tsoles", "live");
            wsTSolesQueue.OnEndpointRequestReceived += new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnWsTSolesQueue);
            getTSolesQueue = await commandConnection.AddHttpEndpoint(HttpEndpointType.GET, "tsoles", "queue");
            getTSolesQueue.OnEndpointRequestReceived += new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnGetTSolesQueue);
            putTSolesQueue = await commandConnection.AddHttpEndpoint(HttpEndpointType.PUT, "tsoles", "queue");
            putTSolesQueue.OnEndpointRequestReceived += new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnPutTSolesQueue);
            patchTSolesQueue = await commandConnection.AddHttpEndpoint(HttpEndpointType.PATCH, "tsoles", "queue");
            patchTSolesQueue.OnEndpointRequestReceived += new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnPatchTSolesQueue);
            deleteTSolesQueue = await commandConnection.AddHttpEndpoint(HttpEndpointType.DELETE, "tsoles", "queue");
            deleteTSolesQueue.OnEndpointRequestReceived += new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnDeleteTSolesQueue);
            getTSolesUpdate = await commandConnection.AddHttpEndpoint(HttpEndpointType.GET, "tsoles", "update");
            getTSolesUpdate.OnEndpointRequestReceived += new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnGetTSolesUpdate);
            getFilterLog = await commandConnection.AddHttpEndpoint(HttpEndpointType.GET, "tsoles", "filterLog");
            getFilterLog.OnEndpointRequestReceived += new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnGetTSolesFilterLog);
            foreach (string file in Directory.EnumerateFiles("/var/run/dsf/tsoles"))
            {
              using (Process.Start("/usr/bin/chown", "dsf.dsf " + file))
                ;
            }
            Console.WriteLine("[info] HTTP endpoints registered");
            while (commandConnection.IsConnected)
            {
              commandConnection.Poll();
              await Task.Delay(500, Program.CancelSource.Token);
            }
          }
        }
        catch (Exception ex) when (ex is IOException || ex is SocketException)
        {
          if (wasConnected)
            Console.WriteLine("[warn] HTTP endpoint manager lost connection to DCS");
        }
        finally
        {
          if (wsTSolesQueue != null)
          {
            wsTSolesQueue.OnEndpointRequestReceived -= new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnWsTSolesQueue);
            wsTSolesQueue.Dispose();
          }
          if (getTSolesQueue != null)
          {
            getTSolesQueue.OnEndpointRequestReceived -= new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnGetTSolesQueue);
            getTSolesQueue.Dispose();
          }
          if (putTSolesQueue != null)
          {
            putTSolesQueue.OnEndpointRequestReceived -= new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnPutTSolesQueue);
            putTSolesQueue.Dispose();
          }
          if (patchTSolesQueue != null)
          {
            patchTSolesQueue.OnEndpointRequestReceived -= new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnPatchTSolesQueue);
            patchTSolesQueue.Dispose();
          }
          if (deleteTSolesQueue != null)
          {
            deleteTSolesQueue.OnEndpointRequestReceived -= new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnDeleteTSolesQueue);
            deleteTSolesQueue.Dispose();
          }
          if (getTSolesUpdate != null)
          {
            getTSolesUpdate.OnEndpointRequestReceived -= new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnGetTSolesUpdate);
            getTSolesUpdate.Dispose();
          }
          if (getFilterLog != null)
          {
            getFilterLog.OnEndpointRequestReceived -= new HttpEndpointUnixSocket.EndpointRequestReceived(HttpEndpoints.OnGetTSolesFilterLog);
            getFilterLog.Dispose();
          }
        }
        await Task.Delay(2000, Program.CancelSource.Token);
        wsTSolesQueue = (HttpEndpointUnixSocket) null;
        getTSolesQueue = (HttpEndpointUnixSocket) null;
        putTSolesQueue = (HttpEndpointUnixSocket) null;
        patchTSolesQueue = (HttpEndpointUnixSocket) null;
        deleteTSolesQueue = (HttpEndpointUnixSocket) null;
        getTSolesUpdate = (HttpEndpointUnixSocket) null;
        getFilterLog = (HttpEndpointUnixSocket) null;
      }
    }

    private static async void OnWsTSolesQueue(
      HttpEndpointUnixSocket unixSocket,
      HttpEndpointConnection requestConnection)
    {
      try
      {
        string jobs;
        using (await JobQueue.LockAsync())
          jobs = JsonSerializer.Serialize<ReadOnlyCollection<TSolesPlugin.Job>>(JobQueue.Jobs, JsonHelper.DefaultJsonOptions);
        await requestConnection.SendResponse(200, jobs, HttpResponseType.JSON);
        while (true)
        {
          using (await JobQueue.LockAsync())
          {
            await JobQueue.WaitForUpdate();
            Program.CancelSource.Token.ThrowIfCancellationRequested();
            jobs = JsonSerializer.Serialize<ReadOnlyCollection<TSolesPlugin.Job>>(JobQueue.Jobs, JsonHelper.DefaultJsonOptions);
          }
          await requestConnection.SendResponse(200, jobs, HttpResponseType.JSON);
        }
      }
      catch (Exception ex)
      {
        if (!(ex is IOException) && !(ex is SocketException) && !(ex is OperationCanceledException))
        {
          Console.WriteLine("[err] Exception in OnWsTSolesQueue: {0}", (object) ex);
          await requestConnection.SendResponse(500, ex.Message, HttpResponseType.PlainText);
        }
      }
      finally
      {
        requestConnection.Close();
      }
    }

    private static async void OnGetTSolesQueue(
      HttpEndpointUnixSocket unixSocket,
      HttpEndpointConnection requestConnection)
    {
      try
      {
        ReceivedHttpRequest receivedHttpRequest = await requestConnection.ReadRequest(Program.CancelSource.Token);
        receivedHttpRequest = (ReceivedHttpRequest) null;
        string jobs;
        using (await JobQueue.LockAsync())
          jobs = JsonSerializer.Serialize<ReadOnlyCollection<TSolesPlugin.Job>>(JobQueue.Jobs, JsonHelper.DefaultJsonOptions);
        await requestConnection.SendResponse(200, jobs, HttpResponseType.JSON);
        jobs = (string) null;
      }
      catch (Exception ex)
      {
        Console.WriteLine("[err] Exception in OnGetTSolesQueue: {0}", (object) ex);
        await requestConnection.SendResponse(500, ex.Message, HttpResponseType.PlainText);
      }
    }

    private static async void OnPutTSolesQueue(
      HttpEndpointUnixSocket unixSocket,
      HttpEndpointConnection requestConnection)
    {
      try
      {
        ReceivedHttpRequest request = await requestConnection.ReadRequest(Program.CancelSource.Token);
        string filename;
        if (request.Queries.TryGetValue("filename", out filename))
        {
          int total = 1;
          string countString;
          int parsedCount;
          if (request.Queries.TryGetValue("total", out countString) && int.TryParse(countString, out parsedCount) && parsedCount > 0)
            total = parsedCount;
          await Updater.WaitForFinish();
          using (await JobQueue.LockAsync())
            await JobQueue.Add(filename, total);
          Console.WriteLine("[info] Job {0} has been queued to be printed {1} times", (object) filename, (object) total);
          await requestConnection.SendResponse();
          countString = (string) null;
        }
        else
        {
          Console.WriteLine("[err] No filename given in OnPutTSolesQueue");
          await requestConnection.SendResponse(400);
        }
        request = (ReceivedHttpRequest) null;
        filename = (string) null;
      }
      catch (Exception ex)
      {
        Console.WriteLine("[err] Exception in OnPutTSolesQueue: {0}", (object) ex);
        await requestConnection.SendResponse(500, ex.Message, HttpResponseType.PlainText);
      }
    }

    private static async void OnPatchTSolesQueue(
      HttpEndpointUnixSocket unixSocket,
      HttpEndpointConnection requestConnection)
    {
      try
      {
        ReceivedHttpRequest request = await requestConnection.ReadRequest(Program.CancelSource.Token);
        string indexString;
        int index;
        if (request.Queries.TryGetValue("index", out indexString) && int.TryParse(indexString, out index))
        {
          using (await JobQueue.LockAsync())
          {
            if (index >= 0 && index < JobQueue.Jobs.Count)
            {
              TSolesPlugin.Job job = JobQueue.Jobs[index];
              await Updater.WaitForFinish();
              string totalString;
              int total;
              if (request.Queries.TryGetValue("total", out totalString) && int.TryParse(totalString, out total))
              {
                if (job.Active)
                  await JobQueue.Update(index, Math.Max(total, job.Completed + 1));
                else
                  await JobQueue.Update(index, Math.Max(total, job.Completed));
                Console.WriteLine("[info] Job {0} has been changed to be printed {1} times", (object) job.Filename, (object) job.Total);
              }
              string newIndexString;
              int newIndex;
              if (request.Queries.TryGetValue("newIndex", out newIndexString) && int.TryParse(newIndexString, out newIndex) && index != newIndex)
              {
                await JobQueue.Move(index, newIndex);
                Console.WriteLine("[info] Job {0} has been moved from #{1} to #{2}", (object) job.Filename, (object) index, (object) newIndex);
              }
              await requestConnection.SendResponse();
              job = (TSolesPlugin.Job) null;
              totalString = (string) null;
              newIndexString = (string) null;
            }
            else
            {
              Console.WriteLine("[err] Invalid index in OnPatchTSolesQueue");
              await requestConnection.SendResponse(400);
            }
          }
        }
        else
        {
          Console.WriteLine("[err] No index given in OnPatchTSolesQueue");
          await requestConnection.SendResponse(400);
        }
        request = (ReceivedHttpRequest) null;
        indexString = (string) null;
      }
      catch (Exception ex)
      {
        Console.WriteLine("[err] Exception in OnPatchTSolesQueue: {0}", (object) ex);
        await requestConnection.SendResponse(500, ex.Message, HttpResponseType.PlainText);
      }
    }

    private static async void OnDeleteTSolesQueue(
      HttpEndpointUnixSocket unixSocket,
      HttpEndpointConnection requestConnection)
    {
      try
      {
        ReceivedHttpRequest request = await requestConnection.ReadRequest(Program.CancelSource.Token);
        await Updater.WaitForFinish();
        using (await JobQueue.LockAsync())
        {
          string indexString;
          if (request.Queries.TryGetValue("index", out indexString))
          {
            int index;
            if (int.TryParse(indexString, out index) && index >= 0 && index < JobQueue.Jobs.Count)
            {
              TSolesPlugin.Job job = JobQueue.Jobs[index];
              await JobQueue.Delete(index);
              Console.WriteLine("[info] Job {0} has been deleted", (object) job.Filename);
              await requestConnection.SendResponse();
              job = (TSolesPlugin.Job) null;
            }
            else
            {
              Console.WriteLine("[err] Invalid index given in OnPatchTSolesQueue");
              await requestConnection.SendResponse(400);
            }
          }
          else
          {
            await JobQueue.DeleteAll();
            Console.WriteLine("[info] Jobs have been deleted");
            await requestConnection.SendResponse();
          }
          indexString = (string) null;
        }
        request = (ReceivedHttpRequest) null;
      }
      catch (Exception ex)
      {
        Console.WriteLine("[err] Exception in OnDeleteTSolesQueue: {0}", (object) ex);
        await requestConnection.SendResponse(500, ex.Message, HttpResponseType.PlainText);
      }
    }

    private static async void OnGetTSolesUpdate(
      HttpEndpointUnixSocket unixSocket,
      HttpEndpointConnection requestConnection)
    {
      try
      {
        ReceivedHttpRequest receivedHttpRequest = await requestConnection.ReadRequest(Program.CancelSource.Token);
        receivedHttpRequest = (ReceivedHttpRequest) null;
        Updater.StartUpdate();
        await requestConnection.SendResponse();
      }
      catch (Exception ex)
      {
        Console.WriteLine("[err] Exception in OnGetTSolesUpdate: {0}", (object) ex);
        await requestConnection.SendResponse(500, ex.Message, HttpResponseType.PlainText);
      }
    }

    private static async void OnGetTSolesFilterLog(
      HttpEndpointUnixSocket unixSocket,
      HttpEndpointConnection requestConnection)
    {
      try
      {
        ReceivedHttpRequest request = await requestConnection.ReadRequest(Program.CancelSource.Token);
        string filter;
        if (request.Queries.TryGetValue("filter", out filter))
        {
          string filterResult = await Traceability.FilterLog(filter);
          await requestConnection.SendResponse(200, filterResult, HttpResponseType.PlainText);
          filterResult = (string) null;
        }
        else
          await requestConnection.SendResponse();
        request = (ReceivedHttpRequest) null;
        filter = (string) null;
      }
      catch (Exception ex)
      {
        Console.WriteLine("[err] Exception in OnGetTSolesFilterLog: {0}", (object) ex);
        await requestConnection.SendResponse(500, ex.Message, HttpResponseType.PlainText);
      }
    }
  }
}
