#if NETCOREAPP2_0
namespace Tests.Shadow.Core
#else
#if NET4
namespace Tests.Shadow.Clr4
#else
#if NET2
namespace Tests.Shadow.Clr2
#else
#if MONO
namespace Tests.Shadow.Mono
#else
namespace Tests.Shadow.Unknown
#endif
#endif
#endif
#endif

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading
open System.Xml

open AltCover.Recorder
open AltCover.Shadow
open NUnit.Framework

[<TestFixture>]
type AltCoverTests() =
  class

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member private self.GetMyMethodName tag =
      ignore tag
//    let st = StackTrace(StackFrame(1))
//    st.GetFrame(0).GetMethod().Name |>
//#if NET2
//    printfn "%s %s 2" tag
//#else
//    printfn "%s %s" tag
//#endif

    member self.resource =
      Assembly.GetExecutingAssembly().GetManifestResourceNames()
      |> Seq.find (fun n -> n.EndsWith("SimpleCoverage.xml", StringComparison.Ordinal))

    member private self.UpdateReport a b =
      Counter.UpdateReport (PostProcessor ignore) (PointProcessor ignore) true a ReportFormat.NCover b b |> ignore

    member self.resource2 =
      Assembly.GetExecutingAssembly().GetManifestResourceNames()
      |> Seq.find
           (fun n ->
           n.EndsWith("Sample1WithModifiedOpenCover.xml", StringComparison.Ordinal))

    [<Test>]
    member self.SafeDisposalProtects() =
      let obj1 =
        { new System.IDisposable with
            member x.Dispose() = ObjectDisposedException("Bang!") |> raise }
      Assist.SafeDispose obj1
      Assert.Pass()

    [<Test>]
    member self.DefaultAccessorsBehaveAsExpected() =
      let v1 = DateTime.UtcNow.Ticks
      let probe = Instance.Clock()
      let v2 = DateTime.UtcNow.Ticks
      Assert.That(Instance.Granularity(), Is.EqualTo 0)
      Assert.That(probe, Is.GreaterThanOrEqualTo v1)
      Assert.That(probe, Is.LessThanOrEqualTo v2)

    [<Test>]
    member self.ShouldBeLinkingTheCorrectCopyOfThisCode() =
      self.GetMyMethodName "=>"
      let tracer =
        { Tracer = String.Empty
          Runner = false
          Definitive = false
          Stream = null
          Formatter = null }
      Assert.That(tracer.GetType().Assembly.GetName().Name, Is.EqualTo "AltCover.Shadow")
      self.GetMyMethodName "<="

    [<Test>]
    member self.OnlyNewIdPairShouldBeSampled() =
      self.GetMyMethodName "=>"
      lock Adapter.Lock (fun () ->
        try
          Adapter.SamplesClear()
          Assert.That(Adapter.AddSample "module" 23, Is.True)
          Assert.That(Adapter.AddSample "module" 24, Is.True)
          Assert.That(Adapter.AddSample "newmodule" 23, Is.True)
          Assert.That(Adapter.AddSample "module" 23, Is.False)
        finally
          Adapter.SamplesClear())
      self.GetMyMethodName "<="

    [<Test>]
    member self.NullIdShouldNotGiveACount() =
      self.GetMyMethodName "=>"
      lock Adapter.Lock (fun () ->
        try
          Adapter.VisitsClear()
          Adapter.VisitImplNone null 23
          Assert.That(Adapter.VisitsSeq(), Is.Empty)
        finally
          Adapter.VisitsClear())
      self.GetMyMethodName "<="

    [<Test>]
    member self.EmptyIdShouldNotGiveACount() =
      self.GetMyMethodName "=>"
      lock Adapter.Lock (fun () ->
        try
          Adapter.VisitsClear()
          Adapter.VisitImplNone String.Empty 23
          Assert.That(Adapter.VisitsSeq(), Is.Empty)
        finally
          Adapter.VisitsClear())
      self.GetMyMethodName "<="

    member self.RealIdShouldIncrementCount() =
      self.GetMyMethodName "=>"
      lock Adapter.Lock (fun () ->
        let save = Instance.trace
        try
          Adapter.VisitsClear()
          Instance.trace <- { Tracer = null
                              Stream = null
                              Formatter = null
                              Runner = false
                              Definitive = false }
          let key = " "
          Instance.Visit key 23
          Assert.That(Adapter.VisitsSeq() |> Seq.length, Is.EqualTo 1)
          Assert.That(Adapter.VisitsEntrySeq key |> Seq.length, Is.EqualTo 1)
          Assert.That(Adapter.VisitCount key 23, Is.EqualTo 1)
        finally
          Adapter.VisitsClear()
          Instance.trace <- save)
      self.GetMyMethodName "<="

#if NET4
    // passing lambdas or other F# types across the CLR divide doesn't work
#else
    [<Test>]
    member self.JunkUspidGivesNegativeIndex() =
      let key = " "
      let index = Counter.FindIndexFromUspid 0 key
      Assert.That(index, Is.LessThan 0)

    [<Test>]
    member self.PayloadGeneratedIsAsExpected() =
      try
        Assert.That (Instance.CallerId(), Is.EqualTo 0)
        Assert.That(Instance.PayloadSelector (fun _ -> true),
                    Is.EqualTo Null)
        Instance.Push 4321
        Assert.That(Instance.PayloadSelector (fun _ -> true),
                    Is.EqualTo (Call 4321))
        try
          Instance.Push 6789
          // 0x1234123412341234 == 1311693406324658740
          Assert.That(Instance.PayloadSelection (fun _ -> 0x1234123412341234L) (fun _ -> 1000L) (fun _ -> true),
                    Is.EqualTo (Both { Time = 1311693406324658000L; Call = 6789 }))
        finally
          Instance.Pop()
        Assert.That(Instance.PayloadSelector (fun _ -> true),
                    Is.EqualTo (Call 4321))
      finally
        Instance.Pop()
      Assert.That(Instance.PayloadSelection (fun _ -> 0x1234123412341234L) (fun _ -> 1000L) (fun _ -> true) ,
                Is.EqualTo (Time 1311693406324658000L))
      let v1 = DateTime.UtcNow.Ticks
      let probed = Instance.PayloadControl (fun _ -> 1000L) (fun _ -> true)
      let v2 = DateTime.UtcNow.Ticks
      match probed with
      | Time probe ->
        Assert.That(probe % 1000L, Is.EqualTo 0L)
        Assert.That(probe, Is.LessThanOrEqualTo v2)
        Assert.That(probe, Is.GreaterThanOrEqualTo (1000L*(v1/1000L)))
      | _ -> Assert.Fail()
      Assert.That (Instance.CallerId(), Is.EqualTo 0)
      Instance.Pop()
      Assert.That (Instance.CallerId(), Is.EqualTo 0)

    [<Test>]
    member self.RealIdShouldIncrementCountSynchronously() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      Instance.Capacity <- 0
      let save = Instance.trace
      try
        Instance.Visits.Clear()
        Instance.trace <- { Tracer=null; Stream=null; Formatter=null;
                            Runner = false; Definitive = false }
        let key = " "
        Instance.VisitSelection Null key 23
        Assert.That (Instance.Visits.Count, Is.EqualTo 1, "A visit that should have happened, didn't")
        Assert.That (Instance.Visits.[key].Count, Is.EqualTo 1, "keys = " + String.Join("; ", Instance.Visits.Keys|> Seq.toArray))
        let v = PointVisits.Default().Visit Null
        Assert.That (Instance.Visits.[key].[23], Is.EqualTo v)
      finally
        Instance.Visits.Clear()
        Instance.trace <- save)
      self.GetMyMethodName "<="

    [<Test>]
    member self.DistinctIdShouldBeDistinct() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      try
        Instance.Visits.Clear()
        let key = " "
        Instance.VisitImpl key 23 Null
        Instance.VisitImpl "key" 42 Null
        Assert.That (Instance.Visits.Count, Is.EqualTo 2)
      finally
        Instance.Visits.Clear())
      self.GetMyMethodName "<="

    [<Test>]
    member self.DistinctLineShouldBeDistinct() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      try
        Instance.Visits.Clear()
        let key = " "
        Instance.VisitImpl key 23 Null
        Instance.VisitImpl key 42 Null
        Assert.That (Instance.Visits.Count, Is.EqualTo 1)
        Assert.That (Instance.Visits.[key].Count, Is.EqualTo 2)
      finally
        Adapter.VisitsClear())
      self.GetMyMethodName "<="

    [<Test>]
    member self.RepeatVisitsShouldIncrementCount() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      try
        Instance.Visits.Clear()
        let key = " "
        Instance.VisitImpl key 23 Null
        Instance.VisitImpl key 23 Null
        let v = (PointVisits.Default().Visit Null).Visit Null
        Assert.That (Instance.Visits.[key].[23], Is.EqualTo v)
      finally
        Instance.Visits.Clear())
      self.GetMyMethodName "<="

    [<Test>]
    member self.RepeatVisitsShouldIncrementTotal() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      try
        Instance.Visits.Clear()
        let key = " "
        let payload = Time DateTime.UtcNow.Ticks
        Instance.VisitImpl key 23 Null
        Instance.VisitImpl key 23 payload
        let v = (PointVisits.Default().Visit Null).Visit payload
        Assert.That (Instance.Visits.[key].[23], Is.EqualTo v)
      finally
        Instance.Visits.Clear())
      self.GetMyMethodName "<="

    [<Test>]
    member self.OldDocumentStartIsNotUpdated() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      let epoch = DateTime.UtcNow
      Counter.startTime <- epoch
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      let before = XmlDocument()
      before.Load (Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource))
      self.UpdateReport (Dictionary<string, Dictionary<int, PointVisits>>()) worker
      worker.Position <- 0L
      let after = XmlDocument()
      after.Load worker
      let startTimeAttr = after.DocumentElement.GetAttribute("startTime")
      let startTime = DateTime.ParseExact(startTimeAttr, "o", null)
      Assert.That (startTime.ToUniversalTime(), Is.LessThan epoch)
      Assert.That (startTime.ToUniversalTime(), Is.EqualTo (Counter.startTime.ToUniversalTime())))
      self.GetMyMethodName "<="

    [<Test>]
    member self.NewDocumentStartIsMadeEarlier() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      let epoch = DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
      Counter.startTime <- epoch
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      let before = XmlDocument()
      before.Load (Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource))
      self.UpdateReport (Dictionary<string, Dictionary<int, PointVisits>>()) worker
      worker.Position <- 0L
      let after = XmlDocument()
      after.Load worker
      let startTimeAttr = after.DocumentElement.GetAttribute("startTime")
      let startTime = DateTime.ParseExact(startTimeAttr, "o", null)
      Assert.That (startTime.ToUniversalTime(), Is.EqualTo epoch)
      Assert.That (startTime.ToUniversalTime(), Is.EqualTo (Counter.startTime.ToUniversalTime())))
      self.GetMyMethodName "<="

    [<Test>]
    member self.NewDocumentMeasureIsNotMadeEarlier() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      let epoch = DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
      Counter.measureTime <- epoch
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      let before = XmlDocument()
      before.Load (Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource))
      self.UpdateReport (Dictionary<string, Dictionary<int, PointVisits>>()) worker
      worker.Position <- 0L
      let after = XmlDocument()
      after.Load worker
      let startTimeAttr = after.DocumentElement.GetAttribute("measureTime")
      let startTime = DateTime.ParseExact(startTimeAttr, "o", null)
      Assert.That (startTime.ToUniversalTime(), Is.GreaterThan epoch)
      Assert.That (startTime.ToUniversalTime(), Is.EqualTo (Counter.measureTime.ToUniversalTime())))
      self.GetMyMethodName "<="

    [<Test>]
    member self.OldDocumentMeasureIsUpdated() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      let epoch = DateTime.UtcNow
      Counter.measureTime <- epoch
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      let before = XmlDocument()
      before.Load (Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource))
      self.UpdateReport (Dictionary<string, Dictionary<int, PointVisits>>()) worker
      worker.Position <- 0L
      let after = XmlDocument()
      after.Load worker
      let startTimeAttr = after.DocumentElement.GetAttribute("measureTime")
      let startTime = DateTime.ParseExact(startTimeAttr, "o", null)
      Assert.That (startTime.ToUniversalTime(), Is.EqualTo epoch)
      Assert.That (startTime.ToUniversalTime(), Is.EqualTo (Counter.measureTime.ToUniversalTime())))
      self.GetMyMethodName "<="

    [<Test>]
    member self.UnknownModuleMakesNoChange() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      use before = new StreamReader (Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource))
      let item = Dictionary<string, Dictionary<int, PointVisits>>()
      item.Add ("not a guid", null)
      self.UpdateReport item worker
      worker.Position <- 0L
      let after = new StreamReader(worker)
      let result = after.ReadToEnd().Split([|'\r';'\n'|], StringSplitOptions.RemoveEmptyEntries)
                   |> Seq.skip 3
      let expected = before.ReadToEnd().Split([|'\r';'\n'|], StringSplitOptions.RemoveEmptyEntries)
                   |> Seq.skip 3
      Assert.That (result,
                   Is.EquivalentTo expected))
      self.GetMyMethodName "<="

    [<Test>]
    member self.KnownModuleWithNothingMakesNoChange() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      use before = new StreamReader (Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource))
      let item = Dictionary<string, Dictionary<int, PointVisits>>()
      item.Add("f6e3edb3-fb20-44b3-817d-f69d1a22fc2f", Dictionary<int,PointVisits>())
      self.UpdateReport item worker
      worker.Position <- 0L
      let after = new StreamReader(worker)
      let result = after.ReadToEnd().Split([|'\r';'\n'|], StringSplitOptions.RemoveEmptyEntries)
                   |> Seq.skip 3
      let expected = before.ReadToEnd().Split([|'\r';'\n'|], StringSplitOptions.RemoveEmptyEntries)
                   |> Seq.skip 3
      Assert.That (result,
                   Is.EquivalentTo expected))
      self.GetMyMethodName "<="

    [<Test>]
    member self.KnownModuleWithNothingInRangeMakesNoChange() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      use before = new StreamReader (Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource))
      let payload = Dictionary<int,PointVisits>()
      payload.[-1] <- { Count = 10; Context = [] }
      payload.[100] <- { Count = 10; Context = [] }
      let item = Dictionary<string, Dictionary<int, PointVisits>>()
      item.Add("f6e3edb3-fb20-44b3-817d-f69d1a22fc2f", payload)
      self.UpdateReport item worker
      worker.Position <- 0L
      let after = new StreamReader(worker)
      let result = after.ReadToEnd().Split([|'\r';'\n'|], StringSplitOptions.RemoveEmptyEntries)
                   |> Seq.skip 3
      let expected = before.ReadToEnd().Split([|'\r';'\n'|], StringSplitOptions.RemoveEmptyEntries)
                   |> Seq.skip 3
      Assert.That (result,
                   Is.EquivalentTo expected))
      self.GetMyMethodName "<="

    [<Test>]
    member self.KnownModuleWithPayloadMakesExpectedChange() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      let payload = Dictionary<int,PointVisits>()
      [0..9 ]
      |> Seq.iter(fun i -> payload.[i] <- { Count = i + 1; Context = [] })
      let item = Dictionary<string, Dictionary<int, PointVisits>>()
      item.Add("f6e3edb3-fb20-44b3-817d-f69d1a22fc2f", payload)
      self.UpdateReport item worker
      worker.Position <- 0L
      let after = XmlDocument()
      after.Load worker
      Assert.That( after.SelectNodes("//seqpnt")
                   |> Seq.cast<XmlElement>
                   |> Seq.map (fun x -> x.GetAttribute("visitcount")),
                   Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1"]))
      self.GetMyMethodName "<="

    [<Test>]
    member self.KnownModuleWithPayloadMakesExpectedChangeInOpenCover() =
      self.GetMyMethodName "=>"
      lock Instance.Visits (fun () ->
      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource2)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      use worker = new MemoryStream()
      worker.Write (buffer, 0, size)
      worker.Position <- 0L
      let payload = Dictionary<int,PointVisits>()
      [0..9 ]
      |> Seq.iter(fun i -> payload.[10 - i] <- { Count = i + 1; Context = [] })
      [11..12]
      |> Seq.iter(fun i -> payload.[i ||| Counter.BranchFlag] <- { Count = i - 10; Context = [] })
      let item = Dictionary<string, Dictionary<int, PointVisits>>()
      item.Add("7C-CD-66-29-A3-6C-6D-5F-A7-65-71-0E-22-7D-B2-61-B5-1F-65-9A", payload)
      Counter.UpdateReport (PostProcessor ignore) (PointProcessor ignore) true item ReportFormat.OpenCover worker worker |> ignore
      worker.Position <- 0L
      let after = XmlDocument()
      after.Load worker
      Assert.That( after.SelectNodes("//SequencePoint")
                   |> Seq.cast<XmlElement>
                   |> Seq.map (fun x -> x.GetAttribute("vc")),
                   Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1"])
      Assert.That( after.SelectNodes("//BranchPoint")
                   |> Seq.cast<XmlElement>
                   |> Seq.map (fun x -> x.GetAttribute("vc")),
                   Is.EquivalentTo [ "2"; "2"]))
      self.GetMyMethodName "<="
#endif

    [<Test>]
    member self.EmptyFlushLeavesNoTrace() =
      self.GetMyMethodName "=>"
      lock Adapter.Lock (fun () ->
        let saved = Console.Out
        try
          Adapter.VisitsClear()
          use stdout = new StringWriter()
          Console.SetOut stdout
          Instance.FlushAll()
          Assert.That(stdout.ToString(), Is.Empty)
        finally
          Adapter.VisitsClear()
          Console.SetOut saved)
      self.GetMyMethodName "<="

    member self.PauseLeavesExpectedTraces() =
      self.GetMyMethodName "=>"
      lock Adapter.Lock (fun () ->
        try
          let saved = Console.Out
          let here = Directory.GetCurrentDirectory()
          let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
          let unique = Path.Combine(where, Guid.NewGuid().ToString())
          let save = Instance.trace
          Instance.trace <- { Tracer = null
                              Stream = null
                              Formatter = null
                              Runner = false
                              Definitive = false }
          try
            Adapter.VisitsClear()
            use stdout = new StringWriter()
            Console.SetOut stdout
            Directory.CreateDirectory(unique) |> ignore
            Directory.SetCurrentDirectory(unique)
            Counter.measureTime <- DateTime.ParseExact
                                     ("2017-12-29T16:33:40.9564026+00:00", "o", null)
            use stream =
              Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
            let size = int stream.Length
            let buffer = Array.create size 0uy
            Assert.That(stream.Read(buffer, 0, size), Is.EqualTo size)
            do use worker = new FileStream(Instance.ReportFile, FileMode.CreateNew)
               worker.Write(buffer, 0, size)
               ()
            [ 0..9 ]
            |> Seq.iter
                 (fun i ->
                 Adapter.VisitsAdd "f6e3edb3-fb20-44b3-817d-f69d1a22fc2f" i (i + 1))
            Adapter.DoPause()
            let head = "Coverage statistics flushing took "
            let tail = " seconds\n"
            let recorded = stdout.ToString().Replace("\r\n", "\n")
            let index1 = recorded.IndexOf(head, StringComparison.Ordinal)
            let index2 = recorded.IndexOf(tail, StringComparison.Ordinal)
            Assert.That(index1, Is.GreaterThanOrEqualTo 0, recorded)
            Assert.That(index2, Is.GreaterThan index1, recorded)
            use worker' = new FileStream(Instance.ReportFile, FileMode.Open)
            let after = XmlDocument()
            after.Load worker'
            Assert.That
              (after.SelectNodes("//seqpnt")
               |> Seq.cast<XmlElement>
               |> Seq.map (fun x -> x.GetAttribute("visitcount")),
               Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1" ])
          finally
            Instance.trace <- save
            if File.Exists Instance.ReportFile then File.Delete Instance.ReportFile
            Adapter.VisitsClear()
            Console.SetOut saved
            Directory.SetCurrentDirectory(here)
            try
              Directory.Delete(unique)
            with :? IOException -> ()
        with :? AbandonedMutexException -> Instance.mutex.ReleaseMutex())
      self.GetMyMethodName "<="

    member self.ResumeLeavesExpectedTraces() =
      self.GetMyMethodName "=>"
      lock Adapter.Lock (fun () ->
        try
          let saved = Console.Out
          let here = Directory.GetCurrentDirectory()
          let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
          let unique = Path.Combine(where, Guid.NewGuid().ToString())
          let save = Instance.trace

          let newTrace =
            { Tracer = null
              Stream = null
              Formatter = null
              Runner = false
              Definitive = false }
          Instance.trace <- newTrace
          try
            Adapter.VisitsClear()
            use stdout = new StringWriter()
            Console.SetOut stdout
            Directory.CreateDirectory(unique) |> ignore
            Directory.SetCurrentDirectory(unique)
            Counter.measureTime <- DateTime.ParseExact
                                     ("2017-12-29T16:33:40.9564026+00:00", "o", null)
            use stream =
              Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
            let size = int stream.Length
            let buffer = Array.create size 0uy
            Assert.That(stream.Read(buffer, 0, size), Is.EqualTo size)
            do use worker = new FileStream(Instance.ReportFile, FileMode.CreateNew)
               worker.Write(buffer, 0, size)
               ()
            [ 0..9 ]
            |> Seq.iter
                 (fun i ->
                 Adapter.VisitsAdd "f6e3edb3-fb20-44b3-817d-f69d1a22fc2f" i (i + 1))
            Adapter.DoResume()
            Assert.That(Adapter.VisitsSeq(), Is.Empty, "Visits should be cleared")
            Assert.That
              (Object.ReferenceEquals(Instance.trace, newTrace), Is.False,
               "trace should be replaced")
            let recorded = stdout.ToString().Trim()
            Assert.That(recorded, Is.EqualTo "Resuming...", recorded)
            use worker' = new FileStream(Instance.ReportFile, FileMode.Open)
            let after = XmlDocument()
            after.Load worker'
            Assert.That
              (after.SelectNodes("//seqpnt")
               |> Seq.cast<XmlElement>
               |> Seq.map (fun x -> x.GetAttribute("visitcount")),
               Is.EquivalentTo
                 [ "1"; "1"; "1"; "1"; "1"; "1"; "0"; String.Empty; "X"; "-1" ])
          finally
            Instance.trace <- save
            if File.Exists Instance.ReportFile then File.Delete Instance.ReportFile
            Adapter.VisitsClear()
            Console.SetOut saved
            Directory.SetCurrentDirectory(here)
            try
              Directory.Delete(unique)
            with :? IOException -> ()
        with :? AbandonedMutexException -> Instance.mutex.ReleaseMutex())
      self.GetMyMethodName "<="

    member self.FlushLeavesExpectedTraces() =
      self.GetMyMethodName "=>"
      lock Adapter.Lock (fun () ->
        try
          let saved = Console.Out
          let here = Directory.GetCurrentDirectory()
          let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
          let unique = Path.Combine(where, Guid.NewGuid().ToString())
          let save = Instance.trace
          Instance.trace <- { Tracer = null
                              Stream = null
                              Formatter = null
                              Runner = false
                              Definitive = false }
          try
            Adapter.VisitsClear()
            use stdout = new StringWriter()
            Console.SetOut stdout
            Directory.CreateDirectory(unique) |> ignore
            Directory.SetCurrentDirectory(unique)
            Counter.measureTime <- DateTime.ParseExact
                                     ("2017-12-29T16:33:40.9564026+00:00", "o", null)
            use stream =
              Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
            let size = int stream.Length
            let buffer = Array.create size 0uy
            Assert.That(stream.Read(buffer, 0, size), Is.EqualTo size)
            do use worker = new FileStream(Instance.ReportFile, FileMode.CreateNew)
               worker.Write(buffer, 0, size)
               ()
            [ 0..9 ]
            |> Seq.iter
                 (fun i ->
                 Adapter.VisitsAdd "f6e3edb3-fb20-44b3-817d-f69d1a22fc2f" i (i + 1))
            Instance.FlushCounter ProcessExit ()
            let head = "Coverage statistics flushing took "
            let tail = " seconds\n"
            let recorded = stdout.ToString().Replace("\r\n", "\n")
            let index1 = recorded.IndexOf(head, StringComparison.Ordinal)
            let index2 = recorded.IndexOf(tail, StringComparison.Ordinal)
            Assert.That(index1, Is.GreaterThanOrEqualTo 0, recorded)
            Assert.That(index2, Is.GreaterThan index1, recorded)
            use worker' = new FileStream(Instance.ReportFile, FileMode.Open)
            let after = XmlDocument()
            after.Load worker'
            Assert.That
              (after.SelectNodes("//seqpnt")
               |> Seq.cast<XmlElement>
               |> Seq.map (fun x -> x.GetAttribute("visitcount")),
               Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1" ])
          finally
            Instance.trace <- save
            if File.Exists Instance.ReportFile then File.Delete Instance.ReportFile
            Adapter.VisitsClear()
            Console.SetOut saved
            Directory.SetCurrentDirectory(here)
            try
              Directory.Delete(unique)
            with :? IOException -> ()
        with :? AbandonedMutexException -> Instance.mutex.ReleaseMutex())
      self.GetMyMethodName "<="
#if NET4
#else
    [<Test>]
#endif
    member self.FlushLeavesExpectedTracesWhenDiverted() =
      let saved = Console.Out
      let here = Directory.GetCurrentDirectory()
      let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
      let unique = Path.Combine(where, Guid.NewGuid().ToString())
      let reportFile = Path.Combine(unique, "FlushLeavesExpectedTraces.xml")
      let outputFile = Path.Combine(unique, "FlushLeavesExpectedTracesWhenDiverted.xml")
      try
        let visits = new Dictionary<string, Dictionary<int, PointVisits>>()
        use stdout = new StringWriter()
        Console.SetOut stdout
        Directory.CreateDirectory(unique) |> ignore
        Directory.SetCurrentDirectory(unique)
        Counter.measureTime <- DateTime.ParseExact
                                 ("2017-12-29T16:33:40.9564026+00:00", "o", null)
        use stream =
          Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
        let size = int stream.Length
        let buffer = Array.create size 0uy
        Assert.That(stream.Read(buffer, 0, size), Is.EqualTo size)
        do use worker = new FileStream(reportFile, FileMode.CreateNew)
           worker.Write(buffer, 0, size)
           ()
        let payload = Dictionary<int, PointVisits>()
        [ 0..9 ] |> Seq.iter (fun i -> payload.[i] <- { Count = i + 1; Context = [] })
        visits.["f6e3edb3-fb20-44b3-817d-f69d1a22fc2f"] <- payload
        Counter.DoFlush (PostProcessor ignore) (PointProcessor ignore) true visits
          AltCover.Recorder.ReportFormat.NCover reportFile (Some outputFile) |> ignore
        use worker' = new FileStream(outputFile, FileMode.Open)
        let after = XmlDocument()
        after.Load worker'
        Assert.That
          (after.SelectNodes("//seqpnt")
           |> Seq.cast<XmlElement>
           |> Seq.map (fun x -> x.GetAttribute("visitcount")),
           Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1" ])
      finally
        if File.Exists reportFile then File.Delete reportFile
        Console.SetOut saved
        Directory.SetCurrentDirectory(here)
        try
          Directory.Delete(unique)
        with :? IOException -> ()

#if NET2
#else
    // Dead simple sequential operation
    // run only once in Framework mode to avoid contention
    [<Test>]
    member self.MailboxFunctionsAsExpected() =
      Instance.Capacity <- 0
      self.RealIdShouldIncrementCount()
      self.PauseLeavesExpectedTraces()
      self.ResumeLeavesExpectedTraces()
      self.FlushLeavesExpectedTraces()
#endif

  end