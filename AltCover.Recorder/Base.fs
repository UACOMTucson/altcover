namespace AltCover.Recorder

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Xml

// These conditionally internal for Gendarme
type
#if DEBUG
#else
     internal
#endif
              ReportFormat =
  | NCover = 0
  | OpenCover = 1
  | OpenCoverWithTracking = 2

type
#if DEBUG
#else
     internal
#endif
              Sampling =
  | All = 0
  | Single = 1

type
#if DEBUG
#else
     internal
#endif
              Tag =
  | Null = 0
  | Time = 1
  | Call = 2
  | Both = 3

#if NETSTANDARD2_0
[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
#else
[<System.Runtime.InteropServices.ProgIdAttribute("ExcludeFromCodeCoverage hack for OpenCover issue 615")>]
#endif
type internal Pair =
  { Time : int64
    Call : int }
  static member Build time call =
    { Time = time
      Call = call }

#if NETSTANDARD2_0
[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
#else
[<System.Runtime.InteropServices.ProgIdAttribute("ExcludeFromCodeCoverage hack for OpenCover issue 615")>]
#endif
type internal Track =
  | Null
  | Time of int64
  | Call of int
  | Both of Pair

#if NETSTANDARD2_0
[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
#else
[<System.Runtime.InteropServices.ProgIdAttribute("ExcludeFromCodeCoverage hack for OpenCover issue 615")>]
#endif
type internal PointVisits =
  { Count : int
    Context : Track list }
  static member Default () =
    { Count = 0
      Context = [] }
  member self.Visit context =
    match context with
    | Null -> { self with Count = self.Count + 1 }
    | something -> { self with Context = something :: self.Context }
  member self.Total () =
    self.Count + self.Context.Length

#if NETSTANDARD2_0
[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
#else
[<System.Runtime.InteropServices.ProgIdAttribute("ExcludeFromCodeCoverage hack for OpenCover issue 615")>]
#endif
[<NoComparison>]
type internal XmlPointVisits =
  { Element : XmlElement
    Context : Track list }
  static member Build e c =
    { Element = e
      Context = c |> Seq.toList }

#if NETSTANDARD2_0
[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
#else
[<System.Runtime.InteropServices.ProgIdAttribute("ExcludeFromCodeCoverage hack for OpenCover issue 615")>]
#endif
[<NoComparison>]
type internal TrackSplit =
  { Times : int64 seq
    Calls : int seq }
  static member Do (ts : Track seq) =
    let (times, calls) = ts
                         |> Seq.map (fun t ->
                               match t with
                               | Track.Time x -> (Some x, None)
                               | Track.Both xy -> (Some xy.Time, Some xy.Call)
                               | Track.Call y -> (None, Some y)
                               | _ -> (None, None))
                         |> Seq.toList
                         |> List.unzip
    { Times = times |> Seq.choose id
      Calls = calls |> Seq.choose id}

type internal PostProcessor = delegate of XmlDocument -> Unit
type internal PointProcessor = delegate of XmlPointVisits -> unit

module Assist =
  let internal SafeDispose x =
    try
      (x :> IDisposable).Dispose()
    with :? ObjectDisposedException -> ()

  let NoneString () : string option = None
  let SomeValue s  : string option = Some s

module Counter =
  /// <summary>
  /// The offset flag for branch counts
  /// </summary>
  let internal BranchFlag = 0x80000000

  let internal BranchMask = 0x7FFFFFFF

  /// <summary>
  /// The time at which coverage run began
  /// </summary>
  let mutable internal startTime = DateTime.UtcNow

  /// <summary>
  /// The finishing time taken of the coverage run
  /// </summary>
  let mutable internal measureTime = DateTime.UtcNow

  /// <summary>
  /// Load the XDocument
  /// </summary>
  /// <param name="path">The XML file to load</param>
  /// <remarks>Idiom to work with CA2202; we still double dispose the stream, but elude the rule.
  /// If this is ever a problem, we will need mutability and two streams, with explicit
  /// stream disposal if and only if the reader or writer doesn't take ownership
  /// </remarks>
  let private ReadXDocument(stream : Stream) =
    let doc = XmlDocument()
    doc.Load(stream)
    doc

  /// <summary>
  /// Write the XDocument
  /// </summary>
  /// <param name="coverageDocument">The XML document to write</param>
  /// <param name="path">The XML file to write to</param>
  /// <remarks>Idiom to work with CA2202 as above</remarks>
  let private WriteXDocument (coverageDocument : XmlDocument) (stream : Stream) =
    coverageDocument.Save(stream)

  let internal FindIndexFromUspid flag uspid =
    let f, c =
      Int32.TryParse
        (uspid, System.Globalization.NumberStyles.Integer,
         System.Globalization.CultureInfo.InvariantCulture)
    if f then (c ||| flag)
    else -1

  let internal OpenCoverXml =
    ("//Module", "hash", "Classes/Class/Methods/Method",
     [ ("SequencePoints/SequencePoint", 0)
       ("BranchPoints/BranchPoint", BranchFlag) ], "vc")

  let internal NCoverXml =
    ("//module", "moduleId", "method", [ ("seqpnt", 0) ], "visitcount")

  let internal XmlByFormat format =
    match format with
    | ReportFormat.OpenCoverWithTracking
    | ReportFormat.OpenCover -> OpenCoverXml
    | _ -> NCoverXml

  /// <summary>
  /// Save sequence point hit counts to xml report file
  /// </summary>
  /// <param name="hitCounts">The coverage results to incorporate</param>
  /// <param name="coverageFile">The coverage file to update as a stream</param>
  let internal UpdateReport (postProcess : PostProcessor)
      (pointProcess : PointProcessor) own
      (counts : Dictionary<string, Dictionary<int, PointVisits>>) format coverageFile
      (outputFile : Stream) =
    let flushStart = DateTime.UtcNow
    let coverageDocument = ReadXDocument coverageFile
    let root = coverageDocument.DocumentElement

    if format = ReportFormat.NCover then
      let startTimeAttr = root.GetAttribute("startTime")
      let measureTimeAttr = root.GetAttribute("measureTime")
      let oldStartTime = DateTime.ParseExact(startTimeAttr, "o", null)
      let oldMeasureTime = DateTime.ParseExact(measureTimeAttr, "o", null)
      startTime <- (if startTime < oldStartTime then startTime
                    else oldStartTime).ToUniversalTime() // Min
      measureTime <- (if measureTime > oldMeasureTime then measureTime
                      else oldMeasureTime).ToUniversalTime() // Max
      root.SetAttribute
        ("startTime",
         startTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture))
      root.SetAttribute
        ("measureTime",
         measureTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture))
      root.SetAttribute
        ("driverVersion",
         "AltCover.Recorder "
         + System.Diagnostics.FileVersionInfo.GetVersionInfo(
             System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion)

    let (m, i, m', s, v) = XmlByFormat format

    coverageDocument.SelectNodes(m)
    |> Seq.cast<XmlElement>
    |> Seq.map (fun el -> el.GetAttribute(i), el)
    |> Seq.filter (fun (k, _) -> counts.ContainsKey k)
    |> Seq.iter (fun (k, affectedModule) ->
         let moduleHits = counts.[k]
         // Don't do this in one leap like --
         // affectedModule.Descendants(XName.Get("seqpnt"))
         // Get the methods, then flip their
         // contents before concatenating
         let nn = affectedModule.SelectNodes(m')
         nn
         |> Seq.cast<XmlElement>
         |> Seq.collect (fun (method : XmlElement) ->
              s
              |> Seq.collect (fun (name, flag) ->
                   method.SelectNodes(name)
                   |> Seq.cast<XmlElement>
                   |> Seq.map (fun x -> (x, flag))
                   |> Seq.toList
                   |> List.rev))
         |> Seq.mapi (fun counter (pt, flag) ->
              ((match format with
                | ReportFormat.OpenCoverWithTracking | ReportFormat.OpenCover ->
                  "uspid"
                  |> pt.GetAttribute
                  |> (FindIndexFromUspid flag)
                | _ -> counter), pt))
         |> Seq.filter (fst >> moduleHits.ContainsKey)
         |> Seq.iter (fun x ->
              let pt = snd x
              let counter = fst x
              let vc =
                Int32.TryParse
                  (pt.GetAttribute(v), System.Globalization.NumberStyles.Integer,
                   System.Globalization.CultureInfo.InvariantCulture) |> snd
              // Treat -ve visit counts (an exemption added in analysis) as zero
              let c = moduleHits.[counter]
              let visits = (max 0 vc) + c.Count + c.Context.Length
              pt.SetAttribute(v, visits.ToString(CultureInfo.InvariantCulture))
              pointProcess.Invoke {Element = pt; Context = c.Context}))
    postProcess.Invoke coverageDocument

    // Save modified xml to a file
    outputFile.Seek(0L, SeekOrigin.Begin) |> ignore
    outputFile.SetLength 0L
    if own then WriteXDocument coverageDocument outputFile
    flushStart

  [<System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability",
                                                    "CA2000:DisposeObjectsBeforeLosingScope",
                                                    Justification = "'Target' is disposed")>]
  let internal DoFlush postProcess pointProcess own counts format report output =
    use coverageFile =
      new FileStream(report, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096,
                     FileOptions.SequentialScan)

    use target =
      match output with
      | None -> new MemoryStream() :> Stream
      | Some f ->
        new FileStream(f, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096,
                       FileOptions.SequentialScan) :> Stream

    let outputFile =
      if Option.isSome output then target
      else coverageFile :> Stream

    let flushStart =
      UpdateReport postProcess pointProcess own counts format coverageFile outputFile
    TimeSpan(DateTime.UtcNow.Ticks - flushStart.Ticks)

  let internal AddVisit (counts : Dictionary<string, Dictionary<int, PointVisits>>)
      moduleId hitPointId context =
    if not (counts.ContainsKey moduleId) then
      counts.[moduleId] <- Dictionary<int, PointVisits>()
    if not (counts.[moduleId].ContainsKey hitPointId) then
      counts.[moduleId].Add(hitPointId, (PointVisits.Default()))
    let c = counts.[moduleId].[hitPointId]
    counts.[moduleId].[hitPointId] <- c.Visit context