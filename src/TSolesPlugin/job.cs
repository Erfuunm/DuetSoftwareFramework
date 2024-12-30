// Decompiled with JetBrains decompiler
// Type: TSolesPlugin.Job
// Assembly: TSolesPlugin, Version=3.4.1.0, Culture=neutral, PublicKeyToken=null
// MVID: 943EFD72-12B6-4D53-BB0D-FD8C4C742C73
// Assembly location: D:\opt directory (T-Soles)\dsf\bin\TSolesPlugin.dll

#nullable enable
namespace TSolesPlugin
{
    public class Job
    {
        public string Filename { get; set; } = string.Empty;

        public bool Active { get; set; }

        public bool ExternallyStarted { get; set; }

        public int Completed { get; set; }

        public int Total { get; set; }

        public long? PrintTime { get; set; }
    }
}