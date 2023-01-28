using DSharpPlus.Entities;

namespace ArtSubmissionsBot
{
    internal static class Cache
    {
        internal static Random Random = new();
        internal static Dictionary<ulong, Dictionary<ulong, bool>> VoteCache = new();

        internal static class Buttons
        {
            internal static DiscordButtonComponent VoteYes(ulong mID) => new(DSharpPlus.ButtonStyle.Success, $"vote_yes_{mID}", "", false, new(Emojis.VoteYesButton));
            internal static DiscordButtonComponent VoteNo(ulong mID) => new(DSharpPlus.ButtonStyle.Danger, $"vote_no_{mID}", "", false, new(Emojis.VoteNoButton));
            internal static DiscordButtonComponent MarkUnimplemented(ulong mID) => new(DSharpPlus.ButtonStyle.Secondary, $"mark_unimplemented_{mID}", "", false, new(Emojis.Submitted));
            internal static DiscordButtonComponent MarkImplmented(ulong mID) => new(DSharpPlus.ButtonStyle.Secondary, $"mark_implemented_{mID}", "", false, new (Emojis.Implemented));
        }

        internal static class Emojis
        {
            internal static readonly DiscordEmoji Submitted = DiscordEmoji.FromName(DiscordConnection.Client, IDs.Submitted);
            internal static readonly DiscordEmoji Accepted = DiscordEmoji.FromName(DiscordConnection.Client, IDs.Accepted);
            internal static readonly DiscordEmoji Implemented = DiscordEmoji.FromName(DiscordConnection.Client, IDs.Implemented);
            internal static readonly DiscordEmoji CloseRejected = DiscordEmoji.FromName(DiscordConnection.Client, IDs.CloseRejected);
            internal static readonly DiscordEmoji Rejected = DiscordEmoji.FromName(DiscordConnection.Client, IDs.Rejected);
            internal static readonly DiscordEmoji VoteYes = DiscordEmoji.FromName(DiscordConnection.Client, IDs.VoteYes);
            internal static readonly DiscordEmoji VoteNo = DiscordEmoji.FromName(DiscordConnection.Client, IDs.VoteNo);
            internal static readonly DiscordEmoji Status = DiscordEmoji.FromName(DiscordConnection.Client, IDs.Status);
            internal static readonly DiscordEmoji Votes = DiscordEmoji.FromName(DiscordConnection.Client, IDs.Votes);
            internal static readonly DiscordEmoji Notes = DiscordEmoji.FromName(DiscordConnection.Client, IDs.Notes);
            internal static readonly DiscordEmoji VoteYesButton = DiscordEmoji.FromName(DiscordConnection.Client, IDs.VoteYesButton);
            internal static readonly DiscordEmoji VoteNoButton = DiscordEmoji.FromName(DiscordConnection.Client, IDs.VoteNoButton);

            internal static class IDs
            {
                internal static readonly string Submitted = ":white_check_mark:";
                internal static readonly string Accepted = ":ballot_box_with_check:";
                internal static readonly string Implemented = ":checkered_flag:";
                internal static readonly string CloseRejected = ":arrows_counterclockwise:";
                internal static readonly string Rejected = ":x:";
                internal static readonly string VoteYes = ":white_check_mark:";
                internal static readonly string VoteNo = ":negative_squared_cross_mark:";
                internal static readonly string Status = ":cyclone:";
                internal static readonly string Votes = ":ballot_box:";
                internal static readonly string Notes = ":pencil:";
                internal static readonly string VoteYesButton = ":heavy_check_mark:";
                internal static readonly string VoteNoButton = ":heavy_multiplication_x:";
            }
        }

        internal static class Channels
        {
            internal static readonly DiscordChannel AssetSubmissions = DiscordConnection.Client.GetChannelAsync(IDs.AssetSubmissions).GetAwaiter().GetResult();
            internal static readonly DiscordChannel AssetVoting = DiscordConnection.Client.GetChannelAsync(IDs.AssetVoting).GetAwaiter().GetResult();
            internal static readonly DiscordChannel CompletedAssets = DiscordConnection.Client.GetChannelAsync(IDs.CompletedAssets).GetAwaiter().GetResult();
            internal static readonly DiscordChannel ImageCache = DiscordConnection.Client.GetChannelAsync(IDs.ImageCache).GetAwaiter().GetResult();

            internal static class IDs
            {
                internal static readonly ulong AssetSubmissions = 989751252810334238uL;
                internal static readonly ulong AssetVoting = 1062226182919180339uL;
                internal static readonly ulong CompletedAssets = 1062590663394349077uL;
                internal static readonly ulong ImageCache = 1068979750741233786uL;
            }
        }

        internal static class Colors
        {
            internal static readonly DiscordColor Submitted = DiscordColor.Goldenrod;
            internal static readonly DiscordColor Accepted = DiscordColor.DarkGreen;
            internal static readonly DiscordColor Implemented = DiscordColor.Lilac;
            internal static readonly DiscordColor CloseRejected = DiscordColor.MidnightBlue;
            internal static readonly DiscordColor Rejected = DiscordColor.Red;
        }
    }
}
