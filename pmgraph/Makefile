
REFERENCE = System.Drawing.dll,System.Windows.Forms.dll,System.Windows.Forms.DataVisualization.dll

pmgraph.exe:  PmGraph.cs PmXml.cs Program.cs
	mcs -reference:$(REFERENCE) $+ -out:$@ 
