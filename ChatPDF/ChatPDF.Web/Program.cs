using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Authentication;
using ChatPDF.Web.Components;
using ChatPDF.Web.Services;
using ChatPDF.Web.Services.Ingestion;
using ChatPDF.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Configure detailed errors for development
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
    {
        options.DetailedErrors = true;
    });
}

// Configure application settings
var ollamaConfig = builder.Configuration.GetSection(OllamaConfiguration.SectionName).Get<OllamaConfiguration>() ?? new OllamaConfiguration();
var appConfig = builder.Configuration.GetSection(ApplicationConfiguration.SectionName).Get<ApplicationConfiguration>() ?? new ApplicationConfiguration();
var identityConfig = builder.Configuration.GetSection(IdentityServerConfiguration.SectionName).Get<IdentityServerConfiguration>() ?? new IdentityServerConfiguration();

// Chat client configuration
var chatClientBuilder = builder.AddOllamaApiClient("chat")
    .AddChatClient();

if (ollamaConfig.Chat.EnableFunctionInvocation)
{
    chatClientBuilder.UseFunctionInvocation();
}

if (ollamaConfig.Chat.EnableOpenTelemetry)
{
    chatClientBuilder.UseOpenTelemetry(configure: c =>
        c.EnableSensitiveData = builder.Environment.IsDevelopment());
}

// Embeddings client configuration
builder.AddOllamaApiClient("embeddings")
    .AddEmbeddingGenerator();

builder.AddQdrantClient("vectordb");
builder.Services.AddQdrantCollection<Guid, IngestedChunk>(appConfig.VectorDatabase.ChunksCollectionName);
builder.Services.AddQdrantCollection<Guid, IngestedDocument>(appConfig.VectorDatabase.DocumentsCollectionName);
builder.Services.AddScoped<DataIngestor>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddSingleton<SemanticSearch>();

// Authentication configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";
})
.AddCookie("Cookies")
.AddOpenIdConnect("oidc", options =>
{
    options.Authority = identityConfig.Authority;
    options.ClientId = identityConfig.ClientId;
    options.ResponseType = "code";
    options.UsePkce = true;
    options.RequireHttpsMetadata = identityConfig.RequireHttpsMetadata;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    
    foreach (var scope in identityConfig.Scopes)
    {
        options.Scope.Add(scope);
    }
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Register configuration objects for dependency injection
builder.Services.Configure<ApplicationConfiguration>(builder.Configuration.GetSection(ApplicationConfiguration.SectionName));
builder.Services.Configure<OllamaConfiguration>(builder.Configuration.GetSection(OllamaConfiguration.SectionName));
builder.Services.Configure<IdentityServerConfiguration>(builder.Configuration.GetSection(IdentityServerConfiguration.SectionName));

// Configure HSTS
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(appConfig.HttpPipeline.HstsMaxAgeDays);
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(appConfig.HttpPipeline.ErrorPage, createScopeForErrors: true);
    app.UseHsts();
}

if (appConfig.HttpPipeline.UseHttpsRedirection)
{
    app.UseHttpsRedirection();
}

if (appConfig.HttpPipeline.UseAntiforgery)
{
    app.UseAntiforgery();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

// Add authentication endpoints
app.MapGet("/Account/Login", async (HttpContext context) =>
{
    await context.ChallengeAsync("oidc", new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        RedirectUri = "/"
    });
}).AllowAnonymous();

app.MapGet("/Account/Logout", async (HttpContext context) =>
{
    await context.SignOutAsync("Cookies");
    await context.SignOutAsync("oidc", new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        RedirectUri = "/"
    });
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Configure data ingestion based on settings
if (appConfig.DataIngestion.IngestOnStartup)
{
    // Important: ensure that any content you ingest is trusted, as it may be reflected back
    // to users or could be a source of prompt injection risk.
    var pdfPath = Path.Combine(builder.Environment.WebRootPath, appConfig.DataIngestion.PdfDirectory);
    await DataIngestor.IngestDataAsync(app.Services, new PDFDirectorySource(pdfPath));
}

app.Run();
