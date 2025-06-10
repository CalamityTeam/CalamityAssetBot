using CalamityAssetBot.Commands;
using CalamityAssetBot.Hosting;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;

namespace CalamityAssetBot;

[UsedImplicitly]
public class Cache : BackgroundService
{
    public Cache(DiscordClient client) => source.SetResult(client);
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Delay(-1, stoppingToken);
    
    private static readonly TaskCompletionSource<DiscordClient> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static DiscordClient Client => Await(source.Task);
    
    private static T Await<T>(Task<T> task) => task.GetAwaiter().GetResult();

    public static class Servers
    {
        public static class IDs
        {
            public const ulong DevServerID = 458428222061936650uL;
            public const ulong ArtServerID = 404725126425214999uL;
        }

        public static readonly DiscordGuild DevServer = Await(Client.GetGuildAsync(IDs.DevServerID));
        public static readonly DiscordGuild ArtServer = Await(Client.GetGuildAsync(IDs.ArtServerID));
    }

    public static class Channels
    {
        public static class DevServer
        {
            public static readonly DiscordChannel AssetVoting = Await(Client.GetChannelAsync(612004648118779904uL));
            public static readonly DiscordChannel CompletedAssets = Await(Client.GetChannelAsync(458446442340548639uL));
            public static readonly DiscordChannel AssetDiscussion = Await(Client.GetChannelAsync(1303223163471265852uL));
        }
        
        public static class ArtServer
        {
            public static readonly DiscordChannel AssetSubmissions = Await(Client.GetChannelAsync(459252132068065280uL));
        }

        public static class DataBase
        {
            public static readonly DiscordChannel ImageCache = Await(Client.GetChannelAsync(1068979750741233786uL));
        }
    }

    public static class Emojis
    {
        public static class Legend
        {
            public static readonly DiscordEmoji PositiveVotes = DiscordEmoji.FromName(Client, ":white_check_mark:");
            public static readonly DiscordEmoji ImprovementVotes = DiscordEmoji.FromName(Client, ":tools:");
            public static readonly DiscordEmoji NegativeVotes = DiscordEmoji.FromName(Client, ":x:");
            
            public static readonly DiscordEmoji Status = DiscordEmoji.FromName(Client, ":cyclone:");
            public static readonly DiscordEmoji Voting = DiscordEmoji.FromName(Client, ":ballot_box:");
            public static readonly DiscordEmoji Notes = DiscordEmoji.FromName(Client, ":book:");
            
            public static readonly DiscordEmoji DevNotes = DiscordEmoji.FromName(Client, ":mag:");
            public static readonly DiscordEmoji ArtistFeedback = DiscordEmoji.FromName(Client, ":pencil:");
        }
        
        public static class Votes
        {
            public static readonly DiscordEmoji Yes = DiscordEmoji.FromGuildEmote(Client, 1097490843243393044uL);
            public static readonly DiscordEmoji NeedsImprovement = Legend.ImprovementVotes; // DiscordEmoji.FromName(Client, ":twisted_rightwards_arrows:");
            public static readonly DiscordEmoji No = DiscordEmoji.FromGuildEmote(Client, 1097490848742117458uL);
            public static readonly DiscordEmoji Neutral = DiscordEmoji.FromGuildEmote(Client, 1097490850285625424uL);
        }
        
        public static class Button
        {
            public static readonly DiscordEmoji Submitted = DiscordEmoji.FromName(Client, ":white_check_mark:");
            public static readonly DiscordEmoji Implemented = DiscordEmoji.FromName(Client, ":checkered_flag:");
        }
    }

    public static class Roles
    {
        public static readonly DiscordRole Spriter = Await(Servers.DevServer.GetRoleAsync(458434431498321920uL));
    }

    public static class Colors
    {
        public static readonly DiscordColor Submitted = DiscordColor.Goldenrod;
        public static readonly DiscordColor Accepted = DiscordColor.DarkGreen;
        public static readonly DiscordColor Rejected = DiscordColor.Red;
        public static readonly DiscordColor CloseRejected = DiscordColor.MidnightBlue;
        public static readonly DiscordColor NeedsImprovementRejected = DiscordColor.Purple;
        public static readonly DiscordColor Implemented = DiscordColor.Lilac;
    }
    
    public static class Buttons
    {
        public static readonly DiscordButtonComponent MarkUnimplemented = new(DiscordButtonStyle.Secondary, "mark_unimplemented", "", false, new(Emojis.Button.Submitted));

        public static readonly DiscordButtonComponent MarkImplemented = new(DiscordButtonStyle.Secondary, "mark_implemented", "", false, new(Emojis.Button.Implemented));

        public static readonly DiscordButtonComponent AddDeveloperNotesPrompt = new(DiscordButtonStyle.Secondary, "devnotes", "Add Selection Notes", false);
        
        public static readonly DiscordButtonComponent AddFeedbackPrompt = new(DiscordButtonStyle.Secondary, "devfeedback", "Add Feedback", false);

        public static DiscordSelectComponent AssetTypeSelection(AssetType currentAssetType) => new("assettype", "Asset Type",
            Enum.GetValues<AssetType>().Select(c => new DiscordSelectComponentOption(Enum.GetName<AssetType>(c)!, Enum.GetName<AssetType>(c)!, isDefault: c == currentAssetType)), false, 1, 1);

        public static DiscordLinkButtonComponent ArtServerQuickLink(DiscordMessage message) => new(message.JumpLink.AbsoluteUri, "Jump to Art Server");
    }

    public static class Modals
    {
        public static DiscordInteractionResponseBuilder AddDeveloperNotes(string? currentNotes, ulong devId) => new DiscordInteractionResponseBuilder()
            .WithCustomId($"{devId}_notes")
            .WithTitle("Developer Acceptance Notes")
            .AddTextInputComponent(new("Notes", "notes", "Which variant was selected?", currentNotes, false, DiscordTextInputStyle.Short));
        
        public static DiscordInteractionResponseBuilder AddFeedback(string? currentFeedback, ulong devId) => new DiscordInteractionResponseBuilder()
            .WithCustomId($"{devId}_feedback")
            .WithTitle("Art Team Feedback")
            .AddTextInputComponent(new("Feedback", "feedback", "Before resubmitting, you should...", currentFeedback, false, DiscordTextInputStyle.Paragraph));
    }
}