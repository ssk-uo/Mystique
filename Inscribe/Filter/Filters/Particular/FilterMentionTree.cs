using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dulcet.Twitter;
using Dulcet.Twitter.Rest;
using Inscribe.Common;
using Inscribe.Filter.Core;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;

namespace Inscribe.Filter.Filters.Particular
{
    public class FilterMentionTree : FilterBase
    {
        long origTracePoint;

        long tracePoint;

        [GuiVisible("トレースポイントID")]
        public long OrigTracePoint
        {
            get { return origTracePoint; }
            set
            {
                origTracePoint = value;
                tracePoint = value;
                RecursiveCheckId(tracePoint);
            }
        }

        private FilterMentionTree() { }

        public FilterMentionTree(long id)
        {
            origTracePoint = id;
            tracePoint = id;
            Task.Factory.StartNew(() => RecursiveCheckId(id));
        }

        private void RecursiveCheckId(long id)
        {
            if (id == 0)
            {
                RaiseRequireReaccept();
                return;
            }
            var cont = TweetStorage.Contains(id);
            if (cont == TweetExistState.Exists)
            {
                // データをチェックして、先があれば再帰
                var tweet = TweetStorage.Get(id);
                if (tweet == null)
                {
                    RaiseRequireReaccept();
                    return;
                }
                if (tweet.Backend.InReplyToStatusId != 0)
                {
                    this.tracePoint = tweet.Backend.InReplyToStatusId;
                    RaisePartialRequireReaccept(tweet.BindingId);
                    RecursiveCheckId(tweet.Backend.InReplyToStatusId);
                    tweet.RefreshInReplyToInfo(); // 返信情報の更新を通知
                }
                else
                {
                    RaiseRequireReaccept();
                }
            }
            else if (cont == TweetExistState.ServerDeleted)
            {
                // 消されてるからダメ
                RaiseRequireReaccept();
                return;
            }
            else
            {
                // tweetを受信しようか
                Action receive = null;
                receive = () =>
                {
                    try
                    {
                        var status = ApiHelper.ExecApi(() => AccountStorage.GetRandom().GetStatus(id));
                        if (status != null)
                        {
                            var vm = TweetStorage.Register(status);
                            Task.Factory.StartNew(() => RecursiveCheckId(status.Id));
                            Task.Factory.StartNew(() =>
                                PerpetuationStorage.EnterLockWhenInitialized(() =>
                                    PerpetuationStorage.Tweets.Where(i => i.InReplyToStatusId == id)
                                    .Select(s => s.Id).ToArray())
                                    .Select(n => TweetStorage.Get(n)).Where(cvm => cvm != null)
                                    .ForEach(cvm => cvm.RefreshInReplyToInfo()));
                        }
                        else
                        {
                            RaiseRequireReaccept();
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionStorage.Register(e, ExceptionCategory.TwitterError, "ツイート " + id + " の受信に失敗しました。", receive);
                        RaiseRequireReaccept();
                    }
                };
                Task.Factory.StartNew(() => receive());
            }
        }

        protected override bool FilterStatus(TweetBackend status)
        {
            return TraceId(status.Id);
        }

        private bool TraceId(long id)
        {
            var vm = TweetStorage.Get(id);
            if (vm == null || !vm.IsStatusInfoContains)
                return false;
            if (vm.BindingId == tracePoint)
                return true;
            if (vm.Backend.InReplyToStatusId == 0)
                return false;
            else
                return TraceId(vm.Backend.InReplyToStatusId);
        }

        public override string Identifier
        {
            get { return "mtree"; }
        }

        public override IEnumerable<object> GetArgumentsForQueryify()
        {
            yield return this.origTracePoint;
        }

        public override string Description
        {
            get { return "指定したIDからの返信ツリー"; }
        }

        public override string FilterStateString
        {
            get { return "ID " + this.origTracePoint + " の返信ツリー"; }
        }
    }
}
