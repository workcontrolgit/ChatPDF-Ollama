# Building AI-Powered Document Chat with RAG in .NET: A Complete Guide for Local LLM Integration

**Transform your .NET applications with AI-powered document conversations using Retrieval-Augmented Generation (RAG) and local language models**

---

## Table of Contents

1. [Introduction to AI and RAG](#introduction-to-ai-and-rag)
2. [Understanding the Technology Stack](#understanding-the-technology-stack)
3. [Project Architecture Overview](#project-architecture-overview)
4. [Setting Up Your Development Environment](#setting-up-your-development-environment)
5. [Core AI Components Deep Dive](#core-ai-components-deep-dive)
6. [Building the RAG Pipeline](#building-the-rag-pipeline)
7. [Implementing Chat with Function Calling](#implementing-chat-with-function-calling)
8. [Vector Database Integration](#vector-database-integration)
9. [Testing and Troubleshooting](#testing-and-troubleshooting)
10. [Best Practices and Security](#best-practices-and-security)
11. [Extending the Application](#extending-the-application)

---

## Introduction to AI and RAG

### What is RAG (Retrieval-Augmented Generation)?

**RAG** is a powerful AI architecture that combines the strengths of retrieval systems with generative AI models. Instead of relying solely on a language model's training data, RAG:

1. **Retrieves** relevant information from your documents
2. **Augments** the AI's context with this specific information
3. **Generates** responses based on both the model's knowledge and your data

### Why RAG Matters for .NET Developers

- **Privacy**: Keep sensitive data on-premises with local LLMs
- **Accuracy**: Get responses based on your specific documents
- **Cost**: No cloud API fees for AI processing
- **Control**: Full control over AI model behavior and data handling

### What You'll Build

By the end of this tutorial, you'll have a complete **ChatPDF** application that:
- Processes PDF documents using AI
- Enables natural language conversations about document content
- Provides accurate answers with source citations
- Runs entirely on your local infrastructure

---

## Understanding the Technology Stack

### Microsoft.Extensions.AI Framework

The foundation of our AI integration is Microsoft's new unified AI framework:

```csharp
// Unified API across different AI providers
IChatClient chatClient;
IEmbeddingGenerator embeddingGenerator;
```

**Key Benefits:**
- **Provider-agnostic**: Switch between OpenAI, Azure OpenAI, Ollama, etc.
- **Dependency injection**: Native .NET DI container integration
- **Observability**: Built-in telemetry and logging
- **Function calling**: Automatic tool invocation

### Local AI with Ollama

**Ollama** provides local AI model hosting:
- **Privacy**: No data leaves your machine
- **Performance**: GPU acceleration available
- **Models**: Access to Llama, Mistral, CodeLlama, and more
- **API Compatible**: OpenAI-style REST API

### Vector Database with Qdrant

**Qdrant** handles semantic search:
- **High Performance**: Optimized for similarity search
- **Scalability**: Handle millions of vectors
- **Filtering**: Search within specific documents
- **Real-time**: Instant updates when documents change

---

## Project Architecture Overview

### High-Level Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Blazor UI     │    │   .NET Services  │    │  AI Components  │
│                 │    │                  │    │                 │
│ • Chat Interface│───▶│ • DocumentService│───▶│ • Ollama LLM    │
│ • File Upload   │    │ • DataIngestor   │    │ • Embeddings    │
│ • Document List │    │ • SemanticSearch │    │ • Function Call │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌─────────────────┐
                       │ Vector Database │
                       │                 │
                       │ • Qdrant DB     │
                       │ • Document Chunks│
                       │ • Embeddings    │
                       └─────────────────┘
```

### Data Flow: Document Processing

```
PDF Upload → Text Extraction → Chunking → Embedding Generation → Vector Storage
     │              │             │              │                    │
     ▼              ▼             ▼              ▼                    ▼
┌─────────┐  ┌─────────────┐  ┌─────────┐  ┌─────────────┐  ┌─────────────┐
│PDF File │  │   Parsed    │  │ Text    │  │   Vector    │  │   Qdrant    │
│10MB max │  │    Text     │  │ Chunks  │  │ Embeddings  │  │  Database   │
└─────────┘  └─────────────┘  └─────────┘  └─────────────┘  └─────────────┘
```

### Data Flow: Chat Interaction

```
User Question → Semantic Search → Context Retrieval → LLM Processing → Response + Citations
      │               │                  │               │                   │
      ▼               ▼                  ▼               ▼                   ▼
┌─────────────┐ ┌─────────────┐ ┌─────────────────┐ ┌─────────────┐ ┌─────────────┐
│"What is...?"│ │Search Vector│ │Top 5 Relevant   │ │LLM processes│ │Answer with  │
│   Query     │ │   Database  │ │   Chunks        │ │context+query│ │page numbers │
└─────────────┘ └─────────────┘ └─────────────────┘ └─────────────┘ └─────────────┘
```

---

## Setting Up Your Development Environment

### Prerequisites

1. **Development Tools**
   ```bash
   # .NET 9 SDK (required)
   dotnet --version  # Should show 9.x.x
   
   # Visual Studio 2022 or VS Code with C# Dev Kit
   ```

2. **Docker Desktop**
   ```bash
   # For running Qdrant vector database
   docker --version
   ```

3. **Ollama Installation**
   ```bash
   # Download from https://ollama.ai/
   # Or install via package manager
   
   # Windows (PowerShell)
   winget install ollama
   
   # macOS
   brew install ollama
   
   # Linux
   curl -fsSL https://ollama.ai/install.sh | sh
   ```

### Setting Up AI Models

```bash
# Start Ollama service
ollama serve

# Download required models (in separate terminal)
ollama pull llama3.2          # Chat model (4.7GB)
ollama pull nomic-embed-text  # Embedding model (274MB)

# Verify models are installed
ollama list
```

### Starting Dependencies

```bash
# Start Qdrant vector database
docker run -d -p 6333:6333 qdrant/qdrant

# Verify Qdrant is running
curl http://localhost:6333/health
```

### Project Setup

```bash
# Clone the project (replace with your repository URL)
git clone <your-repository-url>
cd ChatPDF

# Restore dependencies
dotnet restore

# Run the application
cd ChatPDF.AppHost
dotnet run
```

**Access Points:**
- Main Application: https://localhost:7002
- Aspire Dashboard: https://localhost:15888

---

## Core AI Components Deep Dive

### 1. Microsoft.Extensions.AI Configuration

The `Program.cs` file shows how to configure the AI services:

```csharp
// Chat client configuration - Program.cs:26-33
var chatClientBuilder = builder.AddOllamaApiClient("chat")
    .AddChatClient();

if (ollamaConfig.Chat.EnableFunctionInvocation)
{
    chatClientBuilder.UseFunctionInvocation();
}

// Embeddings client configuration - Program.cs:42-43
builder.AddOllamaApiClient("embeddings")
    .AddEmbeddingGenerator();
```

**Key Concepts:**
- **Named Clients**: Separate clients for chat and embeddings
- **Function Invocation**: Enables AI to call .NET methods
- **Configuration**: Model names and settings from `appsettings.json`

### 2. Embedding Generation for Semantic Search

Embeddings convert text into mathematical vectors that capture semantic meaning:

```csharp
// Example: Converting text to vector
var text = "Machine learning algorithms improve performance";
var embedding = await embeddingGenerator.GenerateAsync(text);
// Result: [0.1, -0.3, 0.7, ...] (768 dimensions)
```

**In the DataIngestor (DataIngestor.cs:42):**
```csharp
var newRecords = await source.CreateChunksForDocumentAsync(modifiedDocument);
await chunksCollection.UpsertAsync(newRecords);
```

### 3. Function Calling for Dynamic Search

The AI automatically calls .NET methods based on user queries:

```csharp
// From Chat.razor - AI function that gets called automatically
[Description("Searches for information using a phrase or keyword")]
private async Task<IEnumerable<string>> SearchAsync(string searchPhrase, string? filenameFilter = null)
{
    var results = await Search.SearchAsync(searchPhrase, filenameFilter, maxResults: 5);
    return results.Select(result =>
        $"<result filename=\"{result.DocumentId}\" page_number=\"{result.PageNumber}\">{result.Text}</result>");
}
```

**How it Works:**
1. User asks: "What is machine learning?"
2. AI decides to call `SearchAsync("machine learning", null)`
3. Function returns relevant document chunks
4. AI uses this context to generate the final answer

---

## Building the RAG Pipeline

### Document Ingestion Process

The **DataIngestor** class handles the complete document processing pipeline:

#### 1. Document Detection and Processing

```csharp
// DataIngestor.cs:34-44 - Processing new/modified documents
var modifiedDocuments = await source.GetNewOrModifiedDocumentsAsync(documentsForSource);
foreach (var modifiedDocument in modifiedDocuments)
{
    logger.LogInformation("Processing {documentId}", modifiedDocument.DocumentId);
    await DeleteChunksForDocumentAsync(modifiedDocument);
    
    await documentsCollection.UpsertAsync(modifiedDocument);
    
    var newRecords = await source.CreateChunksForDocumentAsync(modifiedDocument);
    await chunksCollection.UpsertAsync(newRecords);
}
```

#### 2. Text Chunking Strategy

Documents are split into manageable chunks for better retrieval:

```csharp
// Example chunking logic (conceptual)
private IEnumerable<TextChunk> ChunkDocument(string text, int maxChunkSize = 1000)
{
    // Split by paragraphs, then by sentences if needed
    var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var paragraph in paragraphs)
    {
        if (paragraph.Length <= maxChunkSize)
        {
            yield return new TextChunk { Text = paragraph };
        }
        else
        {
            // Further split large paragraphs
            foreach (var chunk in SplitLargeParagraph(paragraph, maxChunkSize))
                yield return chunk;
        }
    }
}
```

#### 3. Vector Storage with Metadata

Each text chunk is stored with rich metadata:

```csharp
// IngestedChunk structure
public class IngestedChunk
{
    public Guid Key { get; set; }
    public string DocumentId { get; set; }     // "research-paper.pdf"
    public int PageNumber { get; set; }        // 5
    public string Text { get; set; }           // Actual text content
    public int ChunkIndex { get; set; }        // Position in document
    public ReadOnlyMemory<float> Vector { get; set; }  // 768-dim embedding
}
```

### Semantic Search Implementation

The **SemanticSearch** service (SemanticSearch.cs:8-16) performs similarity searches:

```csharp
public async Task<IReadOnlyList<IngestedChunk>> SearchAsync(string text, string? documentIdFilter, int maxResults)
{
    var nearest = vectorCollection.SearchAsync(text, maxResults, new VectorSearchOptions<IngestedChunk>
    {
        Filter = documentIdFilter is { Length: > 0 } ? record => record.DocumentId == documentIdFilter : null,
    });

    return await nearest.Select(result => result.Record).ToListAsync();
}
```

**Key Features:**
- **Similarity Search**: Find semantically similar content
- **Document Filtering**: Search within specific PDFs
- **Relevance Ranking**: Results ordered by similarity score

---

## Implementing Chat with Function Calling

### Chat Component Architecture

The main chat interface is built with Blazor Server and demonstrates real-time AI interaction:

#### 1. Message Processing Loop

```csharp
// Chat.razor - Core chat processing
private async Task ProcessUserMessageAsync(string userInput)
{
    // Add user message
    var userMessage = new ChatMessage(ChatRole.User, userInput);
    messages.Add(userMessage);

    // Prepare chat options with function calling
    var chatOptions = new ChatOptions
    {
        Tools = [AIFunctionFactory.Create(SearchAsync)]
    };

    // Stream AI response
    currentResponseMessage = new ChatMessage(ChatRole.Assistant, "");
    
    await foreach (var update in ChatClient.CompleteStreamingAsync(messages, chatOptions))
    {
        if (update.Delta?.Content is { } content)
        {
            currentResponseMessage = currentResponseMessage with
            {
                Text = currentResponseMessage.Text + content
            };
            StateHasChanged();
        }
    }
    
    // Add completed response to history
    messages.Add(currentResponseMessage);
    currentResponseMessage = null;
}
```

#### 2. Function Registration

Functions are automatically discovered and registered:

```csharp
// AI functions are defined with attributes
[Description("Searches for information using a phrase or keyword")]
private async Task<IEnumerable<string>> SearchAsync(
    string searchPhrase, 
    string? filenameFilter = null)
{
    // Implementation details...
}

// Registered in chat options
var chatOptions = new ChatOptions
{
    Tools = [AIFunctionFactory.Create(SearchAsync)]
};
```

#### 3. Streaming Responses

Real-time response updates for better user experience:

```csharp
// Stream processing
await foreach (var update in ChatClient.CompleteStreamingAsync(messages, chatOptions))
{
    if (update.Delta?.Content is { } content)
    {
        // Update UI in real-time
        currentResponseMessage = currentResponseMessage with
        {
            Text = currentResponseMessage.Text + content
        };
        StateHasChanged(); // Trigger UI update
    }
}
```

---

## Vector Database Integration

### Qdrant Configuration

The vector database is configured through .NET's dependency injection:

```csharp
// Program.cs:45-47 - Vector database setup
builder.AddQdrantClient("vectordb");
builder.Services.AddQdrantCollection<Guid, IngestedChunk>(appConfig.VectorDatabase.ChunksCollectionName);
builder.Services.AddQdrantCollection<Guid, IngestedDocument>(appConfig.VectorDatabase.DocumentsCollectionName);
```

### Collection Management

The system automatically manages vector collections:

```csharp
// DataIngestor.cs:20-21 - Ensure collections exist
await chunksCollection.EnsureCollectionExistsAsync();
await documentsCollection.EnsureCollectionExistsAsync();
```

### Vector Operations

#### Upserting Documents

```csharp
// Store or update document chunks
await chunksCollection.UpsertAsync(newRecords);
```

#### Querying Vectors

```csharp
// Search for similar content
var nearest = vectorCollection.SearchAsync(
    text,                    // Query text
    maxResults,             // Number of results
    new VectorSearchOptions<IngestedChunk>
    {
        Filter = record => record.DocumentId == documentFilter
    });
```

#### Cleanup Operations

```csharp
// Delete chunks when documents are removed
var chunksToDelete = await chunksCollection
    .GetAsync(record => record.DocumentId == documentId, int.MaxValue)
    .ToListAsync();
    
if (chunksToDelete.Any())
{
    await chunksCollection.DeleteAsync(chunksToDelete.Select(r => r.Key));
}
```

---

## Testing and Troubleshooting

### Built-in Diagnostics

The application includes a comprehensive diagnostics page at `/diagnostics`:

#### 1. Document Test
- Verifies PDF file detection
- Checks file system permissions
- Validates document metadata

#### 2. Embedding Test
- Tests connection to Ollama embedding service
- Verifies `nomic-embed-text` model availability
- Validates embedding generation

#### 3. Search Test
- Tests Qdrant vector database connectivity
- Verifies collection existence
- Validates similarity search functionality

#### 4. Chat Test
- Tests LLM connectivity
- Verifies `llama3.2` model availability
- Validates function calling capabilities

### Common Issues and Solutions

| Issue | Symptoms | Solution |
|-------|----------|----------|
| **Ollama Not Running** | Embedding/Chat tests fail | Run `ollama serve` in terminal |
| **Missing Models** | Model-specific error messages | `ollama pull llama3.2` and `ollama pull nomic-embed-text` |
| **Qdrant Unavailable** | Search test fails | `docker run -p 6333:6333 qdrant/qdrant` |
| **No Documents** | Empty document list | Upload PDFs to `wwwroot/Data/` folder |
| **Port Conflicts** | Connection refused errors | Check if ports 6333 and 11434 are available |

### Debugging Tips

1. **Check Logs**: Monitor console output for detailed error messages
2. **Aspire Dashboard**: Use https://localhost:15888 for distributed tracing
3. **Model Status**: Run `ollama list` to verify installed models
4. **Docker Health**: Run `docker ps` to check Qdrant container status
5. **Network Connectivity**: Test endpoints manually with curl

```bash
# Test Ollama
curl http://localhost:11434/api/tags

# Test Qdrant
curl http://localhost:6333/health

# Test embedding generation
curl http://localhost:11434/api/embeddings -d '{
  "model": "nomic-embed-text",
  "prompt": "test text"
}'
```

---

## Best Practices and Security

### Security Considerations

#### 1. Data Privacy
```csharp
// All processing happens locally - no cloud API calls
// Documents never leave your infrastructure
if (appConfig.DataIngestion.IngestOnStartup)
{
    // Important: ensure that any content you ingest is trusted
    var pdfPath = Path.Combine(builder.Environment.WebRootPath, appConfig.DataIngestion.PdfDirectory);
    await DataIngestor.IngestDataAsync(app.Services, new PDFDirectorySource(pdfPath));
}
```

#### 2. Input Validation
- File type validation (PDF only)
- File size limits (10MB default)
- Input sanitization for chat messages

#### 3. Access Control
```csharp
// Authentication and authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";
});

app.UseAuthentication();
app.UseAuthorization();
```

### Performance Optimization

#### 1. Chunking Strategy
- Optimal chunk size: 500-1000 characters
- Overlap between chunks: 50-100 characters
- Balance between context and precision

#### 2. Vector Database Tuning
- Index parameters for faster search
- Memory usage optimization
- Batch operations for ingestion

#### 3. Caching Strategies
```csharp
// Cache embeddings to avoid recomputation
// Cache search results for common queries
// Use in-memory caching for frequently accessed documents
```

### Monitoring and Observability

#### 1. OpenTelemetry Integration
```csharp
if (ollamaConfig.Chat.EnableOpenTelemetry)
{
    chatClientBuilder.UseOpenTelemetry(configure: c =>
        c.EnableSensitiveData = builder.Environment.IsDevelopment());
}
```

#### 2. Logging Best Practices
```csharp
logger.LogInformation("Processing {documentId}", modifiedDocument.DocumentId);
logger.LogWarning("Document {documentId} not found", documentId);
logger.LogError(ex, "Failed to process document {documentId}", documentId);
```

---

## Extending the Application

### Adding New Document Types

1. **Implement New Ingestion Source**
```csharp
public class WordDocumentSource : IIngestionSource
{
    public async Task<IEnumerable<IngestedDocument>> GetNewOrModifiedDocumentsAsync(
        IReadOnlyList<IngestedDocument> existingDocuments)
    {
        // Implementation for Word documents
    }
}
```

2. **Register New Source**
```csharp
builder.Services.AddScoped<WordDocumentSource>();
```

### Advanced Search Features

#### 1. Semantic Filtering
```csharp
// Search within date ranges
// Filter by document categories
// Combine multiple search terms
```

#### 2. Query Enhancement
```csharp
// Query expansion using synonyms
// Automatic spelling correction
// Search result ranking customization
```

### Integration Options

#### 1. Different AI Providers
```csharp
// Switch to Azure OpenAI
builder.AddAzureOpenAIClient("azure-openai")
    .AddChatClient();

// Switch to OpenAI
builder.AddOpenAIClient("openai")
    .AddChatClient();
```

#### 2. Alternative Vector Databases
- Azure AI Search
- Pinecone
- Weaviate
- PostgreSQL with pgvector

#### 3. Enterprise Features
- Multi-tenancy support
- Role-based document access
- Audit logging
- Advanced analytics

### Custom AI Functions

Add domain-specific functionality:

```csharp
[Description("Analyzes document sentiment and returns emotional tone")]
private async Task<string> AnalyzeSentimentAsync(string documentId)
{
    // Custom sentiment analysis logic
    return "Positive";
}

[Description("Summarizes document in specified number of sentences")]
private async Task<string> SummarizeDocumentAsync(string documentId, int maxSentences = 3)
{
    // Custom summarization logic
    return "Document summary...";
}

[Description("Extracts key entities from document (people, places, organizations)")]
private async Task<IEnumerable<string>> ExtractEntitiesAsync(string documentId)
{
    // Custom entity extraction logic
    return new[] { "Microsoft", "Seattle", "John Doe" };
}
```

---

## Conclusion

You now have a complete understanding of building AI-powered document chat applications using:

- **Microsoft.Extensions.AI** for unified AI integration
- **Ollama** for local language model hosting
- **Qdrant** for high-performance vector search
- **Blazor Server** for real-time web interfaces
- **RAG architecture** for accurate, context-aware responses

### Key Takeaways

1. **Local AI is Production-Ready**: Modern tools make on-premises AI deployment accessible
2. **RAG Enhances Accuracy**: Combining retrieval with generation provides better results
3. **.NET AI Framework**: Microsoft's unified approach simplifies AI integration
4. **Security First**: Local processing ensures data privacy and compliance

### Next Steps

- Experiment with different AI models
- Add support for additional document types
- Implement advanced search features
- Deploy to production with Docker containers
- Explore enterprise features and integrations

The future of AI-powered applications is here, and .NET developers are perfectly positioned to build them. Start experimenting with this foundation and create amazing AI experiences for your users!

---

**Happy coding! 🚀**

*Built with ❤️ using .NET 9, Blazor Server, and cutting-edge AI technologies*