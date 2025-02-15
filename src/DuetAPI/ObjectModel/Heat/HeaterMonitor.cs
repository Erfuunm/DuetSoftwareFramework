﻿namespace DuetAPI.ObjectModel
{
    /// <summary>
    /// Information about a heater monitor
    /// </summary>
    public sealed class HeaterMonitor : ModelObject
    {
        /// <summary>
        /// Action to perform when the trigger condition is met
        /// </summary>
        public HeaterMonitorAction? Action
        {
            get => _action;
			set => SetPropertyValue(ref _action, value);
        }
        private HeaterMonitorAction? _action = null;

        /// <summary>
        /// Condition to meet to perform an action
        /// </summary>
        public HeaterMonitorCondition Condition
        {
            get => _condition;
			set => SetPropertyValue(ref _condition, value);
        }
        private HeaterMonitorCondition _condition = HeaterMonitorCondition.Disabled;

        /// <summary>
        /// Limit threshold for this heater monitor
        /// </summary>
        public float? Limit
        {
            get => _limit;
			set => SetPropertyValue(ref _limit, value);
        }
        private float? _limit = null;

        /// <summary>
        /// Sensor number to monitor
        /// </summary>
        public int Sensor
        {
            get => _sensor;
            set => SetPropertyValue(ref _sensor, value);
        }
        private int _sensor = -1;
    }
}
