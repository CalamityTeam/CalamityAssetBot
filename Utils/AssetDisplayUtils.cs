using DSharpPlus.Entities;
using CalamityAssetBot.Commands;

namespace CalamityAssetBot.Utils
{
    public static partial class Extensions
    {
        public static string Display(this AssetType type) => Enum.GetName(type)!;

        public static void PrepareForDev(this DiscordMessageBuilder builder, List<DiscordComponent> components, DiscordMessage publicMessage)
        {
            // Re-usable method to transform a text display component into a "section" component
            void TransformTextToSection(string textIdentifier, DiscordComponent button)
            {
                // Index of the component
                int index = components.FindIndex(c => c is DiscordTextDisplayComponent text && text.Content.Contains(textIdentifier));

                // Remove the current voting display
                DiscordTextDisplayComponent displayComponent = (components[index] as DiscordTextDisplayComponent)!;
                components.RemoveAt(index);

                // Re-add it as a "section" with a button prompt
                components.Insert(index, new DiscordSectionComponent(
                    displayComponent,
                    button));
            }

            // Attach the "dev notes" button to the voting area
            TransformTextToSection($"{Cache.Emojis.Legend.Voting}", Cache.Buttons.AddDeveloperNotesPrompt);

            // Attach the "dev feedback" button to the status area
            TransformTextToSection($"{Cache.Emojis.Legend.Status}", Cache.Buttons.AddFeedbackPrompt);

            // Add a "quick jump" link to the message in the art server
            TransformTextToSection($"-# Submitted", Cache.Buttons.ArtServerQuickLink(publicMessage));

            // Reset the components so we can modify before sending to dev
            builder.ClearComponents();

            // Prepare the message for dev
            builder.AddContainerComponent(new(components, false, Cache.Colors.Submitted));
        }

        public static async Task<(int positiveVotes, int improvementVotes, int negativeVotes)> GetVotes(DiscordMessage message, AssetType assetType)
        {
            // Calculates the voting weight for a given user based on the asset type
            // Spriter votes count for 2 on sprite submissions
            async ValueTask<int> CalculateVoteWeight(DiscordUser user)
            {
                if (assetType != AssetType.Sprite)
                    return 1;

                var member = user as DiscordMember;

                if (member is null)
                {
                    try { member = await Cache.Servers.DevServer.GetMemberAsync(user.Id, true); }
                    catch { }
                }

                return (member?.Roles.Contains(Cache.Roles.Spriter) ?? false) ? 2 : 1;
            }

            // Calculates the total voting score for a given emoji, factoring user vote weights
            ValueTask<int> CalculateVotes(DiscordEmoji emoji) => message.GetReactionsAsync(emoji).Where(x => !x.IsCurrent).SumAwaitAsync(CalculateVoteWeight);

            int positive = await CalculateVotes(Cache.Emojis.Votes.Yes);
            int improvement = await CalculateVotes(Cache.Emojis.Votes.NeedsImprovement);
            int negative = await CalculateVotes(Cache.Emojis.Votes.No);

            return (positive, improvement, negative);
        }

        public static async Task UpdateStatusAsync(this DiscordMessage message, DiscordColor color, string status)
        {
            // Retrieve message components
            var container = message.Components![0] as DiscordContainerComponent;
            var components = container!.Components.SanitizeFileComponents();

            // Find index of status section
            int index = components.FindIndexOfComponentWithText($"{Cache.Emojis.Legend.Status}");
            var component = components[index];
        
            // Update status text
            var lines = component.GetText().Split('\n').ToList();
            lines[0] = $"{Cache.Emojis.Legend.Status} Status: {status}";
        
            // Get updated status section
            string newStatus = string.Join('\n', lines);
        
            // Update component collection
            components[index] = component switch
            {
                DiscordSectionComponent section => new DiscordSectionComponent(newStatus, section.Accessory!),
                DiscordTextDisplayComponent => new DiscordTextDisplayComponent(newStatus),
                _ => components[index]
            };

            // Update message with new color/status
            var builder = new DiscordMessageBuilder().EnableV2Components().AddContainerComponent(new(components, false, color)).CopyFiles(message, out var assetStreams);;

            // Include buttons if this asset has extra ones
            if (message.Components.Count > 1)
                builder.AddActionRowComponent((message.Components[1] as DiscordActionRowComponent)!);
            
            await message.ModifyAsync(builder, attachments: message.Attachments);
            await assetStreams.DisposeAllAsync();

            // Return if this is being called on the public message
            // We just called this recursively from the message in dev if that was the case
            if (message.Channel! == Cache.Channels.ArtServer.AssetSubmissions)
                return;

            ulong publicId = message.GetPublicIdFromDevMessage();

            if (Cache.Channels.ArtServer.AssetSubmissions.TryGetMessage(publicId, out message!))
                await UpdateStatusAsync(message, color, status);
        }

        public static string VoteString(int positiveVotes, int improvementVotes, int negativeVotes) =>
            $"{Cache.Emojis.Legend.PositiveVotes} **{positiveVotes}** -- {Cache.Emojis.Legend.ImprovementVotes} **{improvementVotes}** -- {Cache.Emojis.Legend.NegativeVotes} **{negativeVotes}**";

        public static int FindIndexOfComponentWithText(this List<DiscordComponent> components, string searchText)
        {
            return components.FindIndex(c =>
                (c is DiscordSectionComponent section && section.Components[0] is DiscordTextDisplayComponent sectionText && sectionText.Content.Contains(searchText))
                ||
                (c is DiscordTextDisplayComponent text && text.Content.Contains(searchText)));
        }
        
        public static DiscordComponent? FirstComponentWithText(this IEnumerable<DiscordComponent> components, string searchText)
        {
            return components.FirstOrDefault(c =>
                (c is DiscordSectionComponent section && section.Components[0] is DiscordTextDisplayComponent sectionText && sectionText.Content.Contains(searchText))
                ||
                (c is DiscordTextDisplayComponent text && text.Content.Contains(searchText)),
                null);
        }

        public static string GetText(this DiscordComponent component)
        {
            return component switch
            {
                DiscordSectionComponent section => (section.Components[0] as DiscordTextDisplayComponent)!.Content,
                DiscordTextDisplayComponent text => text.Content,
                _ => throw new Exception($"Could not find text from component of type {component.GetType()}")
            };
        }
    }
}
