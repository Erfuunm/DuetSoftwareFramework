﻿using System;

namespace DuetAPI.ObjectModel
{
    /// <summary>
    /// Attribute used to mark properties that are overridden by the control server
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SbcPropertyAttribute : Attribute
    {
        /// <summary>
        /// Indicates if the property may be used in standalone mode
        /// </summary>
        public bool AvailableInStandaloneMode { get; set; }

        /// <summary>
        /// Constructor of this attribute
        /// </summary>
        /// <param name="availableInStandaloneMode">Defines if the property is available in standalone mode</param>
        public SbcPropertyAttribute(bool availableInStandaloneMode)
        {
            AvailableInStandaloneMode = availableInStandaloneMode;
        }
    }
}
