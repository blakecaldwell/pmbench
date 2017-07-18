These are C# files for XML parsing/graphing tool for pmbench.

Instructions for command-line build:

-Windows host build instruction

Prerequisite: On Windows, you would need c# compiler (csc.exe) which can be obtained from Visual Studio or Microsoft .NET Framework SDK.

Execute the following command in the source directory using developer command prompt:

> csc.exe -target:winexe -out:pmgraph.exe pmgraph.cs program.cs -reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.2\System.Windows.Forms.DataVisualization.dll"

(You may need to modify the path in -reference according to what you have in your system)

- Linux host build instruction
Prerequisite: Monodevelop package (mono-devel).

$ mcs -reference:System.Drawing.dll,System.Windows.Forms.dll,System.Windows.Forms.DataVisualization.dll PmGraph.cs PmXml.cs Program.cs -out:pmgraph.exe

Copy pmgraph.exe to windows and execute there.

You can execute on Linux (see below), but mono's DataVisualization library is incomplete so you will get runtime error)

$ mono ./pmgraph.exe

TODO:
- Get rid of async (requires .NET 4.5) for better Linux compatibility and make code conform to older c# and .NET 4.0
- Replace Forms.DataVisualization with NPlot (see netcontrols.org/nplot/wiki/ )
