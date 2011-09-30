using System.Collections.Generic;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.Attributes
{
    public class FilterVerified : FilterBase
    {
        public FilterVerified() { }

        protected override bool FilterStatus(TweetBackEnd status)
        {
            var ud = UserStorage.Lookup(status.UserId);
            if (ud == null) return false;
            return ud.BackEnd.IsVerified;
        }

        public override string Identifier
        {
            get { return "verified"; }
        }

        public override IEnumerable<object> GetArgumentsForQueryify()
        {
            yield break;
        }

        public override string Description
        {
            get { return "公式認証ユーザーのツイート"; }
        }

        public override string FilterStateString
        {
            get { return "公式認証ユーザー"; }
        }
    }
}
