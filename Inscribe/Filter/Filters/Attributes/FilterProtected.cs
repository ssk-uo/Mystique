using System.Collections.Generic;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.Attributes
{
    public class FilterProtected : FilterBase
    {
        public FilterProtected() { }

        protected override bool FilterStatus(TweetBackEnd status)
        {
            var ud = UserStorage.Lookup(status.UserId);
            if (ud == null) return false;
            return ud.BackEnd.IsProtected;
        }

        public override string Identifier
        {
            get { return "protected"; }
        }

        public override IEnumerable<object> GetArgumentsForQueryify()
        {
            yield break;
        }

        public override string Description
        {
            get { return "プロテクトユーザーのツイート"; }
        }

        public override string FilterStateString
        {
            get { return "プロテクトユーザー"; }
        }
    }
}
