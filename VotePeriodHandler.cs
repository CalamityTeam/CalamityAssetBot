using DSharpPlus.Entities;

namespace ArtSubmissionsBot
{
    internal static class VotePeriodHandler
    {
        internal static System.Timers.Timer VoteTallyTimer = new(TimeSpan.FromMinutes(10)) { AutoReset = true };
        internal static TimeSpan Age(this DiscordMessage message) => DateTime.UtcNow - message.Timestamp.UtcDateTime;
        internal static readonly TimeSpan VotingPeriod = TimeSpan.FromDays(3);

        internal static async Task TallyVotesAsync()
        {
            // Cache the latest message
            DiscordMessage message = await Cache.Channels.AssetVoting.GetMessagesAsync(1).FirstAsync();

            // Ladder through all messages that are younger than voting period length
            while (message.Age() < VotingPeriod ||
                !message.Author.IsCurrent ||
                (message.Embeds[0].Color.Value.Value != Cache.Colors.Rejected.Value && 
                message.Embeds[0].Color.Value.Value != Cache.Colors.CloseRejected.Value))
            {
                // Once old enough messages are reached...
                if (message.Age() > VotingPeriod && message.Author.IsCurrent)
                {
                    // Tally votes
                    int positiveVotes = await message.GetReactionsAsync(Cache.Emojis.PositiveVotes).Where(x => !x.IsCurrent).CountAsync();
                    int improvementVotes = await message.GetReactionsAsync(Cache.Emojis.ImprovementVotes).Where(x => !x.IsCurrent).CountAsync();
                    int negativeVotes = await message.GetReactionsAsync(Cache.Emojis.NegativeVotes).Where(x => !x.IsCurrent).CountAsync();
                    int totalVotes = positiveVotes + improvementVotes + negativeVotes;

                    // Forward the message where it needs to go
                    if (positiveVotes >= totalVotes * (2f / 3f))
                        message = await PassAssetAsync(message);
                    else if (positiveVotes >= totalVotes * 0.5f)
                        message = await CloseRejectAssetAsync(message);
                    else
                        message = await RejectAssetAsync(message);
                    
                    continue;
                }

                // Ladder to the next message
                message = await Cache.Channels.AssetVoting.GetMessagesBeforeAsync(message.Id).FirstAsync();

                // Arbitrary non-bot message to stop the ladder
                if (message.Id == 1061791436971982908uL)
                    break;
            }
        }

        /// <returns>The new message to evaluate</returns>
        internal static async Task<DiscordMessage> PassAssetAsync(DiscordMessage message) =>
            await ForwardAssetAsync(message, Cache.Channels.CompletedAssets, Cache.Colors.Accepted);

        /// <returns>The new message to evaluate</returns>
        internal static async Task<DiscordMessage> CloseRejectAssetAsync(DiscordMessage message) =>
            await UpdateAssetAsync(message, Cache.Colors.CloseRejected);

        /// <returns>The new message to evaluate</returns>
        internal static async Task<DiscordMessage> RejectAssetAsync(DiscordMessage message) =>
            await UpdateAssetAsync(message, Cache.Colors.Rejected);

        private static async Task<DiscordMessage> ForwardAssetAsync(DiscordMessage message, DiscordChannel channel, DiscordColor color)
        {
            // Reformat as a builder
            DiscordMessageBuilder builder = new();

            // Copy over all the embeds, changing their colors and statuses along the way
            List<DiscordEmbed> embeds = message.Embeds.ToList();
            foreach (DiscordEmbed em in embeds)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder(em).WithColor(color);
                List<DiscordEmbedField> fields = embed.Fields.ToList();
                embed.ClearFields();
                foreach (var field in fields)
                {
                    if (field.Name.StartsWith($"{Cache.Emojis.Status}"))
                        embed.AddField(field.Name, "Accepted", field.Inline);
                    else
                        embed.AddField(field.Name, field.Value, field.Inline);
                }

                builder.AddEmbed(embed);
            }

            // Update the public message
            ulong publicID = ulong.Parse(message.Embeds[0].Footer.Text);
            DiscordMessage publicMessage = await Cache.Channels.AssetSubmissions.GetMessageAsync(publicID);
            await publicMessage.ModifyAsync(builder);

            // The public message is re-cached to prevent attachments getting messed up
            publicMessage = await Cache.Channels.AssetSubmissions.GetMessageAsync(publicID);

            // Copy all attachments
            Dictionary<string, FileStream> files = new();
            
            foreach (var attachment in publicMessage.Attachments)
                await FileManager.AttachFileAsync(attachment, builder, files);

            // Add buttons for updating status
            builder.AddActionRowComponent(new[]
            {
                Cache.Buttons.MarkUnimplemented(publicID),
                Cache.Buttons.MarkImplmented(publicID)
            });

            // Send the new message, cache the next message to evaluate, and delete the old message
            await channel.SendMessageAsync(builder);
            DiscordMessage newMessage = await Cache.Channels.AssetVoting.GetMessagesBeforeAsync(message.Id).FirstAsync();
            await message.DeleteAsync();

            // Delete all files
            foreach (var file in files)
            {
                await file.Value.DisposeAsync();
                File.Delete(file.Key);
            }
             
            // Return new evaluation message
            return newMessage;
        }

        private static async Task<DiscordMessage> UpdateAssetAsync(DiscordMessage message, DiscordColor color)
        {
            ulong publicID = ulong.Parse(message.Embeds[0].Footer.Text);

            // Updating the embed is cancer
            // Cache the message and reformat it into a builder
            DiscordMessageBuilder builder = new(message);

            // Copy over all the embeds, changing their colors and statuses along the way
            List<DiscordEmbed> embeds = message.Embeds.ToList();
            foreach (DiscordEmbed em in embeds)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder(em).WithColor(color);
                List<DiscordEmbedField> fields = embed.Fields.ToList();
                embed.ClearFields();
                foreach (var field in fields)
                {
                    if (field.Name.StartsWith($"{Cache.Emojis.Status}"))
                        embed.AddField(field.Name, color.Value == Cache.Colors.CloseRejected.Value ? "Rejected (Close Vote)" : "Rejected", field.Inline);
                    else
                        embed.AddField(field.Name, field.Value, field.Inline);
                }
                
                if (embeds.IndexOf(em) == 0)
                    builder.SetEmbed(embed);
                else
                    builder.AddEmbed(embed);
            }

            // Remove voting buttons
            builder.ClearComponents();

            // Update public message
            await message.ModifyAsync(builder);

            DiscordMessage publicMessage = await Cache.Channels.AssetSubmissions.GetMessageAsync(publicID);
            await publicMessage.ModifyAsync(builder);

            return await Cache.Channels.AssetVoting.GetMessagesBeforeAsync(message.Id).FirstAsync();
        }
    }
}
