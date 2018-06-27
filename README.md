<p align="center"> 
<img src="http://swiftyspiffy.com/img/twitchlib.png" style="max-height: 300px;">
</p>



</p>

## About 
TwitchLib is a powerful C# library that allows for interaction with various Twitch services. Currently supported services are: chat and whisper, API's (v3, v5, helix, undocumented, and third party), and PubSub event system. Below are the descriptions of the core components that make up TwitchLib.

* **TwitchClient**: Handles chat and whisper Twitch services. Complete with a suite of events that fire for virtually every piece of data received from Twitch. Helper methods also exist for replying to whispers or fetching moderator lists.
* **TwitchAPI**: Complete coverage of v3, v5, and Helix endpoints. The API is now a singleton class. This class allows fetching all publically accessable data as well as modify Twitch services like profiles and streams.
* **TwitchPubSub**: Supports all documented Twitch PubSub topics as well as a few undocumented ones.

## Implementing