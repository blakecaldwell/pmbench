These are C# files for XML parsing/graphing tool for pmbench.

Instructions for command-line build:

Prerequisite: On Windows, you would need c# compiler (csc.exe) which can be obtained from Visual Studio or Microsoft .NET Framework SDK.

Execute the following command in the source directory using developer command prompt:

> csc.exe -target:winexe -out:pmgraph.exe pmgraph.cs program.cs -reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.2\System.Windows.Forms.DataVisualization.dll"

(You may need to modify the path in -reference according to what you have in your system)

TODO:
- Linux port - using monodevelop and GTK#
- Get rid of async (requires .NET 4.5) for better Linux compatibility and make code conform to older c# and .NET 4.0
- Replace Forms.DataVisualization with NPlot (see netcontrols.org/nplot/wiki/ )
- Get rid of warnings
