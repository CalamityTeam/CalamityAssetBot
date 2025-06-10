using System.ComponentModel;
using CalamityAssetBot.Utils;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using JetBrains.Annotations;
using NReco.VideoConverter;

namespace CalamityAssetBot.Commands;

public enum AssetType
{
    Sprite,
    Audio,
    Other
}

[UsedImplicitly]
public class SubmitCommand
{
    [Command("submit")]
    [Description("Submits an asset")]
    [RegisterToGuilds(Cache.Servers.IDs.ArtServerID)]
    [UsedImplicitly]
    public async Task SubmitAsync(SlashCommandContext ctx,
        [Parameter("Artist")] [Description("The artist's name (include all names if there are multiple contributors)")]
        string artist,

        [Parameter("Asset-Name")] [Description("The asset you are submitting")]
        string assetName,

        [Parameter("Display")] [Description("The file you want to represent your submission (like a showcase)")]
        DiscordAttachment display,

        [Parameter("Current-Asset")] [Description("If the asset already exists in-game, you can optionally attach it here")]
        DiscordAttachment? current = null,

        [Parameter("Assets")] [Description("If your submission requires multiple files, put them into a .zip and attach it here")]
        DiscordAttachment? assets = null,

        [Parameter("Notes")] [Description("Any additional notes")]
        string? notes = null)
    {
        if (ctx.Channel != Cache.Channels.ArtServer.AssetSubmissions)
        {
            await ctx.RespondAsync($"Please keep all asset submissions to <#{Cache.Channels.ArtServer.AssetSubmissions.Id}>.", true);
            return;
        }

        // 10mb file limit
        if (display.FileSize > 10_000_000)
        {
            await ctx.RespondAsync($"Please keep asset submissions under 10mb in size.", true);
            return;
        }

        // Switch from "thinking..." to "typing..."
        //await ctx.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredChannelMessageWithSource);
        await ctx.DeleteResponseAsync();

        // Sometimes Discord closes this gateway
        try   { await Cache.Channels.ArtServer.AssetSubmissions.TriggerTypingAsync(); }
        catch { /* ignored */ }

        // Use a dictionary to keep track of attached assets
        Dictionary<string, FileStream> assetStreams = [];

        // We use the "component container" for our message display
        // This does not use a builder, but rather a simple collection
        List<DiscordComponent> containerComponents = [];
        
        // However we do need to attach any uploading files to the message with a standard builder
        DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().EnableV2Components();

        // Assemble the submission description
        // This uses normal markdown rules
        string assetDescription =
            $"## {assetName}\n" +
            $"Created by: {artist}";
        
        // Attach notes below the description if they were included
        if (!string.IsNullOrEmpty(notes))
            assetDescription +=
                $"\n### {Cache.Emojis.Legend.Notes} Notes\n" +
                $"{notes}";

        // Use this to track if the "current asset" should be included later in the container
        bool currentUnembeddable = false;

        // Use this to determine voting weights
        AssetType assetType = AssetType.Sprite;

        // Component list quick reference:
        // 
        // [] Always present
        // -- Conditionally present
        // <> Changed to section in dev
        // ** Only present in dev
        //
        // [0]  : Description /+ embeddable current - Text or Section
        // [1]  :                                   - Separator
        // [2]  : Submission display                - Media or File
        // *3*  : Asset type                        - Drop down
        // -3-  : Unembeddable Current              - Media or File
        // [4]  :                                   - Separator
        // <5>  : Voting display                    - Text
        // [6]  :                                   - Separator
        // <7>  : Status + Dev notes                - Text
        // [8]  :                                   - Separator
        // -9-  : Dev Feedback                      - Text
        // -10- :                                   - Separator
        // -9-  : Assets                            - File
        // -10- :                                   - Separator
        // <11> : Submitter                         - Text

        // Attach the "current asset" if one was included
        if (current is not null)
        {
            // If the current asset is embeddable as a thumbnail, include it as the thumbnail in the top right
            if (current.MediaType?.StartsWith("image") ?? false)
            {
                string currentUrl = await current.GetPermanentUrlAsync("current");
                containerComponents.Add(new DiscordSectionComponent(
                    assetDescription,
                    new DiscordThumbnailComponent(currentUrl, $"{assetName} current asset")));
            }

            // Otherwise, mark it for addition later and just add the description normally
            else
            {
                currentUnembeddable = true;
                containerComponents.Add(new DiscordTextDisplayComponent(
                    assetDescription));
            }
        }

        // Otherwise just use the normal description
        else
        {
            containerComponents.Add(new DiscordTextDisplayComponent(
                assetDescription));
        }
        
        // Slight divider between text and image
        containerComponents.Add(new DiscordSeparatorComponent(false));

        // Embed the asset directly within the display if it is embeddable
        if (display.MediaType.IsEmbeddableMedia())
        {
            if (!display.MediaType!.StartsWith("image"))
                assetType = AssetType.Other;

            string displayUrl = await display.GetPermanentUrlAsync("submission");
            containerComponents.Add(new DiscordMediaGalleryComponent([
                new(displayUrl, $"{assetName} submission")
            ]));
        }

        // If it is audio, we download, pass it through ffmpeg to
        // convert to video, then reupload
        else if (display.MediaType?.StartsWith("audio") ?? false)
        {
            assetType = AssetType.Audio;

            // Download the file and dispose the stream so we can pass it to ffmpeg
            var fileToConvert = await Extensions.DownloadFileAsync(display.Url!, display.FileName!);
            await fileToConvert.Value.DisposeAsync();

            // Convert the file and delete the original
            string converted = Extensions.ConvertToVideo(fileToConvert.Key, "submission");
            File.Delete(fileToConvert.Key);

            // Attach as a display
            string displayUrl = await Extensions.GetPermanentUrlAsync(converted);
            containerComponents.Add(new DiscordMediaGalleryComponent([
                new(displayUrl, $"{assetName} submission")
            ]));
        }

        // Otherwise just attach it to the message
        else
        {
            string displayFile = await messageBuilder.AddFileAsync(display, "submission", assetStreams);
            containerComponents.Add(new DiscordFileComponent(
                displayFile, false));
        }

        // If the current asset is included but not embeddable, we
        // want to put it below the actual submission instead
        if (currentUnembeddable)
        {
            containerComponents.Add(new DiscordSeparatorComponent(false));

            if (current!.MediaType?.StartsWith("video") ?? false)
            {
                string displayUrl = await current.GetPermanentUrlAsync("current");
                containerComponents.Add(new DiscordMediaGalleryComponent([
                    new(displayUrl, $"{assetName} current")
                ]));
            }
            
            // Run through the ffmpeg video conversion procedure if it is audio
            else if (current.MediaType?.StartsWith("audio") ?? false)
            {
                // Download the file and dispose the stream so we can pass it to ffmpeg
                var fileToConvert = await Extensions.DownloadFileAsync(current.Url!, current.FileName!);
                await fileToConvert.Value.DisposeAsync();

                // Convert the file and delete the original
                string converted = Extensions.ConvertToVideo(fileToConvert.Key, "current");
                File.Delete(fileToConvert.Key);

                // Attach as a display
                string displayUrl = await Extensions.GetPermanentUrlAsync(converted);
                containerComponents.Add(new DiscordMediaGalleryComponent([
                    new(displayUrl, $"{assetName} current")
                ]));
            }

            // Otherwise just attach it as a file
            else
            {
                string file = await messageBuilder.AddFileAsync(current, "current", assetStreams);
                containerComponents.Add(new DiscordFileComponent(
                    file, false));
            }
        }
        
        // Create divider between component sections
        containerComponents.Add(new DiscordSeparatorComponent(true));

        // Attach voting and status displays
        containerComponents.Add(new DiscordTextDisplayComponent(
            $"### {Cache.Emojis.Legend.Voting} Votes\n" +
            Extensions.VoteString(0, 0, 0)));

        // Small separator between
        containerComponents.Add(new DiscordSeparatorComponent(false));

        containerComponents.Add(new DiscordTextDisplayComponent(
            $"{Cache.Emojis.Legend.Status} Status: **Submitted**"));
        
        // Create divider between component sections
        containerComponents.Add(new DiscordSeparatorComponent(true));

        // Attach the "assets" file if it was included
        // This is usually a .zip file, but it can technically be anything
        // This one is always attached as a file and not an embed
        if (assets is not null)
        {
            string file = await messageBuilder.AddFileAsync(assets, "assets", assetStreams);
            containerComponents.Add(new DiscordFileComponent(
                file, false));
            
            // Create divider between component sections
            containerComponents.Add(new DiscordSeparatorComponent(true));
        }
        
        // Include the actual user who submitted at the bottom (could be different from the artist, or multiple artists)
        containerComponents.Add(new DiscordTextDisplayComponent($"-# Submitted by: @{ctx.User.Username}"));

        // Send the message in art server
        messageBuilder.AddContainerComponent(new(containerComponents, false, Cache.Colors.Submitted));
        var publicMessage = await Cache.Channels.ArtServer.AssetSubmissions.SendMessageAsync(messageBuilder);

        // Prepare and send the message to dev
        // This adds the extra button components
        messageBuilder.PrepareForDev(containerComponents, publicMessage);

        // Insert the asset type after the asset
        containerComponents.Insert(3, new DiscordActionRowComponent([Cache.Buttons.AssetTypeSelection(assetType)]));

        // Send the message in dev
        var devMessage = await Cache.Channels.DevServer.AssetVoting.SendMessageAsync(messageBuilder);

        // Delete all files used in sending the message
        // Start this task now and await it before leaving execution later
        var deletionTask = assetStreams.DisposeAllAsync();

        // Add voting reactions
        await devMessage.CreateReactionAsync(Cache.Emojis.Votes.Yes);
        await devMessage.CreateReactionAsync(Cache.Emojis.Votes.NeedsImprovement);
        await devMessage.CreateReactionAsync(Cache.Emojis.Votes.No);
        await devMessage.CreateReactionAsync(Cache.Emojis.Votes.Neutral);

        // Ensure streams are disposed before leaving
        await deletionTask.ConfigureAwait(false);
    }
}