﻿namespace DuetAPI.ObjectModel
{
    /// <summary>
    /// Details about the first error on start-up
    /// </summary>
    public sealed class StartupError : ModelObject
    {
        /// <summary>
        /// Filename of the macro where the error occurred
        /// </summary>
        public string File
        {
            get => _file;
			set => SetPropertyValue(ref _file, value);
        }
        private string _file = string.Empty;

        /// <summary>
        /// Line number of the error
        /// </summary>
        public long Line
        {
            get => _line;
			set => SetPropertyValue(ref _line, value);
        }
        private long _line;

        /// <summary>
        /// Message of the error
        /// </summary>
        public string Message
        {
            get => _message;
            set => SetPropertyValue(ref _message, value);
        }
        private string _message = string.Empty;
    }
}
