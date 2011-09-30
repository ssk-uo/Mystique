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
using System.Threading.Tasks;

namespace Inscribe.Storage.Perpetuation
{
    /// <summary>
    /// キャッシュと透過プロキシを併せ持つアレ
    /// </summary>
    internal static class TransparentProxy
    {
        private static object dblock = new object();

        private static SerializationContext context;

        private static object urlock = new object();

        private static Dictionary<long, WeakReference> userReference;

        private static object trLock = new object();

        private static Dictionary<long, WeakReference> tweetReference;

        private static QueueTaskDispatcher dbWriter;

        internal static void Initialize()
        {
            ThreadHelper.Halt += () =>
            {
                CloseDB();
                dbWriter.Dispose();
            };
            dbWriter = new QueueTaskDispatcher(1);
            lock (urlock)
            {
                userReference = new Dictionary<long, WeakReference>();
            }
            lock (trLock)
            {
                tweetReference = new Dictionary<long, WeakReference>();
            }
            lock (dblock)
            {
                Perpetuation.DefaultConnectionFactory =
                    new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0");
                // Database.SetInitializer(new DropCreateDatabaseIfModelChanges<SerializationContext>());
                Perpetuation.SetInitializer(new DropCreateDatabaseAlways<SerializationContext>());
                var path = Path.Combine(Path.GetDirectoryName(Define.ExeFilePath), Define.TweetDatabaseFileName);
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
            dbWriter.Enqueue(() =>
                {
                    lock (dblock)
                    {
                        bool addNew = false;
                        lock (trLock)
                        {
                            // register tweet data
                            if (!tweetReference.ContainsKey(tvm.Status.Id))
                            {
                                tweetReference.Add(tvm.Status.Id, new WeakReference(tvm));
                                addNew = true;
                            }
                            else
                            {
                                tweetReference[tvm.Status.Id] = new WeakReference(tvm);
                                addNew = false;
                            }
                        }
                        if(addNew)
                        {
                            context.Tweets.Add(new TweetBackEnd(tvm));
                        }
                        else
                        {
                            var registered = context.Tweets.FirstOrDefault(i => i.Id == tvm.Status.Id);
                            registered.UpdateDynamicData(tvm);
                        }
                        context.SaveChanges();
                    }
                });
        }

        internal static void UpdateUserData(TwitterUser user)
        {
            bool add = false;
            // update user
            lock (urlock)
            {
                if (!userReference.ContainsKey(user.NumericId))
                {
                    userReference.Add(user.NumericId, new WeakReference(user));
                    add = true;
                }
            }
            if (add)
            {
                // register new user for DB
                dbWriter.Enqueue(() =>
                {
                    lock (dblock)
                    {
                        context.Users.Add(new UserBackEnd(user));
                        context.SaveChanges();
                    }
                });
            }
            else
            {
                // check modification of user
                UserBackEnd registered;
                lock (dblock)
                {
                    registered = context.Users.FirstOrDefault(i => i.Id == user.NumericId);
                }
                if (registered == null) return;
                if (registered.CreatedTimestamp.Subtract(user.CreatedTimestamp)
                    .TotalMilliseconds < 0)
                {
                    lock (urlock)
                    {
                        userReference[user.NumericId] = new WeakReference(user);
                    }
                    dbWriter.Enqueue(() =>
                    {
                        lock (dblock)
                        {
                            registered.Overwrite(user);
                            context.SaveChanges();
                        }
                    });
                }
            }
        }

        internal static IEnumerable<TweetViewModel> GetAllTweets()
        {
            long[] ids = null;
            lock (trLock)
            {
                ids = tweetReference.Keys.ToArray();
            }
            return ids.Select(i => GetTweetViewModel(i)).Where(vm => vm != null);
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
            if ((retval = (TweetViewModel)wref.Target) != null)
            {
                // 参照を保持してる
                return retval;
            }
            else
            {
                // GCされたからもう一度取りにいこうよ！
                TweetBackEnd st;
                lock (dblock)
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
            }
            if ((retval = wref.Target as TwitterUser) != null)
            {
                // 強い参照が生きてる
                return retval;
            }
            else
            {
                UserBackEnd su;
                lock (dblock)
                {
                    su = context.Users.FirstOrDefault(i => i.Id == id);
                }
                retval = su.Rebirth();
                lock (urlock)
                {
                    userReference[id] = new WeakReference(retval);
                }
                return retval;
            }
        }

        internal static void CloseDB()
        {
            lock (dblock)
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
            dbWriter.Enqueue(() =>
                {
                    lock (dblock)
                    {
                        var tweet = context.Tweets.FirstOrDefault(t => t.Id == id);
                        context.Tweets.Remove(tweet);
                        context.SaveChanges();
                    }
                });
        }
    }

    public class SerializationContext : DbContext
    {
        public SerializationContext(string cstr) : base(cstr) { }

        public IDbSet<UserBackEnd> Users { get; set; }

        public IDbSet<TweetBackEnd> Tweets { get; set; }
    }
}
