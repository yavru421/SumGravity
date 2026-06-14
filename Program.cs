using SumGravity.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor Server ──────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── App Services ───────────────────────────────────────────────
builder.Services.AddSingleton<FileSystemService>();
builder.Services.AddSingleton<TerminalService>();
builder.Services.AddSingleton<SkillsService>();
builder.Services.AddSingleton<SearchReplaceService>();
builder.Services.AddHttpClient<KoboldCppClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["KoboldCpp:BaseUrl"] ?? "http://localhost:5001/v1";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(300); // allow long LLM responses
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<SumGravity.App>()
    .AddInteractiveServerRenderMode();

app.Run();
