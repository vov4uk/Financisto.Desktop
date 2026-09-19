using Financisto.DataAccess.Data;
using System;
using System.Collections.Generic;

namespace Financisto.Adapter
{
    public class EntityInfo
    {
        public EntityInfo()
        {
            Properties = new Dictionary<string, EntityPropertyInfo>();
        }

        public Type EntityType { get; set; }
        public Func<Entity> Factory { get; set; }
        public IDictionary<string, EntityPropertyInfo> Properties { get; private set; }
    }
}
