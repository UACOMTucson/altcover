﻿namespace AltCover.Commands

open System
open System.Diagnostics.CodeAnalysis
open System.IO
open System.Linq
open System.Management.Automation
open System.Xml
open System.Xml.XPath

open AltCover.PowerShell

[<System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.PowerShell",
  "PS1101:AllCmdletsShouldAcceptPipelineInput", 
  Justification = "TODO")>]
[<Cmdlet(VerbsData.ConvertTo, "SourceMap")>]
[<OutputType(typeof<XmlDocument>)>]
[<SuppressMessage("Microsoft.PowerShell", "PS1008", Justification = "Cobertura is OK")>]
type ConvertToSourceMapCommand(outputFile:String) =
  inherit PSCmdlet()

  new () = ConvertToSourceMapCommand(String.Empty)

  [<Parameter(Mandatory = false,
      ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)>]
  member val OutputFile:string = outputFile with get, set

[<Cmdlet(VerbsData.ConvertTo, "BarChart")>]
[<OutputType(typeof<XmlDocument>)>]
[<SuppressMessage("Microsoft.PowerShell", "PS1008", Justification = "Cobertura is OK")>]
type ConvertToBarChartCommand(outputFile:String) =
  inherit PSCmdlet()

  new () = ConvertToBarChartCommand(String.Empty)

  [<SuppressMessage("Microsoft.Design", "CA1059", Justification="converts concrete types")>]
  [<Parameter(ParameterSetName = "XmlDoc", Mandatory = true, Position = 1,
      ValueFromPipeline = true, ValueFromPipelineByPropertyName = false)>]
  member val XmlDocument:IXPathNavigable = null with get, set

  [<Parameter(ParameterSetName = "FromFile", Mandatory = true, Position = 1,
      ValueFromPipeline = true, ValueFromPipelineByPropertyName = false)>]
  member val InputFile:string = null with get, set

  [<Parameter(ParameterSetName = "XmlDoc", Mandatory = false, Position = 2,
      ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)>]
  [<Parameter(ParameterSetName = "FromFile", Mandatory = false, Position = 2,
      ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)>]
  member val OutputFile:string = outputFile with get, set

  member private self.DoConvert() =
      let navigator = self.XmlDocument.CreateNavigator()
      let format = if navigator.Select("/CoverageSession").OfType<XPathNavigator>().Any() then
                    AltCover.Base.ReportFormat.OpenCover
                   else AltCover.Base.ReportFormat.NCover

      let intermediate = if format = AltCover.Base.ReportFormat.NCover then self.XmlDocument
                         else
                          let modify = XmlUtilities.LoadTransform "OpenCoverToNCoverEx"
                          let temp = XmlDocument()
                          do
                            use feed = temp.CreateNavigator().AppendChild()
                            modify.Transform (self.XmlDocument, feed)
                          temp :> IXPathNavigable

      let transform = XmlUtilities.LoadTransform "NCoverToBarChart"
      let rewrite = XmlDocument()
      do
        use output = rewrite.CreateNavigator().AppendChild()
        transform.Transform (intermediate, output)

      rewrite.DocumentElement.SelectNodes("//script[@language='JavaScript']").OfType<XmlNode>()
      |> Seq.iter(fun n -> let text = n.InnerText
                           let cdata = rewrite.CreateCDataSection(text)
                           n.InnerText <- "//"
                           n.AppendChild cdata |> ignore)

      let doctype = rewrite.CreateDocumentType("html", null, null, null)
      rewrite.PrependChild(doctype) |> ignore
      XmlUtilities.PrependDeclaration rewrite
      rewrite

  override self.ProcessRecord() =
    let here = Directory.GetCurrentDirectory()
    try
      let where = self.SessionState.Path.CurrentLocation.Path
      Directory.SetCurrentDirectory where
      if self.ParameterSetName = "FromFile" then
        self.XmlDocument <- XPathDocument self.InputFile

      let rewrite = self.DoConvert()

      if self.OutputFile |> String.IsNullOrWhiteSpace |> not then
        rewrite.Save(self.OutputFile)

      self.WriteObject rewrite
    finally
      Directory.SetCurrentDirectory here