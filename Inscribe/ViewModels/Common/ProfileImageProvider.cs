using System;
using System.Threading.Tasks;
using Dulcet.Twitter.Rest;
using Inscribe.Authentication;
using Inscribe.Common;
using Inscribe.Storage;
using Livet;

namespace Inscribe.ViewModels.Common
{
    public class ProfileImageProvider : ViewModel
    {
        private AccountInfo _info;
        public ProfileImageProvider(AccountInfo relatedInfo)
        {
            this._info = relatedInfo;
        }

        public Uri ProfileImage
        {
            get
            {
                var ud = UserStorage.Lookup(this._info.ScreenName);
                if (ud != null)
                    return new Uri(ud.BackEnd.ProfileImage);
                else
                {
                    Task.Factory.StartNew(() =>
                    {
                        var info = ApiHelper.ExecApi(() => this._info.GetUserByScreenName(this._info.ScreenName));
                        if (info != null)
                        {
                            UserStorage.Register(info);
                            RaisePropertyChanged(() => ProfileImage);
                        }
                    });
                    return null;
                }
            }
        }
    }
}
