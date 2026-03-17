using System.Collections.Generic;
using Verse;

namespace FoodSystemPipe
{
    public interface IEmbeddedPipeNode
    {
        IEnumerable<IntVec3> VisualPipeCells { get; }
        bool ConnectsPipeAt(IntVec3 cell);
    }
}
