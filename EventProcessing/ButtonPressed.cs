using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace ArtSubmissionsBot.EventProcessing
{
    internal static class ButtonPressed
    {
        internal static async Task Process(ComponentInteractionCreateEventArgs ctx)
        {
            // Check whether the vote was positive or negative
            bool positiveVote;
            if (ctx.Id.StartsWith("vote_yes"))
                positiveVote = true;

            else if (ctx.Id.StartsWith("vote_no"))
                positiveVote = false;

            // Also check if it's marking a completed asset
            else if (ctx.Id.StartsWith("mark_"))
            {
                await UpdateImplemented(ctx);
                return;
            }

            // If the interaction came from somewhere else, ignore it
            else
                return;

            // Parse the public message ID from the button ID
            // And retrieve the votes
            ulong publicID = ulong.Parse(ctx.Id.Replace($"vote_{(positiveVote ? "yes" : "no")}_", ""));

            if (!Cache.VoteCache.ContainsKey(ctx.Message.Id))
                Cache.VoteCache.Add(ctx.Message.Id, new());

            Dictionary<ulong, bool> votes = Cache.VoteCache[ctx.Message.Id];

            // If the user has yet to vote, add it
            if (!votes.ContainsKey(ctx.User.Id))
                votes.Add(ctx.User.Id, positiveVote);

            // Otherwise...
            else
            {
                // If they vote the same thing again, remove their vote
                if (votes[ctx.User.Id] == positiveVote)
                    votes.Remove(ctx.User.Id);

                // If they don't, change it
                else
                    votes[ctx.User.Id] = positiveVote;
            }

            // Save the cache
            FileManager.SaveVoteCache();

            // Updating the embed is cancer
            // Cache the message and reformat it into a builder
            DiscordMessage message;

            try { message = await Cache.Channels.AssetSubmissions.GetMessageAsync(publicID); }
            catch
            {
                await ctx.Interaction.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent(
                        "Error encountered when attempting to access the public submission message.\n" +
                        "Ping Nycro if the cause of this error is not readily apparent."
                    )
                    .AsEphemeral(true)
                );

                return;
            }

            DiscordMessageBuilder builder = new(message);

            // Cache the embeds, and specifically set aside the first one
            List<DiscordEmbed> embeds = builder.Embeds.ToList();
            DiscordEmbedBuilder embed = new(embeds[0]);
            embeds.RemoveAt(0);

            // Update the main embed with the new vote counts
            List<DiscordEmbedField> fields = embed.Fields.ToList();
            embed.ClearFields();
            foreach (var field in fields)
            {
                if (!field.Name.StartsWith(Cache.Emojis.Votes.ToString()))
                    embed.AddField(field.Name, field.Value, field.Inline);

                else
                    embed.AddField(field.Name, $"{Cache.Emojis.VoteYes} {votes.Count(x => x.Value)} - {votes.Count(x => !x.Value)} {Cache.Emojis.VoteNo}", field.Inline);
            }

            // Reset the message's embeds and re-add them
            builder.Embed = embed;
            foreach (var em in embeds)
                builder.AddEmbed(em);

            // Update public message
            await message.ModifyAsync(builder);

            // Re-add buttons
            builder.AddComponents(new DiscordComponent[]
            {
                Cache.Buttons.VoteYes(publicID),
                Cache.Buttons.VoteNo(publicID)
            });

            // Respond to the interaction
            await ctx.Interaction.CreateResponseAsync(DSharpPlus.InteractionResponseType.UpdateMessage, new(builder));

            if (message.Age() > VotePeriodHandler.VotingPeriod && message.Author.IsCurrent)
            {
                // Tally votes
                List<bool> tally = Cache.VoteCache[message.Id].Values.ToList();
                int yesCount = tally.Count(x => x);
                int total = tally.Count;

                // Forward the message where it needs to go
                if (yesCount >= total * (2f / 3f))
                    message = await VotePeriodHandler.PassAssetAsync(message);
                else if (yesCount >= total * 0.5f)
                    message = await VotePeriodHandler.CloseRejectAssetAsync(message);
                else
                    message = await VotePeriodHandler.RejectAssetAsync(message);

                Cache.VoteCache.Remove(message.Id);
            }
        }

        private static async Task UpdateImplemented(ComponentInteractionCreateEventArgs ctx)
        {
            // If the user reacts with the button that wouldn't change the status of the embed, ignore it
            await ctx.Interaction.CreateResponseAsync(DSharpPlus.InteractionResponseType.DeferredMessageUpdate);
            bool implemented = ctx.Id.StartsWith("mark_i");
            if ((implemented && ctx.Message.Embeds[0].Color.Value.Value == Cache.Colors.Implemented.Value) ||
                (!implemented && ctx.Message.Embeds[0].Color.Value.Value == Cache.Colors.Accepted.Value))
            {
                return;
            }

            // Parse the public message ID
            ulong publicID = ulong.Parse(ctx.Id.Replace($"mark_{(implemented ? "" : "un")}implemented_", ""));

            // Updating the embed is cancer
            // Cache the message and reformat it into a builder
            DiscordMessageBuilder builder = new(ctx.Message);

            // Copy over all the embeds, changing their colors and statuses along the way
            List<DiscordEmbed> embeds = ctx.Message.Embeds.ToList();
            DiscordColor color = implemented ? Cache.Colors.Implemented : Cache.Colors.Accepted;
            foreach (DiscordEmbed em in embeds)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder(em).WithColor(color);
                List<DiscordEmbedField> fields = embed.Fields.ToList();
                embed.ClearFields();
                foreach (var field in fields)
                {
                    if (field.Name.StartsWith($"{Cache.Emojis.Status}"))
                        embed.AddField(field.Name, implemented ? "Implemented" : "Accepted", field.Inline);
                    else
                        embed.AddField(field.Name, field.Value, field.Inline);
                }

                if (embeds.IndexOf(em) == 0)
                    builder.Embed = embed;
                else
                    builder.AddEmbed(embed);
            }

            // Update message
            await ctx.Message.ModifyAsync(builder);

            // Clear components and also update public message
            builder.ClearComponents();
            DiscordMessage assetMessage = await Cache.Channels.AssetSubmissions.GetMessageAsync(publicID);
            await assetMessage.ModifyAsync(builder);
        }
    }
}
