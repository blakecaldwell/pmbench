/*
   Copyright (c) 2016, University of Nevada, Las Vegas
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

/* Written by: Julian Seymour  */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PmGraphNS
{
// shorthand for displaying messagebox text
public static class MB
{
    public static void S(string str) 
    {
	MessageBox.Show(str);
    }
}



static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
	Application.EnableVisualStyles();
	Application.SetCompatibleTextRenderingDefault(false);
	Application.Run(new PmGraphNS.PmGraph());
    }
}
}

/* graveyard
 
    public void getAverageRoundForManualPivot()
    {
	BenchSiblings bs = getBenchSiblingsObjectFromKeyAndKey(
		    controlPanel.getKey1FromDropdowns(),
		    controlPanel.getKey2FromDropdowns());

	if (bs == null) {
	    MB.S("Error");
	    return;
	}
Console.WriteLine("getAverageRoundForManualPivot");
	addSeriesAverageToManual(bs);
	graphManual();
    }

    private static string[] splitString(string s, char c)
    {
	char[] delimiter = { c };
	return s.Split(delimiter);
    }


*/
    /*public string setFieldText(string s)
    {
	if (s == null) controlPanel.nameAveragesField.Text = "";
	else controlPanel.nameAveragesField.Text += s;
	return controlPanel.nameAveragesField.Text;
    }*/
/* graveyard
    public Series produceSeries__oldcode(
	    BenchRound br,
	    DetailLevel detail,
	    Access type,
	    int i) 
    {
	string sname = (type == Access.read ? "read" : "write");

	// this produces Chart of data pivotchart of benchround..
	Chart chart = br.getDataChart(detail);	
	Series series = new Series();

	series.ChartArea = "histogram";
	series.Legend = "Legend";
	series.Name = harness.getPivotDumpHeader(i) + " (" + sname + ")";

Console.WriteLine("produceSeries: series.Name: {0}", series.Name);
	if (detail == DetailLevel.fulltype) {
	    br.registerSeriesName(series.Name, type);
	}

	// copying data points 
	DataPoint[] dp = new DataPoint[chart.Series[sname].Points.Count];

	// copy points data from Series[sname] to dp array starting at idx 0: 
	chart.Series[sname].Points.CopyTo(dp, 0);
	// now insert the points to new series one by one
	for (int j = 0; j < dp.Length; j++) {
	    series.Points.Insert(j, dp[j]);
	}

	// set chart graphic properties
	series.BorderWidth = 2;
	series.ChartType = SeriesChartType.FastLine;
	if (i <= 6) {
	    series.Color = (type == Access.read ? readColors[i] : writeColors[i]);
	} else {
	    series.Color = Color.FromArgb(getRandomColorState(type == Access.read));
	}

	// take care of BetterSeries 
	// (got rid of try/catch KeyNotFoundException)
	BetterSeries bs;
	if (allSeries.TryGetValue(series.Name, out bs)) {
Console.WriteLine("produceSeries: reusing BetterSeries");
	    bs.setContainedSeries(series, type);
	} else {
Console.WriteLine("produceSeries: Creating new BetterSeries");
	    bs = new BetterSeries();
	    bs.setContainedSeries(series, type);
	    allSeries[series.Name] = bs;
	    bs.theBenchRound = br;
	    // add to partnerlookup
	    string pname = harness.getPivotDumpHeader(i) + " (" + (type == Access.read ? "write" : "read") + ")";
	    partnerSeries.Add(pname, bs);
	}

	return series;
    }

 
    // this constructor is used for storage only - should never be displayed
    public PivotChart(XmlNode node)
    {
Console.WriteLine("PivotChart() constructor - storage only");
	allSeries = new Dictionary<string, BetterSeries>();
	partnerSeries = new Dictionary<string, BetterSeries>();
	hoverSeries = null;
	randomColorState = new Random(int.Parse("0ddfaced", System.Globalization.NumberStyles.HexNumber));
	flaggedForDeletion = new List<string>();
	selectionCount = 0;

	for (int i = 0; i < 2; i++) {
	    Chart chart = new Chart();
	    ChartArea sumCount = new ChartArea();
	    // current chart measures individual hex buckets, not sum_count, 
	    // but I don't feel like changing it
	    sumCount.Name = "sum_count"; 
	    sumCount.AxisX.ScaleView.Zoomable = true;
	    sumCount.AxisY.ScaleView.Zoomable = true;
	    sumCount.AxisY.Title = "Sample count";
	    sumCount.AxisX.Title = "Latency interval (2^x ns)";
	    Legend legend1 = new Legend();
	    legend1.Name = "Legend1";
	    chart.ChartAreas.Add(sumCount);
	    chart.Name = "Statistics";
	    chart.Legends.Add(legend1);
	    chart.TabIndex = 1;
	    chart.Text = (i == 1 ? "hex_bins" : "sum_count");
	    XmlNode stats = SafeXmlParse.selNode(node, "pmbenchmark/report/statistics");
	    XmlToChart.getHisto(chart, stats, Access.read, Color.Blue, i == 1);
	    XmlToChart.getHisto(chart, stats, Access.write, Color.Red, i == 1);
	    stats = null;
	    switch (i) {
	    case (0):
		miniChart = chart;
		break;
	    case (1):
		fullChart = chart;
		break;
	    default:
		MB.S("new pivot chart from xmlnode error: " + i);
		break;
	    }
	}
    }

    public bool unsetDeletionFlag(Access type)
    {
	if (hasPendingDeletions) {
	    switch (type) {
	    case (Access.read):
		if (hasReadSeries) readDeleteFlag = false;
		break;
	    case (Access.write):
		if (hasReadSeries) writeDeleteFlag = false;
		break;
	    default:
		MB.S("BenchRound.unsetDeletionFlag access type error");
		return true;
	    }

	    return (hasPendingDeletions = (readDeleteFlag | writeDeleteFlag));
	}

	MB.S("BenchRound.unsetDeletionFlag pending deletion flag error");
	return false;
    }

    private static void writeHexBinsToChart(
	    XmlNode bucket,
	    double interval_lo, 
	    double interval_hi,
	    Chart c, 
	    Access type)
    {
	//get the midpoint between interval values
	string sname = (type == Access.read ? "read" : "write");
	double interval_ = (interval_hi - interval_lo) / 16;
	for (int j = 0; j < 16; j++) //graph it (involves retrieving bucket sub hex index), skipping nodes with no samples
	{
	    double hex = SafeXmlParse.toDouble(bucket,
		    "bucket_hexes/hex[@index='" + j + "']");
	    //if (hex == 0) { continue; }
	    double xval = interval_lo + (0.5 + j) * interval_;
	    c.Series[sname].Points.AddXY(xval, hex);
	}
    }

    private static void writeSumCountOnlyToChart(
	    XmlNode bucket, 
	    Chart c, 
	    Access type)
    {
	string sname = (type == Access.read ? "read" : "write");
	double sum_count = SafeXmlParse.toDouble(bucket, "sum_count");
	//if (sum_count == 0) { return; }
	double interval_lo = (double)SafeXmlParse.toInt(bucket, "bucket_interval/interval_lo");
	double interval_hi = (double)SafeXmlParse.toInt(bucket, "bucket_interval/interval_hi");
	double interval_ = (interval_hi - interval_lo);
	double xval = interval_lo + (interval_ / 2);
	c.Series[sname].Points.AddXY(xval, sum_count);
    }
    // get the chart ready. Important for the Series that is produced.
    public static void getHisto__old(
	    Chart chart, 
	    XmlNode stats, 
	    Access type, 
	    bool full)
    {
	string sname = (type == Access.read ? "read" : "write");
	XmlNode histogram = SafeXmlParse.selNode(stats, "histogram[@type='" + sname + "']");

	if (histogram != null) {
	    XmlNode bucket;
	    double interval_lo, interval_hi, sum_count;
	    XmlNodeList bucket0;

	    Series series = new Series();
	    series.ChartArea = (full ? "hex_bins" : "sum_count");
	    series.Legend = "Legend1";
	    series.Name = sname;
	    chart.Series.Add(series);

	    //get all histo_buckets with index 0 (for very large and very small latencies) and deal with the first one (< 2^8 ns)
	    bucket0 = histogram.SelectNodes("histo_bucket[@index='0']");
	    bucket = bucket0.Item(0);
	    sum_count = SafeXmlParse.toDouble(bucket, "sum_count");
	    chart.Series[sname].Points.AddXY(8, sum_count);
	    chart.Series[sname].Points.AddXY(8, sum_count);

	    //intentionally miscalculates x coordinate because 
	    //(2^lo+(j+0.5)*((2^hi-2^lo)/16)) stretches the x axis
	    for (int i = 1; i < 16; i++) {
		bucket = SafeXmlParse.selNode(histogram, 
			"histo_bucket[@index='" + i + "']");
		if (full) {
		    interval_lo = (double)SafeXmlParse.toInt(bucket, 
			    "bucket_interval/interval_lo");
		    interval_hi = (double)SafeXmlParse.toInt(bucket,
			    "bucket_interval/interval_hi");
		    writeHexBinsToChart(bucket, interval_lo, interval_hi, 
			    chart, type);
		} else {
		    writeSumCountOnlyToChart(bucket, chart, type);
		}
	    }
	    
	    //deal with the rest of the index 0 histo buckets
	    for (int j = 1; j < bucket0.Count; j++) {
		bucket = bucket0.Item(j);
		writeSumCountOnlyToChart(bucket, chart, type);
	    }
	    bucket = null;
	    bucket0 = null;
	}
	histogram = null;
    }
///////////
// This is new class factored out from PivotChart.
// This class is the one that has master copy of datapoints.
///////////
public class MasterDataChart__old
{
    private Chart mini, full;

    public MasterDataChart(XmlNode node)
    {
	ChartArea ca; 
	XmlNode stats;

	mini = new Chart();
	ca = new ChartArea();
	ca.Name = "sum_count";
	mini.ChartAreas.Add(ca);

	full = new Chart();
	ca = new ChartArea();
	ca.Name = "sum_count";
	full.ChartAreas.Add(ca);

	stats = SafeXmlParse.selNode(node, "pmbenchmark/report/statistics");
	XmlToChart.getHisto(mini, stats, Access.read, false);
	XmlToChart.getHisto(mini, stats, Access.write, false);

	stats = SafeXmlParse.selNode(node, "pmbenchmark/report/statistics");
	XmlToChart.getHisto(full, stats, Access.read, true);
	XmlToChart.getHisto(full, stats, Access.write, true);

	stats = null;
    }

    public Chart getDataChart(DetailLevel detail)
    {
	switch (detail) {
	case (DetailLevel.mini):
	    return mini;
	case (DetailLevel.full):
	    return full;
	case (DetailLevel.currenttype):
	    MB.S("MDC::getChart : unsupported detail level");
	    return null;
	default:
	    return null;
	}
    }
}

    public int deleteSelectedSeries__old()
    {
	int deleted = 0;

	Chart mini = myDataChart.getDataChart(DetailLevel.mini);
	Chart full = myDataChart.getDataChart(DetailLevel.full);

	if (readDeleteFlag) {
	    if (hasReadSeries) {
		mini.Series.Remove(mini.Series.FindByName("read"));
		full.Series.Remove(full.Series.FindByName("read"));
		hasReadSeries = false;
		deleted += 1;
	    }
	}
	if (writeDeleteFlag) {
	    if (hasWriteSeries) {
		mini.Series.Remove(mini.Series.FindByName("write"));
		full.Series.Remove(full.Series.FindByName("write"));
		hasWriteSeries = false;
		deleted += 1;
	    }
	}
	if (deleted > 0) {
	    hasPendingDeletions = false;
	    wasDeletedDontBother = true;
	    return deleted;
	} else {
	    MB.S("deleteSelectedSeries: called but none deleted??");
	    return 0;
	}
    }
    */
