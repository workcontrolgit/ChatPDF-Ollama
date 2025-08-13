# Quick Fix for Duplicate Key Error

Since you're getting the duplicate key error for "Outgoing-IRA-Transfer-Instructions (3)_20250813_085954.pdf", here's the quickest solution:

## Option 1: Use Development Endpoint (Recommended)

1. Start the application
2. Open your browser and go to one of these endpoints:

```
POST https://localhost:7002/dev/clear-document/Outgoing-IRA-Transfer-Instructions%20(3)_20250813_085954.pdf
```

Or to clear all data:
```
POST https://localhost:7002/dev/clear-all-data
```

## Option 2: Restart Qdrant Container (Nuclear Option)

If the endpoints don't work, you can reset the entire vector database:

```bash
# Stop the current Qdrant container
docker stop $(docker ps -q --filter ancestor=qdrant/qdrant)

# Remove the container (this will delete all vector data)
docker rm $(docker ps -aq --filter ancestor=qdrant/qdrant)

# Start a fresh Qdrant container
docker run -d -p 6333:6333 qdrant/qdrant
```

Then restart your application.

## What Was Fixed

The code now:
1. **Handles duplicate DocumentIds** properly in PDFDirectorySource
2. **Uses supported Qdrant filters** (no more `doc => true`)
3. **Cleans up duplicates** before ingestion
4. **Has recovery tools** for manual cleanup

The application should now start without the duplicate key error.