// <copyright file="SequenceStepTiming.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Models;

/// <summary>
/// JSONB model for step timing configuration.
/// Defines when a sequence step becomes eligible to send.
/// </summary>
public class SequenceStepTiming
{
    /// <summary>
    /// Gets or sets the delay before the step becomes eligible.
    /// </summary>
    public SequenceStepDelay Delay { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional local time to align sending to (e.g. "10:00").
    /// When present, sending is aligned to the next occurrence of this time after the delay elapses.
    /// </summary>
    public string? SendAt { get; set; }

    /// <summary>
    /// Gets or sets the optional allowed weekdays for sending (e.g. ["Monday", "Wednesday"]).
    /// </summary>
    public string[]? AllowedWeekDays { get; set; }
}

/// <summary>
/// Represents a delay duration with a value and unit.
/// </summary>
public class SequenceStepDelay
{
    /// <summary>
    /// Gets or sets the numeric delay value.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Gets or sets the delay unit: "minutes", "hours", or "days".
    /// </summary>
    public string Unit { get; set; } = "minutes";
}
