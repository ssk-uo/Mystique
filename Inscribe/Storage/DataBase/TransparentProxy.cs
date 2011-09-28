using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using Dulcet.Twitter;
using Inscribe.Common;
using Inscribe.Data;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;

namespace Inscribe.Storage.DataBase
{
    /// <summary>
    /// キャッシュと透過プロキシを併せ持つアレ
    /// </summary>
    internal static class TransparentProxy
    {
        private static object dblock = new object();

        private static ReaderWriterLockWrap rwlock = new ReaderWriterLockWrap(System.Threading.LockRecursionPolicy.NoRecursion);

        private static SerializationContext context;

        private static object urlock = new object();

        private static Dictionary<long, WeakReference> userReference;

        private static object trLock = new object();

        private static Dictionary<long, WeakReference> tweetReference;

        internal static void Initialize()
        {
            ThreadHelper.Halt += () => CloseDB();
            lock (urlock)
            {
                userReference = new Dictionary<long, WeakReference>();
            }
            lock (trLock)
            {
                tweetReference = new Dictionary<long, WeakReference>();
            }
            using (rwlock.GetWriterLock())
            {
                Database.DefaultConnectionFactory =
                    new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0");
                // Database.SetInitializer(new DropCreateDatabaseIfModelChanges<SerializationContext>());
                Database.SetInitializer(new DropCreateDatabaseAlways<SerializationContext>());
                var path = Path.Combine(Path.GetDirectoryName(Define.ExeFilePath), Define.DatabaseFileName);
                context = new SerializationContext(path);
                // write-back on memory cache
                context.Tweets.Select(t => t.Id).Distinct().ForEach(id => tweetReference.Add(id, new WeakReference(null)));
                context.Users.Select(u => u.Id).Distinct().ForEach(id => userReference.Add(id, new WeakReference(null)));
                context.SaveChanges();
            }
        }

        internal static bool ContainsTweet(long id)
        {
            lock (trLock)
            {
                return tweetReference.ContainsKey(id);
            }
        }

        internal static bool ContainsUserData(long id)
        {
            lock (urlock)
            {
                return userReference.ContainsKey(id);
            }
        }

        internal static void UpdateTweetData(TweetViewModel tvm)
        {
            UpdateUserData(tvm.Status.User);
            using (rwlock.GetWriterLock())
            {
                lock (trLock)
                {
                    // register tweet data
                    if (!tweetReference.ContainsKey(tvm.Status.Id))
                    {
                        context.Tweets.Add(new SerializedTweetData(tvm));
                        tweetReference.Add(tvm.Status.Id, new WeakReference(tvm));
                    }
                    else
                    {
                        var registered = context.Tweets.FirstOrDefault(i => i.Id == tvm.Status.Id);
                        registered.UpdateDynamicData(tvm);
                        tweetReference[tvm.Status.Id] = new WeakReference(tvm);
                    }
                }
                context.SaveChanges();
            }
        }

        internal static void UpdateUserData(TwitterUser user)
        {
            // update user
            lock (urlock)
            {
                if (!userReference.ContainsKey(user.NumericId))
                {
                    userReference.Add(user.NumericId, new WeakReference(user));
                    using (rwlock.GetWriterLock())
                    {
                        context.Users.Add(new SerializedUserData(user));
                        context.SaveChanges();
                    }
                }
                else
                {
                    using (rwlock.GetWriterLock())
                    {
                        var users = context.Users.Select(i => i.Id).ToArray();
                        var registered = context.Users.FirstOrDefault(i => i.Id == user.NumericId);
                        if (registered.CreatedTimestamp.Subtract(user.CreatedTimestamp)
                            .TotalMilliseconds < 0)
                        {
                            registered.Overwrite(user);
                            userReference[user.NumericId] = new WeakReference(user);
                            context.SaveChanges();
                        }
                    }
                }
            }
        }

        internal static TweetViewModel[] GetAllTweets()
        {
            long[] ids;
            lock (trLock)
            {
                ids = tweetReference.Keys.ToArray();
            }
            return ids.Select(i => GetTweetViewModel(i)).Where(vm => vm != null).ToArray();
        }

        internal static TweetViewModel GetTweetViewModel(long id)
        {
            WeakReference wref;
            TweetViewModel retval = null;
            lock (trLock)
            {
                if (!tweetReference.TryGetValue(id, out wref))
                {
                    // IDすら存在しないので未登録
                    return null;
                }
            }
            if ((retval = wref.Target as TweetViewModel) != null)
            {
                // 参照を保持してる
                return retval;
            }
            else
            {
                // GCされたからもう一度取りにいこうよ！
                SerializedTweetData st;
                using (rwlock.GetWriterLock())
                {
                    st = context.Tweets.FirstOrDefault(i => i.Id == id);
                }
                // lockしてるときはrebirth不可能
                retval = st.Rebirth();
                lock (trLock)
                {
                    tweetReference[id] = new WeakReference(retval);
                }
                return retval;
            }
        }

        internal static bool TryGetTweetViewModel(long id, out TweetViewModel data)
        {
            data = GetTweetViewModel(id);
            return data != null;
        }

        internal static TwitterUser GetUser(long id)
        {
            WeakReference wref;
            TwitterUser retval = null;
            lock (urlock)
            {
                if (!userReference.TryGetValue(id, out wref))
                    return null;
                if ((retval = wref.Target as TwitterUser) != null)
                {
                    // 強い参照が生きてる
                    return retval;
                }
                else
                {
                    SerializedUserData su;
                    using (rwlock.GetWriterLock())
                    {
                        su = context.Users.FirstOrDefault(i => i.Id == id);
                    }
                    retval = su.Rebirth();
                    userReference[id] = new WeakReference(retval);
                    return retval;
                }
            }
        }

        internal static void CloseDB()
        {
            using (rwlock.GetWriterLock())
            {
                context.Dispose();
            }
        }

        internal static int TweetsCount
        {
            get
            {
                lock (trLock)
                {
                    return tweetReference.Count;
                }
            }
        }

        internal static int UsersCount
        {
            get
            {
                lock (urlock)
                {
                    return userReference.Count;
                }
            }
        }

        internal static void RemoveTweet(long id)
        {
            lock (trLock)
            {
                tweetReference.Remove(id);
            }
            using (rwlock.GetWriterLock())
            {
                var tweet = context.Tweets.FirstOrDefault(t => t.Id == id);
                context.Tweets.Remove(tweet);
                context.SaveChanges();
            }
        }
    }

    public class SerializationContext : DbContext
    {
        public SerializationContext(string cstr) : base(cstr) { }

        public IDbSet<SerializedUserData> Users { get; set; }

        public IDbSet<SerializedTweetData> Tweets { get; set; }
    }
}
