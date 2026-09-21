using FG.HighThroughput.Engine.Core;
using FG.HighThroughput.Engine.Infrastructure;
using FG.HighThroughput.Engine.UI.Components;

var builder = WebApplication.CreateBuilder(args);

// UI: Blazor Web App (Interactive Server render mode).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Core/Infrastructure: ADO.NET only, no EF/Dapper.
builder.Services.AddSingleton<IBulkDataWriter, SqlBulkCopyWriter>();
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
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
