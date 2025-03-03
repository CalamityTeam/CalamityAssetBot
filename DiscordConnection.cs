using ArtSubmissionsBot.EventProcessing;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json;

namespace ArtSubmissionsBot
{
    internal static class DiscordConnection
    {
        internal static DiscordClient Client { get; private set; }
        internal static DateTime StartupTime { get; private set; }
        internal static SlashCommandsExtension Slashies { get; private set; }
        internal static readonly string FilePrefix = "BotFiles/";

        internal static async Task RunAsync()
        {
            StartupTime = DateTime.UtcNow;
            VotePeriodHandler.VoteTallyTimer.Elapsed += new((s, e) => VotePeriodHandler.TallyVotesAsync().GetAwaiter().GetResult());
            FileManager.LoadVoteCache();

            Client = new DiscordClient(new DiscordConfiguration()
            {
                Token = File.ReadAllText($"{FilePrefix}Token.txt"),
                TokenType = TokenType.Bot,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug,
                Intents = DiscordIntents.All
            });

            Slashies = Client.UseSlashCommands();
            Slashies.RegisterCommands<SubmitCommand>();
            Slashies.RegisterCommands<AttachCurrentCommand>();

            Client.MessageCreated += new((client, args) => MessageCreated.Process(args));
            Client.MessageDeleted += new((client, args) => MessageDeleted.Process(args));
            Client.ComponentInteractionCreated += new((client, args) => ButtonPressed.Process(args));

            await Client.ConnectAsync();
            VotePeriodHandler.VoteTallyTimer.Start();
            await Task.Delay(-1);
        }
    }
}
