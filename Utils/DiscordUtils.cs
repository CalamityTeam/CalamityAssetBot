using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using CalamityAssetBot.Hosting;
using DSharpPlus.Entities;

namespace CalamityAssetBot.Utils;

public static partial class Extensions
{
    public static async Task<string> AddFileAsync(this DiscordMessageBuilder builder, DiscordAttachment attachment, string? overrideName = null, Dictionary<string, FileStream>? streams = null)
    {
        // Download the file
        var stream = await DownloadFileAsync(attachment.Url!, attachment.FileName!);

        // Include it in the current builder
        string fileName = overrideName is null ? Path.GetFileName(stream.Key) : $"{overrideName}{Path.GetExtension(stream.Key)}";
        builder.AddFile(fileName, stream.Value, true);

        // Add it to the stream collection, if passed in
        streams?.Add(stream.Key, stream.Value);

        // Return the unfurled url for easy tracking
        return $"attachment://{fileName}";
    }

    public static async Task<string> AddFileAsync(this DiscordMessageBuilder builder, string attachmentUrl, string? overrideName = null, Dictionary<string, FileStream>? streams = null)
    {
        // Download the file
        string downloadFileName = UnfurledMediaFileName.Match(attachmentUrl).Groups[1].Value;
        var stream = await DownloadFileAsync(attachmentUrl, downloadFileName);

        // Include it in the current builder
        string fileName = overrideName is null ? Path.GetFileName(stream.Key) : $"{overrideName}{Path.GetExtension(stream.Key)}";
        builder.AddFile(fileName, stream.Value, true);

        // Add it to the stream collection, if passed in
        streams?.Add(stream.Key, stream.Value);

        // Return the unfurled url for easy tracking
        return $"attachment://{fileName}";
    }

    public static async Task<string> GetPermanentUrlAsync(this DiscordAttachment attachment, string? overrideName = null)
    {
        // Files used in slash commands are considered "ephemeral" and are deleted by Discord after processing
        // We download the file and reupload it to a dedicated channel so that its ephemeral status is removed
        string fileName = overrideName is null ? attachment.FileName! : $"{overrideName}{Path.GetExtension(attachment.FileName)}";
        var file = await DownloadFileAsync(attachment.Url!, fileName);

        var builder = new DiscordMessageBuilder().AddFile(file.Value);
        var message = await Cache.Channels.DataBase.ImageCache.SendMessageAsync(builder);

        // After the image is reuploaded, delete it from storage, as now Discord has it
        await file.Value.DisposeAsync();
        File.Delete(file.Key);

        // Return the new image url
        return message.Attachments[0].Url!;
    }

    public static async Task<string> GetPermanentUrlAsync(string filePath)
    {
        FileStream fileStream = File.OpenRead(filePath);

        var builder = new DiscordMessageBuilder().AddFile(fileStream);
        var message = await Cache.Channels.DataBase.ImageCache.SendMessageAsync(builder);

        // After the image is reuploaded, delete it from storage, as now Discord has it
        await fileStream.DisposeAsync();
        File.Delete(filePath);

        // Return the new image url
        return message.Attachments[0].Url!;
    }

    public static bool IsEmbeddableMedia(this string? mediaType) => mediaType is not null && (mediaType.StartsWith("image") || mediaType.StartsWith("video"));

    public static ulong GetPublicIdFromDevMessage(this DiscordMessage message)
    {
        DiscordLinkButtonComponent jumpButton = (((
                    message.Components![0] as DiscordContainerComponent)!
                .Components[^1] as DiscordSectionComponent)!
            .Accessory as DiscordLinkButtonComponent)!;

        return ulong.Parse(jumpButton.Url.Split('/')[^1]);
    }

    public static TimeSpan Age(this DiscordMessage message) => DateTime.UtcNow - message.Timestamp.UtcDateTime;

    public static bool TryGetMessage(this DiscordChannel channel, ulong id, [NotNullWhen(true)] out DiscordMessage? message)
    {
        try
        {
            message = channel.GetMessageAsync(id, true).GetAwaiter().GetResult();
        }
        catch
        {
            message = null;
        }

        return message is not null;
    }

    public static bool TryRefresh(this DiscordMessage message, [NotNullWhen(true)] out DiscordMessage? refreshed)
    {
        try
        {
            refreshed = message.Channel!.GetMessageAsync(message.Id, true).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            refreshed = null;
            return false;
        }
    }

    [GeneratedRegex(@"(?:\d+/\d+/)([^?]+)")]
    private static partial Regex GenerateUnfurledMediaFileName();

    private static readonly Regex UnfurledMediaFileName = GenerateUnfurledMediaFileName();
    public static string Name(this DiscordUnfurledMediaItem media) => UnfurledMediaFileName.Match(media.Url).Groups[1].Value;

    public static List<DiscordComponent> SanitizeFileComponents(this IReadOnlyList<DiscordComponent> componentList)
    {
        var components = componentList.ToList();

        for (int i = 0; i < components.Count; i++)
        {
            var component = components[i];

            switch (component)
            {
                case DiscordContainerComponent container:
                {
                    var containerComponents = container.Components.SanitizeFileComponents();
                    components[i] = new DiscordContainerComponent(containerComponents, container.IsSpoilered, container.Color);
                    break;
                }

                case DiscordFileComponent file:
                    string attachmentUrl = $"attachment://{file.File.Name()}";
                    components[i] = new DiscordFileComponent(attachmentUrl, false);
                    break;
            }
        }

        return components;
    }

    public static async Task<DiscordMessageBuilder> CopyFilesAsync(this DiscordMessageBuilder builder,
        DiscordMessage message,
        Dictionary<string, FileStream>? assetStreams = null,
        string? fileToExclude = null)
    {
        List<Task> tasks = [];

        tasks.AddRange(message.Attachments
            .Where(a => a.FileName != (fileToExclude?.Replace("attachment://", "") ?? ""))
            .Select(attachment => builder.AddFileAsync(attachment, Path.GetFileNameWithoutExtension(attachment.FileName), assetStreams)));

        await AddFileComponents(message.Components ?? []);

        await Task.WhenAll(tasks);
        return builder;

        async Task AddFileComponents(IReadOnlyList<DiscordComponent> containerComponents)
        {
            foreach (var component in containerComponents)
            {
                switch (component)
                {
                    case DiscordContainerComponent container:
                        await AddFileComponents(container.Components);
                        break;

                    case DiscordFileComponent file:
                        if (file.File.Name() != fileToExclude)
                            tasks.Add(builder.AddFileAsync(file.File.Url, Path.GetFileNameWithoutExtension(file.File.Name()), assetStreams));
                        break;
                }
            }
        }
    }

    public static DiscordMessageBuilder PrepContainer(this DiscordMessageBuilder builder,
        IReadOnlyList<DiscordComponent> components,
        DiscordColor? color = null)
        => builder.EnableV2Components().AddContainerComponent(new(components, false, color));

    public static DiscordMessageBuilder CopyFiles(this DiscordMessageBuilder builder,
        DiscordMessage messageToCopy,
        out Dictionary<string, FileStream> assetStreams,
        string? excludedFile = null)
    {
        assetStreams = [];
        builder.CopyFilesAsync(messageToCopy, assetStreams, excludedFile).GetAwaiter().GetResult();
        return builder;
    }
}