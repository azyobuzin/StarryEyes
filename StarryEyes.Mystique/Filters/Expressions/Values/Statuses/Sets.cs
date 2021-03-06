﻿using System;
using System.Collections.Generic;
using StarryEyes.SweetLady.DataModel;

namespace StarryEyes.Mystique.Filters.Expressions.Values.Statuses
{
    public sealed class StatusFavorers : ValueBase
    {
        public override IEnumerable<FilterExpressionType>  SupportedTypes
        {
            get
            {
                yield return FilterExpressionType.Set;
            }
        }

        public override Func<TwitterStatus, ICollection<long>> GetSetValueProvider()
        {
            return _ => _.FavoritedUsers ?? new long[0];
        }

        public override string ToQuery()
        {
            return "favorers";
        }
    }

    public sealed class StatusRetweeters : ValueBase
    {
        public override IEnumerable<FilterExpressionType> SupportedTypes
        {
            get
            {
                yield return FilterExpressionType.Set;
            }
        }

        public override Func<TwitterStatus, ICollection<long>> GetSetValueProvider()
        {
            return _ => _.RetweetedUsers ?? new long[0];
        }

        public override string ToQuery()
        {
            return "retweeters";
        }
    }
}
