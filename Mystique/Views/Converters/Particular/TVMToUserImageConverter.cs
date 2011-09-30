using System;
using System.Windows.Data;
using Inscribe.Common;
using Inscribe.Storage;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;

namespace Mystique.Views.Converters.Particular
{
    public enum UserImageViewKind
    {
        Default,
        Suggested,
        Retweeted,
        DirectMessageRecipient,
    }

    public class TVMToUserImageConverter : OneWayConverter<TweetViewModel, Uri>
    {
        public override Uri ToTarget(TweetViewModel input, object parameter)
        {
            if (input == null) return null;
            UserImageViewKind kind;
            if (!Enum.TryParse(parameter as string, out kind))
                kind = UserImageViewKind.Default;
            switch (kind)
            {
                case UserImageViewKind.Default:
                case UserImageViewKind.Retweeted:
                    var rud = input.UserViewModel;
                    return rud != null ? new Uri(rud.BackEnd.ProfileImage) : null;
                case UserImageViewKind.DirectMessageRecipient:
                    var dud = UserStorage.Lookup(input.BackEnd.DirectMessageReceipientId);
                    return dud != null ? new Uri(dud.BackEnd.ProfileImage) : null;
                case UserImageViewKind.Suggested:
                    return new Uri(TwitterHelper.GetSuggestedUser(input).BackEnd.ProfileImage);
                default:
                    return null;
            }
        }
    }
}
