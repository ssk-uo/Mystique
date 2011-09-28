using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Dulcet.Twitter;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;

namespace Inscribe.Storage.DataBase
{
    /// <summary>
    /// SQL Server Compact に格納するためのツイートデータ
    /// </summary>
    [Table("TweetTable")]
    public class SerializedTweetData
    {
        /// <summary>
        /// For deserialization
        /// </summary>
        public SerializedTweetData() { }

        public SerializedTweetData(TweetViewModel tvm)
        {
            this.Id = tvm.Status.Id;
            this.Text = tvm.Status.Text;
            this.UserId = tvm.Status.User.NumericId;
            this.CreatedAt = tvm.Status.CreatedAt;

            UpdateDynamicData(tvm);

            var status = tvm.Status as TwitterStatus;
            if (status != null)
            {
                this.Source = status.Source;
                this.InReplyToStatusId = status.InReplyToStatusId;
                this.InReplyToUserId = status.InReplyToUserId;
                this.InReplyToUserScreenName = status.InReplyToUserScreenName;
                this.RetweetedOriginalId = status.RetweetedOriginal == null ? 0 : status.RetweetedOriginal.Id;
                return;
            }
            var dmsg = tvm.Status as TwitterDirectMessage;
            if (dmsg != null)
            {
                this.DirectMessageReceipientId = dmsg.Recipient.NumericId;
                return;
            }
            throw new ArgumentException("Statusでもなく、DirectMessageでもありません。");
        }

        public void UpdateDynamicData(TweetViewModel tvm)
        {
            this.FavoredUserIds = tvm.FavoredUsers.Select(u => u.TwitterUser.NumericId).ToArray();
            this.RetweetedUserIds = tvm.RetweetedUsers.Select(u => u.TwitterUser.NumericId).ToArray();
            this.InReplyFroms = tvm.InReplyFroms.ToArray();
        }

        [Key]
        [DatabaseGenerated(System.ComponentModel.DataAnnotations.DatabaseGeneratedOption.Identity)]
        public int InternalId { get; set; }

        /// <summary>
        /// ツイート ID
        /// </summary>
        [Required]
        public long Id { get; set; }

        /// <summary>
        /// DMであるかどうか
        /// </summary>
        [Required]
        public bool IsDirectMessage { get; set; }

        /// <summary>
        /// テキスト
        /// </summary>
        public string Text { get; set; }

        [Required]
        public long UserId { get; set; }

        public DateTime CreatedAt { get; set; }

        // below properties for dm *************************************************************************

        public long DirectMessageReceipientId { get; set; }

        // below properties for status *********************************************************************

        public string Source { get; set; }

        public long InReplyToStatusId { get; set; }

        public long InReplyToUserId { get; set; }

        public string InReplyToUserScreenName { get; set; }

        public long RetweetedOriginalId { get; set; }

        public long[] RetweetedUserIds { get; set; }

        public long[] FavoredUserIds { get; set; }

        public long[] InReplyFroms { get; set; }

        public TweetViewModel Rebirth()
        {
            TwitterStatusBase tsb = null;
            if (this.IsDirectMessage)
            {
                tsb = new TwitterDirectMessage()
                {
                    Id = this.Id,
                    Text = this.Text,
                    User = TransparentProxy.GetUser(this.UserId),
                    CreatedAt = this.CreatedAt,
                    Recipient = TransparentProxy.GetUser(this.DirectMessageReceipientId)
                };
            }
            else
            {
                var rtos = TransparentProxy.GetTweetViewModel(this.RetweetedOriginalId);
                tsb = new TwitterStatus()
                {
                    Id = this.Id,
                    Text = this.Text,
                    User = TransparentProxy.GetUser(this.UserId),
                    CreatedAt = this.CreatedAt,
                    Source = this.Source,
                    InReplyToStatusId = this.InReplyToStatusId,
                    InReplyToUserId = this.InReplyToUserId,
                    InReplyToUserScreenName = this.InReplyToUserScreenName,
                    RetweetedOriginal = rtos != null ? rtos.Status as TwitterStatus : null
                };
            }
            var tvm = new TweetViewModel(tsb);
            if (RetweetedUserIds != null)
                tvm.RegisterRetweetedRangeUnsafe(RetweetedUserIds.Select(id => UserStorage.Get(TransparentProxy.GetUser(id))));
            if (FavoredUserIds != null)
                tvm.RegisterFavoredRangeUnsafe(FavoredUserIds.Select(id => UserStorage.Get(TransparentProxy.GetUser(id))));
            if (InReplyFroms != null)
                tvm.RegisterInReplyFromsUnsafe(InReplyFroms);
            return tvm;
        }
    }
}
