using System.Linq;
using System.Text.RegularExpressions;
using Dulcet.Twitter;
using Inscribe.Configuration.Tabs;
using Inscribe.Storage;
using Inscribe.Text;
using Inscribe.ViewModels.PartBlocks.MainBlock;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;
using Inscribe.Storage.Perpetuation;
using System;

namespace Inscribe.Common
{
    public class TwitterHelper
    {
        public static bool IsMyTweet(TweetViewModel status)
        {
            if (status == null || !status.IsStatusInfoContains) return false;
            return AccountStorage.Accounts
                .Any(d => status.ScreenName == d.ScreenName);
        }

        public static bool IsMyCurrentTweet(TweetViewModel status, TabProperty property)
        {
            if (status == null || !status.IsStatusInfoContains || property == null) return false;
            return property.LinkAccountScreenNames.Any(a => a == status.ScreenName);
        }

        public static bool IsRetweetedThis(TweetViewModel status)
        {
            if (status == null || !status.IsStatusInfoContains) return false;
            var rtd = status.RetweetedUsers.Select(d => d.BackEnd.ScreenName).ToArray();
            return AccountStorage.Accounts.Any(d => rtd.Contains(d.ScreenName));
        }

        public static bool IsRetweetedThisWithCurrent(TweetViewModel status, TabProperty property)
        {
            if (status == null || !status.IsStatusInfoContains || property == null) return false;
            var rtd = status.RetweetedUsers.Select(d => d.BackEnd.ScreenName).ToArray();
            return property.LinkAccountScreenNames.Any(a => rtd.Contains(a));
        }

        public static bool IsFavoredThis(TweetViewModel status)
        {
            if (status == null || !status.IsStatusInfoContains) return false;
            var fvd = status.FavoredUsers.Select(d => d.BackEnd.ScreenName).ToArray();
            return AccountStorage.Accounts.Any(d => fvd.Contains(d.ScreenName));
        }

        public static bool IsFavoredThisWithCurrent(TweetViewModel status, TabProperty property)
        {
            if (status == null || !status.IsStatusInfoContains || property == null) return false;
            var fvd = status.FavoredUsers.Select(d => d.BackEnd.ScreenName).ToArray();
            return property.LinkAccountScreenNames.Any(a => fvd.Contains(a));
        }

        public static bool IsInReplyToMe(TweetViewModel status)
        {
            if (status == null || !status.IsStatusInfoContains) return false;
            return AccountStorage.Accounts.Where(d => !String.IsNullOrEmpty(d.ScreenName)).Any(d =>
                Regex.IsMatch(status.BackEnd.Text, "@" + d.ScreenName + "(?![a-zA-Z0-9_])", RegexOptions.Singleline | RegexOptions.IgnoreCase));
        }

        public static bool IsInReplyToMeCurrent(TweetViewModel status, TabProperty property)
        {
            if (status == null || !status.IsStatusInfoContains || property == null) return false;
            return property.LinkAccountScreenNames.Any(a =>
                Regex.IsMatch(status.BackEnd.Text, "@" + a + "(?![a-zA-Z0-9_])", RegexOptions.Singleline | RegexOptions.IgnoreCase));
        }

        public static bool IsInReplyToMeStrict(TweetViewModel status)
        {
            if (status == null || !status.IsStatusInfoContains) return false;
            if (status.BackEnd.IsDirectMessage)
            {
                var recp = UserStorage.Lookup(status.BackEnd.DirectMessageReceipientId);
                return recp != null && AccountStorage.Accounts.Any(d => d.ScreenName == recp.BackEnd.ScreenName);
            }
            else
            {
                return AccountStorage.Accounts.Any(d => d.ScreenName == status.BackEnd.InReplyToUserScreenName);
            }
        }

        public static bool IsInReplyToMeCurrentStrict(TweetViewModel status, TabProperty property)
        {
            if (status == null || !status.IsStatusInfoContains || property == null) return false;
            if (status.BackEnd.IsDirectMessage)
            {
                var recp = UserStorage.Lookup(status.BackEnd.DirectMessageReceipientId);
                return recp != null && property.LinkAccountScreenNames.Any(a => a == recp.BackEnd.ScreenName);
            }
            else
            {
                return property.LinkAccountScreenNames.Any(a => a == status.BackEnd.InReplyToUserScreenName);
            }
        }

        public static bool IsFollowingCurrent(UserViewModel user, TabProperty property)
        {
            if (user == null) return false;
            return property.LinkAccountInfos.All(i => i.IsFollowing(user.BindingId));
        }

        public static bool IsFollowingAny(UserViewModel user)
        {
            if (user == null) return false;
            return AccountStorage.Accounts.Any(d => d.Followings.Contains(user.BindingId));
        }

        public static bool IsFollowingAll(UserViewModel user)
        {
            if (user == null) return false;
            return AccountStorage.Accounts.All(d => d.Followings.Contains(user.BindingId));
        }

        public static bool IsFollowerCurrent(UserViewModel user, TabProperty property)
        {
            if (user == null || property == null) return false;
            return property.LinkAccountInfos.All(i => i.IsFollowedBy(user.BindingId));
        }

        public static bool IsFollowerAny(UserViewModel user)
        {
            return AccountStorage.Accounts.Any(d => d.Followers.Contains(user.BindingId));
        }

        public static bool IsPublishedByRetweet(TweetViewModel status)
        {
            if (status == null || !status.IsStatusInfoContains) return false;
            return IsPublishedByRetweet(status.BackEnd);
        }

        public static bool IsPublishedByRetweet(TweetBackEnd status)
        {
            if (status == null) return false;
            return status.RetweetedOriginalId != 0;
        }

        public static bool IsMentionOfMe(TwitterStatusBase status)
        {
            var tweet = status as TwitterStatus;
            // DMではなくて、リツイートでもないことを確認する
            if (tweet != null && tweet.RetweetedOriginal == null)
            {
                // リツイートステータス以外で、自分への返信を探す
                var matches = RegularExpressions.AtRegex.Matches(status.Text);
                if (matches.Count > 0 && matches.Cast<Match>().Select(m => m.Value)
                        .Where(s => AccountStorage.Contains(s)).FirstOrDefault() != null)
                    return true;
            }
            return false;
        }

        public static bool IsMentionOfMe(TweetBackEnd backend)
        {
            if (!backend.IsDirectMessage && backend.RetweetedOriginalId == 0)
            {
                var matches = RegularExpressions.AtRegex.Matches(backend.Text);
                if (matches.Count > 0 && matches.Cast<Match>().Select(m => m.Value)
                    .Where(s => AccountStorage.Contains(s)).FirstOrDefault() != null)
                    return true;
            }
            return false;
        }

        public static UserViewModel GetSuggestedUser(TweetViewModel status)
        {
            return GetSuggestedUser(status.BackEnd);
        }

        public static UserViewModel GetSuggestedUser(TweetBackEnd backend)
        {
            if (IsPublishedByRetweet(backend))
            {
                var rtd = TweetStorage.Get(backend.RetweetedOriginalId);
                if (rtd == null)
                    return null;
                else
                    return UserStorage.Lookup(rtd.BackEnd.UserId);
            }
            else
            {
                return UserStorage.Lookup(backend.UserId);
            }
        }
    }
}
