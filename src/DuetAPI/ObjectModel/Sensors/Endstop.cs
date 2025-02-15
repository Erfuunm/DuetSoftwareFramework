﻿namespace DuetAPI.ObjectModel
{
    /// <summary>
    /// Information about an endstop
    /// </summary>
    public sealed class Endstop : ModelObject
    {
        /// <summary>
        /// Whether this endstop is at the high end of the axis
        /// </summary>
        public bool HighEnd
        {
            get => _highEnd;
            set => SetPropertyValue(ref _highEnd, value);
        }
        private bool _highEnd;

        /// <summary>
        /// Number of the referenced probe if type is ZProbeAsEndstop, else null
        /// </summary>
        public int? Probe
        {
            get => _probe;
            set => SetPropertyValue(ref _probe, value);
        }
        private int? _probe;

        /// <summary>
        /// Whether or not the endstop is hit
        /// </summary>
        public bool Triggered
        {
            get => _triggered;
			set => SetPropertyValue(ref _triggered, value);
        }
        private bool _triggered;
        
        /// <summary>
        /// Type of the endstop
        /// </summary>
        public EndstopType Type
        {
            get => _type;
			set => SetPropertyValue(ref _type, value);
        }
        private EndstopType _type = EndstopType.Unknown;
    }
}