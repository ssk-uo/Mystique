using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.Text
{
    public class FilterUserWeb : TextFilterBase
    {
        private FilterUserWeb() { }

        public FilterUserWeb(string needle) : this(needle, false) { }

        public FilterUserWeb(string needle, bool isCaseSensitive)
        {
            this.needle = needle;
            this.isCaseSensitive = isCaseSensitive;
        }

        protected override bool FilterStatus(TweetBackEnd status)
        {
            var ud = UserStorage.Lookup(status.UserId);
            if (ud == null) return false;
            return this.Match(ud.BackEnd.Web, this.needle, this.isCaseSensitive);
        }

        public override string Identifier
        {
            get { return "web"; }
        }

        public override string Description
        {
            get { return "ユーザーWeb"; }
        }
    }
}
