using System.Linq;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.ScreenName
{
    public class FilterBlockFrom : ScreenNameFilterBase
    {
        private FilterBlockFrom() { }
        public FilterBlockFrom(string needle)
        {
            this.needle = needle;
        }

        protected override bool FilterStatus(TweetBackend status)
        {
            return AccountStorage.Accounts.Where(i => Match(i.ScreenName, needle))
                .Any(i => i.IsBlocking(status.UserId));
        }

        public override string Identifier
        {
            get { return "block_from"; }
        }

        public override string Description
        {
            get { return "指定アカウントのブロック"; }
        }

        public override string FilterStateString
        {
            get { return "アカウント @" + this.needle + " でブロックしているユーザー"; }
        }
    }
}
