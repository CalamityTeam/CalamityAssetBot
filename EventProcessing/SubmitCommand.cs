using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace ArtSubmissionsBot.EventProcessing
{
    internal class SubmitCommand : ApplicationCommandModule
    {
        [SlashCommand("submit", "Submits an asset")]
        internal async Task SubmitAsync(InteractionContext ctx,
            [Option("Artist", "The artist's name (include all names if there are multiple contributors)")] string artist,
            [Option("Asset-Name", "The asset you are submitting")] string assetName,
            [Option("Display", "The file you want to represent your submission (like a showcase)")] DiscordAttachment display,
            [Option("Current-Asset", "If the asset already exists in-game, you can optionally attach it here")] DiscordAttachment current = null,
            [Option("Assets", "If your submission requires multiple files, put them into a .zip and attach it here")] DiscordAttachment assets = null,
            [Option("Notes", "Any additional notes")] string notes = null)
        {
            if (ctx.Channel.Id != Cache.Channels.IDs.AssetSubmissions)
            {
                await ctx.CreateResponseAsync($"Please keep all asset submissions to <#{Cache.Channels.IDs.AssetSubmissions}>!", true);
                return;
            }

            // Delete the command because it's ugly
            await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.DeferredChannelMessageWithSource, null);
            await ctx.DeleteResponseAsync();

            // Sometimes Discord closes this gateway
            try { await ctx.Channel.TriggerTypingAsync(); } catch { }

            // Initialize the message
            var message = new DiscordMessageBuilder();

            // Keep a list of all active streams to easily dispose of them after they're used
            Dictionary<string, FileStream> streams = new Dictionary<string, FileStream>();

            // Initialize the showcase embed
            var displayEmbed = new DiscordEmbedBuilder()
                .WithAuthor($"Created by: {artist}")
                .WithTitle(assetName)
                .WithColor(Cache.Colors.Submitted)
                .WithFooter($"Submitted by: {ctx.User.Username}#{ctx.User.Discriminator}");

            // Embed the showcase, if it is able to be displayed
            if (display.MediaType.StartsWith("image"))
            {
                string url = await display.ResetEphemeralAttachmentURL();
                displayEmbed.WithImageUrl(url);
            }

            // Otherwise just attach it
            else
                await FileManager.AttachFileAsync(display, message, streams, "Submission");

            // Attach notes, if any, and set up the voting template
            if (!string.IsNullOrWhiteSpace(notes))
                displayEmbed.AddField($"{Cache.Emojis.Notes} Notes", notes, false);
            displayEmbed.AddField($"{Cache.Emojis.Status} Status", $"Submitted", true);
            displayEmbed.AddField($"{Cache.Emojis.Votes} Votes", $"{Cache.Emojis.VoteYes} 0 - 0 {Cache.Emojis.VoteNo}", true);

            // Attach the display embed to the message
            message.AddEmbed(displayEmbed);

            // If there is a current asset, and it can be embedded, attach it in a separate embed
            if (current != null && current.MediaType.StartsWith("image"))
            {
                string url = await current.ResetEphemeralAttachmentURL();
                var currentEmbed = new DiscordEmbedBuilder()
                    .WithTitle("Current")
                    .WithImageUrl(url)
                    .WithColor(Cache.Colors.Submitted); ;

                message.AddEmbed(currentEmbed);
            }

            // Otherwise just attach it
            else if (current != null)
                await FileManager.AttachFileAsync(current, message, streams, "Current");

            // Attach assets, if provided
            if (assets != null)
                await FileManager.AttachFileAsync(assets, message, streams, "Assets");

            // Send the message in art
            var sent = await ctx.Channel.SendMessageAsync(message);

            // Add voting buttons
            // The message ID of the public message is cached in the buttons' custom IDs
            message.AddComponents(new DiscordComponent[]
            {
                    Cache.Buttons.VoteYes(sent.Id),
                    Cache.Buttons.VoteNo(sent.Id)
            });

            // Send in dev
            DiscordMessage dev = await Cache.Channels.AssetVoting.SendMessageAsync(message);

            // Update vote cache
            Cache.VoteCache.Add(dev.Id, new());
            FileManager.SaveVoteCache();

            // Delete files
            foreach (var stream in streams)
            {
                await stream.Value.DisposeAsync();
                File.Delete(stream.Key);
            }
        }
    }
}
