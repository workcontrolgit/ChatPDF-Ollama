using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.AI;
using ChatPDF.Web.Models;

namespace ChatPDF.Web.Services;

public class ChatHistoryService
{
    private readonly IWebHostEnvironment _environment;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly string _dataPath;

    public ChatHistoryService(IWebHostEnvironment environment, AuthenticationStateProvider authenticationStateProvider)
    {
        _environment = environment;
        _authenticationStateProvider = authenticationStateProvider;
        _dataPath = Path.Combine(_environment.WebRootPath, "ChatHistory");
        Directory.CreateDirectory(_dataPath);
    }

    private async Task<string> GetUserIdAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        
        if (authState.User.Identity?.IsAuthenticated == true)
        {
            // Try multiple claim types to get a reliable user identifier
            var userId = authState.User.FindFirst("sub")?.Value ??
                        authState.User.FindFirst("name")?.Value ??
                        authState.User.FindFirst("preferred_username")?.Value ??
                        authState.User.FindFirst("email")?.Value ??
                        authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                        authState.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ??
                        authState.User.Identity.Name;
            
            return !string.IsNullOrEmpty(userId) ? userId : "anonymous";
        }
        
        return "anonymous";
    }

    private string GetUserFilePath(string userId)
    {
        var safeUserId = string.Join("_", userId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_dataPath, $"{safeUserId}_chats.json");
    }

    public async Task<List<ChatSession>> GetUserChatSessionsAsync()
    {
        var userId = await GetUserIdAsync();
        var filePath = GetUserFilePath(userId);
        
        if (!File.Exists(filePath))
            return new List<ChatSession>();

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var sessions = JsonSerializer.Deserialize<List<ChatSession>>(json) ?? new List<ChatSession>();
            
            // Additional security: filter by user ID to ensure only user's own sessions
            return sessions.Where(s => !s.IsDeleted && s.UserId == userId)
                         .OrderByDescending(s => s.UpdatedAt)
                         .ToList();
        }
        catch
        {
            return new List<ChatSession>();
        }
    }

    public async Task<ChatSession?> GetChatSessionAsync(Guid sessionId)
    {
        var sessions = await GetUserChatSessionsAsync();
        var session = sessions.FirstOrDefault(s => s.Id == sessionId && !s.IsDeleted);
        
        // Additional security check: verify session belongs to current user
        if (session != null)
        {
            var currentUserId = await GetUserIdAsync();
            if (session.UserId != currentUserId)
            {
                throw new UnauthorizedAccessException($"Session {sessionId} does not belong to current user");
            }
        }
        
        return session;
    }

    public async Task<ChatSession> CreateChatSessionAsync(string? title = null)
    {
        var userId = await GetUserIdAsync();
        var session = new ChatSession
        {
            Title = title ?? "New Chat",
            UserId = userId
        };

        await SaveChatSessionAsync(session);
        return session;
    }

    public async Task SaveChatSessionAsync(ChatSession session)
    {
        var userId = await GetUserIdAsync();
        if (session.UserId != userId)
            throw new UnauthorizedAccessException("Cannot save session for different user");

        session.UpdatedAt = DateTime.UtcNow;
        
        var sessions = await GetUserChatSessionsAsync();
        var existingIndex = sessions.FindIndex(s => s.Id == session.Id);
        
        if (existingIndex >= 0)
            sessions[existingIndex] = session;
        else
            sessions.Add(session);

        await SaveUserSessionsAsync(userId, sessions);
    }

    public async Task DeleteChatSessionAsync(Guid sessionId)
    {
        var userId = await GetUserIdAsync();
        var sessions = await GetUserChatSessionsAsync();
        var session = sessions.FirstOrDefault(s => s.Id == sessionId);
        
        if (session != null)
        {
            session.IsDeleted = true;
            await SaveUserSessionsAsync(userId, sessions);
        }
    }

    public async Task AddMessageToSessionAsync(Guid sessionId, ChatMessage message)
    {
        var session = await GetChatSessionAsync(sessionId);
        if (session == null)
            throw new ArgumentException("Session not found");

        session.Messages.Add(message);
        
        // Auto-generate title from first user message
        if (session.Messages.Count == 1 && message.Role == ChatRole.User)
        {
            session.GenerateTitle();
        }

        await SaveChatSessionAsync(session);
    }

    public async Task AddMessagesToSessionAsync(Guid sessionId, IEnumerable<ChatMessage> messages)
    {
        var session = await GetChatSessionAsync(sessionId);
        if (session == null)
            throw new ArgumentException("Session not found");

        foreach (var message in messages)
        {
            session.Messages.Add(message);
        }

        // Auto-generate title from first user message if needed
        if (string.IsNullOrEmpty(session.Title) || session.Title == "New Chat")
        {
            session.GenerateTitle();
        }

        await SaveChatSessionAsync(session);
    }

    private async Task SaveUserSessionsAsync(string userId, List<ChatSession> sessions)
    {
        var filePath = GetUserFilePath(userId);
        var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<int> GetUserChatCountAsync()
    {
        var sessions = await GetUserChatSessionsAsync();
        return sessions.Count;
    }

    public async Task<string> GetCurrentUserIdAsync()
    {
        return await GetUserIdAsync();
    }

    public async Task<Dictionary<string, string>> GetUserClaimsAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var claims = new Dictionary<string, string>();
        
        if (authState.User.Identity?.IsAuthenticated == true)
        {
            foreach (var claim in authState.User.Claims)
            {
                claims[claim.Type] = claim.Value;
            }
        }
        
        return claims;
    }

    public async Task<ChatSession?> GetMostRecentSessionAsync()
    {
        var sessions = await GetUserChatSessionsAsync();
        
        // Return the most recently updated session that has actual messages
        return sessions
            .Where(s => s.Messages.Any(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant))
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefault();
    }

    public async Task CleanupEmptySessionsAsync()
    {
        var userId = await GetUserIdAsync();
        var sessions = await GetUserChatSessionsAsync();
        
        // Find sessions with no user/assistant messages (only system messages)
        var emptySessions = sessions.Where(s => 
            !s.Messages.Any(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)).ToList();
        
        if (emptySessions.Any())
        {
            // Mark empty sessions as deleted
            foreach (var emptySession in emptySessions)
            {
                emptySession.IsDeleted = true;
            }
            
            await SaveUserSessionsAsync(userId, sessions);
        }
    }
}