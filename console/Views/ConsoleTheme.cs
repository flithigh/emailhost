using Spectre.Console;

namespace EmailHost.ConsoleApp.Views;

public static class ConsoleTheme
{
    public static void RenderHeader(string title, string subtitle = "")
    {
        AnsiConsole.Clear();
        var rule = new Rule($"[bold cyan]{Markup.Escape(title)}[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("deepskyblue1")
        };
        AnsiConsole.Write(rule);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            AnsiConsole.MarkupLine($"[grey]» {Markup.Escape(subtitle)}[/]");
        }
        AnsiConsole.WriteLine();
    }

    public static void RenderBanner()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("Flitflap")
                .Centered()
                .Color(Color.DeepSkyBlue1));

        var panel = new Panel(
            Align.Center(
                new Markup("[bold white]Flitflap Email Host - Tenant & Operator Console[/]\n" +
                           "[grey]ASP.NET Core 9 • PostgreSQL Dual-Tier • Garage S3 Engine[/]")))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("cyan"),
            Padding = new Padding(2, 0, 2, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public static void PressAnyKeyToContinue(string prompt = "Press any key to continue...")
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(prompt)}[/]");
        System.Console.ReadKey(true);
    }
}
