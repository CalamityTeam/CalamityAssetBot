using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace ArtSubmissionsBot.EventProcessing
{
    internal class AttachCurrentCommand : ApplicationCommandModule
    {
        [SlashCommandPermissions(DSharpPlus.Permissions.ManageMessages)]
        [SlashCommand("attachasset", "Attaches or updates a submission's currently in-use asset")]
        public async Task AttachAssetAsync(InteractionContext ctx,
            [Option("message-id", "The ID of the message in #asset-voting you are attaching an asset to")]string id,
            [Option("asset", "The asset currently in-use")]DiscordAttachment attachment)
        {
            if (ctx.Channel is DiscordDmChannel)
            {
                await ctx.CreateResponseAsync("You cannot use this command here!");
                return;
            }
            else if (ctx.Guild.Id != Cache.DevServerID)
            {
                var member = await ctx.Guild.GetMemberAsync(ctx.User.Id);
                if (member.Roles.Any(x => x.Id == Cache.DevRoleID))
                    await ctx.CreateResponseAsync($"Please only use this command in <#{Cache.Channels.IDs.ArtDiscussion}>!", true);
                else
                    await ctx.CreateResponseAsync("This command is only meant for developer use!");
                return;
            }
            else if (ctx.Channel.Id != Cache.Channels.IDs.ArtDiscussion)
            {
                await ctx.CreateResponseAsync($"Please only use this command in <#{Cache.Channels.IDs.ArtDiscussion}>!", true);
                return;
            }

            await ctx.DeferAsync();

            // Try to parse the ID and fetch the message
            DiscordMessage message;
            try
            {
                ulong uid = ulong.Parse(id);
                message = await Cache.Channels.AssetVoting.GetMessageAsync(uid);
                if (!message.Author.IsCurrent)
                    throw new Exception("Message is not from the bot!");
            }
            catch
            {
                await ctx.EditResponseAsync(new() { Content = "Message not found!" });
                return;
            }

            // See `Content`
            if (!attachment.MediaType.StartsWith("image"))
            {
                await ctx.EditResponseAsync(new()
                {
                    Content = "Unfortunately, only embed-able images and gifs can be attached as current assets\n" +
                    "(You can't upload a file to a message that's already been sent)"
                });
                return;
            }

            // Create the "current asset" embed
            string url = await attachment.ResetEphemeralAttachmentURL();
            DiscordMessageBuilder builder = new(message);

            // This line handles both clearing an already assigned current asset
            // And prepping to attach a new one
            builder.Embed = builder.Embeds[0];

            // Assemble and attach the embed, then edit the message
            var newEmbed = new DiscordEmbedBuilder()
                    .WithTitle("Current")
                    .WithImageUrl(url)
                    .WithColor(builder.Embed.Color.Value);
            builder.AddEmbed(newEmbed);
            await message.ModifyAsync(builder);

            // If an asset has already passed voting, all ties with the public ID are lost
            // Trying to parse one will result in a crash
            // So just return early
            if (!message.Components.Any())
            {
                await ctx.EditResponseAsync(new() { Content = "Done!" });
                return;
            }

            // Parse the ID, clear the components, and update
            ulong publicID = ulong.Parse(message.Components.First().Components.First().CustomId.Replace($"vote_yes_", ""));
            DiscordMessage publicMessage = await Cache.Channels.AssetSubmissions.GetMessageAsync(publicID);
            builder.ClearComponents();
            await publicMessage.ModifyAsync(builder);

            await ctx.EditResponseAsync(new() { Content = "Done!" });
        }
    }
}
