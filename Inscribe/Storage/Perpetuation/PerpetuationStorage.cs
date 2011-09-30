using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using Inscribe.Common;

namespace Inscribe.Storage.Perpetuation
{
    /// <summary>
    /// SQL Server Compact によって永続化されるストレージ サービス
    /// </summary>
    internal static class PerpetuationStorage
    {
        static object tdblock = new object();

        static TweetDatabase tdatabase;

        static object udblock = new object();

        static UserDatabase udatabase;

        internal static void ConnectDB()
        {
            ThreadHelper.Halt += () => DisconnectDB();
            lock (tdblock)
            {
                lock (udblock)
                {
                    Database.DefaultConnectionFactory = new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0");

                    // Initialize User Database
                    Database.SetInitializer(new DropCreateDatabaseAlways<UserDatabase>());
                    var upath = Path.Combine(Path.GetDirectoryName(Define.ExeFilePath), Define.UserDatabaseFileName);
                    udatabase = new UserDatabase(upath);

                    // Initialize Tweet Database
                    Database.SetInitializer(new DropCreateDatabaseAlways<TweetDatabase>());
                    var tpath = Path.Combine(Path.GetDirectoryName(Define.ExeFilePath), Define.TweetDatabaseFileName);
                    tdatabase = new TweetDatabase(tpath);
                }
            }
        }

        internal static TweetBackend GetTweetBackEnd(long id)
        {
            lock (tdblock)
            {
                if (tdatabase != null)
                    return tdatabase.TweetSet.Where(b => b.Id == id).FirstOrDefault();
                else
                    return null;
            }
        }

        internal static void TweetSaveChange()
        {
            lock (tdblock)
            {
                if (tdatabase != null)
                    tdatabase.SaveChanges();
            }
        }

        internal static UserBackend GetUserBackEnd(long id)
        {
            lock (tdblock)
            {
                if (udatabase != null)
                    return udatabase.UserSet.Where(b => b.Id == id).FirstOrDefault();
                else
                    return null;
            }
        }

        internal static void UserSaveChange()
        {
            lock (udblock)
            {
                if (udatabase != null)
                    udatabase.SaveChanges();
            }
        }

        internal static void DisconnectDB()
        {
            lock (tdblock)
            {
                tdatabase.Dispose();
                tdatabase = null;
            }
            lock (udblock)
            {
                udatabase.Dispose();
                udatabase = null;
            }
        }
    }
}
