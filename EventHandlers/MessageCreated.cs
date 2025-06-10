using CalamityAssetBot.Hosting;
using DSharpPlus.EventArgs;

namespace CalamityAssetBot.EventHandlers;

public class MessageCreated : DiscordEventHandler<MessageCreatedEventArgs>
{
    private static readonly string[] testCommands = ["!t", "!test"];

    public override async Task HandleAsync(MessageCreatedEventArgs args)
    {
        if (args.Channel == Cache.Channels.ArtServer.AssetSubmissions && !args.Message.Author!.IsCurrent)
            await args.Message.DeleteAsync();

        else if (args.Channel == Cache.Channels.DevServer.AssetDiscussion && testCommands.Contains((args.Message.Content ?? "")))
        {
            TimeSpan uptime = DateTime.UtcNow - DiscordBotService.StartupTime;
            TimeSpan latency = Cache.Client.GetConnectionLatency(args.Guild.Id);

            await args.Message.RespondAsync($"Asset Bot Stats\n" +
                $"\n" +
                $"Response Time: {(int)Math.Round(latency.TotalMilliseconds)}ms\n" +
                $"Uptime: {uptime.Days}d, {uptime.Hours}h, {uptime.Minutes}m, {uptime.Seconds}s");
        }
    }
}