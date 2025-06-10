using NReco.VideoConverter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalamityAssetBot.Utils
{
    public static partial class Extensions
    {
        public static async Task<KeyValuePair<string, FileStream>> DownloadFileAsync(string url, string fileName)
        {
            // Ensure the download directory exists
            const string folderPath = "ReuploadFiles";
            Directory.CreateDirectory(folderPath);

            // Download path. Maintains* original file name.
            string path = $"{folderPath}/{fileName}";
            int iterations = 0;

            while (File.Exists(path))
            {
                path = $"{folderPath}/{iterations}{fileName}";
                iterations++;
            }

            // Open the client for download and prepare the file stream.
            var client = new HttpClient();
            var downloadStream = client.GetStreamAsync(url);
            var fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            // Copy the download stream into the file.
            await (await downloadStream).CopyToAsync(fileStream);

            // Dispose of all streams
            await fileStream.DisposeAsync();
            downloadStream.Dispose();
            client.Dispose();

            // Return the file path and a stream representative of the file.
            FileStream stream = new(path, FileMode.Open);
            return new(path, stream);
        }

        public static string ConvertToVideo(string filePath, string? outputName = null)
        {
            // Initialize ffmpeg converter
            FFMpegConverter converter = new();

            string imageType = outputName ?? "submission";

            // Merge the uploaded audio with a simple display image
            FFMpegInput[] inputs = [
                new FFMpegInput($"audio_{imageType}.png") { CustomInputArgs = "-loop 1" },
                new FFMpegInput(filePath)
            ];

            // Output to the same folder as the input, renaming if requested
            string outputFile = outputName is null ?
                filePath.Replace(Path.GetExtension(filePath), ".mp4") :
                filePath.Replace(Path.GetFileName(filePath), outputName + ".mp4");

            // Run conversion
            converter.ConvertMedia(inputs, outputFile, null, new()
            { CustomOutputArgs = "-shortest" });

            // Return path to the new file
            return outputFile;
        }

        public static Task DisposeAllAsync(this Dictionary<string, FileStream> assetStreams)
        {
            return Task.WhenAll(assetStreams.Select(async assetStream =>
            {
                await assetStream.Value.DisposeAsync();
                File.Delete(assetStream.Key);
            }));
        }
    }
}
