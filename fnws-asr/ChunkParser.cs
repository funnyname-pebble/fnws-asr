using System.Text;

namespace fnws_asr;

public class ChunkParser {
    public static async IAsyncEnumerable<byte[]> ParseChunksAsync(Stream stream, string contentType) {
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

    private static int FindBoundary(List<byte> data, byte[] boundary) {
        for (var i = 0; i <= data.Count - boundary.Length; i++) {
            var match = !boundary.Where((t, j) => data[i + j] != t).Any();

            if (match) {
                return i;
            }
        }

        return -1;
    }
}