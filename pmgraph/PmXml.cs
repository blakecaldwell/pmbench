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

/* Written by: Julian Seymour, Jisoo Yang  */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml;

namespace PmGraphNS
{

public enum AccessType { uninitialized = -1, read = 0, write = 1 }
public enum DetailLevel { uninitialized = -1, shorttype = 0, fulltype = 1, currenttype = 2 }

public static class SafeXmlParse
{
    //public static XmlNode safeSelectSingleNode(XmlNode where, string xpath)
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
	    MB.S("node selection null reference exception:\n" + x.ToString());
	    return null;
	}
	catch (OutOfMemoryException x) {
	    MB.S("Out of memory. " + x.ToString() + "You should probably just quit.");
	    GC.WaitForPendingFinalizers();
	    GC.Collect();
	    GC.RegisterForFullGCNotification(10, 10);
	    //some kind of notification here
	    while (true)
	    {
		if (GC.WaitForFullGCComplete() == GCNotificationStatus.Succeeded) break;
		Thread.Sleep(500);
	    }
	    return selNode(where, xpath);
	}
    }

    //public static long safeParseSingleNodeLong(XmlNode where, string xpath)
    public static long toLong(XmlNode where, string xpath)
    {
	if (where == null) {
	    MB.S("(toLong) Error: received null input node");
	    return 0;
	}
	long i = 0;

	try
	{
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

    //public static int safeParseSingleNodeInt(XmlNode where, string xpath) //atoi(node.selectSingleNode(xpath)), with exception handling
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

    //public static double safeParseSingleNodeDouble(XmlNode where, string xpath)
    public static double toDouble(XmlNode where, string xpath)
    {
	if (selNode(where, xpath) == null) {
	    MB.S("(toDouble) Null");
	}
	if (selNode(where, xpath).InnerText == null) {
	    MB.S("SafeParseSingleNodeDouble Null inner text");
	}
	try {
	    return double.Parse(selNode(where, xpath).InnerText);
	} //throwing null exceptions because node selection is causing it to run out of memory
	catch (NullReferenceException x) {
	    MB.S("(toDouble(XmlNode, " + xpath + ") Null reference exception:\n" + x.ToString());
	    return 0;
	}
	catch (OutOfMemoryException) {
	    return toDouble(where, xpath);
	}
    }
}   // SafeXmlParse


public class BenchRound
{
    //private XmlHierarchy hierarchy;
    public XmlNode roundNode; //XML node containing the trial in question, should be of type test_round
    public BenchSiblings seriesObject; //series (with identical params) this round belongs to
    public PivotChart myPivotChart; //chart with histograms for transcription

    public int trialNum; //which trial number among its series
    //private bool hasSumCounts; //does the node have a hit counts sum node for latencies <= 2^8 ns? Temporary fix no longer needed
    public bool dirtyDelta; //some windows result files have impossibly large memory values from (fixed) output of negatives as unsigned
    public string customName;
    private bool readDeleteFlag { get; set; }
    private bool writeDeleteFlag { get;  set; }
    public bool killMeFlag { get; set; }
    public bool flaggedForAverage { get; set; }
    public bool hasPendingDeletions { get; set; }
    public bool hasReadSeries { get; set; }
    public bool hasWriteSeries { get; set; }
//        private string readSeriesName, writeSeriesName;
    public string readSeriesName, writeSeriesName;
    public bool wasDeletedDontBother;

    public bool setDeletionFlag(AccessType type)
    {
	switch (type) {
	case (AccessType.read):
	    if (hasReadSeries) return hasPendingDeletions = readDeleteFlag = true;
	    break;
	case (AccessType.write):
	    if (hasWriteSeries) return hasPendingDeletions = writeDeleteFlag = true;
	    break;
	default:
	    MB.S("BenchRound.setDeletionFlag error");
	    break;
	}
	return false;
    }

    public bool unsetDeletionFlag(AccessType type)
    {
	if (hasPendingDeletions) {
	    switch (type) {
	    case (AccessType.read):
		if (hasReadSeries) readDeleteFlag = false;
		break;
	    case (AccessType.write):
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

    public BenchRound()
    {
	roundNode = null;
	seriesObject = null;
	myPivotChart = null;
	readDeleteFlag = false;
	writeDeleteFlag = false;
	killMeFlag = false;
	readSeriesName = null;
	writeSeriesName = null;
	flaggedForAverage = false;
	customName = null;
	hasPendingDeletions = false;
	hasReadSeries = false;
	hasWriteSeries = false;
    }

    public BenchRound(BenchSiblings bs, XmlNode node, int i)
    {
	roundNode = node;
	seriesObject = bs;
	trialNum = i;
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
	    a = SafeXmlParse.selNode(meminfos[i], "mem_item_info");
	    b = SafeXmlParse.selNode(meminfos[i + 1], "mem_item_info");
	    delta = SafeXmlParse.selNode(meminfos[i], "mem_item_delta");
	    for (int j = 0; j < memstrings_w.Length; j++)

	    {
		{
		    dirtyDelta = true;
		    SafeXmlParse.selNode(delta, memstrings_w[j]).InnerText = (SafeXmlParse.toLong(b, memstrings_w[j]) - SafeXmlParse.toLong(a, memstrings_w[j])).ToString();
		}
	    }
	}
	return dirtyDelta;
    }
    */

    public Chart getRoundChart(DetailLevel detail)
    {
	if (myPivotChart == null) myPivotChart = new PivotChart(roundNode);               
	return myPivotChart.getPivotChart(detail);
    }
    
    public void registerSeriesName(string s, AccessType t)
    {
	if (s == null) return;
	switch (t) {
	case (AccessType.read):
	    if (!hasReadSeries) {
		readSeriesName = s;
		hasReadSeries = true;
	    }
	    break;
	case (AccessType.write):
	    if (!hasWriteSeries) {
		writeSeriesName = s;
		hasWriteSeries = true;
	    }
	    break;
	default:
	    MB.S("BenchRound.registerSeriesName error");
	    return;
	}
    }

    private double totalSamples = -1;
    public double getTotalSamples()
    {
	if (totalSamples < 0) {
	    totalSamples = 0;
	    XmlNodeList samplecounts = this.roundNode.SelectNodes("pmbenchmark/report/result/result_thread/result_details/details_samples");

	    for (int i = 0; i < samplecounts.Count; i++)
	    {
		totalSamples += double.Parse(samplecounts[i].InnerText);
	    }
	}
	return totalSamples;
    }

    public int deleteSelectedSeries()
    {
	int deleted = 0;

	if (!hasPendingDeletions) return 0;

	Chart shortChart = myPivotChart.getPivotChart(DetailLevel.shorttype);
	Chart fullChart = myPivotChart.getPivotChart(DetailLevel.fulltype);
	if (readDeleteFlag) {
	    if (hasReadSeries) {
		shortChart.Series.Remove(shortChart.Series.FindByName("read"));
		fullChart.Series.Remove(fullChart.Series.FindByName("read"));
		hasReadSeries = false;
		deleted += 1;
	    }
	}
	if (writeDeleteFlag) {
	    if (hasWriteSeries) {
		shortChart.Series.Remove(shortChart.Series.FindByName("write"));
		fullChart.Series.Remove(fullChart.Series.FindByName("write"));
		hasWriteSeries = false;
		deleted += 1;
	    }
	}
	if (deleted > 0) {
	    hasPendingDeletions = false;
	    wasDeletedDontBother = true;
	    return deleted;
	} else {
	    MB.S("BenchRound.deleteSelected error");
	    return 0;
	}
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

	for (int i = 0; i < readbuckets.Count; i++)
	{
	    double tempSpike = SafeXmlParse.toDouble(readbuckets[i], "sum_count");
	    if (tempSpike > readSpike)
	    {
		readSpike = tempSpike;
		readSpikeIntervalHi = SafeXmlParse.toInt(readbuckets[i], "bucket_interval/interval_hi");
	    }
	    if (readbuckets[i].Attributes.Item(0).Name.Equals("index") && !readbuckets[i].Attributes.Item(0).Value.Equals("0"))
	    {
		for (int j = 0; j < 16; j++)
		{
		    double hexbin = SafeXmlParse.toDouble(readbuckets[i], "bucket_hexes/hex[@index='" + j + "']");
		    if (hexbin > readSpikeHexVal)
		    {
			readSpikeHexVal = hexbin;
			readSpikeHexBinNum = j;
		    }
		}
	    }

	    tempSpike = SafeXmlParse.toDouble(writebuckets[i], "sum_count");
	    if (tempSpike > writeSpike)
	    {
		writeSpike = tempSpike;
		writeSpikeIntervalHi = SafeXmlParse.toInt(writebuckets[i], "bucket_interval/interval_hi");
	    }
	    if (writebuckets[i].Attributes.Item(0).Name.Equals("index") && !writebuckets[i].Attributes.Item(0).Value.Equals("0"))
	    {
		for (int j = 0; j < 16; j++)
		{
		    double hexbin = SafeXmlParse.toDouble(writebuckets[i], "bucket_hexes/hex[@index='" + j + "']");
		    if (hexbin > writeSpikeHexVal)
		    {
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
	for (int i = 0; i < netavgs.Count; i++)
	{
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

	for (int i = 0; i < test_rounds.Count; i++)
	{
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
	result1 = SafeXmlParse.selNode(parent, report_s + "result").Clone();
	statistics = SafeXmlParse.selNode(parent, report_s + "statistics").Clone();
	sys_mem_info = SafeXmlParse.selNode(parent, report_s + "sys_mem_info").Clone();
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
    private static void addMemItemField(XmlNode item_avg, XmlNode item_, int i, string s, int trials)
    {
	try {
	    double t = SafeXmlParse.toDouble(item_avg, s);
	    t += SafeXmlParse.toDouble(item_, s);
	    if (i == trials) {
		t /= (float)trials;
	    }
	    SafeXmlParse.selNode(item_avg, s).InnerText = t.ToString();
	}
	catch (NullReferenceException x) {
	    MB.S("(addMemItemField) Adding field " + s + " for round " + i + ":\n" + x.ToString());
	}
    }

    //add thread 2's result data to thread 1's, then divide by 5 if i == 5
    private static void addThreadResults(XmlNode thread_avg, XmlNode thread_, int i, int trials)
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
    public static bool addHistograms(XmlNode histo_avg, XmlNode histo, int round, int trials) 
    {
	XmlNode bucket, bucket_avg;
	double sum_temp, term_temp;
	for (int j = 1; j <= 15; j++)
	{
	    //get node 6's bucket
	    try {
		bucket_avg = SafeXmlParse.selNode(histo_avg, "histo_bucket[@index='" + j + "']");
	    }
	    catch (Exception x) {
		MB.S("Error: selecting bucket " + j + " in histo_avg:\n" + x.ToString());
		return false;
	    }

	    //get test node's bucket
	    try {
		bucket = SafeXmlParse.selNode(histo, "histo_bucket[@index='" + j + "']");
	    }
	    catch (Exception x) {
		MB.S("Error: selecting bucket " + j + " in histo:\n" + x.ToString());
		return false;
	    }

	    //get sum_count from node 6's bucket
	    try {
		sum_temp = SafeXmlParse.toDouble(bucket_avg, "sum_count");
	    }
	    catch (Exception x) {
		MB.S("Error: retrieving/parsing sum_temp of bucket_avg " + j + ":\n" + x.ToString());
		return false;
	    }

	    //add sum_count from test node's bucket
	    try {
		term_temp = SafeXmlParse.toDouble(bucket, "sum_count");
	    }
	    catch (Exception x) {
		MB.S("Error: parsing/adding sum_temp of bucket " + j + ":\n" + x.ToString());
		return false;
	    }
	    sum_temp += term_temp;

	    if (round == trials) // this.trialsPerSeries)
	    {
		sum_temp /= trials; // PerSeries;
	    }

	    //update node 6's bucket
	    try {
		SafeXmlParse.selNode(bucket_avg, "sum_count").InnerText = sum_temp.ToString();
	    }
	    catch (Exception x) {
		MB.S("Error updating sum for bucket " + j + " in histo_avg:\n" + x.ToString());
		return false;
	    }

	    for (int k = 0; k < 16; k++)
	    {
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

	for (int j = 0; j < bucket0.Count; j++)
	{
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

    private static void addMemItemsLinux(XmlNode item_avg, XmlNode item_, int i, int trials)
    {
	try
	{
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
	    ratio = SafeXmlParse.toInt(seriesNode, params_s + "ratio");
	    jobs = SafeXmlParse.toInt(seriesNode, params_s + "jobs");
	    cold = SafeXmlParse.toInt(seriesNode, params_s + "cold");
	}
	catch (Exception x) { 
	    MB.S("makeAverageNode: unable to retrieve ratio parameter:" + x.ToString());
	    return null; 
	}
cold += 0; // JY suppress unused var warning
	XmlNode avg = partialCloneBenchmark(trialsPerSeries);
	seriesNode.AppendChild(avg);

	string result_s = report_s + "/result";
	string meminfo_s = report_s + "/sys_mem_info/sys_mem_item";
	result_avg = SafeXmlParse.selNode(avg, result_s);
	sys_mem_items_avg = avg.SelectNodes(meminfo_s);
	string histo_s = report_s + "/statistics/histogram";
	string round_s;
	for (int i = 2; i < this.trialsPerSeries + 1; i++)
	{
	    round_s = "test_round[@iter = '" + i + "']";
	    //average the individual thread results
	    result_ = SafeXmlParse.selNode(seriesNode, round_s + "/" + result_s);
	    for (int j = 1; j <= jobs; j++)
	    {
		thread_avg = SafeXmlParse.selNode(result_avg, "result_thread[@thread_num='" + j + "']");
		thread_ = SafeXmlParse.selNode(result_, "result_thread[@thread_num='" + j + "']");
		addThreadResults(thread_avg, thread_, i, trialsPerSeries);
	    }
	    //average the histograms
	    if (ratio > 0) //deal with read histogram here
	    {
		histo_avg = SafeXmlParse.selNode(avg, histo_s + "[@type='read']");
		histo = SafeXmlParse.selNode(seriesNode, round_s + "/" + histo_s + "[@type='read']");
		if (!addHistograms(histo_avg, histo, i, trialsPerSeries)) {
		    MB.S("makeAverageNode: Error adding read histograms");
		    return null;
		}
	    }

	    if (ratio < 100)
	    {
		histo_avg = SafeXmlParse.selNode(avg, histo_s + "[@type='write']");
		if (histo_avg == null) {
		    MB.S("makeAverageNode: This series of benches (at " + avg.Name + ") have no write histograms at " + histo_s + "[@type='write']" + ", apparently ");
		    return null;
		}
		histo = SafeXmlParse.selNode(seriesNode, round_s + "/" + histo_s + "[@type='write']");
		if (!addHistograms(histo_avg, histo, i, trialsPerSeries)) {
		    MB.S("makeAverageNode: adding write histograms");
		    return null;
		}
	    }

	    //sys_mem_items
	    sys_mem_items_ = seriesNode.SelectNodes(round_s + "/" + meminfo_s);
	    for (int j = 0; j < sys_mem_items_avg.Count; j++)
	    {
		item_avg = SafeXmlParse.selNode(sys_mem_items_avg.Item(j), "mem_item_info");
		item_ = SafeXmlParse.selNode(sys_mem_items_.Item(j), "mem_item_info");
		addMemItems(item_avg, item_, i, trialsPerSeries);
		if (j != sys_mem_items_avg.Count - 1)
		{
		    item_avg = SafeXmlParse.selNode(sys_mem_items_avg.Item(j), "mem_item_delta");
		    item_ = SafeXmlParse.selNode(sys_mem_items_.Item(j), "mem_item_delta");
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
	duration = SafeXmlParse.toInt(p, "duration");
	valueMapsize = SafeXmlParse.toInt(p, "mapsize");
	setsize = SafeXmlParse.toInt(p, "setsize");
	valueJobs = SafeXmlParse.toInt(p, "jobs");
	valueDelay = SafeXmlParse.toInt(p, "delay");
	valueRatio = SafeXmlParse.toInt(p, "ratio");
	shape = SafeXmlParse.selNode(p, "shape").InnerText;
	quiet = SafeXmlParse.toInt(p, "quiet");
	cold = SafeXmlParse.toInt(p, "cold");
	offset = SafeXmlParse.toInt(p, "offset");
	pattern = SafeXmlParse.selNode(p, "pattern").InnerText;
	access = SafeXmlParse.selNode(p, "access").InnerText;
	tsops = SafeXmlParse.selNode(p, "tsops").InnerText;
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

	try //select the node with the user-provided parameters
	{
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
public static class CsvWriter
{
    private static string[] meminfos_headers = { "Pre-warmup", "Pre-run", "Mid-run", "Post-run", "Post-unmap" };
    private static string memitems_headers_linux = "Free KiB,Buffer KiB,Cache KiB,Active KiB,Inactive KiB,Page in/s,Page out/s,Swap in/s,Swap out/s,Major faults\n";
    private static string memitems_headers_windows = "AvailPhys,dwMemoryLoad,TotalPageFile,AvailPageFile,AvailVirtual\n";
    private static string results_headers = "Thread #,Net avg. (us),Net avg. (clk),Latency (us),Latency (clk),Samples,Overhead (us),Overhead (clk)\n"; //,Total\n";
    private static string params_headers = "OS/kernel,Swap device,Phys. memory,Map size,Jobs,Delay,Read/write ratio,Niceness\n";

    private static void writePivotCsvSignature(int term, BenchChart bc)
    {
	string div1 = ",", div2 = "*";

	switch (term) {
	case (0):
	    bc.outfile.Write((bc.pivotIndex == 0 ? div2 : bc.baseParams.operatingSystem) + div1);
	    break;
	case (1):
	    bc.outfile.Write((bc.pivotIndex == 1 ? div2 : bc.baseParams.swapDevice) + div1);
	    break;
	case (2):
	    bc.outfile.Write((bc.pivotIndex == 2 ? div2 : bc.baseParams.valueMemory.ToString() + "MiB") + div1);
	    break;
	case (3):
	    bc.outfile.Write((bc.pivotIndex == 3 ? div2 : bc.baseParams.valueMapsize.ToString() + "MiB") + div1);
	    break;
	case (4):
	    bc.outfile.Write((bc.pivotIndex == 4 ? div2 : bc.baseParams.valueJobs.ToString()) + div1);
	    break;
	case (5):
	    bc.outfile.Write((bc.pivotIndex == 5 ? div2 : bc.baseParams.valueDelay.ToString()) + div1);
	    break;
	case (6):
	    bc.outfile.Write((bc.pivotIndex == 6 ? div2 : bc.baseParams.valueRatio.ToString()) + div1);
	    break;
	/*case (7):
	    outfile.Write((pivotIndex == 7 ? div2 : baseParams.valueNice.ToString()));
	    break;*/
	default:
	    break;
	}
    }

    public static string getPivotDumpHeader(int i, BenchChart bc) //i = crony #
    {
	if (bc.pivotIndex == 8) {
	    return (i == 5 ? "Average" : "Trial " + (i + 1));
	}
	switch (bc.pivotIndex) {
	case (0):   //OS/Kernel
	    return bc.cronies[i].operatingSystem();
	case (1):   //Device
	    return bc.cronies[i].swapDevice();
	case (2):   //Phys. memory
	    return (bc.cronies[i].valueMemory().ToString() + " memory");
	case (3):   //Map size
	    return (bc.cronies[i].valueMapsize().ToString() + " map");
	case (4):   //Jobs
	    return bc.cronies[i].jobs().ToString();
	case (5):   //Delay
	    switch (int.Parse(bc.cronies[i].valueDelay().ToString())) {
	    case (0):
		return "None";
	    default:
		return (bc.cronies[i].valueDelay().ToString() + " clk");
	    }
	case (6):   //Ratio
	    switch (bc.cronies[i].ratio()) {
	    case (0):
		return "Write-only";
	    case (100):
		return "Read-only";
	    default:
		return (bc.cronies[i].ratio().ToString() + "%");
	    }
	case (7):   //Nice
	    return "0"; // cronies[i].valueNice().ToString();
	case (9): //stopgap
	    return bc.cronies[i].customName;
	default:
	    return "ERROR getPivotDumpHeader(" + i + ") index " + bc.pivotIndex;
	}
    }

    public static int writePivotCsvDump(string folder, BenchChart bc, ref StreamWriter outfile)
    {

	string path = "";
	bool good = true;

	string csvfilename = (
	    (bc.pivotIndex == 0 ? "all" : bc.baseParams.operatingSystem) + "_" +
	    (bc.pivotIndex == 1 ? "all" : bc.baseParams.swapDevice) + "_" +
	    (bc.pivotIndex == 2 ? "all" : bc.baseParams.valueMemory.ToString() + "MiB") + "_" +
	    (bc.pivotIndex == 3 ? "all" : bc.baseParams.valueMapsize.ToString() + "MiB") + "_" +
	    (bc.pivotIndex == 4 ? "all" : bc.baseParams.valueJobs.ToString()) + "_" +
	    (bc.pivotIndex == 5 ? "all" : bc.baseParams.valueDelay.ToString()) + "_" +
	    (bc.pivotIndex == 6 ? "all" : bc.baseParams.valueRatio.ToString()) + "_" +
	    (bc.pivotIndex == 7 ? "all" : bc.baseParams.valueNice.ToString())
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

	if (good) {
	    try {
		outfile = new StreamWriter(path);
		outfile.Write(params_headers);
		for (int i = 0; i < 8; i++) {
		    writePivotCsvSignature(i, bc);
		}
		outfile.Write("\n\n");
		XmlNode report, result;
		for (int h = 0; h < bc.cronies.Count; h++)
		{
		    outfile.Write(getPivotDumpHeader(h, bc) + ",");
		    outfile.Write(results_headers);
		    report = SafeXmlParse.selNode(bc.cronies[h].roundNode, "pmbenchmark/report");
		    for (int j = 1; j <= bc.cronies[h].jobs(); j++)
		    {
			result = SafeXmlParse.selNode(report, "result/result_thread[@thread_num='" + j + "']");
			bc.outfile.Write
			(
			    " ," + j + "," +
			    SafeXmlParse.toDouble(result, "result_netavg/netavg_us") + "," +
			    SafeXmlParse.toDouble(result, "result_netavg/netavg_clk") + "," +
			    SafeXmlParse.toDouble(result, "result_details/details_latency/latency_us") + "," +
			    SafeXmlParse.toDouble(result, "result_details/details_latency/latency_clk") + "," +
			    SafeXmlParse.toDouble(result, "result_details/details_samples") + "," +
			    SafeXmlParse.toDouble(result, "result_details/details_overhead/overhead_us") + "," +
			    SafeXmlParse.toDouble(result, "result_details/details_overhead/overhead_clk") + "\n" //"," + 
			);
		    }
		}
		outfile.Write("\n");
		report = null;
		result = null;

		List<XmlNode> histos;
		bool first = true;
		if (bc.pivotIndex == 6 || bc.baseParams.valueRatio > 0)
		{
		    bc.outfile.Write("Read latencies,,");
		    histos = new List<XmlNode>();
		    for (int h = 0; h < bc.cronies.Count; h++)
		    {
			if (bc.cronies[h].ratio() > 0)
			{
			    if (!first) { bc.outfile.Write(","); }
			    else { first = false; }
			    bc.outfile.Write(getPivotDumpHeader(h, bc));
			    histos.Add(SafeXmlParse.selNode(bc.cronies[h].roundNode, "pmbenchmark/report/statistics/histogram[@type='read']"));
			}
		    }
		    bc.outfile.Write("\n");
		    //MB.S("Writing histograms for " + histos.Count + " histograms");
		    writeCommaSeparatePivotHistogramList(histos, bc);
		    histos.Clear();
		}

		if (bc.pivotIndex == 6 || bc.baseParams.valueRatio < 100) {
		    first = true;
		    bc.outfile.Write("Write latencies,,");
		    histos = new List<XmlNode>();
		    for (int h = 0; h < bc.cronies.Count; h++)
		    {
			if (bc.cronies[h].ratio() < 100)
			{
			    if (!first) bc.outfile.Write(","); 
			    else first = false;
			    bc.outfile.Write(getPivotDumpHeader(h, bc));
			    histos.Add(SafeXmlParse.selNode(bc.cronies[h].roundNode, "pmbenchmark/report/statistics/histogram[@type='write']"));
			}
		    }
		    bc.outfile.Write("\n");
		    writeCommaSeparatePivotHistogramList(histos, bc);
		    histos.Clear();
		}
		histos = null;

		for (int m = 0; m < bc.cronies.Count; m++)
		{
		    XmlNodeList sys_mem_items = bc.cronies[m].roundNode.SelectNodes("pmbenchmark/report/sys_mem_info/sys_mem_item");
		    int j = bc.cronies[m].cold();
		    if (bc.cronies[m].windowsbench())
		    {
			bc.outfile.Write(getPivotDumpHeader(m, bc) + "," + memitems_headers_windows);
			for (int k = 0; k < sys_mem_items.Count; k++)
			{
			    bc.outfile.Write(meminfos_headers[k + j] + ",");
			    XmlNode item = sys_mem_items.Item(k);
			    writeCommaSeparateMemInfoWindows(SafeXmlParse.selNode(item, "mem_item_info"), bc);
			    if (!item.Attributes.Item(0).Value.Equals("post-unmap"))
			    {
				bc.outfile.Write("Delta,");
				writeCommaSeparateMemInfoWindows(SafeXmlParse.selNode(item, "mem_item_delta"), bc);
			    }
			    item = null;
			}
		    } else {
			bc.outfile.Write(getPivotDumpHeader(m, bc) + "," + memitems_headers_linux);
			for (int k = 0; k < sys_mem_items.Count; k++)
			{
			    bc.outfile.Write(meminfos_headers[k + j] + ",");
			    XmlNode item = sys_mem_items.Item(k);
			    writeCommaSeparateMemInfoLinux(SafeXmlParse.selNode(item, "mem_item_info"), bc);
			    if (!item.Attributes.Item(0).Value.Equals("post-unmap"))
			    {
				bc.outfile.Write("Delta,");
				writeCommaSeparateMemInfoLinux(SafeXmlParse.selNode(item, "mem_item_delta"), bc);
			    }
			    item = null;
			}
		    }
		    sys_mem_items = null;
		}
		outfile.Flush();
		outfile.Close();

		if (folder == null) MB.S("Wrote CSV to " + path);
	    }
	    catch (IOException x) {
		MB.S("Error writing file to " + path + "\n" + x.ToString());
		return 0;
	    }
	}
	path = null;
	return 1;
    }

    private static void writeCommaSeparateMemInfoLinux(XmlNode info, BenchChart bc)
    {
	try {
	    bc.outfile.Write
	    (
		SafeXmlParse.selNode(info, "free_kib").InnerText + "," +
		SafeXmlParse.selNode(info, "buffer_kib").InnerText + "," +
		SafeXmlParse.selNode(info, "cache_kib").InnerText + "," +
		SafeXmlParse.selNode(info, "active_kib").InnerText + "," +
		SafeXmlParse.selNode(info, "inactive_kib").InnerText + "," +
		SafeXmlParse.selNode(info, "pgpgin").InnerText + "," +
		SafeXmlParse.selNode(info, "pgpgout").InnerText + "," +
		SafeXmlParse.selNode(info, "pswpin").InnerText + "," +
		SafeXmlParse.selNode(info, "pswpout").InnerText + "," +
		SafeXmlParse.selNode(info, "pgmajfault").InnerText + "\n"
	    );
	}
	catch (NullReferenceException x) {
	    MB.S("CsvWriter.writeCommaSeparateMemInfoLinux: Null reference\n" + x.ToString());
	}
    }

    private static void writeCommaSeparateMemInfoWindows(XmlNode info, BenchChart bc)
    {
	bc.outfile.Write
	(
	    SafeXmlParse.selNode(info, "AvailPhys").InnerText + "," +
	    SafeXmlParse.selNode(info, "dwMemoryLoad").InnerText + "," +
	    SafeXmlParse.selNode(info, "TotalPageFile").InnerText + "," +
	    SafeXmlParse.selNode(info, "AvailPageFile").InnerText + "," +
	    SafeXmlParse.selNode(info, "AvailVirtual").InnerText + "\n"
	);
    }

    private static void writeCommaSeparateFullBucket(List<XmlNode> nodes, int i, BenchChart bc)
    {
	//write bucket i of all nodes in order
	double lo = Math.Pow(2, i + 7);
	double hi = Math.Pow(2, i + 8);
	double mid = (hi - lo) / 16;
	for (int j = 0; j < 16; j++) //bucket hexes with indexes 0-15
	{
	    double gap1 = lo + (j * mid);
	    double gap2 = gap1 + mid;
	    bc.outfile.Write(gap1 + "," + gap2 + ",");
	    for (int k = 0; k < nodes.Count; k++)
	    {
		bc.outfile.Write(SafeXmlParse.toDouble(nodes[k], "histo_bucket[@index='" + i + "']/bucket_hexes/hex[@index='" + j + "']"));
		if (k == nodes.Count - 1) {
		    bc.outfile.Write("\n");
		} else {
		    bc.outfile.Write(",");
		}
	    }
	}
    }

    private static void writeCommaSeparateSumCounts(XmlNode[] buckets, BenchChart bc)
    {
	if (buckets == null) {
	    MB.S("writeCommaSeparateSumCounts error: null buckets");
	    return;
	}
	if (buckets[0] == null) {
	    MB.S("writeCommaSeparateSumCounts error: null first element");
	    return;
	}
	if (buckets[0].Attributes.Count == 0) {
	    MB.S("writeCommaSeparateSumCounts error: zero attributes");
	    return;
	}
	if (!buckets[0].Attributes.Item(0).Name.Equals("index")) {
	    MB.S("writeCommaSeparateSumCounts error: attribute name is " + buckets[0].Attributes.Item(0).Name);
	    return;
	}
	try {
//                    int bucket_index = int.Parse(buckets[0].Attributes.Item(0).Value);
	    double lo, hi, interval_hi = SafeXmlParse.toInt(buckets[0], "bucket_interval/interval_hi");
	    double interval_lo = SafeXmlParse.toInt(buckets[0], "bucket_interval/interval_lo");
	    lo = Math.Pow(2, interval_lo);
	    hi = Math.Pow(2, interval_hi);
	    bc.outfile.Write(lo + "," + hi + ",");
	    for (int j = 0; j < buckets.Length; j++)
	    {
		bc.outfile.Write(SafeXmlParse.toDouble(buckets[j], "sum_count"));
		if (j == buckets.Length - 1) bc.outfile.Write("\n");
		else bc.outfile.Write(",");
	    }
	}
	catch (ArgumentException x) {
	    MB.S("writeCommaSeparateSumCounts ArgumentException:\n" + x.ToString());
	    return;
	}
    }

    public static void writeCommaSeparatePivotHistogramList(List<XmlNode> nodes, BenchChart bc)
    {
	//MB.S("writeCommaSeparatePivotHistogramList: Received a list of " + nodes.Count + " nodes");
	XmlNodeList[] bucket0s = new XmlNodeList[nodes.Count];
	bc.outfile.Write("0,256,");
	for (int i = 0; i < nodes.Count; i++)
	{
	    if (nodes[i] == null) {
		MB.S("writeCommaseparatePivotHistogramList: Received null node at position " + i);
		return;
	    }

	    //bucket0s[i] contains all of the bucket 0's for crony i
	    bucket0s[i] = nodes[i].SelectNodes("histo_bucket[@index='0']");

	    if (SafeXmlParse.toInt(bucket0s[i].Item(0), "bucket_interval/interval_lo") != 0)
	    {
		MB.S("commaSeparateHistogramList: missing hit_counts_sum on test round " + i + 1);
	    }

	    bc.outfile.Write(SafeXmlParse.toDouble(bucket0s[i].Item(0), "sum_count"));
	    if (i == nodes.Count - 1) {
		bc.outfile.Write("\n");
	    } else {
		bc.outfile.Write(",");
	    }
	}

	for (int i = 1; i < 16; i++) //buckets with indexes 1-15
	{
	    if (bc.showFull) {
		writeCommaSeparateFullBucket(nodes, i, bc);
	    } else {
		XmlNode[] buckets = new XmlNode[nodes.Count];
		for (int k = 0; k < nodes.Count; k++)
		{
		    buckets[k] = SafeXmlParse.selNode(nodes[k], "histo_bucket[@index='" + i + "']");
		}
		writeCommaSeparateSumCounts(buckets, bc);
	    }
	}
	for (int i = 1; i < bucket0s[0].Count; i++) //skip the first sum_count
	{
	    try {
		XmlNode[] bucket0s_high = new XmlNode[nodes.Count];
		for (int k = 0; k < nodes.Count; k++)
		{
		    bucket0s_high[k] = bucket0s[k].Item(i);
		}
		writeCommaSeparateSumCounts(bucket0s_high, bc);
	    }
	    catch (IndexOutOfRangeException x) {
		MB.S("Index out of range exception at " + i.ToString() + " of " + bucket0s[0].Count + ":\n" + x.ToString());
	    }
	}
	bucket0s = null;
	bc.outfile.Write("\n");
    }
}


///////////
//
//
//
//
//
///////////
public class PivotChart
{
    private class BetterSeries
    {
	protected Series shortSeries, fullSeries, currentSeries;
	protected Point chartPoint;
	protected bool showFull { set; get; }
	private string seriesName;
	private bool selected, grayed;
	protected Color backupColor;
	static Color unselectedColor = Color.FromArgb(32, 32, 32, 32);
	public BenchRound theBenchRound { set; get; }
	private AccessType myAccessType;

	public bool getSelected() { return selected; }

	public AccessType getMyAccessType() { return myAccessType; }

	public void updateSelectionColor(int sel)
	{
	    if (!selected && sel > 0) {
		if (!grayed) {
		    saveBackupColor(shortSeries.Color);
		    setColor(unselectedColor);
		    grayed = true;
		}
	    } else {
		setColor(backupColor);
		saveBackupColor(setColor(backupColor));
		grayed = false;
	    }
	}

	public string deleteFlagYourselfIfSelected()
	{
	    if (!selected) return null;
	    selected = false;
	    theBenchRound.setDeletionFlag(this.myAccessType);
	    setSeriesEnabled(false);
	    return seriesName;
	}

	public string undeleteYourself()
	{
	    theBenchRound.unsetDeletionFlag(this.myAccessType);
	    setSeriesEnabled(true);
	    return seriesName;
	}

	public string finalizeDeletion(Chart sc, Chart fc)
	{
	    if (!selected) return null;
	    sc.Series.Remove(shortSeries);
	    shortSeries.Dispose();
	    shortSeries = null;
	    fc.Series.Remove(fullSeries);
	    fullSeries.Dispose();
	    fullSeries = null;
	    currentSeries = null;
	    selected = false;
	    grayed = false;
	    string s = seriesName;
	    theBenchRound.setDeletionFlag(this.myAccessType);
	    theBenchRound = null;
	    return s;
	}

	public int toggleSelected(int currently)
	{
	    selected = !selected;
	    if (selected) theBenchRound.flaggedForAverage = true;
	    else if (currently > 1) updateSelectionColor(currently - 1);
	    return (selected ? 1 : -1);
	}

	public bool setFull(bool b)
	{
	    showFull = b;
	    currentSeries = (showFull ? fullSeries : shortSeries);
	    return showFull;
	}

	private Series refreshCurrentSeries()
	{
	    setFull(showFull);
	    return currentSeries;
	}

	public void setSeriesEnabled(bool set)
	{
	    shortSeries.Enabled = set;
	    fullSeries.Enabled = set;
	}

	public string getSeriesName()
	{
	    if (currentSeries == null) return seriesName;
	    return currentSeries.Name;
	}

	public Color saveBackupColor(Color c)
	{
	    backupColor = c;
	    return backupColor;
	}

	public Color setColor(Color c)
	{
	    shortSeries.Color = c;
	    fullSeries.Color = c;
	    return c;
	}

	public BetterSeries()
	{
	    shortSeries = null;
	    fullSeries = null;
	    currentSeries = null;
	    seriesName = null;
	    selected = false;
	    backupColor = unselectedColor;
	    grayed = false;
	    myAccessType = AccessType.uninitialized;
	}

	public Series setContainedSeries(Series series, AccessType type)
	{
	    switch (series.Points.Count) {
	    case (250):
		if (this.fullSeries == null) fullSeries = series;
		else MB.S("PivotChart.BetterSeries.setContainedSeries error: full series " + series.Name + " is already set");
		break;
	    case (25):
		if (this.shortSeries == null) shortSeries = series;
		else MB.S("PivotChart.BetterSeries.setContainedSeries error: short series is already set");
		break;
	    default:
		MB.S("PivotChart.BetterSeries.setContainedSeries error: Found a series with " + series.Points.Count + " points, something went wrong");
		return series;
	    }

	    if (seriesName == null)
	    {
		seriesName = series.Name;
		saveBackupColor(series.Color);
		myAccessType = type;
	    }
	    return series;
	}
    }

    private class HoverSeries : BetterSeries
    {
	private Series initializeHoverSeries(Series s)
	{
	    s.ChartType = SeriesChartType.SplineArea;
	    s.Enabled = false;
	    s.IsVisibleInLegend = false;
	    return s;
	}

	public HoverSeries(PivotChart pivotchart)
	{
	    string hts = "hover_short", htf = "hover_full";
	    pivotchart.shortChart.Series.Add(hts);
	    shortSeries = initializeHoverSeries(pivotchart.shortChart.Series.FindByName(hts));
	    pivotchart.fullChart.Series.Add(htf);
	    fullSeries = initializeHoverSeries(pivotchart.fullChart.Series.FindByName(htf));
	    chartPoint = pivotchart.theBenchChart.chartPoint;
	}
    }

    private Chart shortChart, fullChart;
    public Chart currentChart;
    private Point chartPoint;
    private HoverSeries hoverSeries;
    private BenchChart theBenchChart;
    private bool showFull { set; get; }
    private Random randomColorState;
    private Dictionary<string, BetterSeries> allSeries;
    private int selectionCount;
    public List<string> flaggedForDeletion;
    private Dictionary<string, BetterSeries> partnerSeries;

    public string getBetterSeriesName(BenchRound round, AccessType type)
    {
	string append = " (" + (type == AccessType.read ? "read" : "write") + ")";
	try {
	    return allSeries[round.customName + append].getSeriesName();
	}
	catch (ArgumentException) {
	    return null;
	}
    }

    public int getSelectionCount()
    {
	return selectionCount;
    }

    public int deleteFlagSelected(bool nag) //this is complicated and cumbersome in part because I intended to add an undelete option
    {
	Dictionary<string, BetterSeries>.ValueCollection.Enumerator checkus = allSeries.Values.GetEnumerator();
	string s = null;
//                List<string> deleteus = new List<string>();
	while (checkus.MoveNext())
	{
	    s = checkus.Current.deleteFlagYourselfIfSelected();
	    if (s != null) flaggedForDeletion.Add(s);
	}
	selectionCount -= flaggedForDeletion.Count;
	return flaggedForDeletion.Count;
    }

    public int finalizeDeletions(int howmany)
    {
	int ret = flaggedForDeletion.Count;
	for (int i = 0; i < ret; i++)
	{
	    BetterSeries bs = allSeries[flaggedForDeletion[i]];
	    allSeries.Remove(flaggedForDeletion[i]);
	    bs.finalizeDeletion(shortChart, fullChart);
	}
	flaggedForDeletion.Clear();
	return ret;
    }

    private static void writeHexBinsToChart(XmlNode bucket,
	    double interval_lo, double interval_hi,
	    Chart c, AccessType type)
    {
	//get the midpoint between interval values
	string sname = (type == AccessType.read ? "read" : "write");
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

    private static void writeSumCountOnlyToChart(XmlNode bucket, Chart c, AccessType type)
    {
	string sname = (type == AccessType.read ? "read" : "write");
	double sum_count = SafeXmlParse.toDouble(bucket, "sum_count");
	//if (sum_count == 0) { return; }
	double interval_lo = (double)SafeXmlParse.toInt(bucket, "bucket_interval/interval_lo");
	double interval_hi = (double)SafeXmlParse.toInt(bucket, "bucket_interval/interval_hi");
	double interval_ = (interval_hi - interval_lo);
	double xval = interval_lo + (interval_ / 2);
	c.Series[sname].Points.AddXY(xval, sum_count);
    }

    //get the chart ready. Important for the Series that is produced.
    private static void writeHistogramToChart(
	    Chart chart, 
	    XmlNode stats, 
	    AccessType type, 
	    Color color, 
	    bool full)
    {
	string sname = (type == AccessType.read ? "read" : "write");
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
		    interval_lo = (double)SafeXmlParse.toInt(bucket, "bucket_interval/interval_lo");
		    interval_hi = (double)SafeXmlParse.toInt(bucket, "bucket_interval/interval_hi");
		    writeHexBinsToChart(bucket, interval_lo, interval_hi, chart, type);
		} else {
		    writeSumCountOnlyToChart(bucket, chart, type);
		}
	    }

	    for (int j = 1; j < bucket0.Count; j++) //deal with the rest of the index 0 histo buckets
	    {
		bucket = bucket0.Item(j);
		writeSumCountOnlyToChart(bucket, chart, type);
	    }
	    chart.Series[sname].ChartType = SeriesChartType.FastLine;
	    chart.Series[sname].Color = color;
	    bucket = null;
	    bucket0 = null;
	}
	histogram = null;
    }

    // this constructor is used for storage only - should never be displayed
    public PivotChart(XmlNode node)
    {
	partnerSeries = new Dictionary<string, BetterSeries>();
	allSeries = new Dictionary<string, BetterSeries>();
	hoverSeries = null;
	randomColorState = new Random(int.Parse("0ddfaced", System.Globalization.NumberStyles.HexNumber));
	flaggedForDeletion = new List<string>();
	selectionCount = 0;

	for (int i = 0; i < 2; i++) {
	    Chart chart = new Chart();
//Console.WriteLine("PC_n_6");
	    ChartArea sumCount = new ChartArea();
	    sumCount.Name = "sum_count"; //current chart measures individual hex buckets, not sum_count, but I don't feel like changing it

// XXX mono datavisualization doesn't support Axis props.. 
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
	    writeHistogramToChart(chart, stats, AccessType.read, Color.Blue, i == 1);
	    writeHistogramToChart(chart, stats, AccessType.write, Color.Red, i == 1);
	    stats = null;
	    switch (i) {
	    case (0):
		shortChart = chart;
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

    // this constructor is used to produced the displayed chart 
    public PivotChart(BenchChart bc, int width, int height)
    {
	partnerSeries = new Dictionary<string, BetterSeries>();
	allSeries = new Dictionary<string, BetterSeries>();
	hoverSeries = null;
	randomColorState = new Random(int.Parse("0ddfaced", System.Globalization.NumberStyles.HexNumber));
	flaggedForDeletion = new List<string>();
	selectionCount = 0;

	this.theBenchChart = bc;
	chartPoint = theBenchChart.chartPoint;

	for (int i = 0; i < 2; i++) {
	    string s = null;
	    Chart chart = new Chart();
	    ChartArea histogram = new ChartArea();
	    histogram.Name = "histogram";
	    histogram.AxisX.ScaleView.Zoomable = true;
	    histogram.AxisY.ScaleView.Zoomable = true;
	    histogram.AxisY.Title = "Sample count";
	    histogram.AxisX.Title = "Latency interval (2^x ns)" + s;
	    Legend legend1 = new Legend();
	    legend1.Name = "Legend";
	    legend1.DockedToChartArea = "histogram";
	    legend1.IsDockedInsideChartArea = true;

	    chart.ChartAreas.Add(histogram);
	    chart.Name = "Latency histogram (" + (i == 0 ? "short)" : "full)");
	    chart.Legends.Add(legend1);
	    chart.TabIndex = 1;
	    chart.Text = "histogram";

	    chart.MouseWheel += chartZoom;
	    chart.MouseEnter += chartMouseEnter;
	    chart.MouseLeave += chartMouseLeave;
	    chart.MouseMove += chartMouseHover;
	    chart.MouseClick += chartMouseClick;
	    chart.Width = width;
	    chart.Height = height;
	    switch (i) {
	    case (0):
		shortChart = chart;
		break;
	    case (1):
		fullChart = chart;
		break;
	    default:
		MB.S("New pivot chart error: " + i);
		break;
	    }
	}
    }

    public bool setFull(bool b)
    {
	try
	{
	    showFull = hoverSeries.setFull(b);
	    currentChart = (showFull ? fullChart : shortChart);
	}
	catch (NullReferenceException x)
	{
	    MB.S("PivotChart.setFull(" + b.ToString() + ")null reference exception"+x.ToString());
	}
	return showFull;
    }

    public void updateHoverSeries(Series s)
    {
	if (hoverSeries != null) {
	    hoverSeries.setSeriesEnabled(false);

	    if (s != null) {
		hoverSeries.setColor(s.Color);
		try {
		    currentChart.Series[hoverSeries.getSeriesName()].Points.Clear();
		    currentChart.DataManipulator.CopySeriesValues(s.Name, hoverSeries.getSeriesName());
		    hoverSeries.setSeriesEnabled(true);
		}
		catch (ArgumentException x) {
		    MB.S("Argument exception. Series name is " + s.Name + ".\n" + x.ToString());
		}
	    }
	} else {
	    hoverSeries = new HoverSeries(this);
	}
    }

    private static HitTestResult getHitTestResult(Chart c, Point mousepos, Point chartpoint)
    {
	chartpoint.X = mousepos.X;
	chartpoint.Y = mousepos.Y;
	return c.HitTest(c.PointToClient(chartpoint).X, c.PointToClient(chartpoint).Y);
    }

    private static void chartMouseEnter(object sender, EventArgs e)
    {
	Chart theChart = sender as Chart;
	if (!theChart.Focused) theChart.Focus();
    }

    public void chartMouseHover(object sender, EventArgs args)
    {
	HitTestResult htr = getHitTestResult(currentChart, Control.MousePosition, chartPoint);

	if (htr.ChartElementType == ChartElementType.LegendItem) {
	    if (htr.Object == null) {
		MB.S("PivotChart.chartMouseHover error: Null HTR");
	    } else {
		updateHoverSeries(currentChart.Series[(htr.Object as LegendItem).SeriesName]);
	    }
	} else {
	    updateHoverSeries(null);
	}
    }

    private void toggleSelection(BetterSeries bs)
    {
	selectionCount += bs.toggleSelected(selectionCount);
    }

    public void refreshSelectionColors()
    {
	if (selectionCount < 0) {
	    MB.S("refreshSelectionColors error: negative selction count"); 
	} else {
	    Dictionary<string, BetterSeries>.ValueCollection.Enumerator checkus = allSeries.Values.GetEnumerator();
	    while (checkus.MoveNext())
	    {
		checkus.Current.updateSelectionColor(selectionCount);
	    }
	    if (true) theBenchChart.pmgraph.updateSelectionButtons(selectionCount);
	}
    }

    public void selectAll()
    {
	Dictionary<string, BetterSeries>.ValueCollection.Enumerator checkus = allSeries.Values.GetEnumerator();
	{
	    while (checkus.MoveNext())
	    {
		if (!checkus.Current.getSelected()) toggleSelection(checkus.Current);
	    }
	}
	theBenchChart.pmgraph.updateSelectionButtons(selectionCount);
    }

    public void selectNone()
    {
	Dictionary<string, BetterSeries>.ValueCollection.Enumerator checkus = allSeries.Values.GetEnumerator();
	{
	    while (checkus.MoveNext())
	    {
		if (checkus.Current.getSelected()) toggleSelection(checkus.Current);
	    }
	}
    }

    private void chartMouseClick(object sender, EventArgs e)
    {
	MouseEventArgs mouseargs = (MouseEventArgs)e;
	HitTestResult htr = getHitTestResult(currentChart, Control.MousePosition, chartPoint);
	if (htr.ChartElementType == ChartElementType.LegendItem)
	{
	    if (htr.Object == null) {
		MB.S("PivotChart.chartMouseClick error: Null HTR");
	    } else {
		LegendItem item = htr.Object as LegendItem;

		switch (mouseargs.Button) {
		case (MouseButtons.Middle):
		    allSeries[item.SeriesName].theBenchRound.seriesObject.displaySpikes(); 
		    break;
		case (MouseButtons.Right):
		    break;
		case (MouseButtons.Left):
		    try
		    {
			BetterSeries bs = this.allSeries[item.SeriesName];
			toggleSelection(bs); 
			toggleSelection(partnerSeries[bs.getSeriesName()]);
			refreshSelectionColors(); 
		    }
		    //catch (KeyNotFoundException x)
		    catch (KeyNotFoundException ) {
			MB.S("PivotChart.toggleLegendItemSelected error: key " + item.SeriesName + " not found.");
		    }
		    break;
		}
	    }
	}
    }

    private static void chartZoom(object sender, MouseEventArgs args)
    {
	Chart theChart = sender as Chart;
	try
	{
	    Axis x = theChart.ChartAreas.FindByName("histogram").AxisX;
	    Axis y = theChart.ChartAreas.FindByName("histogram").AxisY;
	    double xMin = x.ScaleView.ViewMinimum, xMax = x.ScaleView.ViewMaximum;
	    double yMin = y.ScaleView.ViewMinimum, yMax = y.ScaleView.ViewMaximum;
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
	catch (Exception x) {
	    MB.S("Zoom error:\n" + x.ToString());
	    return;
	}
    }

    private static void chartMouseLeave(object sender, EventArgs e)
    {
	Chart theChart = sender as Chart;
	if (theChart.Focused) theChart.Parent.Focus();
    }

    public Chart getPivotChart(DetailLevel detail)
    {
	switch (detail) {
	case (DetailLevel.shorttype):
	    return shortChart;
	case (DetailLevel.fulltype):
	    return fullChart;
	case (DetailLevel.currenttype):
	    return currentChart;
	default:
	    return null;
	}
    }

    private static Color[] readColors = { 
	Color.Blue, Color.Cyan, Color.Green, 
	Color.MidnightBlue, Color.DarkCyan, 
	Color.BlueViolet, Color.LimeGreen };
    private static Color[] writeColors = {
	Color.Red, Color.Fuchsia, Color.Orange,
	Color.SaddleBrown, Color.Maroon,
	Color.Chocolate, Color.HotPink};

    private int getRandomColorState(bool read)
    {
	return randomColorState.Next() | (read ? int.Parse("ff0000a0", System.Globalization.NumberStyles.HexNumber) : int.Parse("ffff0000", System.Globalization.NumberStyles.HexNumber));
    }

    private Series collectDataPoints(BenchRound br, DetailLevel detail, AccessType type, int i) 
    {
	Series s = null;
	try {
	    string sname = (type == AccessType.read ? "read" : "write");
	    Chart chart = br.getRoundChart(detail);
	    s = new Series();
	    s.ChartArea = "histogram";
	    s.Legend = "Legend";
	    s.Name = theBenchChart.getPivotDumpHeader(i) + " (" + sname + ")";
	    if (detail == DetailLevel.fulltype) br.registerSeriesName(s.Name, type);
	    DataPointCollection r = chart.Series[sname].Points;                  
	    DataPoint[] dp = new DataPoint[chart.Series[sname].Points.Count];
	    chart.Series[sname].Points.CopyTo(dp, 0);
	    for (int j = 0; j < dp.Length; j++)
	    {
		s.Points.Insert(j, dp[j]);
	    }
	    s.ChartType = SeriesChartType.FastLine;
	    if (i <= 6) s.Color = (type == AccessType.read ? readColors[i] : writeColors[i]);
	    else s.Color = Color.FromArgb(getRandomColorState(type == AccessType.read));
	    s.BorderWidth = 2;
	    BetterSeries bs = allSeries[s.Name];
	    bs.setContainedSeries(s, type);
	}
	catch (KeyNotFoundException) {
	    BetterSeries bs = new BetterSeries();
	    bs.setContainedSeries(s, type);
	    allSeries[s.Name] = bs;
	    bs.theBenchRound = br;
	    string pname = theBenchChart.getPivotDumpHeader(i) + " (" + (type == AccessType.read ? "write" : "read") + ")";
	    partnerSeries.Add(pname, bs);
	}
	catch (ArgumentNullException x) {
	    MB.S("collectDataPoints null argument exception\n" + x.ToString());
	}
	catch (NullReferenceException x) {
	    MB.S("collectDataPoint null reference exception\n" + x.ToString());
	}
	return s;
    }

    public void addCollectedPoints(BenchRound br, AccessType s, int i)
    {
	if (br == null) MB.S("Fcuk");
	if (shortChart == null) MB.S("shjit");
	if (shortChart.Series == null) MB.S("Damn");

	try {
	    shortChart.Series.Add(collectDataPoints(br, DetailLevel.shorttype, s, i));
	    fullChart.Series.Add(collectDataPoints(br, DetailLevel.fulltype, s, i));
	}
	catch (NullReferenceException x) {
	    MB.S(
		"addCollectedPoints exception:\n" + 
		"full chart is " + (fullChart == null ? "INDEED" : "NOT") + " null;\n" +
		"short chart is " + (shortChart == null ? "INDEED" : "NOT") + " null;\n" +
		x.ToString()
	   );
	}
    }
}   // PivotChart



///////////
//
//
//
//
//
///////////

// a benchmark with comparisons to some other stuff
//public class BenchPivot {
public class BenchChart
{
    private PivotChart thePivotChart;
    public int pivotIndex;
    public ParamSet baseParams;
    public List<BenchRound> cronies;
    private bool chartReady;
    public PmGraph pmgraph;
    public StreamWriter outfile;
    public bool dumped = false;
    public bool showFull { set; get; }
    public Point chartPoint;

    public void selectAll()
    {
	thePivotChart.selectAll();
    }

    private static string TextDialog(string prompt, string value)
    {
	Form dialog = new Form();
	dialog.Text = prompt;
	dialog.Height = 128;
	TextBox textbox = new TextBox();
	textbox.Text = value;
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
	if (ret == DialogResult.OK) return value;
	return null;
    }

    /*
     * called when average selected button is clicked
     */
    public BenchSiblings averageSelected(int avgc)
    {
	pmgraph.updateSelectionButtons(0);
	XmlDocument doc = new XmlDocument();
	XmlNode fakeSeries = doc.CreateNode(XmlNodeType.Element, "test_nice", doc.NamespaceURI);
	doc.AppendChild(fakeSeries);
	ParamSet ps = new ParamSet();
	int flagcount = 0;

	try {
	    for (int j = 0; j < cronies.Count; j++)
	    {
		if (cronies[j].flaggedForAverage)
		{
		    XmlDocument tempdoc = cronies[j].roundNode.OwnerDocument;
		    XmlNode fakeRound = doc.CreateNode(XmlNodeType.Element, "test_round", doc.NamespaceURI);
		    XmlAttribute iter = doc.CreateAttribute("iter");
		    iter.Value = (flagcount++ + 1).ToString();
		    fakeRound.Attributes.Append(iter);
		    if (SafeXmlParse.selNode(tempdoc, "test_nice/test_round/pmbenchmark") == null)
		    {
			MB.S("pmbenchmark node not found; root element is " + tempdoc.DocumentElement.Name);
		    }
		    fakeRound.AppendChild(doc.ImportNode(SafeXmlParse.selNode(tempdoc, "test_nice/test_round/pmbenchmark"), true));
		    fakeSeries.AppendChild(fakeRound);
		    ps.setParamsFromNode(PmGraph.getParamsNodeFromSeriesNode(fakeSeries));
		    ps.operatingSystem = SafeXmlParse.selNode(tempdoc, "test_nice/test_round/pmbenchmark/report/signature/pmbench_info/version_options").InnerText;
		}
	    }
	}
	catch (FileNotFoundException x) {
	    MB.S("averageSelected:\n" + x.ToString());
	    return null;
	}
	catch (ArgumentException x) {
	    MB.S("averageSelected: ArgumentException\n" + x.ToString());
	    return null;
	}

	BenchSiblings bs = new BenchSiblings(fakeSeries, doc, ps);
	bs.trialsPerSeries = flagcount;
	string temp = "Average" + avgc++;
	string temp2 = TextDialog("Enter name for new average", temp);

	if (temp2 != null) bs.averageRound.customName = temp2;
	else bs.averageRound.customName = temp;

	thePivotChart.selectNone();
	return bs;
    }

    public int deleteSelected(bool nag)
    {
	bool debug = false;
	int deleted = thePivotChart.finalizeDeletions(thePivotChart.flaggedForDeletion.Count);
	if (deleted > 0)
	{
	    for (int i = 0; i < cronies.Count; i++)
	    {
		if (cronies[i].hasPendingDeletions) cronies[i].deleteSelectedSeries(); 
	    }
	    int j = cronies.Count - 1;
	    while (j >= 0)
	    {
		if (!cronies[j].hasReadSeries && !cronies[j].hasWriteSeries)
		{
		    if (debug) MB.S(cronies[j].customName + " has neither a write nor a read series, deleting it now");
		    string s = cronies[j].customName;
		    cronies.RemoveAt(j);
		    pmgraph.removeDeadXmlDoc(s);
		}
		else if (debug) MB.S(cronies[j].customName + " still has a series"); 
		j--;
	    }
	}

	if (debug) {
	    string cronytest = "";
	    for (int i = 0; i < cronies.Count; i++)
	    {
		cronytest += cronies[i].customName + " has " + (cronies[i].hasReadSeries && cronies[i].hasWriteSeries ? "both" : (cronies[i].hasReadSeries ? "read" : (cronies[i].hasWriteSeries ? "write" : ""))) + "\n";
	    }
	    MB.S("Done deleting, pivot now has " + cronies.Count + " cronies:\n" + cronytest);
	}
	thePivotChart.refreshSelectionColors();
	return deleted;
    }

    public int markDeleteSelected(bool nag)
    {
	return thePivotChart.deleteFlagSelected(nag);
    }

    private bool setFull(bool b)
    {
	showFull = thePivotChart.setFull(b);
	return showFull;
    }

    public bool refreshFull(CheckBox b)
    {
	return (b == null ? setFull(showFull) : setFull(b.Checked));
    }

    public BenchChart(ParamSet ps, int pivotindex, List<BenchRound> br, PmGraph pmg)
    {
	thePivotChart = null;
	chartReady = false;
	baseParams = ps;
	pivotIndex = pivotindex;
	cronies = br;
	pmgraph = pmg;
	chartPoint = new Point(); 
    }

    public string getPivotDumpHeader(int i)
    {
	return CsvWriter.getPivotDumpHeader(i, this);
    }

    /*
     * called by PmGraph.graphManual()
     */
    public Chart getPreparedChart(int w, int h, CheckBox b)
    {
	if (!chartReady) return initializePivotChart(w, h, b); 

	try {
	    Chart pivotChart = thePivotChart.getPivotChart(DetailLevel.currenttype);
	    pivotChart.Width = w;
	    pivotChart.Height = h;
	    return pivotChart;
	}
	catch (NullReferenceException x) {
	    MB.S("getPreparedChart info: null reference exception, the pivot chart is " + ((thePivotChart != null) ? "NOT" : "") + " null.\n" + x.ToString());
	    return null;
	}
    }

    private Chart initializePivotChart(int width, int height, CheckBox b) 
    {
	if (this.cronies == null) {
	    MB.S("initializePivotChart : cronies list is null!");
	    return null;
	}

	for (int i = 0; i < cronies.Count; i++)
	{
	    if (cronies[i] == null) {
		MB.S("initPivotCharts Error: crony " + i + " is null!");
		return null;
	    }
	    cronies[i].getRoundChart(DetailLevel.currenttype);
	}

	thePivotChart = new PivotChart(this, width, height);
	for (int i = 0; i < cronies.Count; i++)
	{
	    if (cronies[i].wasDeletedDontBother) continue;
	    try {
		if (cronies[i].ratio() > 0) thePivotChart.addCollectedPoints(cronies[i], AccessType.read, i);
		if (cronies[i].ratio() < 100) thePivotChart.addCollectedPoints(cronies[i], AccessType.write, i);
	    }
	    catch (NullReferenceException x) {
		MB.S("initPivotChart(" + width + ", " + height +
		"): Null reference exception.\n(thePivotChart == null) == " +
		(thePivotChart == null).ToString() + "\n(cronies[" + i +
		"] == null) == " + (cronies[i] == null).ToString() +
		"\n" + x.ToString());
	    }
	}
	thePivotChart.updateHoverSeries(null);
	setFull(b.Checked);
	chartReady = (thePivotChart.currentChart != null);
	if (!chartReady) {
	    MB.S("initPivotChart(" + width + ", " 
		    + height + ") error: pivot chart is NOT ready.");
	}
	return thePivotChart.currentChart;
    }

    public int dumpPivotCsv(string folder)
    {
	return CsvWriter.writePivotCsvDump(folder, this, ref this.outfile);
    }

    public int getChartSelectionCount()
    {
	return thePivotChart.getSelectionCount();
    }

    public void destroyPivotChart()
    {
	if (thePivotChart == null) return;

	if (chartReady) {
	    if (thePivotChart.currentChart == null) {
		MB.S("destroyPovitChart: Chart already destroyed");
		return;
	    }
	    thePivotChart.currentChart.Dispose();
	    thePivotChart.currentChart = null;
	    chartReady = false;
	} else {
	    MB.S("destroyPivotChart info: Chart is not ready");
	}
    }
}   // BenchChart


} // namespace PmGraphSpace
