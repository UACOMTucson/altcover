namespace AltCover.Shadow

open System.Collections.Generic
open AltCover.Recorder

module Adapter =
  let DoPause() = Instance.DoPause null
  let DoResume() = Instance.DoResume null
  let VisitsClear() = Instance.Visits.Clear()
  let SamplesClear() = Instance.Samples.Clear()

  let internal prepareName name =
    if name
       |> Instance.Visits.ContainsKey
       |> not
    then
      let entry = Dictionary<int, PointVisits>()
      Instance.Visits.Add(name, entry)

  let VisitsAdd name line number =
    prepareName name
    Instance.Visits.[name].Add(line, { Count = number; Context = [] })

  let VisitsAddTrack name line number =
    prepareName name
    Instance.Visits.[name].Add(line,
                               { Count = number
                                 Context = [ Call 17
                                             Call 42 ]})
    Instance.Visits.[name].Add(line + 1,
                               { Count = number + 1
                                 Context = [ Time 17L
                                             Both { Time = 42L
                                                    Call = 23 } ] })

  let VisitsSeq() = Instance.Visits |> Seq.cast<obj>
  let VisitsEntrySeq key = Instance.Visits.[key] |> Seq.cast<obj>
  let VisitCount key key2 = Instance.Visits.[key].[key2].Count
  let Lock = Instance.Visits :> obj

  let VisitImplNone moduleId hitPointId =
    Instance.VisitImpl moduleId hitPointId Track.Null
  let VisitImplMethod moduleId hitPointId mId =
    Instance.VisitImpl moduleId hitPointId (Call mId)

  let AddSample moduleId hitPointId =
    Instance.TakeSample Sampling.Single moduleId hitPointId
  let internal NewBoth time track = Both { Time = time; Call = track }