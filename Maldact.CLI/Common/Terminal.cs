using Spectre.Console;

namespace Maldact.CLI.Common;

/// <summary>
/// Provides globally routed access to standard output and standard error ANSI consoles.
/// </summary>
public static class Terminal
{
    /// <summary>
    /// Gets or sets the primary console engine routed to stdout.
    /// </summary>
    public static IAnsiConsole Out { get; set; } = AnsiConsole.Console;

    /// <summary>
    /// Gets or sets the console engine routed explicitly to stderr for diagnostic and error payloads.
    /// </summary>
    public static IAnsiConsole Error { get; set; } = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(Console.Error)
    });
}