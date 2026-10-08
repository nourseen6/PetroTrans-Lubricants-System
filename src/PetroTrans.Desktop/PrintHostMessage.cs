using System.IO;
using System.Text.Json;

namespace PetroTrans.Desktop;

public sealed record PrintHostCommand(string Type, string? FileName);

public static class PrintHostMessage
{
    public static bool TryParse(string? json, out PrintHostCommand? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return TryParse(root.GetString(), out command);
            }

            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            if (type is not "print" and not "pdf")
            {
                return false;
            }

            var fileName = root.TryGetProperty("fileName", out var nameElement) ? nameElement.GetString() : null;
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                fileName = Path.GetFileName(fileName.Trim());
            }

            command = new PrintHostCommand(type, string.IsNullOrWhiteSpace(fileName) ? null : fileName);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
