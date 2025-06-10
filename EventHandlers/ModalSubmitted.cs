using CalamityAssetBot.Hosting;
using CalamityAssetBot.Utils;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace CalamityAssetBot.EventHandlers;

public class ModalSubmitted : DiscordEventHandler<ModalSubmittedEventArgs>
{
    public override async Task HandleAsync(ModalSubmittedEventArgs args)
    {
        string[] buttonArgs = args.Id.Split('_');

        // Only process interactions on the new framework
        if (buttonArgs.Length != 2)
            return;
        
        // Get the ID of the dev message from the interaction
        ulong devId = ulong.Parse(buttonArgs[0]);
        
        if (args.Id == Cache.Modals.AddDeveloperNotes(null, devId).CustomId)
        {
            // This weird null coalescence is just to get Rider to shut up
            string? notes = args.Values!?.GetValueOrDefault("notes", null) ?? null;

            if (string.IsNullOrWhiteSpace(notes))
                notes = null;
            
            // Fetch the message and update
            if (Cache.Channels.DevServer.AssetVoting.TryGetMessage(devId, out var message))
                await UpdateNotes(args.Interaction, message, notes);
        }

        if (args.Id == Cache.Modals.AddFeedback(null, devId).CustomId)
        {
            // This weird null coalescence is just to get Rider to shut up
            string? feedback = args.Values!?.GetValueOrDefault("feedback", null) ?? null;

            if (string.IsNullOrWhiteSpace(feedback))
                feedback = null;

            // Fetch the message and update
            if (Cache.Channels.DevServer.AssetVoting.TryGetMessage(devId, out var message))
                await UpdateFeedback(args.Interaction, message, feedback);
        }
    }

    private static async Task UpdateNotes(DiscordInteraction? interaction, DiscordMessage message, string? notes)
    {
        if (interaction is not null)
            await interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
        
        // Retrieve message components
        var container = message.Components![0] as DiscordContainerComponent;
        var components = container!.Components.SanitizeFileComponents();

        // Find index of status section
        int index = components.FindIndexOfComponentWithText($"{Cache.Emojis.Legend.Status}");
        var component = components[index];
        
        // Update text to match notes submission
        var lines = component.GetText().Split('\n').ToList();

        // Remove current notes line if it exists
        lines = [lines[0]];
            
        // Add a new notes line if notes were included
        if (notes is not null)
            lines.Add($"{Cache.Emojis.Legend.DevNotes} Dev Notes: {notes}");
        
        // Get updated status section
        string status = string.Join("\n", lines);

        // Update component collection
        components[index] = component switch
        {
            DiscordSectionComponent section => new DiscordSectionComponent(status, section.Accessory!),
            DiscordTextDisplayComponent => new DiscordTextDisplayComponent(status),
            _ => components[index]
        };
        
        // Update the message
        var builder = new DiscordMessageBuilder().PrepContainer(components, container.Color).CopyFiles(message, out var assetStreams);
        
        // Include buttons if this asset has extra ones
        if (message.Components.Count > 1)
            builder.AddActionRowComponent((message.Components[1] as DiscordActionRowComponent)!);
        
        await message.ModifyAsync(builder);
        await assetStreams.DisposeAllAsync();

        // Return if this is being called on the public message
        // We just called this recursively from the message in dev if that was the case
        if (message.Channel! == Cache.Channels.ArtServer.AssetSubmissions)
            return;

        ulong publicId = message.GetPublicIdFromDevMessage();
        
        if (Cache.Channels.ArtServer.AssetSubmissions.TryGetMessage(publicId, out message!))
            await UpdateNotes(null, message, notes);
    }

    private static async Task UpdateFeedback(DiscordInteraction? interaction, DiscordMessage message, string? feedback)
    {
        if (interaction is not null)
            await interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
        
        // Retrieve message components
        var container = message.Components![0] as DiscordContainerComponent;
        var components = container!.Components.SanitizeFileComponents();

        // Find index of feedback section (-1 if it does not currently exist)
        int index = components.FindIndexOfComponentWithText($"{Cache.Emojis.Legend.ArtistFeedback}");
        
        // Remove current feedback if it exists
        if (index != -1)
        {
            // Remove the component AND the separator that immediately follows it
            components.RemoveAt(index);
            components.RemoveAt(index);
        }

        // Otherwise assign the index for re-insertion
        else
            index = components.FindIndexOfComponentWithText($"{Cache.Emojis.Legend.Status}") + 2;

        // If feedback was included, insert it into a new section
        if (feedback is not null)
        {
            components.Insert(index, new DiscordSeparatorComponent(true));
            components.Insert(index, new DiscordTextDisplayComponent(
                $"### {Cache.Emojis.Legend.ArtistFeedback} Art Team Feedback\n" +
                $"{feedback}"));
        }
        
        // Update message with new feedback
        var builder = new DiscordMessageBuilder().PrepContainer(components, container.Color).CopyFiles(message, out var assetStreams);
        
        // Include buttons if this asset has extra ones
        if (message.Components.Count > 1)
            builder.AddActionRowComponent((message.Components[1] as DiscordActionRowComponent)!);
        
        await message.ModifyAsync(builder, attachments: message.Attachments);
        await assetStreams.DisposeAllAsync();

        if (message.Channel! == Cache.Channels.ArtServer.AssetSubmissions)
            return;

        ulong publicId = message.GetPublicIdFromDevMessage();
        
        if (Cache.Channels.ArtServer.AssetSubmissions.TryGetMessage(publicId, out message!))
            await UpdateFeedback(null, message, feedback);
    }
}