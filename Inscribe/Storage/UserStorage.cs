using System;
using System.Collections.Concurrent;
using System.Linq;
using Dulcet.Twitter;
using Dulcet.Twitter.Rest;
using Inscribe.ViewModels.PartBlocks.MainBlock;
using Inscribe.Data;
using System.Threading;
using Inscribe.Storage.Perpetuation;
using System.Collections.Generic;
using Inscribe.Common;
using System.Data.Entity;
using System.IO;
using System.Data.Entity.Infrastructure;

namespace Inscribe.Storage
{
    public static class UserStorage
    {
        static ReaderWriterLockWrap lockWrap = new ReaderWriterLockWrap(LockRecursionPolicy.NoRecursion);

        static UserDatabase database;

        static object dblock = new object();

        static Dictionary<string, UserViewModel> dictionary = new Dictionary<string, UserViewModel>();

        static UserStorage()
        {
            ThreadHelper.Halt += () => database.Dispose();
            lock (dblock)
            {
                Database.DefaultConnectionFactory =   
                    new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0");

                // Initialize Tweet Database
                Database.SetInitializer(new DropCreateDatabaseAlways<TweetDatabase>());
                var path = Path.Combine(Path.GetDirectoryName(Define.ExeFilePath), Define.UserDatabaseFileName);
                database = new UserDatabase(path);
            }
        }

        /// <summary>
        /// キャッシュにユーザー情報が存在していたら、すぐに返します。<para />
        /// キャッシュに存在しない場合はNULLを返します。
        /// </summary>
        public static UserViewModel Lookup(string userScreenName)
        {
            if (userScreenName == null)
                throw new ArgumentNullException("userScreenName");
            UserViewModel ret;
            if (dictionary.TryGetValue(userScreenName, out ret))
                return ret;
            else
                return null;
        }

        /// <summary>
        /// User ViewModelを生成して、キャッシュに追加します。
        /// </summary>
        public static void Register(TwitterUser user)
        {
            Get(user);
        }

        /// <summary>
        /// User ViewModelを取得します。<para />
        /// 内部キャッシュを更新します。
        /// </summary>
        /// <param name="user">ユーザー情報(nullは指定できません)</param>
        public static UserViewModel Get(TwitterUser user)
        {
            if (user == null)
                throw new ArgumentNullException("user");
            var newvm = new UserViewModel(user);
            dictionary.AddOrUpdate(user.ScreenName, newvm);
            return newvm;
        }

        /// <summary>
        /// User ViewModelを取得します。<para />
        /// nullを返すことがあります。
        /// </summary>
        /// <param name="userScreenName">ユーザースクリーン名</param>
        /// <param name="useCache">内部キャッシュが可能であれば使用する</param>
        /// <returns></returns>
        public static UserViewModel Get(string userScreenName, bool useCache = true)
        {
            if (String.IsNullOrEmpty(userScreenName))
                throw new ArgumentNullException("userScreenName", "userScreenNameがNullであるか、または空白です。");
            UserViewModel ret = null;
            if (useCache && dictionary.TryGetValue(userScreenName, out ret))
            {
                return ret;
            }
            else
            {
                var acc = AccountStorage.GetRandom(ai => true, true);
                if (acc != null)
                {
                    try
                    {
                        var ud = acc.GetUserByScreenName(userScreenName);
                        if (ud != null)
                        {
                            var uvm = new UserViewModel(ud);
                            dictionary.AddOrUpdate(userScreenName, uvm);
                            return uvm;
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionStorage.Register(e, ExceptionCategory.TwitterError, "ユーザー情報の受信に失敗しました。(ユーザー @" + userScreenName + " を アカウント @" + acc.ScreenName + " で受信しようとしました。)");
                    }
                    return null;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// ストレージに格納されているすべてのユーザーを取得します。
        /// </summary>
        /// <returns></returns>
        public static UserViewModel[] GetAll()
        {
            return dictionary.Values.ToArray();
        }
    }
}
