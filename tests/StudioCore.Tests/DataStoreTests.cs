using System.Text.Json;
using StudioCore.Models;
using StudioCore.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StudioCore.Tests;

public sealed class DataStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "may-backend-test-" + Guid.NewGuid().ToString("N"));

    private DataStore Open()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Data:Directory"] = directory,
            ["AdminCode"] = "test-only-" + Guid.NewGuid().ToString("N")
        }).Build();
        return new DataStore(new TestEnvironment { ContentRootPath = directory }, configuration, NullLogger<DataStore>.Instance);
    }

    [Fact]
    public void SuccessfulWritesSurviveReopeningAndCloneCannotChangeLiveData()
    {
        var store = Open();
        store.Write(data => { data.Settings.StudioName = "Studio kiểm thử"; });
        store.ReadClone(data => { data.Settings.StudioName = "Không được lưu"; return 0; });
        Assert.Equal("Studio kiểm thử", store.Read(data => data.Settings.StudioName));
        Assert.Equal("Studio kiểm thử", Open().Read(data => data.Settings.StudioName));
    }

    [Fact]
    public void VersionSevenExpensesMigrateWithoutLosingAmountOrNote()
    {
        Directory.CreateDirectory(directory);
        var data = new AppData { SchemaVersion = 7 };
        data.Expenses.Add(new ExpenseEntry { Id = "legacy-expense", Date = "2026-01-15", Amount = 123000m, Note = "Chi phí cũ" });
        File.WriteAllText(Path.Combine(directory, "store.json"), JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var store = Open();
        Assert.Equal(8, store.Read(current => current.SchemaVersion));
        var expense = store.Read(current => Assert.Single(current.Expenses));
        Assert.Equal("legacy-expense", expense.Id);
        Assert.Equal("expense", expense.Kind);
        Assert.Equal(123000m, expense.Amount);
        Assert.Equal("Chi phí cũ", expense.Note);
        Assert.Empty(expense.Photos);
        Assert.Equal(8, Open().Read(current => current.SchemaVersion));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "StudioCore";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
