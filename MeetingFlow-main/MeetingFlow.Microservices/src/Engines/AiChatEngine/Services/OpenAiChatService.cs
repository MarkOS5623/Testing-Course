using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AiChatEngine.Services;

/// <summary>
/// OpenAI-backed chat service. Requires OPENAI_API_KEY env var.
/// Falls back to RuleBasedChatService if no key is configured.
/// Demonstrates dependency injection and external service abstraction for testability.
/// </summary>
public class OpenAiChatService : IChatService
{
    private readonly IChatClient _client;
    private const string SystemPrompt = """
        You are a MeetingFlow assistant. You help users manage meetings and tasks.
        When the user wants to perform an action, respond with JSON in this format at the end of your message:
        ACTION: {"type": "<action_type>", "parameters": {<params>}}
        
        Available actions:
        - list_meetings: no parameters needed
        - get_meeting: requires "meetingId"
        - create_task: requires "title" and optionally "meetingId", "assignedTo"
        - complete_task: requires "taskId"
        - delete_task: requires "taskId"
        - list_tasks: optionally "meetingId"
        - register: requires "meetingId", optionally "attendeeId"
        
        If no action is needed (just conversation), don't include the ACTION line.
        Be concise and helpful.
        """;

    public OpenAiChatService(IChatClient client)
    {
        _client = client;
    }

    public async Task<ChatResponse> ProcessAsync(string userMessage, List<ChatMessage> history)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, SystemPrompt)
        };

        foreach (var h in history)
        {
            var role = h.Role.ToLowerInvariant() == "user" ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new(role, h.Content));
        }
        messages.Add(new(ChatRole.User, userMessage));

        var response = await _client.GetResponseAsync(messages);
        var reply = response.Text ?? "I'm not sure how to help with that.";

        // Parse action from response
        ChatAction? action = null;
        var actionIdx = reply.IndexOf("ACTION:", StringComparison.OrdinalIgnoreCase);
        if (actionIdx >= 0)
        {
            var actionJson = reply[(actionIdx + 7)..].Trim();
            reply = reply[..actionIdx].Trim();
            try
            {
                action = System.Text.Json.JsonSerializer.Deserialize<ChatAction>(actionJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* Ignore parse errors — treat as no action */ }
        }

        return new ChatResponse(reply, action);
    }
}
