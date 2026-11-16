using System.Net;

namespace Fundoo.Notification.Application.Templates;

public static class NoteActivityEmailTemplate
{
    public static string Build(string title, string eventType, Guid noteId)
    {
        var action = eventType == "notes.created.v1" ? "created" : "updated";
        return $"""
            <!DOCTYPE html>
            <html><body style="font-family: Arial, sans-serif;">
              <h2>Your note was {action}</h2>
              <p><strong>{WebUtility.HtmlEncode(title)}</strong></p>
              <p>Note reference: {noteId}</p>
              <p>Fundoo Notes</p>
            </body></html>
            """;
    }
}
