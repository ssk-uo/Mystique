using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Inscribe.Common;
using Inscribe.Configuration;
using Inscribe.Data;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;
using Livet;
using Livet.Commands;

namespace Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild
{
    /// <summary>
    /// ツイートを保持するViewModel
    /// </summary>
    public class TweetViewModel : ViewModel
    {
        #region Backend services

        private TweetBackend _backend;
        /// <summary>
        /// バックエンドをそのまま参照します。<para />
        /// NULLが戻ることがあります。
        /// </summary>
        /// <remarks>
        /// このプロパティにバインドしないでください。<para />
        /// 適切な変更通知を受け取れない可能性があります。
        /// </remarks>
        public TweetBackend BackendCache
        {
            get
            {
                _lastReference = DateTime.Now;
                return _backend;
            }
        }

        public bool IsBackendAlive
        {
            get { return _backend != null; }
        }

        internal void SetBackend(TweetBackend backend)
        {
            System.Diagnostics.Debug.WriteLine("Set TVM...(ID:" + this.BindingId + ")");
            this._backend = backend;
            this._lastReference = DateTime.Now;
            this._createdAt = backend.CreatedAt;
            this._isBackendGenerated = true;
            RaisePropertyChanged(() => Backend);
        }

        internal void ReleaseBackend()
        {
            System.Diagnostics.Debug.WriteLine("Release TVM...(ID:" + this.BindingId + ")");
            this._backend = null;
        }

        private DateTime _lastReference;
        public DateTime LastReference
        {
            get { return _lastReference; }
        }

        public readonly long BindingId;

        private bool _isBackendGenerated;

        /// <summary>
        /// バックエンドをDBキャッシュを考慮して取得します。
        /// </summary>
        public TweetBackend Backend
        {
            get
            {
                var bec = this.BackendCache;
                if (bec != null || !_isBackendGenerated)
                {
                    // バックエンドがまだ生きているか、もしくはそもそも存在していない
                    return bec;
                }
                else 
                {
                    // DBから取得
                    System.Diagnostics.Debug.WriteLine("Set TVM...(ID:" + this.BindingId + ")");
                    var be = PerpetuationStorage.GetTweetBackend(this.BindingId);
                    this._backend = be;
                    this._lastReference = DateTime.Now;
                    RaisePropertyChanged(() => Backend);
                    Task.Factory.StartNew(() => TweetStorage.ReleaseCacheIfNeeded());
                    return be;
                }
            }
        }

        #endregion

        public TweetViewModel(TweetBackend backend)
        {
            if (backend == null)
                throw new ArgumentException("backend");
            this.BindingId = backend.Id;
            this._backend = backend;
            this._createdAt = backend.CreatedAt;
            this._isBackendGenerated = true;
            this._lastReference = DateTime.Now;
        }

        public TweetViewModel(long id)
        {
            this.BindingId = id;
            this._isBackendGenerated = false;
            this._lastReference = DateTime.Now;
        }

        /// <summary>
        /// このステータスがステータス情報を保持しているか
        /// </summary>
        public bool IsStatusInfoContains
        {
            get { return this.Backend != null; }
        }

        /// <summary>
        /// Retweetを考慮したすべての本文を取得します。
        /// </summary>
        public string TweetText
        {
            get
            {
                var status = this;
                if (status.Backend.RetweetedOriginalId != 0)
                    status = TweetStorage.Get(status.Backend.RetweetedOriginalId, true);
                return status.Backend.Text;
            }
        }

        #region Twitter Status Property

        public UserViewModel UserViewModel
        {
            get { return UserStorage.Lookup(this.Backend.UserId); }
        }

        public string ScreenName
        {
            get
            {
                var uvm = UserViewModel;
                if (uvm == null)
                    return String.Empty;
                else
                    return uvm.Backend.ScreenName;
            }
        }

        public Uri ProfileImage
        {
            get
            {
                var uvm = UserViewModel;
                if (uvm == null)
                    return null;
                else
                    return new Uri(uvm.Backend.ProfileImage);
            }
        }

        public Uri DirectMessageReceipientImage
        {
            get
            {
                if (Backend.IsDirectMessage)
                {
                    var uvm = UserStorage.Lookup(Backend.DirectMessageReceipientId);
                    if (uvm == null)
                        return null;
                    else
                        return new Uri(uvm.Backend.ProfileImage);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 何も考慮せず本文を返します。<para />
        /// Retweetされたツイートの場合、RTが付きます。また、途切れる可能性があります。
        /// </summary>
        public string Text
        {
            get { return this.Backend.Text; }
        }

        /// <summary>
        /// このツイートのURL(Permalink)を取得します。
        /// </summary>
        public string Permalink
        {
            get
            {
                var uvm = UserViewModel;
                if (uvm == null) return string.Empty;
                return "http://twitter.com/" + this.ScreenName + "/status/" + this.Backend.Id.ToString();
            }
        }

        #endregion

        #region Retweeteds Control

        private object _rtlock = new object();

        private ConcurrentObservable<UserViewModel> _retweeteds = null;

        internal void RegisterRetweetedRangeUnsafe(IEnumerable<UserViewModel> users)
        {
            lock (_rtlock)
            {
                if (_retweeteds == null)
                    _retweeteds = new ConcurrentObservable<UserViewModel>();
                this._retweeteds.AddRange(users.Except(this._retweeteds));
            }
            RaisePropertyChanged(() => RetweetedUsers);
        }

        public bool RegisterRetweeted(UserViewModel user)
        {
            lock (_rtlock)
            {
                if (_retweeteds == null)
                    _retweeteds = new ConcurrentObservable<UserViewModel>();
                if (user == null || this._retweeteds.Contains(user))
                    return false;
                this._retweeteds.Add(user);
            }
            TweetStorage.NotifyTweetStateChanged(this);
            RaisePropertyChanged(() => RetweetedUsers);
            return true;
        }

        public bool RemoveRetweeted(UserViewModel user)
        {
            lock (_rtlock)
            {
                if (_retweeteds == null)
                    return false;
                if (user == null || !this._retweeteds.Contains(user))
                    return false;
                this._retweeteds.Remove(user);
                if (this._retweeteds.Count == 0)
                    this._retweeteds = null;
            }
            TweetStorage.NotifyTweetStateChanged(this);
            RaisePropertyChanged(() => RetweetedUsers);
            return true;
        }

        public void UpdateRetweeteds()
        {
            Task.Factory.StartNew(() =>
                        this.Backend.RetweetedUserIds = this.RetweetedUsers
                        .Select(vm => vm.BindingId).ToArray());
        }

        public IEnumerable<UserViewModel> RetweetedUsers
        {
            get { return (IEnumerable<UserViewModel>)this._retweeteds ?? new UserViewModel[0]; }
        }

        #endregion

        #region Favored Control

        private object _favlock = new object();

        private ConcurrentObservable<UserViewModel> _favoreds = null;

        internal void RegisterFavoredRangeUnsafe(IEnumerable<UserViewModel> users)
        {
            lock (_favlock)
            {
                if (_favoreds == null)
                    _favoreds = new ConcurrentObservable<UserViewModel>();
                this._favoreds.AddRange(users.Except(this._favoreds));
            }
            UpdateFavoreds();
            RaisePropertyChanged(() => FavoredUsers);
        }

        public bool RegisterFavored(UserViewModel user)
        {
            lock (_favlock)
            {
                if (_favoreds == null)
                    _favoreds = new ConcurrentObservable<UserViewModel>();
                if (user == null || this._favoreds.Contains(user))
                    return false;
                this._favoreds.Add(user);
            }
            TweetStorage.NotifyTweetStateChanged(this);
            UpdateFavoreds();
            RaisePropertyChanged(() => FavoredUsers);
            return true;
        }

        public bool RemoveFavored(UserViewModel user)
        {
            lock (_favlock)
            {
                if (this._favoreds == null)
                    return false;
                if (user == null || this._favoreds.Contains(user))
                    this._favoreds.Remove(user);
                else
                    return false;
                if (this._favoreds.Count == 0)
                    this._favoreds = null;
            }
            TweetStorage.NotifyTweetStateChanged(this);
            UpdateFavoreds();
            RaisePropertyChanged(() => FavoredUsers);
            return true;
        }

        private void UpdateFavoreds()
        {
            Task.Factory.StartNew(() =>
                this.Backend.FavoredUserIds = this.FavoredUsers.Select(vm => vm.BindingId).ToArray());
        }

        public IEnumerable<UserViewModel> FavoredUsers
        {
            get { return (IEnumerable<UserViewModel>)this._favoreds ?? new UserViewModel[0]; }
        }

        #endregion

        #region Reply Chains Control

        private object _irlock = new object();

        /// <summary>
        /// このツイートに返信しているツイートのID
        /// </summary>
        private ConcurrentBag<long> inReplyFroms = null;

        internal void RegisterInReplyFromsUnsafe(long[] ids)
        {
            lock (_irlock)
            {
                if (inReplyFroms == null)
                    inReplyFroms = new ConcurrentBag<long>();
                ids.ForEach(i => this.inReplyFroms.Add(i));
            }
        }

        /// <summary>
        /// このツイートに返信を行っていることを登録します。
        /// </summary>
        /// <param name="tweetId">返信しているツイートのID</param>
        public void RegisterInReplyToThis(long tweetId)
        {
            lock (_irlock)
            {
                if (inReplyFroms == null)
                    inReplyFroms = new ConcurrentBag<long>();
                this.inReplyFroms.Add(tweetId);
                TweetStorage.NotifyTweetStateChanged(this);
            }
        }

        /// <summary>
        /// このツイートに返信しているツイートID
        /// </summary>
        public IEnumerable<long> InReplyFroms
        {
            get { return (IEnumerable<long>)this.inReplyFroms ?? new long[0]; }
        }

        #endregion

        #region Explicit Controlling Methods

        public void SettingValueChanged()
        {
            RaisePropertyChanged(() => Backend);
            RaisePropertyChanged(() => NameAreaWidth);
        }

        public void RefreshInReplyToInfo()
        {
            RaisePropertyChanged(() => ReplyText);
        }

        #endregion

        #region Setting dependent property

        public double NameAreaWidth
        {
            get { return (double)Setting.Instance.TweetExperienceProperty.NameAreaWidth; }
        }

        public bool IsP3StyleIcon
        {
            get { return Setting.Instance.TweetExperienceProperty.UseP3StyleIcon; }
        }

        #endregion

        #region Binding Helper Property

        public bool IsProtected
        {
            get { return TwitterHelper.GetSuggestedUser(this).Backend.IsProtected; }
        }

        public bool IsVerified
        {
            get { return TwitterHelper.GetSuggestedUser(this).Backend.IsVerified; }
        }

        public bool IsStatus
        {
            get { return !this.Backend.IsDirectMessage; }
        }

        public bool IsDirectMessage
        {
            get { return this.Backend.IsDirectMessage; }
        }

        public bool IsMention
        {
            get
            {
                return this.Backend.InReplyToStatusId != 0;
            }
        }

        public bool IsMentionToMe
        {
            get
            {
                return TwitterHelper.IsInReplyToMe(this);
            }
        }

        public bool IsPublishedByRetweet
        {
            get
            {
                return TwitterHelper.IsPublishedByRetweet(this);
            }
        }

        public bool IsFavored
        {
            get
            {
                return TwitterHelper.IsFavoredThis(this);
            }
        }

        public string ReplyText
        {
            get
            {
                if (this.Backend.InReplyToStatusId != 0)
                {
                    var tweet = TweetStorage.Get(this.Backend.InReplyToStatusId);
                    if (tweet == null || !tweet.IsStatusInfoContains)
                        return "受信していません";
                    else
                        return "@" + tweet.ScreenName + ": " + tweet.Backend.Text;
                }
                else
                {
                    return String.Empty;
                }
            }
        }

        public bool ShowRetweetButton
        {
            get
            {
                return !this.IsProtected;
            }
        }

        public bool ShowUnofficialRetweetButton
        {
            get
            {
                return Setting.Instance.TweetExperienceProperty.ShowUnofficialRetweetButton && !this.IsProtected;
            }
        }

        public bool ShowQuoteButton
        {
            get
            {
                return Setting.Instance.TweetExperienceProperty.ShowQuoteButton;
            }
        }

        public bool ShowDeleteButton
        {
            get { return AccountStorage.Contains(this.ScreenName); }
        }

        public bool IsMyTweet
        {
            get
            {
                var ud = UserStorage.Lookup(this.Backend.UserId);
                return ud != null && AccountStorage.Get(ud.Backend.ScreenName) != null;
            }
        }

        private DateTime _createdAt;
        public DateTime CreatedAt
        {
            get
            {
                return _createdAt;
            }
        }

        #endregion

        #region Commands

        #region CopySTOTCommand
        ViewModelCommand _CopySTOTCommand;

        public ViewModelCommand CopySTOTCommand
        {
            get
            {
                if (_CopySTOTCommand == null)
                    _CopySTOTCommand = new ViewModelCommand(CopySTOT);
                return _CopySTOTCommand;
            }
        }

        private void CopySTOT()
        {
            CopyClipboard(TwitterHelper.GetSuggestedUser(this).Backend.ScreenName + ":" +
                this.TweetText + " [" + this.Permalink + "]");

        }
        #endregion

        #region CopyWebUrlCommand
        ViewModelCommand _CopyWebUrlCommand;

        public ViewModelCommand CopyWebUrlCommand
        {
            get
            {
                if (_CopyWebUrlCommand == null)
                    _CopyWebUrlCommand = new ViewModelCommand(CopyWebUrl);
                return _CopyWebUrlCommand;
            }
        }

        private void CopyWebUrl()
        {
            CopyClipboard(this.Permalink);
        }
        #endregion

        #region CopyScreenNameCommand
        ViewModelCommand _CopyScreenNameCommand;

        public ViewModelCommand CopyScreenNameCommand
        {
            get
            {
                if (_CopyScreenNameCommand == null)
                    _CopyScreenNameCommand = new ViewModelCommand(CopyScreenName);
                return _CopyScreenNameCommand;
            }
        }

        private void CopyScreenName()
        {
            CopyClipboard(TwitterHelper.GetSuggestedUser(this).Backend.ScreenName);
        }
        #endregion

        private void CopyClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
                NotifyStorage.Notify("コピーしました: " + text);
            }
            catch (Exception ex)
            {
                ExceptionStorage.Register(ex, ExceptionCategory.InternalError,
                    "コピーに失敗しました");
            }
        }

        #region ShowTweetCommand

        ViewModelCommand _ShowTweetCommand;

        public ViewModelCommand ShowTweetCommand
        {
            get
            {
                if (_ShowTweetCommand == null)
                    _ShowTweetCommand = new ViewModelCommand(ShowTweet);
                return _ShowTweetCommand;
            }
        }

        private void ShowTweet()
        {
            Browser.Start(this.Permalink);
        }

        #endregion

        #endregion

        public override bool Equals(object obj)
        {
            var tdtv = obj as TweetViewModel;
            if (tdtv != null)
                return this.BindingId == tdtv.BindingId;
            else
                return false;
        }

        public override int GetHashCode()
        {
            return (int)this.BindingId;
        }
    }
}
