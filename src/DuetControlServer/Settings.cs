﻿using DuetAPI.Utility;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DuetControlServer
{
    /// <summary>
    /// Settings provider
    /// </summary>
    public static class Settings
    {
        private const string DefaultConfigFile = "/opt/dsf/conf/config.json";
        private const string DefaultPluginsFile = "/opt/dsf/conf/plugins.txt";
        private const RegexOptions RegexFlags = RegexOptions.IgnoreCase | RegexOptions.Singleline;

        /// <summary>
        /// Defines whether the mainboard and expansion boards may be updated automatically during unattended upgrades
        /// </summary>
        public static bool AutoUpdateFirmware { get; set; } = true;

        /// <summary>
        /// Indicates if this program is only launched to update the board firmware
        /// </summary>
        [JsonIgnore]
        public static bool UpdateOnly { get; set; }

        /// <summary>
        /// Do NOT start the SPI task. This is meant entirely for development purposes and should not be used!
        /// </summary>
        [JsonIgnore]
        public static bool NoSpi { get; set; }

        /// <summary>
        /// Path to the configuration file
        /// </summary>
        [JsonIgnore]
        public static string ConfigFilename { get; set; } = DefaultConfigFile;

        /// <summary>
        /// Whether this DCS instance may support third-party plugins.
        /// If this is set to false, dsf-config.g will be run right after the start
        /// </summary>
        public static bool PluginSupport { get; set; } = true;

        /// <summary>
        /// Whether this DCS instance may support third-party root plugins.
        /// This is only respected if <see cref="PluginSupport"/> is set to true
        /// </summary>
        public static bool RootPluginSupport { get; set; }

        /// <summary>
        /// Path to the file holding a list of loaded plugins
        /// </summary>
        public static string PluginsFilename { get; set; } = DefaultPluginsFile;

        /// <summary>
        /// Time to wait before auto-restarting a stopped plugin that has the SbcAutoRestart option set
        /// </summary>
        public static int PluginAutoRestartInterval { get; set; } = 2000;

        /// <summary>
        /// Minimum log level for console output
        /// </summary>
        public static LogLevel LogLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// Directory in which DSF-related UNIX sockets reside
        /// </summary>
        public static string SocketDirectory { get; set; } = DuetAPI.Connection.Defaults.SocketDirectory;

        /// <summary>
        /// UNIX socket file for DuetControlServer
        /// </summary>
        /// <seealso cref="DuetAPI"/>
        public static string SocketFile { get; set; } = DuetAPI.Connection.Defaults.SocketFile;

        /// <summary>
        /// Fully-qualified path to the main IPC UNIX socket (evaluated during runtime)
        /// </summary>
        [JsonIgnore]
        public static string FullSocketPath => Path.Combine(SocketDirectory, SocketFile);

        /// <summary>
        /// File to contain the last start error of DCS. Once DCS starts successfully, it is deleted
        /// </summary>
        public static string StartErrorFile { get; set; } = DuetAPI.Connection.Defaults.StartErrorFile;

        /// <summary>
        /// Maximum number of simultaneously pending IPC connections
        /// </summary>
        public static int Backlog { get; set; } = 4;

        /// <summary>
        /// Poll interval for connected IPC clients (in ms)
        /// </summary>
        public static int SocketPollInterval { get; set; } = 2000;

        /// <summary>
        /// Virtual SD card directory.
        /// Paths starting with 0:/ are mapped to this directory
        /// </summary>
        public static string BaseDirectory { get; set; } = "/opt/dsf/sd";

        /// <summary>
        /// Directory holding DSF plugins
        /// </summary>
        /// <remarks>
        /// This directory is not created by the DCS package. It is provided by DPS
        /// </remarks>
        public static string PluginDirectory { get; set; } = "/opt/dsf/plugins";

        /// <summary>
        /// Set this to true to prevent M999 from stopping this application
        /// </summary>
        public static bool NoTerminateOnReset { get; set; }

        /// <summary>
        /// Internal model update interval after which properties of the machine model from
        /// the host controller (e.g. network information and mass storage devices) are updated (in ms)
        /// </summary>
        public static int HostUpdateInterval { get; set; } = 4000;

        /// <summary>
        /// Maximum time to keep messages in the object model unless client(s) pick them up (in s).
        /// Note that messages are only cleared when the host update task runs.
        /// </summary>
        public static double MaxMessageAge { get; set; } = 60.0;

        /// <summary>
        /// SPI device that is connected to RepRapFirmware
        /// </summary>
        public static string SpiDevice { get; set; } = "/dev/spidev0.0";

        /// <summary>
        /// SPI Tx and Rx buffer size
        /// Should not be greater than the kernel spidev buffer size
        /// </summary>
        public static int SpiBufferSize { get; set; } = SPI.Communication.Consts.BufferSize;

        /// <summary>
        /// SPI Transfer Mode 0-3
        /// </summary>
        public static int SpiTransferMode { get; set; } = 0;

        /// <summary>
        /// Frequency to use for SPI transfers (in Hz)
        /// </summary>
        public static int SpiFrequency { get; set; } = 8_000_000;

        /// <summary>
        /// Maximum allowed time when waiting for the first SPI transfer (in ms)
        /// </summary>
        public static int SpiConnectTimeout { get; set; } = 500;

        /// <summary>
        /// Maximum allowed delay between data exchanges during a full transfer (in ms)
        /// </summary>
        public static int SpiTransferTimeout { get; set; } = 500;

        /// <summary>
        /// Maximum allowed delay between full transfers (in ms)
        /// </summary>
        public static int SpiConnectionTimeout { get; set; } = 4000;

        /// <summary>
        /// Maximum number of sequential transfer retries
        /// </summary>
        public static int MaxSpiRetries { get; set; } = 3;

        /// <summary>
        /// Path to the GPIO chip device node
        /// </summary>
        public static string GpioChipDevice { get; set; } = "/dev/gpiochip0";

        /// <summary>
        /// Number of the GPIO pin that is used by RepRapFirmware to flag its ready state
        /// </summary>
        public static int TransferReadyPin { get; set; } = 25;      // Pin 22 on the RaspPi expansion header

        /// <summary>
        /// File containing the current CPU temperature
        /// </summary>
        public static string CpuTemperaturePath { get; set; } = "/sys/class/thermal/thermal_zone0/temp";

        /// <summary>
        /// Divide numeric value of <see cref="CpuTemperaturePath"/> by this
        /// </summary>
        public static float CpuTemperatureDivider { get; set; } = 1000F;

        /// <summary>
        /// Number of codes to buffer in the internal print subsystem
        /// </summary>
        public static int BufferedPrintCodes { get; set; } = 32;

        /// <summary>
        /// Number of codes to buffer per macro
        /// </summary>
        public static int BufferedMacroCodes { get; set; } = 16;

        /// <summary>
        /// Maximum number of pending codes per code channel
        /// </summary>
        public static int MaxCodesPerInput { get; set; } = 32;

        /// <summary>
        /// Maximum space of buffered codes per channel (in bytes)
        /// </summary>
        public static int MaxBufferSpacePerChannel { get; set; } = 1536;

        /// <summary>
        /// Maximum size of a binary encoded G/M/T-code. This is limited by RepRapFirmware (see code queue)
        /// </summary>
        public static int MaxCodeBufferSize { get; set; } = 256;

        /// <summary>
        /// Maximum supported length of messages to be sent to RepRapFirmware
        /// </summary>
        public static int MaxMessageLength { get; set; } = 4096;

        /// <summary>
        /// List of string chunks that are identified by RepRapFirmware
        /// </summary>
        /// <remarks>
        /// Only if a comment contains one of these identifiers they will be sent to the firmware
        /// </remarks>
        public static List<string> FirmwareComments { get; set; } = new()
        {
            "printing object",			// slic3r
            "MESH",						// Cura
            "process",					// S3D
            "stop printing object",		// slic3r
            "layer",					// S3D "; layer 1, z=0.200"
            "LAYER",					// Ideamaker, Cura (followed by layer number starting at zero)
            "BEGIN_LAYER_OBJECT z=",	// KISSlicer (followed by Z height)
            "HEIGHT",					// Ideamaker
            "PRINTING",					// Ideamaker
            "REMAINING_TIME"			// Ideamaker
        };

        /// <summary>
        /// Interval of object model updates (in ms)
        /// </summary>
        public static int ModelUpdateInterval { get; set; } = 250;

        /// <summary>
        /// Maximum lock time of the object model. If this time is exceeded, a deadlock is reported and the application is terminated.
        /// Set this to -1 to disable the automatic deadlock detection
        /// </summary>
        public static int MaxMachineModelLockTime { get; set; } = -1;

        /// <summary>
        /// Size of the read buffer used when reading from files (in bytes)
        /// </summary>
        public static int FileBufferSize { get; set; } = 32768;

        /// <summary>
        /// How many bytes to parse max at the beginning of a file to retrieve G-code file information (in bytes)
        /// </summary>
        public static int FileInfoReadLimitHeader { get; set; } = 16384;

        /// <summary>
        /// How many bytes to parse max at the end of a file to retrieve G-code file information (in bytes)
        /// </summary>
        public static int FileInfoReadLimitFooter { get; set; } = 262144;

        /// <summary>
        /// Maximum allowed layer height. Used by the file info parser
        /// </summary>
        public static double MaxLayerHeight { get; set; } = 0.9;

        /// <summary>
        /// Regular expressions for finding the layer height (case insensitive)
        /// </summary>
        public static List<Regex> LayerHeightFilters { get; set; } = new()
        {
            new Regex(@"^\s*layer_height\D+(?<mm>(\d+\.?\d*))", RegexFlags),            // Slic3r / Prusa Slicer
            new Regex(@"Layer height\D+(?<mm>(\d+\.?\d*))", RegexFlags),                // Cura
            new Regex(@"layerHeight\D+(?<mm>(\d+\.?\d*))", RegexFlags),                 // Simplify3D
            new Regex(@"layer_thickness_mm\D+(?<mm>(\d+\.?\d*))", RegexFlags),          // KISSlicer and Canvas
            new Regex(@"layerThickness\D+(?<mm>(\d+\.?\d*))", RegexFlags),              // Matter Control
            new Regex(@"sliceHeight\D+(?<mm>(\d+\.?\d*))", RegexFlags)                  // Kiri:Moto
        };

        /// <summary>
        /// Regular expressions for finding the total number of layers
        /// </summary>
        /// <remarks>
        /// If the number of layers cannot be found, the total number of layers is calculated from the layer and object heights (if applicable)
        /// </remarks>
        public static List<Regex> NumLayersFilters { get; set; } = new()
        {
            new Regex(@"NUM_LAYERS\D+(\d+)", RegexFlags)
        };

        /// <summary>
        /// Regular expressions for finding the filament consumption (case insensitive, single line)
        /// </summary>
        public static List<Regex> FilamentFilters { get; set; } = new()
        {
            new Regex(@"filament used\D+(((?<mm>\d+\.?\d*)\s*mm)(\D+)?)+", RegexFlags),                     // Slic3r and Kiri:Moto (mm)
            new Regex(@"filament used\D+(((?<m>\d+\.?\d*)m([^m]|$))(\D+)?)+", RegexFlags),                  // Cura (m)
            new Regex(@"filament length\D+(((?<mm>\d+\.?\d*)\s*mm)(\D+)?)+", RegexFlags),                   // Simplify3D (mm)
            new Regex(@"filament used \[mm\]\D+((?<mm>\d+\.?\d*)(\D+)?)+", RegexFlags),                     // Prusa Slicer (mm)
            new Regex(@"material\#(?<index>\d+)\D+(?<mm>\d+\.?\d*)", RegexFlags),                           // IdeaMaker (mm)
            new Regex(@"Ext\s*\#\d+\D+(?<mm>\d+\.?\d*)", RegexFlags),                                       // KISSSlicer v2.0 (mm)
            new Regex(@"Filament used per extruder:\r\n;\s*(?<name>.+)\s+=\s*(?<mm>[0-9.]+)", RegexFlags),  // Canvas
            new Regex(@"filament used extruder (?<index>\d+) \(mm\) = (?<mm>\d+\.?\d*)", RegexFlags)        // MatterControl v2
        };

        /// <summary>
        /// Regular expressions for finding the slicer (case insensitive)
        /// </summary>
        public static List<Regex> GeneratedByFilters { get; set; } = new()
        {
            new Regex(@"generated by\s+(.+)", RegexFlags),                              // Slic3r, Simplify3D, Kiri:Moto
            new Regex(@"Sliced by\s+(.+)", RegexFlags),                                 // IdeaMaker and Canvas
            new Regex(@"(KISSlicer.*)", RegexFlags),                                    // KISSlicer
            new Regex(@"Sliced at:\s*(.+)", RegexFlags),                                // Cura (old)
            new Regex(@"Generated with\s*(.+)", RegexFlags)                             // Cura (new)
        };

        /// <summary>
        /// Regular expressions for finding the print time
        /// </summary>
        public static List<Regex> PrintTimeFilters { get; set; } = new()
        {
            new Regex(@"estimated printing time .*= ((?<d>(\d+))d\s*)?((?<h>(\d+))h\s*)?((?<m>(\d+))m\s*)?((?<s>(\d+))s)?", RegexFlags),                // Slic3r PE
            new Regex(@"TIME:(?<s>(\d+\.?\d*))", RegexFlags),                                                                                           // Cura
            new Regex(@"Build Time:\s+((?<h>(\d+\.?\d*)) hour(s)?\s*)?((?<m>(\d+\.?\d*)) minute(s)?\s*)?((?<s>(\d+\.?\d*)) second(s)?)?", RegexFlags),  // Simplify3D, KISSlicer, Canvas, IceSL
            new Regex(@"print time:\s+(?<s>(\d+\.?\d*))(s)?", RegexFlags),                                                                              // Kiri:Moto, and IdeaMaker v4
            new Regex(@"Total estimated \(pre-cool\) minutes: ((?<m>\d+\.?\d*))", RegexFlags),                                                          // KISSlicer v2.0
            new Regex(@"total print time \(s\) = (?<s>(\d+\.?\d*))", RegexFlags),                                                                       // MatterControl v2
            new Regex(@"Build time:\s+(?<h>(\d+\.?\d*)):(?<m>(\d+\.?\d*)):(?<s>(\d+\.?\d*))", RegexFlags)                                               // REACTOR
        };

        /// <summary>
        /// Regular expressions for finding the simulated time
        /// </summary>
        public static List<Regex> SimulatedTimeFilters { get; set; } = new()
        {
            new Regex(@"Simulated print time\D+(?<s>(\d+\.?\d*))", RegexFlags)
        };

        /// <summary>
        /// Initialize settings and load them from the config file or create it if it does not exist
        /// </summary>
        /// <returns>False if the application is supposed to terminate</returns>
        public static bool Init(string[] args)
        {
            // Check if a custom config is supposed to be loaded
            string? lastArg = null;
            foreach (string arg in args)
            {
                if (lastArg == "-c" || lastArg == "--config")
                {
                    ConfigFilename = arg;
                }
                else if (arg == "-h" || arg == "--help")
                {
                    Console.WriteLine("Available command line arguments:");
                    Console.WriteLine("-u, --update: Update RepRapFirmware and exit");
                    Console.WriteLine("-l, --log-level [trace,debug,info,warn,error,fatal,off]: Set minimum log level");
                    Console.WriteLine("-c, --config: Override path to the JSON config file");
                    Console.WriteLine("-r, --no-reset-stop: Do not terminate this application when M999 has been processed");
                    Console.WriteLine("-S, --socket-directory: Specify the UNIX socket directory");
                    Console.WriteLine("-s, --socket-file: Specify the UNIX socket file");
                    Console.WriteLine("-b, --base-directory: Set the virtual SD card base directory");
                    Console.WriteLine("-D, --no-spi: Do NOT connect over SPI. Not recommended, use at your own risk!");
                    Console.WriteLine("-h, --help: Display this text");
                    return false;
                }
                lastArg = arg;
            }

            // See if the file exists and attempt to load the settings from it, otherwise create it
            try
            {
                if (File.Exists(ConfigFilename))
                {
                    LoadFromFile(ConfigFilename);
                    ParseParameters(args);
                }
                else
                {
                    ParseParameters(args);
                    SaveToFile(ConfigFilename);
                }
            }
            finally
            {
                // Initialize logging
                LoggingConfiguration logConfig = new();
                ColoredConsoleTarget logConsoleTarget = new()
                {
                    // Create a layout for messages like:
                    // [trace] Really verbose stuff
                    // [debug] Verbose debugging stuff
                    // [info] This is a regular log message
                    // [warning] Something not too nice
                    // [error] IPC#3: This is an IPC error message
                    //         System.Exception: Foobar
                    //         at { ... }
                    // [error] That is some other error message
                    //         System.Exception: Yada yada
                    //         at { ... }
                    // [fatal] System.Exception: Blah blah
                    //         at { ... }
                    Layout = @"[${level:lowercase=true}] ${when:when=!contains('${logger}','.') and !ends-with('${logger}','.g'):inner=${logger}${literal:text=\:} }${message}${onexception:when='${message}'!='${exception:format=ToString}'):${newline}   ${exception:format=ToString}}"
                };
                logConfig.AddRule(LogLevel, LogLevel.Fatal, logConsoleTarget);
                LogManager.AutoShutdown = false;
                LogManager.Configuration = logConfig;
            }

            if (UpdateOnly && Console.IsInputRedirected && !AutoUpdateFirmware)
            {
                // Do not start DCS if no firmware updates are supposed to be installed during unattended updates
                return false;
            }

            // Go on
            return true;
        }

        /// <summary>
        /// Parse the command line parameters
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        private static void ParseParameters(string[] args)
        {
            string? lastArg = null;
            foreach (string arg in args)
            {
                if (lastArg == "-l" || lastArg == "--log-level")
                {
                    LogLevel = LogLevel.FromString(arg);
                }
                else if (lastArg == "-S" || lastArg == "--socket-directory")
                {
                    SocketDirectory = arg;
                }
                else if (lastArg == "-s" || lastArg == "--socket-file")
                {
                    SocketFile = arg;
                }
                else if (lastArg == "-b" || lastArg == "--base-directory")
                {
                    BaseDirectory = arg;
                }
                else if (arg == "-u" || arg == "--update")
                {
                    UpdateOnly = true;
                }
                else if (arg == "-r" || arg == "--no-terminate-on-reset")
                {
                    NoTerminateOnReset = true;
                }
                else if (arg == "-D" || arg == "--no-spi")
                {
                    NoSpi = true;
                }
                lastArg = arg;
            }
        }

        /// <summary>
        /// Load the settings from a given file
        /// </summary>
        /// <param name="fileName">File to load the settings from</param>
        private static void LoadFromFile(string fileName)
        {
            byte[] content;
            using (FileStream fileStream = new(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileBufferSize))
            {
                content = new byte[fileStream.Length];
                _ = fileStream.Read(content, 0, (int)fileStream.Length);
            }

            Utf8JsonReader reader = new(content);
            PropertyInfo? property = null;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        string propertyName = reader.GetString()!;
                        property = typeof(Settings).GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                        if (property is null || Attribute.IsDefined(property, typeof(JsonIgnoreAttribute)))
                        {
                            // Skip non-existent and ignored properties
                            if (reader.Read())
                            {
                                if (reader.TokenType == JsonTokenType.StartArray)
                                {
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) { }
                                }
                                else if (reader.TokenType == JsonTokenType.StartObject)
                                {
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) { }
                                }
                            }
                        }
                        break;

                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        if (property?.PropertyType == typeof(bool))
                        {
                            property.SetValue(null, reader.GetBoolean());
                        }
                        else
                        {
                            throw new JsonException($"Bad boolean type: {property?.PropertyType.Name}");
                        }
                        break;

                    case JsonTokenType.Number:
                        if (property?.PropertyType == typeof(int))
                        {
                            property.SetValue(null, reader.GetInt32());
                        }
                        else if (property?.PropertyType == typeof(uint))
                        {
                            property.SetValue(null, reader.GetUInt32());
                        }
                        else if (property?.PropertyType == typeof(float))
                        {
                            property.SetValue(null, reader.GetSingle());
                        }
                        else if (property?.PropertyType == typeof(double))
                        {
                            property.SetValue(null, reader.GetDouble());
                        }
                        else
                        {
                            throw new JsonException($"Bad number type: {property?.PropertyType.Name}");
                        }
                        break;

                    case JsonTokenType.String:
                        if (property?.PropertyType == typeof(string))
                        {
                            property.SetValue(null, reader.GetString());
                        }
                        else if (property?.PropertyType == typeof(LogLevel))
                        {
                            property.SetValue(null, LogLevel.FromString(reader.GetString()));
                        }
                        else
                        {
                            throw new JsonException($"Bad string type: {property?.PropertyType.Name}");
                        }
                        break;

                    case JsonTokenType.StartArray:
                        if (property?.PropertyType == typeof(List<Regex>))
                        {
                            JsonRegexListConverter regexListConverter = new();
                            property.SetValue(null, regexListConverter.Read(ref reader, typeof(List<Regex>), null));
                        }
                        else if (property?.PropertyType == typeof(List<string>))
                        {
                            List<string> list = new();
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                list.Add(reader.GetString()!);
                            }
                            property.SetValue(null, list);
                        }
                        else
                        {
                            throw new JsonException($"Bad list type: {property?.PropertyType.Name}");
                        }
                        break;
                }
            }

            if (!NoSpi && (SpiDevice == "/dev/null" || GpioChipDevice == "/dev/null"))
            {
                // Do NOT start the SPI subsystem if one of the used devices is disabled
                NoSpi = true;
            }
        }

        /// <summary>
        /// Save the settings to a given file
        /// </summary>
        /// <param name="fileName">File to save the settings to</param>
        private static void SaveToFile(string fileName)
        {
            using FileStream fileStream = new(fileName, FileMode.Create, FileAccess.Write, FileShare.None, FileBufferSize);
            using Utf8JsonWriter writer = new(fileStream, new JsonWriterOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true
            });

            writer.WriteStartObject();
            foreach (PropertyInfo property in typeof(Settings).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                if (!Attribute.IsDefined(property, typeof(JsonIgnoreAttribute)))
                {
                    object? value = property.GetValue(null);
                    if (value is string stringValue)
                    {
                        writer.WriteString(property.Name, stringValue);
                    }
                    else if (value is bool boolValue)
                    {
                        writer.WriteBoolean(property.Name, boolValue);
                    }
                    else if (value is int intValue)
                    {
                        writer.WriteNumber(property.Name, intValue);
                    }
                    else if (value is uint uintValue)
                    {
                        writer.WriteNumber(property.Name, uintValue);
                    }
                    else if (value is float floatValue)
                    {
                        writer.WriteNumber(property.Name, floatValue);
                    }
                    else if (value is double doubleValue)
                    {
                        writer.WriteNumber(property.Name, doubleValue);
                    }
                    else if (value is List<Regex> regexList)
                    {
                        writer.WritePropertyName(property.Name);

                        JsonRegexListConverter regexListConverter = new();
                        regexListConverter.Write(writer, regexList, null);
                    }
                    else if (value is List<string> stringList)
                    {
                        writer.WritePropertyName(property.Name);
                        writer.WriteStartArray();
                        foreach (string item in stringList)
                        {
                            writer.WriteStringValue(item);
                        }
                        writer.WriteEndArray();
                    }
                    else if (value is LogLevel logLevelValue)
                    {
                        writer.WriteString(property.Name, logLevelValue.ToString().ToLowerInvariant());
                    }
                    else
                    {
                        throw new JsonException($"Unknown value type {property.PropertyType.Name}");
                    }
                }
            }
            writer.WriteEndObject();
        }
    }
}
