using fnws_asr;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

app.MapGet("/heartbeat", () => "asr")
    .WithName("Heartbeat");

app.MapPost("/NmspServlet", async (HttpRequest req, HttpResponse res, IConfiguration configuration) => { 
        Console.WriteLine("Post request received");
        res.ContentType = "multipart/form-data; boundary=--Nuance_NMSP_vutc5w1XobDdefsYG3wq";
        await using var stream = req.Body;
        var chunks = new List<byte[]>();
        await foreach (var chunk in ChunkParser.ParseChunksAsync(stream, req.ContentType)) {
            chunks.Add(chunk);
        }
        chunks = chunks.Skip(3).ToList();
        if (chunks.Count > 15) {
            chunks = chunks.Skip(12).Take(chunks.Count - 15).ToList();
        }
        
        var pcmArray = AudioTools.SpeexToPcm(chunks);
        
        var audioLengthInSeconds = pcmArray.Length / (4.0f * 16000);
        if (audioLengthInSeconds < 0.5f) {
            Console.WriteLine($"Audio too short: {audioLengthInSeconds:F2} seconds - skipping");
            var content = await Response.CreateResponse([]).Content.ReadAsStringAsync();
            return content;
        }
        
        // Create WAV file in memory
        var wav_data = AudioTools.CreateWavFile(pcmArray, 1, 32, 16000);
         
        try {
            var transcription = await Transcription.TranscribeAudio(wav_data, configuration);
            Console.WriteLine($"Transcription: {transcription.Text}");
            //create words array full of json objects that is as long as the number of words in the transcription
            if (transcription.Text != null) {
                var words = transcription.Text.Split(' ')
                    .Select(word => new Transcription.WordWithConfidence { word = word, confidence = 1.0 })
                    .ToList();
                var content = await Response.CreateResponse(words).Content.ReadAsStringAsync();

                return content;
            } else {
                var content = await Response.CreateResponse([]).Content.ReadAsStringAsync();
                return content;
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Transcription error: {ex.Message}");
            var content = await Response.CreateResponse([]).Content.ReadAsStringAsync();
            return content;
        }
    })
    .WithName("NmspServlet");

app.Run();