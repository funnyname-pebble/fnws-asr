using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace fnws_asr;
public abstract class Transcription {
    public static async Task<TranscriptionResponse> TranscribeAudio(byte[] wavData, IConfiguration configuration) {
    // Get API key from environment variable
    var apiKey = configuration["GROQ_API_KEY"] ?? 
                     Environment.GetEnvironmentVariable("GROQ_API_KEY");
    if (string.IsNullOrEmpty(apiKey)) {
        throw new Exception("GROQ_API_KEY environment variable not set");
    }
    
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    
    var content = new MultipartFormDataContent();
    
    // Add the WAV file
    var fileContent = new ByteArrayContent(wavData);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
    content.Add(fileContent, "file", "audio.wav");
    
    // Add other parameters
    content.Add(new StringContent("whisper-large-v3-turbo"), "model");
    content.Add(new StringContent("0"), "temperature");
    content.Add(new StringContent("json"), "response_format");
    
    var response = await client.PostAsync("https://api.groq.com/openai/v1/audio/transcriptions", content);
    response.EnsureSuccessStatusCode();
    
    var jsonResponse = await response.Content.ReadAsStringAsync();
    var transcription = JsonSerializer.Deserialize<TranscriptionResponse>(jsonResponse);

    return transcription;
}
public class TranscriptionResponse {
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("segments")]
    public List<Segment>? Segments { get; set; }
    
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

public abstract class Segment {
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("start")]
    public float Start { get; set; }
    
    [JsonPropertyName("end")]
    public float End { get; set; }
    
    [JsonPropertyName("words")]
    public List<Word>? Words { get; set; }
}

public abstract class Word {
    [JsonPropertyName("word")]
    public string? WordText { get; set; }
    
    [JsonPropertyName("start")]
    public float Start { get; set; }
    
    [JsonPropertyName("end")]
    public float End { get; set; }
}

public class WordWithConfidence {
    public string? word { get; set; }
    public double confidence { get; set; }
}
}