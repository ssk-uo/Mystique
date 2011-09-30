using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Inscribe.Filter.Core;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;
using Inscribe.Text;

namespace Inscribe.Filter.Filters.Particular
{
    public class FilterConversation : FilterBase
    {
        private string user1;

        [GuiVisible("ユーザー1")]
        public string User1
        {
            get { return user1; }
            set { user1 = value; }
        }

        private string user2;

        [GuiVisible("ユーザー2")]
        public string User2
        {
            get { return user2; }
            set { user2 = value; }
        }

        private FilterConversation() { }

        public FilterConversation(string user1, string user2)
        {
            this.user1 = user1;
            this.user2 = user2;
        }

        protected override bool FilterStatus(TweetBackEnd status)
        {
            // conversation control
            if (status.IsDirectMessage)
            {
                var send = UserStorage.Lookup(status.UserId);
                var recp = UserStorage.Lookup(status.DirectMessageReceipientId);
                return
                    (send.BackEnd.ScreenName.Equals(user1, StringComparison.CurrentCultureIgnoreCase) &&
                     recp.BackEnd.ScreenName.Equals(user2, StringComparison.CurrentCultureIgnoreCase)) ||
                    (send.BackEnd.ScreenName.Equals(user2, StringComparison.CurrentCultureIgnoreCase) &&
                     recp.BackEnd.ScreenName.Equals(user1, StringComparison.CurrentCultureIgnoreCase));
            }
            else
            {
                if (RegularExpressions.AtRegex.Matches(status.Text).Cast<Match>()
                    .Any(m => m.Value.Equals(user1, StringComparison.CurrentCultureIgnoreCase) ||
                        m.Value.Equals(user2, StringComparison.CurrentCultureIgnoreCase)))
                    return true;
                var vm = TweetStorage.Get(status.Id);
                if (vm != null && vm.InReplyFroms.Select(id => TweetStorage.Get(id))
                    .Where(irvm => irvm != null)
                    .Any(irvm => CheckScreenName(irvm.BackEnd.UserId, user1) || CheckScreenName(irvm.BackEnd.UserId, user2)))
                    return true;
                else
                    return false;
            }
        }

        private bool CheckScreenName(long userId, string checkScreenName)
        {
            var ud = UserStorage.Lookup(userId);
            return ud != null && ud.BackEnd.ScreenName.Equals(checkScreenName, StringComparison.CurrentCultureIgnoreCase);
        }

        public override string Identifier
        {
            get { return "conv"; }
        }

        public override IEnumerable<object> GetArgumentsForQueryify()
        {
            yield return this.user1;
            yield return this.user2;
        }

        public override string Description
        {
            get { return "2ユーザー間の会話"; }
        }

        public override string FilterStateString
        {
            get { return "@" + user1 + " と @" + user2 + " の会話"; }
        }
    }
}
