using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Financisto.Common.Attribute;
using Financisto.Common.Localization;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class TreeNode
    {
        public string Name { get; private set; }

        public string Type { get; private set; }

        public List<TreeNode> Child { get; set; }

        private TreeNode(string key, string type)
        {
            Name = LocalizationService.Instance[key];
            Type = type;
        }

        public TreeNode(string key)
          : this(key, string.Empty)
        {
        }

        public TreeNode(Type type)
        {
            Type = type.ToString();
            string key = ((HeaderAttribute)System.Attribute.GetCustomAttribute(type, typeof(HeaderAttribute))!).Header;
            Name = LocalizationService.Instance[key];
        }
    }
}
