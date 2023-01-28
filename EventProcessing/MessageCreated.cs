using DSharpPlus.EventArgs;
namespace ArtSubmissionsBot.EventProcessing
{
    internal static class MessageCreated
    {
        internal static async Task Process(MessageCreateEventArgs args)
        {
            // Delete any messages sent in the asset submission channels that aren't command executions
            if (!args.Message.Author.IsCurrent && args.Channel.Id == Cache.Channels.AssetSubmissions.Id)
            {
                await args.Message.DeleteAsync();
            }
        }
    }
}
