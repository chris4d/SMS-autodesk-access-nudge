using SmsNudge;
using Xunit;

public class NotifTimingTests
{
    [Fact]
    public void Mode_DefaultsToImmediate_AndParsesVariants()
    {
        Assert.Equal(NotifMode.Immediate, new NotifTiming().ResolvedMode);
        Assert.Equal(NotifMode.DailyDigest, new NotifTiming { Mode = "dailydigest" }.ResolvedMode);
        Assert.Equal(NotifMode.DailyDigest, new NotifTiming { Mode = "Daily " }.ResolvedMode);
        Assert.Equal(NotifMode.Immediate, new NotifTiming { Mode = "bogus" }.ResolvedMode);
    }

    [Fact]
    public void DigestTime_ParsesDefaultAndRejectsGarbage()
    {
        Assert.Equal(new TimeSpan(16, 30, 0), new NotifTiming().ResolvedTime);
        Assert.Equal(new TimeSpan(9, 5, 0), new NotifTiming { DigestTime = "9:05" }.ResolvedTime);
        Assert.Null(new NotifTiming { DigestTime = "25:00" }.ResolvedTime);
        Assert.Null(new NotifTiming { DigestTime = "abc" }.ResolvedTime);
    }

    [Fact]
    public void Days_DefaultWeekdaysAndParsesFullNames()
    {
        var t = new NotifTiming();
        Assert.True(t.IsAllowedDay(DayOfWeek.Friday));
        Assert.False(t.IsAllowedDay(DayOfWeek.Sunday));

        var allDays = new NotifTiming { DigestDays = new() { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" } };
        Assert.True(allDays.IsAllowedDay(DayOfWeek.Sunday));

        var empty = new NotifTiming { DigestDays = new() };
        Assert.True(empty.IsAllowedDay(DayOfWeek.Sunday));
    }

    [Fact]
    public void DigestDue_OnlyAtOrAfterTimeOnAllowedDayAndOncePerDay()
    {
        var timing = new NotifTiming { Mode = "Daily", DigestTime = "16:30" };
        var monday = new DateTime(2026, 9, 14); // Monday

        // before 16:30 → not due
        Assert.False(DueAt(timing, monday.AddHours(15.5)));

        // at 16:30 on a weekday, never digested today → due
        Assert.True(DueAt(timing, monday.AddMinutes(16 * 60 + 30)));

        // after already digested today → not due
        var state = new NotifiedState { LastDigestDate = monday.ToString("yyyy-MM-dd") };
        var already = AlreadyDigestDay(timing, monday.AddMinutes(16 * 60 + 45), state.LastDigestDate);
        Assert.False(already);

        // wrong weekday at 16:30 → not due
        var saturday = new DateTime(2026, 9, 19);
        Assert.False(DueAt(timing, saturday.AddMinutes(16 * 60 + 30)));
    }

    private static bool DueAt(NotifTiming t, DateTime now) =>
        t.ResolvedTime is { } then && t.IsAllowedDay(now.DayOfWeek) && now.TimeOfDay >= then;

    private static bool AlreadyDigestDay(NotifTiming t, DateTime now, string lastDigestDate) =>
        t.ResolvedTime is { } then && t.IsAllowedDay(now.DayOfWeek) && now.TimeOfDay >= then
        && lastDigestDate != now.ToString("yyyy-MM-dd");
}
