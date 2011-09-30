using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Threading.Tasks;

namespace Inscribe.Storage.Perpetuation
{
    internal sealed class TweetDatabase : DbContext
    {
        internal TweetDatabase(string cstr) : base(cstr) { }

        public IDbSet<TweetBackend> TweetSet { get; set; }
    }
}
