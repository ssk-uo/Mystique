using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dulcet.Twitter;
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

        private TweetBackEnd _backend;
        /// <summary>
        /// バックエンドをそのまま参照します。<para />
        /// NULLが戻ることがあります。
        /// </summary>
        /// <remarks>
        /// このプロパティにバインドしないでください。<para />
        /// 適切な変更通知を受け取れない可能性があります。
        /// </remarks>
        public TweetBackEnd BackEndCache
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

        internal void SetBackEnd(TweetBackEnd backend)
        {
            this._backend = backend;
            this._lastReference = DateTime.Now;
            this._backendIsGenerated = true;
            RaisePropertyChanged(() => BackEnd);
        }

        internal void ReleaseBackend()
        {
            this._backend = null;
        }

        private DateTime _lastReference;
        public DateTime LastReference
        {
            get { return _lastReference; }
        }

        public readonly long BindingId;

        private bool _backendIsGenerated;

        /// <summary>
        /// バックエンドをDBキャッシュを考慮して取得します。
        /// </summary>
        public TweetBackEnd BackEnd
        {
            get
            {
                var bec = this.BackEndCache;
                if (bec != null || !_backendIsGenerated)
                {
                    // バックエンドがまだ生きているか、もしくはそもそも存在していない
                    return bec;
                }
                else 
                {
                    // DBから取得
                    throw new NotImplementedException();
                    RaisePropertyChanged(() => BackEnd);
                }
            }
        }

        #endregion

        public TweetViewModel(TweetBackEnd backend)
        {
            if (backend == null)
                throw new ArgumentException("backend");
            this.BindingId = backend.Id;
            this._backend = backend;
            this._backendIsGenerated = true;
            this._lastReference = DateTime.Now;
        }

        public TweetViewModel(long id)
        {
            this.BindingId = id;
            this._backendIsGenerated = false;
            this._lastReference = DateTime.Now;
        }

        /// <summary>
        /// このステータスがステータス情報を保持しているか
        /// </summary>
        public bool IsStatusInfoContains
        {
            get { return this.BackEnd != null; }
        }

        /// <summary>
        /// Retweetを考慮したすべての本文を取得します。
        /// </summary>
        public string TweetText
        {
            get
            {
                var status = this;
                if (status.BackEnd.RetweetedOriginalId != 0)
                    status = TweetStorage.Get(status.BackEnd.RetweetedOriginalId, true);
                return status.BackEnd.Text;
            }
        }

        #region Twitter Status Property

        public UserViewModel UserViewModel
        {
            get { return UserStorage.Lookup(this.BackEnd.UserId); }
        }

        public string ScreenName
        {
            get
            {
                var uvm = UserViewModel;
                if (uvm == null)
                    return String.Empty;
                else
                    return uvm.BackEnd.ScreenName;
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
                    return new Uri(uvm.BackEnd.ProfileImage);
            }
        }

        public Uri DirectMessageReceipientImage
        {
            get
            {
                if (BackEnd.IsDirectMessage)
                {
                    var uvm = UserStorage.Lookup(BackEnd.DirectMessageReceipientId);
                    if (uvm == null)
                        return null;
                    else
                        return new Uri(uvm.BackEnd.ProfileImage);
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
            get { return this.BackEnd.Text; }
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
                return "http://twitter.com/" + this.ScreenName + "/status/" + this.BackEnd.Id.ToString();
            }
        }

        #endregion

        #region Retweeteds Control

        private ConcurrentObservable<UserViewModel> _retweeteds = new ConcurrentObservable<UserViewModel>();

        internal void RegisterRetweetedRangeUnsafe(IEnumerable<UserViewModel> users)
        {
            lock (_retweeteds)
            {
                this._retweeteds.AddRange(users.Except(this._retweeteds));
            }
            RaisePropertyChanged(() => RetweetedUsers);
        }

        public bool RegisterRetweeted(UserViewModel user)
        {
            lock (_retweeteds)
            {
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
            lock (_retweeteds)
            {
                if (user == null || !this._retweeteds.Contains(user))
                    return false;
                this._retweeteds.Remove(user);
            }
            TweetStorage.NotifyTweetStateChanged(this);
            RaisePropertyChanged(() => RetweetedUsers);
            return true;
        }

        public ConcurrentObservable<UserViewModel> RetweetedUsers
        {
            get { return this._retweeteds; }
        }

        #endregion

        #region Favored Control

        private ConcurrentObservable<UserViewModel> _favoreds = new ConcurrentObservable<UserViewModel>();

        internal void RegisterFavoredRangeUnsafe(IEnumerable<UserViewModel> users)
        {
            lock (_favoreds)
            {
                this._favoreds.AddRange(users.Except(this._favoreds));
            }
            RaisePropertyChanged(() => FavoredUsers);
        }

        public bool RegisterFavored(UserViewModel user)
        {
            lock (_favoreds)
            {
                if (user == null || this._favoreds.Contains(user))
                    return false;
                this._favoreds.Add(user);
            }
            TweetStorage.NotifyTweetStateChanged(this);
            RaisePropertyChanged(() => FavoredUsers);
            return true;
        }

        public bool RemoveFavored(UserViewModel user)
        {
            lock (_favoreds)
            {
                if (user == null || this._favoreds.Contains(user))
                    this._favoreds.Remove(user);
            }
            TweetStorage.NotifyTweetStateChanged(this);
            RaisePropertyChanged(() => FavoredUsers);
            return true;
        }

        public ConcurrentObservable<UserViewModel> FavoredUsers
        {
            get { return this._favoreds; }
        }

        #endregion

        #region Reply Chains Control

        /// <summary>
        /// このツイートに返信しているツイートのID
        /// </summary>
        private ConcurrentBag<long> inReplyFroms = new ConcurrentBag<long>();

        internal void RegisterInReplyFromsUnsafe(long[] ids)
        {
            ids.ForEach(i => this.inReplyFroms.Add(i));
        }

        /// <summary>
        /// このツイートに返信を行っていることを登録します。
        /// </summary>
        /// <param name="tweetId">返信しているツイートのID</param>
        public void RegisterInReplyToThis(long tweetId)
        {
            this.inReplyFroms.Add(tweetId);
            TweetStorage.NotifyTweetStateChanged(this);
        }

        /// <summary>
        /// このツイートに返信しているツイートID
        /// </summary>
        public IEnumerable<long> InReplyFroms
        {
            get { return this.inReplyFroms; }
        }

        #endregion

        #region Explicit Controlling Methods

        public void SettingValueChanged()
        {
            RaisePropertyChanged(() => BackEnd);
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
            get { return TwitterHelper.GetSuggestedUser(this).BackEnd.IsProtected; }
        }

        public bool IsVerified
        {
            get { return TwitterHelper.GetSuggestedUser(this).BackEnd.IsVerified; }
        }

        public bool IsStatus
        {
            get { return !this.BackEnd.IsDirectMessage; }
        }

        public bool IsDirectMessage
        {
            get { return this.BackEnd.IsDirectMessage; }
        }

        public bool IsMention
        {
            get
            {
                return this.BackEnd.InReplyToStatusId != 0;
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
                if (this.BackEnd.InReplyToStatusId != 0)
                {
                    var tweet = TweetStorage.Get(this.BackEnd.InReplyToStatusId);
                    if (tweet == null || !tweet.IsStatusInfoContains)
                        return "受信していません";
                    else
                        return "@" + tweet.ScreenName + ": " + tweet.BackEnd.Text;
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
                var ud = UserStorage.Lookup(this.BackEnd.UserId);
                return ud != null && AccountStorage.Get(ud.BackEnd.ScreenName) != null;
            }
        }

        public DateTime CreatedAt
        {
            get
            {
                if (this.BackEnd == null)
                    return DateTime.MinValue;
                else
                    return this.BackEnd.CreatedAt;
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
            CopyClipboard(TwitterHelper.GetSuggestedUser(this).BackEnd.ScreenName + ":" +
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
            CopyClipboard(TwitterHelper.GetSuggestedUser(this).BackEnd.ScreenName);
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
