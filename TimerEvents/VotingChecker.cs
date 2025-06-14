using CalamityAssetBot.Commands;
using CalamityAssetBot.Hosting;
using CalamityAssetBot.Utils;
using DSharpPlus.Entities;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace CalamityAssetBot.TimerEvents;

[UsedImplicitly]
public class VotingChecker(ILogger<VotingChecker> logger) : AsyncTimer(logger)
{
    public override TimeSpan Interval  => TimeSpan.FromMinutes(5);
    
    public static readonly TimeSpan VotingPeriod = TimeSpan.FromDays(3);
    
    public override async Task RunAsync()
    {
        DiscordMessage message = await Cache.Channels.DevServer.AssetVoting.GetMessagesAsync(1).FirstAsync();
        ulong? previousId = null;

        while (true)
        {
            // Fetch the next message to check
            if (previousId is not null)
                message = await Cache.Channels.DevServer.AssetVoting.GetMessagesBeforeAsync(previousId.Value, 1).FirstAsync();
            
            // Suggestions legend message, marks the transition to the new system
            // Any messages older than this are un-processable
            if (message.Id == 1382066986430759003uL)
                break;
            
            // Cache the next ID to check
            previousId = message.Id;

            // Only check old enough messages
            if (message.Age() < VotingPeriod)
                continue;

            // Only check the bot's messages
            if (!message.Author!.IsCurrent)
                continue;

            // Only check messages on the new system
            if (!(message.Components?.Any() ?? false) || message.Components[0] is not DiscordContainerComponent container)
                break;

            // Break from the loop if we've hit any message that have already finished voting
            if ((container.Color?.Value ?? 0) != Cache.Colors.Submitted.Value)
                break;

            await DiscordBotService.CommandAccess.WaitAsync();

            // Now we tally the votes
            DiscordSelectComponent assetTypeSelection = ((container.Components[3] as DiscordActionRowComponent)!.Components[0] as DiscordSelectComponent)!;
            AssetType assetType = Enum.Parse<AssetType>(assetTypeSelection.Options.First(o => o.Default).Value);
            (int positiveVotes, int improvementVotes, int negativeVotes) = await Extensions.GetVotes(message, assetType);
            int totalVotes = positiveVotes + improvementVotes + negativeVotes;

            // If 2/3's+ the votes were positive, pass the asset
            if (positiveVotes >= totalVotes * (2f / 3f))
            {
                // Since we will delete the message while passing it, we cannot then search
                // for other messages posted before it.
                // Go ahead and fetch the next message to check while we still have access to the current message.
                var nextMessage = await Cache.Channels.DevServer.AssetVoting.GetMessagesBeforeAsync(previousId.Value, 1).FirstAsync();
                await PassAssetAsync(message);

                previousId = null;
                message = nextMessage;
            }

            // If more than 1/2, use close vote
            else if (positiveVotes >= totalVotes * 0.5f)
                await message.UpdateStatusAsync(Cache.Colors.CloseRejected, "Rejected (Close Vote)");

            // If there were more improvement votes than negative, request changes
            else if (improvementVotes > negativeVotes)
                await message.UpdateStatusAsync(Cache.Colors.NeedsImprovementRejected, "Rejected (Changes Requested)");

            // Otherwise simply reject
            else
                await message.UpdateStatusAsync(Cache.Colors.Rejected, "Rejected");

            DiscordBotService.CommandAccess.Release();
        }
    }

    private static async Task PassAssetAsync(DiscordMessage message)
    {
        // Retrieve message components
        var container = message.Components![0] as DiscordContainerComponent;
        var components = container!.Components.SanitizeFileComponents();

        // Find index of status section
        int index = components.FindIndexOfComponentWithText($"{Cache.Emojis.Legend.Status}");
        var component = components[index];
        
        // Update status text
        var lines = component.GetText().Split('\n').ToList();
        lines[0] = $"{Cache.Emojis.Legend.Status} Status: Accepted";
        
        // Get updated status section
        string newStatus = string.Join('\n', lines);
        
        // Update component collection
        components[index] = component switch
        {
            DiscordSectionComponent section => new DiscordSectionComponent(newStatus, section.Accessory!),
            DiscordTextDisplayComponent => new DiscordTextDisplayComponent(newStatus),
            _ => components[index]
        };

        // Include the container and a new action row for extra buttons
        var builder = new DiscordMessageBuilder().PrepContainer(components, Cache.Colors.Accepted).CopyFiles(message, out var assetStreams);
        builder.AddActionRowComponent(Cache.Buttons.MarkImplemented, Cache.Buttons.MarkUnimplemented);
        
        // Include all files and send the message
        await Cache.Channels.DevServer.CompletedAssets.SendMessageAsync(builder);
        await assetStreams.DisposeAllAsync();

        var publicId = message.GetPublicIdFromDevMessage();
        await message.DeleteAsync();

        if (Cache.Channels.ArtServer.AssetSubmissions.TryGetMessage(publicId, out message!))
            await message.UpdateStatusAsync(Cache.Colors.Accepted, "Accepted");
    }
}