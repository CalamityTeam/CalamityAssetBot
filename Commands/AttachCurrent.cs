using CalamityAssetBot.Utils;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalamityAssetBot.Commands
{
    [UsedImplicitly]
    public class AttachCurrentCommand
    {
        [Command("attachcurrent")]
        [Description("Attaches a \"current asset\" to a given submission")]
        [RegisterToGuilds(Cache.Servers.IDs.DevServerID)]
        public async Task AttachCurrentAsync(SlashCommandContext ctx,
            [Parameter("message_id")] [Description("The ID of the message to attach an asset to")]
            string id,

            [Parameter("asset")][Description("The asset to attach")]
            DiscordAttachment? attachment = null)
        {
            // Check channels
            if (ctx.Channel != Cache.Channels.DevServer.AssetDiscussion && ctx.Channel != Cache.Channels.DevServer.AssetVoting)
            {
                await ctx.RespondAsync($"Please attach assets in either <#{Cache.Channels.DevServer.AssetDiscussion.Id}> or <#{Cache.Channels.DevServer.AssetVoting.Id}>", true);
                return;
            }
            
            // ID casting check
            if (!ulong.TryParse(id, out ulong devId))
            {
                await ctx.RespondAsync($"Invalid message ID", true);
                return;
            }

            // Attempt to retrieve the message(s) from dev and public
            if (!Cache.Channels.DevServer.AssetVoting.TryGetMessage(devId, out var devMessage))
            {
                await ctx.RespondAsync($"Unable to find message with the specified ID", true);
                return;
            }

            // This doesn't need to throw if not found
            var publicId = devMessage.GetPublicIdFromDevMessage();
            Cache.Channels.ArtServer.AssetSubmissions.TryGetMessage(publicId, out var publicMessage);

            if (!devMessage.Author!.IsCurrent || (devMessage.Components?.Count ?? 0) == 0 || devMessage.Components![0] is not DiscordContainerComponent)
            {
                await ctx.RespondAsync($"Unable to attach current asset to the given message", true);
                return;
            }
            
            // Retrieve components
            var devContainer = (devMessage.Components![0] as DiscordContainerComponent)!;
            var devComponents = devContainer.Components.SanitizeFileComponents();

            var publicContainer = publicMessage?.Components![0] as DiscordContainerComponent ?? null;
            var publicComponents = publicMessage is null ? null : publicContainer!.Components.SanitizeFileComponents();
            string? fileToRemove = null;

            // Remove "unembeddable current" if it already exists
            // The 5th item will be the current if it was included, otherwise it will be a separator
            if (devComponents[4] is not DiscordSeparatorComponent)
            {
                // If it's an attached file, we want to remove it from the message data
                if (devComponents[4] is DiscordFileComponent file)
                    fileToRemove = file.File.Url.Replace("attachment://", "");

                devComponents.RemoveAt(4);
                publicComponents?.RemoveAt(3);
            }

            // Remove thumbnail if it already exists
            else if (devComponents[0] is DiscordSectionComponent section)
            {
                devComponents[0] = section.Components[0];

                if (publicComponents is not null)
                    publicComponents[0] = (publicComponents[0] as DiscordSectionComponent)!.Components[0];
            }

            // At this state, the components are structured as if there was never
            // a "current asset" included, even if one was previously
            DiscordMessageBuilder builder = new DiscordMessageBuilder().CopyFiles(devMessage, out var assetStreams, fileToRemove);

            // Remove the "current asset" if one is not being assigned
            if (attachment is null)
            {
                builder.PrepContainer(devComponents, devContainer.Color);
                
                await devMessage.ModifyAsync(builder);
                await ctx.RespondAsync("Done!");

                // Update public message if it was found
                if (publicComponents is null)
                {
                    await assetStreams.DisposeAllAsync();
                    return;
                }

                builder.ClearComponents();
                builder.PrepContainer(publicComponents, publicContainer!.Color);
                
                await publicMessage!.ModifyAsync(builder);
                await assetStreams.DisposeAllAsync();
                return;
            }

            // Retrieve the asset name from the submission title
            string assetName = (devComponents[0] as DiscordTextDisplayComponent)!.Content.Split('\n')[0].Replace("## ", "");

            // Attach our new current asset
            // For images, place it as a thumbnail in the top right
            if (attachment.MediaType?.StartsWith("image") ?? false)
            {
                string currentUrl = await attachment.GetPermanentUrlAsync("addedcurrent");
                devComponents[0] = new DiscordSectionComponent(
                    devComponents[0],
                    new DiscordThumbnailComponent(currentUrl, $"{assetName} current asset", false));
                
                // Update public if found
                if (publicComponents is not null)
                    publicComponents[0] = new DiscordSectionComponent(
                        publicComponents[0],
                        new DiscordThumbnailComponent(currentUrl, $"{assetName} current asset", false));
            }

            // For videos, place it below the asset submission
            else if (attachment.MediaType?.StartsWith("video") ?? false)
            {
                var displayUrl = await attachment.GetPermanentUrlAsync("addedcurrent");
                devComponents.Insert(4, new DiscordMediaGalleryComponent([
                    new(displayUrl, $"{assetName} current asset")
                ]));
                
                // Update public if found
                publicComponents?.Insert(3, new DiscordMediaGalleryComponent([
                    new(displayUrl, $"{assetName} current asset")
                ]));
            }

            // For audios, convert to video, then place it below the asset submission
            else if (attachment.MediaType?.StartsWith("audio") ?? false)
            {
                // Download the file and dispose the stream so we can pass it to ffmpeg
                var fileToConvert = await Extensions.DownloadFileAsync(attachment.Url!, attachment.FileName!);
                await fileToConvert.Value.DisposeAsync();

                // Convert the file and delete the original
                string converted = Extensions.ConvertToVideo(fileToConvert.Key, "addedcurrent");
                File.Delete(fileToConvert.Key);

                // Attach as a display
                string displayUrl = await Extensions.GetPermanentUrlAsync(converted);
                devComponents.Insert(4, new DiscordMediaGalleryComponent([
                    new(displayUrl, $"{assetName} current asset")
                ]));
                
                // Update public if found
                publicComponents?.Insert(3, new DiscordMediaGalleryComponent([
                    new(displayUrl, $"{assetName} current asset")
                ]));
            }

            // Otherwise just attach it as a file
            else
            {
                string file = await builder.AddFileAsync(attachment, "addedcurrent", assetStreams);
                devComponents.Insert(4, new DiscordFileComponent(
                    file, false));
                
                publicComponents?.Insert(3, new DiscordFileComponent(
                    file, false));
            }
            
            // Update dev message
            builder.PrepContainer(devComponents, devContainer.Color);
            
            await devMessage.ModifyAsync(builder);
            await ctx.RespondAsync("Done!");

            if (publicComponents is null)
            {
                await assetStreams.DisposeAllAsync();
                return;
            }
            
            // Update the public message if it was found
            builder.ClearComponents();
            builder.PrepContainer(publicComponents, publicContainer!.Color);
            
            await publicMessage!.ModifyAsync(builder);
            await assetStreams.DisposeAllAsync();
        }
    }
}
