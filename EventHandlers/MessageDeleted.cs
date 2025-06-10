using CalamityAssetBot.Hosting;
using CalamityAssetBot.TimerEvents;
using CalamityAssetBot.Utils;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace CalamityAssetBot.EventHandlers;

public class MessageDeleted : DiscordEventHandler<MessageDeletedEventArgs>
{
    public override async Task HandleAsync(MessageDeletedEventArgs args)
    {
        if (!(args.Message.Author?.IsCurrent ?? false))
            return;

        if (!args.Message.Components!.Any() || args.Message.Components![0] is not DiscordContainerComponent)
            return;

        if (args.Channel == Cache.Channels.ArtServer.AssetSubmissions)
        {
            await foreach (var message in Cache.Channels.DevServer.AssetVoting.GetMessagesAsync(100))
            {
                if (!message.Author!.IsCurrent || !(message.Components?.Any() ?? false) || message.Components[0] is not DiscordSectionComponent)
                    continue;

                var publicId = message.GetPublicIdFromDevMessage();
                if (publicId != args.Message.Id)
                    continue;

                await RemoveSubmission(message);
                return;
            }
        }

        // The age check is because we delete messages in dev if they get passed and forward them to another channel
        if (args.Channel == Cache.Channels.DevServer.AssetVoting && args.Message.Age() < VotingChecker.VotingPeriod)
        {
            await RemoveSubmission(args.Message);
        }
    }

    private static async Task RemoveSubmission(DiscordMessage message)
    {
        DiscordMessage? publicMessage = null;
        ulong publicId = message.GetPublicIdFromDevMessage();
        
        try { publicMessage = await Cache.Channels.ArtServer.AssetSubmissions.GetMessageAsync(publicId, true); }
        catch { /* ignored */ }

        if (publicMessage is not null)
            await publicMessage.DeleteAsync();
        
        try { await message.DeleteAsync(); }
        catch { /* ignored */ }
    }
}