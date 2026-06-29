using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace JellyfinReporter.Discord;

/// <summary>
/// Finds or creates pinned status messages by a unique header prefix, so
/// multiple pinned messages (Jellyfin health, Sonarr queue, Radarr queue)
/// can coexist in the same channel without ambiguity.
/// </summary>
public static class PinnedMessageLocator
{
    /// <summary>
    /// Finds the pinned message whose content starts with <paramref name="headerPrefix"/>.
    /// Returns null if none match.
    /// </summary>
    public static async Task<RestMessage?> FindByHeaderAsync(TextChannel channel, string headerPrefix, CancellationToken cancellationToken = default)
    {
        var pinned = await channel.GetPinnedMessagesAsync(cancellationToken: cancellationToken);
        return pinned.FirstOrDefault(m => m.Content.StartsWith(headerPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Finds the pinned message starting with <paramref name="headerPrefix"/>. If not found,
    /// sends a new message with <paramref name="properties"/> and pins it.
    /// </summary>
    public static async Task<RestMessage> FindOrCreatePinnedAsync(
        TextChannel channel,
        string headerPrefix,
        MessageProperties properties,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByHeaderAsync(channel, headerPrefix, cancellationToken);
        if (existing is not null)
            return existing;

        var message = await channel.SendMessageAsync(properties, cancellationToken: cancellationToken);
        await message.PinAsync(cancellationToken: cancellationToken);
        return message;
    }

    /// <summary>
    /// Finds a pinned message by header, adopting an orphan pinned message
    /// (one starting with "```") when a header match doesn't exist. The orphan
    /// is reformatted to the new header scheme by replacing its content with
    /// <paramref name="properties"/>. Returns null only if neither a header
    /// match nor an orphan is found.
    /// </summary>
    public static async Task<RestMessage?> FindOrAdoptOrphanAsync(
        TextChannel channel,
        string headerPrefix,
        MessageProperties properties,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByHeaderAsync(channel, headerPrefix, cancellationToken);
        if (existing is not null)
            return existing;

        var pinned = await channel.GetPinnedMessagesAsync(cancellationToken: cancellationToken);
        var orphan = pinned.FirstOrDefault(m => m.Content.StartsWith("```", StringComparison.Ordinal));

        if (orphan is null)
            return null;

        await orphan.ModifyAsync(p =>
        {
            p.Content = properties.Content;
            p.Components = properties.Components;
        }, cancellationToken: cancellationToken);

        return orphan;
    }
}
