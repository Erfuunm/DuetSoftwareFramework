﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using DuetAPI;
using DuetAPI.Connection;
using DuetAPI.ObjectModel;
using DuetAPI.Utility;
using DuetAPIClient;
using DuetWebServer.Singletons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuetWebServer.Controllers
{
    /// <summary>
    /// MVC Controller for /rr_ requests
    /// </summary>
    [ApiController]
    [Authorize(Policy = Authorization.Policies.ReadOnly)]
    [Route("/")]
    public class RepRapFirmwareController : ControllerBase
    {
        /// <summary>
        /// App configuration
        /// </summary>
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Logger instance
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Host application lifetime
        /// </summary>
        private readonly IHostApplicationLifetime _applicationLifetime;

        /// <summary>
        /// Object model provider
        /// </summary>
        private readonly IModelProvider _modelProvider;

        /// <summary>
        /// Create a new controller instance
        /// </summary>
        /// <param name="configuration">Launch configuration</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="applicationLifetime">Application lifecycle instance</param>
        /// <param name="modelProvider">Model provider</param>
        public RepRapFirmwareController(IConfiguration configuration, ILogger<RepRapFirmwareController> logger, IHostApplicationLifetime applicationLifetime, IModelProvider modelProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _applicationLifetime = applicationLifetime;
            _modelProvider = modelProvider;
        }

        /// <summary>
        /// GET /rr_connect?password={password}
        /// Attempt to create a new connection and log in using the (optional) password
        /// The extra "time" parameter is currently ignored in SBC mode
        /// </summary>
        /// <param name="password">Password to check</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [AllowAnonymous]
        [HttpGet("rr_connect")]
        public async Task<IActionResult> Connect(string password, [FromServices] ISessionStorage sessionStorage)
        {
            try
            {
                using CommandConnection connection = await BuildConnection();
                if (await connection.CheckPassword(password))
                {
                    int sessionId = await connection.AddUserSession(AccessLevel.ReadWrite, SessionType.HTTP, HttpContext.Connection.RemoteIpAddress.ToString());
                    _ = sessionStorage.MakeSessionKey(sessionId, HttpContext.Connection.RemoteIpAddress.ToString(), true);

                    // See RepRapFirmware/src/Platform/Platform.cpp -> Platform::GetBoardString()
                    ObjectModel model = await connection.GetObjectModel();
                    string boardString = model.Boards.First(board => board.CanAddress == 0)?.ShortName switch
                    {
                        "Mini5plus" => "duet5lcunknown",
                        "MB6HC" => "duet3mb6hc100",
                        "MB6XD" => "duet3mb6xd100",
                        "FMDC" => "fmdc",
                        "2WiFi" => "duetwifi10",
                        "2Ethernet" => "duetethernet10",
                        "2SBC" => "duet2sbc10",
                        "2Maestro" => "duetmaestro100",
                        "PC001373" => "pc001373",
                        _ => "unknown"
                    };

                    return Content(JsonSerializer.Serialize(new
                    {
                        apiLevel = 1,
                        err = 0,
                        isEmulated = true,
                        sessionTimeout = 8000,
                        boardType = boardString
                    }), "application/json");
                }
                else
                {
                    _logger.LogWarning("Invalid password");
                    return Content("{\"err\":1,\"isEmulated\":true}", "application/json");
                }
            }
            catch (Exception e)
            {
                if (e is AggregateException ae)
                {
                    e = ae.InnerException;
                }
                if (e is IncompatibleVersionException)
                {
                    _logger.LogError($"[{nameof(Connect)}] Incompatible DCS version");
                    return StatusCode(502, "Incompatible DCS version");
                }
                if (e is SocketException)
                {
                    string startErrorFile = _configuration.GetValue("StartErrorFile", Defaults.StartErrorFile);
                    if (System.IO.File.Exists(startErrorFile))
                    {
                        string startError = await System.IO.File.ReadAllTextAsync(startErrorFile);
                        _logger.LogError($"[{nameof(Connect)}] {startError}");
                        return StatusCode(503, startError);
                    }

                    _logger.LogError($"[{nameof(Connect)}] DCS is not started");
                    return StatusCode(503, "Failed to connect to Duet, please check your connection (DCS is not started)");
                }
                _logger.LogWarning(e, $"[{nameof(Connect)}] Failed to handle connect request");
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// GET /rr_disconnect
        /// Disconnect again from the RepRapFirmware controller
        /// </summary>
        /// <param name="sessionStorage">Session storage singleton</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [AllowAnonymous]
        [HttpGet("rr_disconnect")]
        public async Task<IActionResult> Disconnect([FromServices] ISessionStorage sessionStorage)
        {
            try
            {
                if (HttpContext.User != null)
                {
                    // Remove the internal session
                    int sessionId = sessionStorage.RemoveTicket(HttpContext.User);

                    // Remove the DSF user session again
                    if (sessionId > 0)
                    {
                        using CommandConnection connection = await BuildConnection();
                        await connection.RemoveUserSession(sessionId);
                    }
                }
                return NoContent();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_disconnect request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// GET /rr_gcode?gcode={gcode}
        /// Execute plain G/M/T-code(s) from the request body and return the G-code response when done.
        /// </summary>
        /// <param name="sessionStorage">Session storage singleton</param>
        /// <param name="async">Execute code asynchronously (don't wait for a code result)</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [HttpGet("rr_gcode")]
        [Authorize(Policy = Authorization.Policies.ReadWrite)]
        public async Task<IActionResult> DoCode(string gcode, [FromServices] ISessionStorage sessionStorage)
        {
            try
            {
                using CommandConnection connection = await BuildConnection();
                _logger.LogInformation($"[{nameof(DoCode)}] Executing code '{gcode}'");
                _ = await connection.PerformSimpleCode(gcode, CodeChannel.HTTP, true);
                return Content("{\"bufferSpace\":255,\"err\":0}", "application/json");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_gcode request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// GET /rr_reply
        /// Retrieve the last G-code reply.
        /// Messages are only cached per emulated sessions, so they are not cached before an emulated client actually connects.
        /// </summary>
        /// <returns>Last G-code reply</returns>
        [HttpGet("rr_reply")]
        [Authorize(Policy = Authorization.Policies.ReadWrite)]
        public IActionResult Reply([FromServices] ISessionStorage sessionStorage)
        {
            try
            {
                string reply = sessionStorage.GetCachedMessages(HttpContext.User);
                return Content(reply, "text/plain");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_reply request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// Indicates if the last upload was successful
        /// </summary>
        private bool _lastUploadSuccessful = true;

        /// <summary>
        /// GET /rr_upload
        /// Get the last file upload result.
        /// </summary>
        /// <returns>
        /// HTTP status code:
        /// (200) Error code result
        /// </returns>
        [HttpGet("rr_upload")]
        [Authorize(Policy = Authorization.Policies.ReadOnly)]
        public IActionResult UploadResult() => Content("{\"err\":" + (_lastUploadSuccessful ? '0' : '1') + "}", "application/json");

        /// <summary>
        /// POST /rr_upload?name={filename}
        /// Upload a file from the HTTP body and create the subdirectories if necessary
        /// </summary>
        /// <param name="name">Destination of the file to upload</param>
        /// <param name="sessionStorage">Session storage singleton</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [Authorize(Policy = Authorization.Policies.ReadWrite)]
        [DisableRequestSizeLimit]
        [HttpPost("rr_upload")]
        public async Task<IActionResult> UploadFile(string name, string time, string crc32, [FromServices] ISessionStorage sessionStorage)
        {
            name = HttpUtility.UrlDecode(name);

            try
            {
                sessionStorage.SetLongRunningHttpRequest(HttpContext.User, true);
                try
                {
                    string resolvedPath = await ResolvePath(name);

                    // Create directory if necessary
                    string directory = Path.GetDirectoryName(resolvedPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    uint computedCrc32 = 0;
                    await using (FileStream stream = new(resolvedPath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        // Write file
                        await Request.Body.CopyToAsync(stream);

                        // Compute CRC32 if necessary
                        if (!string.IsNullOrEmpty(crc32))
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            computedCrc32 = await Utility.CRC32.Calculate(stream);
                        }
                    }

                    // Verify CRC32 checksum if necessary
                    if (!string.IsNullOrEmpty(crc32) && !computedCrc32.ToString("x8").Equals(crc32, StringComparison.InvariantCultureIgnoreCase))
                    {
                        _logger.LogWarning("CRC32 check failed in rr_upload ({0} != {1:x8})", crc32, computedCrc32);
                        _lastUploadSuccessful = false;
                        System.IO.File.Delete(resolvedPath);
                        return Content("{\"err\":1}", "application/json");
                    }

                    // Set last modified time if applicable
                    if (!string.IsNullOrEmpty(time) && DateTime.TryParse(time, out DateTime lastModified))
                    {
                        System.IO.File.SetLastWriteTime(resolvedPath, lastModified);
                    }

                    _lastUploadSuccessful = true;
                    return Content("{\"err\":0}", "application/json");
                }
                finally
                {
                    sessionStorage.SetLongRunningHttpRequest(HttpContext.User, false);
                }
            }
            catch (Exception e)
            {
                _lastUploadSuccessful = false;
                _logger.LogError(e, "Failed to handle rr_upload request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// GET /rr_download?name={filename}
        /// Download the specified file.
        /// </summary>
        /// <param name="name">File to download</param>
        /// <returns>
        /// HTTP status code:
        /// (200) File content
        /// (404) File not found
        /// </returns>_
        [HttpGet("rr_download")]
        public async Task<IActionResult> DownloadFile(string name)
        {
            name = HttpUtility.UrlDecode(name);

            try
            {
                string resolvedPath = await ResolvePath(name);
                if (!System.IO.File.Exists(resolvedPath))
                {
                    _logger.LogWarning($"[{nameof(DownloadFile)}] Could not find file {name} (resolved to {resolvedPath})");
                    return NotFound(HttpUtility.UrlPathEncode(name));
                }

                FileStream stream = new(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return File(stream, "application/octet-stream");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_download request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// GET /rr_delete?name={filename}
        /// Delete the given file or directory.
        /// </summary>
        /// <param name="name">File or directory to delete</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [Authorize(Policy = Authorization.Policies.ReadWrite)]
        [HttpGet("rr_delete")]
        public async Task<IActionResult> DeleteFileOrDirectory(string name)
        {
            name = HttpUtility.UrlDecode(name);

            string resolvedPath = "n/a";
            try
            {
                resolvedPath = await ResolvePath(name);

                if (Directory.Exists(resolvedPath))
                {
                    Directory.Delete(resolvedPath);
                    return Content("{\"err\":0}", "application/json");
                }

                if (System.IO.File.Exists(resolvedPath))
                {
                    System.IO.File.Delete(resolvedPath);
                    return Content("{\"err\":0}", "application/json");
                }

                _logger.LogWarning($"[{nameof(DeleteFileOrDirectory)} Could not find file {name} (resolved to {resolvedPath})");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_delete request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// GET /rr_filelist?dir={directory}&first={first}
        /// Retrieve file list
        /// </summary>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [HttpGet("rr_filelist")]
        public async Task<IActionResult> GetFileList(string dir, int first = -1)
        {
            try
            {
                string resolvedPath = await ResolvePath(dir);
                return Content(FileLists.GetFileList(dir, resolvedPath, first), "application/json");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_filelist request");
            }
            return Content("{\"err\":2}");
        }

        /// <summary>
        /// GET /rr_files?dir={directory}&first={first}&flagDirs={flagDirs}
        /// Retrieve files list
        /// </summary>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [HttpGet("rr_files")]
        public async Task<IActionResult> GetFiles(string dir, int first = 0, int flagDirs = 0)
        {
            try
            {
                string resolvedPath = await ResolvePath(dir);
                return Content(FileLists.GetFiles(dir, resolvedPath, first, flagDirs != 0), "application/json");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_filelist request");
            }
            return Content("{\"err\":1}");
        }

        /// <summary>
        /// GET /rr_model?key={key}&flags={flags}
        /// Retrieve object model information
        /// </summary>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// (503) Service Unavailable
        /// </returns>
        [HttpGet("rr_model")]
        public async Task<IActionResult> GetModel(string key = "", string flags = "")
        {
            try
            {
                // Check key and flags for valid chars
                foreach (char c in key)
                {
                    if (!char.IsLetterOrDigit(c) && c != '.' && c != '[' && c != ']')
                    {
                        _logger.LogWarning("Invalid character in rr_model key parameter: '{0}'", c);
                        return Content("{\"err\":1}", "application/json");
                    }
                }
                foreach (char c in flags)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        _logger.LogWarning("Invalid character in rr_model flags parameter: '{0}'", c);
                        return Content("{\"err\":1}", "application/json");
                    }
                }

                // Update special "seqs" values in common live query result, retrieve partial DSF OM, or fall back to M409
                if (string.IsNullOrWhiteSpace(key) && flags.Contains('f'))
                {
                    // Get live values from RRF
                    using CommandConnection connection = await BuildConnection();
                    string response = await connection.PerformSimpleCode($"M409 K\"{key}\" F\"{flags}\"");

                    // Update sequence numbers where applicable
                    using JsonDocument jsonDoc = JsonDocument.Parse(response);
                    if (jsonDoc.RootElement.TryGetProperty("result", out JsonElement resultElement) && resultElement.TryGetProperty("seqs", out JsonElement seqsElement))
                    {
                        Dictionary<string, object> result = JsonSerializer.Deserialize<Dictionary<string, object>>(resultElement.GetRawText());
                        {
                            Dictionary<string, object> seqs = JsonSerializer.Deserialize<Dictionary<string, object>>(seqsElement.GetRawText());
                            lock (_modelProvider)
                            {
                                if (seqs.ContainsKey("reply"))
                                {
                                    seqs["reply"] = _modelProvider.ReplySeq;
                                }
                                if (seqs.ContainsKey("volumes"))
                                {
                                    seqs["volumes"] = _modelProvider.VolumesSeq;
                                }
                            }
                            result["seqs"] = seqs;
                        }

                        return Content(JsonSerializer.Serialize(new
                        {
                            key,
                            flags,
                            result
                        }), "application/json");
                    }

                    // Otherwise pass it on
                    return Content(response, "application/json");
                }
                else if (!key.Contains('[') && !flags.Contains('f'))
                {
                    // If no live parameters are requested, return data from the main DSF object model
                    using SubscribeConnection connection = await BuildSubscribeConnection(key + ".**");
                    using JsonDocument queryResult = await connection.GetObjectModelPatch();

                    // Get down to the requested depth
                    JsonElement result = queryResult.RootElement;
                    foreach (string depth in key.Split('.'))
                    {
                        if (result.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var subItem in result.EnumerateObject())
                            {
                                result = subItem.Value;
                                break;
                            }
                        }
                    }

                    // Return final result
                    return Content(JsonSerializer.Serialize(new
                    {
                        key,
                        flags,
                        result
                    }), "application/json");
                }
                else
                {
                    // Fall back to M409 for now. Note that it may return values which are not provided by DSF!
                    using CommandConnection connection = await BuildConnection();
                    string response = await connection.PerformSimpleCode($"M409 K\"{key}\" F\"{flags}\"");
                    return Content(response, "application/json");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_model request");
            }
            return StatusCode(503);
        }

        /// <summary>
        /// Move a file or directory from a to b
        /// </summary>
        /// <param name="old">Source path</param>
        /// <param name="new">Destination path</param>
        /// <param name="deleteexisting">Delete existing file (optional, default "no")</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [Authorize(Policy = Authorization.Policies.ReadWrite)]
        [HttpGet("rr_move")]
        public async Task<IActionResult> MoveFileOrDirectory(string old, string @new, string deleteexisting = "no")
        {
            try
            {
                string source = await ResolvePath(old), destination = await ResolvePath(@new);

                // Deal with directories
                if (Directory.Exists(source))
                {
                    if (Directory.Exists(destination))
                    {
                        if (deleteexisting == "yes")
                        {
                            Directory.Delete(destination);
                        }
                        else
                        {
                            _logger.LogWarning("Directory {0} already exists", old);
                            return Content("{\"err\":1}", "application/json");
                        }
                    }

                    Directory.Move(source, destination);
                    return Content("{\"err\":0}", "application/json");
                }

                // Deal with files
                if (System.IO.File.Exists(source))
                {
                    if (System.IO.File.Exists(destination))
                    {
                        if (deleteexisting == "yes")
                        {
                            System.IO.File.Delete(destination);
                        }
                        else
                        {
                            _logger.LogWarning("File {0} already exists", old);
                            return Content("{\"err\":1}", "application/json");
                        }
                    }

                    System.IO.File.Move(source, destination);
                    return Content("{\"err\":0}", "application/json");
                }

                _logger.LogWarning("File or directory {0} not found in rr_move", old);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_move request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// GET /rr_mkdir?dir={dir}
        /// Create the given directory.
        /// </summary>
        /// <param name="dir">Directory to create</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [Authorize(Policy = Authorization.Policies.ReadWrite)]
        [HttpGet("rr_mkdir")]
        public async Task<IActionResult> CreateDirectory(string dir)
        {
            try
            {
                string resolvedPath = await ResolvePath(dir);
                Directory.CreateDirectory(resolvedPath);
                return Content("{\"err\":0}", "application/json");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_mkdir request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// Last queried file info
        /// </summary>
        private GCodeFileInfo _lastFileInfo;

        /// <summary>
        /// GET /rr_fileinfo?name={filename}
        /// Parse a given G-code file and return information about this job file as a JSON object.
        /// If name is omitted, info about the file being printed is returned.
        /// </summary>
        /// <param name="filename">Optional G-code file to analyze</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [HttpGet("rr_fileinfo")]
        public async Task<IActionResult> GetFileInfo(string name = "")
        {
            try
            {
                // Filename defaults to the file being printed if it is not present
                int? printDuration = null;
                using CommandConnection connection = await BuildConnection();
                if (string.IsNullOrEmpty(name))
                {
                    ObjectModel model = await connection.GetObjectModel();
                    if (string.IsNullOrEmpty(model.Job.File.FileName))
                    {
                        // Not printing a file, cannot get fileinfo
                        return Content("{\"err\":1}", "application/json");
                    }
                    name = model.Job.File.FileName;
                    printDuration = model.Job.Duration;
                }

                // Get fileinfo
                string resolvedPath = await connection.ResolvePath(name);
                if (!System.IO.File.Exists(resolvedPath))
                {
                    _logger.LogWarning($"[{nameof(GetFileInfo)}] Could not find file {name} (resolved to {resolvedPath})");
                    return Content("{\"err\":1}", "application/json");
                }
                GCodeFileInfo info = await connection.GetFileInfo(resolvedPath, true);
                lock (this)
                {
                    _lastFileInfo = info;
                    _lastFileInfo.FileName = resolvedPath;
                }

                // Return it in RRF format
                Dictionary<string, object> result = new()
                {
                    { "err", 0 },
                    { "fileName", name },
                    { "size", info.Size }
                };
                if (info.LastModified != null)
                {
                    result.Add("lastModified", info.LastModified.Value.ToString("s"));
                }
                result.Add("height", Math.Round(info.Height, 2));
                result.Add("layerHeight", Math.Round(info.LayerHeight, 2));
                result.Add("numLayers", info.NumLayers);
                if (info.PrintTime != null)
                {
                    result.Add("printTime", info.PrintTime.Value);
                }
                if (info.SimulatedTime != null)
                {
                    result.Add("simulatedTime", info.SimulatedTime.Value);
                }
                if (info.Filament.Count > 0)
                {
                    result.Add("filament", info.Filament.Select(val => Math.Round(val, 1)).ToArray());
                }
                if (printDuration != null)
                {
                    result.Add("printDuration", printDuration.Value);
                }
                if (info.Thumbnails.Count > 0)
                {
                    List<object> thumbnails = new();
                    foreach (ThumbnailInfo thumbnail in info.Thumbnails)
                    {
                        thumbnails.Add(new
                        {
                            width = thumbnail.Width,
                            height = thumbnail.Height,
                            format = thumbnail.Format switch
                            {
                                ThumbnailInfoFormat.PNG => "png",
                                ThumbnailInfoFormat.JPEG => "jpeg",
                                ThumbnailInfoFormat.QOI => "qoi",
                                _ => "unknown"
                            },
                            offset = thumbnail.Offset,
                            size = thumbnail.Size
                        });
                    }
                    result.Add("thumbnails", thumbnails);
                }
                result.Add("generatedBy", info.GeneratedBy);
                return Content(JsonSerializer.Serialize(result), "application/json");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_fileinfo request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        /// <summary>
        /// GET /rr_thumbnail?name={filename}&offset={offset}
        /// Get the thumbnail from a given filename
        /// </summary>
        /// <param name="filename">G-code file to read thumbnails from</param>
        /// <param name="offset">Start offset of the thumbnail query</param>
        /// <returns>
        /// HTTP status code:
        /// (200) JSON response
        /// </returns>
        [HttpGet("rr_thumbnail")]
        public async Task<IActionResult> GetThumbnail(string name, long offset)
        {
            try
            {
                // Filename defaults to the file being printed if it is not present
                using CommandConnection connection = await BuildConnection();
                if (string.IsNullOrEmpty(name))
                {
                    _logger.LogWarning("Missing name parameter in rr_thumbnail");
                    return Content("{\"err\":1}", "application/json");
                }

                // Get actual filename
                string resolvedPath = await connection.ResolvePath(name);
                if (!System.IO.File.Exists(resolvedPath))
                {
                    _logger.LogWarning($"[{nameof(GetThumbnail)}] Could not find file {name} (resolved to {resolvedPath})");
                    return Content("{\"err\":1}", "application/json");
                }

                // Get fileinfo and cache it
                GCodeFileInfo info = null;
                lock (this)
                {
                    if (_lastFileInfo != null && _lastFileInfo.FileName == resolvedPath)
                    {
                        info = _lastFileInfo;
                    }
                }
                if (info == null)
                {
                    info = await connection.GetFileInfo(resolvedPath, true);
                    lock (this)
                    {
                        _lastFileInfo = info;
                        _lastFileInfo.FileName = resolvedPath;
                    }
                }

                // Get corresponding thumbnail
                string data = null;
                foreach (ThumbnailInfo item in info.Thumbnails)
                {
                    if (item.Offset >= offset && offset < item.Offset + item.Size)
                    {
                        // NB: This only works because base64 data consists only of ASCII characters
                        data = item.Data?[(int)(offset - item.Offset)..];
                        break;
                    }
                }

                // Return result
                if (data == null)
                {
                    _logger.LogWarning("Failed to find corresponding thumbnail in rr_thumbnail");
                    return Content("{\"err\":1}", "application/json");
                }
                return Content(JsonSerializer.Serialize(new
                {
                    fileName = name,
                    offset,
                    data,
                    next = 0,
                    err = 0
                }), "application/json");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle rr_thumbnail request");
            }
            return Content("{\"err\":1}", "application/json");
        }

        private async Task<CommandConnection> BuildConnection()
        {
            CommandConnection connection = new();
            await connection.Connect(_configuration.GetValue("SocketPath", Defaults.FullSocketPath));
            return connection;
        }

        private async Task<SubscribeConnection> BuildSubscribeConnection(string filter)
        {
            SubscribeConnection connection = new();
            await connection.Connect(SubscriptionMode.Patch, new string[] { filter }, _configuration.GetValue("SocketPath", Defaults.FullSocketPath));
            return connection;
        }

        private async Task<string> ResolvePath(string path)
        {
            using CommandConnection connection = await BuildConnection();
            return await connection.ResolvePath(path);
        }
    }
}