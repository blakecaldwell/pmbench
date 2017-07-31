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

public enum Access { read = 0, write = 1 }
public enum DetailLevel { mini = 0, full = 1 }

public static class UncheckedXmlParse
{
    public static XmlNode selNode(XmlNode where, string xpath)
    {
	return where.SelectSingleNode(xpath);
    }

    public static long toLong(XmlNode where, string xpath)
    {
	return long.Parse(selNode(where, xpath).InnerText);
    }

    public static int toInt(XmlNode where, string xpath) 
    {
	return int.Parse(selNode(where, xpath).InnerText);
    }

    public static double toDouble(XmlNode where, string xpath)
    {
	return double.Parse(selNode(where, xpath).InnerText);
    }
}

public static class DebugXmlParse
{
    // oldname: safeSelectSingleNode(XmlNode where, string xpath)
    public static XmlNode selNode(XmlNode where, string xpath)
    {
	try { 
	    return where.SelectSingleNode(xpath);
	}
	catch (System.Xml.XPath.XPathException x) {
	    MB.S("XPath error:\n" + x.ToString());
	    return null;
	}
	catch (NullReferenceException x) {
	    if (where == null) {
		MB.S("selNode() exception: 'where' is null\n"+
		    "\nxpath: " + xpath);
	    }
	    MB.S("selNode() null reference exception:\n" + x.ToString() +
		    "\nxpath: " + xpath);
	    return null;
	}
    }

    // oldname: safeParseSingleNodeLong(XmlNode where, string xpath)
    public static long toLong(XmlNode where, string xpath)
    {
	if (where == null) {
	    MB.S("(toLong) Error: received null input node");
	    return 0;
	}
	long i = 0;

	try {
	    if (selNode(where, xpath) == null) {
		MB.S("(toLong) xpath " + xpath + " returned a null reference");
	    }
	    i = long.Parse(selNode(where, xpath).InnerText);
	}
	catch (NullReferenceException x) {
	    MB.S("Exception parsing integer from node inner text:\n" + x.ToString());
	}
	catch (OverflowException x) {
	    MB.S("(toLong) Overflow exception at node " + xpath + ":\n" + x.ToString());
	}
	return i;
    }

    //oldname safeParseSingleNodeInt(XmlNode where, string xpath) 
    public static int toInt(XmlNode where, string xpath) 
    {
	if (where == null) {
	    MB.S("(toInt) Error: received null input node");
	    return 0;
	}
	int i = 0;

	try {
	    if (selNode(where, xpath) == null) {
		MB.S("toInt xpath " + xpath + " returned a null reference on node " + where.Name);
	    }
	    i = int.Parse(selNode(where, xpath).InnerText);
	}
	catch (NullReferenceException x) {
	    MB.S("Exception parsing integer from node inner text:\n" + x.ToString());
	}
	catch (OverflowException x) {
	    MB.S("(toInt) Overflow exception at node " + xpath + ", with long value " + long.Parse(selNode(where, xpath).InnerText) + ":\n" + x.ToString());
	}
	catch (FormatException x) {
	    MB.S("(toInt) Format exception at node " + where.Name + ", XPath " + xpath + ":\n" + x.ToString());
	}
	return i;
    }

    //oldname: safeParseSingleNodeDouble(XmlNode where, string xpath)
    public static double toDouble(XmlNode where, string xpath)
    {
	if (selNode(where, xpath) == null) {
	    MB.S("toDouble: Null");
	}
	if (selNode(where, xpath).InnerText == null) {
	    MB.S("toDouble: Null inner text");
	}
	try {
	    return double.Parse(selNode(where, xpath).InnerText);
	} //throwing null exceptions because node selection is causing it to run out of memory
	catch (NullReferenceException x) {
	    MB.S("toDouble: (XmlNode, " + xpath + ") Null reference exception:\n" + x.ToString());
	    return 0;
	}
    }
}   // DebugXmlParse

public static class XmlParse
{
    public static XmlNode selNode(XmlNode where, string xpath)
    { return UncheckedXmlParse.selNode(where, xpath); }
    public static long toLong(XmlNode where, string xpath)
    { return UncheckedXmlParse.toLong(where, xpath); }
    public static int toInt(XmlNode where, string xpath) 
    { return UncheckedXmlParse.toInt(where, xpath); }
    public static double toDouble(XmlNode where, string xpath)
    { return UncheckedXmlParse.toDouble(where, xpath); }
}

public class BenchRound
{
    public XmlNode roundNode; //XML node containing the trial in question, should be of type test_round
    public BenchSiblings seriesObject; //series (with identical params) this round belongs to

    public PlotPoints plotPoints; // caching histograms data

    public bool dirtyDelta; //some windows result files have impossibly large memory values from (fixed) output of negatives as unsigned
    public string customName;
    public bool flaggedForAverage { get; set; }

    public bool hasReadSeries { get; set; }
    public bool hasWriteSeries { get; set; }

    public bool hasPendingDeletions { get; set; }
    private bool readDeleteFlag { get; set; }
    private bool writeDeleteFlag { get;  set; }

    public string readSeriesName, writeSeriesName;
    public bool wasDeletedDontBother;

    public bool setDeletionFlag(Access type)
    {
	switch (type) {
	case (Access.read):
	    if (hasReadSeries) {
		hasPendingDeletions = readDeleteFlag = true;
		return true;
	    }
	    break;
	case (Access.write):
	    if (hasWriteSeries) {
		hasPendingDeletions = writeDeleteFlag = true;
		return true;
	    }
	    break;
	default:
	    MB.S("BenchRound.setDeletionFlag bad access type");
	    break;
	}
	return false;
    }

    public BenchRound() { ;}

    public BenchRound(BenchSiblings bench, XmlNode node, int i)
    {
	roundNode = node;
	seriesObject = bench;
    }

    public int jobs() { return seriesObject.benchParams.valueJobs; }
    public int ratio() { return seriesObject.benchParams.valueRatio; }
    public string operatingSystem() { return seriesObject.benchParams.operatingSystem; }
    public string swapDevice() { return seriesObject.benchParams.swapDevice; }
    public int valueMemory() { return seriesObject.benchParams.valueMemory; }
    public int valueMapsize() { return seriesObject.benchParams.valueMapsize; }
    public int valueDelay() { return seriesObject.benchParams.valueDelay; }
    public int valueNice() { return seriesObject.benchParams.valueNice; }
    public int cold() { return seriesObject.benchParams.cold; }
    public string paramsKey1() { return seriesObject.benchParams.paramsKey1; }
    public string paramsKey2() { return seriesObject.benchParams.paramsKey2; }
    public bool windowsbench() { return seriesObject.windowsbench; }

    /*
    private string[] memstrings_w = { "AvailPhys", "dwMemoryLoad", "TotalPageFile", "AvailPageFile", "AvailVirtual" };

    private bool checkNegativeDeltas() //fix a windows-only problem caused by bad programming
    {
	XmlNode a, b, delta;
	XmlNodeList meminfos = roundNode.SelectNodes("pmbenchmark/report/sys_mem_info/sys_mem_item");
	for (int i = 0; i < meminfos.Count - 1; i++)
	{
	    a = XmlParse.selNode(meminfos[i], "mem_item_info");
	    b = XmlParse.selNode(meminfos[i + 1], "mem_item_info");
	    delta = XmlParse.selNode(meminfos[i], "mem_item_delta");
	    for (int j = 0; j < memstrings_w.Length; j++)

	    {
		{
		    dirtyDelta = true;
		    XmlParse.selNode(delta, memstrings_w[j]).InnerText = (XmlParse.toLong(b, memstrings_w[j]) - XmlParse.toLong(a, memstrings_w[j])).ToString();
		}
	    }
	}
	return dirtyDelta;
    }
    */

    /*
     * create cache of plot points filled with data from xml
     */
    public void populatePlotPoints()
    {
	if (plotPoints == null) {
	    plotPoints = new PlotPoints(roundNode);
	}
    }

    /*
     * copy cached series points to chart's Series object
     */
    public void copyDataPointsTo(Series to, DetailLevel detail, Access type)
    {
	// copying data points 
	// this produces Chart of masterdatachart of benchround..
	Series from = plotPoints.getSeries(detail, type);
	DataPoint[] dp = new DataPoint[from.Points.Count];

	// copy points data from Series to dp array starting at idx 0: 
	from.Points.CopyTo(dp, 0);
	// now insert the points to new series one by one
	for (int j = 0; j < dp.Length; j++) {
	    to.Points.Insert(j, dp[j]);
	}
    }

    
    public void registerSeriesName(string s, Access t)
    {
	if (s == null) return;

	switch (t) {
	case (Access.read):
	    if (!hasReadSeries) {
		readSeriesName = s;
		hasReadSeries = true;
	    }
	    break;
	case (Access.write):
	    if (!hasWriteSeries) {
		writeSeriesName = s;
		hasWriteSeries = true;
	    }
	    break;
	default:
	    MB.S("registerSeriesName error");
	    return;
	}
    }

    private double totalSamples = -1;
    public double getTotalSamples()
    {
	if (totalSamples < 0) {
	    totalSamples = 0;
	    XmlNodeList samplecounts = this.roundNode.SelectNodes("pmbenchmark/report/result/result_thread/result_details/details_samples");

	    for (int i = 0; i < samplecounts.Count; i++) {
		totalSamples += double.Parse(samplecounts[i].InnerText);
	    }
	}
	return totalSamples;
    }

    public int deleteSelectedSeries()
    {
	int deleted = 0;

	Series mini_rd = plotPoints.getSeries(DetailLevel.mini, Access.read);
	Series mini_wr = plotPoints.getSeries(DetailLevel.mini, Access.write);
	Series full_rd = plotPoints.getSeries(DetailLevel.full, Access.read);
	Series full_wr = plotPoints.getSeries(DetailLevel.full, Access.write);

	if (readDeleteFlag) {
	    if (hasReadSeries) {
		mini_rd.Dispose();
		full_rd.Dispose();
		hasReadSeries = false;
		deleted += 1;
	    }
	}
	if (writeDeleteFlag) {
	    if (hasWriteSeries) {
		mini_wr.Dispose();
		full_wr.Dispose();
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

    public double[] calculateTimeSpent(double[] zones, Access type)
    {
	XmlNode nodeStat = XmlParse.selNode(roundNode, 
		"pmbenchmark/report/statistics");

	string sname = (type == Access.read) ? "read" : "write";

	XmlNode nodeHisto = XmlParse.selNode(nodeStat,
		"histogram[@type='" + sname + "']");

	if (nodeHisto == null) return null; 

	return XmlToSeries.getRuntime(nodeHisto, zones);
    }
}   // BenchRound


//a series of benchmarks with common parameters
public class BenchSiblings
{
    public XmlNode seriesNode, averageNode; //XML nodes for the series root, and the node containing series averages
    public List<BenchRound> Trials; //rounds from this series
    public XmlDocument theDoc;	    //hosting XML document
    public ParamSet benchParams;    //this series' params
    public BenchRound averageRound; //the BenchRound object, not to be confused with its XML node
    private delegate void addMemItemsDelegate(XmlNode item_avg, XmlNode item_, int i, int trials); //I just wanted to try these out
    public bool windowsbench = false; //temporary fix for some windows benchmarks
    public int trialsPerSeries = -1; //future plans
    public double samplesPerThread, netAverageLatency, readSpike, writeSpike, readSpikeHexVal, writeSpikeHexVal, readSamplesTotal, writeSamplesTotal; //average samples per thread, net average acces latency, highest latency count in read and write histograms, and total samples
    public int readSpikeIntervalHi = -1, writeSpikeIntervalHi = -1, readSpikeHexBinNum = -1, writeSpikeHexBinNum = -1; //for locating the read and write spikes
    public bool spikesCalculated = false;

    /*
     * calling getAverageNode populates average node.
     */
    public BenchRound getAverageRound()
    {
	if (averageRound == null) {
	    if (getAverageNode() == null) {
		MB.S("getAverageRound: still unable to create average node");
		return null;
	    }
	    averageRound = new BenchRound(this, getAverageNode(), trialsPerSeries + 1);
	    calculateSpikes();
	    this.Trials.Add(averageRound);
	}

	return averageRound;
    }

    private void calculateSpikes()
    {
	if (this.spikesCalculated) {
	    MB.S("calculateSpikes: Spikes already calculated!");
	}

	XmlNodeList readbuckets = averageNode.SelectNodes("pmbenchmark/report/statistics/histogram[@type='read']/histo_bucket");
	XmlNodeList writebuckets = averageNode.SelectNodes("pmbenchmark/report/statistics/histogram[@type='write']/histo_bucket");
	readSpike = 0;
	writeSpike = 0;
	readSpikeHexVal = 0;
	writeSpikeHexVal = 0;

	// split read/write cases as we can have read-only or write-only runs
	for (int i = 0; i < readbuckets.Count; i++) {
	    double tempSpike = XmlParse.toDouble(readbuckets[i], "sum_count");
	    if (tempSpike > readSpike) {
		readSpike = tempSpike;
		readSpikeIntervalHi = XmlParse.toInt(readbuckets[i], "bucket_interval/interval_hi");
	    }

	    if (readbuckets[i].Attributes.Item(0).Name.Equals("index") 
		 && !readbuckets[i].Attributes.Item(0).Value.Equals("0")) {
		for (int j = 0; j < 16; j++) {
		    double hexbin = XmlParse.toDouble(readbuckets[i], "bucket_hexes/hex[@index='" + j + "']");
		    if (hexbin > readSpikeHexVal) {
			readSpikeHexVal = hexbin;
			readSpikeHexBinNum = j;
		    }
		}
	    }
	}

	for (int i = 0; i < writebuckets.Count; i++) {
	    double tempSpike = XmlParse.toDouble(writebuckets[i], "sum_count");
	    if (tempSpike > writeSpike) {
		writeSpike = tempSpike;
		writeSpikeIntervalHi = XmlParse.toInt(writebuckets[i], "bucket_interval/interval_hi");
	    }
	    if (writebuckets[i].Attributes.Item(0).Name.Equals("index") 
		&& !writebuckets[i].Attributes.Item(0).Value.Equals("0")) {
		for (int j = 0; j < 16; j++) {
		    double hexbin = XmlParse.toDouble(writebuckets[i], "bucket_hexes/hex[@index='" + j + "']");
		    if (hexbin > writeSpikeHexVal) {
			writeSpikeHexVal = hexbin;
			writeSpikeHexBinNum = j;
		    }
		}
	    }
	}

	XmlNodeList netavgs = averageNode.SelectNodes("pmbenchmark/report/result/result_thread/result_netavg/netavg_us");
	XmlNodeList samples = averageNode.SelectNodes("pmbenchmark/report/result/result_thread/result_details/details_samples");
	samplesPerThread = 0;
	netAverageLatency = 0;
	for (int i = 0; i < netavgs.Count; i++) {
	    netAverageLatency += double.Parse(netavgs[i].InnerText.ToString());
	    samplesPerThread += double.Parse(samples[i].InnerText.ToString());
	}
	netAverageLatency /= netavgs.Count;
	samplesPerThread /= netavgs.Count;
	this.spikesCalculated = true;
    }

    public void displaySpikes()
    {
	MessageBox.Show(
		"Net average latency:\t" + netAverageLatency + 
		"\nAverage samples per thread:\t" + samplesPerThread + 
		"\nRead spike:\t" + readSpike + 
		" at bucket 2^" + readSpikeIntervalHi + 
		"; " + readSpikeHexVal + 
		" at bin " + readSpikeHexBinNum + 
		"\nWrite spike:\t" + writeSpike + 
		" at bucket 2^" + writeSpikeIntervalHi + 
		"; " + writeSpikeHexVal + 
		" at bin " + writeSpikeHexBinNum);
    }

    public BenchSiblings()
    {
	seriesNode = null;
	Trials = null;
	theDoc = null;
	benchParams = null;
	averageNode = null;
	averageRound = null;
    }

    /*
     * this one performs average calculation via getAverageRound() call 
     */
    public BenchSiblings(XmlNode node, XmlDocument doc, ParamSet ps)
    {
	seriesNode = node;
	theDoc = doc;
	benchParams = ps;

	try {
	    if (benchParams == null) {
		throw new NullReferenceException("Null benchparams, assuming this is a windows benchmark you're importing");
	    } else if (benchParams.operatingSystem == null) {
		windowsbench = true;
		MB.S("Benchparams are non-null, but null OS name, assuming this is a windows benchmark you're importing");
	    } else {
		windowsbench = benchParams.operatingSystem.Contains("Windows");
	    }
	}
	catch (NullReferenceException x) {
	    //go ahead and assume it is
	    MB.S(x.ToString());
	    windowsbench = true;
	}

	Trials = new List<BenchRound>();
	XmlNodeList test_rounds = seriesNode.SelectNodes("test_round");
	this.trialsPerSeries = test_rounds.Count;

	for (int i = 0; i < test_rounds.Count; i++) {
	    BenchRound br = new BenchRound(this, test_rounds.Item(i), i + 1);
	    Trials.Add(br);
	}
	test_rounds = null;

	if (getAverageRound() == null) {
	    MB.S("BenchSiblings(): Unable to generate average round");
	}
    }

    //get the node of the trial containing averages
    public XmlNode getAverageNode()
    {
	if (averageNode == null) averageNode = makeAverageNode();

	return averageNode;
    }

    //clone the first benchmark's result, statistics and 
    //sys_mem_info and insert the partial clone as last child of parent
    private XmlNode partialCloneBenchmark(int trials)
    {
	XmlNode avg = null, parent = this.seriesNode;
	XmlDocument doc = this.theDoc;
	if (doc == null) doc = parent.OwnerDocument;
	XmlNode result1, statistics, sys_mem_info, report_temp, pmb_temp;
	string report_s = "test_round[@iter='1']/pmbenchmark/report/";
	result1 = XmlParse.selNode(parent, report_s + "result").Clone();
	statistics = XmlParse.selNode(parent, report_s + "statistics").Clone();
	sys_mem_info = XmlParse.selNode(parent, report_s + "sys_mem_info").Clone();
	report_temp = doc.CreateNode(XmlNodeType.Element, "report", doc.NamespaceURI); //VERY IMPORTANT LESSON: using the wrong namespace will cause seemingly impossible shit like node.SelectSingleNode(node.FirstChild.Name) returning null
	report_temp.AppendChild(result1);
	report_temp.AppendChild(statistics);
	report_temp.AppendChild(sys_mem_info);
	pmb_temp = doc.CreateNode(XmlNodeType.Element, "pmbenchmark", doc.NamespaceURI);
	pmb_temp.AppendChild(report_temp);
	avg = doc.CreateNode(XmlNodeType.Element, "test_round", doc.NamespaceURI);
	XmlAttribute iter = doc.CreateAttribute("iter");
	iter.Value = (trials + 1).ToString();
	avg.Attributes.Append(iter);
	avg.AppendChild(pmb_temp);
	return avg;
    }

    //works for some elements in result as well
    private static void addMemItemField(
	    XmlNode item_avg, 
	    XmlNode item_, 
	    int i, 
	    string s, 
	    int trials)
    {
	try {
	    double t = XmlParse.toDouble(item_avg, s);
	    t += XmlParse.toDouble(item_, s);
	    if (i == trials) {
		t /= (float)trials;
	    }
	    XmlParse.selNode(item_avg, s).InnerText = t.ToString();
	}
	catch (NullReferenceException x) {
	    MB.S("addMemItemField: Adding field " + s + 
		    " for round " + i + ":\n" + x.ToString());
	}
    }

    //add thread 2's result data to thread 1's, then divide by 5 if i == 5
    private static void addThreadResults(
	    XmlNode thread_avg, 
	    XmlNode thread_, 
	    int i, 
	    int trials)
    {
	addMemItemField(thread_avg, thread_, i, "result_netavg/netavg_us", trials);
	addMemItemField(thread_avg, thread_, i, "result_netavg/netavg_clk", trials);
	addMemItemField(thread_avg, thread_, i, "result_details/details_latency/latency_us", trials);
	addMemItemField(thread_avg, thread_, i, "result_details/details_latency/latency_clk", trials);
	addMemItemField(thread_avg, thread_, i, "result_details/details_samples", trials);
	addMemItemField(thread_avg, thread_, i, "result_details/details_overhead/overhead_us", trials);
	addMemItemField(thread_avg, thread_, i, "result_details/details_overhead/overhead_clk", trials);
	addMemItemField(thread_avg, thread_, i, "result_details/details_total", trials);
    }

    //add the second histogram to the first, then divide if it's round 5
    public static bool addHistograms(
	    XmlNode histo_avg, 
	    XmlNode histo, 
	    int round, 
	    int trials) 
    {
	XmlNode bucket, bucket_avg;
	double sum_temp, term_temp;

	for (int j = 1; j <= 15; j++) {
	    //get node 6's bucket
	    try {
		bucket_avg = XmlParse.selNode(histo_avg, "histo_bucket[@index='" + j + "']");
	    }
	    catch (Exception x) {
		MB.S("Error: selecting bucket " + j + " in histo_avg:\n" + x.ToString());
		return false;
	    }

	    //get test node's bucket
	    try {
		bucket = XmlParse.selNode(histo, "histo_bucket[@index='" + j + "']");
	    }
	    catch (Exception x) {
		MB.S("Error: selecting bucket " + j + " in histo:\n" + x.ToString());
		return false;
	    }

	    //get sum_count from node 6's bucket
	    try {
		sum_temp = XmlParse.toDouble(bucket_avg, "sum_count");
	    }
	    catch (Exception x) {
		MB.S("Error: retrieving/parsing sum_temp of bucket_avg " + j + ":\n" + x.ToString());
		return false;
	    }

	    //add sum_count from test node's bucket
	    try {
		term_temp = XmlParse.toDouble(bucket, "sum_count");
	    }
	    catch (Exception x) {
		MB.S("Error: parsing/adding sum_temp of bucket " + j + ":\n" + x.ToString());
		return false;
	    }
	    sum_temp += term_temp;

	    if (round == trials) { // this.trialsPerSeries)
		sum_temp /= trials; // PerSeries;
	    }

	    //update node 6's bucket
	    try {
		XmlParse.selNode(bucket_avg, "sum_count").InnerText = sum_temp.ToString();
	    }
	    catch (Exception x) {
		MB.S("Error updating sum for bucket " + j + " in histo_avg:\n" + x.ToString());
		return false;
	    }

	    for (int k = 0; k < 16; k++) {
		try {
		    addMemItemField(bucket_avg, bucket, round, "bucket_hexes/hex[@index='" + k + "']", trials);
		}
		catch (Exception x) {
		    MB.S("Error adding hex " + k + " in bucklet " + j + ":\n" + x.ToString());
		    return false;
		}
	    }
	}
	//fix for a poor choice I made in writing the XML
	XmlNodeList bucket0, bucket0_avg;

	try {
	    bucket0_avg = histo_avg.SelectNodes("bucket_hexes/hex[@index='0']");
	    bucket0 = histo.SelectNodes("bucket_hexes/hex[@index='0']");
	}
	catch (System.Xml.XPath.XPathException x) {
	    MB.S("Error getting nodes for bucket0:\n" + x.ToString());
	    return false;
	}

	if (bucket0.Count != bucket0_avg.Count) {
	    MB.S("Error: somehow, current node and average have a different number of bucket0's");
	    return false;
	}

	for (int j = 0; j < bucket0.Count; j++) {
	    try {
		bucket_avg = bucket0_avg.Item(j); //why can't you access these like an array?
		bucket = bucket0.Item(j);
		addMemItemField(bucket_avg, bucket, round, "sum_count", trials);
	    }
	    catch (Exception x) {
		MB.S("Error updating sum count for " + j + "th bucket 0:\n" + x.ToString());
		return false;
	    }
	}
	return true;
    }

    private static void addMemItemsLinux(
	    XmlNode item_avg, 
	    XmlNode item_, 
	    int i, 
	    int trials)
    {
	try {
	    addMemItemField(item_avg, item_, i, "free_kib", trials);
	    addMemItemField(item_avg, item_, i, "buffer_kib", trials);
	    addMemItemField(item_avg, item_, i, "cache_kib", trials);
	    addMemItemField(item_avg, item_, i, "active_kib", trials);
	    addMemItemField(item_avg, item_, i, "inactive_kib", trials);
	    addMemItemField(item_avg, item_, i, "pgpgin", trials);
	    addMemItemField(item_avg, item_, i, "pgpgout", trials);
	    addMemItemField(item_avg, item_, i, "pswpin", trials);
	    addMemItemField(item_avg, item_, i, "pswpout", trials);
	    addMemItemField(item_avg, item_, i, "pgmajfault", trials);
	}
	catch (NullReferenceException x) {
	    MB.S("(addMemItemsLinux) Null reference " + x.ToString());
	}
    }

    private static void addMemItemsWindows(XmlNode item_avg, XmlNode item_, int i, int trials)
    {
	addMemItemField(item_avg, item_, i, "AvailPhys", trials);
	addMemItemField(item_avg, item_, i, "dwMemoryLoad", trials);
	addMemItemField(item_avg, item_, i, "TotalPageFile", trials);
	addMemItemField(item_avg, item_, i, "AvailPageFile", trials);
	addMemItemField(item_avg, item_, i, "AvailVirtual", trials);
    }

    //do something with the average of some benchmarks
    private XmlNode makeAverageNode()
    {
	if (this.trialsPerSeries == 1) {
	    return this.Trials[0].roundNode;
	}
	if (averageNode != null) {
	    MB.S("makeAverageNode: Attempted to insert average node for a series that already has one");
	    return averageNode;
	}

	int ratio, jobs, cold;
	XmlNode histo;
	XmlNode histo_avg;
	XmlNode result_;
	XmlNode result_avg;
	XmlNode thread_avg;
	XmlNode thread_;
	XmlNode item_;
	XmlNode item_avg;
	XmlNodeList sys_mem_items_, sys_mem_items_avg;
	addMemItemsDelegate addMemItems;
	if (windowsbench) {
	    addMemItems = addMemItemsWindows;
	} else {
	    addMemItems = addMemItemsLinux;
	}

	string report_s = "pmbenchmark/report";
	try {
	    string params_s = "test_round/" + report_s + "/signature/params/";
	    ratio = XmlParse.toInt(seriesNode, params_s + "ratio");
	    jobs = XmlParse.toInt(seriesNode, params_s + "jobs");
	    cold = XmlParse.toInt(seriesNode, params_s + "cold");
	}
	catch (Exception x) { 
	    MB.S("makeAverageNode: unable to retrieve ratio parameter:" + x.ToString());
	    return null; 
	}
cold += 0; //  suppress unused variable warning
	XmlNode avg = partialCloneBenchmark(trialsPerSeries);
	seriesNode.AppendChild(avg);

	string result_s = report_s + "/result";
	string meminfo_s = report_s + "/sys_mem_info/sys_mem_item";
	result_avg = XmlParse.selNode(avg, result_s);
	sys_mem_items_avg = avg.SelectNodes(meminfo_s);
	string histo_s = report_s + "/statistics/histogram";
	string round_s;
	for (int i = 2; i < this.trialsPerSeries + 1; i++)
	{
	    round_s = "test_round[@iter = '" + i + "']";
	    //average the individual thread results
	    result_ = XmlParse.selNode(seriesNode, round_s + "/" + result_s);
	    for (int j = 1; j <= jobs; j++)
	    {
		thread_avg = XmlParse.selNode(result_avg, "result_thread[@thread_num='" + j + "']");
		thread_ = XmlParse.selNode(result_, "result_thread[@thread_num='" + j + "']");
		addThreadResults(thread_avg, thread_, i, trialsPerSeries);
	    }
	    //average the histograms
	    if (ratio > 0) //deal with read histogram here
	    {
		histo_avg = XmlParse.selNode(avg, histo_s + "[@type='read']");
		histo = XmlParse.selNode(seriesNode, round_s + "/" + histo_s + "[@type='read']");
		if (!addHistograms(histo_avg, histo, i, trialsPerSeries)) {
		    MB.S("makeAverageNode: Error adding read histograms");
		    return null;
		}
	    }

	    if (ratio < 100)
	    {
		histo_avg = XmlParse.selNode(avg, histo_s + "[@type='write']");
		if (histo_avg == null) {
		    MB.S("makeAverageNode: This series of benches (at " + avg.Name + ") have no write histograms at " + histo_s + "[@type='write']" + ", apparently ");
		    return null;
		}
		histo = XmlParse.selNode(seriesNode, round_s + "/" + histo_s + "[@type='write']");
		if (!addHistograms(histo_avg, histo, i, trialsPerSeries)) {
		    MB.S("makeAverageNode: adding write histograms");
		    return null;
		}
	    }

	    //sys_mem_items
	    sys_mem_items_ = seriesNode.SelectNodes(round_s + "/" + meminfo_s);
	    for (int j = 0; j < sys_mem_items_avg.Count; j++)
	    {
		item_avg = XmlParse.selNode(sys_mem_items_avg.Item(j), "mem_item_info");
		item_ = XmlParse.selNode(sys_mem_items_.Item(j), "mem_item_info");
		addMemItems(item_avg, item_, i, trialsPerSeries);
		if (j != sys_mem_items_avg.Count - 1)
		{
		    item_avg = XmlParse.selNode(sys_mem_items_avg.Item(j), "mem_item_delta");
		    item_ = XmlParse.selNode(sys_mem_items_.Item(j), "mem_item_delta");
		    addMemItems(item_avg, item_, i, trialsPerSeries);
		}
	    }
	}
	return avg;
    }

}   // BenchSiblings

public class ParamSet
{
    public int indexKernel, indexDevice, indexMemory, indexMapsize, indexJobs, indexDelay, indexRatio, indexNice, valueMemory, valueMapsize, valueJobs, valueDelay, valueRatio, valueNice;
    public string operatingSystem, swapDevice;
    public string paramsKey1, paramsKey2;
    public int duration, setsize, quiet, cold, offset;
    public string shape, pattern, access, tsops;
//        private static int[] physMemValues = { 256, 512, 1024, 2048, 4096, 8192, 16384 };
    private static int[] mapSizeValues = { 512, 1024, 2048, 4096, 8192, 16384, 32768 };
    private static int[] jobsValues = { 1, 8 };
    private static int[] delayValues = { 0, 1000 };
    private static int[] ratioValues = { 0, 50, 100 };
    private static int[] niceValues = { 19, -20, 0 };

    public ParamSet()
    {
	operatingSystem = null;
	swapDevice = null;
	paramsKey1 = null;
	paramsKey2 = null;
	shape = null;
	pattern = null;
	access = null;
	tsops = null;
    }

    public string printReadableParams()
    {
	return operatingSystem + " " 
	    + swapDevice + " " 
	    + valueMemory + " MiB, with parameters -m " 
	    + valueMapsize + " -j " 
	    + valueJobs + " -d " 
	    + valueDelay + " -r " 
	    + valueRatio + " -n " 
	    + valueNice;
    }

    public void setParamsFromNode(XmlNode p) //This is ALSO not a constructor
    {
	if (p == null) {
	    MB.S("setParamsFromNode: received null input node");
	    return;
	}
	duration = XmlParse.toInt(p, "duration");
	valueMapsize = XmlParse.toInt(p, "mapsize");
	setsize = XmlParse.toInt(p, "setsize");
	valueJobs = XmlParse.toInt(p, "jobs");
	valueDelay = XmlParse.toInt(p, "delay");
	valueRatio = XmlParse.toInt(p, "ratio");
	shape = XmlParse.selNode(p, "shape").InnerText;
	quiet = XmlParse.toInt(p, "quiet");
	cold = XmlParse.toInt(p, "cold");
	offset = XmlParse.toInt(p, "offset");
	pattern = XmlParse.selNode(p, "pattern").InnerText;
	access = XmlParse.selNode(p, "access").InnerText;
	tsops = XmlParse.selNode(p, "tsops").InnerText;
    }

    public void setKey1IndicesFromKey(string key1)
    {
	char[] delimiter = { '_' };
	string[] key1_split = key1.Split(delimiter);
	this.indexKernel = int.Parse(key1_split[0]);
	this.indexDevice = int.Parse(key1_split[1]);
	this.indexMemory = int.Parse(key1_split[2]);
	this.paramsKey1 = key1;
    }

    public void setKey2ValuesFromKey(string key2)
    {
	char[] delimiter = { '_' };
	string[] key_split = key2.Split(delimiter);

	//select the node with the user-provided parameters
	try {
	    this.valueMapsize = mapSizeValues[int.Parse(key_split[0])];
	    this.valueJobs = jobsValues[int.Parse(key_split[1])];
	    this.valueDelay = delayValues[int.Parse(key_split[2])];
	    this.valueRatio = ratioValues[int.Parse(key_split[3])];
	    this.valueNice = niceValues[int.Parse(key_split[4])];
	    this.paramsKey2 = key2;
	}
	catch (Exception x) {
	    MB.S("setKey2ValuesFromKey:\n" + x.ToString());
	}
    }

    public static ParamSet makeParamsFromKeysAndNode(string key1, string key2, XmlNode p)
    {
	if (p == null) return null;

	ParamSet ps = new ParamSet();
	ps.setKey1IndicesFromKey(key1);
	ps.setKey2ValuesFromKey(key2);
	ps.setParamsFromNode(p);

	return ps;
    }

    public string getXPath() //get XPath query string for the series node (should be named test_nice) with these parameters, relative to (any) XML document root. For replacing bad test data.
    {
	return "benchmark_set/test_content/test_mapsize[@iter='" 
	    + valueMapsize + "']/test_jobs[@iter='" 
	    + valueJobs + "']/test_delay[@iter='" 
	    + valueDelay + "']/test_ratio[@iter='" 
	    + valueRatio + "']/test_nice[@iter='" 
	    + valueNice + "']";
    }
}   // ParamSet


//////////
//
//
//
//
//
//////////
public class CsvWriter
{
    private StreamWriter outfile;

    private static string[] meminfos_headers = {
	"Pre-warmup", "Pre-run", "Mid-run", "Post-run", "Post-unmap" };
    private static string memitems_headers_linux = 
	"Free KiB,Buffer KiB,Cache KiB,Active KiB,Inactive KiB,Page in/s,Page out/s,Swap in/s,Swap out/s,Major faults\n";
    private static string memitems_headers_windows = 
	"AvailPhys,dwMemoryLoad,TotalPageFile,AvailPageFile,AvailVirtual\n";
    private static string results_headers = 
	"Thread #,Net avg. (us),Net avg. (clk),Latency (us),Latency (clk),Samples,Overhead (us),Overhead (clk)\n"; //,Total\n";
    private static string params_headers = 
	"OS/kernel,Swap device,Phys. memory,Map size,Jobs,Delay,Read/write ratio,Niceness\n";

    private void writePivotCsvSignature(int term, Harness hn)
    {
	string div1 = ",", div2 = "*";

	switch (term) {
	case (0):
	    outfile.Write((hn.pivotIndex == 0 ? div2 : hn.baseParams.operatingSystem) + div1);
	    break;
	case (1):
	    outfile.Write((hn.pivotIndex == 1 ? div2 : hn.baseParams.swapDevice) + div1);
	    break;
	case (2):
	    outfile.Write((hn.pivotIndex == 2 ? div2 : hn.baseParams.valueMemory.ToString() + "MiB") + div1);
	    break;
	case (3):
	    outfile.Write((hn.pivotIndex == 3 ? div2 : hn.baseParams.valueMapsize.ToString() + "MiB") + div1);
	    break;
	case (4):
	    outfile.Write((hn.pivotIndex == 4 ? div2 : hn.baseParams.valueJobs.ToString()) + div1);
	    break;
	case (5):
	    outfile.Write((hn.pivotIndex == 5 ? div2 : hn.baseParams.valueDelay.ToString()) + div1);
	    break;
	case (6):
	    outfile.Write((hn.pivotIndex == 6 ? div2 : hn.baseParams.valueRatio.ToString()) + div1);
	    break;
	/*case (7):
	    outfile.Write((pivotIndex == 7 ? div2 : baseParams.valueNice.ToString()));
	    break;*/
	default:
	    break;
	}
    }

    private void writeMemInfoLinux(XmlNode info)
    {
	outfile.Write
	(
	    XmlParse.selNode(info, "free_kib").InnerText + "," +
	    XmlParse.selNode(info, "buffer_kib").InnerText + "," +
	    XmlParse.selNode(info, "cache_kib").InnerText + "," +
	    XmlParse.selNode(info, "active_kib").InnerText + "," +
	    XmlParse.selNode(info, "inactive_kib").InnerText + "," +
	    XmlParse.selNode(info, "pgpgin").InnerText + "," +
	    XmlParse.selNode(info, "pgpgout").InnerText + "," +
	    XmlParse.selNode(info, "pswpin").InnerText + "," +
	    XmlParse.selNode(info, "pswpout").InnerText + "," +
	    XmlParse.selNode(info, "pgmajfault").InnerText + "\n"
	);
    }

    private void writeMemInfoWindows(XmlNode info)
    {
	outfile.Write
	(
	    XmlParse.selNode(info, "AvailPhys").InnerText + "," +
	    XmlParse.selNode(info, "dwMemoryLoad").InnerText + "," +
	    XmlParse.selNode(info, "TotalPageFile").InnerText + "," +
	    XmlParse.selNode(info, "AvailPageFile").InnerText + "," +
	    XmlParse.selNode(info, "AvailVirtual").InnerText + "\n"
	);
    }

    private void writeFullBucket(List<XmlNode> nodes, int i)
    {
	//write bucket i of all nodes in order
	double lo = Math.Pow(2, i + 7);
	double hi = Math.Pow(2, i + 8);
	double mid = (hi - lo) / 16;
	for (int j = 0; j < 16; j++) { //bucket hexes with indexes 0-15
	    double gap1 = lo + (j * mid);
	    double gap2 = gap1 + mid;
	    outfile.Write(gap1 + "," + gap2 + ",");
	    for (int k = 0; k < nodes.Count; k++) {
		outfile.Write(XmlParse.toDouble(nodes[k], "histo_bucket[@index='" + i + "']/bucket_hexes/hex[@index='" + j + "']"));
		if (k == nodes.Count - 1) {
		    outfile.Write("\n");
		} else {
		    outfile.Write(",");
		}
	    }
	}
    }

    private void writeSumCounts(XmlNode[] buckets)
    {
	if (buckets == null) {
	    MB.S("writeSumCounts error: null buckets");
	    return;
	}
	if (buckets[0] == null) {
	    MB.S("writeSumCounts error: null first element");
	    return;
	}
	if (buckets[0].Attributes.Count == 0) {
	    MB.S("writeSumCounts error: zero attributes");
	    return;
	}
	if (!buckets[0].Attributes.Item(0).Name.Equals("index")) {
	    MB.S("writeSumCounts error: attribute name is " + buckets[0].Attributes.Item(0).Name);
	    return;
	}
	try {
//            int bucket_index = int.Parse(buckets[0].Attributes.Item(0).Value);
	    double lo, hi, interval_hi = XmlParse.toInt(buckets[0], "bucket_interval/interval_hi");
	    double interval_lo = XmlParse.toInt(buckets[0], "bucket_interval/interval_lo");
	    lo = Math.Pow(2, interval_lo);
	    hi = Math.Pow(2, interval_hi);
	    outfile.Write(lo + "," + hi + ",");
	    for (int j = 0; j < buckets.Length; j++) {
		outfile.Write(XmlParse.toDouble(buckets[j], "sum_count"));
		if (j == buckets.Length - 1) {
		    outfile.Write("\n");
		} else {
		    outfile.Write(",");
		}
	    }
	}
	catch (ArgumentException x) {
	    MB.S("writeSumCounts ArgumentException:\n" + x.ToString());
	    return;
	}
    }

    private void writePivotHistogramList(List<XmlNode> nodes, bool useFull)
    {
	XmlNodeList[] bucket0s = new XmlNodeList[nodes.Count];
	outfile.Write("0,256,");
	for (int i = 0; i < nodes.Count; i++) {
	    if (nodes[i] == null) {
		MB.S("writeCommaseparatePivotHistogramList: Received null node at position " + i);
		return;
	    }

	    //bucket0s[i] contains all of the bucket 0's for round i
	    bucket0s[i] = nodes[i].SelectNodes("histo_bucket[@index='0']");

	    if ( XmlParse.toInt(bucket0s[i].Item(0),
			"bucket_interval/interval_lo") 
		    != 0)
	    {
		MB.S("writePivotHistogramList: missing hit_counts_sum on test round " + i + 1);
	    }

	    outfile.Write(XmlParse.toDouble(bucket0s[i].Item(0), "sum_count"));
	    if (i == nodes.Count - 1) {
		outfile.Write("\n");
	    } else {
		outfile.Write(",");
	    }
	}

	for (int i = 1; i < 16; i++) { //buckets with indexes 1-15
	    if (useFull) {
		writeFullBucket(nodes, i);
	    } else {
		XmlNode[] buckets = new XmlNode[nodes.Count];
		for (int k = 0; k < nodes.Count; k++) {
		    buckets[k] = XmlParse.selNode(nodes[k], "histo_bucket[@index='" + i + "']");
		}
		writeSumCounts(buckets);
	    }
	}
	for (int i = 1; i < bucket0s[0].Count; i++) { //skip the first sum_count
	    try {
		XmlNode[] bucket0s_high = new XmlNode[nodes.Count];
		for (int k = 0; k < nodes.Count; k++) {
		    bucket0s_high[k] = bucket0s[k].Item(i);
		}
		writeSumCounts(bucket0s_high);
	    }
	    catch (IndexOutOfRangeException x) {
		MB.S("Index out of range exception at " + i.ToString() + " of " + bucket0s[0].Count + ":\n" + x.ToString());
	    }
	}
	bucket0s = null;
	outfile.Write("\n");
    }

    /*
     * the public entry point
     * returns 1 if everything is OK, 0 if any error
     */
    public int exportCsvToFile(string folder, Harness hn)
    {
	string path = "";
	bool good = true;

	string csvfilename = (
	    (hn.pivotIndex == 0 ? "all" : hn.baseParams.operatingSystem) + "_" +
	    (hn.pivotIndex == 1 ? "all" : hn.baseParams.swapDevice) + "_" +
	    (hn.pivotIndex == 2 ? "all" : hn.baseParams.valueMemory.ToString() + "MiB") + "_" +
	    (hn.pivotIndex == 3 ? "all" : hn.baseParams.valueMapsize.ToString() + "MiB") + "_" +
	    (hn.pivotIndex == 4 ? "all" : hn.baseParams.valueJobs.ToString()) + "_" +
	    (hn.pivotIndex == 5 ? "all" : hn.baseParams.valueDelay.ToString()) + "_" +
	    (hn.pivotIndex == 6 ? "all" : hn.baseParams.valueRatio.ToString()) + "_" +
	    (hn.pivotIndex == 7 ? "all" : hn.baseParams.valueNice.ToString())
	);

	if (folder == null) {
	    using (SaveFileDialog save = new SaveFileDialog())
	    {
		save.Filter = "csv files (*.csv)|*.csv";
		save.FilterIndex = 1;
		save.RestoreDirectory = true;
		save.AddExtension = true;
		save.DefaultExt = "csv";
		save.FileName = csvfilename;
		save.InitialDirectory = Environment.SpecialFolder.UserProfile.ToString();
		if (save.ShowDialog() == DialogResult.OK) {
		    path = Path.GetFullPath(save.FileName);
		} else {
		    good = false;
		}
	    }
	} else {
	    path = folder + "\\" + csvfilename + ".csv";
	}

	if (!good) goto out_ret_1;

	try { 
	    outfile = new StreamWriter(path);
	}
	catch (IOException e) {
	    MessageBox.Show("Error creating file:" + e.ToString());
	    goto out_ret_0;
	}

	outfile.Write(params_headers);
	for (int i = 0; i < 8; i++) {
	    writePivotCsvSignature(i, hn);
	}
	outfile.Write("\n\n");
	XmlNode report, result;
	for (int h = 0; h < hn.rounds.Count; h++) {
	    outfile.Write(getPivotDumpHeader(h, hn) + ",");
	    outfile.Write(results_headers);
	    report = XmlParse.selNode(hn.rounds[h].roundNode,
		    "pmbenchmark/report");
	    for (int j = 1; j <= hn.rounds[h].jobs(); j++) {
		result = XmlParse.selNode(report, "result/result_thread[@thread_num='" + j + "']");
		outfile.Write
		(
		    " ," + j + "," +
		    XmlParse.toDouble(result, "result_netavg/netavg_us") + "," +
		    XmlParse.toDouble(result, "result_netavg/netavg_clk") + "," +
		    XmlParse.toDouble(result, "result_details/details_latency/latency_us") + "," +
		    XmlParse.toDouble(result, "result_details/details_latency/latency_clk") + "," +
		    XmlParse.toDouble(result, "result_details/details_samples") + "," +
		    XmlParse.toDouble(result, "result_details/details_overhead/overhead_us") + "," +
		    XmlParse.toDouble(result, "result_details/details_overhead/overhead_clk") + "\n" //"," + 
		);
	    }
	}
	outfile.Write("\n");
	report = null;
	result = null;

	List<XmlNode> histos;
	bool first = true;
	if (hn.pivotIndex == 6 || hn.baseParams.valueRatio > 0) {
	    outfile.Write("Read latencies,,");
	    histos = new List<XmlNode>();
	    for (int h = 0; h < hn.rounds.Count; h++) {
		if (hn.rounds[h].ratio() > 0) {
		    if (!first) outfile.Write(",");
		    else first = false; 
		    outfile.Write(getPivotDumpHeader(h, hn));
		    histos.Add(XmlParse.selNode(hn.rounds[h].roundNode, "pmbenchmark/report/statistics/histogram[@type='read']"));
		}
	    }
	    outfile.Write("\n");
	    //MB.S("Writing histograms for " + histos.Count + " histograms");
	    writePivotHistogramList(histos, true);
	    histos.Clear();
	}

	if (hn.pivotIndex == 6 || hn.baseParams.valueRatio < 100) {
	    first = true;
	    outfile.Write("Write latencies,,");
	    histos = new List<XmlNode>();
	    for (int h = 0; h < hn.rounds.Count; h++) {
		if (hn.rounds[h].ratio() < 100) {
		    if (!first) outfile.Write(","); 
		    else first = false;
		    outfile.Write(getPivotDumpHeader(h, hn));
		    histos.Add(XmlParse.selNode(hn.rounds[h].roundNode, "pmbenchmark/report/statistics/histogram[@type='write']"));
		}
	    }
	    outfile.Write("\n");
	    writePivotHistogramList(histos, true);
	    histos.Clear();
	}
	histos = null;

	for (int m = 0; m < hn.rounds.Count; m++) {
	    XmlNodeList sys_mem_items = hn.rounds[m].roundNode.SelectNodes("pmbenchmark/report/sys_mem_info/sys_mem_item");
	    int j = hn.rounds[m].cold();
	    if (hn.rounds[m].windowsbench()) {
		outfile.Write(getPivotDumpHeader(m, hn) + "," + memitems_headers_windows);
		for (int k = 0; k < sys_mem_items.Count; k++) {
		    outfile.Write(meminfos_headers[k + j] + ",");
		    XmlNode item = sys_mem_items.Item(k);
		    writeMemInfoWindows(
			    XmlParse.selNode(item, "mem_item_info"));
		    if (!item.Attributes.Item(0).Value.Equals("post-unmap")) {
			outfile.Write("Delta,");
			writeMemInfoWindows(
				XmlParse.selNode(item, "mem_item_delta"));
		    }
		    item = null;
		}
	    } else {
		outfile.Write(getPivotDumpHeader(m, hn) + "," + memitems_headers_linux);
		for (int k = 0; k < sys_mem_items.Count; k++) {
		    outfile.Write(meminfos_headers[k + j] + ",");
		    XmlNode item = sys_mem_items.Item(k);
		    writeMemInfoLinux(
			    XmlParse.selNode(item, "mem_item_info"));
		    if (!item.Attributes.Item(0).Value.Equals("post-unmap")) {
			outfile.Write("Delta,");
			writeMemInfoLinux(
				XmlParse.selNode(item, "mem_item_delta"));
		    }
		    item = null;
		}
	    }
	    sys_mem_items = null;
	}
	outfile.Flush();
	outfile.Close();

	if (folder == null) MB.S("Wrote CSV to " + path);

out_ret_1:
	return 1;

out_ret_0:
	return 0;
    }

    /*
     * returns string (it doesn't write to any file)
     */
    public static string getPivotDumpHeader(int i, Harness hn) //i = round #
    {
	if (hn.pivotIndex == 8) {
	    return (i == 5 ? "Average" : "Trial " + (i + 1));
	}
	switch (hn.pivotIndex) {
	case (0):   //OS/Kernel
	    return hn.rounds[i].operatingSystem();
	case (1):   //Device
	    return hn.rounds[i].swapDevice();
	case (2):   //Phys. memory
	    return (hn.rounds[i].valueMemory().ToString() + " memory");
	case (3):   //Map size
	    return (hn.rounds[i].valueMapsize().ToString() + " map");
	case (4):   //Jobs
	    return hn.rounds[i].jobs().ToString();
	case (5):   //Delay
	    switch (int.Parse(hn.rounds[i].valueDelay().ToString())) {
	    case (0):
		return "None";
	    default:
		return (hn.rounds[i].valueDelay().ToString() + " clk");
	    }
	case (6):   //Ratio
	    switch (hn.rounds[i].ratio()) {
	    case (0):
		return "Write-only";
	    case (100):
		return "Read-only";
	    default:
		return (hn.rounds[i].ratio().ToString() + "%");
	    }
	case (7):   //Nice
	    return "0"; // rounds[i].valueNice().ToString();
	case (9): //stopgap -- current 'manual' uses this
	    return hn.rounds[i].customName;
	default:
	    return "getPivotDumpHeader: (" + i + ") index " + hn.pivotIndex;
	}
    }

}   // CsvWriter


//////////////////////////////////
// Xml data importer for PivotChart 
// 
public static class XmlToSeries
{
    private static double xvalToSec(double xval)
    {
	return Math.Pow(2, xval - 30);
    }

    private static void writeHexBinsToSeries(
	    XmlNode bucket,
	    double interval_lo, 
	    double interval_hi,
	    Series series)
    {
	// get the midpoint between interval values
	double interval_ = (interval_hi - interval_lo) / 16;

	// graph it (involves retrieving bucket sub hex index)
	for (int j = 0; j < 16; j++) {
	    double hex = XmlParse.toDouble(bucket,
		    "bucket_hexes/hex[@index='" + j + "']");
	    double xval = interval_lo + (0.5 + j) * interval_;
	    series.Points.AddXY(xvalToSec(xval), hex);
	}
    }

    private static void writeSumCountOnlyToChart(XmlNode bucket, Series series)
    {
	double sum_count = XmlParse.toDouble(bucket, "sum_count");

	if (sum_count == 0) return;

	double interval_lo = (double)XmlParse.toInt(bucket, "bucket_interval/interval_lo");
	double interval_hi = (double)XmlParse.toInt(bucket, "bucket_interval/interval_hi");
	double interval_ = (interval_hi - interval_lo);
	double xval = interval_lo + (interval_ / 2);
	series.Points.AddXY(xvalToSec(xval), sum_count);
    }

    /*
     * this is needed to avoid misreprenting the area due to sparse data point
     * in the high latency samples.
     * todo: may need smoothing out the distribution..
     */
    private static void writeSumCountOnlyToChart_interpolate(XmlNode bucket, Series series)
    {
	double sum_count = XmlParse.toDouble(bucket, "sum_count");

	if (sum_count == 0) return;
	double interval_lo = (double)XmlParse.toInt(bucket, "bucket_interval/interval_lo");
	double interval_hi = (double)XmlParse.toInt(bucket, "bucket_interval/interval_hi");
	double delta = (interval_hi - interval_lo)/ 16;

	// don't smooth out when we have low sample count
	if (sum_count <= 32) {
	    for (int i = 0; i < 16; i++) {
		double xval = interval_lo + i * delta;
		series.Points.AddXY(xvalToSec(xval), 
			i == 8 ? sum_count : 0);
	    }
	} else {
	    for (int i = 0; i < 16; i++) {
		double xval = interval_lo + i * delta;
		series.Points.AddXY(xvalToSec(xval), sum_count / 16);
	    }
	}
    }
    
    private static void getHisto_body(
	    Series series, 
	    XmlNode nodeStat, 
	    string sname,
	    bool full)
    {
	XmlNode histogram = XmlParse.selNode(nodeStat,
		"histogram[@type='" + sname + "']");

	if (histogram == null) return; // nothing in there..

	XmlNode bucket;
	XmlNodeList bucket0;

	double interval_lo, interval_hi, sum_count;

	series.Name = sname;

	//get all histo_buckets with index 0 
	//(for very large and very small latencies) 
	//and deal with the first one (< 2^8 ns)
	bucket0 = histogram.SelectNodes("histo_bucket[@index='0']");
	bucket = bucket0.Item(0);
	sum_count = XmlParse.toDouble(bucket, "sum_count");

	series.Points.AddXY(xvalToSec(8), sum_count);
	series.Points.AddXY(xvalToSec(8), sum_count);

	//intentionally miscalculates x coordinate because 
	//(2^lo+(j+0.5)*((2^hi-2^lo)/16)) stretches the x axis
	for (int i = 1; i < 16; i++) {
	    bucket = XmlParse.selNode(histogram, 
		    "histo_bucket[@index='" + i + "']");
	    if (full) {
		interval_lo = (double)XmlParse.toInt(bucket, 
			"bucket_interval/interval_lo");
		interval_hi = (double)XmlParse.toInt(bucket,
			"bucket_interval/interval_hi");
		writeHexBinsToSeries(bucket, interval_lo, interval_hi, 
			series);
	    } else {
		writeSumCountOnlyToChart(bucket, series);
	    }
	}
	
	//deal with the rest of the index 0 histo buckets
	if (full) {
	    for (int i = 1; i < bucket0.Count; i++) {
		bucket = bucket0.Item(i);
		writeSumCountOnlyToChart_interpolate(bucket, series);
		//writeSumCountOnlyToChart(bucket, series);
	    }
	} else {
	    for (int i = 1; i < bucket0.Count; i++) {
		bucket = bucket0.Item(i);
		writeSumCountOnlyToChart(bucket, series);
	    }
	}
    }

    public static void getHisto(Series[] serial, XmlNode stats, bool full)
    {
	getHisto_body(serial[0], stats, "read", full);
	getHisto_body(serial[1], stats, "write", full);
    }

    // Caller should provide correct histogram node. 
    // Use full set here
    public static double[] getRuntime(XmlNode nodeHisto, double[] zones)
    {
	var result = new double[zones.Length];	// should be 5

	XmlNodeList bucket0;
	XmlNode bucket;

	double interval_lo, interval_hi;
	double sum_count;

	int current_zone = 0;
	double xval = 8;

	// advance zone
	while (xvalToSec(xval) > zones[current_zone]) current_zone++;

	bucket0 = nodeHisto.SelectNodes("histo_bucket[@index='0']");
	bucket = bucket0.Item(0);
	sum_count = XmlParse.toDouble(bucket, "sum_count");
	
	result[current_zone] += sum_count * xvalToSec(xval)/2;

	
	for (int i = 1; i < 16; i++) {
	    bucket = XmlParse.selNode(nodeHisto, 
		    "histo_bucket[@index='" + i + "']");
	    interval_lo = (double)XmlParse.toInt(bucket, 
		    "bucket_interval/interval_lo");
	    interval_hi = (double)XmlParse.toInt(bucket,
		    "bucket_interval/interval_hi");
	    double sixteenth = (interval_hi - interval_lo) / 16;

	    for (int j = 0; j < 16; j++) {
		double count = XmlParse.toDouble(bucket,
			"bucket_hexes/hex[@index='" + j + "']");
		xval = interval_lo + (0.5 + j) * sixteenth;
		while (xvalToSec(xval) > zones[current_zone]) current_zone++;
		result[current_zone] += xvalToSec(xval) * count;
	    }
	}
	
	//deal with the rest of the index 0 histo buckets
	for (int i = 1; i < bucket0.Count; i++) {
	    bucket = bucket0.Item(i);

	    sum_count = XmlParse.toDouble(bucket, "sum_count");

	    interval_lo = (double)XmlParse.toInt(bucket, "bucket_interval/interval_lo");
	    interval_hi = (double)XmlParse.toInt(bucket, "bucket_interval/interval_hi");
	    double midpoint = (interval_hi - interval_lo);

	    xval = interval_lo + (midpoint / 2);

	    while (xvalToSec(xval) > zones[current_zone]) current_zone++;
	    result[current_zone] += xvalToSec(xval) * sum_count;
	}

	return result;
    }

}   // XmlToSeries


////////////////////////////////////////////////
// This class is the one that has a copy of datapoints for Chart
public class PlotPoints
{
    private Series[] mini;
    private Series[] full;

    //
    // populates items
    public PlotPoints(XmlNode node)
    {
	XmlNode stats;

	mini = new Series[2];
	mini[0] = new Series();	// read
	mini[1] = new Series(); // write

	full = new Series[2];
	full[0] = new Series(); // read
	full[1] = new Series(); // write

	stats = XmlParse.selNode(node, "pmbenchmark/report/statistics");
	XmlToSeries.getHisto(mini, stats, false); // read and write

	stats = XmlParse.selNode(node, "pmbenchmark/report/statistics");
	XmlToSeries.getHisto(full, stats, true);
    }

    public Series getSeries(DetailLevel detail, Access type)
    {
	switch (detail) {
	case DetailLevel.mini:
	    return mini[type == Access.read ? 0: 1];
	case DetailLevel.full:
	    return full[type == Access.read ? 0: 1];
	default:
	    return null;
	}
    }
}

///////////
//
//
//
///////////
public class PivotChart
{
    // Contains and controls three Series for the same dataset.
    // keeps track of mouse selection status
    // oldname: BetterSeries
    private class Binder
    {
	public Series miniSeries, fullSeries, logSeries;
	public string seriesName;
	public Access myAccessType;

	public Color activeColor;

	public static Color unselectedColor = Color.FromArgb(32, 32, 32, 32);

	public bool selected { set; get;}
	public bool grayed { set; get;}

	public BenchRound theBenchRound;

	public Binder()
	{
	    selected = false;
	    grayed = false;
	    activeColor = unselectedColor;
	}

	public void updateSelectionColor(int sel)
	{
	    if (!selected && sel > 0) {
		if (!grayed) {
		    setColor(unselectedColor);
		    grayed = true;
		}
	    } else {
		setColor(activeColor);
		grayed = false;
	    }
	}

	public string deleteFlagYourselfIfSelected()
	{
	    if (!selected) return null;

	    selected = false;
	    theBenchRound.setDeletionFlag(myAccessType);

	    setSeriesEnabled(false);
	    return seriesName;
	}

	/*
	 * this blows up and frees all series data 
	 */
	public string finalizeDeletion(Chart mini, Chart full, Chart log)
	{
	    mini.Series.Remove(miniSeries);
	    miniSeries.Dispose();
	    miniSeries = null;

	    full.Series.Remove(fullSeries);
	    fullSeries.Dispose();
	    fullSeries = null;

	    log.Series.Remove(logSeries);
	    logSeries.Dispose();
	    logSeries = null;

	    selected = false;
	    grayed = false;

	    string s = seriesName;
	    theBenchRound.setDeletionFlag(myAccessType);
	    theBenchRound = null;
	    return s;
	}

	public int toggleSelected(int currently)
	{
	    selected = !selected;

	    if (selected) {
		theBenchRound.flaggedForAverage = true;
	    } else if (currently > 1) {
		updateSelectionColor(currently - 1);
	    }
	    return selected ? 1 : -1;
	}

	public void setSeriesEnabled(bool set)
	{
	    // only set when different
	    // this is critical to graphics performance
	    if (fullSeries.Enabled != set) {
		miniSeries.Enabled = set;
		fullSeries.Enabled = set;
		logSeries.Enabled = set;
	    }
	}

	public Color setColor(Color c)
	{
	    miniSeries.Color = c;
	    fullSeries.Color = c;
	    logSeries.Color = c;
	    return c;
	}
    }

    private class Hover : Binder
    {
	private Series lastDrawn;

	private Series initHoverSeries(Series s)
	{
	    s.ChartType = SeriesChartType.SplineArea;
	    s.Enabled = false;
	    s.IsVisibleInLegend = false;
	    
	    // add dummy entry to prevent 'can't draw Log scale'
	    // exception for empty series. This gets overriden
	    // whenever hover event updates the series.
	    s.Points.Clear();
	    s.Points.AddXY(0.1, 100); 
	    s.Points.AddXY(0.01, 10); 
	    return s;
	}

	public Hover(PivotChart pivotchart)
	{
	    string name = "hover";
	    lastDrawn = null;

	    pivotchart.miniChart.Series.Add(name);
	    miniSeries = initHoverSeries(pivotchart.miniChart.Series[name]);

	    pivotchart.fullChart.Series.Add(name);
	    fullSeries = initHoverSeries(pivotchart.fullChart.Series[name]);

	    pivotchart.logChart.Series.Add(name);
	    logSeries = initHoverSeries(pivotchart.logChart.Series[name]);
	    seriesName = name;
	}

	/*
	 * called by mouse hover event handler
	 */
	public void updateHoverSeries(Series s, Chart ch)
	{
	    if (s == null) return;

	    // the following line is important for CPU usage
	    if (ReferenceEquals(lastDrawn, s)) return;

	    setColor(s.Color);

	    // redrawing by copying data series is expensive.. but..

	    ch.Series[seriesName].Points.Clear();
	    ch.DataManipulator.CopySeriesValues(s.Name, seriesName);

	    lastDrawn = s;
	}
    }

    private Chart miniChart, fullChart, logChart;
    public Chart currentChart;	//mini, full, or log

    private Random randomColorState;

    private int selectionCount;
    private Hover hover;
    private Dictionary<string, Binder> allSeries;
    private Dictionary<string, Binder> partnerSeries;

    private Harness harness;	// back pointer

    public void testerror_dumpSelectionStatus()
    {
	Console.WriteLine("PivotChart::dumpSelectionStatus()");
	Console.WriteLine(" selectionCount:{0}", selectionCount);
	Console.WriteLine(" allSeries.Count:{0}", allSeries.Count);
	var iter = allSeries.Values.GetEnumerator();
	int i = 0;
	while (iter.MoveNext()) {
	    Console.WriteLine("  item {0}: selected={1},grayed={2}",
		    i++, iter.Current.selected, iter.Current.grayed);
	}
    }

    public void testerror_dumpChartSeries()
    {
	Console.WriteLine("PivotChart::dumpChartSeries()");

	Console.WriteLine(" allSeries.Count:{0}", allSeries.Count);
	var iter = allSeries.Keys.GetEnumerator();
	int i = 0;
	while (iter.MoveNext()) {
	    Console.WriteLine("  item {0}: Key={1}", i++, iter.Current);
	}

	Console.WriteLine(" partnerSeries.Count:{0}", partnerSeries.Count);
	var iter_ps = partnerSeries.Keys.GetEnumerator();
	i = 0;
	while (iter_ps.MoveNext()) {
	    Console.WriteLine("  item {0}: Key={1}", i++, iter_ps.Current);
	}

	Console.WriteLine(" miniChart.Series.Count:{0}", 
		miniChart.Series.Count);
	var iter_mi = miniChart.Series.GetEnumerator();
	i = 0;
	while (iter_mi.MoveNext()) {
	    Console.WriteLine("  item {0}:name={1}", 
		    i++, iter_mi.Current.Name);
	}

	Console.WriteLine(" fullChart.Series.Count:{0}", 
		fullChart.Series.Count);
	var iter_fu = fullChart.Series.GetEnumerator();
	i = 0;
	while (iter_fu.MoveNext()) {
	    Console.WriteLine("  item {0}:name={1}", 
		    i++, iter_fu.Current.Name);
	}

	Console.WriteLine(" logChart.Series.Count:{0}", 
		logChart.Series.Count);
	var iter_lo = logChart.Series.GetEnumerator();
	i = 0;
	while (iter_lo.MoveNext()) {
	    Console.WriteLine("  item {0}:name={1}", 
		    i++, iter_lo.Current.Name);
	}

    }

    public void resetAllSelection()
    {
	var iter = allSeries.Values.GetEnumerator();

	while (iter.MoveNext()) {
	    iter.Current.selected = false;
	    iter.Current.grayed = false;
	}

	selectionCount  = 0;
    }

    public int getSelectionCount()
    {
	return selectionCount;
    }
    
    public int deleteSelectedChartObjects()
    {
	List<string> deletionList = new List<string>();

	var iter = allSeries.Values.GetEnumerator();

	// build up list while flagging corresponding benchround obj
	while (iter.MoveNext()) {
	    string s = iter.Current.deleteFlagYourselfIfSelected();
	    if (s != null) deletionList.Add(s);
	}

	selectionCount -= deletionList.Count;

	int count = deletionList.Count;

	if (count == 0) return 0;

	foreach (var name in deletionList) {
	    Binder bs = allSeries[name];
	    allSeries.Remove(name);
	    partnerSeries.Remove(name);	// no exception thrown even if not found
	    bs.finalizeDeletion(miniChart, fullChart, logChart);
	}

	return count;
    }

    public PivotChart(Harness hn, int width, int height)
    {
	allSeries = new Dictionary<string, Binder>();
	partnerSeries = new Dictionary<string, Binder>();

	hover = null;
	randomColorState = new Random(0x0ddfaced);
	selectionCount = 0;

	harness = hn;

	for (int i = 0; i < 3; i++) {
	    Chart chart = new Chart();
	    ChartArea chartarea = new ChartArea();
	    chartarea.Name = "histogram";
	    chartarea.AxisX.Title = "Latency in second (log)";
	    chartarea.AxisX.ScaleView.Zoomable = true;
	    chartarea.AxisX.MajorTickMark.Enabled = true;
	    chartarea.AxisX.MinorTickMark.Enabled = true;
	    chartarea.AxisX.MinorTickMark.Interval = 1;
	    chartarea.AxisX.MajorGrid.Enabled = true;
//	    chartarea.AxisX.LabelStyle.Format = "##.#";
	    chartarea.AxisX.IsLogarithmic = false;

	    // mono has problem with calling CustomLabels methods..
	    //chartarea.AxisX2.Interval = 1;
	    //chartarea.AxisX2.Maximum = 32;
	    //chartarea.AxisX2.CustomLabels.Add(9.5,10.5,"1us");
	    //chartarea.AxisX2.CustomLabels.Add(19.5,20.5,"1ms");

	    chartarea.AxisY.Title = "Sample count";
	    chartarea.AxisY.ScaleView.Zoomable = true;
	    //chartarea.AxisY.IsLogarithmic = true;


	    Legend legend = new Legend();
	    legend.Name = "Legend";
	    legend.TextWrapThreshold = 80;
	    legend.DockedToChartArea = "histogram";

	    chart.ChartAreas.Add(chartarea);
	    chart.Name = "Latency histogram (" + (i == 0 ? "mini)" : "full)");
	    chart.Legends.Add(legend);
	    chart.TabIndex = 1;
	    chart.Text = "Latency histogram";

//	    chart.MouseWheel += chartZoom;
	    chart.MouseMove += chartMouseHover;
	    chart.MouseClick += chartMouseClick;
	    chart.Width = width;
	    chart.Height = height;
	    switch (i) {
	    case 0:
		miniChart = chart;
		break;
	    case 1:
		fullChart = chart;
		break;
	    case 2:
		logChart = chart;
		chartarea.AxisY.IsLogarithmic = true;
		break;
	    }
	}
    }

    public void setCurrentToFull()
    {
	currentChart = fullChart;
    }
    public void setCurrentToMini()
    {
	currentChart = miniChart;
    }
    public void setCurrentToLog()
    {
	currentChart = logChart;
    }

    // MSChart can't draw empty series with Log x scale, ugh.
    // solution here is to temporaily switch to Linear X scale
    // when we have nothing to draw. set it back to Log when
    // more series are added. Also, set it Linear when empty.
    public void setAllLogX()
    {
	miniChart.ChartAreas[0].AxisX.IsLogarithmic = true;
	fullChart.ChartAreas[0].AxisX.IsLogarithmic = true;
	logChart.ChartAreas[0].AxisX.IsLogarithmic = true;
    }
    public void setAllLinearX()
    {
	miniChart.ChartAreas[0].AxisX.IsLogarithmic = false;
	fullChart.ChartAreas[0].AxisX.IsLogarithmic = false;
	logChart.ChartAreas[0].AxisX.IsLogarithmic = false;
    }

    public void createHoverSeries()
    {
	if (hover != null) {
Console.WriteLine("createHoverSeries(): already exists");
	    return;
	}
	hover = new Hover(this);

	hover.setSeriesEnabled(false);
    }

    private static HitTestResult getHitTestResult(Chart c, Point mousepos)
    {
	Point point = new Point();
	
	point.X = mousepos.X;
	point.Y = mousepos.Y;

	return c.HitTest(c.PointToClient(point).X, c.PointToClient(point).Y);
    }

    public void chartMouseHover(object sender, EventArgs args)
    {
	HitTestResult htr = getHitTestResult(currentChart, 
		Control.MousePosition);

	if (htr.ChartElementType == ChartElementType.LegendItem) {
	    if (htr.Object == null) return; //error?

	    Series s = currentChart.Series[(htr.Object as LegendItem).SeriesName];
	    hover.updateHoverSeries(s, currentChart);
	    hover.setSeriesEnabled(true);
	} else {
	    hover.setSeriesEnabled(false);
	}
    }

    private void toggleSelection(Binder bs)
    {
	selectionCount += bs.toggleSelected(selectionCount);
    }

    /*
     * called both internally and by Harness when series deleted
     */
    public void refreshSelectionColors()
    {
	if (selectionCount < 0) {
	    MB.S("refreshSelectionColors error: negative selction count"); 
	    return;
	} 

	var iter = allSeries.Values.GetEnumerator();
	while (iter.MoveNext()) {
	    iter.Current.updateSelectionColor(selectionCount);
	}

	harness.pmgraph.updateSelectionButtons(selectionCount);
    }

    public void selectAll()
    {
	var iter = allSeries.Values.GetEnumerator();
	while (iter.MoveNext()) {
	    if (!iter.Current.selected) toggleSelection(iter.Current);
	}

	refreshSelectionColors();
    }

    public void selectNone()
    {
	var iter = allSeries.Values.GetEnumerator();
	while (iter.MoveNext()) {
	    if (iter.Current.selected) toggleSelection(iter.Current);
	}

	refreshSelectionColors();
    }

    private void chartMouseClick(object sender, EventArgs e)
    {
	MouseEventArgs mouseargs = (MouseEventArgs)e;
	HitTestResult htr = getHitTestResult(currentChart,
		Control.MousePosition);

	if (htr.ChartElementType != ChartElementType.LegendItem) return;
	
	// clicked on legend
	if (htr.Object == null) {
	    MB.S("chartMouseClick error: Null HTR");
	    return;
	} 

	LegendItem li = htr.Object as LegendItem;

	switch (mouseargs.Button) {
	case MouseButtons.Middle:
	    allSeries[li.SeriesName].theBenchRound.seriesObject.displaySpikes();
	    break;
	case MouseButtons.Left:
	    Binder bs_a = allSeries[li.SeriesName];
	    toggleSelection(bs_a); 

	    Binder bs_b;
	    if (partnerSeries.TryGetValue(bs_a.seriesName, out bs_b)) {
		toggleSelection(bs_b);
	    }

	    refreshSelectionColors();
	    break;
	case MouseButtons.Right:
	    break;
	}
    }

    private static void chartZoom(object sender, MouseEventArgs args)
    {
	Chart chart = sender as Chart;

	Axis x = chart.ChartAreas[0].AxisX;
	Axis y = chart.ChartAreas[0].AxisY;

	double xMin = x.ScaleView.ViewMinimum;
	double xMax = x.ScaleView.ViewMaximum;
	double yMin = y.ScaleView.ViewMinimum;
	double yMax = y.ScaleView.ViewMaximum;

	double x1 = 0, x2 = 0, y1 = 0, y2 = 0;

	if (args.Delta < 0) {
	    x1 = x.PixelPositionToValue(args.Location.X)- (xMax - xMin) * 2;
	    x2 = x.PixelPositionToValue(args.Location.X)+ (xMax - xMin) * 2;
	    y1 = y.PixelPositionToValue(args.Location.Y)- (yMax - yMin) * 2;
	    y2 = y.PixelPositionToValue(args.Location.Y)+ (yMax - yMin) * 2;
	}

	if (args.Delta > 0) {
	    x1 = x.PixelPositionToValue(args.Location.X)- (xMax - xMin) / 2;
	    x2 = x.PixelPositionToValue(args.Location.X)+ (xMax - xMin) / 2;
	    y1 = y.PixelPositionToValue(args.Location.Y)- (yMax - yMin) / 2;
	    y2 = y.PixelPositionToValue(args.Location.Y)+ (yMax - yMin) / 2;
	}
	x.ScaleView.Zoom(x1, x2);
	y.ScaleView.Zoom(y1, y2);
    }

    public static Color[] readColors = { 
	Color.Blue, Color.Cyan, Color.Green, 
	Color.MidnightBlue, Color.DarkCyan, 
	Color.BlueViolet, Color.LimeGreen };

    public static Color[] writeColors = {
	Color.Red, Color.Fuchsia, Color.Orange,
	Color.SaddleBrown, Color.Maroon,
	Color.Chocolate, Color.HotPink};

    //  needs better color assignment...
    public Color getColor(int i, Access type)
    {
	if (i <= 6) {
	    return type == Access.read ? readColors[i] : writeColors[i];
	} else {
	    return Color.FromArgb(getRandomColorState(type == Access.read));
	}
    }

    private int getRandomColorState(bool read)
    {
	unchecked {
	return randomColorState.Next() | 
	    (read ? (int)0xff0000a0u : (int)0xffff0000u);
	}
    }

    /*
     * Log chart doesn't allow zero.
     * so we change 0 to 1 for log display.
     * This is for disply only, and we retain/use real data for csv output
     */
    private static void massageDataPoints(Series series)
    {
	// now insert the points to new series one by one
	foreach(var datapoint in series.Points) {
	    if (datapoint.YValues[0] == 0.0) {
		datapoint.YValues[0] = 1.0;
	    }
	}
    }

    /*
     * rewrite of produce Series from BenchRound using options.
     * it populates internal objects along the way.
     * (old name: collectDataPoints()) 
     */
    private Series produceSeries(BenchRound br, DetailLevel detail, Access type)
    {
	string sname = (type == Access.read ? "read" : "write");

	Series series = new Series();

	series.ChartArea = "histogram";
	series.Legend = "Legend";
	series.Name = br.customName + " (" + sname + ")";

	if (detail == DetailLevel.full) {
	    br.registerSeriesName(series.Name, type);
	}

	// copy data points from benchround's cache to this series
	br.copyDataPointsTo(series, detail, type);

	// set chart graphic properties
	series.BorderWidth = 2;
	series.ChartType = SeriesChartType.FastLine;

	return series;
    }

    private Binder addSeriesBody(BenchRound br, Access type)
    {
	Series series;
	Binder binder;
	Color color;

	binder = new Binder();
	binder.theBenchRound = br;
	color = getColor(734, type);

	// add mini series
	series = produceSeries(br, DetailLevel.mini, type);
	binder.miniSeries = series;
	miniChart.Series.Add(series);

	// add full series
	series = produceSeries(br, DetailLevel.full, type);
	binder.fullSeries = series;
	fullChart.Series.Add(series);

	// add log series
	series = produceSeries(br, DetailLevel.full, type);
	massageDataPoints(series); // zero to 1 for log drawing
	binder.logSeries = series;
	logChart.Series.Add(series);

	binder.seriesName = series.Name;
	binder.myAccessType = type;
	binder.activeColor = color;
	binder.setColor(binder.activeColor);
	
	allSeries[series.Name] = binder;
	return binder;
    }

    public void addSeries(BenchRound br)
    {
	Binder binder;

	if (br.ratio() > 0) {	// read data exist
	    binder = addSeriesBody(br, Access.read);
	    
	    // populating partnerSeries information
	    if (br.ratio() >= 100) {
		; // don't have partner series..
	    } else {
		// XXX below hack relies on how we name series..
		string pname = br.customName + " (write)";
		partnerSeries.Add(pname, binder);
	    }
	}

	if (br.ratio() < 100) {  // write data exist
	    binder = addSeriesBody(br, Access.write);

	    // populating partnerSeries information
	    if (br.ratio() <= 0) {
		; // don't have partner series..
	    } else {
		// XXX below hack relies on how we name series..
		string pname = br.customName + " (read)";
		partnerSeries.Add(pname, binder);
	    }
	}
    }
    /*
     * call this after adding series to bump hover series to front
     */
    public void bumpHoverSeries()
    {
	// bring the series to the front to better highlight
	Series s; 
	s = hover.miniSeries;
	miniChart.Series.Remove(s);
	miniChart.Series.Add(s);
	s = hover.fullSeries;
	fullChart.Series.Remove(s);
	fullChart.Series.Add(s);
	s = hover.logSeries;
	logChart.Series.Remove(s);
	logChart.Series.Add(s);
    }

    public BenchRuntimeStat getRuntimeStatsSelected(double[] threshold)
    {
	var rts = new BenchRuntimeStat();

	// we do selected series first.
	// go through the list and process selected series.
	rts.stats = new List<BenchRuntimeItem>();

	int i = 0;
	foreach (var item in allSeries) {
	    if (!item.Value.selected) continue;
	    var stat = new BenchRuntimeItem();

	    stat.bname = item.Value.seriesName; 
	    // the callee allocates array
	    stat.timespent = item.Value.theBenchRound.calculateTimeSpent(
		    threshold, item.Value.myAccessType);

	    rts.stats.Add(stat);
	    i++;
	}
	if (i != selectionCount) {
	    MB.S("selection count mismatch.");
	}

	// Build aggregate from stats using partner series info.
	rts.agg_stats = new List<BenchRuntimeItem>();
	// N.B., HashSet collection compares strings by value.
	var statAdded = new HashSet<string>();
	var iter = rts.stats.GetEnumerator();
	while (iter.MoveNext()) {
	    var stat = iter.Current;
	    if (statAdded.Contains(stat.bname)) continue;
	    var binder = allSeries[stat.bname];
	    var agg = new BenchRuntimeItem();
	   
	    // need to use original name without (read)/(write) suffix
	    agg.bname = binder.theBenchRound.customName;
	    agg.timespent = (double[]) stat.timespent.Clone();

	    statAdded.Add(stat.bname);
	    
	    // now check for partner series
	    Binder partner;
	    if (partnerSeries.TryGetValue(stat.bname, out partner)) {
		//found. we expect it to be the next item in rts.stats
		if (iter.MoveNext()) { // next item exists
		    var partner_stat = iter.Current;
		    // check if it is indeed the same by comparing names
		    if (partner_stat.bname != partner.seriesName) {
			MB.S("getRuntimeStatsSelected: name mismatch");
		    }
		    for (int j = 0; j < 5; j++) {
			agg.timespent[j] += partner_stat.timespent[j];
		    }
		    statAdded.Add(partner_stat.bname);
		} else {
		    rts.agg_stats.Add(agg);
		    break;  // out of while loop
		}
	    } else {
		//not found. probably read-only or write-only benchmark
		// nothing to do;
		;
	    }
	    rts.agg_stats.Add(agg);
	}
	return rts;
    }

}   // PivotChart


///////////
// used to transfer collected data to GUI
public class BenchRuntimeItem
{
    public string bname; // name of benchmark run
    public double[] timespent;  // sum of latency * count over ranges

    public BenchRuntimeItem() { ; }
}

public class BenchRuntimeStat
{
    public List<BenchRuntimeItem> stats; // for individual bench
    public List<BenchRuntimeItem> agg_stats; // partner series aggregated 

    public BenchRuntimeStat() { ; }
}



///////////
//
//
//
///////////
public class Harness
{
    public int pivotIndex;
    public ParamSet baseParams;
    public List<BenchRound> rounds;
    public PivotChart thePivotChart; // turn back to private after debug
    public PmGraph pmgraph;

    // this constructor is for embedded 'harness'
    public Harness(PmGraph pmg)
    {
	thePivotChart = null;
	baseParams = null;
	pivotIndex = 9;
	rounds = new List<BenchRound>(); // empty initially.
	pmgraph = pmg;
    }

    // this constructor is for old messy code
    public Harness(
	    ParamSet ps, int pivotindex, List<BenchRound> br, PmGraph pmg)
    {
	thePivotChart = null;
	baseParams = ps;
	pivotIndex = pivotindex;
	rounds = br;
	pmgraph = pmg;
    }

    private static string TextDialog(string prompt, string text)
    {
	Form dialog = new Form();
	dialog.Text = prompt;
	dialog.Height = 128;
	TextBox textbox = new TextBox();
	textbox.Text = text;
	textbox.Width = 256;
	textbox.Location = new Point (16, 16);
	Button ok = new Button(), cancel = new Button();
	ok.Name = ok.Text = "Submit";
	ok.Location = new Point(64, 48);
	ok.DialogResult = DialogResult.OK;
	dialog.AcceptButton = ok;
	cancel.Name = cancel.Text = "Cancel";
	cancel.Location = new Point(144, 48);
	dialog.CancelButton = cancel;
	dialog.Controls.AddRange(new Control[] { textbox, ok, cancel });
	DialogResult ret = dialog.ShowDialog();
	if (ret == DialogResult.OK) return text;
	return null;
    }

    /*
     * called when average selected button is clicked
     */
    public BenchSiblings averageSelected(int avgc)
    {
	pmgraph.updateSelectionButtons(0);

	XmlDocument doc = new XmlDocument();
	XmlNode fakeSeries = doc.CreateNode( XmlNodeType.Element, 
		"test_nice", doc.NamespaceURI);
	doc.AppendChild(fakeSeries);
	ParamSet ps = new ParamSet();
	int flagcount = 0;

	foreach (var r in rounds) {
	    if (!r.flaggedForAverage) continue;

	    XmlDocument tempdoc = r.roundNode.OwnerDocument;
	    XmlNode fakeRound = doc.CreateNode(XmlNodeType.Element, 
		    "test_round", doc.NamespaceURI);

	    XmlAttribute iter = doc.CreateAttribute("iter");
	    iter.Value = (flagcount++ + 1).ToString();
	    fakeRound.Attributes.Append(iter);

	    if (XmlParse.selNode(tempdoc, 
			"test_nice/test_round/pmbenchmark") == null) {
		MB.S("pmbenchmark node not found; root element is " 
			+ tempdoc.DocumentElement.Name);
	    }
	    fakeRound.AppendChild(doc.ImportNode(
			XmlParse.selNode(tempdoc, 
			    "test_nice/test_round/pmbenchmark"), true));
	    fakeSeries.AppendChild(fakeRound);

	    ps.setParamsFromNode(PmGraph.getParamsNodeFromSeriesNode(fakeSeries));
	    ps.operatingSystem = XmlParse.selNode(tempdoc, 
		    "test_nice/test_round/pmbenchmark/report/signature/pmbench_info/version_options").InnerText;
	}

	BenchSiblings bench = new BenchSiblings(fakeSeries, doc, ps);
	bench.trialsPerSeries = flagcount;

	string temp = "Average" + avgc++;
	string temp2 = TextDialog("Enter name for new average", temp);

	if (temp2 != null) bench.averageRound.customName = temp2;
	else bench.averageRound.customName = temp;

	// NB. caller calls registerXmlDocName(name, doc). 
	thePivotChart.selectNone();
	return bench;
    }

    public int deleteSelected()
    {
	// first, clean up GUI - blow up the Chart series object
	int deleted = thePivotChart.deleteSelectedChartObjects();

	if (deleted == 0) goto out_here;

	// then delete benchround's plotpoints objects
	foreach (var br in rounds) {
	    if (br.hasPendingDeletions) br.deleteSelectedSeries(); 
	}

	// now delete round item and Xml doc associated with emptied chart
	// reverse traverse as we remove from the list
	for (int i = rounds.Count - 1; i >= 0; i--) {
	    if (!rounds[i].hasReadSeries && !rounds[i].hasWriteSeries) {
		string s = rounds[i].customName;
		pmgraph.removeDeadXmlDoc(s);
		rounds.RemoveAt(i);
	    }
	}

	if (rounds.Count == 0) {
	    thePivotChart.setAllLinearX();
	}

out_here:
	thePivotChart.refreshSelectionColors();
	return deleted;
    }

    public void selectAll()
    {
	thePivotChart.selectAll();
    }

    public Chart switchToChart(string name)
    {
	switch(name) {
	case "mini":
	    thePivotChart.setCurrentToMini();
	    break;
	case "full":
	    thePivotChart.setCurrentToFull();
	    break;
	case "log":
	    thePivotChart.setCurrentToLog();
	    break;
	default:
	    MB.S("switchToChart: unrecognized chart name");
	    thePivotChart.setCurrentToFull();
	    break;
	}

	return thePivotChart.currentChart;
    }

    public void addNewBenchrounds(List<BenchRound> brs)
    {
	foreach(var br in brs) {
	    rounds.Add(br);

	    br.populatePlotPoints();
	    if (br.wasDeletedDontBother) {
Console.WriteLine("addNewBenchrounds: wasDeletedDontBother");
		continue;
	    }
	    thePivotChart.addSeries(br);
	}
	thePivotChart.setAllLogX();
	thePivotChart.bumpHoverSeries();
	thePivotChart.resetAllSelection();
	thePivotChart.refreshSelectionColors();
    }


    /*
     * newly populate charts and initialize.
     * rounds can have data, and chart will be rebuilt from it.
     */
    public Chart rebuildAndGetNewChart(int width, int height) 
    {
	thePivotChart = new PivotChart(this, width, height);

	foreach (var br in rounds) {
	    br.populatePlotPoints();

	    if (br.wasDeletedDontBother) continue;

	    thePivotChart.addSeries(br);
	}

	if (rounds.Count == 0) {
	    thePivotChart.setAllLinearX();
	} else {
	    thePivotChart.setAllLogX();
	}

	thePivotChart.createHoverSeries();

	thePivotChart.setCurrentToFull();

	return thePivotChart.currentChart;
    }

    /*
     * called by PmGraph.updateChart() <- not being used actively..
     */
    public Chart getPreparedChart()
    {
	return thePivotChart.currentChart;
    }

    /*
     * called by PmGraph.redrawManual()
     */
    public void destroyPivotChart()
    {
	if (thePivotChart == null) return;

	if (thePivotChart.currentChart == null) {
	    MB.S("destroyPivotChart: Chart already destroyed");
	    return;
	}
	thePivotChart.currentChart.Dispose();
	thePivotChart.currentChart = null;
    }

    /*
     * called when export CSV button clicked and other automated paths
     */
    public int exportCsv(string folder)
    {
	var foo = new CsvWriter();
	return foo.exportCsvToFile(folder, this);
    }

    public int getChartSelectionCount()
    {
	return thePivotChart.getSelectionCount();
    }

    // statistic retrieval. aware of currently selected items
    public BenchRuntimeStat getRuntimeStats(double[] threshold)
    {
	// provide default..
	if (threshold == null) {
	    threshold = new double[5] { 0.5e-6, 3e-6, 50e-6, 500e-6, 100 }; 
	}
	
	return thePivotChart.getRuntimeStatsSelected(threshold);
    }


}   // Harness

} // namespace PmGraphSpace
