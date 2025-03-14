﻿using Lazvard.Message.Cli;
using Microsoft.Extensions.Logging;
using Spectre.Console;


using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddConsole();
});

AnsiConsole.Write(
    new FigletText("Lajvard")
    .Color(Color.Blue3_1)
    );
AnsiConsole.WriteLine("");
AnsiConsole.WriteLine("  Azure ServiceBus Simulation");
AnsiConsole.Write(new Rule());
AnsiConsole.WriteLine("");

await CommandHandler.Handle(args, loggerFactory);