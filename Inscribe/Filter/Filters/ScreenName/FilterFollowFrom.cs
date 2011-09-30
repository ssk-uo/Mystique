using System.Linq;
using Dulcet.Twitter;
using Inscribe.Filter.Core;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.ScreenName
{
    public class FilterFollowFrom : ScreenNameFilterBase
    {
        private bool acceptBlocking = false;
        [GuiVisible("フォロー中ユーザーによるブロック中ユーザーのRTを受け入れる")]
        public bool AcceptBlocking
        {
            get { return this.acceptBlocking; }
            set
            {
                if (this.acceptBlocking == value) return;
                this.acceptBlocking = value;
                RaiseRequireReaccept();
            }
        }

        private FilterFollowFrom() { }
        public FilterFollowFrom(string needle) : this(needle, false) { }

        public FilterFollowFrom(string needle, bool acceptBlocking)
        {
            this.needle = needle;
            this.acceptBlocking = acceptBlocking;
        }

        protected override bool FilterStatus(TweetBackEnd status)
        {
            if (status.IsDirectMessage || status.RetweetedOriginalId != 0)
            {
                return AccountStorage.Accounts.Where(i => Match(i.ScreenName, needle))
                    .Any(i => i.IsFollowing(status.UserId));
            }
            else
            {
                var rtd = TweetStorage.Get(status.RetweetedOriginalId);
                return AccountStorage.Accounts.Where(i => Match(i.ScreenName, needle))
                    .Any(i => i.IsFollowing(status.UserId)) &&
                    (rtd == null ||
                    AccountStorage.Accounts.Where(i => Match(i.ScreenName, needle))
                    .All(i => i.IsBlocking(rtd.BackEnd.UserId)));
            }
        }

        public override string Identifier
        {
            get { return "follow_from"; }
        }

        public override string Description
        {
            get { return "指定アカウントのフォロー"; }
        }

        public override string FilterStateString
        {
            get { return "アカウント @" + this.needle + " のフォロー"; }
        }
    }
}
