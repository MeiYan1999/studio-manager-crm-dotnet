using System.Globalization;
using MayAlbum.Models;

namespace MayAlbum.Services;

/// <summary>
/// Các phép tính dùng chung cho chấm công và bảng lương: giờ làm được làm tròn
/// tới 15 phút gần nhất, đi muộn / về sớm so với ca chuẩn của nhân sự.
/// </summary>
public static class OperationsMath
{
    public static int ToMinutes(string? clock)
    {
        if (string.IsNullOrWhiteSpace(clock))
        {
            return -1;
        }

        return TimeOnly.TryParseExact(
            clock.Trim(),
            ["HH:mm", "H:mm"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var time)
            ? time.Hour * 60 + time.Minute
            : -1;
    }

    public static decimal WorkedHours(AttendanceRecord record)
    {
        var start = ToMinutes(record.In);
        var end = ToMinutes(record.Out);
        if (start < 0 || end < 0 || end <= start)
        {
            return 0;
        }

        var minutes = end - start;
        var rounded = Math.Round(minutes / 15m, 0, MidpointRounding.AwayFromZero) * 15m;
        return rounded / 60m;
    }

    public static (int Late, int Early) Lateness(AttendanceRecord record, string? shift)
    {
        var (shiftStart, shiftEnd) = StaffShifts.Window(shift);
        var expectedStart = ToMinutes(shiftStart);
        var expectedEnd = ToMinutes(shiftEnd);
        var actualStart = ToMinutes(record.In);
        var actualEnd = ToMinutes(record.Out);

        var late = actualStart >= 0 && actualStart > expectedStart ? actualStart - expectedStart : 0;
        var early = actualEnd >= 0 && actualEnd < expectedEnd ? expectedEnd - actualEnd : 0;
        return (late, early);
    }

    public static string AttendanceStatus(AttendanceRecord record, string? shift)
    {
        var start = ToMinutes(record.In);
        if (start < 0)
        {
            return "missing_in";
        }

        var end = ToMinutes(record.Out);
        if (end < 0)
        {
            return "open";
        }

        var (late, early) = Lateness(record, shift);
        if (late > 0 && early > 0)
        {
            return "late_early";
        }

        if (late > 0)
        {
            return "late";
        }

        return early > 0 ? "early" : "ontime";
    }

    public static string StatusLabel(string status) => status switch
    {
        "late" => "Đi muộn",
        "early" => "Về sớm",
        "late_early" => "Muộn & về sớm",
        "open" => "Chưa tan ca",
        "missing_in" => "Thiếu giờ vào",
        _ => "Đúng giờ"
    };
}
