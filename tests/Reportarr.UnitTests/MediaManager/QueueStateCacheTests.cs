using JellyfinReporter.MediaManager;
using Xunit;

namespace Reportarr.UnitTests.MediaManager;

public class QueueStateCacheTests
{
    [Fact]
    public void UpdateAndGetSpeed_FirstTick_ReturnsNullAndRecords()
    {
        var cache = new QueueStateCache();
        var now = DateTime.UtcNow;

        var speed = cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 1000m, now, isDownloading: true);

        Assert.Null(speed);
    }

    [Fact]
    public void UpdateAndGetSpeed_SecondTick_ReturnsDerivedSpeed()
    {
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;
        cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 1000m, t0, isDownloading: true);

        // 500 bytes downloaded over 10 seconds => 50 B/s
        var speed = cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 500m, t0.AddSeconds(10), isDownloading: true);

        Assert.NotNull(speed);
        Assert.Equal(50, speed);
    }

    [Fact]
    public void UpdateAndGetSpeed_LessThanOneSecondElapsed_ReturnsNull()
    {
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;
        cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 1000m, t0, isDownloading: true);

        var speed = cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 900m, t0.AddMilliseconds(500), isDownloading: true);

        Assert.Null(speed);
    }

    [Fact]
    public void UpdateAndGetSpeed_SizeLeftIncreased_ResetsAndReturnsNull()
    {
        // A re-download / different item reusing the id: size grew, so we
        // reset baseline and don't report a bogus negative speed.
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;
        cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 500m, t0, isDownloading: true);

        var speed = cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 900m, t0.AddSeconds(10), isDownloading: true);

        Assert.Null(speed);
    }

    [Fact]
    public void UpdateAndGetSpeed_NotDownloading_RemovesKeyAndReturnsNull()
    {
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;
        cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 1000m, t0, isDownloading: true);

        var speed = cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 800m, t0.AddSeconds(5), isDownloading: false);

        Assert.Null(speed);
        // A subsequent downloading tick should be treated as first-tick again
        var speed2 = cache.UpdateAndGetSpeed("dl-1", currentSizeLeft: 800m, t0.AddSeconds(6), isDownloading: true);
        Assert.Null(speed2);
    }

    [Fact]
    public void UpdateAndGetSpeed_DownloadsAreTrackedIndependently()
    {
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;
        cache.UpdateAndGetSpeed("dl-a", 1000m, t0, true);
        cache.UpdateAndGetSpeed("dl-b", 2000m, t0, true);

        var speedA = cache.UpdateAndGetSpeed("dl-a", 500m, t0.AddSeconds(10), true);
        var speedB = cache.UpdateAndGetSpeed("dl-b", 1800m, t0.AddSeconds(10), true);

        Assert.Equal(50, speedA);
        Assert.Equal(20, speedB);
    }

    [Fact]
    public void PruneTo_RemovesKeysNotInLiveSet()
    {
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;
        cache.UpdateAndGetSpeed("dl-1", 1000m, t0, true);
        cache.UpdateAndGetSpeed("dl-2", 2000m, t0, true);
        cache.UpdateAndGetSpeed("dl-3", 3000m, t0, true);

        cache.PruneTo(["dl-1", "dl-3"]);

        // dl-2 was pruned, so a follow-up tick is treated as first-tick
        var speed = cache.UpdateAndGetSpeed("dl-2", 2000m, t0.AddSeconds(10), true);
        Assert.Null(speed);
        // dl-1 still has history
        var speed1 = cache.UpdateAndGetSpeed("dl-1", 500m, t0.AddSeconds(10), true);
        Assert.NotNull(speed1);
    }

    [Fact]
    public void UpdateAndGetSpeed_FallsBackToItemId_WhenDownloadIdIsNull()
    {
        // Items without a downloadId are keyed by their int id. Two different
        // null-downloadId items must not collide.
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;

        cache.UpdateAndGetSpeed("1", 1000m, t0, true);
        cache.UpdateAndGetSpeed("2", 2000m, t0, true);

        var speed1 = cache.UpdateAndGetSpeed("1", 500m, t0.AddSeconds(10), true);
        Assert.Equal(50, speed1);
    }
}
