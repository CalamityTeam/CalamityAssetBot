using DSharpPlus.Entities;
using System.Net;
using Newtonsoft.Json;
using System.IO;

namespace ArtSubmissionsBot
{
    internal static class FileManager
    {
        internal static readonly string FolderPath = $"{DiscordConnection.FilePrefix}ReuploadFiles/";

        internal static async Task<KeyValuePair<string, FileStream>> DownloadFileAsync(string url, string fileName)
        {
            // Prepare downloading tools
            string path = $"{FolderPath}{fileName}";
            using var wClient = new WebClient();
            bool done = false;

            // Make sure the client is disposed of after downloading
            // And that `done` is marked as true so that the code can continue
            //
            // For some reason WebClient.DownloadFileAsync() is not actually asynchronous :(
            wClient.DownloadFileCompleted += (s, e) =>
            {
                done = true;
                wClient.Dispose();
            };

            wClient.DownloadFileAsync(new Uri(url), path);

            // Actually do something while waiting because sometimes
            // the code likes to break whenever you are waiting on an empty while loop
            Console.WriteLine("Waiting on file download...");
            while (!done)
            {
                Console.Write("\rWaiting on file download...");
            }

            // Return the file path and a stream representative of the file
            FileStream stream = new(path, FileMode.Open);
            return new(path, stream);
        }

        internal static async Task AttachFileAsync(DiscordAttachment attachment, DiscordMessageBuilder message, Dictionary<string, FileStream> files, string overrideName = null)
        {
            // Download the file
            var stream = await DownloadFileAsync(attachment.Url, attachment.FileName);

            // Attach the file with the same name if one is not provided
            if (overrideName is null)
                message.AddFile(attachment.FileName, stream.Value);

            // Otherwise, attach it with the provided name
            else
            {
                string extension = Path.GetExtension(attachment.FileName);
                message.AddFile($"{overrideName}{extension}", stream.Value, true);
            }

            // Add the stream and file location to the dictionary map
            files.Add(stream.Key, stream.Value);
        }

        internal static async Task<string> ResetEphemeralAttachmentURL(this DiscordAttachment attachment)
        {
            // Download the file and reupload it so its ephemeral status is removed
            var file = await DownloadFileAsync(attachment.Url, attachment.FileName);
            var builder = new DiscordMessageBuilder()
                .AddFile(file.Value);
            var message = await Cache.Channels.ImageCache.SendMessageAsync(builder);

            // After the image is reuploaded, delete it from storage, as now Discord has it
            await file.Value.DisposeAsync();
            File.Delete(file.Key);

            // Return the new image url
            return message.Attachments[0].Url;
        }
    }
}
