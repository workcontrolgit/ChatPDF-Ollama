namespace ChatPDF.Web.Configuration;

public class OllamaConfiguration
{
    public const string SectionName = "Ollama";
    
    public ChatConfiguration Chat { get; set; } = new();
    public EmbeddingsConfiguration Embeddings { get; set; } = new();
}

public class ChatConfiguration
{
    public string ModelName { get; set; } = "llama3.1";
    public bool EnableFunctionInvocation { get; set; } = true;
    public bool EnableOpenTelemetry { get; set; } = false;
}

public class EmbeddingsConfiguration
{
    public string ModelName { get; set; } = "nomic-embed-text";
}