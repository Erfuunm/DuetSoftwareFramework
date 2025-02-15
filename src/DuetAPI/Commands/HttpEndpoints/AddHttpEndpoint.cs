﻿using DuetAPI.ObjectModel;
using DuetAPI.Utility;

namespace DuetAPI.Commands
{
    /// <summary>
    /// Register a new HTTP endpoint via DuetWebServer. This will create a new HTTP endpoint under /machine/{Namespace}/{EndpointPath}.
    /// Returns a path to the UNIX socket which DuetWebServer will connect to whenever a matching HTTP request is received.
    /// A plugin using this command has to open a new UNIX socket with the given path that DuetWebServer can connect to
    /// </summary>
    /// <seealso cref="ReceivedHttpRequest"/>.
    /// <seealso cref="SendHttpResponse"/>
    [RequiredPermissions(SbcPermissions.RegisterHttpEndpoints)]
    public class AddHttpEndpoint : Command<string>
    {
        /// <summary>
        /// Type of the HTTP request
        /// </summary>
        public HttpEndpointType EndpointType { get; set; }

        /// <summary>
        /// Namespace of the plugin wanting to create a new third-party endpoint
        /// </summary>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// Path to the endpoint to register
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is an upload request
        /// </summary>
        /// <seealso cref="HttpEndpoint.IsUploadRequest"/>
        public bool IsUploadRequest { get; set; }
    }
}
