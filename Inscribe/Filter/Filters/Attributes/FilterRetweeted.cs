using System.Collections.Generic;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.Attributes
{
    public class FilterRetweeted : FilterBase
    {
        public FilterRetweeted() { }

        protected override bool FilterStatus(TweetBackend status)
        {
            return status.RetweetedOriginalId != 0;
        }

        public override string Identifier
        {
            get { return "retweeted"; }
        }

        public override IEnumerable<object> GetArgumentsForQueryify()
        {
            yield break;
        }

        public override string Description
        {
            get { return "リツイートされたステータス"; }
        }

        public override string FilterStateString
        {
            get { return "RTされたステータス"; }
        }
    }
}
