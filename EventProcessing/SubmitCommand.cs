using System.ComponentModel;
using DSharpPlus.Entities;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;

namespace ArtSubmissionsBot.EventProcessing
{
    internal class SubmitCommand
    {
        [Command("submit")]
        [Description("Submits an asset")]
        internal async Task SubmitAsync(SlashCommandContext ctx,
            [Parameter("Artist")]
            [Description("The artist's name (include all names if there are multiple contributors)")]
            string artist,
            
            [Parameter("Asset-Name")]
            [Description("The asset you are submitting")]
            string assetName,
            
            [Parameter("Display")]
            [Description("The file you want to represent your submission (like a showcase)")]
            DiscordAttachment display,
            
            [Parameter("Current-Asset")]
            [Description("If the asset already exists in-game, you can optionally attach it here")]
            DiscordAttachment current = null,
            
            [Parameter("Assets")]
            [Description("If your submission requires multiple files, put them into a .zip and attach it here")]
            DiscordAttachment assets = null,
            
            [Parameter("Notes")]
            [Description("Any additional notes")]
            string notes = null)
        {
            if (ctx.Channel.Id != Cache.Channels.IDs.AssetSubmissions)
            {
                await ctx.RespondAsync($"Please keep all asset submissions to <#{Cache.Channels.IDs.AssetSubmissions}>!", true);
                return;
            }

            // Delete the command because it's ugly
            await ctx.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredChannelMessageWithSource);
            await ctx.DeleteResponseAsync();

            // Sometimes Discord closes this gateway
            try { await ctx.Channel.TriggerTypingAsync(); } catch { }

            // Initialize the message
            var message = new DiscordMessageBuilder();

            // Keep a list of all active streams to easily dispose of them after they're used
            Dictionary<string, FileStream> streams = [];
            List<DiscordEmbedBuilder> embeds = [];

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
            displayEmbed.AddField($"{Cache.Emojis.Votes} Votes", $"{Cache.Emojis.PositiveVotes} **0** -- {Cache.Emojis.ImprovementVotes} **0** -- {Cache.Emojis.NegativeVotes} **0**", true);

            // Attach the display embed to the message
            embeds.Add(displayEmbed);

            // If there is a current asset, and it can be embedded, attach it in a separate embed
            if (current != null && current.MediaType.StartsWith("image"))
            {
                string url = await current.ResetEphemeralAttachmentURL();
                var currentEmbed = new DiscordEmbedBuilder()
                    .WithTitle("Current")
                    .WithImageUrl(url)
                    .WithColor(Cache.Colors.Submitted); ;

                embeds.Add(currentEmbed);
            }

            // Otherwise just attach it
            else if (current != null)
                await FileManager.AttachFileAsync(current, message, streams, "Current");

            // Attach assets, if provided
            if (assets != null)
                await FileManager.AttachFileAsync(assets, message, streams, "Assets");

            // Send the message in art
            message.AddEmbeds(embeds.Select(b => b.Build()));
            var sent = await ctx.Channel.SendMessageAsync(message);
            
            message.ClearEmbeds();
            embeds[0].Footer = new() { Text = sent.Id.ToString() };
            
            // Send in dev
            message.AddEmbeds(embeds.Select(b => b.Build()));
            DiscordMessage dev = await Cache.Channels.AssetVoting.SendMessageAsync(message);
            
            // Add voting buttons
            // The message ID of the public message is cached in the buttons' custom IDs
            await dev.CreateReactionAsync(Cache.Emojis.VoteYesButton);
            await dev.CreateReactionAsync(Cache.Emojis.NeedsImprovementButton);
            await dev.CreateReactionAsync(Cache.Emojis.VoteNoButton);
            await dev.CreateReactionAsync(Cache.Emojis.NeutralButton);

            // Delete files
            foreach (var stream in streams)
            {
                await stream.Value.DisposeAsync();
                File.Delete(stream.Key);
            }
        }
    }
}
