namespace AltCover.Recorder

open System.Collections.Generic
open System.IO
open System.IO.Compression
open System

[<NoComparison>]
type Tracer =
  { Tracer : string
    Runner : bool
    Definitive : bool
    Stream : System.IO.Stream
    Formatter : System.IO.BinaryWriter }
#if NETSTANDARD2_0
  static member Core() =
    typeof<Microsoft.FSharp.Core.CompilationMappingAttribute>.Assembly.Location
#endif

  static member Create(name : string) =
    { Tracer = name
      Runner = false
      Definitive = false
      Stream = null
      Formatter = null }

  member this.IsConnected() =
    match this.Stream with
    | null -> false
    | _ -> this.Runner

  member internal this.SetSink s =
    { this with Stream = s
                Formatter = new BinaryWriter(s)
                Runner = true }

  member this.Connect() =
    if File.Exists this.Tracer then
      Seq.initInfinite (fun i -> Path.ChangeExtension(this.Tracer, sprintf ".%d.acv" i))
      |> Seq.filter (File.Exists >> not)
      |> Seq.map (fun f ->
           let fs = File.OpenWrite f
           let s = new DeflateStream(fs, CompressionMode.Compress)
           this.SetSink s)
      |> Seq.head
    else this

  member this.Close() = try
                          this.Stream.Flush()
                          this.Formatter.Close()
                        with
                        | :? ObjectDisposedException -> ()

  member internal this.Push (moduleId : string) (hitPointId : int) context =
    this.Formatter.Write moduleId
    this.Formatter.Write hitPointId
    match context with
    | Null -> this.Formatter.Write(Tag.Null |> byte)
    | Time t ->
      this.Formatter.Write(Tag.Time |> byte)
      this.Formatter.Write(t)
    | Call t ->
      this.Formatter.Write(Tag.Call |> byte)
      this.Formatter.Write(t)
    | Both pair ->
      this.Formatter.Write(Tag.Both |> byte)
      this.Formatter.Write(pair.Time)
      this.Formatter.Write(pair.Call)

  member internal this.CatchUp(visits : Dictionary<string, Dictionary<int, PointVisits>>) =
    let empty = Null
    visits.Keys
    |> Seq.iter (fun moduleId ->
         visits.[moduleId].Keys
         |> Seq.iter (fun hitPointId ->
              let c = visits.[moduleId].[hitPointId]
              let push = this.Push moduleId hitPointId
              [ seq { 1..c.Count } |> Seq.map (fun _ -> empty)
                c.Context |> List.toSeq ]
              |> Seq.concat
              |> Seq.iter push))
    visits.Clear()

  member this.OnStart() =
    let running =
      if this.Tracer <> "Coverage.Default.xml.acv" then this.Connect()
      else this
    { running with Definitive = true }

  member this.OnConnected f g =
    if this.IsConnected() then f()
    else g()

  member internal this.OnFinish visits =
    this.CatchUp visits
    this.Close()

  member internal this.OnVisit visits moduleId hitPointId context =
    this.CatchUp visits
    this.Push moduleId hitPointId context