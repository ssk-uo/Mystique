using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dulcet.Twitter.Rest;
using Inscribe.Common;
using Inscribe.Filter.Core;
using Inscribe.Storage;
using Inscribe.Storage.Perpetuation;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;
using System.Threading;

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
            System.Diagnostics.Debug.WriteLine("***** Recursive IN:" + id);
            if (id == 0)
            {
                System.Diagnostics.Debug.WriteLine("***** OK:" + id);
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
                    System.Diagnostics.Debug.WriteLine("***** OK:" + id);
                    RaiseRequireReaccept();
                }
                else if (tweet.Backend.InReplyToStatusId != 0)
                {
                    System.Diagnostics.Debug.WriteLine("***** New trace point:" + tweet.Backend.InReplyToStatusId);
                    this.tracePoint = tweet.Backend.InReplyToStatusId;
                    RaisePartialRequireReaccept(tweet.BindingId);
                    RecursiveCheckId(tweet.Backend.InReplyToStatusId);
                    tweet.RefreshInReplyToInfo(); // 返信情報の更新を通知
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("***** OK:" + id);
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
                        System.Diagnostics.Debug.WriteLine("***** Receiving:" + id);
                        var status = ApiHelper.ExecApi(() => AccountStorage.GetRandom().GetStatus(id));
                        System.Diagnostics.Debug.WriteLine("***** Received:" + id);
                        if (status != null)
                        {
                            TweetStorage.Register(status, _ => RecursiveCheckId(id));
                            Task.Factory.StartNew(() =>
                                PerpetuationStorage.EnterLockWhenInitialized(() =>
                                    PerpetuationStorage.Tweets.Where(i => i.InReplyToStatusId == id)
                                    .Select(s => s.Id).ToArray())
                                    .Select(n => TweetStorage.Get(n)).Where(cvm => cvm != null)
                                    .ForEach(cvm => cvm.RefreshInReplyToInfo()));
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("***** OK:" + id);
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

        private Tuple<long, long>[] idTable = null;

        private DateTime cacheGenerated = DateTime.MinValue;

        /// <summary>
        /// IDキャッシュを構築
        /// </summary>
        private void InvalidateIdCache()
        {
            System.Diagnostics.Debug.WriteLine("regenerate cache.");
            var idt = PerpetuationStorage.EnterLockWhenInitialized(() =>
                PerpetuationStorage.Tweets
                .Where(i => i.InReplyToStatusId != 0)
                .Select(i => new { i.Id, i.InReplyToStatusId }).ToArray())
                .Select(t => new Tuple<long, long>(t.Id, t.InReplyToStatusId)).ToArray();
            Interlocked.Exchange(ref idTable, idt);
            cacheGenerated = DateTime.Now;
        }

        private object idclock = new object();

        protected override bool FilterStatus(TweetBackend status)
        {
            lock (idclock)
            {
                if ((DateTime.Now - cacheGenerated).TotalSeconds > 10 || idTable == null)
                    InvalidateIdCache();
            }
            if (status.Id == tracePoint || status.InReplyToStatusId == tracePoint)
                return true;
            else if (status.InReplyToStatusId == 0)
                return false;
            else
                return TraceId(status.Id);
        }

        private bool TraceId(long id)
        {
            if (id == tracePoint)
                return true;
            var be = idTable.FirstOrDefault(i => i.Item1 == id);
            if (be == null || be.Item2 == 0)
                return false;
            else
                return TraceId(be.Item2);
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
