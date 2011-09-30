using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.Text
{
    public class FilterUserLocation : TextFilterBase
    {
        private FilterUserLocation() { }

        public FilterUserLocation(string needle) : this(needle, false) { }

        public FilterUserLocation(string needle, bool isCaseSensitive)
        {
            this.needle = needle;
            this.isCaseSensitive = isCaseSensitive;
        }

        protected override bool FilterStatus(TweetBackEnd status)
        {
            var ud = UserStorage.Lookup(status.UserId);
            if (ud == null) return false;
            return this.Match(ud.BackEnd.Location, this.needle, this.isCaseSensitive);
        }

        public override string Identifier
        {
            get { return "loc"; }
        }

        public override string Description
        {
            get { return "ユーザーのLocation"; }
        }
    }
}
