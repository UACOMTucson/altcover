namespace AltCover.Avalonia

open System
open System.Globalization
open System.Linq
open System.Resources

open AltCover.Augment
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Html
open Avalonia.Markup.Xaml
open ReactiveUI

module UICommon =

    let GetResourceString (key:string) =
        let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()
        let resources = new ResourceManager("AltCover.Visualizer.Strings", executingAssembly)
        resources.GetString(key)

type MainWindowViewModel  () =
    inherit ReactiveObject()

    let mutable exit = ReactiveCommand.Create(new Action(fun _ -> Application.Current.Exit()))

    member this.Exit
        with get() =
            exit

type MainWindow () as this =
    inherit Window()

    let mutable armed = false
    let mutable justOpened = String.Empty

    do this.InitializeComponent()

    member this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
        this.DataContext <- MainWindowViewModel()

        this.Title <- "AltCover.Visualizer"
        this.FindControl<TabItem>("Visualizer").Header <- UICommon.GetResourceString "Visualizer"

        ["Open"; "Refresh"; "Font"; "Exit"]
        |> Seq.iter (fun n -> let item = this.FindControl<TextBlock>(n + "Text")
                              item.Text <- UICommon.GetResourceString n)

        let openFile = new Event<String option>()
        this.FindControl<MenuItem>("Open").Click
                    |> Event.add(fun _ -> let d = OpenFileDialog()
                                          // d.InitialDirectory <- readFolder()
                                          // d.Filters <- [ ]
                                          d.Title <- UICommon.GetResourceString "Open Coverage File"
                                          d.AllowMultiple <- false
                                          let a = d.ShowAsync(this)
                                                  |> Async.AwaitTask
                                          async {
                                                  (a |> Async.RunSynchronously).
                                                     FirstOrDefault()
                                                  |> Option.nullable
                                                  |> openFile.Trigger
                                          } |> Async.Start )

        let click = openFile.Publish
                    |> Event.choose id
                    |> Event.map (fun n -> justOpened <- n
                                           -1)
        let select =
           this.FindControl<MenuItem>("List").Items.OfType<MenuItem>()
           |> Seq.mapi (fun n (i : MenuItem) -> i.Click |> Event.map (fun _ -> n))

        // The sum of all these events -- we have explicitly selected a file
        let fileSelection = select |> Seq.fold Event.merge click

        let refresh = this.FindControl<MenuItem>("Refresh").Click
                      |> Event.map (fun _ -> 0)

        Event.merge fileSelection refresh
        |> Event.add (fun index ->
               printfn "%d - %s" index justOpened
(*             let h = handler
             async {
               let current =
                 FileInfo(if index < 0 then h.justOpened
                          else h.coverageFiles.[index])
               match CoverageFile.LoadCoverageFile current with
               | Left failed ->
                 InvalidCoverageFileMessage h.mainWindow failed
                 InvokeOnGuiThread(fun () -> updateMRU h current.FullName false)
               | Right coverage ->
                 // check if coverage is newer that the source files
                 let sourceFiles =
                   coverage.Document.CreateNavigator().Select("//seqpnt/@document")
                   |> Seq.cast<XPathNavigator>
                   |> Seq.map (fun x -> x.Value)
                   |> Seq.distinct

                 let missing =
                   sourceFiles
                   |> Seq.map (fun f -> new FileInfo(f))
                   |> Seq.filter (fun f -> not f.Exists)

                 if not (Seq.isEmpty missing) then MissingSourceFileMessage h.mainWindow current
                 let newer =
                   sourceFiles
                   |> Seq.map (fun f -> new FileInfo(f))
                   |> Seq.filter (fun f -> f.Exists && f.LastWriteTimeUtc > current.LastWriteTimeUtc)
                 // warn if not
                 if not (Seq.isEmpty newer) then OutdatedCoverageFileMessage h.mainWindow current
                 let model =
                   new TreeStore(typeof<string>, typeof<Gdk.Pixbuf>, typeof<string>, typeof<Gdk.Pixbuf>, typeof<string>,
                                 typeof<Gdk.Pixbuf>, typeof<string>, typeof<Gdk.Pixbuf>)
                 Mappings.Clear()
                 let ApplyToModel (theModel : TreeStore) (group : XPathNavigator * string) =
                   let name = snd group
                   let row = theModel.AppendValues(name, AssemblyIcon.Force())
                   PopulateAssemblyNode theModel row (fst group)

                 let assemblies = coverage.Document.CreateNavigator().Select("//module") |> Seq.cast<XPathNavigator>
                 assemblies
                 |> Seq.map (fun node -> (node, node.GetAttribute("assemblyIdentity", String.Empty).Split(',') |> Seq.head))
                 |> Seq.sortBy (fun nodepair -> snd nodepair)
                 |> Seq.iter (ApplyToModel model)
                 let UpdateUI (theModel : TreeModel) (info : FileInfo) () =
                   // File is good so enable the refresh button
                   h.refreshButton.Sensitive <- true
                   // Do real UI work here
                   h.classStructureTree.Model <- theModel
                   updateMRU h info.FullName true
                 ////ShowMessage h.mainWindow (sprintf "%s\r\n>%A" info.FullName h.coverageFiles) MessageType.Info
                 InvokeOnGuiThread(UpdateUI model current)
             }
             |> Async.Start*)
             )

        this.FindControl<TabItem>("About").Header <- UICommon.GetResourceString "About"
        this.FindControl<TextBlock>("Program").Text <- "AltCover.Visualizer " + AssemblyVersionInformation.AssemblyFileVersion
        this.FindControl<TextBlock>("Description").Text <- UICommon.GetResourceString "ProgramDescription"

        let copyright = AssemblyVersionInformation.AssemblyCopyright
        this.FindControl<TextBlock>("Copyright").Text <- copyright

        let link = this.FindControl<HtmlLabel>("Link")
        link.Text <- """<center><a href="http://www.github.com/SteveGilham/altcover">""" +
                      UICommon.GetResourceString "WebsiteLabel" +
                      "</a></center>"

        link.PointerPressed |> Event.add (fun _ -> armed <- true)
        link.PointerLeave |> Event.add (fun _ -> armed <- false)
        link.PointerReleased
        |> Event.add (fun _ -> ())

        // Windows -- Process Start (url)
        // Mac -- ("open", url)
        // *nix -- ("xdg-open", url)
        //Application.Instance.Open("http://www.github.com/SteveGilham/altcover"))

        this.FindControl<TextBlock>("License").Text <- UICommon.GetResourceString "AboutDialog.License"
        this.FindControl<TextBlock>("MIT").Text <- String.Format(CultureInfo.InvariantCulture,
                                                                 UICommon.GetResourceString "License",
                                                                 copyright)