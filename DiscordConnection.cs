using ArtSubmissionsBot.EventProcessing;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArtSubmissionsBot
{
    internal static class DiscordConnection
    {
        internal static DiscordClient Client => client.Task.GetAwaiter().GetResult();
        private static readonly TaskCompletionSource<DiscordClient> client = new(TaskCreationOptions.RunContinuationsAsynchronously);
        
        internal static DateTime StartupTime { get; private set; }

        internal const string FilePrefix = "BotFiles/";

        internal static async Task RunAsync()
        {
            StartupTime = DateTime.UtcNow;
            VotePeriodHandler.VoteTallyTimer.Elapsed += new((s, e) => VotePeriodHandler.TallyVotesAsync().GetAwaiter().GetResult());

            var builder = new HostApplicationBuilder();

            builder.Services.AddLogging(l => l.AddConsole());
            builder.Services.AddDiscordClient(await File.ReadAllTextAsync($"{FilePrefix}Token.txt"), DiscordIntents.All);
            
            builder.Services.ConfigureEventHandlers(events =>
            {
                events.HandleMessageCreated(async (_, args) => await MessageCreated.Process(args));
                events.HandleMessageDeleted(async (_, args) => await MessageDeleted.Process(args));
                events.HandleMessageReactionAdded(async (_, args) => await ReactionAdded.Process(args));
                events.HandleComponentInteractionCreated(async (_, args) => await ButtonPressed.Process(args));
            });

            builder.Services.AddCommandsExtension(
                (_, commands) => {
                commands.AddProcessor<SlashCommandProcessor>();
                commands.AddCommands<SubmitCommand>();
                commands.AddCommands<AttachCurrentCommand>();
            },
                new CommandsConfiguration() {
                RegisterDefaultCommandProcessors = false,
                UseDefaultCommandErrorHandler = false
            });

            builder.Services.PostConfigureAll<DiscordClient>(c => client.SetResult(c));
            VotePeriodHandler.VoteTallyTimer.Start();
            
            await builder.Build().RunAsync();
            await Task.Delay(-1);
        }

        internal static void SetEmbed(this DiscordMessageBuilder builder, DiscordEmbed embed)
        {
            builder.ClearEmbeds();
            builder.AddEmbed(embed);
        }
    }
}
