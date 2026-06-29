using System.Text;

namespace JellyfinReporter.Discord;

public static class MessageTemplateHelper
{
    public const string HealthHeader = "🟢 Jellyfin Health";

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
        "<@{0}> Hickory switch and a bucket of spit, your server fell over and died like a twit.",
        "<@{0}> It's raining, it's pouring, your server's a whore-ing piece of garbage that died on the floor.",
        "<@{0}> There was an old lady who lived in a shoe — she had so many children, your server caught one and died too.",
        "<@{0}> Little Boy Blue come blow your horn — the server's in the haystack, dead and forlorn.",
        "<@{0}> Georgie Porgie pudding and pie — your server crashed, started to fry, and then it up and fucking died.",
        "<@{0}> Peter Peter pumpkin eater, had a server but couldn't keep her — she crashed and burned and now you weep, you incompetent creep.",
        "<@{0}> Hey diddle diddle, the cat and the fiddle — the server jumped over the moon, missed, and died in a ditch with a spoon.",
        "<@{0}> Little Bo Peep has lost her sheep — and also her server that's six feet deep.",
        "<@{0}> Jack be nimble, Jack be quick — Jack couldn't fix his server and took a dick.",
        "<@{0}> A-tisket a-tasket, your server's in a casket — brown and yellow, what a shitty fellow.",
        "<@{0}> Wee Willie Winkie runs through the town — your server gave up, shut the fuck down.",
        "<@{0}> Simple Simon met a pie man going to the fair — said your server's fucking broken and nobody will care.",
        "<@{0}> I'm a little teapot short and stout — tip me over and pour your server's fucking doubt, 'cause it's dead.",
        "<@{0}> Eeeny meeny miny moe — your server's dead and it shows.",
        "<@{0}> Here we go round the mulberry bush — your server's a useless pile of mush.",
        "<@{0}> One two buckle my shoe — three four server's dead on the floor.",
        "<@{0}> On top of spaghetti all covered in cheese — your server died slowly and brought us to our knees.",
        "<@{0}> Down by the station early in the morning — your server took a dirt nap without any warning.",
        "<@{0}> Head and shoulders, knees and toes — your server's dead and nobody knows what the fuck happened.",
        "<@{0}> Two little dicky birds sitting on a wall — one named Peter, one named Paul — fly away Peter, fly away Paul — but your server can't fly 'cause it fucking died, that's all.",
        "<@{0}> The eensy weensy spider climbed up the water spout — down came the rain and washed your server out — out came the sun and dried up all the rain — and your server is still a useless piece of shit again.",
        "<@{0}> Five little ducks went out to play — over the hills and far away — mother duck said quack quack quack — but your server went tits up and won't come back.",
        "<@{0}> Ten in the bed and the little one said roll over — they all rolled over and your server fell out and broke into chunks on the floor like a fucking clover.",
        "<@{0}> The wheels on the bus go round and round — your server fell off and hit the ground.",
        "<@{0}> Baby shark doo doo doo doo doo doo — your server died doo doo doo doo doo doo — what a shitshow doo doo doo doo doo doo — it's in hell now.",
        "<@{0}> Found a peanut, found a peanut — just now — just now — found your server fucking dead and broke my goddamn heart somehow.",
        "<@{0}> Alexander was a hero, Alexander was great — but your server just shat itself and sealed its own damn fate.",
        "<@{0}> Here we go looby loo, here we go looby light — your server died on a Tuesday night.",
        "<@{0}> Frog went a-courting and he did ride — with sword and pistol by his side — your server went a-dying and it sighed — 'I can't believe I fucking died.'",
        "<@{0}> Go in and out the window, go in and out the window — go in and out the window — your server hit the floor.",
        "<@{0}> Mama's little baby loves shortening bread — your server's so fucking dead it painted the room red.",
        "<@{0}> Oh my darling, oh my darling, oh my darling Clementine — you are dead and gone forever, just like your server's fucking spine.",
        "<@{0}> The farmer in the dell, the farmer in the dell — the farmer's server fell — and then it burned to hell.",
        "<@{0}> Alice the camel has three humps — Alice the camel had two humps — Alice the camel had one hump — your server had no pulse and went down like a chump.",
        "<@{0}> John Jacob Jingleheimer Schmidt — his server died just like yours did — so don't you fucking bitch.",
        "<@{0}> When the saints go marching in — when the saints go marching in — your server is sure as hell not joining — 'cause it's rotting in a fucking bin.",
        "<@{0}> I've been working on the railroad — all the livelong day — I've been working on the railroad — your server went and passed away.",
        "<@{0}> Do your ears hang low? Do they wobble to and fro? Can you tie 'em in a knot? Can you tie 'em in a bow? Your server can't do shit 'cause it's six feet down below.",
        "<@{0}> The ants go marching one by one hurrah hurrah — the ants go marching one by one your server's day is fucking done.",
        "<@{0}> Swing low sweet chariot coming for to carry me home — swing low sweet chariot — your server died alone.",
        "<@{0}> Kumbaya my Lord kumbaya — your server's crashed and gone away.",
        "<@{0}> What shall we do with a drunken server? What shall we do with a drunken server? Put it in the ground until it's fucking sober.",
        "<@{0}> Oh Shenandoah I long to see you — and your server working like it's supposed to — but it's not you bastard fix your shit.",
        "<@{0}> I love the mountains, I love the rolling hills — I love the flowers, I love the daffodils — I hate your fucking server 'cause it never works and gives me chills.",
        "<@{0}> Let me call you sweetheart, I'm in love with you — let me see your server working for a minute or two — but it's down again you fuckwit, what the hell you gonna do?",
        "<@{0}> You are my sunshine, my only sunshine — your server makes me happy when skies are gray — but now it's broken so please go fix it — or I'll shove this monitor up your ass all day.",
        "<@{0}> She'll be coming 'round the mountain when she comes — she'll be riding six white horses when she comes — but your server won't be coming 'round the corner — it's a smoking digital corpse."
    ];
    public static string GetServerStatusMessage(bool isHealthy)
    {
        var sb = new StringBuilder();
        var status = isHealthy ? "HEALTHY   🟢" : "UNHEALTHY 🔴";
        var nowUtc = DateTime.UtcNow;
        var timestamp = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _tz).ToString("ddd MMM dd hh:mm tt");

        sb.AppendLine(HealthHeader);
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