using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace ArtSubmissionsBot.EventProcessing
{
    internal class MessageDeleted
    {
        internal static async Task Process(MessageDeletedEventArgs args)
        {
            // Don't handle DMs
            if (args.Channel is null || args.Channel is DiscordDmChannel)
                return;

            // If the message was in dev's asset voting and posted by the bot
            if (args.Channel.Id == Cache.Channels.IDs.AssetVoting && args.Message.Author.IsCurrent)
            {
                await RemoveSubmission(args.Message);
                return;
            }

            // If the message was in art public and was posted by the bot
            if (args.Channel.Id == Cache.Channels.IDs.AssetSubmissions && args.Message.Author.IsCurrent)
            {
                // Iterate through the 100 most recent submissions in dev to see if any are the corresponding dev voting message
                await foreach (var message in Cache.Channels.AssetVoting.GetMessagesAsync(100))
                {
                    // If there are no message components, that means either the vote has concluded, or
                    // it was posted by another dev and not the bot
                    if (message.Components.Count <= 0)
                        continue;

                    // Parse the art server ID to check
                    ulong publicID = ulong.Parse(message.Components.First().CustomId.Replace($"vote_yes_", ""));
                    if (publicID == args.Message.Id)
                    {
                        await RemoveSubmission(message);
                        return;
                    }
                }
            }
        }

        public static async Task RemoveSubmission(DiscordMessage devMessage)
        {
            // Fetch the public message
            DiscordMessage? publicMessage = null;
            ulong publicID = ulong.Parse(devMessage.Components.First().CustomId.Replace($"vote_yes_", ""));

            try { publicMessage = await Cache.Channels.AssetSubmissions.GetMessageAsync(publicID); }
            catch { }

            // The public message may have been the one that was deleted
            // If so, don't attempt to delete it
            if (publicMessage is not null)
                await publicMessage.DeleteAsync();

            if (Cache.VoteCache.ContainsKey(devMessage.Id))
                Cache.VoteCache.Remove(devMessage.Id);

            // If the dev message has not yet been deleted, delete it now
            // Re-fetching the message ensures we don't attempt to delete a message that doesn't exist
            try
            {
                devMessage = await Cache.Channels.AssetVoting.GetMessageAsync(devMessage.Id);
                await devMessage.DeleteAsync();
            }

            catch { }
        }
    }
}
