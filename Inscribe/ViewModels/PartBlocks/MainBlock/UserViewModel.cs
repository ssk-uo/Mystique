using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Livet;
using Dulcet.Twitter;
using Livet.Commands;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.ViewModels.PartBlocks.MainBlock
{
    public class UserViewModel : ViewModel
    {
        #region Backend services

        private UserBackEnd _backend;

        private UserBackEnd BackendCache
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

        internal void SetBackend(UserBackEnd backend)
        {
            this._backend = backend;
            this._lastReference = DateTime.Now;
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

        #endregion

        public UserViewModel(UserBackEnd user)
        {
            if (user == null)
                throw new NullReferenceException("user");
            this._backend = user;
            this.BindingId = user.Id;
        }

        public long BindingId { get; private set; }

        /// <summary>
        /// バックエンドをDBキャッシュを考慮して取得します。
        /// </summary>
        public UserBackEnd BackEnd
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
                    throw new NotImplementedException();
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
    }
}