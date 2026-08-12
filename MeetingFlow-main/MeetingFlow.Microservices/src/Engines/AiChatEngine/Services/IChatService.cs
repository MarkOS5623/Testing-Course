namespace AiChatEngine.Services;

public interface IChatService
{
    Task<ChatResponse> ProcessAsync(string userMessage, List<ChatMessage> history);
}

public record ChatMessage(string Role, string Content);

public record ChatResponse(string Reply, ChatAction? Action = null);

public record ChatAction(string Type, Dictionary<string, string> Parameters);
