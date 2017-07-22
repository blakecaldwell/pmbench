/*
   Copyright (c) 2016, 2017 University of Nevada, Las Vegas
   All rights reserved.
  
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions are met:
 
      1. Redistributions of source code must retain the above copyright
         notice, this list of conditions and the following disclaimer.
      2. Redistributions in binary form must reproduce the above copyright
         notice, this list of conditions and the following disclaimer in the
         documentation and/or other materials provided with the distribution.
   
   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
   AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
   IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
   ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
   LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
   CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
   SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
   INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
   CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
   ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
   POSSIBILITY OF SUCH DAMAGE.
 */

/* Written by: Julian Seymour, Jisoo Yang  */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml;

namespace PmGraphNS
{

////////////////////////////////////////////////////////
// control panel
// see PmPurged.cs for old junks
public class ControlPanel : FlowLayoutPanel
{
    public Button averageSelectedButton, deleteSelectedButton;
    public Button selectAllButton, helpButton;

    private Button screenshotButton;
    private Button aboutButton;
    private Button debugDumpInternalButton;
    private Button debugRebuildChartButton;

    private RadioButton[] selectChart;
    private Panel chartSelectPanel;

    private PmGraph pmgraph;

    private Button exportManualButton;
    private Button importManualSingleButton;

    private static int debugenable_state = 0;

    // Help, About, About enables debug 
    private void easteregg(int id)
    {
	if (debugenable_state == 2 && id == 2)  {
	    debugenable_state = 3;
	    this.Controls.Add(debugDumpInternalButton);
	    this.Controls.Add(debugRebuildChartButton);
	    return;
	}
	if (id == 1 && debugenable_state == 0) {
	    debugenable_state = 1;
	    return;
	} 
	if (id == 2 && debugenable_state == 1) {
	    debugenable_state = 2;
	    return;
	}
	if (debugenable_state == 3) return;

	debugenable_state = 0;
    }
    
    private void about_click(object sender, EventArgs e)
    {
	easteregg(2);
	MessageBox.Show("pmgraph v0.8\n\n" +
		"Copyright (c) 2017 - All rights reserved\n" +
		"Pmbench project - http://bitbucket.org/jisooy/pmbench");
    }

    private void helpButton_click(object sender, EventArgs e)
    {
	easteregg(1);
	MessageBox.Show(
	"Import XML: opens XML files produced by pmbench with the -f parameter;\n" +
	"Export CSV: saves a CSV file reflecting the data presented in the graph;\n" +
	"Average: adds the average of all selected items to the graph;\n" +
	"Delete: removes the selected items from the graph;\n" +
	"Select All: selects all graphed items.\n\n" +
	"Hover the mouse over a graphed item in the legend to highlight it;\n" +
	"Left click an item's name to select it;\n"+
	"Middle click an item's name to show its peak latencies."
	);
    }

    public ControlPanel(PmGraph p)
    {
	pmgraph = p;

	Width = 78;
	Height = pmgraph.Height - SystemInformation.CaptionHeight;

	FlowDirection = FlowDirection.TopDown;

	importManualSingleButton = initButton("Import XML", 
		importSingleBenches_click, true);
	exportManualButton = initButton("Export CSV", 
		pmgraph.exportCsvManual_click, false);
	averageSelectedButton = initButton("Average selected",
		pmgraph.averageSelectedButton_click, false);
	deleteSelectedButton = initButton("Delete seleted",
		pmgraph.deleteSelectedButton_click, false);
	selectAllButton = initButton("Select all", 
		pmgraph.selectAll_click, false);
	helpButton = initButton("Instructions", 
		helpButton_click, true);

	Controls.Add(importManualSingleButton);
	Controls.Add(exportManualButton);
	Controls.Add(averageSelectedButton);
	Controls.Add(deleteSelectedButton);
	Controls.Add(selectAllButton);
	Controls.Add(helpButton);

	// chart selection radio button 
	chartSelectPanel = new Panel();
	chartSelectPanel.BorderStyle = BorderStyle.Fixed3D;
	chartSelectPanel.Size = new Size(78, 80);

	String[] chtnames = { "mini", "full", "log" };
	String[] chttexts = { "Mini (linear)", "Full (linear)", "Full (Log)" };
	bool[] chtchecked = { false, true, false };
	selectChart = new RadioButton[3];
	for (int i = 0; i < 3; i++) {
	    RadioButton rb = new RadioButton();
	    rb.Name = chtnames[i];
	    rb.Text = chttexts[i];
	    rb.Checked = chtchecked[i];
	    rb.Size = new Size(90, 18);
	    rb.CheckedChanged += new EventHandler(pmgraph.chartchange_click);
	    rb.Location = new Point(0, i * 25 + 3);
	    selectChart[i] = rb;
	}
	chartSelectPanel.Controls.AddRange(selectChart);
	Controls.Add(chartSelectPanel);

	screenshotButton = initButton("Screenshot", 
		pmgraph.screenshot_click, true);
	Controls.Add(screenshotButton);

	aboutButton = initButton("About", 
		about_click, true);
	Controls.Add(aboutButton);

	debugDumpInternalButton = initButton("debug dump", 
		pmgraph.testError_click, true);
	debugRebuildChartButton = initButton("redraw chart", 
		pmgraph.rebuildChart_click, true);

	selectAllButton.Enabled = true;
	exportManualButton.Enabled = false;
    }

    public void selectChartRadioFull()
    {
	selectChart[1].Checked = true;
    }

    private static Button initButton(string text, EventHandler e, bool enable)
    {
	Button b = new Button();
	b.Text = text;
	b.Click += new EventHandler(e);
	b.Enabled = enable;
	return b;
    }

    public void importSingleBenches_click(object sender, EventArgs args)
    {
	int before = 0;

	if (deleteSelectedButton.Enabled) before += 1;
	if (averageSelectedButton.Enabled) before += 4;

	using (OpenFileDialog fd = new OpenFileDialog())
	{
	    fd.InitialDirectory = Environment.SpecialFolder.Desktop.ToString();
	    fd.Title = "Select benchmark(s) to import";
	    fd.Filter = "xml files (*.xml)|*.xml";
	    fd.Multiselect = true;

	    if (fd.ShowDialog() == DialogResult.OK) {
		pmgraph.importSingle(fd.FileNames);
	    }
	}
	setManualButtonsEnabled(before);
    }

    public void setManualButtonsEnabled(int i)
    {
	exportManualButton.Enabled = (i > 0);
	deleteSelectedButton.Enabled = (i > 0);
	averageSelectedButton.Enabled = (i > 2);
    }
}   // ControlPanel


////////////////////////////////////////////////////////
// PmGraph partial class that matters
// see PmPurged.cs for old junks
public partial class PmGraph : System.Windows.Forms.Form
{
    private Panel mainPanel;
    private ControlPanel controlPanel;

    private Chart currentChart;	// hold onto the current Chart object
    public Harness manual;

    private Dictionary<string, XmlDocument> xmlFiles;

    public PmGraph()
    {
	SuspendLayout();

	this.Text = "Pmgraph - Pmbench XML result reader";
	Point originPoint = new Point(0, 0);

	this.MinimumSize = new Size(500, 400);
	this.MaximumSize = new Size(
		Screen.GetWorkingArea(originPoint).Width,
		Screen.GetWorkingArea(originPoint).Height );

	this.Size = new Size(780, 580);

	mainPanel = new Panel();
	mainPanel.Location = new Point(0, 0);
	mainPanel.Width = this.Width;
	mainPanel.Height = this.Height - SystemInformation.CaptionHeight;

	controlPanel = new ControlPanel(this);

	mainPanel.Controls.Add(controlPanel);

	Controls.Add(mainPanel);

	xmlFiles = new Dictionary<string, XmlDocument>();

	manual = new Harness(this);

	// initialize pivotchart
	Chart newchart = manual.rebuildAndGetNewChart(
		calculateChartWidth(), calculateChartHeight());

	if (newchart == null) {
	    MB.S("PmGraph: null chart returned");
	    System.Windows.Forms.Application.Exit();
	}

	attachChart(newchart);

	Resize += new EventHandler(resize_event);

	ResumeLayout();
    }

    public void testerror_dumpXmlFilesToConsole()
    {
	Console.WriteLine("PmGraph::dumpXmlFiles (count = {0})",
		xmlFiles.Count);
	foreach(string key in xmlFiles.Keys) {
	    Console.WriteLine(key);
	}
    }

    private int calculateChartWidth()
    {
	return mainPanel.Width 
		- controlPanel.Width 
		- controlPanel.Margin.Left 
		- controlPanel.Margin.Right;
    }

    private int calculateChartHeight()
    {
	return mainPanel.Height;
    }

    private void resize_event(object sender, EventArgs args)
    {
	mainPanel.Width = this.Width; 
	mainPanel.Height = this.Height - SystemInformation.CaptionHeight;

	if (mainPanel.Controls.Contains(currentChart)) {
	    currentChart.Width = calculateChartWidth();
	    currentChart.Height = calculateChartHeight();

	    // don't need to call Refresh()
	    //currentChart.Refresh();
	}
    }

    private void detachChart()
    {
	if (mainPanel.Controls.Contains(currentChart)) {
	    mainPanel.Controls.Remove(currentChart);
	    currentChart = null;
	}
    }

    private void attachChart(Chart newchart)
    {
	newchart.Location = new Point(
		controlPanel.Width 
		+ controlPanel.Margin.Left 
		+ controlPanel.Margin.Right
		, 0);
	mainPanel.Controls.Add(newchart);
	currentChart = newchart;
    }

    /*
     * Hard redraw of chart- basically remove/recreate/reattach chart..
     */
    private void redrawManual()
    {
	detachChart();

	manual.destroyPivotChart();

	Chart newchart = manual.rebuildAndGetNewChart(
		calculateChartWidth(),
		calculateChartHeight());

	if (newchart == null) MB.S("redrawManual: null chart returned");

	attachChart(newchart);
    }

    public void chartchange_click(object sender, EventArgs e)
    {
	RadioButton b = sender as RadioButton;

	if (!b.Checked) return; 

	detachChart();

	Chart chart = manual.switchToChart(b.Name);

	chart.Width = calculateChartWidth();
	chart.Height = calculateChartHeight();

	attachChart(chart);
    }

    public static XmlNode getParamsNodeFromSeriesNode(XmlNode node)
    {
	return SafeXmlParse.selNode(node,
		"test_round/pmbenchmark/report/signature/params");
    }

    // returns docname
    private string registerXmlDocName(string s, XmlDocument doc)
    {
	int trynum = 0;
	string t = s;
	while (true) {
	    try {
		xmlFiles.Add(t, doc);
		break;
	    }
	    catch (ArgumentException) {	// name collision
		t = s + trynum++;
	    }
	}
	return t;
    }

    public void removeDeadXmlDoc(string docname)
    {
	try {
	    XmlDocument doc = xmlFiles[docname];
	    xmlFiles.Remove(docname);
	}
	catch (KeyNotFoundException) {
	    MB.S("removeDeadXmlDoc(): attempted to delete nonexistent XmlDocument " + docname);
	}
    }

    /*
     * called by controlPanel when import dialog is OKed. 
     */
    public void importSingle(string[] filenames)
    {
	List<BenchRound> brs = new List<BenchRound>();

	foreach (string fpath in filenames) {
	    XmlDocument doc = new XmlDocument();
	    XmlNode fakeSeries = doc.CreateNode(XmlNodeType.Element,
		    "test_nice", doc.NamespaceURI);
	    doc.AppendChild(fakeSeries);

	    ParamSet ps = new ParamSet();

	    XmlDocument tempdoc = new XmlDocument();
	    tempdoc.Load(fpath);
	    XmlNode fakeRound = doc.CreateNode(XmlNodeType.Element,
		    "test_round", doc.NamespaceURI);

	    XmlAttribute iter = doc.CreateAttribute("iter");
	    iter.Value = ("1").ToString();
	    fakeRound.Attributes.Append(iter);

	    fakeRound.AppendChild(doc.ImportNode(
			SafeXmlParse.selNode(tempdoc, "pmbenchmark"), true));
	    fakeSeries.AppendChild(fakeRound);

	    ps.setParamsFromNode(getParamsNodeFromSeriesNode(fakeSeries));
	    ps.operatingSystem = SafeXmlParse.selNode(tempdoc, 
		    "pmbenchmark/report/signature/pmbench_info/version_options").InnerText;

	    BenchSiblings bench = new BenchSiblings(fakeSeries, doc, ps);
	    string fname = Path.GetFileNameWithoutExtension(fpath);
	    bench.averageRound.customName = registerXmlDocName(fname, doc);

	    //XXX fix this! moved from addSeriesAverageToManual()
	    if (manual.baseParams == null) {
		manual.baseParams = bench.benchParams;
	    }
	    brs.Add(bench.averageRound);
	}

	manual.addNewBenchrounds(brs);
    }

    public void exportCsvManual_click(object sender, EventArgs e)
    {
	manual.exportCsv(null);
    }

    private int averageCounter = 0;
    public void averageSelectedButton_click(object sender, EventArgs e)
    {
	BenchSiblings bench = manual.averageSelected(averageCounter++);

	registerXmlDocName(bench.averageRound.customName, bench.theDoc);

	// XXX below 'if' can go??
	if (manual.baseParams == null) manual.baseParams = bench.benchParams;

	var brs = new List<BenchRound>();
	brs.Add(bench.averageRound);
	manual.addNewBenchrounds(brs);
    }

    public void deleteSelectedButton_click(object sender, EventArgs e)
    {
	manual.deleteSelected();
    }

    public void selectAll_click(object sender, EventArgs e)
    {
	manual.selectAll();
    }

    public void screenshot_click(object sender, EventArgs e)
    {
	using (MemoryStream ms = new MemoryStream()) {
	    currentChart.SaveImage(ms, ChartImageFormat.Bmp);
	    Clipboard.SetImage(new Bitmap(ms));
	}
	MessageBox.Show("Chart image saved to Clipboard");
    }

    /*
     * called by PivotChart::refreshSelectionColors()
     */
    public void updateSelectionButtons(int i)
    {
	controlPanel.setManualButtonsEnabled(i);
    }

    public void testError_click(object sender, EventArgs e)
    {
	//testerror_dumpXmlFilesToConsole();
	try {
	    manual.thePivotChart.testerror_dumpChartSeries();
	    manual.thePivotChart.testerror_dumpSelectionStatus();
	} catch (NullReferenceException) {
	    Console.WriteLine("Null reference manual.thePivotChart.testerror_dumpChartSeries()");
	}
    }

    public void rebuildChart_click(object sender, EventArgs e)
    {
	redrawManual();
	controlPanel.selectChartRadioFull();
	
    }

}   //PmGraph

} // namespace PmGraphSpace
