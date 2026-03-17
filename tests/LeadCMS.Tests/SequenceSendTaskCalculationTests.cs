// <copyright file="SequenceSendTaskCalculationTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Models;

namespace LeadCMS.Tests;

public class SequenceSendTaskCalculationTests
{
    [Fact]
    public void MinutesDelay_AddsCorrectMinutes()
    {
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(30, "minutes");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.Should().Be(baseTime.AddMinutes(30));
    }

    [Fact]
    public void HoursDelay_AddsCorrectHours()
    {
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(2, "hours");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.Should().Be(baseTime.AddHours(2));
    }

    [Fact]
    public void DaysDelay_AddsCorrectDays()
    {
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(3, "days");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.Should().Be(baseTime.AddDays(3));
    }

    [Fact]
    public void ZeroDelay_ReturnsBaseTime()
    {
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(0, "minutes");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.Should().Be(baseTime);
    }

    [Fact]
    public void SendAt_AlignsToTargetTimeNextDay_WhenAlreadyPassed()
    {
        // Base time is noon UTC, sendAt is 10:00, no timezone offset
        // After 1 day delay, landing at noon June 2 → should move to 10:00 on June 3
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(1, "days", sendAt: "10:00");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.Should().Be(new DateTime(2025, 6, 3, 10, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void SendAt_AlignsToTargetTimeToday_WhenNotYetPassed()
    {
        // Base time is 8:00 UTC, sendAt is 10:00, no timezone offset
        // After 1 day delay, landing at 8:00 June 2 → should move to 10:00 on June 2
        var baseTime = new DateTime(2025, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(1, "days", sendAt: "10:00");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.Should().Be(new DateTime(2025, 6, 2, 10, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void SendAt_WithTimezoneOffset_ConvertsCorrectly()
    {
        // Base time is 16:00 UTC, timezone is +180 (UTC+3), sendAt is "10:00"
        // After 1 day delay → 16:00 UTC June 2 → local 19:00 June 2 → past 10:00
        // → move to 10:00 local June 3 → UTC 07:00 June 3
        var baseTime = new DateTime(2025, 6, 1, 16, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(1, "days", sendAt: "10:00");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 180, null);

        result.Should().Be(new DateTime(2025, 6, 3, 7, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void AllowedWeekDays_SkipsDisallowedDays()
    {
        // June 1 2025 is a Sunday. Delay 0, sendAt not set, only allow Monday.
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(0, "minutes", allowedDays: new[] { "Monday" });

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.Should().Be(new DateTime(2025, 6, 2, 10, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void AllowedWeekDays_StaysOnCurrentDay_WhenAllowed()
    {
        // June 2 2025 is a Monday.
        var baseTime = new DateTime(2025, 6, 2, 10, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(0, "minutes", allowedDays: new[] { "Monday", "Wednesday", "Friday" });

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.Should().Be(baseTime);
    }

    [Fact]
    public void SendAt_WithAllowedWeekDays_CombinesBoth()
    {
        // June 1 2025 is Sunday. 1-day delay → Monday. sendAt 09:00, only allow Tuesday+Thursday.
        // After delay: Monday June 2 at 12:00. SendAt 09:00 → already past → Tuesday June 3 09:00.
        // Tuesday is allowed, so keeps June 3 at 09:00.
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(1, "days", sendAt: "09:00", allowedDays: new[] { "Tuesday", "Thursday" });

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        result.Should().Be(new DateTime(2025, 6, 3, 9, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void SendAt_WithAllowedWeekDays_AdvancesToNextAllowedDay()
    {
        // June 2 2025 is Monday. 0-delay, sendAt 09:00, only allow Wednesday.
        // Landing at Monday 12:00. sendAt 09:00 → past → Tuesday June 3 09:00.
        // Tuesday not allowed → advance to Wednesday June 4 09:00.
        var baseTime = new DateTime(2025, 6, 2, 12, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(0, "minutes", sendAt: "09:00", allowedDays: new[] { "Wednesday" });

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, 0, null);

        result.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        result.Should().Be(new DateTime(2025, 6, 4, 9, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void NegativeTimezoneOffset_HandledCorrectly()
    {
        // Base time 2:00 UTC, timezone -300 (UTC-5), sendAt 10:00,  0-minute delay
        // Local time: June 1 at 21:00 (prev day) → wait, 2:00 - 300m = 2:00 - 5h = actually June 1 at 21:00 is wrong
        // Actually: +offset means add. So -300 offset → local = UTC + (-300min) = UTC - 5h.
        // 2:00 UTC → local -3:00 → that would be prev day 21:00. sendAt 10:00 → next occurrence is today 10:00.
        // 10:00 local → UTC = 10:00 - (-300 min) = 10:00 + 5h = 15:00 UTC
        var baseTime = new DateTime(2025, 6, 1, 2, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(0, "minutes", sendAt: "10:00");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, false, -300, null);

        result.Should().Be(new DateTime(2025, 6, 1, 15, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void UseContactTimeZone_UsesContactOffsetInsteadOfSequenceOffset()
    {
        // Base time is 16:00 UTC, contact timezone is -300 (UTC-5), sendAt is 10:00.
        // After 1 day delay -> June 2 16:00 UTC -> June 2 11:00 local, which is past 10:00.
        // Therefore this should roll to June 3 10:00 local -> June 3 15:00 UTC.
        var baseTime = new DateTime(2025, 6, 1, 16, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(1, "days", sendAt: "10:00");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, true, 180, -300);

        result.Should().Be(new DateTime(2025, 6, 3, 15, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void UseContactTimeZone_FallsBackToSequenceOffset_WhenContactOffsetMissing()
    {
        var baseTime = new DateTime(2025, 6, 1, 16, 0, 0, DateTimeKind.Utc);
        var timing = MakeTiming(1, "days", sendAt: "10:00");

        var result = SequenceSendTask.CalculateScheduledAt(baseTime, timing, true, 180, null);

        result.Should().Be(new DateTime(2025, 6, 3, 7, 0, 0, DateTimeKind.Unspecified));
    }

    private static SequenceStepTiming MakeTiming(
        int delayValue,
        string delayUnit,
        string? sendAt = null,
        string[]? allowedDays = null)
    {
        return new SequenceStepTiming
        {
            Delay = new SequenceStepDelay { Value = delayValue, Unit = delayUnit },
            SendAt = sendAt,
            AllowedWeekDays = allowedDays,
        };
    }
}
