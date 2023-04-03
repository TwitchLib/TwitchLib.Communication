# Changelog

## Version 2.0.0
### Addresses
##### Issues
- https://github.com/TwitchLib/TwitchLib/issues/1093
- https://github.com/TwitchLib/TwitchLib.Client/issues/206
- https://github.com/TwitchLib/TwitchLib/issues/1104
- https://github.com/TwitchLib/TwitchLib.Communication/issues/13
- https://github.com/TwitchLib/TwitchLib.Communication/issues/7

##### Pull Requests
- none

---

### Changes

---

#### IClient
##### Changed
- now extends `IDisposable`
- `event EventHandler<OnReconnectedEventArgs> OnReconnected;`
    - to `event EventHandler<OnConnectedEventArgs> OnReconnected;`
    - now the `event`handlers argument is `OnConnectedEventArgs` instead of `OnReconnectedEventArgs`
    - the specific `event`handler itself, determines wether the args are in context of connect or reconnect
- `IClient.Send(string message)` is now synchronized because
    - `ThrottlerService` got removed
    - https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream?view=netstandard-2.0#remarks
##### Added
- none
##### Removed
- see also: https://discuss.dev.twitch.tv/t/deprecation-of-chat-commands-through-irc/40486
    - `bool SendWhisper(string message);`
    - `void WhisperThrottled(OnWhisperThrottledEventArgs eventArgs);`
- `event EventHandler<OnDataEventArgs> OnData;`
    - as far as i got it right,
        - binary data is not received
        - it has never ever been used/raised
- `event EventHandler<OnMessageThrottledEventArgs> OnMessageThrottled;`
    - because `ThrottlerService` is now part of `TwitchLib.Client`
- `event EventHandler<OnStateChangedEventArgs> OnStateChanged;`
    - neither used by `TwitchLib.Client` nor by `TwitchLib.PubSub`
---

#### ClientOptions
##### Changed
- `value`s for properties can only be passed by `ctor`
- `ctor` also takes an argument for `ReconnectionPolicy`
    - by leaving it `null`, a `default` `ReconnectionPolicy` is created, that attempts to reconnect every 3_000 milliseconds for ten times
- `DisconnectWait` became an unsigned integer (`uint`), to ensure only positive values are used for it
##### Removed
- see also: https://discuss.dev.twitch.tv/t/deprecation-of-chat-commands-through-irc/40486
    - `TimeSpan WhisperThrottlingPeriod { get; set; }`
    - `int WhispersAllowedInPeriod { get; set; }`
    - `int WhisperQueueCapacity { get; set; }`
##### <span id="ClientOptions.Moved">Moved</span>
- the following properties went to `TwitchLib.Client.Models.SendOptions`
    - `int SendQueueCapacity { get; set; }`
    - `TimeSpan SendCacheItemTimeout { get; set; }`
    - `ushort SendDelay { get; set; }`
    - `TimeSpan ThrottlingPeriod { get; set; }`
    - `int MessagesAllowedInPeriod { get; set; }`

---

#### ConnectionWatchDog
- now the `ConnectionWatchDog` enforces reconnect according to the `ReconnectionPolicy`
- `ConnectionWatchDog` does not send `PING :tmi.twitch.tv`-messages anymore
    - `TwitchLib.Client` receives `PING :tmi.twitch.tv`-messages and has to reply with `PONG :tmi.twitch.tv`
        - https://dev.twitch.tv/docs/irc/#keepalive-messages
        - `TwitchLib.Client` does so
            - it handles received PING-messages
    - `TwitchLib.PubSub` has to send `PING :tmi.twitch.tv` within at least every five minutes
        - https://dev.twitch.tv/docs/pubsub/#connection-management
        - `TwitchLib.PubSub` does so
            - it has its own PING- and PONG-Timer

---

#### Throttling/ThrottlerService
- `TwitchLib.Communication.IClient` doesnt throttle messages anymore
    - `TwitchLib.PubSub` does not need it
    - only `TwitchLib.Client` needs it
        - so, throttling went to `TwitchLib.Client.Services.ThrottlerService` in combination with `TwitchLib.Client.Services.Throttler`
- everything related to throttling got removed
    - `TwitchLib.Communication.Events.OnMessageThrottledEventArgs`
    - `TwitchLib.Communication.Interfaces.IClientOptions`
        - see also [ClientOptions.Moved](#ClientOptions.Moved)
        - `int SendQueueCapacity { get; set; }`
        - `TimeSpan SendCacheItemTimeout { get; set; }`
        - `ushort SendDelay { get; set; }`
        - `TimeSpan ThrottlingPeriod { get; set; }`
        - `int MessagesAllowedInPeriod { get; set; }`

---

#### OnStateChangedEventArgs
- removed
- neither used by `TwitchLib.Client` nor by `TwitchLib.PubSub`