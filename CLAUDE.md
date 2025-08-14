# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Essential Commands

### Build and Run
```bash
# Build the entire solution
dotnet build

# Run the application (uses .NET Aspire orchestration)
cd ChatPDF.AppHost
dotnet run

# Run tests
dotnet test

# Run specific test file
dotnet test --filter "ClassName=YourTestClass"
```

### Frontend Development
```bash
# Install npm dependencies (for Tailwind CSS)
cd ChatPDF.Web
npm install

# Build Tailwind CSS
npx tailwindcss build -i tailwind.css -o wwwroot/tailwind.css --watch
```

### Database Management (Development Only)
```bash
# Access development endpoints when app is running:
# POST https://localhost:7002/dev/clear-all-data
# POST https://localhost:7002/dev/cleanup-duplicates
# GET https://localhost:7002/dev/clear-document/{documentId}
```

### Prerequisites Setup
```bash
# Start Ollama service
ollama serve

# Pull required AI models
ollama pull llama3.2
ollama pull nomic-embed-text

# Start Qdrant vector database
docker run -d -p 6333:6333 qdrant/qdrant
```

## Architecture Overview

### High-Level System Architecture
This is a **RAG (Retrieval-Augmented Generation)** application that enables AI-powered conversations about PDF documents using local LLMs. The system follows a **microservices architecture** with .NET Aspire orchestration.

**Core Flow**: PDF Upload → Text Extraction & Chunking → Vector Embeddings → Semantic Search → AI Chat with Function Calling

### Key Components

#### **AI Integration Layer** (`Microsoft.Extensions.AI`)
- **ChatClient**: Handles LLM conversations with function calling
- **EmbeddingGenerator**: Converts text to semantic vectors using `nomic-embed-text`
- **Function Calling**: AI automatically invokes `SearchAsync()` method for document retrieval

#### **RAG Pipeline** (`Services.Ingestion/`)
- **DataIngestor**: Orchestrates the entire document processing pipeline with concurrency protection
- **PDFDirectorySource**: Handles PDF file detection, change tracking, and text extraction using PdfPig
- **VectorDatabaseCleaner**: Manages duplicate cleanup and database maintenance

#### **Vector Search** (`SemanticSearch.cs`)
- **Qdrant Integration**: High-performance vector similarity search
- **Filtering**: Search within specific documents or across all documents
- **Metadata**: Stores page numbers, document IDs, and chunk indices with vectors

#### **Blazor Server Frontend**
- **Chat.razor**: Main chat interface with streaming responses and real-time AI function calling
- **DocumentService**: PDF file management and deletion
- **ChatHistoryService**: Persistent conversation storage

### Critical Architecture Patterns

#### **Ingestion Process Flow**
1. **File Detection**: `PDFDirectorySource` scans for new/modified PDFs using file timestamps
2. **Duplicate Handling**: Groups existing documents by `DocumentId`, keeps most recent version
3. **Text Processing**: PdfPig extracts text → Semantic Kernel chunks into ~200 char segments
4. **Vector Generation**: Each chunk becomes a 768-dimensional embedding via `nomic-embed-text`
5. **Storage**: Qdrant stores vectors with metadata (DocumentId, PageNumber, ChunkIndex)

#### **Chat with RAG Pattern**
1. **User Query**: Submitted through Blazor Server real-time connection
2. **Function Calling**: AI automatically calls `SearchAsync(searchPhrase, filenameFilter)`
3. **Semantic Search**: Query vectorized → similarity search in Qdrant → top 5 chunks returned
4. **Context Injection**: Retrieved chunks formatted as `<result>` tags in AI context
5. **Response Generation**: LLM generates response using both its knowledge and retrieved context
6. **Citation**: Response includes exact page numbers and document references

#### **Vector Database Schema**
```csharp
// IngestedChunk (main collection)
{
    Key: Guid,                    // Primary key
    DocumentId: string,           // "document.pdf" 
    PageNumber: int,              // Source page
    Text: string,                 // Chunk content
    ChunkIndex: int,              // Position in document
    Vector: ReadOnlyMemory<float> // 768-dim embedding
}

// IngestedDocument (metadata collection)  
{
    Key: Guid,
    SourceId: string,             // "PDFDirectorySource:Data"
    DocumentId: string,           // "document.pdf"
    DocumentVersion: string       // File timestamp (ISO format)
}
```

## Important Constraints and Patterns

### **Qdrant Filter Limitations**
- **Supported**: `doc => doc.PropertyName == value`
- **NOT Supported**: `doc => true` (constant expressions)
- **Workaround**: Use SourceId-based filtering for bulk operations

### **Concurrency Management**
- **DataIngestor**: Uses `SemaphoreSlim` to prevent concurrent ingestion
- **Duplicate Handling**: Automatically cleans up before processing new documents

### **Error Recovery**
- **Built-in Diagnostics**: `/diagnostics` page tests all system components
- **Development Endpoints**: Database cleanup tools for troubleshooting
- **Graceful Degradation**: System continues if individual documents fail processing

### **Configuration Structure**
- **Aspire Host**: Orchestrates Ollama, Qdrant, and web app containers
- **Connection Strings**: Separate endpoints for chat, embeddings, and vector DB
- **Model Configuration**: Configurable via `appsettings.json` Ollama section
- **Ingestion Settings**: PDF directory, file size limits, startup processing

## Key Files for Understanding

### **Critical Service Files**
- `DataIngestor.cs`: Core RAG pipeline orchestration
- `Chat.razor`: AI conversation implementation with function calling  
- `SemanticSearch.cs`: Vector similarity search logic
- `PDFDirectorySource.cs`: PDF processing and change detection

### **Configuration Files**
- `Program.cs` (Web): DI container setup, AI client configuration, authentication
- `Program.cs` (AppHost): .NET Aspire orchestration and service dependencies
- `appsettings.json`: Model names, connection strings, ingestion settings

### **Important Patterns**
- **Function Calling**: AI functions decorated with `[Description]` attributes
- **Vector Collections**: `VectorStoreCollection<Guid, T>` pattern for Qdrant integration
- **Streaming Responses**: `CompleteStreamingAsync` for real-time UI updates
- **Duplicate Management**: Grouping and version-based cleanup strategies