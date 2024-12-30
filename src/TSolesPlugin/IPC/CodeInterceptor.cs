// Decompiled with JetBrains decompiler
// Type: TSolesPlugin.IPC.CodeInterceptor
// Assembly: TSolesPlugin, Version=3.4.1.0, Culture=neutral, PublicKeyToken=null
// MVID: 943EFD72-12B6-4D53-BB0D-FD8C4C742C73
// Assembly location: C:\Users\user\Downloads\Telegram Desktop\opt directory (T-Soles)\bin\TSolesPlugin.dll

using DuetAPI;
using DuetAPI.Commands;
using DuetAPI.Connection;
using DuetAPI.ObjectModel;
using DuetAPIClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;


#nullable enable
namespace TSolesPlugin.IPC
{
  public static class CodeInterceptor
  {
    private const string StartCode = "startprinting";
    private const string EndCode = "endprinting";
    private static volatile bool _inIntermediateSegment;

    public static bool InIntermediateSegment
    {
      get => CodeInterceptor._inIntermediateSegment;
      set => CodeInterceptor._inIntermediateSegment = value;
    }

    public static async Task Run()
    {
      // ISSUE: reference to a compiler-generated field
      //int num1 = this.\u003C\u003E1__state;
      while (true)
      {
        bool wasConnected = false;
        try
        {
          using (InterceptConnection interceptConnection = new InterceptConnection())
          {
            await interceptConnection.Connect(InterceptionMode.Pre, (IEnumerable<CodeChannel>) null, (IEnumerable<string>) null, false, Program.SocketPath, Program.CancelSource.Token);
            Console.WriteLine("[info] Subscriber connected");
            wasConnected = true;
            do
            {
              Code code = await interceptConnection.ReceiveCode(Program.CancelSource.Token);
              int num2;
              if (code.Type == CodeType.MCode)
              {
                int? majorNumber = code.MajorNumber;
                int num3 = 24;
                if (majorNumber.GetValueOrDefault() == num3 & majorNumber.HasValue)
                {
                  num2 = (int) code.Parameter('J', (object) 0) > 0 ? 1 : 0;
                  goto label_10;
                }
              }
              num2 = 0;
label_10:
              if (num2 != 0)
              {
                await ModelObserver.Resume();
                await interceptConnection.ResolveCode(MessageType.Success, "");
              }
              else if (code.Channel == CodeChannel.File && !code.Flags.HasFlag((Enum) CodeFlags.IsFromMacro))
              {
                if (code.Type == CodeType.Comment && !string.IsNullOrWhiteSpace(code.Comment))
                {
                  string comment = code.Comment.Trim();
                  if (CodeInterceptor.InIntermediateSegment && comment.Equals("startprinting", StringComparison.InvariantCultureIgnoreCase))
                  {
                    Console.WriteLine("[info] Resuming code execution");
                    CodeInterceptor.InIntermediateSegment = false;
                  }
                  else if (!CodeInterceptor.InIntermediateSegment && comment.Equals("endprinting", StringComparison.InvariantCultureIgnoreCase))
                  {
                    using (await JobQueue.LockAsync())
                    {
                      if (!JobQueue.ProcessingLastJob)
                      {
                        Console.WriteLine("[info] Skipping code execution");
                        CodeInterceptor.InIntermediateSegment = true;
                      }
                    }
                  }
                  await interceptConnection.IgnoreCode();
                  comment = (string) null;
                }
                else if (CodeInterceptor.InIntermediateSegment)
                  await interceptConnection.ResolveCode(MessageType.Success, "");
                else
                  await interceptConnection.IgnoreCode();
              }
              else
                await interceptConnection.IgnoreCode();
              code = (Code) null;
            }
            while (interceptConnection.IsConnected);
          }
        }
        catch (Exception ex) when (ex is IOException || ex is SocketException)
        {
          if (wasConnected)
            Console.WriteLine("[warn] Code interceptor lost connection to DCS");
          else if (!(ex is SocketException))
            Console.WriteLine("[warn] Failed to set up code interception");
        }
        CodeInterceptor.InIntermediateSegment = false;
        await Task.Delay(2000, Program.CancelSource.Token);
      }
    }
  }
}
