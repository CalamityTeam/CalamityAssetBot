using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System.Linq;

namespace ArtSubmissionsBot.EventProcessing;

public class ReactionAdded
{
    internal static async Task Process(MessageReactionAddedEventArgs ctx)
    {
        if (ctx.Message.ChannelId != Cache.Channels.IDs.AssetVoting || !ctx.Message.Author.IsCurrent || !ctx.Message.Embeds.Any() || ctx.Message.Embeds[0].Footer is null)
            return;

        // Parse the public message ID from the button ID
        // And retrieve the votes
        ulong publicID = ulong.Parse(ctx.Message.Embeds[0].Footer.Text);

        // Updating the embed is cancer
        // Cache the message and reformat it into a builder
        DiscordMessage message;

        try
        {
            message = await Cache.Channels.AssetSubmissions.GetMessageAsync(publicID);
        }
        catch
        {
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

        int positiveVotes = await ctx.Message.GetReactionsAsync(Cache.Emojis.PositiveVotes).Where(x => !x.IsCurrent).CountAsync();
        int improvementVotes = await ctx.Message.GetReactionsAsync(Cache.Emojis.ImprovementVotes).Where(x => !x.IsCurrent).CountAsync();
        int negativeVotes = await ctx.Message.GetReactionsAsync(Cache.Emojis.NegativeVotes).Where(x => !x.IsCurrent).CountAsync();
        
        foreach (var field in fields)
        {
            if (!field.Name.StartsWith(Cache.Emojis.Votes.ToString()))
                embed.AddField(field.Name, field.Value, field.Inline);

            else
                embed.AddField(field.Name,
                    $"{Cache.Emojis.PositiveVotes} **{positiveVotes}** -- {Cache.Emojis.ImprovementVotes} **{improvementVotes}** -- {Cache.Emojis.NegativeVotes} **{negativeVotes}**",
                    field.Inline);
        }

        // Reset the message's embeds and re-add them
        builder.SetEmbed(embed);

        foreach (var em in embeds)
            builder.AddEmbed(em);

        // Update public message
        await message.ModifyAsync(builder);
        await ctx.Message.ModifyAsync(builder);

        if (message.Age() > VotePeriodHandler.VotingPeriod && message.Author.IsCurrent)
        {
            // Tally votes
            int totalVotes = positiveVotes + improvementVotes + negativeVotes;

            // Forward the message where it needs to go
            if (positiveVotes >= totalVotes * (2f / 3f))
                message = await VotePeriodHandler.PassAssetAsync(message);
            else if (positiveVotes >= totalVotes * 0.5f)
                message = await VotePeriodHandler.CloseRejectAssetAsync(message);
            else
                message = await VotePeriodHandler.RejectAssetAsync(message);
        }
    }
}