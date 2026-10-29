using System.IO.Compression;
using System.Xml.Linq;
using StudioCore.Models;
using StudioCore.Services;
using Xunit;

namespace StudioCore.Tests;

public class PasswordTests
{
    [Fact]
    public void HashesHaveIndependentSaltsAndRejectWrongPasswords()
    {
        const string password = "test-only-password-2026";
        var first = PasswordHasher.Hash(password);
        var second = PasswordHasher.Hash(password);
        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify(password, first));
        Assert.True(PasswordHasher.Verify(password, second));
        Assert.False(PasswordHasher.Verify("wrong", first));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plaintext")]
    [InlineData("pbkdf2-sha256$9999$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$120000$invalid!$invalid!")]
    public void InvalidStoredHashesFailClosed(string? encoded) =>
        Assert.False(PasswordHasher.Verify("test-only-password", encoded));
}

public class AttendanceTests
{
    [Theory]
    [InlineData("08:30", "17:37", "9")]
    [InlineData("08:30", "17:38", "9.25")]
    [InlineData("08:30", "08:38", "0.25")]
    [InlineData("08:30", "08:30", "0")]
    [InlineData("18:30", "08:30", "0")]
    [InlineData("invalid", "18:30", "0")]
    [InlineData("08:30", "", "0")]
    public void HoursRoundAtQuarterHourBoundary(string start, string end, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            OperationsMath.WorkedHours(new AttendanceRecord { In = start, Out = end }));

    [Theory]
    [InlineData("", "", "missing_in")]
    [InlineData("08:30", "", "open")]
    [InlineData("08:30", "18:30", "ontime")]
    [InlineData("08:31", "18:30", "late")]
    [InlineData("08:30", "18:29", "early")]
    [InlineData("08:31", "18:29", "late_early")]
    public void StatusReflectsActualClockTimes(string start, string end, string expected) =>
        Assert.Equal(expected, OperationsMath.AttendanceStatus(
            new AttendanceRecord { In = start, Out = end }, StaffShifts.Normal));

    [Theory]
    [InlineData(StaffShifts.Early, "07:31", "17:28")]
    [InlineData(StaffShifts.Normal, "08:31", "18:28")]
    [InlineData(StaffShifts.Late, "09:01", "18:58")]
    public void LatenessUsesAssignedShift(string shift, string start, string end) =>
        Assert.Equal((1, 2), OperationsMath.Lateness(new AttendanceRecord { In = start, Out = end }, shift));
}

public class PermissionTests
{
    [Fact]
    public void ExplicitPermissionsDoNotFallBackToEmployeeDefaults()
    {
        Assert.Empty(StudioPermissions.Normalize([], false, true));
        Assert.Equal(StudioPermissions.EmployeeDefaults, StudioPermissions.Normalize(null, false, false));
        Assert.Equal(new[] { StudioPermissions.Finance },
            StudioPermissions.Normalize(["finance", "unknown", "finance"], false, true));
    }

    [Fact]
    public void AccessRequiresAnAccountAndAnAssignedPermission()
    {
        Assert.False(StudioAccess.Has(null, StudioPermissions.Finance));
        var employee = new StudioUser { Role = "employee", Permissions = [StudioPermissions.Albums] };
        Assert.True(StudioAccess.Has(employee, StudioPermissions.Albums));
        Assert.False(StudioAccess.Has(employee, StudioPermissions.Finance));
        Assert.True(StudioAccess.Has(new StudioUser { Role = "admin" }, StudioPermissions.Finance));
    }
}

public class DriveTests
{
    [Theory]
    [InlineData("https://drive.google.com/drive/folders/abcdefghij_123?usp=sharing", "abcdefghij_123")]
    [InlineData("https://drive.google.com/open?id=abcdefghij_123", "abcdefghij_123")]
    [InlineData("abcdefghij_123", "abcdefghij_123")]
    [InlineData("", null)]
    [InlineData("not a folder", null)]
    public void FolderInputIsParsedWithoutCallingGoogle(string input, string? expected) =>
        Assert.Equal(expected, GoogleDriveService.ExtractFolderId(input));
}

public class ExcelTests
{
    [Fact]
    public void WorkbookPreservesVietnameseAndTreatsFormulaLikeTextAsText()
    {
        var book = new ExcelWorkbook();
        book.AddSheet("Thu chi", new ExcelColumn("Ghi chú"), new ExcelColumn("Số tiền", ExcelColumnKind.Money))
            .AddRow("Biên nhận <ảnh> & tiếng Việt", 123000m)
            .AddRow("=1+1", 0m);
        using var bytes = new MemoryStream(book.Build());
        using var zip = new ZipArchive(bytes);
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")))
        {
            using var xml = entry.Open();
            XDocument.Load(xml);
        }
        using var sheetStream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var sheet = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = sheet.Descendants(ns + "c").ToList();
        Assert.Contains(cells, cell => cell.Value == "Biên nhận <ảnh> & tiếng Việt");
        var formulaText = Assert.Single(cells.Where(cell => cell.Value == "=1+1"));
        Assert.Equal("inlineStr", formulaText.Attribute("t")?.Value);
        Assert.Empty(sheet.Descendants(ns + "f"));
        Assert.Contains(cells, cell => cell.Element(ns + "v")?.Value == "123000");
    }
}
