using System;
using System.Threading;
using System.Threading.Tasks;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;
using Livet;
using Inscribe.Configuration;
using Inscribe.Configuration.Settings;

namespace Inscribe.ViewModels.PartBlocks.MainBlock
{
    public class UserViewModel : ViewModel
    {
        #region Backend services

        private UserBackend _backend;

        private UserBackend BackendCache
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

        internal void SetBackend(UserBackend backend)
        {
            if (this._backend != null) return;
            if (Interlocked.Exchange(ref _backend, backend) == null)
                Task.Factory.StartNew(() => UserStorage.AddCacheCount());
            this._lastReference = DateTime.Now;
        }

        internal void ReleaseBackend()
        {
            if (this._backend == null) return;
            if (Interlocked.Exchange(ref _backend, null) != null)
                UserStorage.ReleaseCacheCount();
        }

        private DateTime _lastReference;
        public DateTime LastReference
        {
            get { return _lastReference; }
        }

        #endregion

        public UserViewModel(UserBackend user)
        {
            if (user == null)
                throw new NullReferenceException("user");
            this._backend = user;
            this.BindingId = user.Id;
            Task.Factory.StartNew(() => UserStorage.AddCacheCount());
        }

        public long BindingId { get; private set; }

        /// <summary>
        /// バックエンドをDBキャッシュを考慮して取得します。
        /// </summary>
        public UserBackend Backend
        {
            get
            {
                var bec = this.BackendCache;
                if (bec != null)
                {
                    // alive backend
                    return bec;
                }
                else
                {
                    // from DB
                    var be = PerpetuationStorage.GetUserBackend(this.BindingId);
                    if (Interlocked.Exchange(ref _backend, be) == null)
                        Task.Factory.StartNew(() => UserStorage.AddCacheCount());
                    this._lastReference = DateTime.Now;
                    RaisePropertyChanged(() => Backend);
                    return be;
                }
            }
        }

        public override bool Equals(object obj)
        {
            var tvm = obj as UserViewModel;
            return tvm != null && tvm.BindingId == this.BindingId;
        }

        public override int GetHashCode()
        {
            return (int)this.BindingId;
        }


        #region User Names

        private static string GetUserName(UserViewModel user)
        {
            return user.Backend.UserName;
        }

        private static string GetScreenName(UserViewModel user)
        {
            return user.Backend.ScreenName;
        }

        public string Name
        {
            get { return GetUserName(this); }
        }

        public string ScreenName
        {
            get { return GetScreenName(this); }
        }

        public string RetweetedScreenName
        {
            get { return this.Backend.ScreenName; }
        }

        public string ViewName
        {
            get
            {
                switch (Setting.Instance.TweetExperienceProperty.UserNameViewMode)
                {
                    case NameView.ID:
                        return GetScreenName(this);
                    case NameView.Name:
                        return GetUserName(this);
                    case NameView.Both:
                    default:
                        return GetScreenName(this) + " (" + GetUserName(this) + ")";
                }
            }
        }

        public string NotifyViewName
        {
            get
            {
                switch (Setting.Instance.TweetExperienceProperty.NotificationNameViewMode)
                {
                    case NameView.ID:
                        return GetScreenName(this);
                    case NameView.Name:
                        return GetUserName(this);
                    case NameView.Both:
                    default:
                        return GetScreenName(this) + " (" + GetUserName(this) + ")";
                }
            }
        }

        #endregion
    }
}