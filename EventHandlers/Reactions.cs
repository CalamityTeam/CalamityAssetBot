using CalamityAssetBot.Commands;
using CalamityAssetBot.Hosting;
using CalamityAssetBot.Utils;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace CalamityAssetBot.EventHandlers
{
    public class ReactionAdded : DiscordEventHandler<MessageReactionAddedEventArgs>
    {
        public override Task HandleAsync(MessageReactionAddedEventArgs args) => Reactions.HandleAsync(args.User, args.Channel, args.Message, args.Emoji);
    }

    public class ReactionRemoved : DiscordEventHandler<MessageReactionRemovedEventArgs>
    {
        public override Task HandleAsync(MessageReactionRemovedEventArgs args) => Reactions.HandleAsync(args.User, args.Channel, args.Message, args.Emoji);
    }

    public static class Reactions
    {
        public static async Task HandleAsync(DiscordUser user, DiscordChannel channel, DiscordMessage message, DiscordEmoji emoji)
        {
            // Ignore our own reactions
            if (user.IsCurrent)
                return;

            // Only process reactions in asset voting
            if (channel != Cache.Channels.DevServer.AssetVoting)
                return;

            // ALWAYS re-fetch the message to ensure data is up to date
            if (!message.TryRefresh(out message!))
                return;

            // Only process reaction on our messages
            if (!message.Author!.IsCurrent)
                return;

            // Only process voting reactions
            DiscordEmoji[] voteReactions = [Cache.Emojis.Votes.Yes, Cache.Emojis.Votes.No, Cache.Emojis.Votes.NeedsImprovement];
            if (!voteReactions.Contains(emoji))
                return;

            // Don't do anything for old voting style submissions
            if (message.Components!.Count == 0 || message.Components[0] is not DiscordContainerComponent container || (container.Color?.Value ?? 0) != Cache.Colors.Submitted.Value)
                return;

            // Retrieve the asset type from the message
            // This will always be the 4th (index 3) component in messages in the dev server
            DiscordSelectComponent assetTypeSelection = ((container.Components[3] as DiscordActionRowComponent)!.Components[0] as DiscordSelectComponent)!;
            AssetType assetType = Enum.Parse<AssetType>(assetTypeSelection.Options.First(o => o.Default).Value);
            (int positiveVotes, int improvementVotes, int negativeVotes) = await Extensions.GetVotes(message, assetType);
            
            // Assemble the voting string
            string voteString = Extensions.VoteString(positiveVotes, improvementVotes, negativeVotes);
            
            // Update the vote counts
            await UpdateVoteCounts(message, voteString);
        }
        
        public static async Task UpdateVoteCounts(DiscordMessage message, string voteString)
        {
            // Retrieve message components
            var container = (message.Components![0] as DiscordContainerComponent)!;
            var components = container.Components.SanitizeFileComponents();
            
            // Find index of voting section
            int index = components.FindIndexOfComponentWithText($"{Cache.Emojis.Legend.Voting}");
            var component = components[index];

            // Update voting section
            var lines = component.GetText().Split('\n').ToList();
            lines[1] = voteString;
            components[index] = component switch
            {
                DiscordSectionComponent section => new DiscordSectionComponent(string.Join('\n', lines), section.Accessory!),
                DiscordTextDisplayComponent => new DiscordTextDisplayComponent(string.Join('\n', lines)),
                _ => components[index]
            };

            // Update message with new votes
            DiscordMessageBuilder builder = new DiscordMessageBuilder().PrepContainer(components, container.Color).CopyFiles(message, out var assetStreams);
            await message.ModifyAsync(builder);
            await assetStreams.DisposeAllAsync();

            // Return if this is being called on the public message
            // We just called this recursively from the message in dev if that was the case
            if (message.Channel! == Cache.Channels.ArtServer.AssetSubmissions)
                return;

            ulong publicId = message.GetPublicIdFromDevMessage();
            
            if (Cache.Channels.ArtServer.AssetSubmissions.TryGetMessage(publicId, out message!))
                await UpdateVoteCounts(message, voteString);
        }
    }
}
