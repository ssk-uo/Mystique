using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Inscribe.Storage.Perpetuation;
using Inscribe.Storage;

namespace Inscribe.Filter.Filters.ScreenName
{
    public class FilterUser : ScreenNameFilterBase
    {
        private FilterUser() { }

        public FilterUser(string needle)
        {
            this.needle = needle;
        }

        protected override bool FilterStatus(TweetBackend status)
        {
            var ud = UserStorage.Lookup(status.UserId);
            if (ud == null) return false;
            return Match(ud.Backend.ScreenName, needle);
        }

        public override string Identifier
        {
            get { return "user"; }
        }

        public override string Description
        {
            get { return "ユーザー@ID"; }
        }
    }
}
