using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using Inscribe.Common;
using System;

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
                Database.SetInitializer(new DropCreateDatabaseAlways<PerpetuationDatabase>());
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
                    database.SaveChanges();
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
                    database.SaveChanges();
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

        internal static void SaveChange()
        {
            lock (dblock)
            {
                if (database != null)
                    database.SaveChanges();
            }
        }

        internal static void DisconnectDB()
        {
            lock (dblock)
            {
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
    }
}
