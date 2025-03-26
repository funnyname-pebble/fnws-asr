using SpeexSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var decoder = new SpeexDecoder(SpeexMode.Wideband);
int frameSize = decoder.FrameSize;

app.MapGet("/heartbeat", () => "asr")
    .WithName("Heartbeat");

app.MapPost("/NmspServlet", async (HttpRequest req, HttpResponse res) => { 
        Console.WriteLine("Post request received");
        await using var stream = req.Body;
        var chunks = new List<byte[]>();
        await foreach (var chunk in ParseChunksAsync(stream, req.ContentType)) {
            chunks.Add(chunk);
        }
        chunks = chunks.Skip(3).ToList();
        if (chunks.Count > 15) {
            chunks = chunks.Skip(12).Take(chunks.Count - 15).ToList();
        }
        
        var pcmArray = SpeexToPcm(chunks);
        
        var audioLengthInSeconds = pcmArray.Length / (4.0f * 16000);
        if (audioLengthInSeconds < 0.5f) {
            Console.WriteLine($"Audio too short: {audioLengthInSeconds:F2} seconds - skipping");
            return "";
        }
        
        // Create WAV file in memory
        var wav_data = CreateWavFile(pcmArray, 1, 32, 16000);
        //write wav file with timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        File.WriteAllBytes($"audio-{timestamp}.wav", wav_data);
        
        
        return "OK";
})
.WithName("NmspServlet");

app.Run();
return;

async IAsyncEnumerable<byte[]> ParseChunksAsync(Stream stream, string contentType) {
    var boundary = Encoding.UTF8.GetBytes("--" + contentType.Split(';')[1].Split('=')[1].Trim());
    var thisFrame = new List<byte>();
    var buffer = new byte[4096];
    int bytesRead;

    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
        thisFrame.AddRange(buffer[..bytesRead]);
        var end = FindBoundary(thisFrame, boundary);

        while (end > -1) {
            var frame = thisFrame.GetRange(0, end).ToArray();
            thisFrame.RemoveRange(0, end + boundary.Length);

            if (frame.Length > 0) {
                var frameString = Encoding.UTF8.GetString(frame);
                var splitIndex = frameString.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                if (splitIndex > -1) {
                    var content = frame[(splitIndex + 4)..];
                    yield return content[..^2];
                }
            }

            end = FindBoundary(thisFrame, boundary);
        }
    }

    Console.WriteLine("End of input.");
}

int FindBoundary(List<byte> data, byte[] boundary) {
    for (var i = 0; i <= data.Count - boundary.Length; i++) {
        var match = !boundary.Where((t, j) => data[i + j] != t).Any();

        if (match) {
            return i;
        }
    }

    return -1;
}

byte[] SpeexToPcm(List<byte[]> chunks) {
    var pcm_data = new MemoryStream();
    const float volumeFactor = 1f;
    float maxAmplitude = 0;
    var allDecoded = new List<float>();
        
    foreach (var chunk in chunks) {
        var decoded = new float[frameSize];
        decoder.Decode(chunk, decoded);
        foreach (var value in decoded) {
            var absValue = Math.Abs(value);
            maxAmplitude = Math.Max(maxAmplitude, absValue);
            allDecoded.Add(value);
        }
    }
        
    var normalizationFactor = (maxAmplitude > 0) ? 0.8f / maxAmplitude : 1.0f;

    foreach (var scaledValue in allDecoded.Select(value => value * volumeFactor * normalizationFactor)) {
        pcm_data.Write(BitConverter.GetBytes(scaledValue));
    }

    return pcm_data.ToArray();
}

byte[] CreateWavFile(byte[] pcmData, int channels, int bitsPerSample, int sampleRate) {
    using var memoryStream = new MemoryStream();
    using var writer = new BinaryWriter(memoryStream);

    int byteRate = sampleRate * channels * bitsPerSample / 8;
    int blockAlign = channels * bitsPerSample / 8;
    int subChunk2Size = pcmData.Length;
    int chunkSize = 36 + subChunk2Size;

    // RIFF header
    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(chunkSize);
    writer.Write(Encoding.ASCII.GetBytes("WAVE"));

    // fmt sub-chunk
    writer.Write(Encoding.ASCII.GetBytes("fmt "));
    writer.Write(16); // SubChunk1Size (16 for PCM)
    writer.Write((short)3); // AudioFormat (3 for IEEE float)
    writer.Write((short)channels);
    writer.Write(sampleRate);
    writer.Write(byteRate);
    writer.Write((short)blockAlign);
    writer.Write((short)bitsPerSample);

    // data sub-chunk
    writer.Write(Encoding.ASCII.GetBytes("data"));
    writer.Write(subChunk2Size);
    writer.Write(pcmData);

    return memoryStream.ToArray();
}