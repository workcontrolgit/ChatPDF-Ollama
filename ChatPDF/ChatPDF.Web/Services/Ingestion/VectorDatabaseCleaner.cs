using Microsoft.Extensions.VectorData;

namespace ChatPDF.Web.Services.Ingestion;

public class VectorDatabaseCleaner(
    ILogger<VectorDatabaseCleaner> logger,
    VectorStoreCollection<Guid, IngestedChunk> chunksCollection,
    VectorStoreCollection<Guid, IngestedDocument> documentsCollection)
{
    public async Task ClearAllDataAsync()
    {
        logger.LogWarning("Clearing all vector database data...");
        
        try
        {
            // Since we can't use generic filters or delete collections directly,
            // we'll use the known SourceId patterns to clear data
            var commonSourceIds = new[] 
            { 
                "PDFDirectorySource:Data", 
                "PDFDirectorySource:wwwroot/Data",
                "PDFDirectorySource:wwwroot\\Data" 
            };
            
            var totalChunksDeleted = 0;
            var totalDocumentsDeleted = 0;
            
            foreach (var sourceId in commonSourceIds)
            {
                try
                {
                    var documentsToDelete = await documentsCollection.GetAsync(doc => doc.SourceId == sourceId, int.MaxValue).ToListAsync();
                    
                    foreach (var document in documentsToDelete)
                    {
                        // Delete chunks for this document
                        var chunks = await chunksCollection.GetAsync(chunk => chunk.DocumentId == document.DocumentId, int.MaxValue).ToListAsync();
                        if (chunks.Any())
                        {
                            await chunksCollection.DeleteAsync(chunks.Select(c => c.Key));
                            totalChunksDeleted += chunks.Count;
                        }
                    }
                    
                    // Delete documents
                    if (documentsToDelete.Any())
                    {
                        await documentsCollection.DeleteAsync(documentsToDelete.Select(d => d.Key));
                        totalDocumentsDeleted += documentsToDelete.Count;
                    }
                    
                    logger.LogInformation("Cleared {documentCount} documents and {chunkCount} chunks for SourceId: {sourceId}", 
                        documentsToDelete.Count, documentsToDelete.Sum(d => 
                            chunksCollection.GetAsync(chunk => chunk.DocumentId == d.DocumentId, int.MaxValue).ToListAsync().Result.Count),
                        sourceId);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not clear data for SourceId: {sourceId}", sourceId);
                }
            }

            logger.LogInformation("Vector database cleared successfully - deleted {totalDocuments} documents and {totalChunks} chunks", 
                totalDocumentsDeleted, totalChunksDeleted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing vector database");
            throw;
        }
    }

    public async Task ClearDocumentAsync(string documentId)
    {
        logger.LogInformation("Clearing data for document {documentId}", documentId);
        
        try
        {
            // Delete chunks for the document
            var chunks = await chunksCollection.GetAsync(chunk => chunk.DocumentId == documentId, int.MaxValue).ToListAsync();
            if (chunks.Any())
            {
                await chunksCollection.DeleteAsync(chunks.Select(c => c.Key));
                logger.LogInformation("Deleted {chunkCount} chunks for document {documentId}", chunks.Count, documentId);
            }

            // Delete the document record
            var documents = await documentsCollection.GetAsync(doc => doc.DocumentId == documentId, int.MaxValue).ToListAsync();
            if (documents.Any())
            {
                await documentsCollection.DeleteAsync(documents.Select(d => d.Key));
                logger.LogInformation("Deleted {documentCount} document records for {documentId}", documents.Count, documentId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing document {documentId}", documentId);
            throw;
        }
    }

    public async Task CleanupDuplicatesAsync(string? sourceId = null)
    {
        logger.LogInformation("Cleaning up duplicate documents for sourceId: {sourceId}", sourceId ?? "all sources");
        
        try
        {
            var allDocuments = new List<IngestedDocument>();
            
            if (!string.IsNullOrEmpty(sourceId))
            {
                // Get documents for specific SourceId
                allDocuments = await documentsCollection.GetAsync(doc => doc.SourceId == sourceId, int.MaxValue).ToListAsync();
            }
            else
            {
                // If no sourceId provided, try common patterns or skip
                var commonSourceIds = new[] 
                { 
                    "PDFDirectorySource:Data", 
                    "PDFDirectorySource:wwwroot/Data",
                    "PDFDirectorySource:wwwroot\\Data" 
                };
                
                foreach (var commonSourceId in commonSourceIds)
                {
                    try
                    {
                        var docs = await documentsCollection.GetAsync(doc => doc.SourceId == commonSourceId, int.MaxValue).ToListAsync();
                        allDocuments.AddRange(docs);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Could not get documents for SourceId: {sourceId}", commonSourceId);
                    }
                }
                
                if (!allDocuments.Any())
                {
                    logger.LogInformation("No documents found for cleanup. This may be expected if no data has been ingested yet.");
                    return;
                }
            }
            
            // Find duplicates
            var duplicateGroups = allDocuments
                .GroupBy(d => d.DocumentId)
                .Where(g => g.Count() > 1)
                .ToList();
                
            if (!duplicateGroups.Any())
            {
                logger.LogInformation("No duplicate documents found");
                return;
            }

            logger.LogInformation("Found {duplicateGroupCount} documents with duplicates", duplicateGroups.Count);

            foreach (var duplicateGroup in duplicateGroups)
            {
                var documentId = duplicateGroup.Key;
                var duplicates = duplicateGroup.OrderByDescending(d => d.DocumentVersion).ToList();
                var toKeep = duplicates.First(); // Keep the most recent version
                var toDelete = duplicates.Skip(1).ToList();

                logger.LogInformation("Document {documentId}: keeping 1, deleting {deleteCount} duplicates", 
                    documentId, toDelete.Count);

                // Delete chunks for duplicate documents
                foreach (var doc in toDelete)
                {
                    var chunks = await chunksCollection.GetAsync(chunk => chunk.DocumentId == doc.DocumentId, int.MaxValue).ToListAsync();
                    if (chunks.Any())
                    {
                        await chunksCollection.DeleteAsync(chunks.Select(c => c.Key));
                        logger.LogInformation("Deleted {chunkCount} chunks for duplicate document {documentId}", 
                            chunks.Count, doc.DocumentId);
                    }
                }

                // Delete duplicate document records
                await documentsCollection.DeleteAsync(toDelete.Select(d => d.Key));
            }

            logger.LogInformation("Duplicate cleanup completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during duplicate cleanup");
            throw;
        }
    }
}