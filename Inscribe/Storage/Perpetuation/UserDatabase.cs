using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;

namespace Inscribe.Storage.Perpetuation
{
    internal sealed class UserDatabase : DbContext
    {
        internal UserDatabase(string cstr) : base(cstr) { }

        public IDbSet<UserBackend> UserSet { get; set; }
    }
}
