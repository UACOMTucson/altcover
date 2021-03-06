#if RUNNER
namespace AltCover.Fake

open System
open System.IO
open System.Reflection
open AltCover

module Trace =
  open Fake.Core

  let Create() =
    FSApi.Logging.Primitive { Primitive.Logging.Create() with Info = Trace.trace
                                                              Warn = Trace.traceImportant
                                                              Error = Trace.traceError
                                                              Echo = Trace.traceVerbose }

  let internal DoDefault(log : FSApi.Logging option) =
    match log with
    | Some logging -> logging
    | None -> Create()

[<NoComparison>]
type Implementation =
  | DotNetCore
  | Framework

type Api =
  static member Prepare(args : FSApi.PrepareParams, ?log : FSApi.Logging) =
    AltCover.Api.Prepare args (Trace.DoDefault log)
  static member Collect(args : FSApi.CollectParams, ?log : FSApi.Logging) =
    AltCover.Api.Collect args (Trace.DoDefault log)
  static member Ipmo() = AltCover.Api.Ipmo()
  static member Version() = AltCover.Api.Version()
  // Finds the tool from within the .nuget package
  static member toolPath toolType =
    let here = Assembly.GetExecutingAssembly().Location
    let root = Path.Combine(Path.GetDirectoryName here, "../..")

    let target =
      match toolType with
      | Framework _ -> "AltCover.exe"
      | _ -> "AltCover.dll"
    match Directory.GetFiles(root, target, SearchOption.AllDirectories)
          |> Seq.filter (fun f ->
               let coretype =
                 f
                 |> Path.GetDirectoryName
                 |> Path.GetFileName
               coretype.StartsWith("netstandard", StringComparison.Ordinal) |> not)
          |> Seq.tryHead with
    | Some path -> path
    | None -> String.Empty
#else
namespace AltCover_Fake.DotNet

open System
open System.Reflection
open AltCover_Fake.DotNet
#endif

module DotNet =
  open Fake.DotNet

  type DotNet.TestOptions with

    member private self.ExtendCustomParams options =
      let custom = self.Common.CustomParams

      let extended =
        match custom with
        | None -> Some options
        | Some thing -> Some(thing + " " + options)

      // the constructors are version dependent
      let OptionsConstructor = self.Common.GetType().GetConstructors().[0]

      let args =
        OptionsConstructor.GetParameters()
        |> Array.map (fun info ->
             let t = info.ParameterType
             if t.GetTypeInfo().IsValueType then Activator.CreateInstance(t)
             else null)

      let common = OptionsConstructor.Invoke(args)
      self.Common.GetType().GetFields(BindingFlags.NonPublic ||| BindingFlags.Instance)
      |> Array.iter (fun f ->
           f.SetValue(common,
                      (if f.Name <> "CustomParams@" then f.GetValue self.Common
                       else extended :> obj)))
      let TestOptionsConstructor = self.GetType().GetConstructors().[0]

      let args' =
        TestOptionsConstructor.GetParameters()
        |> Array.map (fun info ->
             let t = info.ParameterType
             if t.GetTypeInfo().IsValueType then Activator.CreateInstance(t)
             else null)

      let result = TestOptionsConstructor.Invoke(args')
      self.GetType().GetFields(BindingFlags.NonPublic ||| BindingFlags.Instance)
      |> Array.iter (fun f ->
           f.SetValue(result,
                      (if f.Name <> "Common@" then f.GetValue self
                       else common)))
      result :?> DotNet.TestOptions

#if RUNNER
    member self.WithParameters (prepare : FSApi.PrepareParams)
                               (collect : FSApi.CollectParams)
                               (force : DotNet.CLIArgs) =
      DotNet.ToTestArguments
#else
    member self.WithParameters (prepare : AltCover_Fake.DotNet.Testing.AltCover.PrepareParams)
           (collect : AltCover_Fake.DotNet.Testing.AltCover.CollectParams)
           (force : AltCover_Fake.DotNet.Testing.DotNet.CLIArgs) =
      AltCover_Fake.DotNet.Testing.Internals.ToTestArguments
#endif
        prepare collect force |> self.ExtendCustomParams
    member self.WithImportModule() = self.ExtendCustomParams "/p:AltCoverIpmo=true"
    member self.WithGetVersion() = self.ExtendCustomParams "/p:AltCoverGetVersion=true"