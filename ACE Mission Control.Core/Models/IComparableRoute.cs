using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    interface IComparableRoute
    {
        long LastModificationTime { get; }
        int Id { get; }
    }
}
