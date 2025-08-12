namespace ChatPDF.Web.Configuration;

public class ApplicationConfiguration
{
    public const string SectionName = "Application";
    
    public VectorDatabaseConfiguration VectorDatabase { get; set; } = new();
    public DataIngestionConfiguration DataIngestion { get; set; } = new();
    public HttpPipelineConfiguration HttpPipeline { get; set; } = new();
}

public class VectorDatabaseConfiguration
{
    public string ChunksCollectionName { get; set; } = "data-chatpdf-chunks";
    public string DocumentsCollectionName { get; set; } = "data-chatpdf-documents";
}

public class DataIngestionConfiguration
{
    public string PdfDirectory { get; set; } = "Data";
    public bool IngestOnStartup { get; set; } = true;
    public int MaxFileSizeMB { get; set; } = 10;
}

public class HttpPipelineConfiguration
{
    public string ErrorPage { get; set; } = "/Error";
    public int HstsMaxAgeDays { get; set; } = 30;
    public bool UseHttpsRedirection { get; set; } = true;
    public bool UseAntiforgery { get; set; } = true;
}