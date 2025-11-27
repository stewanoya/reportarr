using System.Text;

namespace JellyfinReporter;

public static class MessageTemplateHelper
{
    private const string _eST = "Eastern Standard Time";
    private static readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(_eST);
    private static readonly string[] _messages =
    [
        "Hey <@{0}>, twinkle twinkle little star, your server's fucked beyond bizarre.",
        "<@{0}> Hickory dickory dock, your shitty server's in shock — and also dead. Tick tock.",
        "<@{0}> Mary had a little lamb, your server had a little *cram* — then exploded like a damn traffic jam.",
        "<@{0}> Ring around the rosie, your server's getting toasty — ashes, ashes, it's fucked to hell, you nosy.",
        "<@{0}> Row row row your boat, gently off a cliff — your server did exactly that, now it's dead as shit.",
        "<@{0}> Old MacDonald had a farm, E-I-E-I-O… and on that farm your server died, what a fucking show.",
        "<@{0}> Humpty Dumpty sat on a wall, your server fell harder and shattered its balls.",
        "<@{0}> Baa baa black sheep, have you any RAM? 'No sir, no sir,' said your broken-ass program.",
        "<@{0}> This little server went to market, this little server stayed home, this little server said 'fuck this shit' and shut its whole ass down.",
        "<@{0}> London Bridge is falling down — and so is your fucking server, clown.",
        "<@{0}> Patty cake, patty cake, baker's man — your server burned down faster than it ran.",
        "<@{0}> Jack and Jill went up the hill to fetch a pail of water — your server stayed at home to die, because it's a useless bastard.",
        "<@{0}> Little Miss Muffet sat on a tuffet, your server sat on a bug and completely ate shit.",
        "<@{0}> If you're happy and you know it, clap your hands — but your server isn't happy; it died and pissed its pants.",
        "<@{0}> Hickety pickety my black hen, your server won't be alive again.",
        "<@{0}> Rub-a-dub-dub, three men in a tub — your server joined them because it's a soggy-ass dud.",
        "<@{0}> Pop goes the weasel — pop goes your server, right up its own diesel-soaked asshole.",
        "<@{0}> Skip to my Lou, my darling — skip to the part where your server fucking collapsed.",
        "<@{0}> Three blind mice, three blind mice — your server runs code about as well as those fuckers could drive.",
        "<@{0}> Hickory switch and a bucket of spit, your server fell over and died like a twit."
    ];
    public static string GetServerStatusMessage(bool isHealthy)
    {
        var sb = new StringBuilder();
        var status = isHealthy ? "HEALTHY   🟢" : "UNHEALTHY 🔴";
        var nowUtc = DateTime.UtcNow;
        var timestamp = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _tz).ToString("ddd MMM dd hh:mm tt");

        sb.AppendLine("```");
        sb.AppendLine("┌─────────────────────────────────┐");
        sb.AppendLine("│        SERVER STATUS REPORT     │");
        sb.AppendLine("├─────────────────────────────────┤");
        sb.AppendLine($"│ Status: {status,-24}│");
        sb.AppendLine($"│ As of:  {timestamp,-23} │");
        sb.AppendLine("└─────────────────────────────────┘");
        sb.AppendLine("```");

        return sb.ToString();
    }

    public static string GetUnhealthyTaggedMessage(ulong userId)
    {
        var random = new Random();
        var selected = _messages[random.Next(_messages.Length)];

        return string.Format(selected, userId);
    }
}