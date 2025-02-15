﻿namespace DuetAPI.ObjectModel
{
    /// <summary>
    /// Information about a G/M/T-code channel
    /// </summary>
    public sealed class InputChannel : ModelObject
    {
        /// <summary>
        /// True if the input is is in active mode i.e. executing commands for its assigned motion system,
        /// false if it is assigned to a motion system other than the current one
        /// </summary>
        /// <remarks>
        /// This will always be true except for the File and File2 inputs
        /// </remarks>
        public bool Active
        {
            get => _active;
            set => SetPropertyValue(ref _active, value);
        }
        private bool _active = true;

        /// <summary>
        /// Whether relative positioning is being used
        /// </summary>
        public bool AxesRelative
        {
            get => _axesRelative;
            set => SetPropertyValue(ref _axesRelative, value);
        }
        private bool _axesRelative = false;

        /// <summary>
        /// Emulation used on this channel
        /// </summary>
        public Compatibility Compatibility
        {
            get => _compatibility;
            set => SetPropertyValue(ref _compatibility, value);
        }
        private Compatibility _compatibility = Compatibility.RepRapFirmware;

        /// <summary>
        /// Whether inches are being used instead of mm
        /// </summary>
        public DistanceUnit DistanceUnit
        {
            get => _distanceUnit;
            set => SetPropertyValue(ref _distanceUnit, value);
        }
        private DistanceUnit _distanceUnit = DistanceUnit.MM;

        /// <summary>
        /// Whether relative extrusion is being used
        /// </summary>
        public bool DrivesRelative
        {
            get => _drivesRelative;
            set => SetPropertyValue(ref _drivesRelative, value);
        }
        private bool _drivesRelative = true;

        /// <summary>
        /// Current feedrate in mm/s
        /// </summary>
        public float FeedRate
        {
            get => _feedRate;
            set => SetPropertyValue(ref _feedRate, value);
        }
        private float _feedRate = 50.0F;

        /// <summary>
        /// Whether a macro file is being processed
        /// </summary>
        public bool InMacro
        {
            get => _inMacro;
            set => SetPropertyValue(ref _inMacro, value);
        }
        private bool _inMacro = false;

        /// <summary>
        /// Indicates if inverse time mode (G73) is active
        /// </summary>
        public bool InverseTimeMode
        {
            get => _inverseTimeMode;
            set => SetPropertyValue(ref _inverseTimeMode, value);
        }
        private bool _inverseTimeMode;

        /// <summary>
        /// Number of the current line
        /// </summary>
        public long LineNumber
        {
            get => _lineNumber;
            set => SetPropertyValue(ref _lineNumber, value);
        }
        private long _lineNumber = 0;

        /// <summary>
        /// Indicates if the current macro file can be restarted after a pause
        /// </summary>
        public bool MacroRestartable
        {
            get => _macroRestartable;
            set => SetPropertyValue(ref _macroRestartable, value);
        }
        private bool _macroRestartable;

        /// <summary>
        /// Active motion system index
        /// </summary>
        public int MotionSystem
        {
            get => _motionSystem;
            set => SetPropertyValue(ref _motionSystem, value);
        }
        private int _motionSystem;

        /// <summary>
        /// Name of this channel
        /// </summary>
        public CodeChannel Name
        {
            get => _name;
            set => SetPropertyValue(ref _name, value);
        }
        private CodeChannel _name = CodeChannel.Unknown;

        /// <summary>
        /// Index of the selected plane
        /// </summary>
        public int SelectedPlane
        {
            get => _selectedPlane;
            set => SetPropertyValue(ref _selectedPlane, value);
        }
        private int _selectedPlane;

        /// <summary>
        /// Depth of the stack
        /// </summary>
        public byte StackDepth
        {
            get => _stackDepth;
            set => SetPropertyValue(ref _stackDepth, value);
        }
        private byte _stackDepth = 0;

        /// <summary>
        /// State of this input channel
        /// </summary>
        public InputChannelState State
        {
            get => _state;
            set => SetPropertyValue(ref _state, value);
        }
        private InputChannelState _state = InputChannelState.Idle;

        /// <summary>
        /// Whether volumetric extrusion is being used
        /// </summary>
        public bool Volumetric
        {
            get => _volumetric;
            set => SetPropertyValue(ref _volumetric, value);
        }
        private bool _volumetric = false;
    }
}
