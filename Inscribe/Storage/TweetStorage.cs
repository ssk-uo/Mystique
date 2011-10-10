using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dulcet.Twitter;
using Inscribe.Common;
using Inscribe.Configuration;
using Inscribe.Data;
using Inscribe.Subsystems;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;
using Livet;
using Inscribe.ViewModels.PartBlocks.MainBlock;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Storage
{
    /// <summary>
    /// ツイートの存在状態
    /// </summary>
    public enum TweetExistState
    {
        Unreceived,
        EmptyExists,
        Exists,
        ServerDeleted,
    }

    /// <summary>
    /// ツイートデータ(ViewModel)保持ベースクラス
    /// </summary>
    public static class TweetStorage
    {
        /// <summary>
        /// ディクショナリのロック
        /// </summary>
        static ReaderWriterLockWrap dicLockWrap = new ReaderWriterLockWrap(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// 登録済みステータスディクショナリ
        /// </summary>
        static Dictionary<long, TweetViewModel> dictionary = new Dictionary<long, TweetViewModel>();

        static ReaderWriterLockWrap edLockWrap = new ReaderWriterLockWrap(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// 仮登録ステータスディクショナリ
        /// </summary>
        static Dictionary<long, TweetViewModel> empties = new Dictionary<long, TweetViewModel>();

        /// <summary>
        /// 削除予約されたツイートIDリスト
        /// </summary>
        static LinkedList<long> deleteReserveds = new LinkedList<long>();

        /// <summary>
        /// ツイートストレージの作業用スレッドディスパッチャ
        /// </summary>
        static QueueTaskDispatcher operationDispatcher;

        static TweetStorage()
        {
            operationDispatcher = new QueueTaskDispatcher(1);
            ThreadHelper.Halt += () => operationDispatcher.Dispose();
        }

        /// <summary>
        /// ツイートを受信したか、また明示的削除などが行われたかを確認します。
        /// </summary>
        public static TweetExistState Contains(long id)
        {
            using (dicLockWrap.GetReaderLock())
            using (edLockWrap.GetReaderLock())
            {
                System.Diagnostics.Debug.WriteLine("contains");
                if (dictionary.ContainsKey(id))
                    return TweetExistState.Exists;
                else if (deleteReserveds.Contains(id))
                    return TweetExistState.ServerDeleted;
                else if (empties.ContainsKey(id))
                    return TweetExistState.EmptyExists;
                else
                    return TweetExistState.Unreceived;
            }
        }

        /// <summary>
        /// ツイートデータを取得します。
        /// </summary>
        /// <param name="id">ツイートID</param>
        /// <param name="createEmpty">存在しないとき、空のViewModelを作って登録して返す</param>
        public static TweetViewModel Get(long id, bool createEmpty = false)
        {
            using (dicLockWrap.GetReaderLock())
            using (createEmpty ? edLockWrap.GetUpgradableReaderLock() : edLockWrap.GetReaderLock())
            {
                TweetViewModel ret;
                if (dictionary.TryGetValue(id, out ret))
                    return ret;
                if (empties.TryGetValue(id, out ret))
                    return ret;
                if (createEmpty)
                {
                    using (edLockWrap.GetWriterLock())
                    {
                        var nvm = new TweetViewModel(id);
                        empties.Add(id, nvm);
                        return nvm;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 登録されているステータスを抽出します。
        /// </summary>
        /// <param name="predicate">抽出条件</param>
        /// <returns>条件にマッチするステータス、または登録されているすべてのステータス</returns>
        public static IEnumerable<TweetViewModel> GetAll(Func<TweetViewModel, bool> predicate = null)
        {
            using (dicLockWrap.GetReaderLock())
            {
                if (predicate == null)
                    return dictionary.Values.ToArray();
                else
                    return dictionary.Values.AsParallel().Where(predicate).ToArray();
            }
        }

        /// <summary>
        /// カウンタキャッシュ
        /// </summary>
        private static volatile int _count = 0;

        /// <summary>
        /// 登録されているツイートの個数を取得します。
        /// </summary>
        public static int Count
        {
            get
            {
                return _count;
            }
        }

        /// <summary>
        /// 受信したツイートを登録します。<para />
        /// 諸々の処理を自動で行います。
        /// </summary>
        public static void Register(TwitterStatusBase statusBase)
        {
            TweetViewModel robj;
            using (dicLockWrap.GetReaderLock())
            {
                if (dictionary.ContainsKey(statusBase.Id))
                    return;
            }
            var status = statusBase as TwitterStatus;
            if (status != null)
            {
                RegisterStatus(status);
            }
            else
            {
                var dmsg = statusBase as TwitterDirectMessage;
                if (dmsg != null)
                {
                    RegisterDirectMessage(dmsg);
                }
                else
                {
                    throw new InvalidOperationException("不明なステータスを受信しました: " + statusBase);
                }
            }
        }

        public static void Register(TwitterStatusBase statusBase, Action<TweetViewModel> receiver)
        {
            TweetViewModel robj;
            using (dicLockWrap.GetReaderLock())
            {
                if (dictionary.TryGetValue(statusBase.Id, out robj))
                {
                    receiver(robj);
                    return;
                }
            }
            var status = statusBase as TwitterStatus;
            if (status != null)
            {
                RegisterStatus(status)
                        .ContinueWith(t => Task.Factory.StartNew(() => receiver(t.Result)));
                return;
            }
            else
            {
                var dmsg = statusBase as TwitterDirectMessage;
                if (dmsg != null)
                {
                    RegisterDirectMessage(dmsg)
                        .ContinueWith(t => Task.Factory.StartNew(() => receiver(t.Result)));
                    return;
                }
                else
                {
                    throw new InvalidOperationException("不明なステータスを受信しました: " + statusBase);
                }
            }
        }

        /// <summary>
        /// ステータスの追加に際しての処理
        /// </summary>
        private static Task<TweetViewModel> RegisterStatus(TwitterStatus status)
        {
            var regAction = new Func<Task<TweetViewModel>>(() =>
            {
                UserStorage.Register(status.User);
                return RegisterCore(status)
                    .ContinueWith(t =>
                    {
                        if (TwitterHelper.IsMentionOfMe(status))
                            EventStorage.OnMention(t.Result);
                        return t.Result;
                    });
            });
            if (status.RetweetedOriginal != null)
            {
                // リツイートのオリジナルステータスを登録
                return RegisterCore(status.RetweetedOriginal)
                    .ContinueWith(t =>
                        {
                            var vm = t.Result;
                            // リツイートユーザーに登録
                            var user = UserStorage.Get(status.User);
                            var tuser = UserStorage.Get(status.RetweetedOriginal.User);
                            if (vm.RegisterRetweeted(user))
                            {
                                if (!vm.IsStatusInfoContains)
                                    vm.SetBackend(new Perpetuation.TweetBackend(status.RetweetedOriginal));
                                // 自分が関係していれば
                                if (AccountStorage.Contains(status.RetweetedOriginal.User.ScreenName)
                                    || AccountStorage.Contains(user.Backend.ScreenName))
                                    EventStorage.OnRetweeted(vm, user);
                            }
                            return regAction().Result;
                        });
            }
            else
            {
                return regAction();
            }
        }

        /// <summary>
        /// ダイレクトメッセージの追加に際しての処理
        /// </summary>
        private static Task<TweetViewModel> RegisterDirectMessage(TwitterDirectMessage dmsg)
        {
            UserStorage.Register(dmsg.Sender);
            UserStorage.Register(dmsg.Recipient);
            return RegisterCore(dmsg).ContinueWith(t =>
                {
                    EventStorage.OnDirectMessage(t.Result);
                    return t.Result;
                });
        }

        /// <summary>
        /// ステータスベースの登録処理
        /// </summary>
        private static Task<TweetViewModel> RegisterCore(TwitterStatusBase statusBase)
        {
            PreProcess(statusBase);
            TweetViewModel generated = null;
            Task<TweetViewModel> result = new Task<TweetViewModel>(() => generated);
            operationDispatcher.Enqueue(() =>
            {
                generated = RegisterBackend(new TweetBackend(statusBase));
                // generated = RegisterSink(statusBase);
                result.Start();
            });
            return result;
            // .else. return RegisterBackend(new Perpetuation.TweetBackend(statusBase));
        }

        internal static TweetViewModel RegisterBackend(TweetBackend backend)
        {
            TweetViewModel viewModel;
            using (edLockWrap.GetUpgradableReaderLock())
            {
                if (empties.TryGetValue(backend.Id, out viewModel))
                {
                    // 既にViewModelが生成済み
                    if (!viewModel.IsStatusInfoContains)
                        viewModel.SetBackend(backend);
                    using (edLockWrap.GetWriterLock())
                    {
                        empties.Remove(backend.Id);
                    }
                }
                else
                {
                    // 全く初めて触れるステータス
                    _count++;
                    viewModel = new TweetViewModel(backend);
                }
            }
            if (ValidateTweet(viewModel))
            {
                // プリプロセッシング
                using (dicLockWrap.GetUpgradableReaderLock())
                using (edLockWrap.GetReaderLock())
                {
                    if (!deleteReserveds.Contains(backend.Id))
                    {
                        if (dictionary.ContainsKey(backend.Id))
                        {
                            return viewModel; // すでにKrile内に存在する
                        }
                        else
                        {
                            if (PerpetuationStorage.AddTweetBackend(viewModel.Backend))
                            {
                                using (dicLockWrap.GetWriterLock())
                                {
                                    dictionary.Add(backend.Id, viewModel);
                                }
                            }
                        }
                        Task.Factory.StartNew(() => RaiseStatusAdded(viewModel));
                    }
                }
                // 返信先の登録
                if (backend.InReplyToStatusId != 0)
                {
                    Get(backend.InReplyToStatusId, true).RegisterInReplyToThis(backend.Id);
                }

                PerpetuationStorage.EnterLockWhenInitialized(() =>
                    PerpetuationStorage.Tweets.Where(b => b.InReplyToStatusId == backend.Id)
                    .Select(b => b.Id)
                    .ToArray())
                    .ForEach(id => viewModel.RegisterInReplyToThis(id));
            }
            return viewModel;
        }

        internal static void WritebackFromDb(TweetBackend backend)
        {
            using (dicLockWrap.GetWriterLock())
            {
                if (!dictionary.ContainsKey(backend.Id))
                {
                    dictionary.Add(backend.Id, new TweetViewModel(backend));
                    _count++;
                }
            }
        }


        /// <summary>
        /// 登録可能なツイートか判定
        /// </summary>
        /// <returns></returns>
        public static bool ValidateTweet(TweetViewModel viewModel)
        {
            if (viewModel.Backend == null || viewModel.Backend.UserId == 0)
                throw new ArgumentException("データが破損しています。");
            return true;
        }

        /// <summary>
        /// ステータスのプリプロセッシング
        /// </summary>
        private static void PreProcess(TwitterStatusBase status)
        {
            try
            {
                if (status.Entities != null)
                {
                    // extracting t.co
                    var urls = status.Entities.GetChildNode("urls");
                    if (urls != null)
                    {
                        // indicesの始まりが遅い順に置換していく
                        urls.GetChildNodes("item")
                            .Where(i => i.GetChildNode("indices") != null)
                            .Where(i => i.GetChildNode("indices").GetChildValues("item") != null)
                            .OrderByDescending(i => i.GetChildNode("indices").GetChildValues("item")
                                .Select(s => int.Parse(s.Value)).First())
                            .ForEach(i =>
                        {
                            var expand = i.GetChildValue("expanded_url").Value;
                            if (String.IsNullOrWhiteSpace(expand))
                                expand = i.GetChildValue("url").Value;
                            if (!String.IsNullOrWhiteSpace(expand))
                            {
                                var indices = i.GetChildNode("indices").GetChildValues("item")
                                    .Select(v => int.Parse(v.Value)).OrderBy(v => v).ToArray();
                                if (indices.Length == 2)
                                {
                                    status.Text = status.Text.Substring(0, indices[0]) +
                                        expand + status.Text.Substring(indices[1]);
                                }
                            }
                        });
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// ツイートをツイートストレージから除去
        /// </summary>
        /// <param name="id">ツイートID</param>
        public static void Trim(long id)
        {
            TweetViewModel remobj = null;
            using (dicLockWrap.GetWriterLock())
            using (edLockWrap.GetWriterLock())
            {
                empties.Remove(id);
                if (dictionary.TryGetValue(id, out remobj))
                {
                    dictionary.Remove(id);
                    Task.Factory.StartNew(() => RaiseStatusRemoved(remobj));
                }
            }
        }

        /// <summary>
        /// ツイートの削除
        /// </summary>
        /// <param name="id">削除するツイートID</param>
        public static void Remove(long id)
        {
            TweetViewModel remobj = null;
            using (dicLockWrap.GetWriterLock())
            using (edLockWrap.GetWriterLock())
            {
                System.Diagnostics.Debug.WriteLine("rmv");
                // 削除する
                deleteReserveds.AddLast(id);
                empties.Remove(id);
                if (dictionary.TryGetValue(id, out remobj))
                {
                    dictionary.Remove(id);
                    Task.Factory.StartNew(() => RaiseStatusRemoved(remobj));
                }
            }
            if (remobj != null)
            {
                // リツイート判定
                var be = remobj.Backend;
                if (be.RetweetedOriginalId != 0)
                {
                    var ros = TweetStorage.Get(be.RetweetedOriginalId);
                    if (ros != null)
                        ros.RemoveRetweeted(UserStorage.Lookup(be.UserId));
                }
            }
        }

        #region Event notification

        /// <summary>
        /// ツイートの内部状態が変化したことを通知します。<para />
        /// (例えば、ふぁぼられたりRTされたり返信貰ったりなど。)
        /// </summary>
        public static void NotifyTweetStateChanged(TweetViewModel tweet)
        {
            Task.Factory.StartNew(() => RaiseStatusStateChanged(tweet));
        }

        #region TweetStorageChangedイベント

        public static event EventHandler<TweetStorageChangedEventArgs> TweetStorageChanged;
        private static Notificator<TweetStorageChangedEventArgs> _TweetStorageChangedEvent;
        public static Notificator<TweetStorageChangedEventArgs> TweetStorageChangedEvent
        {
            get
            {
                if (_TweetStorageChangedEvent == null)
                    _TweetStorageChangedEvent = new Notificator<TweetStorageChangedEventArgs>();
                return _TweetStorageChangedEvent;
            }
            set { _TweetStorageChangedEvent = value; }
        }

        private static void OnTweetStorageChanged(TweetStorageChangedEventArgs e)
        {
            var threadSafeHandler = Interlocked.CompareExchange(ref TweetStorageChanged, null, null);
            if (threadSafeHandler != null) threadSafeHandler(null, e);
            TweetStorageChangedEvent.Raise(e);
        }

        #endregion

        static void RaiseStatusAdded(TweetViewModel added)
        {
            // Mention通知設定がないか、
            // 自分へのMentionでない場合にのみRegisterする
            // +
            // Retweet通知設定がないか、
            // 自分のTweetのRetweetでない場合にのみRegisterする
            TweetViewModel tvm;
            UserViewModel uvm;
            if ((!Setting.Instance.NotificationProperty.NotifyMention ||
                !TwitterHelper.IsMentionOfMe(added.Backend)) &&
                (!Setting.Instance.NotificationProperty.NotifyRetweet ||
                added.Backend.RetweetedOriginalId == 0 ||
                ((tvm = TweetStorage.Get(added.Backend.RetweetedOriginalId)) != null &&
                (uvm = UserStorage.Lookup(tvm.Backend.UserId)) != null &&
                AccountStorage.ContainsId(uvm.BindingId))))
                NotificationCore.RegisterNotify(added);
            OnTweetStorageChanged(new TweetStorageChangedEventArgs(TweetActionKind.Added, added));
            NotificationCore.DispatchNotify(added);
        }

        static void RaiseStatusRemoved(TweetViewModel removed)
        {
            OnTweetStorageChanged(new TweetStorageChangedEventArgs(TweetActionKind.Removed, removed));
        }

        static void RaiseStatusStateChanged(TweetViewModel changed)
        {
            OnTweetStorageChanged(new TweetStorageChangedEventArgs(TweetActionKind.Changed, changed));
        }

        static void RaiseRefreshTweets()
        {
            OnTweetStorageChanged(new TweetStorageChangedEventArgs(TweetActionKind.Refresh));
        }

        #endregion

        #region Cache control

        private static long _cachedCount = 0;

        /// <summary>
        /// ツイートキャッシュが追加されたことを通知します。
        /// </summary>
        public static void AddCacheCount()
        {
            var count = Interlocked.Increment(ref _cachedCount);
            if (!Setting.IsInitialized || Setting.Instance.KernelProperty.TweetCacheMaxCount == 0) return;
            if (count > Setting.Instance.KernelProperty.TweetCacheMaxCount)
                ReleaseCacheIfNeeded();
        }

        /// <summary>
        /// ツイートキャッシュが解放されたことを通知します。
        /// </summary>
        public static void ReleaseCacheCount()
        {
            Interlocked.Decrement(ref _cachedCount);
        }

        private static int _isReleasing = 0;

        /// <summary>
        /// 必要であればキャッシュを削除し、メモリ領域を解放します。
        /// </summary>
        private static void ReleaseCacheIfNeeded()
        {
            if (!Setting.IsInitialized || Setting.Instance.KernelProperty.TweetCacheMaxCount == 0) return;
            if (Interlocked.Exchange(ref _isReleasing, 1) == 1) return;
            try
            {
                TweetViewModel[] releases = null;
                using (dicLockWrap.GetUpgradableReaderLock())
                {
                    releases = dictionary.Values.Where(uvm => uvm.IsBackendAlive).ToArray();
                }
                Interlocked.Exchange(ref _cachedCount, releases.Length);
                if (releases.Length > Setting.Instance.KernelProperty.TweetCacheMaxCount)
                {
                    Task.Factory.StartNew(() =>
                    {
                        using (NotifyStorage.NotifyManually("ツイートキャッシュを最適化しています..."))
                        {
                            releases
                                .OrderByDescending(tvm => tvm.LastReference)
                                .Skip((int)(Setting.Instance.KernelProperty.TweetCacheMaxCount * Setting.Instance.KernelProperty.TweetCacheSurviveDensity))
                                .ForEach(uvm =>
                                {
                                    uvm.ReleaseBackend();
                                    Thread.Sleep(0);
                                });
                        }
                    });
                }
            }
            finally
            {
                _isReleasing = 0;
            }
        }

        #endregion
    }


    public class TweetStorageChangedEventArgs : EventArgs
    {
        public TweetStorageChangedEventArgs(TweetActionKind act, TweetViewModel added = null)
        {
            this.ActionKind = act;
            this.Tweet = added;
        }

        public TweetViewModel Tweet { get; set; }

        public TweetActionKind ActionKind { get; set; }
    }

    public enum TweetActionKind
    {
        /// <summary>
        /// ツイートが追加された
        /// </summary>
        Added,
        /// <summary>
        /// ツイートが削除された
        /// </summary>
        Removed,
        /// <summary>
        /// ツイートの固有情報が変更された
        /// </summary>
        Changed,
        /// <summary>
        /// ストレージ全体に変更が入った
        /// </summary>
        Refresh,
    }
}
