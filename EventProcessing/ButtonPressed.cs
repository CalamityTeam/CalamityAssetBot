using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace ArtSubmissionsBot.EventProcessing
{
    internal static class ButtonPressed
    {
        internal static async Task Process(ComponentInteractionCreatedEventArgs ctx)
        { 
            if (ctx.Id.StartsWith("mark_"))
                await UpdateImplemented(ctx);
        }

        private static async Task UpdateImplemented(ComponentInteractionCreatedEventArgs ctx)
        {
            // If the user reacts with the button that wouldn't change the status of the embed, ignore it
            await ctx.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
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
                    builder.SetEmbed(embed);

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
