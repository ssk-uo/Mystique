using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Dulcet.Twitter;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;

namespace Inscribe.Storage.Perpetuation
{
    /// <summary>
    /// SQL Server Compact に格納するためのツイートデータ
    /// </summary>
    [Table("TweetTable")]
    public class TweetBackend
    {
        /// <summary>
        /// For deserialization
        /// </summary>
        public TweetBackend() { }

        public TweetBackend(TwitterStatusBase tsb)
        {
            this.Id = tsb.Id;
            this.Text = tsb.Text;
            this.UserId = tsb.User.NumericId;
            this.CreatedAt = tsb.CreatedAt;

            var status = tsb as TwitterStatus;
            if (status != null)
            {
                this.Source = status.Source;
                this.InReplyToStatusId = status.InReplyToStatusId;
                this.InReplyToUserId = status.InReplyToUserId;
                this.InReplyToUserScreenName = status.InReplyToUserScreenName;
                this.RetweetedOriginalId = status.RetweetedOriginal == null ? 0 : status.RetweetedOriginal.Id;
                return;
            }
            var dmsg = tsb as TwitterDirectMessage;
            if (dmsg != null)
            {
                this.DirectMessageReceipientId = dmsg.Recipient.NumericId;
                return;
            }
            throw new ArgumentException("Statusでもなく、DirectMessageでもありません。");
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
    }
}
