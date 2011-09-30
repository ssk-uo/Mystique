using System.Collections.Generic;
using System.Linq;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.ScreenName
{
    public class FilterRetweetFrom : ScreenNameFilterBase
    {
        private FilterRetweetFrom() { }

        public FilterRetweetFrom(string needle)
        {
            this.needle = needle;
        }

        protected override bool FilterStatus(TweetBackend status)
        {
            var vm = TweetStorage.Get(status.Id);
            if (vm == null) return false;
            return vm.RetweetedUsers.Any(u => Match(u.Backend.ScreenName, needle));
        }

        public override string Identifier
        {
            get { return "rt_from"; }
        }

        public override IEnumerable<object> GetArgumentsForQueryify()
        {
            yield return needle;
        }

        public override string Description
        {
            get { return "指定ユーザーのRetweet"; }
        }

        public override string FilterStateString
        {
            get { return "ユーザー @" + this.needle + " のRetweet"; }
        }
    }
}
