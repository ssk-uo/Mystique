using System;
using System.Linq;
using System.Threading;
using Inscribe.Storage;
using Livet;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Core
{
    /// <summary>
    /// Statistics Service
    /// </summary>
    public static class Statistics
    {
        private static readonly DateTime wakeupTimeStamp = DateTime.Now;

        static Timer tweetSpeedUpdate;
        static Timer wakeUpTimeUpdate;

        /// <summary>
        /// 初期化時アクセス用
        /// </summary>
        public static void Initialize() { }

        static Statistics()
        {
            tweetSpeedUpdate = new Timer(TweetSpeedUpdateSink, null, 0, 1000 * 30);
            wakeUpTimeUpdate = new Timer(WakeupTimeUpdateSink, null, 0, 1000);
        }

        private static bool _isUpdatingSpeed = false;
        private static void TweetSpeedUpdateSink(object o)
        {
            if (_isUpdatingSpeed) return;
            _isUpdatingSpeed = true;
            // 鳥人
            var tweettimes= PerpetuationStorage.EnterLockWhenInitialized(() => PerpetuationStorage.Tweets.Select(b => b.CreatedAt).ToArray());
            var morigin = (DateTime.Now - new TimeSpan(0, 1, 0));
            TweetSpeedPerMin = tweettimes.Count(t => t > morigin);
            var horigin = (DateTime.Now - new TimeSpan(1, 0, 0));
            TweetSpeedPerHour = tweettimes.Count(t => t > horigin);
            _isUpdatingSpeed = false;
            OnTweetSpeedUpdated(EventArgs.Empty);
        }

        private static void WakeupTimeUpdateSink(object o)
        {
            OnWakeupTimeUpdated(EventArgs.Empty);
        }

        #region TweetSpeedUpdatedイベント

        public static event EventHandler<EventArgs> TweetSpeedUpdated;
        private static Notificator<EventArgs> _TweetSpeedUpdatedEvent;
        public static Notificator<EventArgs> TweetSpeedUpdatedEvent
        {
            get
            {
                if (_TweetSpeedUpdatedEvent == null) _TweetSpeedUpdatedEvent = new Notificator<EventArgs>();
                return _TweetSpeedUpdatedEvent;
            }
            set { _TweetSpeedUpdatedEvent = value; }
        }

        private static void OnTweetSpeedUpdated(EventArgs e)
        {
            var threadSafeHandler = Interlocked.CompareExchange(ref TweetSpeedUpdated, null, null);
            if (threadSafeHandler != null) threadSafeHandler(null, e);
            TweetSpeedUpdatedEvent.Raise(e);
        }

        #endregion

        #region WakeupTimeUpdatedイベント

        public static event EventHandler<EventArgs> WakeupTimeUpdated;
        private static Notificator<EventArgs> _WakeupTimeUpdatedEvent;
        public static Notificator<EventArgs> WakeupTimeUpdatedEvent
        {
            get
            {
                if (_WakeupTimeUpdatedEvent == null) _WakeupTimeUpdatedEvent = new Notificator<EventArgs>();
                return _WakeupTimeUpdatedEvent;
            }
            set { _WakeupTimeUpdatedEvent = value; }
        }

        private static void OnWakeupTimeUpdated(EventArgs e)
        {
            var threadSafeHandler = Interlocked.CompareExchange(ref WakeupTimeUpdated, null, null);
            if (threadSafeHandler != null) threadSafeHandler(null, e);
            WakeupTimeUpdatedEvent.Raise(e);
        }

        #endregion

        public static int TweetSpeedPerMin { get; private set; }

        public static int TweetSpeedPerHour { get; private set; }

        public static string WakeupTime
        {
            get
            {
                var wakeup = DateTime.Now.Subtract(wakeupTimeStamp);
                return Math.Floor(wakeup.TotalHours).ToString() + ":" +
                    wakeup.Minutes.ToString("00") + ":" +
                    wakeup.Seconds.ToString("00");
            }
        }
    }
}
