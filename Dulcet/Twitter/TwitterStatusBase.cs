using System;
using System.Xml.Linq;

namespace Dulcet.Twitter
{
    public abstract class TwitterStatusBase
    {
        public static string GetStatusId(XElement node)
        {
            if (node == null || node.Element("id") == null)
                return null;
            return node.Element("id").Value;
        }

        public long Id { get; set; }

        public string Text { get; set; }

        public TwitterUser User { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// DBへの格納時、Entity情報は失われます。
        /// </summary>
        public TwitterEntityNode Entities { get; set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as TwitterStatusBase);
        }

        public bool Equals(TwitterStatusBase status)
        {
            return status != null && this.Id == status.Id;
        }

        public override int GetHashCode()
        {
            return (int)this.Id;
        }
    }
}
