using Inscribe.Storage.Perpetuation;
using Inscribe.Storage;

namespace Inscribe.Filter.Filters.Text
{
    public class FilterUserBio : TextFilterBase
    {
        private FilterUserBio() { }

        public FilterUserBio(string needle) : this(needle, false) { }

        public FilterUserBio(string needle, bool isCaseSensitive)
        {
            this.needle = needle;
            this.isCaseSensitive = isCaseSensitive;
        }

        protected override bool FilterStatus(TweetBackend status)
        {
            var ud = UserStorage.Lookup(status.UserId);
            return this.Match(ud.Backend.Bio, this.needle, this.isCaseSensitive);
        }

        public override string Identifier
        {
            get { return "bio"; }
        }

        public override string Description
        {
            get { return "ユーザーのBio(Description)"; }
        }
    }
}
