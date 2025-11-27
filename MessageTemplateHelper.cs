using System.Text;

namespace JellyfinReporter;

public static class MessageTemplateHelper
{
    public static string GetServerStatusMessage(bool isHealthy)
    {
        var sb = new StringBuilder();
        var status = isHealthy ? "HEALTHY   🟢" : "UNHEALTHY 🔴";
        var timestamp = DateTime.Now.ToString("ddd MMM dd hh:mm tt");

        sb.AppendLine("```");
        sb.AppendLine("┌─────────────────────────────────────┐");
        sb.AppendLine("│        SERVER STATUS REPORT         │");
        sb.AppendLine("├─────────────────────────────────────┤");
        sb.AppendLine($"│ Status: {status,-28}│");
        sb.AppendLine($"│ As of:  {timestamp,-27} │");
        sb.AppendLine("└─────────────────────────────────────┘");
        sb.AppendLine("```");

        return sb.ToString();
    }

    public static string GetUnhealthyTaggedMessage(ulong userId)
    {
        var messages = new[]
        {
            "<@{0}> Wake the fuck up samurai, your shitty server is burning. Again.",
            "Hey <@{0}>, your server just ate shit harder than your last relationship.",
            "<@{0}> Congrats dipshit, your server has achieved maximum death. 💀",
            "BREAKING: <@{0}>'s poverty-spec potato server has catastrophically shit itself.",
            "<@{0}> Your server is deader than your social life. And that's saying something.",
            "Yo <@{0}>, the server is down. I'd act surprised but we both know you're incompetent as fuck.",
            "<@{0}> Server status: Absolutely fucking cooked. Your fault. Do better.",
            "Alert: <@{0}>'s janky-ass dumpster fire has finally collapsed. Shocking nobody.",
            "<@{0}> The server died faster than your will to live. Fix it, dumbass.",
            "Hey genius <@{0}>, your bargain-bin server can't handle being turned on. It's dead.",
            "<@{0}> Plot twist: Server = fucked. You = also fucked. Get moving.",
            "Surprise motherfucker! <@{0}>'s server has shit the bed spectacularly.",
            "<@{0}> Your server is having a moment. A 'being fucking dead' moment.",
            "PSA: <@{0}> spent $3 on hosting and it shows. Server's toast. RIP.",
            "<@{0}> How many times do we have to teach you this lesson, old man? SERVER. IS. DOWN.",
            "Friendly reminder <@{0}>: Your server is more unstable than your mental health. It's offline.",
            "<@{0}> Imagine being this bad at hosting. Couldn't be me. Server's dead, chief.",
            "URGENT: <@{0}>'s clown-ass server has entered the afterlife. Permanently offline.",
            "<@{0}> The server you forgot to feed has died of neglect. Great job, jackass.",
            "Hey <@{0}>, your server just pulled a Titanic. Down and not coming back up anytime soon."
        };

        var random = new Random();
        var selected = messages[random.Next(messages.Length)];

        return string.Format(selected, userId);
    }
}