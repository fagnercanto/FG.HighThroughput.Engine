using FG.HighThroughput.Engine.Core;
using FG.HighThroughput.Engine.Infrastructure;
using FG.HighThroughput.Engine.UI.Components;

var builder = WebApplication.CreateBuilder(args);

// UI: Blazor Web App (Interactive Server render mode).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Core/Infrastructure: ADO.NET only, no EF/Dapper.
builder.Services.AddSingleton<IBulkDataWriter, SqlBulkCopyWriter>();
builder.Services.AddSingleton<IIngestionDataReader, SqlIngestionDataReader>();
// Transient: each ingestion run needs a fresh Channel (it is completed at the end of a run).
builder.Services.AddTransient<IngestionPipeline>();
builder.Services.AddSingleton<TestDataFileGenerator>();

// Startup routine (F5): ensures LocalDB database/table exist via raw SQL.
builder.Services.AddHostedService<DatabaseInitializerHostedService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

// MapStaticAssets() relies on the Static Web Assets manifest, which is only reliably
// available when running via `dotnet run`/F5/IIS Express. Running the published/compiled
// .exe directly (e.g. double-clicking it, or `bin/.../App.exe`) logs a warning and falls
// back to serving raw files from wwwroot - UseStaticFiles() below guarantees that fallback
// works correctly in every hosting scenario for this POC (no fingerprinting/compression
// needed here: just app.css and the locally-restored Bootstrap files).
app.UseStaticFiles();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Lets the browser download the generated file to the user's own machine, regardless of
// where the server actually runs (unlike Process.Start("explorer.exe"), which only makes
// sense when server and client are the same machine, e.g. local LocalDB/F5 scenario).
app.MapGet("/download/{fileName}", (string fileName, IWebHostEnvironment environment) =>
{
    var appDataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
    var filePath = Path.GetFullPath(Path.Combine(appDataDirectory, fileName));

    // Guard against path traversal (e.g. "../../Program.cs"): the resolved path must stay
    // inside App_Data.
    if (!filePath.StartsWith(appDataDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
    {
        return Results.NotFound();
    }

    return Results.File(filePath, "text/plain", Path.GetFileName(filePath), enableRangeProcessing: true);
});

app.Run();
