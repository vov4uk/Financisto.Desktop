using Financisto.Tests.Common;

namespace Financisto.Tests.Common
{
    using System;
    using AutoFixture;
    using AutoFixture.Xunit3;

    public class AutoMoqDataAttribute : AutoDataAttribute
    {
        public AutoMoqDataAttribute()
            : base(CreateFixture)
        {
        }

        protected AutoMoqDataAttribute(Func<IFixture> fixtureFactory)
            : base(fixtureFactory)
        {
        }

        public static IFixture CreateFixture() => new DataFixture();
    }
}
