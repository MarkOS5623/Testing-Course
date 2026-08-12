using System.Text.RegularExpressions;

namespace AiChatEngine.Services;

/// <summary>
/// Rule-based chat service that works without an API key.
/// Perfect for unit testing — no external dependencies.
/// Demonstrates how to program to an interface for testability.
/// </summary>
public class RuleBasedChatService : IChatService
{
    public Task<ChatResponse> ProcessAsync(string userMessage, List<ChatMessage> history)
    {
        var msg = userMessage.Trim().ToLowerInvariant();

        // List meetings
        if (msg.Contains("list") && msg.Contains("meeting") || msg.Contains("show") && msg.Contains("meeting") || msg == "meetings")
        {
            return Task.FromResult(new ChatResponse(
                "Here are the meetings:",
                new ChatAction("list_meetings", new())));
        }

        // List tasks for a meeting
        if ((msg.Contains("list") || msg.Contains("show")) && msg.Contains("task"))
        {
            var meetingId = ExtractGuid(userMessage);
            var parameters = new Dictionary<string, string>();
            if (meetingId is not null) parameters["meetingId"] = meetingId;
            return Task.FromResult(new ChatResponse(
                "Here are the tasks:",
                new ChatAction("list_tasks", parameters)));
        }

        // Add task
        if (msg.Contains("add") && msg.Contains("task") || msg.Contains("create") && msg.Contains("task"))
        {
            var title = ExtractQuoted(userMessage) ?? ExtractAfterKeyword(userMessage, "task");
            var meetingId = ExtractGuid(userMessage);
            var parameters = new Dictionary<string, string>();
            if (title is not null) parameters["title"] = title;
            if (meetingId is not null) parameters["meetingId"] = meetingId;
            return Task.FromResult(new ChatResponse(
                $"Creating task: {title ?? "(please specify title)"}",
                new ChatAction("create_task", parameters)));
        }

        // Complete task
        if (msg.Contains("complete") && msg.Contains("task") || msg.Contains("done") && msg.Contains("task") || msg.Contains("finish") && msg.Contains("task"))
        {
            var taskId = ExtractGuid(userMessage);
            var parameters = new Dictionary<string, string>();
            if (taskId is not null) parameters["taskId"] = taskId;
            return Task.FromResult(new ChatResponse(
                "Marking task as completed.",
                new ChatAction("complete_task", parameters)));
        }

        // Delete task
        if (msg.Contains("delete") && msg.Contains("task") || msg.Contains("remove") && msg.Contains("task"))
        {
            var taskId = ExtractGuid(userMessage);
            var parameters = new Dictionary<string, string>();
            if (taskId is not null) parameters["taskId"] = taskId;
            return Task.FromResult(new ChatResponse(
                "Deleting task.",
                new ChatAction("delete_task", parameters)));
        }

        // Register for meeting
        if (msg.Contains("register") || msg.Contains("sign up") || msg.Contains("signup"))
        {
            var meetingId = ExtractGuid(userMessage);
            var parameters = new Dictionary<string, string>();
            if (meetingId is not null) parameters["meetingId"] = meetingId;
            return Task.FromResult(new ChatResponse(
                "I'll register you for the meeting.",
                new ChatAction("register", parameters)));
        }

        // Meeting details
        if (msg.Contains("detail") || msg.Contains("info") && msg.Contains("meeting"))
        {
            var meetingId = ExtractGuid(userMessage);
            var parameters = new Dictionary<string, string>();
            if (meetingId is not null) parameters["meetingId"] = meetingId;
            return Task.FromResult(new ChatResponse(
                "Here are the meeting details:",
                new ChatAction("get_meeting", parameters)));
        }

        // Help / fallback
        return Task.FromResult(new ChatResponse(
            "I can help you with:\n" +
            "- List meetings\n" +
            "- Show meeting details (provide meeting ID)\n" +
            "- Add task to a meeting (e.g. 'add task \"Prepare slides\" for <meetingId>')\n" +
            "- Complete task (provide task ID)\n" +
            "- Delete task (provide task ID)\n" +
            "- Register for a meeting\n" +
            "- List tasks for a meeting\n\n" +
            "Try: 'list meetings' or 'add task \"Review agenda\" for <meetingId>'"));
    }

    private static string? ExtractGuid(string text)
    {
        var match = Regex.Match(text, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return match.Success ? match.Value : null;
    }

    private static string? ExtractQuoted(string text)
    {
        var match = Regex.Match(text, "\"([^\"]+)\"");
        if (match.Success) return match.Groups[1].Value;
        match = Regex.Match(text, "'([^']+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractAfterKeyword(string text, string keyword)
    {
        var idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var after = text[(idx + keyword.Length)..].Trim().TrimStart(':').Trim();
        // Remove trailing "for <guid>"
        var forIdx = after.IndexOf(" for ", StringComparison.OrdinalIgnoreCase);
        if (forIdx > 0) after = after[..forIdx].Trim();
        return string.IsNullOrEmpty(after) ? null : after;
    }
}
