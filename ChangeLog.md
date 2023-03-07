# ChangeLog
## still addresses the following issue(s)
- https://github.com/TwitchLib/TwitchLib/issues/1093
- https://github.com/TwitchLib/TwitchLib.Client/issues/206
- https://github.com/TwitchLib/TwitchLib/issues/1104

## addresses the follwing issue(s)
- https://github.com/TwitchLib/TwitchLib.Communication/issues/13
- https://github.com/TwitchLib/TwitchLib.Communication/issues/7

## public
This section describes the changes that probably affect consumers of this API.
### behavioral
The `ReconnectionPolicy` within the `IClientOptions` is going to be enforced now right from the beginning.

If no `ReconnectionPolicy` is passed to `IclientOptions` constructor, a `default` is going to be used.

That `default` is going to make 10 attempts, each 3 seconds one, to connect to twitch.

OK. While writing this expensive ChangeLog, I experience that I missed something.
If no reconnect is desired, at this very moment the ctor `public ReconnectionPolicy(int reconnectInterval, int? maxAttempts)` should be used with a `value` of `1` for `maxAttempts`.

Im going to fix that...


### IClient, WebSocketClient and TcpClient
#### ctor
- ability to pass an pass an `Microsoft.Extensions.Logging.ILogger` into it

#### removed
- `TimeSpan DefaultKeepAliveInterval { get; set; }`
    - has never been used
- `int SendQueueLength { get; }`
- `int WhisperQueueLength { get; }`
- `void SendFailed(OnSendFailedEventArgs eventArgs);`
    - to raise/invoke `OnSendFailed`
    - should not be done from outside
- `void Error(OnErrorEventArgs eventArgs);`
    - to raise/invoke `OnError`
    - should not be done from outside
- `void MessageThrottled(OnMessageThrottledEventArgs eventArgs);`
    - to raise/invoke `OnMessageThrottled`
    - should not be done from outside
- `void WhisperThrottled(OnWhisperThrottledEventArgs eventArgs);`
    - to raise/invoke `OnWhisperThrottled`
    - should not be done from outside

#### changed
- `void Close()`
    - Parameter `bool callDisconnect = true` removed
    - its not needed anymore

#### added
- `bool SendPONG()`
    - whenever a PONG-Message has to be sent, use this method
### IClientOptions and ClientOptions
In general, all values can only be passed via `ctor` and can not be changed afterwards.

#### changed
- `ReconnectionPolicy ReconnectionPolicy { get; set; } = new ReconnectionPolicy(3000, maxAttempts: 10);`
    - `ReconnectionPolicy ReconnectionPolicy { get; }`
    - removed `set`, `value` can only be passed via ctor. to make the API more robust
- `bool UseSsl { get; set; } = true;`
    - `bool UseSsl { get; }`
    - removed `set`, `value` can only be passed via ctor. to make the API more robust
- `int DisconnectWait { get; set; } = 20000;`
    - `uint DisconnectWait { get; }`
    - removed `set`, `value` can only be passed via ctor. to make the API more robust
- `ClientType ClientType { get; set; } = ClientType.Chat;`
    - `ClientType ClientType { get; }`
    - removed `set`, `value` can only be passed via ctor. to make the API more robust
- `ushort SendDelay { get; set; } = 50;`
    - `ushort SendDelay { get; }`
    - removed `set`, `value` can only be passed via ctor. to make the API more robust
- `TimeSpan ThrottlingPeriod { get; set; } = TimeSpan.FromSeconds(30);`
    - `TimeSpan ThrottlingPeriod { get; } = TimeSpan.FromSeconds(30);`
    - removed `set`, `value` is fixed to 30 seconds
    - applies to both, Messages and Whispers
    - please take a look at
        - the new `enum`s `MessageRateLimit` and `WhisperRateLimit`
        - https://dev.twitch.tv/docs/irc/#rate-limits
        - https://discuss.dev.twitch.tv/t/whisper-rate-limiting/2836
#### added
- `ISendOptions MessageSendOptions { get; }`
    - `value` can only be passed via ctor. to make the API more robust
    - some Options are now bundled within `ISendOptions`
- `ISendOptions WhisperSendOptions { get; }`
    - `value` can only be passed via ctor. to make the API more robust
    - some Options are now bundled within `ISendOptions`
#### moved
the following options went to `ISendOptions`
- `TimeSpan SendCacheItemTimeout { get; set; } = TimeSpan.FromMinutes(30);`
    - as `TimeSpan CacheItemTimeout { get; }`
- `int SendQueueCapacity { get; set; } = 10000;`
    - `uint QueueCapacity { get; }`
- `int MessagesAllowedInPeriod { get; set; } = 100;`
    - as `uint SendsAllowedInPeriod { get; }`
#### removed
the following options went to `ISendOptions`
- `int WhisperQueueCapacity { get; set; } = 10000;`
    - `uint QueueCapacity { get; }`
- `int WhispersAllowedInPeriod { get; set; } = 100;`
    - as `uint SendsAllowedInPeriod { get; }`
- `TimeSpan WhisperThrottlingPeriod { get; set; } = TimeSpan.FromSeconds(60);`
    - there is only one throttling period
        - `TimeSpan ThrottlingPeriod { get; set; } = TimeSpan.FromSeconds(30);`

### MessageRateLimit
An `enum`, that provides `uint` constants that can/should be used for `ISendOptions.SendsAllowedInPeriod` that are passed to `IClientOptions` for `IClientOptions.MessageSendOptions`
According to https://dev.twitch.tv/docs/irc/#rate-limits , it provides three values
- `Limit_20_in_30_Seconds` with the `uint`-`value` 20
- `Limit_100_in_30_Seconds` with the `uint`-`value` 100
- `Limit_7500_in_30_Seconds` with the `uint`-`value` 7_500

### WhisperRateLimit
An `enum`, that provides a single `uint` constant that can/should be used for `ISendOptions.SendsAllowedInPeriod` that are passed to `IClientOptions` for `IClientOptions.WhisperSendOptions`
According to https://discuss.dev.twitch.tv/t/whisper-rate-limiting/2836 , it provides the following value
- `Limit_100_in_60_Seconds` with the `uint`-`value` 50
    - yeah, thats right, 50
    - because of some `internals`, `IClientOptions.ThrottlingPeriod` has a fixed `value` of 30; that's why this constants `value` is 50

### ISendOptions and SendOptions
This `interface` and its implementation/realization are newly introduced and hold moved/removed properties from `IClientOptions`
- `uint SendsAllowedInPeriod { get; }`
- `TimeSpan CacheItemTimeout { get; }`
- `uint QueueCapacity { get; }`


## internal
The client acts a bit more synchronous.
### WebSocketClient and TcpClient
Almost everything that both clients have in common went to an `abstract` `class` called `ABaseClient`.

`WebSocketClient` and `TcpClient` inherit from `ABaseClient` and realise the following three methods:

- `internal override void SendIRC(string message)`
    - this is the 'real'-send
    - take care: this method is not `Thread`-safe!
    - please take a look at (also documented/mentioned inline)

            https://stackoverflow.com/a/59619916
            links from within this thread:
            the 4th point: https://www.codetinkerer.com/2018/06/05/aspnet-core-websockets.html
            https://github.com/dotnet/corefx/blob/d6b11250b5113664dd3701c25bdf9addfacae9cc/src/Common/src/System/Net/WebSockets/ManagedWebSocket.cs#L22-L28

            https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream?view=netstandard-2.0#remarks

    - to avoid manual locks, etsy., the (now) `ThrottlerService` (formerly) `Throttlers` has changed a lot
- `internal override void ListenTaskAction()`
    - provides the `Action` for one single `Task` that listens to new IRC-Messages within `NetworkServices`
- `protected override void SpecificClientConnect()`
    - establishes the connection related to the specific underlying `System.Net.Sockets.TcpClient`/`System.Net.WebSockets.ClientWebSocket`

### NetworkServices
Holds and controls
- the `ListenTask` with the `Action` provided by `WebSocketClient`/`TcpClient`
- the `MonitorTask` with the `Task` that is returned from `ConnectionWatchDog.StartMonitorTask()`
- the `ThrottlerService`

### ConnectionWatchDog
`ConnectionWatchDog` has only two missions (tasks):
- watch for the connection-state each 200 milliseconds
- initiate `ReconnectInternal()` whenever the client is not connected anymore
- ok, it also barks. formally it raises `IClient.OnStateChanged`

### ThrottlerService (formerly Throttlers)
Previously there were four `Task`s.

Two to reset throttling-windows of messages and whispers and two to send messages and whispers.

As mentioned above, having one `Thread` listening for IRC-Messages and one `Thread` sending IRC-Messages, is fine for the `System.Net.Sockets.TcpClient`/`System.Net.WebSockets.ClientWebSocket`.

Having several `Thread`s sending IRC-Messages, ahhhh, that could cause interferences...

To only have one `Thread`/`Task` that sends IRC-Messages a couple of changes have been made.

The `ThrottlerService` now holds one single `Timer` that fires each 30 seconds and resets the throttling-windows.
- Thats the secret behind the confusion within the `enum` `WhisperRateLimit` and its constants `value` of `50`

A new `enum` named `MessageType` is introduced.

It discriminates/differentiates the following three types of messages:
- `ByPass`
- `Message`
- `Whisper`

`BlockingCollection` is replaced by `ConcurrentQueue`. `ConcurrentQueue` is fast and it returns `null`, if there is no item in the queue.

Now the `ThrottlerService` holds two `IDictionary`s
- `IDictionary<MessageType, ISendOptions> Options`
- `IDictionary<MessageType, ConcurrentQueue<Tuple<DateTime, string>>>`

Now there is only one `Task` that is responsible for sending IRC-Messages.

That `Task` loops through the `enum` `MessageType`s `values`.
For each `value` it takes the `ISendOptions`, the respective `ConcurrentQueue` and the next item from that queue.

So, within one roundtrip, each `MessageType` gets its chance to be sent, starting with `MessageType.ByPass`.

## Issues
### Known issues
#### ReconnectionPolicy
- `ReconnectionPolicy` has to consider the case that no reconnect is desired

### Fixed issues
#### SendOptions
- `value` of zero for `ISendOptions.SendsAllowedInPeriod`
