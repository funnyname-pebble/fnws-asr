using System.Text;
using SpeexSharp;

namespace fnws_asr;

public class AudioTools {
    static readonly SpeexDecoder decoder = new SpeexDecoder(SpeexMode.Wideband);
    static int frameSize = decoder.FrameSize;
    
    public static byte[] SpeexToPcm(List<byte[]> chunks) {
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
    
    public static byte[] CreateWavFile(byte[] pcmData, int channels, int bitsPerSample, int sampleRate) {
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);

        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = channels * bitsPerSample / 8;
        var subChunk2Size = pcmData.Length;
        var chunkSize = 36 + subChunk2Size;

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
}