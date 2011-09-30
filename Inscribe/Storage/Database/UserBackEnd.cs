using System;
using System.ComponentModel.DataAnnotations;
using Dulcet.Twitter;

namespace Inscribe.Storage.Perpetuation
{
    [Table("UserTable")]
    public class UserBackEnd
    {
        /// <summary>
        /// For deserialization
        /// </summary>
        public UserBackEnd() { }

        public UserBackEnd(TwitterUser user)
        {
            this.Id = user.NumericId;
            Overwrite(user);
        }

        public void Overwrite(TwitterUser user)
        {
            if (this.Id != user.NumericId)
                throw new ArgumentException("IDが一致しません。");
            this.ScreenName = user.ScreenName;
            this.UserName = user.UserName;
            this.Location = user.Location;
            this.Bio = user.Bio;
            this.ProfileImage = user.ProfileImage.OriginalString;
            this.Web = user.Web;
            this.IsProtected = user.IsProtected;
            this.IsVerified = user.IsVerified;
            this.CreatedAt = user.CreatedAt;
            this.Followers = user.Followers;
            this.Followings = user.Followings;
            this.Favorites = user.Favorites;
            this.Listed = user.Listed;
            this.Tweets = user.Tweets;
            this.CreatedTimestamp = user.CreatedTimestamp;
        }

        [Key]
        [DatabaseGenerated(System.ComponentModel.DataAnnotations.DatabaseGeneratedOption.Identity)]
        public int InternalId { get; set; }

        [Required]
        public long Id { get; set; }

        [Required]
        public string ScreenName { get; set; }

        public string UserName { get; set; }

        public string Location { get; set; }

        public string Bio { get; set; }

        public string ProfileImage { get; set; }

        public string Web { get; set; }

        public bool IsProtected { get; set; }

        public bool IsVerified { get; set; }

        public DateTime CreatedAt { get; set; }

        public long Followers { get; set; }

        public long Followings { get; set; }

        public long Favorites { get; set; }

        public long Listed { get; set; }

        public long Tweets { get; set; }

        public DateTime CreatedTimestamp { get; set; }

        public TwitterUser Rebirth()
        {
            return new TwitterUser(this.CreatedTimestamp)
            {
                NumericId = this.Id,
                ScreenName = this.ScreenName,
                UserName = this.UserName,
                Bio = this.Bio,
                Location = this.Location,
                ProfileImage = new Uri(this.ProfileImage),
                Web = this.Web,
                IsProtected = this.IsProtected,
                IsVerified = this.IsVerified,
                CreatedAt = this.CreatedAt,
                Followers = this.Followers,
                Followings = this.Followings,
                Favorites = this.Favorites,
                Listed = this.Listed,
                Tweets = this.Tweets
            };
        }
    }
}
