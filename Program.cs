using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CalamityAssetBot;
using CalamityAssetBot.Hosting;
using CalamityAssetBot.Logging;
using CalamityAssetBot.Utils;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;

//----------------------//
//----------------------//

var workingDirectory = Directory.CreateDirectory("BotFiles");
Directory.SetCurrentDirectory(workingDirectory.FullName);

AppDomain.CurrentDomain.UnhandledException += (sender, exception) =>
{
    DiscordLogger.Writer.Close();
    File.WriteAllText("Crash.txt", exception.ExceptionObject.ToString());
};

var builder = new HostApplicationBuilder();

builder.Services.AddHostedSingleton<DiscordBotService>();
builder.Services.AddHostedSingleton<Cache>();

builder.Services.AddDiscordClient(DiscordBotService.Token, DiscordBotService.Intents);
builder.Services.RegisterEventHandlers();
builder.Services.AddAsyncTimers();

builder.Services.AddCommandsExtension(
    (_, commands) => {
        commands.AddCheck<DiscordBotService.OneCommandAtATime>();
        commands.AddProcessor<SlashCommandProcessor>();
        commands.AddCommands(typeof(DiscordBotService).Assembly);
        commands.CommandExecuted += async (_, args) => await DiscordBotService.OneCommandAtATime.CompleteCommandAsync();
        commands.CommandErrored += async (_, args) => await DiscordBotService.OneCommandAtATime.CompleteCommandAsync();
    },

    new CommandsConfiguration() {
        RegisterDefaultCommandProcessors = false,
        UseDefaultCommandErrorHandler = false
    }
);

builder.Services.AddLogging(logger =>
{
    logger.ClearProviders();
    logger.AddProvider(new DiscordLoggerProvider());
    logger.SetMinimumLevel(LogLevel.Trace);
});

var host =  builder.Build();
host.Run();