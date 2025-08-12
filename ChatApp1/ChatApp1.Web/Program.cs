using Microsoft.Extensions.AI;
using ChatPDF.Web.Components;
using ChatPDF.Web.Services;
using ChatPDF.Web.Services.Ingestion;
using ChatPDF.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Configure Ollama settings
var ollamaConfig = builder.Configuration.GetSection(OllamaConfiguration.SectionName).Get<OllamaConfiguration>() ?? new OllamaConfiguration();

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
builder.Services.AddQdrantCollection<Guid, IngestedChunk>("data-chatpdf-chunks");
builder.Services.AddQdrantCollection<Guid, IngestedDocument>("data-chatpdf-documents");
builder.Services.AddScoped<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();
app.MapRazorComponents<ChatPDF.Web.Components.App>()
    .AddInteractiveServerRenderMode();

// By default, we ingest PDF files from the /wwwroot/Data directory. You can ingest from
// other sources by implementing IIngestionSource.
// Important: ensure that any content you ingest is trusted, as it may be reflected back
// to users or could be a source of prompt injection risk.
await DataIngestor.IngestDataAsync(
    app.Services,
    new PDFDirectorySource(Path.Combine(builder.Environment.WebRootPath, "Data")));

app.Run();
