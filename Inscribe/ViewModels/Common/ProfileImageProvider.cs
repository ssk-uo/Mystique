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
                    return new Uri(ud.Backend.ProfileImage);
                else
                {
                    Task.Factory.StartNew(() => {
                        try
                        {
                            var info = UserStorage.Get(this._info.ScreenName);
                            if (info != null)
                                RaisePropertyChanged(() => ProfileImage);
                        }
                        catch (Exception e)
                        {
                            ExceptionStorage.Register(e, ExceptionCategory.TwitterError, 
                                "ユーザー情報を取得できません。", () => RaisePropertyChanged(() => ProfileImage));
                        }
                    });
                    return null;
                }
            }
        }
    }
}
