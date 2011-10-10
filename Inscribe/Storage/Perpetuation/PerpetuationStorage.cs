using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using Inscribe.Common;
using Inscribe.Configuration;
using System.Threading;

namespace Inscribe.Storage.Perpetuation
{
    /// <summary>
    /// SQL Server Compact によって永続化されるストレージ サービス
    /// </summary>
    internal static class PerpetuationStorage
    {
        static object dblock = new object();

        static PerpetuationDatabase database;

        internal static void ConnectDB()
        {
            ThreadHelper.Halt += () => DisconnectDB();
            lock (dblock)
            {
                Database.DefaultConnectionFactory = new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0");
                // Initialize Database
                if (Setting.Instance.KernelProperty.TweetPerpetuation)
                    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<PerpetuationDatabase>()); // Modelが変化したら破棄
                else
                    Database.SetInitializer(new DropCreateDatabaseAlways<PerpetuationDatabase>()); // 常に破棄
                var path = Path.Combine(Path.GetDirectoryName(Define.ExeFilePath), Define.DatabaseFileName);
                database = new PerpetuationDatabase(path);
            }
        }

        internal static bool AddTweetBackend(TweetBackend tbe)
        {
            lock (dblock)
            {
                if (database != null)
                {
                    database.TweetSet.Add(tbe);
                    // database.SaveChanges();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        internal static TweetBackend GetTweetBackend(long id)
        {
            lock (dblock)
            {
                if (database != null)
                    return database.TweetSet.Where(b => b.Id == id).FirstOrDefault();
                else
                    return null;
            }
        }

        internal static bool AddUserBackend(UserBackend ube)
        {
            lock (dblock)
            {
                if (database != null)
                {
                    database.UserSet.Add(ube);
                    // database.SaveChanges();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        internal static UserBackend GetUserBackend(long id)
        {
            lock (dblock)
            {
                if (database != null)
                    return database.UserSet.Where(b => b.Id == id).FirstOrDefault();
                else
                    return null;
            }
        }

        static int saving = 0;
        internal static void SaveChange()
        {
            /*
            if (Interlocked.Exchange(ref saving, 1) == 1) return;
            try
            {
                lock (dblock)
                {
                    if (database != null)
                        database.SaveChanges();
                }
            }
            finally
            {
                saving = 0;
            }
            */
        }

        internal static void DisconnectDB()
        {
            lock (dblock)
            {
                database.SaveChanges();
                database.Dispose();
                database = null;
            }
        }

        public static void EnterLockWhenInitialized(Action doInLock)
        {
            lock (dblock)
            {
                if (database != null)
                    doInLock();
            }
        }

        public static T EnterLockWhenInitialized<T>(Func<T> doInLock)
        {
            lock (dblock)
            {
                if (database == null)
                    return default(T);
                else
                    return doInLock();
            }
        }

        public static IQueryable<TweetBackend> Tweets
        {
            get
            {
                if (database == null)
                    return null;
                else
                    return database.TweetSet;
            }
        }

        public static IQueryable<UserBackend> Users
        {
            get
            {
                if (database == null)
                    return null;
                else
                    return database.UserSet;
            }
        }

        /// <summary>
        /// TweetStorageが本格的に稼働する前にコールしてください。
        /// </summary>
        internal static void Writeback()
        {
            TweetBackend[] tbes;
            UserBackend[] ubes;
            // データベースのトリム
            lock (dblock)
            {
                if (database == null)
                    throw new InvalidOperationException();
                tbes = database.TweetSet
                    .OrderByDescending(be => be.CreatedAt)
                    .Take(Setting.Instance.KernelProperty.TweetPerpetuationMaxCount)
                    .ToArray();
                database.TweetSet
                    .OrderByDescending(be => be.CreatedAt)
                    .Skip(Setting.Instance.KernelProperty.TweetPerpetuationMaxCount)
                    .ToArray()
                    .ForEach(be => database.TweetSet.Remove(be));
                ubes = database.UserSet.Join(
                    database.TweetSet.Select(tbe => tbe.UserId).Distinct(),
                    ube => ube.Id,
                    uid => uid,
                    (ub, id) => ub).ToArray();
            }
            tbes.ForEach(be =>
                TweetStorage.WritebackFromDb(be));
            ubes.ForEach(be =>
                UserStorage.WritebackFromDb(be));
        }
    }
}
