using CalamityAssetBot.Commands;
using CalamityAssetBot.Hosting;
using CalamityAssetBot.Utils;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace CalamityAssetBot.EventHandlers;

public class InteractionCreated : DiscordEventHandler<ComponentInteractionCreatedEventArgs>
{
    public override async Task HandleAsync(ComponentInteractionCreatedEventArgs args)
    {
        // ALWAYS re-fetch the message to ensure data is up to date
        if (!args.Message.TryRefresh(out var message))
            return;

        // Only process our own interactions
        if (!message.Author!.IsCurrent)
            return;

        // Only process interactions for submissions on the new system
        if (!(message.Components?.Any() ?? false) || message.Components![0] is not DiscordContainerComponent)
            return;
        
        // Get the container components
        var components = (message.Components[0] as DiscordContainerComponent)!.Components;

        if (args.Id == Cache.Buttons.AddDeveloperNotesPrompt.CustomId)
        {
            // Attempt to get the dev notes component, return null if not found
            DiscordComponent? notesComponent = components.FirstComponentWithText($"{Cache.Emojis.Legend.DevNotes}");

            // Attempt to get the dev notes, return null if not found
            string? devNotes = notesComponent?.GetText().Split('\n')[1].Replace($"{Cache.Emojis.Legend.DevNotes} Dev Notes: ", "") ?? null;

            if (string.IsNullOrWhiteSpace(devNotes))
                devNotes = null;

            // Send the modal
            await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, Cache.Modals.AddDeveloperNotes(devNotes, message.Id));
        }

        else if (args.Id == Cache.Buttons.AddFeedbackPrompt.CustomId)
        {
            // Attempt to get the status component, return null if not found
            // The status component contains dev acceptance notes
            DiscordComponent? feedbackComponent = components.FirstComponentWithText($"{Cache.Emojis.Legend.ArtistFeedback}");

            // Attempt to get the feedback, return null if not found
            string? feedback = feedbackComponent?.GetText().Split('\n')[1] ?? null;

            // Send the modal
            await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, Cache.Modals.AddFeedback(feedback, message.Id));
        }

        // Update "implementations status" for accepted assets
        else if (args.Id == Cache.Buttons.MarkImplemented.CustomId)
            await message.UpdateStatusAsync(Cache.Colors.Implemented, "Implemented");

        else if (args.Id == Cache.Buttons.MarkUnimplemented.CustomId)
            await message.UpdateStatusAsync(Cache.Colors.Accepted, "Accepted");

        else if (args.Id == Cache.Buttons.AssetTypeSelection(AssetType.Sprite).CustomId)
            await UpdateAssetType(args.Interaction, message, args.Interaction.Data.Values[0]!);
    }

    private static async Task UpdateAssetType(DiscordInteraction interaction, DiscordMessage message, string assetTypeValue)
    {
        await interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
        
        // Retrieve components
        var container = (message.Components![0] as DiscordContainerComponent)!;
        var components = container.Components.SanitizeFileComponents();

        // Update asset type
        AssetType assetType = Enum.Parse<AssetType>(assetTypeValue);
        components[3] = new DiscordActionRowComponent([Cache.Buttons.AssetTypeSelection(assetType)]);

        // Update vote counts
        // Find index of voting section
        int index = components.FindIndexOfComponentWithText($"{Cache.Emojis.Legend.Voting}");
        var component = components[index];

        (int positiveVotes, int improvementVotes, int negativeVotes) = await Extensions.GetVotes(message, assetType);
        string voteString = Extensions.VoteString(positiveVotes, improvementVotes, negativeVotes);

        // Update voting section
        var lines = component.GetText().Split('\n').ToList();
        lines[1] = voteString;
        components[index] = component switch
        {
            DiscordSectionComponent section => new DiscordSectionComponent(string.Join('\n', lines), section.Accessory!),
            DiscordTextDisplayComponent => new DiscordTextDisplayComponent(string.Join('\n', lines)),
            _ => components[index]
        };

        // Update dev
        var builder = new DiscordMessageBuilder().PrepContainer(components, container.Color).CopyFiles(message, out var assetStreams);
        await message.ModifyAsync(builder, false, message.Attachments);
        await assetStreams.DisposeAllAsync();

        // Update votes in public
        if (Cache.Channels.ArtServer.AssetSubmissions.TryGetMessage(message.GetPublicIdFromDevMessage(), out message!))
            await Reactions.UpdateVoteCounts(message, voteString);
    }
}