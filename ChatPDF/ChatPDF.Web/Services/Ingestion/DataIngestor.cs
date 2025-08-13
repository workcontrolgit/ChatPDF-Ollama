using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace ChatPDF.Web.Services.Ingestion;

public class DataIngestor(
    ILogger<DataIngestor> logger,
    VectorStoreCollection<Guid, IngestedChunk> chunksCollection,
    VectorStoreCollection<Guid, IngestedDocument> documentsCollection)
{
    private static readonly SemaphoreSlim _ingestionSemaphore = new(1, 1);
    public static async Task IngestDataAsync(IServiceProvider services, IIngestionSource source)
    {
        using var scope = services.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<DataIngestor>();
        await ingestor.IngestDataAsync(source);
    }

    public async Task IngestDataAsync(IIngestionSource source)
    {
        await _ingestionSemaphore.WaitAsync();
        try
        {
            logger.LogInformation("Starting ingestion for source {sourceId}", source.SourceId);
            
            await chunksCollection.EnsureCollectionExistsAsync();
            await documentsCollection.EnsureCollectionExistsAsync();

            var sourceId = source.SourceId;
            var documentsForSource = await documentsCollection.GetAsync(doc => doc.SourceId == sourceId, top: int.MaxValue).ToListAsync();
            
            logger.LogInformation("Found {documentCount} existing documents for source {sourceId}", 
                documentsForSource.Count, sourceId);
                
            // Check for and report duplicates
            var duplicates = documentsForSource
                .GroupBy(d => d.DocumentId)
                .Where(g => g.Count() > 1)
                .ToList();
                
            if (duplicates.Any())
            {
                logger.LogWarning("Found {duplicateCount} documents with duplicate DocumentIds", duplicates.Count);
                foreach (var duplicate in duplicates)
                {
                    logger.LogWarning("Duplicate DocumentId: {documentId} has {count} entries", 
                        duplicate.Key, duplicate.Count());
                }
            }

            var deletedDocuments = await source.GetDeletedDocumentsAsync(documentsForSource);
            foreach (var deletedDocument in deletedDocuments)
            {
                logger.LogInformation("Removing ingested data for {documentId}", deletedDocument.DocumentId);
                await DeleteChunksForDocumentAsync(deletedDocument);
                await documentsCollection.DeleteAsync(deletedDocument.Key);
            }

            var modifiedDocuments = await source.GetNewOrModifiedDocumentsAsync(documentsForSource);
            foreach (var modifiedDocument in modifiedDocuments)
            {
                logger.LogInformation("Processing {documentId}", modifiedDocument.DocumentId);
                
                // Find ALL existing documents with same DocumentId to delete their chunks
                var existingDocuments = documentsForSource.Where(d => d.DocumentId == modifiedDocument.DocumentId).ToList();
                if (existingDocuments.Any())
                {
                    logger.LogInformation("Removing {count} existing entries for {documentId}", 
                        existingDocuments.Count, modifiedDocument.DocumentId);
                        
                    foreach (var existingDoc in existingDocuments)
                    {
                        await DeleteChunksForDocumentAsync(existingDoc);
                        await documentsCollection.DeleteAsync(existingDoc.Key);
                    }
                }

                try
                {
                    await documentsCollection.UpsertAsync(modifiedDocument);

                    var newRecords = await source.CreateChunksForDocumentAsync(modifiedDocument);
                    await chunksCollection.UpsertAsync(newRecords);
                    
                    logger.LogInformation("Successfully processed {documentId} with {chunkCount} chunks", 
                        modifiedDocument.DocumentId, newRecords.Count());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing document {documentId}", modifiedDocument.DocumentId);
                    throw;
                }
            }

            logger.LogInformation("Ingestion is up-to-date");
        }
        finally
        {
            _ingestionSemaphore.Release();
        }

        async Task DeleteChunksForDocumentAsync(IngestedDocument document)
        {
            var documentId = document.DocumentId;
            var chunksToDelete = await chunksCollection.GetAsync(record => record.DocumentId == documentId, int.MaxValue).ToListAsync();
            if (chunksToDelete.Any())
            {
                await chunksCollection.DeleteAsync(chunksToDelete.Select(r => r.Key));
            }
        }
    }
}
