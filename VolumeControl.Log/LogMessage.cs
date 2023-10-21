﻿using VolumeControl.Log.Enum;

namespace VolumeControl.Log
{
    /// <summary>
    /// Represents a message to be written to the log.
    /// </summary>
    public sealed class LogMessage
    {
        #region Constructor
        /// <summary>
        /// Creates a new <see cref="LogMessage"/> instance with the specified <paramref name="eventType"/>.
        /// </summary>
        /// <param name="eventType">The <see cref="Enum.EventType"/> of this message.</param>
        /// <param name="lines">The lines in this message.</param>
        public LogMessage(EventType eventType, params object?[] lines)
        {
            EventType = eventType;
            Lines = lines.ToList();
        }
        /// <summary>
        /// Creates a new empty <see cref="LogMessage"/> instance with the specified <paramref name="eventType"/>.
        /// </summary>
        /// <param name="eventType">The <see cref="Enum.EventType"/> of this message.</param>
        public LogMessage(EventType eventType)
        {
            EventType = eventType;
            Lines = new();
        }
        #endregion Constructor

        #region Properties
        /// <summary>
        /// Gets or sets the event type of this message.
        /// </summary>
        public EventType EventType { get; set; }
        /// <summary>
        /// Gets or sets the lines in this message.
        /// </summary>
        public List<object?> Lines { get; set; }
        /// <summary>
        /// Gets whether there are any lines in this message.
        /// </summary>
        public bool IsEmpty => Lines.Count == 0;
        #endregion Properties

        #region Methods
        internal void RemoveNullLines()
        {
            for (int i = Lines.Count - 1; i >= 0; --i)
            {
                if (Lines[i] == null)
                {
                    Lines.RemoveAt(i);
                }
            }
        }
        #endregion Methods
    }
}
