using System;

namespace TwitchLib.Communication.Models
{
    public class ReconnectionPolicy
    {
        private int _currentAttempt;
        private TimeSpan maxTimeout;

        public ReconnectionPolicy(TimeSpan? maxTimeout = null)
        {
            this.maxTimeout = maxTimeout ?? TimeSpan.FromMinutes(5);
        }

        public TimeSpan GetTimeout()
        {
            var timeout = TimeSpan.FromSeconds(Math.Pow(2, _currentAttempt));

            _currentAttempt++;

            return timeout.CompareTo(maxTimeout) == -1 ? timeout : maxTimeout;
        }

        public int GetAttempt()
        {
            return _currentAttempt;
        }

        public void Reset()
        {
            _currentAttempt = 0;
        }
    }
}