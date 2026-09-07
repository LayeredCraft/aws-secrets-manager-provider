using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AWSConfiguration.Core.Internal;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AWSConfiguration.Core.Tests.Internal;

public class PollingConfigurationProviderTests
{
    private sealed class FakePollingProvider : PollingConfigurationProvider
    {
        public Func<CancellationToken, HashSet<(string, string?)>> FetchImpl { get; set; } = _ => new();
        public TimeSpan? Interval { get; set; }

        public FakePollingProvider() : base(null)
        {
        }

        protected override string ResourceDescription => "test values";

        protected override string ResourceNoun => "test value";

        protected override string DuplicateKeyOptionsHint => "Adjust the test options.";

        protected override TimeSpan? PollingInterval => Interval;

        protected override Task<HashSet<(string, string?)>> FetchConfigurationAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(FetchImpl(cancellationToken));
        }
    }

    private static HashSet<(string, string?)> Values(params (string Key, string? Value)[] pairs)
    {
        return new HashSet<(string, string?)>(pairs);
    }

    [Fact]
    public void Load_should_populate_data_from_fetch()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key1", "Value1"), ("Key2", "Value2"))
        };

        sut.Load();

        sut.Get("key1").Should().Be("Value1");
        sut.Get("KEY2").Should().Be("Value2");
    }

    [Fact]
    public void Load_should_throw_on_duplicate_configuration_keys()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key", "First"), ("Key", "Second"))
        };

        var loadAction = () => sut.Load();

        var exception = loadAction.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Configuration key 'Key' was generated more than once (keys are case-insensitive).");
        exception.Message.Should().Contain("Adjust the test options.");
    }

    [Fact]
    public async Task ForceReloadAsync_should_fire_reload_token_when_values_change()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key", "Initial"))
        };

        var callbackCallCount = 0;
        object? callbackState = null;
        void ChangeCallback(object? state)
        {
            callbackCallCount++;
            callbackState = state;
        }

        const string changeCallbackState = "state";
        sut.GetReloadToken().RegisterChangeCallback(ChangeCallback, changeCallbackState);

        sut.Load();
        callbackCallCount.Should().Be(0);

        sut.FetchImpl = _ => Values(("Key", "Updated"));

        await sut.ForceReloadAsync(CancellationToken.None);

        callbackCallCount.Should().Be(1);
        callbackState.Should().BeSameAs(changeCallbackState);
        sut.Get("Key").Should().Be("Updated");
    }

    [Fact]
    public async Task ForceReloadAsync_should_not_fire_reload_token_when_values_unchanged()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key", "Initial"))
        };

        var callbackCallCount = 0;
        void ChangeCallback(object? state) => callbackCallCount++;

        sut.GetReloadToken().RegisterChangeCallback(ChangeCallback, null);

        sut.Load();

        await sut.ForceReloadAsync(CancellationToken.None);

        callbackCallCount.Should().Be(0);
        sut.Get("Key").Should().Be("Initial");
    }

    [Fact]
    public void Polling_should_reload_when_values_change()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key", "Initial")),
            Interval = TimeSpan.FromMilliseconds(100)
        };

        var callbackCallCount = 0;
        void ChangeCallback(object? state) => callbackCallCount++;

        sut.GetReloadToken().RegisterChangeCallback(ChangeCallback, null);

        sut.Load();
        sut.Get("Key").Should().Be("Initial");

        sut.FetchImpl = _ => Values(("Key", "Updated"));

        Thread.Sleep(300);

        callbackCallCount.Should().Be(1);
        sut.Get("Key").Should().Be("Updated");
    }

    [Fact]
    public void Dispose_should_stop_polling_without_throwing()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key", "Initial")),
            Interval = TimeSpan.FromMilliseconds(50)
        };

        sut.Load();

        var disposeAction = sut.Dispose;

        disposeAction.Should().NotThrow();
    }
}
