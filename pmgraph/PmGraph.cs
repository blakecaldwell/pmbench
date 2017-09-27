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
// trivial class overrides..
public class PmButton : Button
{
    public PmButton(string text, EventHandler e, bool enable)
    {
	Text = text;
	Click += new EventHandler(e);
	Enabled = enable;
    }

    // override default size rather than setting size..
    // N.B., Winform default button size: (75,23)
    protected override Size DefaultSize
    {
	get { return new Size(82,23); }
    }
}

////////////////////////////////////////////////////////
// control panel
// see PmPurged.cs for old junks
public class ControlPanel : FlowLayoutPanel
{
    public PmButton averageSelectedButton, deleteSelectedButton;
    public PmButton selectAllButton, helpButton;

    private PmButton screenshotButton;
    private PmButton aboutButton;
    private PmButton debugDumpInternalButton;
    private PmButton debugRebuildChartButton;

    private PmButton barGraphButton;

    private RadioButton[] selectChart;
    private Panel chartSelectPanel;

    private PmGraph pmgraph;

    private PmButton exportManualButton;
    private PmButton importManualSingleButton;

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

    private void about_click(object sender, EventArgs e)
    {
	easteregg(2);
	MessageBox.Show("pmgraph v0.8\n\n" +
		"Copyright (c) 2017 - All rights reserved\n" +
		"Pmbench project - http://bitbucket.org/jisooy/pmbench");
    }

    public ControlPanel(PmGraph p)
    {
	pmgraph = p;

	Width = 84;
	Height = pmgraph.Height - SystemInformation.CaptionHeight;

	FlowDirection = FlowDirection.TopDown;

	importManualSingleButton = new PmButton("Import XML", 
		importSingleBenches_click, true);
	exportManualButton = new PmButton("Export CSV", 
		pmgraph.exportCsvManual_click, false);
	averageSelectedButton = new PmButton("Average selected",
		pmgraph.averageSelectedButton_click, false);
	deleteSelectedButton = new PmButton("Delete seleted",
		pmgraph.deleteSelectedButton_click, false);
	selectAllButton = new PmButton("Select all", 
		pmgraph.selectAll_click, false);
	helpButton = new PmButton("Instructions", 
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
	chartSelectPanel.Size = new Size(84, 80);

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

	barGraphButton = new PmButton("Bar Graph", bargraph_click, false);
	Controls.Add(barGraphButton);

	screenshotButton = new PmButton("Screenshot", 
		pmgraph.screenshot_click, true);
	Controls.Add(screenshotButton);

	aboutButton = new PmButton("About", about_click, true);
	Controls.Add(aboutButton);

	debugDumpInternalButton = new PmButton("debug dump", 
		pmgraph.testError_click, true);
	debugRebuildChartButton = new PmButton("redraw chart", 
		rebuildChart_click, true);

	selectAllButton.Enabled = true;
	exportManualButton.Enabled = false;
    }

    private void importSingleBenches_click(object sender, EventArgs args)
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

    private void bargraph_click(object sender, EventArgs args)
    {
	if (pmgraph.launchBarWindow()) {
	    barGraphButton.Enabled = false;
	}
    }

    private void rebuildChart_click(object sender, EventArgs e)
    {
	pmgraph.redrawChart();
	selectChart[1].Checked = true;
    }

    public void setManualButtonsEnabled(int i)
    {
	exportManualButton.Enabled = (i > 0);
	deleteSelectedButton.Enabled = (i > 0);
	averageSelectedButton.Enabled = (i > 2);
	barGraphButton.Enabled = (i > 0) && !pmgraph.isBarWindowActive();
    }

    public void setBarGraphButtonEnabled()
    {
	barGraphButton.Enabled = true;
    }

}   // ControlPanel



////////////////////////////////////////////////////////
// PmBar class 
// draws bar chart comparing benchmarks 
public class PmBar : System.Windows.Forms.Form
{
    private PmButton screenshotButton;
    private PmButton reloadButton;
    private CheckBox aggregateRWBox;
    private CheckBox scaleTo100Box;
    private PmButton thresholdButton;

    private FlowLayoutPanel leftPanel;

    private Chart chartSplit, chartAggre;

    private Harness harness;

    private double[] threshold = new double [5] {
	    0.5e-6,	    // < 0.5us   hits
	    3e-6,	    // <   3us   soft fault 
	    50e-6,	    // <  50us   hard fault, fast path
	    500e-6,	    // < 0.5ms   hard fault, slow path
	    100,	    // >=0.5ms   hard fault, high latency (sched?)
	};

    private void createLeftPanel()
    {
	leftPanel = new FlowLayoutPanel();
	leftPanel.Location = new Point(0, 0);
	leftPanel.Width = 84;
	leftPanel.Height = this.Height - SystemInformation.CaptionHeight;

	reloadButton = new PmButton("Reload", reload_click, true);
	leftPanel.Controls.Add(reloadButton);
	thresholdButton = new PmButton("Set threshold", threshold_click, true);
	leftPanel.Controls.Add(thresholdButton);

	aggregateRWBox = new CheckBox() {
	    Text = "Combine RW",
	    Enabled = true,
	    Checked = true };
	aggregateRWBox.CheckedChanged += new EventHandler(aggregate_click);
	leftPanel.Controls.Add(aggregateRWBox);

	scaleTo100Box = new CheckBox() {
	    Text = "Scale to 100",
	    Enabled = true,
	    Checked = false };
	scaleTo100Box.CheckedChanged += new EventHandler(scaleTo100_click);
	leftPanel.Controls.Add(scaleTo100Box);

	screenshotButton = new PmButton("Screenshot", screenshot_click, true);
	leftPanel.Controls.Add(screenshotButton);

	this.Controls.Add(leftPanel);
    }

    private Chart createNewChart(string name)
    {
	Chart chart = new Chart();
	ChartArea ca = new ChartArea();
	Legend legend = new Legend();

	ca.Name = name;
	ca.AxisX.Title = "Benchmarks";
	ca.AxisX.ScaleView.Zoomable = true;
	ca.AxisX.MajorTickMark.Enabled = true;
	ca.AxisY.Title = "Time Spent";
	ca.AxisY.ScaleView.Zoomable = true;

	legend.Name = "Average";
	legend.TextWrapThreshold = 80;
	legend.DockedToChartArea = name;

	chart.ChartAreas.Add(ca);
	chart.Name = "Runtime bar";
	chart.Legends.Add(legend);
	chart.TabIndex = 1;
	chart.Text = "Runtime breakdown bar chart";

	chart.Location = new Point(
		leftPanel.Width 
		+ leftPanel.Margin.Left 
		+ leftPanel.Margin.Right
		, 0);
	return chart;
    }


    private void createCharts()
    {
	chartSplit = createNewChart("barchart");

	chartAggre = createNewChart("barchart r/w combined");
	if (aggregateRWBox.Checked) {
	    this.Controls.Remove(chartSplit);
	    this.Controls.Add(chartAggre);
	} else {
	    this.Controls.Add(chartSplit);
	    this.Controls.Remove(chartAggre);
	}

	// below sets size
	resize_event(null, null);
    }

    private void createDataSeries()
    {
	string[] sname = {"hit", "soft", "hard low", "hard mid", "hard high"};

	SeriesChartType ctype;
	ctype = scaleTo100Box.Checked ? SeriesChartType.StackedColumn100
				    : SeriesChartType.StackedColumn;
	Series s;
	for (int i = 0; i < 5; i++) {
	    s = new Series(sname[i]);
	    s.ChartType = ctype;
	    chartSplit.Series.Add(s);

	    s = new Series(sname[i]);
	    s.ChartType = ctype;
	    chartAggre.Series.Add(s);
	}
    }

    public PmBar(Harness hn)
    {
	SuspendLayout();
	harness = hn;

	this.Text = "PmGraph - Pmbench XML result comparison";
	this.Size = new Size(480, 380);

	createLeftPanel();
	createCharts();

	createDataSeries();

	Resize += new EventHandler(this.resize_event);

	importDataSeries();

	ResumeLayout();
    }

    private void deleteDataSeries()
    {
	foreach (var series in chartSplit.Series) {
	    series.Dispose();
	}
	foreach (var series in chartAggre.Series) {
	    series.Dispose();
	}
	chartSplit.Series.Clear();
	chartAggre.Series.Clear();
    }

    private void importDataSeries()
    {
	BenchRuntimeStat rts = harness.getRuntimeStats(threshold);

	foreach (var stat in rts.stats) {
	    for (int j = 0; j < rts.stats[0].timespent.Length; j++) {
		int k = chartSplit.Series[j].Points.AddY(stat.timespent[j]);
		//XXX: slow indexing on list
		chartSplit.Series[j].Points[k].AxisLabel = stat.bname;
	    }
	}

	foreach (var stat in rts.agg_stats) {
	    for (int j = 0; j < rts.agg_stats[0].timespent.Length; j++) {
		int k = chartAggre.Series[j].Points.AddY(stat.timespent[j]);
		//XXX: slow indexing on list
		chartAggre.Series[j].Points[k].AxisLabel = stat.bname;
	    }
	}
    }

    private void resize_event(object sender, EventArgs args)
    {
	leftPanel.Height = this.Height - SystemInformation.CaptionHeight;

	chartSplit.Width = this.Width 
		    - leftPanel.Width
		    - leftPanel.Margin.Left
		    - leftPanel.Margin.Right - 5;
	chartSplit.Height = leftPanel.Height - 5;
	chartAggre.Width = this.Width 
		    - leftPanel.Width
		    - leftPanel.Margin.Left
		    - leftPanel.Margin.Right - 5;
	chartAggre.Height = leftPanel.Height - 5;
    }

    public void reload_click(object sender, EventArgs args)
    {
	SuspendLayout();

	deleteDataSeries();
	createDataSeries();
	importDataSeries();

	ResumeLayout();
    }

    private void screenshot_click(object sender, EventArgs args)
    {
	Chart current = aggregateRWBox.Checked ? chartAggre : chartSplit;

	using (MemoryStream ms = new MemoryStream()) {
	    current.SaveImage(ms, ChartImageFormat.Bmp);
	    Clipboard.SetImage(new Bitmap(ms));
	}
	MessageBox.Show("Chart image saved to Clipboard");
    }

    private void aggregate_click(object sender, EventArgs args)
    {
	CheckBox c = sender as CheckBox;

	SuspendLayout();
	if (c.Checked) {
	    this.Controls.Remove(chartSplit);
	    this.Controls.Add(chartAggre);
	} else {
	    this.Controls.Add(chartSplit);
	    this.Controls.Remove(chartAggre);
	}
	ResumeLayout();
    }

    private void scaleTo100_click(object sender, EventArgs args)
    {
	CheckBox c = sender as CheckBox;

	SeriesChartType ctype = c.Checked ? SeriesChartType.StackedColumn100
				    : SeriesChartType.StackedColumn;

	SuspendLayout();
	foreach (var series in chartSplit.Series) {
	    series.ChartType = ctype;
	}
	foreach (var series in chartAggre.Series) {
	    series.ChartType = ctype;
	}
	ResumeLayout();
    }

    public void threshold_click(object sender, EventArgs args)
    {
	Form myForm = new Form();
	myForm.SuspendLayout();
	myForm.FormBorderStyle = FormBorderStyle.FixedDialog;

	myForm.Size = new Size(260,210);
	var flowPanel = new FlowLayoutPanel();

	Button okay = new Button() {
	    Text = "OK",
	    DialogResult = DialogResult.OK,
	    Location = new Point(40, 145) };
	Button cancel = new Button() {
	    Text = "Cancel",
	    DialogResult = DialogResult.Cancel,
	    Location = new Point(140, 145) };
	
	Label hitLabel = new Label() {
	    Text = "Hits are less than :",
	    TextAlign = ContentAlignment.MiddleLeft };
	Label softLabel = new Label() {
	    Text = "Soft faluts are < :",
	    TextAlign = ContentAlignment.MiddleLeft };
	Label hard_lowLabel = new Label() {
	    Text = "Hard (low) are < :",
	    TextAlign = ContentAlignment.MiddleLeft };
	Label hard_midLabel = new Label() {
	    Text = "Hard (mid) are < :",
	    TextAlign = ContentAlignment.MiddleLeft };
	
	TextBox hitBox = new TextBox() {
	    Text = (threshold[0] * 1e6).ToString(),
	    Multiline = false };
	TextBox softBox = new TextBox() {
	    Text = (threshold[1] * 1e6).ToString(),
	    Multiline = false };
	TextBox hard_lowBox = new TextBox() {
	    Text = (threshold[2] * 1e6).ToString(),
	    Multiline = false };
	TextBox hard_midBox = new TextBox() {
	    Text = (threshold[3] * 1e6).ToString(),
	    Multiline = false };
	
	Label us0 = new Label() { Text = "us" };
	Label us1 = new Label() { Text = "us" };
	Label us2 = new Label() { Text = "us" };
	Label us3 = new Label() { Text = "us" };
	us0.TextAlign = ContentAlignment.MiddleLeft;
	us1.TextAlign = ContentAlignment.MiddleLeft;
	us2.TextAlign = ContentAlignment.MiddleLeft;
	us3.TextAlign = ContentAlignment.MiddleLeft;

	flowPanel.SuspendLayout();
	flowPanel.FlowDirection = FlowDirection.LeftToRight;
	flowPanel.Controls.AddRange(new Control[] { 
		hitLabel, hitBox, us0,
		softLabel, softBox, us1,
		hard_lowLabel, hard_lowBox, us2,
		hard_midLabel, hard_midBox, us3
		} );
	flowPanel.Size = new Size(320, 105);
	flowPanel.Location = new Point(10, 5);
	flowPanel.ResumeLayout();
	myForm.Controls.Add(flowPanel);

	myForm.Controls.Add(okay);
	myForm.Controls.Add(cancel);
	
	Label infoLabel = new Label() { 
	    Text = "* Reload graph to use new threshold setting. *",
	    Location = new Point(10,120),
	    Size = new Size(300, 20) };

	myForm.Controls.Add(infoLabel);
	
	myForm.ResumeLayout();
	myForm.ShowDialog();

	if (myForm.DialogResult == DialogResult.OK) {
	    double[] temp = new double[5];
	    try {
		temp[0] = Convert.ToDouble(hitBox.Text) * 1e-6;
		temp[1] = Convert.ToDouble(softBox.Text) * 1e-6;
		temp[2] = Convert.ToDouble(hard_lowBox.Text) * 1e-6;
		temp[3] = Convert.ToDouble(hard_midBox.Text) * 1e-6;
		temp[4] = 100;
	    }
	    catch (FormatException) {
		MessageBox.Show("Illegal entry - numeric value only.");
		goto error_out;
	    }
	    catch (OverflowException) {
		MessageBox.Show("Illegal entry - value out of range.");
		goto error_out;
	    }
	    
	    for (int i = 0; i < 4; i++) {
		if (temp[i] > temp[i+1]) {
		    MessageBox.Show("Numbers must be in an asending order.");
		    goto error_out;
		}
	    }
	    Array.Copy(temp, threshold, 5);
	}

error_out:
	myForm.Dispose();
    }
}

////////////////////////////////////////////////////////
// PmGraph class that matters
// see PmPurged.cs for old junks
public class PmGraph : System.Windows.Forms.Form
{
    private ControlPanel controlPanel;
    private PmBar barWindow;

    private Chart currentChart;	// hold onto the current Chart object
    private Harness harness;

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


	controlPanel = new ControlPanel(this);

	Controls.Add(controlPanel);

	xmlFiles = new Dictionary<string, XmlDocument>();

	harness = new Harness(this);

	// initialize pivotchart
	Chart newchart = harness.rebuildAndGetNewChart(
		calculateChartWidth(), calculateChartHeight());

	if (newchart == null) {
	    MB.S("PmGraph: null chart returned");
	    System.Windows.Forms.Application.Exit();
	}

	attachChart(newchart);

	this.Resize += (sender, arg) =>
	    {
		if (Controls.Contains(currentChart)) {
		    currentChart.Width = calculateChartWidth();
		    currentChart.Height = calculateChartHeight();
		    //currentChart.Refresh();
		}
	    };

	ResumeLayout();
    }

    public void testerror_dumpXmlFilesToConsole()
    {
	Console.WriteLine("PmGraph::dumpXmlFiles (count = {0})",
		xmlFiles.Count);
	foreach(string key in xmlFiles.Keys) {
	    Console.WriteLine("  " + key);
	}
    }

    private int calculateChartWidth()
    {
	return this.Width 
		- controlPanel.Width 
		- controlPanel.Margin.Left 
		- controlPanel.Margin.Right
		- SystemInformation.FrameBorderSize.Width * 2
		;
    }

    private int calculateChartHeight()
    {
	return this.Height 
		- SystemInformation.CaptionHeight 
		- SystemInformation.FrameBorderSize.Height * 2
		;
    }

    private void detachChart()
    {
	if (Controls.Contains(currentChart)) {
	    Controls.Remove(currentChart);
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
	Controls.Add(newchart);
	currentChart = newchart;
    }

    /*
     * Hard redraw of chart- basically remove/recreate/reattach chart..
     */
    public void redrawChart()
    {
	detachChart();

	harness.destroyPivotChart();

	Chart newchart = harness.rebuildAndGetNewChart(
		calculateChartWidth(),
		calculateChartHeight());

	if (newchart == null) MB.S("redrawChart: null chart returned");

	attachChart(newchart);
    }

    public void chartchange_click(object sender, EventArgs e)
    {
	RadioButton b = sender as RadioButton;

	if (!b.Checked) return; 

	detachChart();

	Chart chart = harness.switchToChart(b.Name);

	chart.Width = calculateChartWidth();
	chart.Height = calculateChartHeight();

	attachChart(chart);
    }

    public static XmlNode getParamsNodeFromSeriesNode(XmlNode node)
    {
	return XmlParse.selNode(node,
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
			XmlParse.selNode(tempdoc, "pmbenchmark"), true));
	    fakeSeries.AppendChild(fakeRound);

	    ps.setParamsFromNode(getParamsNodeFromSeriesNode(fakeSeries));
	    ps.operatingSystem = XmlParse.selNode(tempdoc, 
		    "pmbenchmark/report/signature/pmbench_info/version_options").InnerText;

	    BenchSiblings bench = new BenchSiblings(fakeSeries, doc, ps);
	    string fname = Path.GetFileNameWithoutExtension(fpath);
	    bench.averageRound.customName = registerXmlDocName(fname, doc);

	    //XXX fix this! moved from addSeriesAverageToManual()
	    if (harness.baseParams == null) {
		harness.baseParams = bench.benchParams;
	    }
	    brs.Add(bench.averageRound);
	}

	harness.addNewBenchrounds(brs);
    }

    public void exportCsvManual_click(object sender, EventArgs e)
    {
	harness.exportCsv(null);
    }

    private int averageCounter = 0;
    public void averageSelectedButton_click(object sender, EventArgs e)
    {
	BenchSiblings bench = harness.averageSelected(averageCounter++);

	registerXmlDocName(bench.averageRound.customName, bench.theDoc);

	// XXX below 'if' can go??
	if (harness.baseParams == null) harness.baseParams = bench.benchParams;

	var brs = new List<BenchRound>();
	brs.Add(bench.averageRound);
	harness.addNewBenchrounds(brs);
    }

    public void deleteSelectedButton_click(object sender, EventArgs e)
    {
	harness.deleteSelected();
    }

    public void selectAll_click(object sender, EventArgs e)
    {
	harness.selectAll();
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
	try {
	    testerror_dumpXmlFilesToConsole();
	    harness.thePivotChart.testerror_dumpChartSeries();
	    harness.thePivotChart.testerror_dumpSelectionStatus();
	} catch (NullReferenceException) {
	    Console.WriteLine("Null reference harness.thePivotChart.testerror_dumpChartSeries()");
	}
    }
    
    // return true if new bar window is created.
    // return false if one already exists.
    public bool launchBarWindow()
    {
	if (barWindow != null) return false;

	barWindow = new PmBar(harness);
	barWindow.FormClosed += (s, e) => 
	    {
		barWindow = null;
		controlPanel.setBarGraphButtonEnabled();
	    };
	barWindow.Show();
	return true;
    }

    public bool isBarWindowActive()
    {
	return (barWindow != null);
    }
}   //PmGraph

} // namespace PmGraphSpace
