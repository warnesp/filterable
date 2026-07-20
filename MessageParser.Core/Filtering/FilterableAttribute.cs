using System;

namespace MessageParser.Core.Filtering
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FilterableAttribute : Attribute
    {
        public string FriendlyName { get; }

        public FilterableAttribute(string friendlyName)
        {
            FriendlyName = friendlyName ?? throw new ArgumentNullException(nameof(friendlyName));
        }
    }
}
