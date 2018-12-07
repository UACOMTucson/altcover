using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("AltCover.Base")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("AltCover.Base")]
[assembly: AssemblyCopyright("Copyright ©  2018")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("055c8af5-d442-4889-93aa-c86cceb9e89c")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyDescription("Part of a cross-platform coverage gathering and processing tool set for .net/.net core and Mono")]
#if DEBUG
#if NETSTANDARD2_0
[assembly: InternalsVisibleTo("AltCover.Shadow.Adapter")]
[assembly: InternalsVisibleTo("AltCover.Shadow.Tests")]
#else
#if NETCOREAPP2_0
[assembly: InternalsVisibleTo("AltCover.Tests")]
[assembly: InternalsVisibleTo("AltCover.XTests")]
[assembly: InternalsVisibleTo("AltCover.Tests.Visualizer")]
#else
[assembly: InternalsVisibleTo("AltCover.Recorder, PublicKey=0024000004800000940000000602000000240000525341310004000001000100916443A2EE1D294E8CFA7666FB3F512D998D7CEAC4909E35EDB2AC1E104DE68890A93716D1D1931F7228AAC0523CACF50FD82CDB4CCF4FF4BF0DED95E3A383F4F371E3B82C45502CE74D7D572583495208C1905E0F1E8A3CCE66C4C75E4CA32E9A8F8DEE64E059C0DC0266E8D2CB6D7EBD464B47E062F80B63D390E389217FB7")]
[assembly: InternalsVisibleTo("AltCover.Recorder, PublicKey=002400000480000094000000060200000024000052534131000400000100010041C08339BC8FE3A8B847E3EC38CB1BB31A9B39855347761BAB7AC04E726FFB227B147DF92DE5C3D8BCE3B7CFC7C9AC8110AF2E22F5E35D9CB0EBF47C36890DF617BD83E211002A1979DAB26CC18743DE674CE6F34ABAC834F597364BC5598C133F192596FC2161A832A9BBD33835DBB44F3B924A6F736BE6217ECE42889ABBCF")]
#endif
#endif
#else
#endif