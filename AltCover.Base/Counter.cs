using System;
using System.Collections.Generic;
using System.Text;

namespace AltCover.Recorder
{
    public static class Counter
    {
        static internal void AddVisit(Dictionary<string, Dictionary<int, PointVisits>> counts,
                               string moduleId,
                               int hitPointId,
                               Track context)
        {
            if (!counts.ContainsKey(moduleId))
            {
                counts[moduleId] = new Dictionary<int, PointVisits>();
            }
            if (!counts[moduleId].ContainsKey(hitPointId))
            {
                counts[moduleId].Add(hitPointId, PointVisits.Default());
            }

            counts[moduleId][hitPointId] = counts[moduleId][hitPointId].Visit(context);
        }
    }
}