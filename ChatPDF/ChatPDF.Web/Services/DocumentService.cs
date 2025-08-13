using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.Options;
using ChatPDF.Web.Configuration;

namespace ChatPDF.Web.Services;

public class DocumentService(
    ILogger<DocumentService> logger,
    IWebHostEnvironment environment,
    IOptions<ApplicationConfiguration> appConfig,
    VectorStoreCollection<Guid, IngestedChunk> chunksCollection,
    VectorStoreCollection<Guid, IngestedDocument> documentsCollection)
{
    public async Task<bool> DeleteDocumentAsync(string fileName)
    {
        try
        {
            // Get the full file path
            var dataPath = Path.Combine(environment.WebRootPath, appConfig.Value.DataIngestion.PdfDirectory);
            var filePath = Path.Combine(dataPath, fileName);

            // Check if file exists
            if (!File.Exists(filePath))
            {
                logger.LogWarning("File {fileName} not found at {filePath}", fileName, filePath);
                return false;
            }

            // Find the document in the vector database using the filename as DocumentId
            var documentsToDelete = await documentsCollection
                .GetAsync(doc => doc.DocumentId == fileName, top: int.MaxValue)
                .ToListAsync();

            // Delete associated chunks from vector database
            foreach (var document in documentsToDelete)
            {
                var chunksToDelete = await chunksCollection
                    .GetAsync(chunk => chunk.DocumentId == document.DocumentId, top: int.MaxValue)
                    .ToListAsync();

                if (chunksToDelete.Any())
                {
                    await chunksCollection.DeleteAsync(chunksToDelete.Select(c => c.Key));
                    logger.LogInformation("Deleted {count} chunks for document {documentId}", 
                        chunksToDelete.Count, document.DocumentId);
                }

                // Delete the document record
                await documentsCollection.DeleteAsync(document.Key);
                logger.LogInformation("Deleted document record for {documentId}", document.DocumentId);
            }

            // Delete the physical file
            File.Delete(filePath);
            logger.LogInformation("Deleted physical file {fileName}", fileName);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete document {fileName}", fileName);
            return false;
        }
    }

    public List<string> GetAvailableDocuments()
    {
        var dataPath = Path.Combine(environment.WebRootPath, appConfig.Value.DataIngestion.PdfDirectory);
        if (!Directory.Exists(dataPath))
        {
            return new List<string>();
        }

        return Directory.GetFiles(dataPath, "*.pdf")
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name)
            .ToList()!;
    }
}