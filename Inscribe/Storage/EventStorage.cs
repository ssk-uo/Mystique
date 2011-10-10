using System;
using System.Collections.Generic;
using System.Threading;
using Inscribe.Data;
using Inscribe.ViewModels.PartBlocks.MainBlock;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;
using Livet;
using Inscribe.Configuration;

namespace Inscribe.Storage
{
    public static class EventStorage
    {
        static DateTime wakeupTime = DateTime.Now;

        private static SafeLinkedList<EventDescription> events = new SafeLinkedList<EventDescription>();

        public static IEnumerable<EventDescription> Events
        {
            get { return events; }
        }

        public static void Remove(EventDescription evd)
        {
            events.Remove(evd);
            OnEventChanged(EventArgs.Empty);
        }

        private static void Register(EventDescription description)
        {
            if (description.TargetTweet != null &&
                Setting.Instance.TimelineFilteringProperty.MuteFilterCluster != null &&
                Setting.Instance.TimelineFilteringProperty.MuteFilterCluster.Filter(description.TargetTweet.Backend))
                return;
            events.AddLast(description);
            OnEventChanged(EventArgs.Empty);
            OnEventRegistered(new EventDescriptionEventArgs(description));
        }

        #region EventRegisteredイベント

        public static event EventHandler<EventDescriptionEventArgs> EventRegistered;
        private static Notificator<EventDescriptionEventArgs> _EventRegisteredEvent;
        public static Notificator<EventDescriptionEventArgs> EventRegisteredEvent
        {
            get
            {
                if (_EventRegisteredEvent == null) _EventRegisteredEvent = new Notificator<EventDescriptionEventArgs>();
                return _EventRegisteredEvent;
            }
            set { _EventRegisteredEvent = value; }
        }

        private static void OnEventRegistered(EventDescriptionEventArgs e)
        {
            var threadSafeHandler = Interlocked.CompareExchange(ref EventRegistered, null, null);
            if (threadSafeHandler != null) threadSafeHandler(null, e);
            EventRegisteredEvent.Raise(e);
        }

        #endregion
      
        #region EventChangedイベント

        public static event EventHandler<EventArgs> EventChanged;
        private static Notificator<EventArgs> _EventChangedEvent;
        public static Notificator<EventArgs> EventChangedEvent
        {
            get
            {
                if (_EventChangedEvent == null) _EventChangedEvent = new Notificator<EventArgs>();
                return _EventChangedEvent;
            }
            set { _EventChangedEvent = value; }
        }

        private static void OnEventChanged(EventArgs e)
        {
            var threadSafeHandler = Interlocked.CompareExchange(ref EventChanged, null, null);
            if (threadSafeHandler != null) threadSafeHandler(null, e);
            EventChangedEvent.Raise(e);
        }

        #endregion

        public static void OnRetweeted(TweetViewModel tweet, UserViewModel retweeter)
        {
            if (AccountStorage.ContainsId(retweeter.BindingId) || tweet.CreatedAt < wakeupTime)
                return;
            Register(new EventDescription(EventKind.Retweet, retweeter,
                    UserStorage.Lookup(tweet.Backend.UserId), tweet));
        }

        public static void OnFavored(TweetViewModel tweet, UserViewModel favorer)
        {
            if (AccountStorage.ContainsId(favorer.BindingId))
                return;
            Register(new EventDescription(EventKind.Favorite, favorer,
                 UserStorage.Lookup(tweet.Backend.UserId), tweet));
        }

        public static void OnUnfavored(TweetViewModel tweet, UserViewModel favorer)
        {
            if (AccountStorage.ContainsId(favorer.BindingId))
                return;
            Register(new EventDescription(EventKind.Unfavorite,
                favorer, UserStorage.Lookup(tweet.Backend.UserId), tweet));
        }

        public static void OnFollowed(UserViewModel fromUser, UserViewModel toUser)
        {
            Register(new EventDescription(EventKind.Follow,
                fromUser, toUser));
        }

        public static void OnRemoved(UserViewModel fromUser, UserViewModel toUser)
        {
            Register(new EventDescription(EventKind.Unfollow,
                fromUser, toUser));
        }

        public static void OnMention(TweetViewModel tweet)
        {
            if (AccountStorage.ContainsId(tweet.Backend.UserId) || tweet.CreatedAt < wakeupTime)
                return;
            Register(new EventDescription(EventKind.Mention,
                UserStorage.Lookup(tweet.Backend.UserId), null, tweet));
        }

        public static void OnDirectMessage(TweetViewModel tweet)
        {
            if (AccountStorage.ContainsId(tweet.Backend.UserId) || tweet.CreatedAt < wakeupTime)
                return;
            Register(new EventDescription(EventKind.DirectMessage,
                UserStorage.Lookup(tweet.Backend.UserId), null, tweet));
        }
    }

    public class EventDescription
    {
        public EventKind Kind { get; private set; }

        public UserViewModel SourceUser { get; private set; }

        public UserViewModel TargetUser { get; private set; }

        public TweetViewModel TargetTweet { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public EventDescription(EventKind kind, UserViewModel source, UserViewModel target, TweetViewModel tweet = null)
        {
            this.CreatedAt = DateTime.Now;
            this.Kind = kind;
            this.SourceUser = source;
            this.TargetUser = target;
            this.TargetTweet = tweet;
        }
    }

    public enum EventKind
    {
        Retweet,
        Favorite,
        Unfavorite,
        Follow,
        Unfollow,
        Mention,
        DirectMessage,
        Undefined
    }

    public class EventDescriptionEventArgs : EventArgs
    {
        public EventDescription EventDescription { get; private set; }
        public EventDescriptionEventArgs(EventDescription desc)
        {
            this.EventDescription = desc;
        }
    }
}
