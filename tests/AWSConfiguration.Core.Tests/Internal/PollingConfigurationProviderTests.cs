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

        public FakePollingProvider(Microsoft.Extensions.Logging.ILogger? logger = null) : base(logger)
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

    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
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
        exception.Message.Should().Contain("Configuration key 'Key' was generated more than once with different values (keys are case-insensitive).");
        exception.Message.Should().Contain("Adjust the test options.");
    }

    [Fact]
    public void Load_should_throw_on_case_variant_duplicate_keys_with_different_values()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key", "First"), ("KEY", "Second"))
        };

        var loadAction = () => sut.Load();

        loadAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*Configuration key 'KEY' was generated more than once with different values (keys are case-insensitive).*");
    }

    [Fact]
    public void Load_should_warn_and_ignore_case_variant_duplicate_keys_with_identical_values()
    {
        var logger = new RecordingLogger();
        var sut = new FakePollingProvider(logger)
        {
            FetchImpl = _ => Values(("Key", "Value"), ("KEY", "Value"))
        };

        sut.Load();

        sut.Get("Key").Should().Be("Value");
        logger.Messages.Should().Contain(m => m.Contains("Duplicate configuration key"));
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

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && callbackCallCount == 0)
        {
            Thread.Sleep(50);
        }

        callbackCallCount.Should().Be(1);
        sut.Get("Key").Should().Be("Updated");
    }

    [Fact]
    public void Polling_should_keep_previous_data_when_reload_produces_duplicate_keys()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key", "Initial")),
            Interval = TimeSpan.FromMilliseconds(50)
        };

        sut.Load();
        sut.Get("Key").Should().Be("Initial");

        sut.FetchImpl = _ => Values(("Key", "First"), ("Key", "Second"));

        Thread.Sleep(250);

        sut.Get("Key").Should().Be("Initial");
    }

    [Fact]
    public void Polling_should_retry_reload_after_duplicate_key_failure()
    {
        var sut = new FakePollingProvider
        {
            FetchImpl = _ => Values(("Key", "Initial")),
            Interval = TimeSpan.FromMilliseconds(50)
        };

        sut.Load();
        sut.Get("Key").Should().Be("Initial");

        // A reload that fails duplicate-key validation must not be marked as
        // applied; otherwise identical subsequent fetches would be skipped and
        // the data would go stale.
        sut.FetchImpl = _ => Values(("Key", "First"), ("Key", "Second"));
        Thread.Sleep(250);
        sut.Get("Key").Should().Be("Initial");

        sut.FetchImpl = _ => Values(("Key", "Recovered"));

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && sut.Get("Key") != "Recovered")
        {
            Thread.Sleep(50);
        }

        sut.Get("Key").Should().Be("Recovered");
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
