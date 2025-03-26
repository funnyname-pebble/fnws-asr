using System.Text;
using System.Text.Json;

namespace fnws_asr;

public class Response {
    public static HttpResponseMessage CreateResponse(List<Transcription.WordWithConfidence> words) {
        const string boundary = "--Nuance_NMSP_vutc5w1XobDdefsYG3wq";
        var body = new StringBuilder();

        // Start with CRLF as in Python output
        body.Append("\r\n");
        body.Append($"--{boundary}\r\n");
        body.Append("Content-Type: application/JSON; charset=utf-8\r\n");
        body.Append("Content-Disposition: form-data; name=\"QueryResult\"\r\n\r\n");

        // Process words list
        if (words.Count > 0) {
            if (string.IsNullOrEmpty(words[0].word)) {
                //pop the first element
                words.RemoveAt(0);
            }
        }

        // Create exact JSON structure matching Python format
        var payload = new {
            words = new[] { words } // Double array wrap like [[{...}]]
        };

        // Serialize with exact Python formatting
        var options = new JsonSerializerOptions {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        body.Append(JsonSerializer.Serialize(payload, options));

        // Add closing boundary
        body.Append($"\r\n--{boundary}--\r\n");

        var content = new StringContent(body.ToString(), Encoding.UTF8);
        content.Headers.Remove("Content-Type");
        content.Headers.TryAddWithoutValidation("Content-Type", 
            $"multipart/form-data; boundary={boundary}");

        return new HttpResponseMessage {
            Content = content
        };
    }
}