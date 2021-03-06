namespace AltCover

open System
open System.Diagnostics.CodeAnalysis
open System.Globalization
open System.IO
open System.Linq
open System.Reflection

open AltCover.Base
open Augment
open Mono.Cecil
open Mono.Options

[<ExcludeFromCodeCoverage>]
type internal AssemblyInfo =
  { Path : string
    Name : string
    Refs : string list }

module internal Main =
  let init() =
    CommandLine.error <- []
    CommandLine.dropReturnCode := false
    Visitor.inputDirectory <- None
    Visitor.outputDirectory <- None
    ProgramDatabase.SymbolFolders.Clear()
#if NETCOREAPP2_0
    Instrument.ResolutionTable.Clear()
#else
    Visitor.keys.Clear()
    Visitor.defaultStrongNameKey <- None
    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AltCover.Recorder.snk")
    use buffer = new MemoryStream()
    stream.CopyTo(buffer)
    let snk = StrongNameKeyPair(buffer.ToArray())
    Visitor.Add snk
    Visitor.recorderStrongNameKey <- Some (snk)
#endif
    Visitor.reportPath <- None
    Visitor.NameFilters.Clear()
    Visitor.interval <- None
    Visitor.TrackingNames.Clear()
    Visitor.reportFormat <- None
    Visitor.inplace := false
    Visitor.collect := false
    Visitor.single <- false
    Visitor.coverstyle <- CoverStyle.All
    Visitor.sourcelink := false

  let ValidateCallContext predicate x =
    if not (String.IsNullOrWhiteSpace x) then
      let k = x.Trim()
      if Char.IsDigit <| k.Chars(0) then
        if predicate || k.Length > 1 then
          CommandLine.error <- String.Format
                                 (CultureInfo.CurrentCulture,
                                  CommandLine.resources.GetString "InvalidValue",
                                  "--callContext", x) :: CommandLine.error
          (false, Left None)
        else
          let (ok, n) = Int32.TryParse(k)
          if ok then (ok, Left(Some(pown 10 (7 - n))))
          else
            CommandLine.error <- String.Format
                                   (CultureInfo.CurrentCulture,
                                    CommandLine.resources.GetString "InvalidValue",
                                    "--callContext", x) :: CommandLine.error
            (false, Left None)
      else (true, Right k)
    else
      CommandLine.error <- String.Format
                             (CultureInfo.CurrentCulture,
                              CommandLine.resources.GetString "InvalidValue",
                              "--callContext", x) :: CommandLine.error
      (false, Left None)

  let internal DeclareOptions() =
    [ ("i|inputDirectory=",
       (fun x ->
       if CommandLine.ValidateDirectory "--inputDirectory" x then
         if Option.isSome Visitor.inputDirectory then
           CommandLine.error <- String.Format
                                  (CultureInfo.CurrentCulture,
                                   CommandLine.resources.GetString "MultiplesNotAllowed",
                                   "--inputDirectory") :: CommandLine.error
         else Visitor.inputDirectory <- Some(Path.GetFullPath x)))

      ("o|outputDirectory=",
       (fun x ->
       if CommandLine.ValidatePath "--outputDirectory" x then
         if Option.isSome Visitor.outputDirectory then
           CommandLine.error <- String.Format
                                  (CultureInfo.CurrentCulture,
                                   CommandLine.resources.GetString "MultiplesNotAllowed",
                                   "--outputDirectory") :: CommandLine.error
         else
           CommandLine.doPathOperation
             (fun _ -> Visitor.outputDirectory <- Some(Path.GetFullPath x)) () false))

      ("y|symbolDirectory=",
       (fun x ->
       if CommandLine.ValidateDirectory "--symbolDirectory" x then
         ProgramDatabase.SymbolFolders.Add x))
#if NETCOREAPP2_0
      ("d|dependency=",
       (fun x ->
       let path =
         x
         |> Environment.ExpandEnvironmentVariables
         |> Path.GetFullPath

       let name, ok = CommandLine.ValidateAssembly "--dependency" path
       if ok then
         Instrument.ResolutionTable.[name] <- AssemblyDefinition.ReadAssembly path))
#else
      ("k|key=",
       (fun x ->
             let (pair, ok) = CommandLine.ValidateStrongNameKey "--key" x
             if ok then Visitor.Add pair))
      ("sn|strongNameKey=",
       (fun x ->
             let (pair, ok) = CommandLine.ValidateStrongNameKey "--strongNameKey" x
             if ok then
               if Option.isSome Visitor.defaultStrongNameKey then
                CommandLine.error <- String.Format(CultureInfo.CurrentCulture,
                                                CommandLine.resources.GetString "MultiplesNotAllowed",
                                                "--strongNameKey") :: CommandLine.error
               else Visitor.defaultStrongNameKey <- Some pair
                    Visitor.Add pair))
#endif

      ("x|xmlReport=",
       (fun x ->
       if CommandLine.ValidatePath "--xmlReport" x then
         if Option.isSome Visitor.reportPath then
           CommandLine.error <- String.Format
                                  (CultureInfo.CurrentCulture,
                                   CommandLine.resources.GetString "MultiplesNotAllowed",
                                   "--xmlReport") :: CommandLine.error
         else
           CommandLine.doPathOperation
             (fun () -> Visitor.reportPath <- Some(Path.GetFullPath x)) () false))
      ("f|fileFilter=",
       (fun x ->
       x
       |> CommandLine.ValidateRegexes
       |> Seq.iter (FilterClass.File >> Visitor.NameFilters.Add)))
      ("p|pathFilter=",
       (fun x ->
       x.Replace('\u0000', '\\')
       |> CommandLine.ValidateRegexes
       |> Seq.iter (FilterClass.Path >> Visitor.NameFilters.Add)))
      ("s|assemblyFilter=",
       (fun x ->
       x.Replace('\u0000', '\\')
       |> CommandLine.ValidateRegexes
       |> Seq.iter (FilterClass.Assembly >> Visitor.NameFilters.Add)))
      ("e|assemblyExcludeFilter=",
       (fun x ->
       x.Replace('\u0000', '\\')
       |> CommandLine.ValidateRegexes
       |> Seq.iter (FilterClass.Module >> Visitor.NameFilters.Add)))
      ("t|typeFilter=",
       (fun x ->
       x.Replace('\u0000', '\\')
       |> CommandLine.ValidateRegexes
       |> Seq.iter (FilterClass.Type >> Visitor.NameFilters.Add)))
      ("m|methodFilter=",
       (fun x ->
       x.Replace('\u0000', '\\')
       |> CommandLine.ValidateRegexes
       |> Seq.iter (FilterClass.Method >> Visitor.NameFilters.Add)))
      ("a|attributeFilter=",
       (fun x ->
       x.Replace('\u0000', '\\')
       |> CommandLine.ValidateRegexes
       |> Seq.iter (FilterClass.Attribute >> Visitor.NameFilters.Add)))
      ("c|callContext=",
       (fun x ->
       if Visitor.single then
         CommandLine.error <- String.Format
                                (CultureInfo.CurrentCulture,
                                 CommandLine.resources.GetString "Incompatible",
                                 "--single", "--callContext") :: CommandLine.error
       else
         let (ok, selection) = ValidateCallContext (Option.isSome Visitor.interval) x
         if ok then
           match selection with
           | Left n -> Visitor.interval <- n
           | Right name -> Visitor.TrackingNames.Add(name)))
      ("opencover",
       (fun _ ->
       if Option.isSome Visitor.reportFormat then
         CommandLine.error <- String.Format
                                (CultureInfo.CurrentCulture,
                                 CommandLine.resources.GetString "MultiplesNotAllowed",
                                 "--opencover") :: CommandLine.error
       else Visitor.reportFormat <- Some ReportFormat.OpenCover))
      (CommandLine.ddFlag "inplace" Visitor.inplace)
      (CommandLine.ddFlag "save" Visitor.collect)
      ("single",
       (fun _ ->
       if Visitor.single then
         CommandLine.error <- String.Format
                                (CultureInfo.CurrentCulture,
                                 CommandLine.resources.GetString "MultiplesNotAllowed",
                                 "--single") :: CommandLine.error
       else if Option.isSome Visitor.interval || Visitor.TrackingNames.Any() then
         CommandLine.error <- String.Format
                                (CultureInfo.CurrentCulture,
                                 CommandLine.resources.GetString "Incompatible",
                                 "--single", "--callContext") :: CommandLine.error
       else Visitor.single <- true))
      ("linecover",
       (fun _ ->
       match Visitor.coverstyle with
       | CoverStyle.LineOnly ->
         CommandLine.error <- String.Format
                                (CultureInfo.CurrentCulture,
                                 CommandLine.resources.GetString "MultiplesNotAllowed",
                                 "--linecover") :: CommandLine.error
       | CoverStyle.BranchOnly ->
         CommandLine.error <- String.Format
                                (CultureInfo.CurrentCulture,
                                 CommandLine.resources.GetString "Incompatible",
                                 "--linecover", "--branchcover") :: CommandLine.error
       | _ -> Visitor.coverstyle <- CoverStyle.LineOnly))
      ("branchcover",
       (fun _ ->
       match Visitor.coverstyle with
       | CoverStyle.BranchOnly ->
         CommandLine.error <- String.Format
                                (CultureInfo.CurrentCulture,
                                 CommandLine.resources.GetString "MultiplesNotAllowed",
                                 "--branchcover") :: CommandLine.error
       | CoverStyle.LineOnly ->
         CommandLine.error <- String.Format
                                (CultureInfo.CurrentCulture,
                                 CommandLine.resources.GetString "Incompatible",
                                 "--branchcover", "--linecover") :: CommandLine.error
       | _ -> Visitor.coverstyle <- CoverStyle.BranchOnly))
      (CommandLine.ddFlag "dropReturnCode" CommandLine.dropReturnCode)
      (CommandLine.ddFlag "sourcelink" Visitor.sourcelink)
      ("?|help|h", (fun x -> CommandLine.help <- not (isNull x)))

      ("<>",
       (fun x ->
       CommandLine.error <- String.Format
                              (CultureInfo.CurrentCulture,
                               CommandLine.resources.GetString "InvalidValue", "AltCover",
                               x) :: CommandLine.error)) ] // default end stop
    |> List.fold
         (fun (o : OptionSet) (p, a) ->
         o.Add(p, CommandLine.resources.GetString(p), new System.Action<string>(a)))
         (OptionSet())

  let internal ProcessOutputLocation(action : Either<string * OptionSet, string list * OptionSet>) =
    match action with
    | Right(rest, options) ->
      // Check that the directories are distinct
      let fromDirectory = Visitor.InputDirectory()
      let toDirectory = Visitor.OutputDirectory()
      if fromDirectory = toDirectory then
        CommandLine.error <- CommandLine.resources.GetString "NotInPlace"
                             :: CommandLine.error
      CommandLine.doPathOperation (fun () ->
        if !Visitor.inplace && CommandLine.error |> List.isEmpty
           && toDirectory |> Directory.Exists then
          CommandLine.error <- String.Format
                                 (CultureInfo.CurrentCulture,
                                  CommandLine.resources.GetString "SaveExists",
                                  toDirectory) :: CommandLine.error
        if CommandLine.error |> List.isEmpty then CommandLine.ensureDirectory toDirectory)
        () false
      if CommandLine.error
         |> List.isEmpty
         |> not
      then Left("UsageError", options)
      else
        if !Visitor.inplace then
          Output.Info
          <| String.Format
               (CultureInfo.CurrentCulture, (CommandLine.resources.GetString "savingto"),
                toDirectory)
          Output.Info
          <| String.Format
               (CultureInfo.CurrentCulture,
                (CommandLine.resources.GetString "instrumentingin"), fromDirectory)
        else
          Output.Info
          <| String.Format
               (CultureInfo.CurrentCulture,
                (CommandLine.resources.GetString "instrumentingfrom"), fromDirectory)
          Output.Info
          <| String.Format
               (CultureInfo.CurrentCulture,
                (CommandLine.resources.GetString "instrumentingto"), toDirectory)
        Right
          (rest, DirectoryInfo(fromDirectory), DirectoryInfo(toDirectory),
           DirectoryInfo(Visitor.SourceDirectory()))
    | Left intro -> Left intro

  let internal ImageLoadResilient (f : unit -> 'a) (tidy : unit -> 'a) =
    try
      f()
    with
    | :? BadImageFormatException
    | :? ArgumentException
    | :? IOException -> tidy()

  let internal PrepareTargetFiles (fromInfo : DirectoryInfo) (toInfo : DirectoryInfo)
      (sourceInfo : DirectoryInfo) =
    // Copy all the files into the target directory
    let files = fromInfo.GetFiles()
    files
    |> Seq.iter (fun info ->
         let fullName = info.FullName
         let filename = info.Name
         let copy = Path.Combine(toInfo.FullName, filename)
         File.Copy(fullName, copy, true))

    // Track the symbol-bearing assemblies
    let assemblies =
      sourceInfo.GetFiles()
      |> Seq.fold (fun (accumulator : AssemblyInfo list) info ->
           let fullName = info.FullName
           ImageLoadResilient (fun () ->
             use stream = File.OpenRead(fullName)
             use def = AssemblyDefinition.ReadAssembly(stream)
             let assemblyPdb = ProgramDatabase.GetPdbWithFallback def
             if def
                |> Visitor.IsIncluded
                |> Visitor.IsInstrumented
                && Option.isSome assemblyPdb
             then
               String.Format
                 (CultureInfo.CurrentCulture,
                  (CommandLine.resources.GetString "instrumenting"), fullName)
               |> Output.Info
               { Path = fullName
                 Name = def.Name.Name
                 Refs =
                   def.MainModule.AssemblyReferences
                   |> Seq.map (fun r -> r.Name)
                   |> Seq.toList }
               :: accumulator
             else accumulator) (fun () -> accumulator)) []

    // sort the assemblies into order so that the depended-upon are processed first
    let candidates =
      assemblies
      |> Seq.map (fun a -> a.Name)
      |> Seq.fold (fun (s : Set<string>) n -> Set.add n s) Set.empty<string>

    let simplified =
      assemblies
      |> List.map
           (fun a ->
           { a with Refs = a.Refs |> List.filter (fun n -> Set.contains n candidates) })

    let rec bundle unassigned unresolved collection n =
      match unassigned with
      | [] -> collection
      | _ ->
        let stage =
          (if n <= 1 then unassigned
           else unassigned |> List.filter (fun u -> u.Refs |> List.isEmpty))
          |> List.sortBy (fun u -> u.Name)

        let waiting = stage |> List.fold (fun s a -> Set.remove a.Name s) unresolved

        let next =
          unassigned
          |> List.filter (fun u ->
               u.Refs
               |> List.isEmpty
               |> not)
          |> List.map
               (fun a ->
               { a with Refs = a.Refs |> List.filter (fun n -> Set.contains n waiting) })
        bundle next waiting (stage :: collection) (n - 1)

    let sorted =
      bundle simplified candidates [] (simplified |> List.length)
      |> List.concat
      |> List.rev
      |> List.map (fun a -> (a.Path, a.Name))

    List.unzip sorted

  let internal DoInstrumentation arguments =
#if NETCOREAPP2_0
    let dotnetBuild =
      Assembly.GetEntryAssembly() // is null for unit tests
      |> Option.nullable
      |> Option.map (fun a -> Path.GetFileName(a.Location).Equals("MSBuild.dll"))
      |> Option.getOrElse false
#else
    let dotnetBuild = false
#endif
    let check1 =
      DeclareOptions()
      |> CommandLine.ParseCommandLine arguments
      |> CommandLine.ProcessHelpOption
      |> ProcessOutputLocation
    match check1 with
    | Left(intro, options) ->
      CommandLine.HandleBadArguments dotnetBuild arguments intro options
        (Runner.DeclareOptions())
      255
    | Right(rest, fromInfo, toInfo, targetInfo) ->
      let report = Visitor.ReportPath()

      let result =
        CommandLine.doPathOperation (fun () ->
          report
          |> Path.GetDirectoryName
          |> CommandLine.ensureDirectory
          let (assemblies, assemblyNames) = PrepareTargetFiles fromInfo toInfo targetInfo
          Output.Info
          <| String.Format
               (CultureInfo.CurrentCulture,
                (CommandLine.resources.GetString "reportingto"), report)
          let reporter, document =
            match Visitor.ReportKind() with
            | ReportFormat.OpenCover -> OpenCover.ReportGenerator()
            | _ -> Report.ReportGenerator()

          let visitors =
            [ reporter
              Instrument.InstrumentGenerator assemblyNames ]

          Visitor.Visit visitors (assemblies)
          document.Save(report)
          if !Visitor.collect then Runner.SetRecordToFile report
          CommandLine.ProcessTrailingArguments rest toInfo) 255 true
      CommandLine.ReportErrors "Instrumentation" (dotnetBuild && !Visitor.inplace)
      result

  let internal (|Select|_|) (pattern : String) offered =
    if offered
       |> String.IsNullOrWhiteSpace
       |> not
       && pattern.StartsWith(offered, StringComparison.OrdinalIgnoreCase)
    then Some offered
    else None

  let internal Main arguments =
    let first =
      arguments
      |> Seq.tryHead
      |> Option.getOrElse String.Empty
    init()
    match first with
    | Select "Runner" _ ->
      Runner.init()
      Runner.DoCoverage arguments (DeclareOptions())
    | Select "ipmo" _ ->
      Path.Combine
        (Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName,
         "AltCover.PowerShell.dll")
      |> Path.GetFullPath
      |> sprintf "Import-Module %A"
      |> (Output.Info)
      0
    | Select "version" _ ->
      Runner.WriteResourceWithFormatItems "AltCover.Version"
        [| AssemblyVersionInformation.AssemblyFileVersion |] false
      0
    | _ -> DoInstrumentation arguments

  // mocking point
  let mutable internal EffectiveMain = Main