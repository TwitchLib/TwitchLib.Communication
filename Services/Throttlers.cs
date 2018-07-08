using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchLib.Communication.Services
{
    public class Throttlers
    {
        public readonly BlockingCollection<Tuple<DateTime, string>> SendQueue = new BlockingCollection<Tuple<DateTime, string>>();
        public readonly BlockingCollection<Tuple<DateTime, string>> WhisperQueue = new BlockingCollection<Tuple<DateTime, string>>();
        public bool Reconnecting { get; set; } = false;
        public bool ShouldDispose { get; set; } = false;
        public bool ResetThrottlerRunning;
        public bool ResetWhisperThrottlerRunning;
        
        private readonly TimeSpan _throttlingPeriod;
        private readonly TimeSpan _whisperThrottlingPeriod;
        public int SentCount = 0;
        public int WhispersSent = 0;
        public Task ResetThrottler;
        public Task ResetWhisperThrottler;

        public Throttlers(TimeSpan throttlingPeriod, TimeSpan whisperThrottlingPeriod)
        {
            _throttlingPeriod = throttlingPeriod;
            _whisperThrottlingPeriod = whisperThrottlingPeriod;
        }

        public void StartThrottlingWindowReset()
        {
            ResetThrottler = Task.Run(async () => {
                ResetThrottlerRunning = true;
                while (!ShouldDispose && !Reconnecting)
                {
                    Interlocked.Exchange(ref SentCount, 0);
                    await Task.Delay(_throttlingPeriod);
                }
                ResetThrottlerRunning = false;
                return Task.CompletedTask;
            });
        }

        public void StartWhisperThrottlingWindowReset()
        {
            ResetWhisperThrottler = Task.Run(async () => {
                ResetWhisperThrottlerRunning = true;
                while (!ShouldDispose && !Reconnecting)
                {
                    Interlocked.Exchange(ref WhispersSent, 0);
                    await Task.Delay(_whisperThrottlingPeriod);
                }
                ResetWhisperThrottlerRunning = false;
                return Task.CompletedTask;
            });
        }

        public void IncrementSentCount()
        {
            Interlocked.Increment(ref SentCount);
        }

        public void IncrementWhisperCount()
        {
            Interlocked.Increment(ref WhispersSent);
        }
    }
}
