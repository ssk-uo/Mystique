using System.Collections.Generic;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.Attributes
{
    public class FilterDirectMessage : FilterBase
    {
        protected override bool FilterStatus(TweetBackEnd status)
        {
            return status.IsDirectMessage;
        }

        public override string Identifier
        {
            get { return "dmsg"; }
        }

        public override IEnumerable<object> GetArgumentsForQueryify()
        {
            yield break;
        }

        public override string Description
        {
            get { return "ダイレクトメッセージ"; }
        }

        public override string FilterStateString
        {
            get { return "ダイレクトメッセージ"; }
        }
    }
}
