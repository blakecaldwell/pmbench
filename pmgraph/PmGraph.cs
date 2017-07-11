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
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml;
//using Microsoft.VisualBasic;

namespace PmGraphSpace
{
    public partial class BenchRound
    {
        //private XmlHierarchy hierarchy;
        public XmlNode roundNode; //XML node containing the trial in question, should be of type test_round
        public BenchSiblings seriesObject; //series (with identical params) this round belongs to
        public BenchPivot.PivotChart myPivotChart; //chart with histograms for transcription
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
        private string readSeriesName, writeSeriesName;
        public bool wasDeletedDontBother;

        public bool setDeletionFlag(BenchPivot.AccessType type)
        {
            switch (type)
            {
                case (BenchPivot.AccessType.read):
                    if (hasReadSeries) return hasPendingDeletions = readDeleteFlag = true;
                    break;
                case (BenchPivot.AccessType.write):
                    if (hasWriteSeries) return hasPendingDeletions = writeDeleteFlag = true;
                    break;
                default:
                    MessageBox.Show("BenchRound.setDeletionFlag error");
                    break;
            }
            return false;
        }

        public bool unsetDeletionFlag(BenchPivot.AccessType type)
        {
            if (hasPendingDeletions)
            {
                switch (type)
                {
                    case (BenchPivot.AccessType.read):
                        if (hasReadSeries) readDeleteFlag = false;
                        break;
                    case (BenchPivot.AccessType.write):
                        if (hasReadSeries) writeDeleteFlag = false;
                        break;
                    default:
                        MessageBox.Show("BenchRound.unsetDeletionFlag access type error");
                        return true;
                }
                return (hasPendingDeletions = (readDeleteFlag | writeDeleteFlag));
            }
            MessageBox.Show("BenchRound.unsetDeletionFlag pending deletion flag error");
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

        private static XmlNode safeSelectSingleNode(XmlNode n, string s) { return PmGraph.safeSelectSingleNode(n, s); }
        private static int safeParseSingleNodeInt(XmlNode n, string s) { return PmGraph.safeParseSingleNodeInt(n, s); }
        private static long safeParseSingleNodeLong(XmlNode n, string s) { return PmGraph.safeParseSingleNodeLong(n, s); }
        private static double safeParseSingleNodeDouble(XmlNode n, string s) { return PmGraph.safeParseSingleNodeDouble(n, s); }
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
        private string[] memstrings_w = { "AvailPhys", "dwMemoryLoad", "TotalPageFile", "AvailPageFile", "AvailVirtual" };
        public bool windowsbench() { return seriesObject.windowsbench; }

        /*private bool checkNegativeDeltas() //fix a windows-only problem caused by bad programming
        {
            XmlNode a, b, delta;
            XmlNodeList meminfos = roundNode.SelectNodes("pmbenchmark/report/sys_mem_info/sys_mem_item");
            for (int i = 0; i < meminfos.Count - 1; i++)
            {
                a = safeSelectSingleNode(meminfos[i], "mem_item_info");
                b = safeSelectSingleNode(meminfos[i + 1], "mem_item_info");
                delta = safeSelectSingleNode(meminfos[i], "mem_item_delta");
                for (int j = 0; j < memstrings_w.Length; j++)
                {
                    if (safeParseSingleNodeLong(a, memstrings_w[j]) > safeParseSingleNodeLong(b, memstrings_w[j]))
                    {
                        dirtyDelta = true;
                        safeSelectSingleNode(delta, memstrings_w[j]).InnerText = (safeParseSingleNodeLong(b, memstrings_w[j]) - safeParseSingleNodeLong(a, memstrings_w[j])).ToString();
                    }
                }
            }
            return dirtyDelta;
        }*/

        public Chart getRoundChart(BenchPivot.DetailLevel detail)
        {
            if (myPivotChart == null) myPivotChart = new BenchPivot.PivotChart(roundNode);               
            return (myPivotChart.getPivotChart(detail));
        }
        
        public void registerSeriesName(string s, BenchPivot.AccessType t)
        {
            if (s == null) return;
            switch (t)
            {
                case (BenchPivot.AccessType.read):
                    if (!hasReadSeries)
                    {
                        readSeriesName = s;
                        hasReadSeries = true;
                    }
                    break;
                case (BenchPivot.AccessType.write):
                    if (!hasWriteSeries)
                    {
                        writeSeriesName = s;
                        hasWriteSeries = true;
                    }
                    break;
                default:
                    MessageBox.Show("BenchRound.registerSeriesName error");
                    return;
            }
        }

        private double totalSamples = -1;
        public double getTotalSamples()
        {
            if (totalSamples < 0)
            {
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
            Chart shortChart = myPivotChart.getPivotChart(BenchPivot.DetailLevel.shorttype);
            Chart fullChart = myPivotChart.getPivotChart(BenchPivot.DetailLevel.fulltype);
            if (readDeleteFlag)
            {
                if (hasReadSeries)
                {
                    shortChart.Series.Remove(shortChart.Series.FindByName("read"));
                    fullChart.Series.Remove(fullChart.Series.FindByName("read"));
                    hasReadSeries = false;
                    deleted += 1;
                }
            }
            if (writeDeleteFlag)
            {
                if (hasWriteSeries)
                {
                    shortChart.Series.Remove(shortChart.Series.FindByName("write"));
                    fullChart.Series.Remove(fullChart.Series.FindByName("write"));
                    hasWriteSeries = false;
                    deleted += 1;
                }
            }
            if (deleted > 0)
            {
                hasPendingDeletions = false;
                wasDeletedDontBother = true;
                return deleted;
            }
            else
            {
                MessageBox.Show("BenchRound.deleteSelected error");
                return 0;
            }
        }
    }

    public partial class BenchSiblings //a series of benchmarks with common parameters
    {
        public XmlNode seriesNode, averageNode; //XML nodes for the series root, and the node containing series averages
        public List<BenchRound> Trials; //rounds from this series
        public XmlDocument theDoc; //hosting XML document
        public ParamSet benchParams; //this series' params
        public BenchRound averageRound; //the BenchRound object, not to be confused with its XML node
        private delegate void addMemItemsDelegate(XmlNode item_avg, XmlNode item_, int i, int trials); //I just wanted to try these out
        public bool windowsbench = false; //temporary fix for some windows benchmarks
        public int trialsPerSeries = -1; //future plans
        public double samplesPerThread, netAverageLatency, readSpike, writeSpike, readSpikeHexVal, writeSpikeHexVal, readSamplesTotal, writeSamplesTotal; //average samples per thread, net average acces latency, highest latency count in read and write histograms, and total samples
        public int readSpikeIntervalHi = -1, writeSpikeIntervalHi = -1, readSpikeHexBinNum = -1, writeSpikeHexBinNum = -1; //for locating the read and write spikes
        public bool spikesCalculated = false;

        public BenchRound getAverageRound()
        {
            if (averageRound == null)
            {
                if (getAverageNode() == null) { MessageBox.Show("(BenchRound.getAverageRound) Error: still unable to create average node"); return null; }
                averageRound = new BenchRound(this, getAverageNode(), trialsPerSeries + 1);
                calculateSpikes();
                this.Trials.Add(averageRound);
            }
            return (averageRound);
        }

        private void calculateSpikes()
        {
            if (this.spikesCalculated)
            {
                MessageBox.Show("calculateSpikes error: Spikes already calculated!");
            }
            XmlNodeList readbuckets = averageNode.SelectNodes("pmbenchmark/report/statistics/histogram[@type='read']/histo_bucket");
            XmlNodeList writebuckets = averageNode.SelectNodes("pmbenchmark/report/statistics/histogram[@type='write']/histo_bucket");
            readSpike = 0;
            writeSpike = 0;
            readSpikeHexVal = 0;
            writeSpikeHexVal = 0;
            for (int i = 0; i < readbuckets.Count; i++)
            {
                double tempSpike = safeParseSingleNodeDouble(readbuckets[i], "sum_count");
                if (tempSpike > readSpike)
                {
                    readSpike = tempSpike;
                    readSpikeIntervalHi = safeParseSingleNodeInt(readbuckets[i], "bucket_interval/interval_hi");
                }
                if (readbuckets[i].Attributes.Item(0).Name.Equals("index") && !readbuckets[i].Attributes.Item(0).Value.Equals("0"))
                {
                    for (int j = 0; j < 16; j++)
                    {
                        double hexbin = safeParseSingleNodeDouble(readbuckets[i], "bucket_hexes/hex[@index='" + j + "']");
                        if (hexbin > readSpikeHexVal)
                        {
                            readSpikeHexVal = hexbin;
                            readSpikeHexBinNum = j;
                        }
                    }
                }

                tempSpike = safeParseSingleNodeDouble(writebuckets[i], "sum_count");
                if (tempSpike > writeSpike)
                {
                    writeSpike = tempSpike;
                    writeSpikeIntervalHi = safeParseSingleNodeInt(writebuckets[i], "bucket_interval/interval_hi");
                }
                if (writebuckets[i].Attributes.Item(0).Name.Equals("index") && !writebuckets[i].Attributes.Item(0).Value.Equals("0"))
                {
                    for (int j = 0; j < 16; j++)
                    {
                        double hexbin = safeParseSingleNodeDouble(writebuckets[i], "bucket_hexes/hex[@index='" + j + "']");
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
            MessageBox.Show("Net average latency:\t" + netAverageLatency + "\nAverage samples per thread:\t" + samplesPerThread + "\nRead spike:\t" + readSpike + " at bucket 2^" + readSpikeIntervalHi + "; " + readSpikeHexVal + " at bin " + readSpikeHexBinNum + "\nWrite spike:\t" + writeSpike + " at bucket 2^" + writeSpikeIntervalHi + "; " + writeSpikeHexVal + " at bin " + writeSpikeHexBinNum);
        }

        public BenchSiblings()
        {
            this.seriesNode = null;
            this.Trials = null;
            this.theDoc = null;
            this.benchParams = null;
            this.averageNode = null;
            this.averageRound = null;
        }

        public BenchSiblings(XmlNode node, XmlDocument doc, ParamSet bp)
        {
            seriesNode = node;
            theDoc = doc;
            benchParams = bp;
            try
            {
                if (benchParams == null) { throw new NullReferenceException("Null benchparams, assuming this is a windows benchmark you're importing"); }
                else if (benchParams.operatingSystem == null) { windowsbench = true; MessageBox.Show("Benchparams are non-null, but null OS name, assuming this is a windows benchmark you're importing"); }
                else windowsbench = benchParams.operatingSystem.Contains("Windows");
            }
            catch (NullReferenceException x)
            {
                //go ahead and assume it is
                MessageBox.Show(x.ToString());
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
            if (getAverageRound() == null) { MessageBox.Show("(BenchSiblings(XmlNode, XmlDocument, ParamSet) Error: Unable to generate average round"); }
        }

        private static XmlNode safeSelectSingleNode(XmlNode n, string s) { return PmGraph.safeSelectSingleNode(n, s); }
        private static int safeParseSingleNodeInt(XmlNode n, string s) { return PmGraph.safeParseSingleNodeInt(n, s); }
        private static double safeParseSingleNodeDouble(XmlNode n, string s) { return PmGraph.safeParseSingleNodeDouble(n, s); }

        public XmlNode getAverageNode() //get the node of the trial containing averages
        {
            if (averageNode == null) { averageNode = makeAverageNode(); }
            return averageNode;
        }

        private XmlNode partialCloneBenchmark(int trials) //clone the first benchmark's result, statistics and sys_mem_info and insert the partial clone as last child of parent
        {
            XmlNode avg = null, parent = this.seriesNode;
            XmlDocument doc = this.theDoc;
            if (doc == null) doc = parent.OwnerDocument;
            XmlNode result1, statistics, sys_mem_info, report_temp, pmb_temp;
            string report_s = "test_round[@iter='1']/pmbenchmark/report/";
            result1 = safeSelectSingleNode(parent, report_s + "result").Clone();
            statistics = safeSelectSingleNode(parent, report_s + "statistics").Clone();
            sys_mem_info = safeSelectSingleNode(parent, report_s + "sys_mem_info").Clone();
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

        private static void addMemItemField(XmlNode item_avg, XmlNode item_, int i, string s, int trials) //works for some elements in result as well
        {
            try
            {
                double t = safeParseSingleNodeDouble(item_avg, s);
                t += safeParseSingleNodeDouble(item_, s);
                if (i == trials) { t /= (float)trials; }
                safeSelectSingleNode(item_avg, s).InnerText = t.ToString();
            }
            catch (NullReferenceException x) { MessageBox.Show("(addMemItemField) Adding field " + s + " for round " + i + ":\n" + x.ToString()); }
        }

        private static void addThreadResults(XmlNode thread_avg, XmlNode thread_, int i, int trials) //add thread 2's result data to thread 1's, then divide by 5 if i == 5
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

        public static bool addHistograms(XmlNode histo_avg, XmlNode histo, int round, int trials) //add the second histogram to the first, then divide if it's round 5
        {
            XmlNode bucket, bucket_avg;
            double sum_temp, term_temp;
            for (int j = 1; j <= 15; j++)
            {
                //get node 6's bucket
                try { bucket_avg = safeSelectSingleNode(histo_avg, "histo_bucket[@index='" + j + "']"); }
                catch (Exception x)
                {
                    MessageBox.Show("Error: selecting bucket " + j + " in histo_avg:\n" + x.ToString());
                    return false;
                }

                //get test node's bucket
                try { bucket = safeSelectSingleNode(histo, "histo_bucket[@index='" + j + "']"); }
                catch (Exception x) { MessageBox.Show("Error: selecting bucket " + j + " in histo:\n" + x.ToString()); return false; }

                //get sum_count from node 6's bucket
                try { sum_temp = safeParseSingleNodeDouble(bucket_avg, "sum_count"); }
                catch (Exception x) { MessageBox.Show("Error: retrieving/parsing sum_temp of bucket_avg " + j + ":\n" + x.ToString()); return false; }

                //add sum_count from test node's bucket
                try { term_temp = safeParseSingleNodeDouble(bucket, "sum_count"); }
                catch (Exception x) { MessageBox.Show("Error: parsing/adding sum_temp of bucket " + j + ":\n" + x.ToString()); return false; }
                sum_temp += term_temp;

                if (round == trials) // this.trialsPerSeries)
                {
                    sum_temp /= trials; // PerSeries;
                }

                //update node 6's bucket
                try { safeSelectSingleNode(bucket_avg, "sum_count").InnerText = sum_temp.ToString(); }
                catch (Exception x) { MessageBox.Show("Error updating sum for bucket " + j + " in histo_avg:\n" + x.ToString()); return false; }

                for (int k = 0; k < 16; k++)
                {
                    try
                    {
                        addMemItemField(bucket_avg, bucket, round, "bucket_hexes/hex[@index='" + k + "']", trials);
                    }
                    catch (Exception x) { MessageBox.Show("Error adding hex " + k + " in bucklet " + j + ":\n" + x.ToString()); return false; }
                }
            }
            //fix for a poor choice I made in writing the XML
            XmlNodeList bucket0, bucket0_avg;
            try
            {
                bucket0_avg = histo_avg.SelectNodes("bucket_hexes/hex[@index='0']");
                bucket0 = histo.SelectNodes("bucket_hexes/hex[@index='0']");
            }
            catch (System.Xml.XPath.XPathException x) { MessageBox.Show("Error getting nodes for bucket0:\n" + x.ToString()); return false; }
            if (bucket0.Count != bucket0_avg.Count)
            {
                MessageBox.Show("Error: somehow, current node and average have a different number of bucket0's"); return false;
            }
            for (int j = 0; j < bucket0.Count; j++)
            {
                try
                {
                    bucket_avg = bucket0_avg.Item(j); //why can't you access these like an array?
                    bucket = bucket0.Item(j);
                    addMemItemField(bucket_avg, bucket, round, "sum_count", trials);
                }
                catch (Exception x) { MessageBox.Show("Error updating sum count for " + j + "th bucket 0:\n" + x.ToString()); return false; }
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
            catch (NullReferenceException x) { MessageBox.Show("(addMemItemsLinux) Null reference " + x.ToString()); }
        }
        private static void addMemItemsWindows(XmlNode item_avg, XmlNode item_, int i, int trials)
        {
            addMemItemField(item_avg, item_, i, "AvailPhys", trials);
            addMemItemField(item_avg, item_, i, "dwMemoryLoad", trials);
            addMemItemField(item_avg, item_, i, "TotalPageFile", trials);
            addMemItemField(item_avg, item_, i, "AvailPageFile", trials);
            addMemItemField(item_avg, item_, i, "AvailVirtual", trials);
        }

        private XmlNode makeAverageNode() //do something with the average of some benchmarks
        {
            if (this.trialsPerSeries == 1) { return this.Trials[0].roundNode; }
            if (averageNode != null) { MessageBox.Show("(BenchSiblings.makeAverage) Warning: Attempted to insert average node for a series that already has one"); return averageNode; }
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
            if (windowsbench) { addMemItems = addMemItemsWindows; } else { addMemItems = addMemItemsLinux; }
            string report_s = "pmbenchmark/report";
            try
            {
                string params_s = "test_round/" + report_s + "/signature/params/";
                ratio = safeParseSingleNodeInt(seriesNode, params_s + "ratio");
                jobs = safeParseSingleNodeInt(seriesNode, params_s + "jobs");
                cold = safeParseSingleNodeInt(seriesNode, params_s + "cold");
            }
            catch (Exception x) { MessageBox.Show("(BenchSiblings.makeAverageNode) Error: unable to retrieve ratio parameter:\n" + x.ToString()); return null; }
            XmlNode avg = partialCloneBenchmark(trialsPerSeries);
            seriesNode.AppendChild(avg);

            string result_s = report_s + "/result";
            string meminfo_s = report_s + "/sys_mem_info/sys_mem_item";
            result_avg = safeSelectSingleNode(avg, result_s);
            sys_mem_items_avg = avg.SelectNodes(meminfo_s);
            string histo_s = report_s + "/statistics/histogram";
            string round_s;
            for (int i = 2; i < this.trialsPerSeries + 1; i++)
            {
                round_s = "test_round[@iter = '" + i + "']";
                //average the individual thread results
                result_ = safeSelectSingleNode(seriesNode, round_s + "/" + result_s);
                for (int j = 1; j <= jobs; j++)
                {
                    thread_avg = safeSelectSingleNode(result_avg, "result_thread[@thread_num='" + j + "']");
                    thread_ = safeSelectSingleNode(result_, "result_thread[@thread_num='" + j + "']");
                    addThreadResults(thread_avg, thread_, i, trialsPerSeries);
                }
                //average the histograms
                if (ratio > 0) //deal with read histogram here
                {
                    histo_avg = safeSelectSingleNode(avg, histo_s + "[@type='read']");
                    histo = safeSelectSingleNode(seriesNode, round_s + "/" + histo_s + "[@type='read']");
                    if (!addHistograms(histo_avg, histo, i, trialsPerSeries)) { MessageBox.Show("(BenchSiblings.makeAverageNode) Error adding read histograms"); return null; }
                }
                if (ratio < 100)
                {
                    histo_avg = safeSelectSingleNode(avg, histo_s + "[@type='write']");
                    if (histo_avg == null)
                    {
                        MessageBox.Show("(BenchSiblings.makeAverageNode) This series of benches (at " + avg.Name + ") have no write histograms at " + histo_s + "[@type='write']" + ", apparently ");
                        return null;
                    }
                    histo = safeSelectSingleNode(seriesNode, round_s + "/" + histo_s + "[@type='write']");
                    if (!addHistograms(histo_avg, histo, i, trialsPerSeries)) { MessageBox.Show("(BenchSiblings.makeAverageNode) Error adding write histograms"); return null; }
                }

                //sys_mem_items
                sys_mem_items_ = seriesNode.SelectNodes(round_s + "/" + meminfo_s);
                for (int j = 0; j < sys_mem_items_avg.Count; j++)
                {
                    item_avg = safeSelectSingleNode(sys_mem_items_avg.Item(j), "mem_item_info");
                    item_ = safeSelectSingleNode(sys_mem_items_.Item(j), "mem_item_info");
                    addMemItems(item_avg, item_, i, trialsPerSeries);
                    if (j != sys_mem_items_avg.Count - 1)
                    {
                        item_avg = safeSelectSingleNode(sys_mem_items_avg.Item(j), "mem_item_delta");
                        item_ = safeSelectSingleNode(sys_mem_items_.Item(j), "mem_item_delta");
                        addMemItems(item_avg, item_, i, trialsPerSeries);
                    }
                }
            }
            return avg;
        }

    }

    public partial class ParamSet
    {
        public int indexKernel, indexDevice, indexMemory, indexMapsize, indexJobs, indexDelay, indexRatio, indexNice, valueMemory, valueMapsize, valueJobs, valueDelay, valueRatio, valueNice;
        public string operatingSystem, swapDevice;
        public string paramsKey1, paramsKey2;
        public int duration, setsize, quiet, cold, offset;
        public string shape, pattern, access, tsops;
        private static int[] physMemValues = { 256, 512, 1024, 2048, 4096, 8192, 16384 };
        private static int[] mapSizeValues = { 512, 1024, 2048, 4096, 8192, 16384, 32768 };
        private static int[] jobsValues = { 1, 8 };
        private static int[] delayValues = { 0, 1000 };
        private static int[] ratioValues = { 0, 50, 100 };
        private static int[] niceValues = { 19, -20, 0 };

        public ParamSet()
        {
            this.operatingSystem = null;
            this.swapDevice = null;
            this.paramsKey1 = null;
            this.paramsKey2 = null;
            this.shape = null;
            this.pattern = null;
            this.access = null;
            this.tsops = null;
        }

        private static XmlNode safeSelectSingleNode(XmlNode n, string s) { return PmGraph.safeSelectSingleNode(n, s); }
        private static int safeParseSingleNodeInt(XmlNode n, string s) { return PmGraph.safeParseSingleNodeInt(n, s); }
        private static double safeParseSingleNodeDouble(XmlNode n, string s) { return PmGraph.safeParseSingleNodeDouble(n, s); }

        public string printReadableParams()
        {
            return (operatingSystem + " " + swapDevice + " " + valueMemory + " MiB, with parameters -m " + valueMapsize + " -j " + valueJobs + " -d " + valueDelay + " -r " + valueRatio + " -n " + valueNice);
        }

        public void setParamsFromNode(XmlNode p) //This is ALSO not a constructor
        {
            if (p == null) { MessageBox.Show("ParamSet.setParamsFromNode: Error: received null input node"); return; }
            this.duration = safeParseSingleNodeInt(p, "duration");
            this.valueMapsize = safeParseSingleNodeInt(p, "mapsize");
            this.setsize = safeParseSingleNodeInt(p, "setsize");
            this.valueJobs = safeParseSingleNodeInt(p, "jobs");
            this.valueDelay = safeParseSingleNodeInt(p, "delay");
            this.valueRatio = safeParseSingleNodeInt(p, "ratio");
            this.shape = safeSelectSingleNode(p, "shape").InnerText;
            this.quiet = safeParseSingleNodeInt(p, "quiet");
            this.cold = safeParseSingleNodeInt(p, "cold");
            this.offset = safeParseSingleNodeInt(p, "offset");
            this.pattern = safeSelectSingleNode(p, "pattern").InnerText;
            this.access = safeSelectSingleNode(p, "access").InnerText;
            this.tsops = safeSelectSingleNode(p, "tsops").InnerText;
        }

        public string getBothKeys() { return paramsKey1 + "_" + paramsKey2; }

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
            catch (Exception x) { MessageBox.Show("ParamSet.setKey2ValuesFromKey:\n" + x.ToString()); }
        }

        public static ParamSet makeParamsFromKeysAndNode(string key1, string key2, XmlNode p)
        {
            if (p == null) { return null; }
            ParamSet bp = new ParamSet();
            bp.setKey1IndicesFromKey(key1);
            bp.setKey2ValuesFromKey(key2);
            bp.setParamsFromNode(p);
            return bp;
        }

        public string getXPath() //get XPath query string for the series node (should be named test_nice) with these parameters, relative to (any) XML document root. For replacing bad test data.
        {
            return "benchmark_set/test_content/test_mapsize[@iter='" + valueMapsize + "']/test_jobs[@iter='" + valueJobs + "']/test_delay[@iter='" + valueDelay + "']/test_ratio[@iter='" + valueRatio + "']/test_nice[@iter='" + valueNice + "']";
        }
    }

    public partial class BenchPivot //a benchmark with comparisons to some other stuff
    {
        public partial class PivotChart
        {
            public partial class BetterSeries
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
                    if (!selected && sel > 0)
                    {
                        if (!grayed)
                        {
                            saveBackupColor(shortSeries.Color);
                            setColor(unselectedColor);
                            grayed = true;
                        }
                    }
                    else
                    {
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
                    switch (series.Points.Count)
                    {
                        case (250):
                            if (this.fullSeries == null) fullSeries = series;
                            else MessageBox.Show("BenchPivot.PivotChart.BetterSeries.setContainedSeries error: full series " + series.Name + " is already set");
                            break;
                        case (25):
                            if (this.shortSeries == null) shortSeries = series;
                            else MessageBox.Show("BenchPivot.PivotChart.BetterSeries.setContainedSeries error: short series is already set");
                            break;
                        default:
                            MessageBox.Show("BenchPivot.PivotChart.BetterSeries.setContainedSeries error: Found a series with " + series.Points.Count + " points, something went wrong");
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

            private partial class HoverSeries : BetterSeries
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
                    chartPoint = pivotchart.theBenchPivot.chartPoint;
                }
            }

            private Chart shortChart, fullChart;
            public Chart currentChart;
            private Point chartPoint;
            private HoverSeries hoverSeries;
            private BenchPivot theBenchPivot;
            private bool showFull { set; get; }
            private Random randomColorState;
            private Dictionary<string, BetterSeries> allSeries;
            private int selectionCount;
            public List<string> flaggedForDeletion;
            private Dictionary<string, BetterSeries> partnerSeries;

            public string getBetterSeriesName(BenchRound round, AccessType type)
            {
                string append = " (" + (type == AccessType.read ? "read" : "write") + ")";
                try
                {
                    return allSeries[round.customName + append].getSeriesName();
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            public int getSelectionCount() { return selectionCount; }

            public int deleteFlagSelected(bool nag) //this is complicated and cumbersome in part because I intended to add an undelete option
            {
                Dictionary<string, BetterSeries>.ValueCollection.Enumerator checkus = allSeries.Values.GetEnumerator();
                string s = null;
                List<string> deleteus = new List<string>();
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

            private static void writeHexBinsToChart(XmlNode bucket, double interval_lo, double interval_hi, Chart c, AccessType type)
            {
                //get the midpoint between interval values
                string sname = (type == AccessType.read ? "read" : "write");
                double interval_ = (interval_hi - interval_lo) / 16;
                for (int j = 0; j < 16; j++) //graph it (involves retrieving bucket sub hex index), skipping nodes with no samples
                {
                    double hex = safeParseSingleNodeDouble(bucket, "bucket_hexes/hex[@index='" + j + "']");
                    //if (hex == 0) { continue; }
                    double xval = interval_lo + (0.5 + j) * interval_;
                    c.Series[sname].Points.AddXY(xval, hex);
                }
            }

            private static void writeSumCountOnlyToChart(XmlNode bucket, Chart c, AccessType type)
            {
                string sname = (type == AccessType.read ? "read" : "write");
                double sum_count = safeParseSingleNodeDouble(bucket, "sum_count");
                //if (sum_count == 0) { return; }
                double interval_lo = (double)safeParseSingleNodeInt(bucket, "bucket_interval/interval_lo");
                double interval_hi = (double)safeParseSingleNodeInt(bucket, "bucket_interval/interval_hi");
                double interval_ = (interval_hi - interval_lo);
                double xval = interval_lo + (interval_ / 2);
                c.Series[sname].Points.AddXY(xval, sum_count);
            }

            private static void writeHistogramToChart(Chart chart, XmlNode stats, AccessType type, Color color, bool full) //get the chart ready. Important for the Series that is produced.
            {
                string sname = (type == AccessType.read ? "read" : "write");
                XmlNode histogram = safeSelectSingleNode(stats, "histogram[@type='" + sname + "']");
                if (histogram != null)
                {
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
                    sum_count = safeParseSingleNodeDouble(bucket, "sum_count");
                    chart.Series[sname].Points.AddXY(8, sum_count);
                    chart.Series[sname].Points.AddXY(8, sum_count);

                    for (int i = 1; i < 16; i++) //intentionally miscalculates x coordinate because (2^lo+(j+0.5)*((2^hi-2^lo)/16)) stretches the x axis
                    {
                        bucket = safeSelectSingleNode(histogram, "histo_bucket[@index='" + i + "']");
                        if (full)
                        {
                            interval_lo = (double)safeParseSingleNodeInt(bucket, "bucket_interval/interval_lo");
                            interval_hi = (double)safeParseSingleNodeInt(bucket, "bucket_interval/interval_hi");
                            writeHexBinsToChart(bucket, interval_lo, interval_hi, chart, type);
                        }
                        else { writeSumCountOnlyToChart(bucket, chart, type); }
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

            public PivotChart()
            {
                partnerSeries = new Dictionary<string, BetterSeries>();
                this.allSeries = new Dictionary<string, BetterSeries>();
                hoverSeries = null;
                randomColorState = new Random(int.Parse("0ddfaced", System.Globalization.NumberStyles.HexNumber));
                flaggedForDeletion = new List<string>();
                selectionCount = 0;
            }

            public PivotChart(XmlNode node) //this constructor is used for storage only, these charts are never displayed
            {
                partnerSeries = new Dictionary<string, BetterSeries>();
                this.allSeries = new Dictionary<string, BetterSeries>();
                hoverSeries = null;
                randomColorState = new Random(int.Parse("0ddfaced", System.Globalization.NumberStyles.HexNumber));
                flaggedForDeletion = new List<string>();
                selectionCount = 0;

                for (int i = 0; i < 2; i++)
                {
                    Chart c = new Chart();
                    ChartArea sumCount = new ChartArea();
                    sumCount.Name = "sum_count"; //current chart measures individual hex buckets, not sum_count, but I don't feel like changing it
                    sumCount.AxisX.ScaleView.Zoomable = true;
                    sumCount.AxisY.ScaleView.Zoomable = true;
                    sumCount.AxisY.Title = "Sample count";
                    sumCount.AxisX.Title = "Latency interval (2^x ns)";
                    Legend legend1 = new Legend();
                    legend1.Name = "Legend1";
                    c.ChartAreas.Add(sumCount);
                    c.Name = "Statistics";
                    c.Legends.Add(legend1);
                    c.TabIndex = 1;
                    c.Text = (i == 1 ? "hex_bins" : "sum_count");
                    XmlNode stats = safeSelectSingleNode(node, "pmbenchmark/report/statistics");
                    writeHistogramToChart(c, stats, AccessType.read, Color.Blue, i == 1);
                    writeHistogramToChart(c, stats, AccessType.write, Color.Red, i == 1);
                    stats = null;
                    switch (i)
                    {
                        case (0):
                            shortChart = c;
                            break;
                        case (1):
                            fullChart = c;
                            break;
                        default:
                            MessageBox.Show("new pivot chart from xmlnode error: " + i);
                            break;
                    }
                }
        }

            public PivotChart(BenchPivot bp, int width, int height) //this constructor is used to produced the displayed chart 
            {
                partnerSeries = new Dictionary<string, BetterSeries>();
                this.allSeries = new Dictionary<string, BetterSeries>();
                hoverSeries = null;
                randomColorState = new Random(int.Parse("0ddfaced", System.Globalization.NumberStyles.HexNumber));
                flaggedForDeletion = new List<string>();
                selectionCount = 0;

                this.theBenchPivot = bp;
                chartPoint = theBenchPivot.chartPoint;

                for (int i = 0; i < 2; i++)
                {
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
                    switch (i)
                    {
                        case (0):
                            shortChart = chart;
                            break;
                        case (1):
                            fullChart = chart;
                            break;
                        default:
                            MessageBox.Show("New pivot chart error: " + i);
                            break;
                    }
                }
                if (shortChart == null || fullChart == null) MessageBox.Show("Error, null chart");
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
                    MessageBox.Show("PivotChart.setFull(" + b.ToString() + ")null reference exception"+x.ToString());
                }
                return showFull;
            }

            public void updateHoverSeries(Series s)
            {
                if (hoverSeries != null)
                {
                    hoverSeries.setSeriesEnabled(false);
                    if (s != null)
                    {
                        hoverSeries.setColor(s.Color);
                        try
                        {
                            currentChart.Series[hoverSeries.getSeriesName()].Points.Clear();
                            currentChart.DataManipulator.CopySeriesValues(s.Name, hoverSeries.getSeriesName());
                            hoverSeries.setSeriesEnabled(true);
                        }
                        catch (ArgumentException x)
                        {
                            MessageBox.Show("Argument exception. Series name is " + s.Name + ".\n" + x.ToString());
                        }
                    }
                }
                else
                {
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
                if (htr.ChartElementType == ChartElementType.LegendItem)
                {
                    if (htr.Object == null) { MessageBox.Show("PivotChart.chartMouseHover error: Null HTR"); }
                    else updateHoverSeries(currentChart.Series[(htr.Object as LegendItem).SeriesName]);
                }
                else updateHoverSeries(null);
            }

            private void toggleSelection(BetterSeries bs)
            {
                selectionCount += bs.toggleSelected(selectionCount);
            }

            public void refreshSelectionColors()
            {
                if (selectionCount < 0)  MessageBox.Show("refreshSelectionColors error: negative selction count"); 
                else
                {
                    Dictionary<string, BetterSeries>.ValueCollection.Enumerator checkus = allSeries.Values.GetEnumerator();
                    while (checkus.MoveNext())
                    {
                        checkus.Current.updateSelectionColor(selectionCount);
                    }
                    if (true) theBenchPivot.parserForBenchPivot.updateSelectionButtons(selectionCount);
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
                theBenchPivot.parserForBenchPivot.updateSelectionButtons(selectionCount);
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
                    if (htr.Object == null) { MessageBox.Show("PivotChart.chartMouseClick error: Null HTR"); }
                    else
                    {
                        LegendItem item = htr.Object as LegendItem;
                        switch (mouseargs.Button)
                        {
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
                                catch (KeyNotFoundException )
                                {
                                    MessageBox.Show("BenchPivot.PivotChart.toggleLegendItemSelected error: key " + item.SeriesName + " not found.");
                                }
                                break;
                        }
                    }
                }
                else { }
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
                    if (args.Delta < 0)
                    {
                        x1 = x.PixelPositionToValue(args.Location.X) - (xMax - xMin) * 2;
                        x2 = x.PixelPositionToValue(args.Location.X) + (xMax - xMin) * 2;
                        y1 = y.PixelPositionToValue(args.Location.Y) - (yMax - yMin) * 2;
                        y2 = y.PixelPositionToValue(args.Location.Y) + (yMax - yMin) * 2;

                    }
                    if (args.Delta > 0)
                    {
                        x1 = x.PixelPositionToValue(args.Location.X) - (xMax - xMin) / 2;
                        x2 = x.PixelPositionToValue(args.Location.X) + (xMax - xMin) / 2;
                        y1 = y.PixelPositionToValue(args.Location.Y) - (yMax - yMin) / 2;
                        y2 = y.PixelPositionToValue(args.Location.Y) + (yMax - yMin) / 2;
                    }
                    x.ScaleView.Zoom(x1, x2);
                    y.ScaleView.Zoom(y1, y2);
                }
                catch (Exception x) { MessageBox.Show("Zoom error:\n" + x.ToString()); return; }
            }

            private static void chartMouseLeave(object sender, EventArgs e)
            {
                Chart theChart = sender as Chart;
                if (theChart.Focused) theChart.Parent.Focus();
            }

            public Chart getPivotChart(DetailLevel detail)
            {
                switch (detail)
                {
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

            private int getRandomColorState(bool read)
            {
                return randomColorState.Next() | (read ? int.Parse("ff0000a0", System.Globalization.NumberStyles.HexNumber) : int.Parse("ffff0000", System.Globalization.NumberStyles.HexNumber));
            }
            private void show(string s) { MessageBox.Show(s); }
            private Series collectDataPoints(BenchRound br, DetailLevel detail, AccessType type, int i) 
            {
                Series s = null;
                try
                {
                    string sname = (type == AccessType.read ? "read" : "write");
                    Chart chart = br.getRoundChart(detail);
                    s = new Series();
                    s.ChartArea = "histogram";
                    s.Legend = "Legend";
                    s.Name = theBenchPivot.getPivotDumpHeader(i) + " (" + sname + ")";
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
                //catch (KeyNotFoundException x)
                catch (KeyNotFoundException )
                {
                    BetterSeries bs = new BetterSeries();
                    bs.setContainedSeries(s, type);
                    allSeries[s.Name] = bs;
                    bs.theBenchRound = br;
                    string pname = theBenchPivot.getPivotDumpHeader(i) + " (" + (type == AccessType.read ? "write" : "read") + ")";
                    partnerSeries.Add(pname, bs);
                }
                catch (ArgumentNullException x)
                {
                    MessageBox.Show("collectDataPoints null argument exception\n" + x.ToString());
                }
                catch (NullReferenceException x)
                {
                    MessageBox.Show("collectDataPoint null reference exception\n" + x.ToString());
                }
                return s;
            }

            public void addCollectedPoints(BenchRound br, AccessType s, int i)
            {
                if (br == null) MessageBox.Show("Fuck");
                if (shortChart == null) MessageBox.Show("shjit");
                if (shortChart.Series == null) MessageBox.Show("Damn");
                try
                {
                    shortChart.Series.Add(collectDataPoints(br, DetailLevel.shorttype, s, i));
                    fullChart.Series.Add(collectDataPoints(br, DetailLevel.fulltype, s, i));
                }
                catch (NullReferenceException x)
                {
                    MessageBox.Show
                   (
                        "addCollectedPoints exception:\n" + 
                        "full chart is " + (fullChart == null ? "INDEED" : "NOT") + " null;\n" +
                        "short chart is " + (shortChart == null ? "INDEED" : "NOT") + " null;\n" +
                        x.ToString()
                   );
                }
            }
        }

        private static class CsvWriter
        {
            private static string[] meminfos_headers = { "Pre-warmup", "Pre-run", "Mid-run", "Post-run", "Post-unmap" };
            private static string memitems_headers_linux = "Free KiB,Buffer KiB,Cache KiB,Active KiB,Inactive KiB,Page in/s,Page out/s,Swap in/s,Swap out/s,Major faults\n";
            private static string memitems_headers_windows = "AvailPhys,dwMemoryLoad,TotalPageFile,AvailPageFile,AvailVirtual\n";
            private static string results_headers = "Thread #,Net avg. (us),Net avg. (clk),Latency (us),Latency (clk),Samples,Overhead (us),Overhead (clk)\n"; //,Total\n";
            private static string params_headers = "OS/kernel,Swap device,Phys. memory,Map size,Jobs,Delay,Read/write ratio,Niceness\n";

            private static void writePivotCsvSignature(int term, BenchPivot bp)
            {
                string div1 = ",", div2 = "*";
                switch (term)
                {
                    case (0):
                        bp.outfile.Write((bp.pivotIndex == 0 ? div2 : bp.baseParams.operatingSystem) + div1);
                        break;
                    case (1):
                        bp.outfile.Write((bp.pivotIndex == 1 ? div2 : bp.baseParams.swapDevice) + div1);
                        break;
                    case (2):
                        bp.outfile.Write((bp.pivotIndex == 2 ? div2 : bp.baseParams.valueMemory.ToString() + "MiB") + div1);
                        break;
                    case (3):
                        bp.outfile.Write((bp.pivotIndex == 3 ? div2 : bp.baseParams.valueMapsize.ToString() + "MiB") + div1);
                        break;
                    case (4):
                        bp.outfile.Write((bp.pivotIndex == 4 ? div2 : bp.baseParams.valueJobs.ToString()) + div1);
                        break;
                    case (5):
                        bp.outfile.Write((bp.pivotIndex == 5 ? div2 : bp.baseParams.valueDelay.ToString()) + div1);
                        break;
                    case (6):
                        bp.outfile.Write((bp.pivotIndex == 6 ? div2 : bp.baseParams.valueRatio.ToString()) + div1);
                        break;
                    /*case (7):
                        outfile.Write((pivotIndex == 7 ? div2 : baseParams.valueNice.ToString()));
                        break;*/
                    default:
                        break;
                }
            }

            public static string getPivotDumpHeader(int i, BenchPivot bp) //i = crony #
            {
                if (bp.pivotIndex == 8) { return (i == 5 ? "Average" : "Trial " + (i + 1)); }
                switch (bp.pivotIndex)
                {
                    case (0):   //OS/Kernel
                        return bp.cronies[i].operatingSystem();
                    case (1):   //Device
                        return bp.cronies[i].swapDevice();
                    case (2):   //Phys. memory
                        return (bp.cronies[i].valueMemory().ToString() + " memory");
                    case (3):   //Map size
                        return (bp.cronies[i].valueMapsize().ToString() + " map");
                    case (4):   //Jobs
                        return bp.cronies[i].jobs().ToString();
                    case (5):   //Delay
                        switch (int.Parse(bp.cronies[i].valueDelay().ToString()))
                        {
                            case (0):
                                return "None";
                            default:
                                return (bp.cronies[i].valueDelay().ToString() + " clk");
                        }
                    case (6):   //Ratio
                        switch (bp.cronies[i].ratio())
                        {
                            case (0):
                                return "Write-only";
                            case (100):
                                return "Read-only";
                            default:
                                return (bp.cronies[i].ratio().ToString() + "%");
                        }
                    case (7):   //Nice
                        return "0"; // cronies[i].valueNice().ToString();
                    case (9): //stopgap
                        return bp.cronies[i].customName;
                    default:
                        return "ERROR getPivotDumpHeader(" + i + ") index " + bp.pivotIndex;
                }
            }

            public static int writePivotCsvDump(string folder, BenchPivot bp, ref StreamWriter outfile)
            {

                string path = "";
                bool good = true;

                string csvfilename = (
                    (bp.pivotIndex == 0 ? "all" : bp.baseParams.operatingSystem) + "_" +
                    (bp.pivotIndex == 1 ? "all" : bp.baseParams.swapDevice) + "_" +
                    (bp.pivotIndex == 2 ? "all" : bp.baseParams.valueMemory.ToString() + "MiB") + "_" +
                    (bp.pivotIndex == 3 ? "all" : bp.baseParams.valueMapsize.ToString() + "MiB") + "_" +
                    (bp.pivotIndex == 4 ? "all" : bp.baseParams.valueJobs.ToString()) + "_" +
                    (bp.pivotIndex == 5 ? "all" : bp.baseParams.valueDelay.ToString()) + "_" +
                    (bp.pivotIndex == 6 ? "all" : bp.baseParams.valueRatio.ToString()) + "_" +
                    (bp.pivotIndex == 7 ? "all" : bp.baseParams.valueNice.ToString())
                );

                if (folder == null)
                {
                    using (SaveFileDialog save = new SaveFileDialog())
                    {
                        save.Filter = "csv files (*.csv)|*.csv";
                        save.FilterIndex = 1;
                        save.RestoreDirectory = true;
                        save.AddExtension = true;
                        save.DefaultExt = "csv";
                        save.FileName = csvfilename;
                        save.InitialDirectory = Environment.SpecialFolder.UserProfile.ToString();
                        if (save.ShowDialog() == DialogResult.OK) { path = Path.GetFullPath(save.FileName); }
                        else { good = false; }
                    }
                }
                else { path = folder + "\\" + csvfilename + ".csv"; }
                if (good)
                {
                    try
                    {
                        outfile = new StreamWriter(path);
                        outfile.Write(params_headers);
                        for (int i = 0; i < 8; i++) { writePivotCsvSignature(i, bp); }
                        outfile.Write("\n\n");
                        XmlNode report, result;
                        for (int h = 0; h < bp.cronies.Count; h++)
                        {
                            outfile.Write(getPivotDumpHeader(h, bp) + ",");
                            outfile.Write(results_headers);
                            report = safeSelectSingleNode(bp.cronies[h].roundNode, "pmbenchmark/report");
                            for (int j = 1; j <= bp.cronies[h].jobs(); j++)
                            {
                                result = safeSelectSingleNode(report, "result/result_thread[@thread_num='" + j + "']");
                                bp.outfile.Write
                                (
                                    " ," + j + "," +
                                    safeParseSingleNodeDouble(result, "result_netavg/netavg_us") + "," +
                                    safeParseSingleNodeDouble(result, "result_netavg/netavg_clk") + "," +
                                    safeParseSingleNodeDouble(result, "result_details/details_latency/latency_us") + "," +
                                    safeParseSingleNodeDouble(result, "result_details/details_latency/latency_clk") + "," +
                                    safeParseSingleNodeDouble(result, "result_details/details_samples") + "," +
                                    safeParseSingleNodeDouble(result, "result_details/details_overhead/overhead_us") + "," +
                                    safeParseSingleNodeDouble(result, "result_details/details_overhead/overhead_clk") + "\n" //"," + 
                                );
                            }
                        }
                        outfile.Write("\n");
                        report = null;
                        result = null;

                        List<XmlNode> histos;
                        bool first = true;
                        if (bp.pivotIndex == 6 || bp.baseParams.valueRatio > 0)
                        {
                            bp.outfile.Write("Read latencies,,");
                            histos = new List<XmlNode>();
                            for (int h = 0; h < bp.cronies.Count; h++)
                            {
                                if (bp.cronies[h].ratio() > 0)
                                {
                                    if (!first) { bp.outfile.Write(","); }
                                    else { first = false; }
                                    bp.outfile.Write(getPivotDumpHeader(h, bp));
                                    histos.Add(safeSelectSingleNode(bp.cronies[h].roundNode, "pmbenchmark/report/statistics/histogram[@type='read']"));
                                }
                            }
                            bp.outfile.Write("\n");
                            //MessageBox.Show("Writing histograms for " + histos.Count + " histograms");
                            writeCommaSeparatePivotHistogramList(histos, bp);
                            histos.Clear();
                        }
                        if (bp.pivotIndex == 6 || bp.baseParams.valueRatio < 100)
                        {
                            first = true;
                            bp.outfile.Write("Write latencies,,");
                            histos = new List<XmlNode>();
                            for (int h = 0; h < bp.cronies.Count; h++)
                            {
                                if (bp.cronies[h].ratio() < 100)
                                {
                                    if (!first) { bp.outfile.Write(","); }
                                    else { first = false; }
                                    bp.outfile.Write(getPivotDumpHeader(h, bp));
                                    histos.Add(safeSelectSingleNode(bp.cronies[h].roundNode, "pmbenchmark/report/statistics/histogram[@type='write']"));
                                }
                            }
                            bp.outfile.Write("\n");
                            writeCommaSeparatePivotHistogramList(histos, bp);
                            histos.Clear();
                        }
                        histos = null;

                        for (int m = 0; m < bp.cronies.Count; m++)
                        {
                            XmlNodeList sys_mem_items = bp.cronies[m].roundNode.SelectNodes("pmbenchmark/report/sys_mem_info/sys_mem_item");
                            int j = bp.cronies[m].cold();
                            if (bp.cronies[m].windowsbench())
                            {
                                bp.outfile.Write(getPivotDumpHeader(m, bp) + "," + memitems_headers_windows);
                                for (int k = 0; k < sys_mem_items.Count; k++)
                                {
                                    bp.outfile.Write(meminfos_headers[k + j] + ",");
                                    XmlNode item = sys_mem_items.Item(k);
                                    writeCommaSeparateMemInfoWindows(safeSelectSingleNode(item, "mem_item_info"), bp);
                                    if (!item.Attributes.Item(0).Value.Equals("post-unmap"))
                                    {
                                        bp.outfile.Write("Delta,");
                                        writeCommaSeparateMemInfoWindows(safeSelectSingleNode(item, "mem_item_delta"), bp);
                                    }
                                    item = null;
                                }
                            }
                            else
                            {
                                bp.outfile.Write(getPivotDumpHeader(m, bp) + "," + memitems_headers_linux);
                                for (int k = 0; k < sys_mem_items.Count; k++)
                                {
                                    bp.outfile.Write(meminfos_headers[k + j] + ",");
                                    XmlNode item = sys_mem_items.Item(k);
                                    writeCommaSeparateMemInfoLinux(safeSelectSingleNode(item, "mem_item_info"), bp);
                                    if (!item.Attributes.Item(0).Value.Equals("post-unmap"))
                                    {
                                        bp.outfile.Write("Delta,");
                                        writeCommaSeparateMemInfoLinux(safeSelectSingleNode(item, "mem_item_delta"), bp);
                                    }
                                    item = null;
                                }
                            }
                            sys_mem_items = null;
                        }
                        outfile.Flush();
                        outfile.Close();

                        if (folder == null) { MessageBox.Show("Wrote CSV to " + path); }
                    }
                    catch (IOException x) { MessageBox.Show("Error writing file to " + path + "\n" + x.ToString()); return 0; }
                }
                path = null;
                return 1;
            }

            private static void writeCommaSeparateMemInfoLinux(XmlNode info, BenchPivot bp)
            {
                try
                {
                    bp.outfile.Write
                    (
                        safeSelectSingleNode(info, "free_kib").InnerText + "," +
                        safeSelectSingleNode(info, "buffer_kib").InnerText + "," +
                        safeSelectSingleNode(info, "cache_kib").InnerText + "," +
                        safeSelectSingleNode(info, "active_kib").InnerText + "," +
                        safeSelectSingleNode(info, "inactive_kib").InnerText + "," +
                        safeSelectSingleNode(info, "pgpgin").InnerText + "," +
                        safeSelectSingleNode(info, "pgpgout").InnerText + "," +
                        safeSelectSingleNode(info, "pswpin").InnerText + "," +
                        safeSelectSingleNode(info, "pswpout").InnerText + "," +
                        safeSelectSingleNode(info, "pgmajfault").InnerText + "\n"
                    );
                }
                catch (NullReferenceException x)
                {
                    MessageBox.Show("BenchPivot.CsvWriter.writeCommaSeparateMemInfoLinux error: Null reference exception\n" + x.ToString());
                }
            }

            private static void writeCommaSeparateMemInfoWindows(XmlNode info, BenchPivot bp)
            {
                bp.outfile.Write
                (
                    safeSelectSingleNode(info, "AvailPhys").InnerText + "," +
                    safeSelectSingleNode(info, "dwMemoryLoad").InnerText + "," +
                    safeSelectSingleNode(info, "TotalPageFile").InnerText + "," +
                    safeSelectSingleNode(info, "AvailPageFile").InnerText + "," +
                    safeSelectSingleNode(info, "AvailVirtual").InnerText + "\n"
                );
            }

            private static void writeCommaSeparateFullBucket(List<XmlNode> nodes, int i, BenchPivot bp)
            {
                //write bucket i of all nodes in order
                double lo = Math.Pow(2, i + 7);
                double hi = Math.Pow(2, i + 8);
                double mid = (hi - lo) / 16;
                for (int j = 0; j < 16; j++) //bucket hexes with indexes 0-15
                {
                    double gap1 = lo + (j * mid);
                    double gap2 = gap1 + mid;
                    bp.outfile.Write(gap1 + "," + gap2 + ",");
                    for (int k = 0; k < nodes.Count; k++)
                    {
                        bp.outfile.Write(safeParseSingleNodeDouble(nodes[k], "histo_bucket[@index='" + i + "']/bucket_hexes/hex[@index='" + j + "']"));
                        if (k == nodes.Count - 1) { bp.outfile.Write("\n"); }
                        else { bp.outfile.Write(","); }
                    }
                }
            }

            private static void writeCommaSeparateSumCounts(XmlNode[] buckets, BenchPivot bp)
            {
                if (buckets == null)
                {
                    MessageBox.Show("writeCommaSeparateSumCounts error: null buckets");
                    return;
                }
                if (buckets[0] == null)
                {
                    MessageBox.Show("writeCommaSeparateSumCounts error: null first element");
                    return;
                }
                if (buckets[0].Attributes.Count == 0)
                {
                    MessageBox.Show("writeCommaSeparateSumCounts error: zero attributes");
                    return;
                }
                if (!buckets[0].Attributes.Item(0).Name.Equals("index"))
                {
                    MessageBox.Show("writeCommaSeparateSumCounts error: attribute name is " + buckets[0].Attributes.Item(0).Name);
                    return;
                }
                try
                {
                    int bucket_index = int.Parse(buckets[0].Attributes.Item(0).Value);
                    double lo, hi, interval_hi = safeParseSingleNodeInt(buckets[0], "bucket_interval/interval_hi");
                    double interval_lo = safeParseSingleNodeInt(buckets[0], "bucket_interval/interval_lo");
                    lo = Math.Pow(2, interval_lo);
                    hi = Math.Pow(2, interval_hi);
                    bp.outfile.Write(lo + "," + hi + ",");
                    for (int j = 0; j < buckets.Length; j++)
                    {
                        bp.outfile.Write(safeParseSingleNodeDouble(buckets[j], "sum_count"));
                        if (j == buckets.Length - 1) bp.outfile.Write("\n");
                        else bp.outfile.Write(",");
                    }
                }
                catch (ArgumentException x)
                {
                    MessageBox.Show("writeCommaSeparateSumCounts ArgumentException:\n" + x.ToString());
                    return;
                }
            }

            public static void writeCommaSeparatePivotHistogramList(List<XmlNode> nodes, BenchPivot bp)
            {
                //MessageBox.Show("writeCommaSeparatePivotHistogramList: Received a list of " + nodes.Count + " nodes");
                XmlNodeList[] bucket0s = new XmlNodeList[nodes.Count];
                bp.outfile.Write("0,256,");
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] == null)
                    {
                        MessageBox.Show("writeCommaseparatePivotHistogramList: Received null node at position " + i);
                        return;
                    }

                    //bucket0s[i] contains all of the bucket 0's for crony i
                    bucket0s[i] = nodes[i].SelectNodes("histo_bucket[@index='0']");

                    if (safeParseSingleNodeInt(bucket0s[i].Item(0), "bucket_interval/interval_lo") != 0)
                    {
                        MessageBox.Show("commaSeparateHistogramList: missing hit_counts_sum on test round " + i + 1);
                    }

                    bp.outfile.Write(safeParseSingleNodeDouble(bucket0s[i].Item(0), "sum_count"));
                    if (i == nodes.Count - 1)
                    {
                        bp.outfile.Write("\n");
                    }
                    else
                    {
                        bp.outfile.Write(",");
                    }
                }

                for (int i = 1; i < 16; i++) //buckets with indexes 1-15
                {
                    if (bp.showFull)
                    {
                        writeCommaSeparateFullBucket(nodes, i, bp);
                    }
                    else
                    {
                        XmlNode[] buckets = new XmlNode[nodes.Count];
                        for (int k = 0; k < nodes.Count; k++)
                        {
                            buckets[k] = safeSelectSingleNode(nodes[k], "histo_bucket[@index='" + i + "']");
                        }
                        writeCommaSeparateSumCounts(buckets, bp);
                    }
                }
                for (int i = 1; i < bucket0s[0].Count; i++) //skip the first sum_count
                {
                    try
                    {
                        XmlNode[] bucket0s_high = new XmlNode[nodes.Count];
                        for (int k = 0; k < nodes.Count; k++)
                        {
                            bucket0s_high[k] = bucket0s[k].Item(i);
                        }
                        writeCommaSeparateSumCounts(bucket0s_high, bp);
                    }
                    catch (IndexOutOfRangeException x)
                    {
                        MessageBox.Show("Index out of range exception at " + i.ToString() + " of " + bucket0s[0].Count + ":\n" + x.ToString());
                    }
                }
                bucket0s = null;
                bp.outfile.Write("\n");
            }
        }

        private PivotChart thePivotChart;
        protected int pivotIndex;
        protected ParamSet baseParams;
        public List<BenchRound> cronies;
        private bool chartReady;
        public PmGraph parserForBenchPivot;
        protected StreamWriter outfile;
        private static Color[] readColors = { Color.Blue, Color.Cyan, Color.Green, Color.MidnightBlue, Color.DarkCyan, Color.BlueViolet, Color.LimeGreen };
        private static Color[] writeColors = { Color.Red, Color.Fuchsia, Color.Orange, Color.SaddleBrown, Color.Maroon, Color.Chocolate, Color.HotPink };
        public bool dumped = false;
        private bool showFull { set; get; }
        public Point chartPoint;
        public enum AccessType { uninitialized = -1, read = 0, write = 1 }
        public enum DetailLevel { uninitialized = -1, shorttype = 0, fulltype = 1, currenttype = 2 }

        public void selectAll()
        {
            this.thePivotChart.selectAll();
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

        public BenchSiblings averageSelected(int avgc)
        {
            parserForBenchPivot.updateSelectionButtons(0);
            XmlDocument doc = new XmlDocument();
            XmlNode fakeSeries = doc.CreateNode(XmlNodeType.Element, "test_nice", doc.NamespaceURI);
            doc.AppendChild(fakeSeries);
            ParamSet bp = new ParamSet();
            int flagcount = 0;
            try
            {
                for (int j = 0; j < cronies.Count; j++)
                {
                    if (cronies[j].flaggedForAverage)
                    {
                        XmlDocument tempdoc = cronies[j].roundNode.OwnerDocument;
                        XmlNode fakeRound = doc.CreateNode(XmlNodeType.Element, "test_round", doc.NamespaceURI);
                        XmlAttribute iter = doc.CreateAttribute("iter");
                        iter.Value = (flagcount++ + 1).ToString();
                        fakeRound.Attributes.Append(iter);
                        if (safeSelectSingleNode(tempdoc, "test_nice/test_round/pmbenchmark") == null)
                        {
                            MessageBox.Show("pmbenchmark node not found; root element is " + tempdoc.DocumentElement.Name);
                        }
                        fakeRound.AppendChild(doc.ImportNode(safeSelectSingleNode(tempdoc, "test_nice/test_round/pmbenchmark"), true));
                        fakeSeries.AppendChild(fakeRound);
                        bp.setParamsFromNode(PmGraph.getParamsNodeFromSeriesNode(fakeSeries));
                        bp.operatingSystem = safeSelectSingleNode(tempdoc, "test_nice/test_round/pmbenchmark/report/signature/pmbench_info/version_options").InnerText;
                    }
                }
            }
            catch (FileNotFoundException x)
            {
                MessageBox.Show("averageSelected:\n" + x.ToString());
                return null;
            }
            catch (ArgumentException x)
            {
                MessageBox.Show("averageSelected: ArgumentException\n" + x.ToString());
                return null;
            }
            BenchSiblings bs = new BenchSiblings(fakeSeries, doc, bp);
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
                        if (debug) MessageBox.Show(cronies[j].customName + " has neither a write nor a read series, deleting it now");
                        string s = cronies[j].customName;
                        cronies.RemoveAt(j);
                        parserForBenchPivot.removeDeadXmlDoc(s);
                    }
                    else if (debug) MessageBox.Show(cronies[j].customName + " still has a series"); 
                    j--;
                }
            }
            if (debug)
            {
                string cronytest = "";
                for (int i = 0; i < cronies.Count; i++)
                {
                    cronytest += cronies[i].customName + " has " + (cronies[i].hasReadSeries && cronies[i].hasWriteSeries ? "both" : (cronies[i].hasReadSeries ? "read" : (cronies[i].hasWriteSeries ? "write" : ""))) + "\n";
                }
                MessageBox.Show("Done deleting, pivot now has " + cronies.Count + " cronies:\n" + cronytest);
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

        public BenchPivot(ParamSet bp, int pivotindex, List<BenchRound> br, PmGraph parser)
        {
            this.thePivotChart = null;
            this.chartReady = false;
            this.baseParams = bp;
            this.pivotIndex = pivotindex;
            this.cronies = br;
            this.parserForBenchPivot = parser;
            chartPoint = new Point(); 
        }

        public string getPivotDumpHeader(int i) { return CsvWriter.getPivotDumpHeader(i, this);  }
        private static XmlNode safeSelectSingleNode(XmlNode n, string s) { return PmGraph.safeSelectSingleNode(n, s); }
        private static int safeParseSingleNodeInt(XmlNode n, string s) { return PmGraph.safeParseSingleNodeInt(n, s); }
        private static double safeParseSingleNodeDouble(XmlNode n, string s) { return PmGraph.safeParseSingleNodeDouble(n, s); }

        public Chart getPreparedChart(int w, int h, CheckBox b)
        {
            if (!chartReady) return initializePivotChart(w, h, b); 
            try
            {
                Chart pivotChart = thePivotChart.getPivotChart(DetailLevel.currenttype);
                pivotChart.Width = w;
                pivotChart.Height = h;
                return pivotChart;
            }
            catch (NullReferenceException x)
            {
                MessageBox.Show("BenchPivot.getPrepare_Chart info: null reference exception, the pivot chart is " + ((thePivotChart != null) ? "NOT" : "") + " null.\n" + x.ToString());
                return null;
            }
        }

        private Chart initializePivotChart(int width, int height, CheckBox b) 
        {
            if (this.cronies == null) { MessageBox.Show("BenchPivot.updateCharts Error: cronies list is empty!"); return null; }
            for (int i = 0; i < cronies.Count; i++)
            {
                if (cronies[i] == null) { MessageBox.Show("BenchPivot.updateCharts Error: crony " + i + " is null!"); return null; }
                cronies[i].getRoundChart(DetailLevel.currenttype);
            }

            thePivotChart = new PivotChart(this, width, height);
            for (int i = 0; i < cronies.Count; i++)
            {
                if (cronies[i].wasDeletedDontBother) continue;
                try
                {
                    if (cronies[i].ratio() > 0) thePivotChart.addCollectedPoints(cronies[i], AccessType.read, i);
                    if (cronies[i].ratio() < 100) thePivotChart.addCollectedPoints(cronies[i], AccessType.write, i);
                }
                catch (NullReferenceException x)
                {
                    MessageBox.Show("BenchPivot.initPivotChart(" + width + ", " + height + "): Null reference exception.\n(thePivotChart == null) == " + (thePivotChart == null).ToString() + "\n(cronies[" + i + "] == null) == " + (cronies[i] == null).ToString() + "\n" + x.ToString());
                }
            }
            thePivotChart.updateHoverSeries(null);
            setFull(b.Checked);
            chartReady = (thePivotChart.currentChart != null);
            if (!chartReady) { MessageBox.Show("BenchPivot.initPivotChart(" + width + ", " + height + ") error: pivot chart is NOT ready."); }
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
            if (thePivotChart != null)
            {
                if (chartReady)
                {
                    if (thePivotChart.currentChart == null) { MessageBox.Show("destroyChart Warning: Chart already destroyed"); return; }
                    thePivotChart.currentChart.Dispose();
                    thePivotChart.currentChart = null;
                    chartReady = false;
                }
                else { MessageBox.Show("BenchPivot.destroyPivotChart info: Chart is not ready"); }
            }
        }
    }

    public partial class PmGraph : Form
    {
        public partial class ControlPanel : FlowLayoutPanel
        {
            public Button resultsButton, loadAutomaticButton, exportButton, autoExportButton, verifyButton, cancelButton, averageSelectedButton, deleteSelectedButton, selectAllButton, selectNoneButton, helpButton;

            public CheckBox autoCheck, fullCheck;
            public ComboBox dropKernel, dropDevice, dropMemory, dropMapsize, dropJobs, dropDelay, dropRatio;
            public int radioIndex;
            public int currentKernelIndex, currentDeviceIndex, currentMemoryIndex, currentMapsizeIndex, currentJobsIndex, currentDelayIndex, currentRatioIndex;
            private int tempKernelIndex, tempDeviceIndex, tempMemoryIndex, tempMapsizeIndex, tempJobsIndex, tempDelayIndex, tempRatioIndex, tempRadioIndex;
            private bool tempAutoChecked;
            private static Padding controlPadding = new Padding(0, 6, 0, 0), panelPadding = new Padding(5, 0, 0, 0), actionButtonPadding = new Padding(3, 0, 0, 0);
            public RadioButton radioKernel, radioDevice, radioMemory, radioMapsize, radioJobs, radioDelay, radioRatio, radioNone, radioSelected;
            private static Size radioSize = new Size(13, 13), labelSize = new Size(72, 14);
            private CancellationTokenSource cancelSource;
            private FlowLayoutPanel loadValidateRow, exportRow;
            private PmGraph parser;
            private string importStandaloneDirectory = null;
            private Button importManualAverageButton, exportManualButton, importManualSingleButton;
            public CheckBox manualCheck;
            private Label manualLabel;
            //public TextBox nameAveragesField;

            /*public string getTextBoxContents(bool clear)
            {
                string s = nameAveragesField.Text;
                if (clear) nameAveragesField.Clear();
                return s;
            }*/

            
            
            private void helpButton_click(object sender, EventArgs e)
            {
                MessageBox.Show
               (
                    "Buttons:\n" +
                        "\tImport opens XML files produced by pmbench with the -f parameter;\n" +
                        "\tExport saves a CSV file reflecting the data presented in the graph;\n" +
                        "\tAverage adds the average of all selected items to the graph;\n" +
                        "\tDelete removes the selected items from the graph;\n" +
                        "\tSelect all selects all graphed items.\n\n" +
                    "Hover the mouse over a graphed item's name in the legend to highlight it;\n" +
                    "Left click an item's name to select it;\n"+
                    "Middle click an item's name to show its peak latencies."
               );
            }

            private void radio_click(object o, EventArgs args)
            {
                RadioButton b = o as RadioButton;
                if (b == null)
                {
                    MessageBox.Show("ControlPanel.radio_click(null, args) Error");
                    return;
                }
                if (!b.Checked) { return; }
                else
                {
                    exportButton.Enabled = false;
                    radioSelected = b;
                    setClickedRadioButton(radioSelected.Name);
                    parser.dropSelectionChanged(autoCheck);
                }
            }

            private RadioButton getRadioButton(int id)
            {
                switch (id)
                {
                    case (0): return radioKernel;
                    case (1): return radioDevice;
                    case (2): return radioMemory;
                    case (3): return radioMapsize;
                    case (4): return radioJobs;
                    case (5): return radioDelay;
                    case (6): return radioRatio;
                    //case (7): return radioNice;
                    case (8): return radioNone;
                    default: MessageBox.Show("ControlPanel.getRadioButton(" + id + "): Error, no such radio button"); return null;
                }
            }

            public void setRadioIndex(int i)
            {
                RadioButton current = getRadioButton(radioIndex), newbutton = getRadioButton(i);
                if (current.InvokeRequired || newbutton.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)delegate () { setRadioIndex(i); });
                }
                else
                {
                    radioIndex = i;
                    newbutton.Select();
                }
            }

            private bool dropdownsEnabled, autoActionButtonsEnabled;

            private void manualCheck_click(object sender, EventArgs args)
            {
                if (manualCheck.Checked == true)
                {
                    //loadAutomaticButton.Enabled = false;
                    //dropdownsEnabled = radioDelay.Enabled;
                    //autoActionButtonsEnabled = autoExportButton.Enabled;
                    setControlsEnabled(false, false, true);
                    //importManualAverageButton.Enabled = true;
                    averageSelectedButton.Enabled = parser.doesPivotHaveSelections();
                    deleteSelectedButton.Enabled = parser.doesPivotHaveSelections();
                    selectAllButton.Enabled = (!parser.doesPivotHaveSelections());
                    exportManualButton.Enabled = true;
                    importManualSingleButton.Enabled = true;
                    //nameAveragesField.Enabled = true;
                }
                if (manualCheck.Checked == false)
                {
                    setControlsEnabled(dropdownsEnabled, autoActionButtonsEnabled, autoActionButtonsEnabled);
                    exportManualButton.Enabled = false;
                    //importManualAverageButton.Enabled = false;
                    averageSelectedButton.Enabled = false;
                    deleteSelectedButton.Enabled = false;
                    importManualSingleButton.Enabled = false;
                    selectAllButton.Enabled = false;
                    //nameAveragesField.Enabled = false;
                }
            }

            private TextBox initTextBox(string s)
            {
                TextBox tb = new TextBox();
                tb.MaxLength = 255;
                tb.AllowDrop = true;
                tb.Width = 255;
                return tb;
            }

            public ControlPanel(PmGraph p)
            {
                if (p == null) { MessageBox.Show("ControlPanel(null) Error"); return; }
                parser = p;

                Padding checkPadding = new Padding(6, 8, 0, 0), checkLabelPadding = new Padding(0, 8, 0, 0); ;

                autoCheck = new CheckBox();
                autoCheck.Enabled = true;
                autoCheck.Size = radioSize;
                autoCheck.Margin = checkPadding;
                autoCheck.Checked = true;
                Label labelUpdate = new Label();
                labelUpdate.Text = "Auto update";
                labelUpdate.Enabled = true;
                labelUpdate.Size = new Size(65, 14);
                labelUpdate.Margin = checkLabelPadding;

                fullCheck = new CheckBox();
                fullCheck.Enabled = true;
                fullCheck.Size = radioSize;
                fullCheck.Margin = checkPadding;
                Label fullLabel = new Label();
                fullLabel.Text = "Show detailed results";
                fullCheck.Checked = true;
                fullLabel.Enabled = true;
                fullLabel.Margin = checkLabelPadding;
                fullCheck.CheckStateChanged += new EventHandler(parser.showFullChanged_action);
                Label labelNone = new Label();
                labelNone.Text = "No pivot variable";
                radioNone = initRadioButton("None", "test iteration");
                radioNone.Margin = new Padding(0, 0, 0, 0);

                this.Width = 220;
                this.Height = parser.Height;

                manualCheck = new CheckBox();
                manualCheck.Enabled = true;
                manualCheck.Width = 16;
                manualLabel = new Label();
                manualLabel.Text = "Manual import/export";
                manualCheck.Checked = true;
                manualCheck.CheckedChanged += new EventHandler(manualCheck_click);

                manualCheck.Margin = new Padding(0, 0, 0, 5);
                manualLabel.Margin = new Padding(0, 5, 0, 0);
                manualLabel.Width = 110;

                this.Controls.AddRange(new Control[]
                {
                //initControlRow2(new Control[] { manualLabel, manualCheck }, 130, 20, FlowDirection.RightToLeft, new Padding(4, 0, 0, 0)),
                initControlRow(new Control[]
                {
                    importManualSingleButton = initButton("Import", importSingleBenches_click, true),
                    exportManualButton = initButton("Export", parser.exportCsvManual, false)
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
                /*initControlRow(new Control[]
                {
                    importManualAverageButton = initButton("Import & avg", importAverageBenches_click, true)
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),*/
                //nameAveragesField = initTextBox("Enter name"),
                initControlRow(new Control[]
                {
                    averageSelectedButton = initButton("Average selected", parser.averageSelectedButton_click, false),
                    deleteSelectedButton = initButton("Delete seleted", parser.deleteSelectedButton_click, false)
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
                initControlRow(new Control[]
                {
                    selectAllButton = initButton("Select all", parser.selectAll_click, false),
                    helpButton = initButton("Instructions", helpButton_click, true)
                    //autoCheck, labelUpdate
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
                /*initControlRow(new Control[]
                {
                    selectInstructions
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
                initControlRow(new Control[]
                {
                    selectInstructions
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),*/
                /*initControlRow(new Control[]
                {
                    dropKernel = initDropMenu(new object[] { "Fedora 23 native", "Fedora 23 Xen", "Windows 10 native", "Windows 10 Xen" }, 0),
                    initDropLabel("OS/Kernel"),
                    radioKernel = initRadioButton("Kernel", "OS/kernel")
                }, 213, 25, FlowDirection.RightToLeft, panelPadding),
                initControlRow(new Control[]
                {
                    dropDevice = initDropMenu(new object[] { "Chatham", "NAND SSD", "RAM disk" }, 0),
                    initDropLabel("Swap device"),
                    radioDevice = initRadioButton("Device", "swap device")
                }, 213, 25, FlowDirection.RightToLeft, panelPadding),
                initControlRow(new Control[]
                {
                    dropMemory = initDropMenu(new object[] { 256, 512, 1024, 2048, 4096, 8192, 16384 }, 1),
                    initDropLabel("Phys. memory"),
                    radioMemory = initRadioButton("Memory", "physical memory")
                }, 213, 25, FlowDirection.RightToLeft, panelPadding),
                initControlRow(new Control[]
                {
                    dropMapsize = initDropMenu(new object[] { 512, 1024, 2048, 4096, 8192, 16384, 32768 }, 1),
                    initDropLabel("Map size"),
                    radioMapsize = initRadioButton("Mapsize", "map size")
                }, 213, 25, FlowDirection.RightToLeft, panelPadding),
                initControlRow(new Control[]
                {
                    dropJobs = initDropMenu(new object[] { 1, 8 }, 0),
                    initDropLabel("Jobs"),
                    radioJobs = initRadioButton("Jobs", "number of worker threads")
                }, 213, 25, FlowDirection.RightToLeft, panelPadding),
                initControlRow(new Control[]
                {
                    dropDelay = initDropMenu(new object[] { 0, 1000 }, 0),
                    initDropLabel("Delay"),
                    radioDelay = initRadioButton("Delay", "delay period")
                }, 213, 25, FlowDirection.RightToLeft, panelPadding),
                initControlRow(new Control[]
                {
                    dropRatio = initDropMenu(new object[] { 0, 50, 100 }, 0),
                    initDropLabel("Ratio"),
                    radioRatio = initRadioButton("Ratio", "read to write percentage")
                }, 213, 25, FlowDirection.RightToLeft, panelPadding),
                /*initControlRow(new Control[]
                {
                    dropNice = initDropMenu(new object[] { 19, -20, 0 }, 2),
                    initDropLabel("Nice"),
                    radioNice = initRadioButton("Nice", "thread priority")
                }, 213, 25, FlowDirection.RightToLeft, panelPadding),*/
                /*initControlRow(new Control[]
                {
                    labelNone,
                    radioNone
                }, 119, 16, FlowDirection.RightToLeft, new Padding(6, 6, 0, 0)),
                loadValidateRow = initControlRow(new Control[]
                {
                    loadAutomaticButton = initButton("Load XML", parser.loadXmlFiles_click, true),
                    verifyButton = initButton("Validate", parser.validate_click, false)
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
                initControlRow(new Control[]
                {
                    resultsButton = initButton("Results", parser.getResults_click, false),
                    //autoCheck, labelUpdate
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
                exportRow = initControlRow(new Control[]
                {
                    exportButton = initButton("Export CSV", parser.exportCsv_click, false),
                    autoExportButton = initButton("Auto export", parser.autoCsvDump_click, false)
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),*/
                /*initControlRow(new Control[]
                {
                    fullCheck, fullLabel
                }, 220, 28, FlowDirection.LeftToRight, actionButtonPadding)*/
                });
                FlowDirection = FlowDirection.TopDown;

                cancelButton = new Button();
                cancelButton.Text = "Cancel";
                cancelButton.Width = 50;
                cancelButton.Click += new EventHandler(cancel_click);
                cancelButton.Enabled = true;

                manualCheck_click(null, null);
                exportManualButton.Enabled = false;
            }

            public string getKey1Value(int i, int j)
            {
                switch (i)
                {
                    case 0: return dropKernel.Items[j].ToString();
                    case 1: return dropDevice.Items[j].ToString();
                    case 2: return dropMemory.Items[j].ToString();
                    default: return "ERROR";
                }
            }

            public string getKey1FromDropdowns()
            {
                return
                (
                    getKeyElementFromDropdowns(0) + "_" +
                    getKeyElementFromDropdowns(1) + "_" +
                    getKeyElementFromDropdowns(2)
                );
            }

            public string getKey2FromDropdowns()
            {
                return
                (
                    getKeyElementFromDropdowns(3) + "_" +
                    getKeyElementFromDropdowns(4) + "_" +
                    getKeyElementFromDropdowns(5) + "_" +
                    getKeyElementFromDropdowns(6) + "_" +
                    getKeyElementFromDropdowns(7)
                );
            }

            private string getKeyElementFromDropdowns(int menu)
            {
                ComboBox cb = getDropMenu(menu);
                if (cb.InvokeRequired) { return (string)this.Invoke(new Func<string>(() => getKeyElementFromDropdowns(menu))); }
                else { return cb.SelectedIndex.ToString(); }
            }

            private string getDropdownValueFromIndex(int menu, int index)
            {
                ComboBox cb = getDropMenu(menu);
                if (cb.InvokeRequired) { return (string)this.Invoke(new Func<string>(() => getDropdownValueFromIndex(menu, index))); }
                else { return cb.Items[index].ToString(); }
            }

            public string getNodeSelectionPathFromKey2(string key2)
            {
                char[] delimiter = { '_' };
                string[] key_split = key2.Split(delimiter);
                return
                (
                    "/benchmark_set/test_content/test_mapsize[@iter='" + getDropdownValueFromIndex(3, int.Parse(key_split[0])) +
                    "']/test_jobs[@iter='" + getDropdownValueFromIndex(4, int.Parse(key_split[1])) +
                    "']/test_delay[@iter='" + getDropdownValueFromIndex(5, int.Parse(key_split[2])) +
                    "']/test_ratio[@iter='" + getDropdownValueFromIndex(6, int.Parse(key_split[3])) +
                    "']/test_nice[@iter='" + getDropdownValueFromIndex(7, int.Parse(key_split[4])) +
                    "']"
                );
            }

            public int getPivotVariableCount(int i)
            {
                switch (i)
                {
                    case (0):
                        return dropKernel.Items.Count;
                    case (1):
                        return dropDevice.Items.Count;
                    case (2):
                        return dropMemory.Items.Count;
                    case (3):
                        return dropMapsize.Items.Count;
                    case (4):
                        return dropJobs.Items.Count;
                    case (5):
                        return dropDelay.Items.Count;
                    case (6):
                        return dropRatio.Items.Count;
                    case (7):
                        return 0; // dropNice.Items.Count;
                    case (8):
                        return 6;
                    default:
                        return 0;
                }
            }

            private void setClickedRadioButton(string name) //there has to be a better way of doing this
            {
                if (name.Equals("radioKernel")) { radioIndex = 0; } else { radioKernel.Checked = false; }
                if (name.Equals("radioDevice")) { radioIndex = 1; } else { radioDevice.Checked = false; }
                if (name.Equals("radioMemory")) { radioIndex = 2; } else { radioMemory.Checked = false; }
                if (name.Equals("radioMapsize")) { radioIndex = 3; } else { radioMapsize.Checked = false; }
                if (name.Equals("radioJobs")) { radioIndex = 4; } else { radioJobs.Checked = false; }
                if (name.Equals("radioDelay")) { radioIndex = 5; } else { radioDelay.Checked = false; }
                if (name.Equals("radioRatio")) { radioIndex = 6; } else { radioRatio.Checked = false; }
                //if (name.Equals("radioNice")) { radioIndex = 7; } else { radioNice.Checked = false; }
                if (name.Equals("radioNone")) { radioIndex = 8; } else { radioNone.Checked = false; }
            }

            private int getDropSelectedIndex(ComboBox cb)
            {
                if (cb.InvokeRequired) { return (int)this.Invoke(new Func<int>(() => getDropSelectedIndex(cb))); }
                else { return cb.SelectedIndex; }
            }
            private int getDropSelectedIndex(int i) { return getDropSelectedIndex(getDropMenu(i)); }

            private int getDropSelectedValue(ComboBox cb)
            {
                if (cb.InvokeRequired) { return (int)this.Invoke(new Func<int>(() => getDropSelectedIndex(cb))); }
                else
                {
                    if (cb.Name.Equals(dropKernel.Name) || cb.Name.Equals(dropDevice.Name)) { MessageBox.Show("(ControlPanel.getDropSelectedValue) Error: ComboBox " + cb.Name + " has non-integer member values"); return -1; }
                    return (int)(cb.SelectedItem);
                }
            }

            public void updateSavedIndices()
            {
                currentKernelIndex = getDropSelectedIndex(dropKernel);
                currentDeviceIndex = getDropSelectedIndex(dropDevice);
                currentMemoryIndex = getDropSelectedIndex(dropMemory);
                currentMapsizeIndex = getDropSelectedIndex(dropMapsize);
                currentJobsIndex = getDropSelectedIndex(dropJobs);
                currentDelayIndex = getDropSelectedIndex(dropDelay);
                currentRatioIndex = getDropSelectedIndex(dropRatio);
                //currentNiceIndex = getDropSelectedIndex(dropNice);
            }

            private static Button initButton(string text, EventHandler e, bool enable)
            {
                Button b = new Button();
                b.Text = text;
                b.Click += new EventHandler(e);
                b.Enabled = enable;
                return b;
            }

            private ComboBox initDropMenu(object[] itemNames, int i)
            {
                ComboBox cb = new ComboBox();
                cb.Items.AddRange(itemNames);
                cb.DropDownStyle = ComboBoxStyle.DropDownList;
                cb.SelectedIndex = i;
                cb.SelectedValueChanged += new EventHandler(parser.dropSelectionChanged_action);
                return cb;
            }

            private static Label initDropLabel(string name)
            {
                Label label = new Label();
                label.Text = name;
                label.Size = labelSize;
                label.Margin = controlPadding;
                return label;
            }

            private RadioButton initRadioButton(string name, string text)
            {
                RadioButton rb = new RadioButton();
                rb.Name = "radio" + name;
                rb.Size = radioSize;
                rb.Text = "Use " + text + " as pivot variable";
                rb.CheckedChanged += new EventHandler(radio_click);
                rb.Margin = controlPadding;
                return rb;
            }

            private FlowLayoutPanel initControlRow2(Control[] controls, int w, int h, FlowDirection d, Padding p)
            {
                FlowLayoutPanel flp = initControlRow(controls, w, h, d, p);
                return flp;
            }

            private FlowLayoutPanel initControlRow(Control[] controls, int w, int h, FlowDirection d, Padding p)
            {
                FlowLayoutPanel flp = new FlowLayoutPanel();
                flp.Width = w;
                flp.Height = h;
                flp.Controls.AddRange(controls);
                flp.FlowDirection = d;
                flp.Margin = p;
                return flp;
            }

            private string getPivotKeyElement(int ri, bool saved)
            {
                if (radioIndex == ri) { return ("*"); }
                switch (ri)
                {
                    case 0: return (saved ? currentKernelIndex.ToString() : getDropSelectedIndex(ri).ToString());
                    case 1: return (saved ? currentDeviceIndex.ToString() : getDropSelectedIndex(ri).ToString());
                    case 2: return (saved ? currentMemoryIndex.ToString() : getDropSelectedIndex(ri).ToString());
                    case 3: return (saved ? currentMapsizeIndex.ToString() : getDropSelectedIndex(ri).ToString());
                    case 4: return (saved ? currentJobsIndex.ToString() : getDropSelectedIndex(ri).ToString());
                    case 5: return (saved ? currentDelayIndex.ToString() : getDropSelectedIndex(ri).ToString());
                    case 6: return (saved ? currentRatioIndex.ToString() : getDropSelectedIndex(ri).ToString());
                    //case 7: return (saved ? currentNiceIndex.ToString() : getDropSelectedIndex(ri).ToString());
                    default: return ("(getPivotKeyElement(" + ri + ", " + saved.ToString() + ") Error");
                }
            }

            public string getPivotKeys(bool saved)
            {
                return
                (
                    getPivotKeyElement(0, saved) + "_" +
                    getPivotKeyElement(1, saved) + "_" +
                    getPivotKeyElement(2, saved) + "_" +
                    getPivotKeyElement(3, saved) + "_" +
                    getPivotKeyElement(4, saved) + "_" +
                    getPivotKeyElement(5, saved) + "_" +
                    getPivotKeyElement(6, saved) + "_" +
                    getPivotKeyElement(7, saved)
                );
            }

            public void setControlsEnabled(bool t1, bool t2, bool t3)
            {
                return;
                radioKernel.Enabled = t1;
                radioDevice.Enabled = t1;
                radioMemory.Enabled = t1;
                radioMapsize.Enabled = t1;
                radioJobs.Enabled = t1;
                radioDelay.Enabled = t1;
                radioRatio.Enabled = t1;
                //radioNice.Enabled = t1;
                radioNone.Enabled = t1;
                dropKernel.Enabled = t1;
                dropDevice.Enabled = t1;
                dropMemory.Enabled = t1;
                dropMapsize.Enabled = t1;
                dropJobs.Enabled = t1;
                dropDelay.Enabled = t1;
                dropRatio.Enabled = t1;
                //dropNice.Enabled = t1;
                verifyButton.Enabled = t2;
                exportButton.Enabled = t2;
                resultsButton.Enabled = t2;
                autoExportButton.Enabled = t2;
                autoCheck.Enabled = t3;
            }

            public void setCancelButton(CancellationTokenSource cancel, int row)
            {
                if (cancel == null)
                {
                    if (exportRow.Controls.Contains(cancelButton)) { exportRow.Controls.Remove(cancelButton); }
                    else if (loadValidateRow.Controls.Contains(cancelButton)) { loadValidateRow.Controls.Remove(cancelButton); }
                    else { MessageBox.Show("(ControlPanel.setCancelButton) Error: Attempted to remove nonexistent cancel button"); }
                }
                else
                {
                    cancelSource = cancel;
                    if (row == 0) { loadValidateRow.Controls.Add(cancelButton); }
                    else if (row == 2) { exportRow.Controls.Add(cancelButton); }
                    else { MessageBox.Show("(ControlPanel.setCancelButton) Error: Attempted to set cancel button on nonexistent row " + row); }
                }
                return;
            }
            private void cancel_click(object sender, EventArgs args) { cancelSource.Cancel(); }

            private ComboBox getDropMenu(int menu)
            {
                switch (menu)
                {
                    case (0): return dropKernel;
                    case (1): return dropDevice;
                    case (2): return dropMemory;
                    case (3): return dropMapsize;
                    case (4): return dropJobs;
                    case (5): return dropDelay;
                    case (6): return dropRatio;
                    //case (7): return dropNice;
                    default: MessageBox.Show("ControlPanel.getDropMenu(" + menu + "): Error, No corresponding drop menu " + menu); return null;
                }
            }

            public void setDropMenuSelected(int menu, int selection)
            {
                ComboBox cb = getDropMenu(menu);
                if (cb == null) { MessageBox.Show("(ControlPanel.setDropMenuSelected(" + menu + ", " + selection + ")) Error: getDropMenu(" + menu + ") returned null"); return; }
                else if (cb.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)delegate () { setDropMenuSelected(menu, selection); });
                }
                else if (selection >= 0 && selection < cb.Items.Count) { cb.SelectedIndex = selection; }
                else { MessageBox.Show("(ControlPanel.setDropMenuSelected(" + menu + ", " + selection + ")) Error: seletion index out of range"); }
            }

            public void saveTempIndices()
            {
                tempKernelIndex = dropKernel.SelectedIndex;
                tempDeviceIndex = dropDevice.SelectedIndex;
                tempMemoryIndex = dropMemory.SelectedIndex;
                tempMapsizeIndex = dropMapsize.SelectedIndex;
                tempJobsIndex = dropJobs.SelectedIndex;
                tempDelayIndex = dropDelay.SelectedIndex;
                tempRatioIndex = dropRatio.SelectedIndex;
                //tempNiceIndex = dropNice.SelectedIndex;
                tempRadioIndex = radioIndex;
                tempAutoChecked = autoCheck.Checked;
            }

            public void restoreTempIndices()
            {
                dropKernel.SelectedIndex = tempKernelIndex;
                dropDevice.SelectedIndex = tempDeviceIndex;
                dropMemory.SelectedIndex = tempMemoryIndex;
                dropMapsize.SelectedIndex = tempMapsizeIndex;
                dropJobs.SelectedIndex = tempJobsIndex;
                dropDelay.SelectedIndex = tempDelayIndex;
                dropRatio.SelectedIndex = tempRatioIndex;
                //dropNice.SelectedIndex = tempNiceIndex;
                radioIndex = tempRadioIndex;
                autoCheck.Checked = tempAutoChecked;
            }

            public string makePivotCronyKey1(int rindex, int iter)
            {
                return
                (
                    (rindex == 0 ? iter : currentKernelIndex) + "_" +
                    (rindex == 1 ? iter : currentDeviceIndex) + "_" +
                    (rindex == 2 ? iter : currentMemoryIndex)
                );
            }

            public string makePivotCronyKey2(int rindex, int iter)
            {
                return
                (
                  (rindex == 3 ? iter : currentMapsizeIndex) + "_" +
                  (rindex == 4 ? iter : currentJobsIndex) + "_" +
                  (rindex == 5 ? iter : currentDelayIndex) + "_" +
                  (rindex == 6 ? iter : currentRatioIndex) //+ "_" +
                  //(rindex == 7 ? iter : currentNiceIndex)
                );
            }

            public void importSingleBenches_click(object sender, EventArgs args)
            {
                int before = 0;
                if (deleteSelectedButton.Enabled) before += 1;
                if (averageSelectedButton.Enabled) before += 4;
                setManualButtonsEnabled(false, 0);
                using (OpenFileDialog fd = new OpenFileDialog())
                {
                   if (importStandaloneDirectory != null) { fd.InitialDirectory = importStandaloneDirectory; }
                   else { fd.InitialDirectory = Environment.SpecialFolder.Desktop.ToString(); }
                   fd.Title = "Select benchmark(s) to import and average";
                   fd.Filter = "xml files (*.xml)|*.xml";
                   fd.Multiselect = true;
                   if (fd.ShowDialog() == DialogResult.OK)
                   {
                        parser.importSingle(fd.FileNames);
                   }
                }
                setManualButtonsEnabled(true, before);
            }

            public void setManualButtonsEnabled(bool t1, int i) //, bool t2)
            {
                manualCheck.Enabled = t1;
                exportManualButton.Enabled = t1;
                //importManualAverageButton.Enabled = t;
                importManualSingleButton.Enabled = t1;
                deleteSelectedButton.Enabled = (t1 && i > 0);
                averageSelectedButton.Enabled = (t1 && i > 2);
            }
        }

        private Dictionary<string, XmlDocument> xmlFiles;
        private Dictionary<string, BenchSiblings> allBenchSiblings;
        private Dictionary<string, BenchPivot> allBenchPivots;
        private Chart theChart;
        private FlowLayoutPanel flowPanel;
        public ControlPanel controlPanel;
        private BenchPivot theBenchPivot;
        private string[] kernelFilenameStrings = { "Fedora23_native", "Fedora23_Xen", "Windows10_native", "Windows10_Xen" };
        private string[] deviceFilenameStrings = { "chatham", "NANDSSD", "RAMDISK" };
        private int[] physMemValues = { 256, 512, 1024, 2048, 4096, 8192, 16384 };
        private int totalValidated = 0, failedValidation = 0;
        private string replaceDir;
        private BenchPivot manualPivot = null;

        public PmGraph()
        {
            Point originPoint = new Point(0, 0);
            this.MinimumSize = new Size(800, 600);
            this.MaximumSize = new Size(Screen.GetWorkingArea(originPoint).Width, Screen.GetWorkingArea(originPoint).Height);
            this.Resize += new EventHandler(resize_event);
            flowPanel = new FlowLayoutPanel();
            flowPanel.Location = new Point(0, 0);
            flowPanel.Width = this.Width;
            flowPanel.Height = this.Height;
            flowPanel.FlowDirection = FlowDirection.LeftToRight;
            flowPanel.Controls.AddRange(new Control[] { controlPanel = new ControlPanel(this) });
            //controlPanel.radioDevice.Checked = true;
            this.Controls.AddRange(new Control[] { flowPanel });
            xmlFiles = new Dictionary<string, XmlDocument>();
            allBenchSiblings = new Dictionary<string, BenchSiblings>();
            allBenchPivots = new Dictionary<string, BenchPivot>();
        }

        private void resize_event(object sender, EventArgs args)
        {
            flowPanel.Width = this.Width; flowPanel.Height = this.Height;
            if (flowPanel.Controls.Contains(theChart)) { updateChart(); } //conditional prevents updates during asyncronous loops
        }

        public void dropSelectionChanged_action(object o, EventArgs args) { dropSelectionChanged(controlPanel.autoCheck); }

        public void showFullChanged_action(object o, EventArgs args)
        {
            //MessageBox.Show("PmGraph.showFullChanged_action: details checkbox is " + (controlPanel.fullCheck.Checked ? "now" : "no longer") + " checked.");
            theBenchPivot.refreshFull(controlPanel.fullCheck);
            dropSelectionChanged(controlPanel.autoCheck);
        }

        public void dropSelectionChanged(CheckBox t)
        {
            if (t.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate () { dropSelectionChanged(t); });
            }
            else
            {
                controlPanel.exportButton.Enabled = false;
                if (t.Checked) { getResults(false); }
            }
        }

        public static XmlNode safeSelectSingleNode(XmlNode where, string xpath)
        {
            try { return where.SelectSingleNode(xpath); }
            catch (System.Xml.XPath.XPathException x) { MessageBox.Show("XPath error:\n" + x.ToString()); return null; }
            catch (NullReferenceException x)
            {
                MessageBox.Show("node selection null reference exception:\n" + x.ToString());
                return null;
            }
            catch (OutOfMemoryException x)
            {
                MessageBox.Show("Out of memory. " + x.ToString() + "You should probably just quit.");
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.RegisterForFullGCNotification(10, 10);
                //some kind of notification here
                while (true)
                {
                    if (GC.WaitForFullGCComplete() == GCNotificationStatus.Succeeded) { break; }
                    Thread.Sleep(500);
                }
                return safeSelectSingleNode(where, xpath);
            }
        }

        public static long safeParseSingleNodeLong(XmlNode where, string xpath)
        {
            if (where == null) { MessageBox.Show("(safeParseSingleNodeLong) Error: received null input node"); return 0; }
            long i = 0;
            try
            {
                if (safeSelectSingleNode(where, xpath) == null) { MessageBox.Show("(safeParseSingleNodeLong) xpath " + xpath + " returned a null reference"); }
                i = long.Parse(safeSelectSingleNode(where, xpath).InnerText);
            }
            catch (NullReferenceException x) { MessageBox.Show("Exception parsing integer from node inner text:\n" + x.ToString()); }
            catch (OverflowException x) { MessageBox.Show("(safeParseSingleNodeLong) Overflow exception at node " + xpath + ":\n" + x.ToString()); }
            return i;
        }

        public static int safeParseSingleNodeInt(XmlNode where, string xpath) //atoi(node.selectSingleNode(xpath)), with exception handling
        {
            if (where == null) { MessageBox.Show("(safeParseSingleNodeInt) Error: received null input node"); return 0; }
            int i = 0;
            try
            {

                if (safeSelectSingleNode(where, xpath) == null)
                {
                    MessageBox.Show("safeParseSingleNodeInt xpath " + xpath + " returned a null reference on node " + where.Name);
                }
                i = int.Parse(safeSelectSingleNode(where, xpath).InnerText);
            }
            catch (NullReferenceException x) { MessageBox.Show("Exception parsing integer from node inner text:\n" + x.ToString()); }
            catch (OverflowException x)
            {
                MessageBox.Show("(safeParseSingleNodeInt) Overflow exception at node " + xpath + ", with long value " + long.Parse(safeSelectSingleNode(where, xpath).InnerText) + ":\n" + x.ToString());
            }
            catch (FormatException x) { MessageBox.Show("(safeParseSingleNodeInt) Format exception at node " + where.Name + ", XPath " + xpath + ":\n" + x.ToString()); }
            return i;
        }

        public static double safeParseSingleNodeDouble(XmlNode where, string xpath)
        {
            if (safeSelectSingleNode(where, xpath) == null) { MessageBox.Show("(safeParseSingleNodeDouble) Null"); }
            if (safeSelectSingleNode(where, xpath).InnerText == null) { MessageBox.Show("SafeParseSingleNodeDouble Null inner text"); }
            try { return double.Parse(safeSelectSingleNode(where, xpath).InnerText); } //throwing null exceptions because node selection is causing it to run out of memory
            catch (NullReferenceException x)
            {
                MessageBox.Show("(safeParseSingleNodeDouble(XmlNode, " + xpath + ") Null reference exception:\n" + x.ToString());
                return 0;
            }
            catch (OutOfMemoryException) { return safeParseSingleNodeDouble(where, xpath); }
        }

        private void loadActionHandler(int i) { controlPanel.loadAutomaticButton.Text = i.ToString(); }
        private void garbageWaitHandler(int i)
        {
            if (i <= 0) { this.Text = "pmbench XML parser"; }
            else { this.Text = "Waiting for the garbage man (" + i + "s)"; }
        }

        public async void loadXmlFiles_click(object sender, EventArgs args)
        {
            controlPanel.loadAutomaticButton.Enabled = false;
            controlPanel.setControlsEnabled(false, false, false);
            string folderPath = null;
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                try { folderDialog.RootFolder = Environment.SpecialFolder.DesktopDirectory; }
                catch (Exception x)
                {
                    MessageBox.Show("Exception setting folder dialog root:\n" + x.ToString());
                    controlPanel.setControlsEnabled(true, false, false);
                    controlPanel.loadAutomaticButton.Enabled = true;
                    return;
                }
                folderDialog.ShowNewFolderButton = false;
                folderDialog.Description = "Select folder containing results XML files";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    folderPath = folderDialog.SelectedPath;
                    controlPanel.setManualButtonsEnabled(false, 0); //disableManual();
                }
                else
                {
                    controlPanel.setControlsEnabled(true, false, true);
                    controlPanel.loadAutomaticButton.Enabled = true;
                    return;
                }
            }
            System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;

            using (CancellationTokenSource cancelSource = new CancellationTokenSource())
            {
                controlPanel.saveTempIndices();
                Progress<int> loadProgress = new Progress<int>(loadActionHandler);
                controlPanel.setCancelButton(cancelSource, 0);
                int added = await Task.Factory.StartNew(() => loadXmlLoop(folderPath, loadProgress, cancelSource.Token), TaskCreationOptions.LongRunning);
                controlPanel.setCancelButton(null, 0);
                controlPanel.restoreTempIndices();
                System.Windows.Forms.Cursor.Current = Cursors.Default;
                if (added == 0) { MessageBox.Show("Results files not found"); controlPanel.loadAutomaticButton.Enabled = true; return; }
                else if (added == -1) { controlPanel.loadAutomaticButton.Text = "Load XML"; controlPanel.loadAutomaticButton.Enabled = true; }
                else { controlPanel.setControlsEnabled(true, true, true); controlPanel.exportButton.Enabled = false; }
            }
        }

        private int loadXmlLoop(string folderPath, IProgress<int> progress, CancellationToken token)
        {
            int added = 0;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 7; k++)
                    {
                        if (token.IsCancellationRequested) { MessageBox.Show("Cancelled with " + added + " files loaded"); return -1; }
                        //attempt to load the file
                        string path = folderPath + "\\" + "results_" + kernelFilenameStrings[i] + "_" + deviceFilenameStrings[j] + "_" + physMemValues[k] + "_final.xml";
                        XmlDocument doc = new XmlDocument();
                        try { doc.Load(path); }
                        catch (FileNotFoundException)
                        {
                            doc = null;
                            continue;
                        }
                        catch (Exception x)
                        {
                            MessageBox.Show("Non-404 exception on " + path + ":\n" + x.ToString());
                            doc = null;
                            continue;
                        }
                        try
                        {
                            xmlFiles.Add(i + "_" + j + "_" + k, doc);
                            added++;
                            progress.Report(added);
                        }
                        catch (ArgumentNullException x) { MessageBox.Show("(loadXmlLoop) Null argument exception:\n" + x.ToString()); return added; }
                        catch (ArgumentException x)
                        {
	    // Jisoo: uncommented the followin two lines
                            MessageBox.Show("Exception adding key " + i + "_" + j + "_" + k + " to dictionary:\n" + x.ToString());
                            return added;
                        }
                    }
                }
            }
            return added;
        }

        public async void validate_click(object sender, EventArgs args)
        {
            controlPanel.saveTempIndices();
            controlPanel.radioNone.Select();
            controlPanel.setControlsEnabled(false, false, false);
            controlPanel.autoCheck.Checked = false;

            using (CancellationTokenSource cancelSource = new CancellationTokenSource())
            {
                Progress<int> cancelProgress = new Progress<int>(garbageWaitHandler);
                controlPanel.setCancelButton(cancelSource, 2);
                System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;
                string dirtybenches = await Task.Factory.StartNew(() => validateLoop(cancelProgress, cancelSource.Token), TaskCreationOptions.LongRunning);
                controlPanel.setCancelButton(null, 0);
                System.Windows.Forms.Cursor.Current = Cursors.Default;
                if (failedValidation == 0) { MessageBox.Show("All parameter sets are clean, or were fixed automatically"); }
                else if (totalValidated > 0)
                {
                    Clipboard.SetText(dirtybenches);
                    MessageBox.Show(failedValidation + " of " + totalValidated + " parameter sets have integer overflow and need to be re-tested. These parameter sets have beeen copied to the clipboard.");
                }
                dirtybenches = null;
            }
            controlPanel.restoreTempIndices();
            controlPanel.setControlsEnabled(true, true, true);
            if (totalValidated > 0) { controlPanel.verifyButton.Enabled = false; }
        }

        private string validateLoop(IProgress<int> progress, CancellationToken token)
        {
            string dirtybenches = "";
            Dictionary<string, string> rewrites = new Dictionary<string, string>();
            BenchSiblings bs = null;
            for (int j = 0; j < controlPanel.dropKernel.Items.Count; j++) //0
            {
                if (j == 3) { continue; }
                controlPanel.setDropMenuSelected(0, j);
                for (int k = 0; k < controlPanel.dropDevice.Items.Count; k++) //1
                {
                    controlPanel.setDropMenuSelected(1, k);
                    for (int m = 1; m < controlPanel.dropMemory.Items.Count; m++) //2
                    {
                        controlPanel.setDropMenuSelected(2, m);
                        if (getDocFromKey(controlPanel.getKey1FromDropdowns()) == null) { continue; }
                        for (int n = m; n < controlPanel.dropMapsize.Items.Count; n++) //3
                        {
                            controlPanel.setDropMenuSelected(3, n);
                            for (int p = 0; p < controlPanel.dropJobs.Items.Count; p++) //4
                            {
                                controlPanel.setDropMenuSelected(4, p);
                                for (int q = 0; q < controlPanel.dropDelay.Items.Count; q++) //5
                                {
                                    controlPanel.setDropMenuSelected(5, q);
                                    for (int r = 0; r < controlPanel.dropRatio.Items.Count; r++) //6
                                    {
                                        controlPanel.setDropMenuSelected(6, r);
                                        //for (int s = 0; s < controlPanel.dropNice.Items.Count; s++) //7
                                        //{
                                            if (token.IsCancellationRequested)
                                            {
                                                MessageBox.Show("Canceled with " + ((int)(totalValidated - failedValidation)).ToString() + " valid and " + failedValidation + " invalid benchmarks");
                                                totalValidated = 0;
                                                failedValidation = 0;
                                                return "";
                                            }
                                            //controlPanel.setDropMenuSelected(7, s);
                                            bs = getBenchSiblingsObjectFromKeyAndKey(controlPanel.getKey1FromDropdowns(), controlPanel.getKey2FromDropdowns());
                                            if (bs == null) { continue; }
                                            totalValidated++;
                                            /*if (false)
                                            {
                                                failedValidation++;
                                                dirtybenches += bs.benchParams.printReadableParams() + "\n";
                                            }*/
                                            bs = null;
                                        //} // nice
                                    } // ratio
                                } // delay
                            } // jobs
                        } // map size
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    } // memory
                } // device
            } // OS
            return dirtybenches;
        }

        private void autoCsvDumpHandler(object sender, EventArgs args) //handle progress for auto export loop
        {

        }

        public async void autoCsvDump_click(object sender, EventArgs args) //user clicked the auto export button, iterate and export all possible combinations pivot CSVs
        {
            removeChart();
            controlPanel.saveTempIndices();
            controlPanel.setControlsEnabled(false, false, false);
            controlPanel.autoCheck.Checked = false;

            string folderPath;
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog()) //get output directory
            {
                folderDialog.ShowNewFolderButton = true;
                folderDialog.Description = "Select folder to mass export CSV files";
                DialogResult result = folderDialog.ShowDialog();
                if (result == DialogResult.OK) { folderPath = folderDialog.SelectedPath; }
                else
                {
                    controlPanel.exportButton.Enabled = true;
                    controlPanel.autoExportButton.Enabled = true;
                    return;
                }
            }

            int files;
            using (CancellationTokenSource cancelSource = new CancellationTokenSource()) //loop preparation and execution
            {
                controlPanel.saveTempIndices();
                Progress<int> autoCsvDumpProgress = new Progress<int>();
                controlPanel.setCancelButton(cancelSource, 2);
                System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;
                files = await Task.Factory.StartNew(() => autoCsvDumpLoop(folderPath, autoCsvDumpProgress, cancelSource.Token), TaskCreationOptions.LongRunning);
                controlPanel.setCancelButton(null, 2);
                controlPanel.restoreTempIndices();
                System.Windows.Forms.Cursor.Current = Cursors.Default;
                MessageBox.Show("Wrote " + files + " files to " + folderPath);
                controlPanel.restoreTempIndices();
                controlPanel.setControlsEnabled(true, true, true);
            }
        }

        private int autoCsvDumpLoop(string folderPath, IProgress<int> progress, CancellationToken token)
        {
            int files = 0;
            for (int i = 0; i < 9; i++) //8
            {
                controlPanel.setRadioIndex(i);
                for (int j = 0; j < controlPanel.dropKernel.Items.Count; j++) //0
                {
                    controlPanel.setDropMenuSelected(0, j);
                    for (int k = 0; k < controlPanel.dropDevice.Items.Count; k++) //1
                    {
                        controlPanel.setDropMenuSelected(1, k);
                        for (int m = 1; m < controlPanel.dropMemory.Items.Count; m++) //2
                        {
                            controlPanel.setDropMenuSelected(2, m);
                            if (i > 2 && getDocFromKey(controlPanel.getKey1FromDropdowns()) == null) { continue; }
                            for (int n = m; n < controlPanel.dropMapsize.Items.Count; n++) //3
                            {
                                controlPanel.setDropMenuSelected(3, n);
                                for (int p = 0; p < controlPanel.dropJobs.Items.Count; p++) //4
                                {
                                    controlPanel.setDropMenuSelected(4, p);
                                    for (int q = 0; q < controlPanel.dropDelay.Items.Count; q++) //5
                                    {
                                        controlPanel.setDropMenuSelected(5, q);
                                        for (int r = 0; r < controlPanel.dropRatio.Items.Count; r++) //6
                                        {
                                            controlPanel.setDropMenuSelected(6, r);
                                            //for (int s = 0; s < controlPanel.dropNice.Items.Count; s++) //7
                                            //{
                                                if (token.IsCancellationRequested) { return files; }
                                                //controlPanel.setDropMenuSelected(7, s);
                                                if (getResults(false))
                                                {
                                                    if (!theBenchPivot.dumped && theBenchPivot.cronies.Count > 1)
                                                    {
                                                        files += dumpPivotCsv(folderPath);
                                                        theBenchPivot.dumped = true;
                                                    }
                                                }
                                           // }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            MessageBox.Show("autoCsvDumpLoop: Exiting");
            return files;
        }

        private XmlDocument getDocFromKey(string key)
        {
            try { XmlDocument doc = xmlFiles[key]; return doc; }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public void getAverageRoundForManualPivot()
        {
            BenchSiblings bs = getBenchSiblingsObjectFromKeyAndKey(controlPanel.getKey1FromDropdowns(), controlPanel.getKey2FromDropdowns());
            if (bs == null) { MessageBox.Show("Error"); return; }
            addSeriesAverageToManualPivot(bs);
        }

        private XmlNode getBenchSiblingsNodeFromDocAndKey(XmlDocument doc, string key2)
        {
            char[] delimiter = { '_' };
            string[] key_split = key2.Split(delimiter);
            try //select the node with the user-provided parameters
            {
                return safeSelectSingleNode(doc, controlPanel.getNodeSelectionPathFromKey2(key2));
            }
            catch (System.Xml.XPath.XPathException x) { MessageBox.Show("getBenchesFromDocAndKey: Malformed XPath\n" + x.ToString()); return null; }
        }

        private BenchPivot makePivot(BenchSiblings bs, int ri)
        {
            if (controlPanel.radioIndex == 8) { return (new BenchPivot(bs.benchParams, ri, bs.Trials, this)); }
            else
            {
                BenchSiblings b;
                List<BenchRound> cronies = new List<BenchRound>();
                for (int i = 0; i < controlPanel.getPivotVariableCount(ri); i++)
                {
                    b = getBenchSiblingsObjectFromKeyAndKey(controlPanel.makePivotCronyKey1(ri, i), controlPanel.makePivotCronyKey2(ri, i));
                    if (b != null) { cronies.Add(b.getAverageRound()); }
                }
                b = null;
                return (new BenchPivot(bs.benchParams, ri, cronies, this));
            }
        }

        private BenchSiblings getBenchSiblingsObjectFromKeyAndKey(string key1, string key2)
        {
            BenchSiblings bs = null;
            try { bs = allBenchSiblings[key1 + "_" + key2]; }
            catch (KeyNotFoundException)
            {
                XmlDocument doc = getDocFromKey(key1);
                if (doc == null) { return null; }
                else
                {
                    XmlNode sn = getBenchSiblingsNodeFromDocAndKey(doc, key2);
                    if (sn == null) { return null; }
                    ParamSet bp = ParamSet.makeParamsFromKeysAndNode(key1, key2, getParamsNodeFromSeriesNode(sn));
                    if (bp == null)
                    { return null; }
                    string[] splat = splitString(key1, '_');
                    bp.operatingSystem = controlPanel.getKey1Value(0, int.Parse(splat[0]));
                    bp.swapDevice = controlPanel.getKey1Value(1, int.Parse(splat[1]));
                    bp.valueMemory = int.Parse(controlPanel.getKey1Value(2, int.Parse(splat[2])));
                    splat = null;
                    bs = new BenchSiblings(sn, doc, bp);
                    sn = null;
                }
                allBenchSiblings[key1 + "_" + key2] = bs; //removing this causes duplicates
            }
            return bs;
        }

        private BenchPivot getBenchPivotFromKey(string s)
        {
            BenchPivot bp = null;
            try { bp = allBenchPivots[s]; }
            catch (KeyNotFoundException) { return null; }
            return bp;
        }

        private BenchPivot getBenchPivotFromDropdowns()
        {
            BenchPivot bp = getBenchPivotFromKey(controlPanel.getPivotKeys(false));
            if (bp == null)
            {
                BenchSiblings bs = getBenchSiblingsObjectFromKeyAndKey(controlPanel.getKey1FromDropdowns(), controlPanel.getKey2FromDropdowns());
                if (bs == null) { return null; }
                bp = makePivot(bs, controlPanel.radioIndex);
                allBenchPivots[controlPanel.getPivotKeys(false)] = bp;
            }
            return bp;
        }

        public void getResults_click(object o, EventArgs args) { getResults(true); }
        private bool getResults(bool click)
        {
            controlPanel.exportButton.Enabled = false;
            if (click)
            {
                if (controlPanel.dropMemory.SelectedIndex > controlPanel.dropMapsize.SelectedIndex)
                {
                    MessageBox.Show("Physical memory should not exceed map size.");
                    return false;
                }
            }
            if (!flowPanel.InvokeRequired) { removeChart(); }
            if (controlPanel.manualCheck.Checked == false)
            {
                if (getDocFromKey(controlPanel.getKey1FromDropdowns()) == null) { return false; }
                controlPanel.updateSavedIndices();
                theBenchPivot = getBenchPivotFromDropdowns();
            }
            if (theBenchPivot == null) return false;
            updateChart();
            return true;
        }

        private void updateChart()
        {
            try
            {
                if (flowPanel == null) { MessageBox.Show("flow panel is null!"); return; }
                if (flowPanel.InvokeRequired) { return; }
                if (theBenchPivot == null) { MessageBox.Show("Bench pivot is null!"); return; }
                theChart = theBenchPivot.getPreparedChart(getChartWidth(), getChartHeight(), controlPanel.fullCheck);
                theChart.Location = new Point(controlPanel.Width + controlPanel.Margin.Left + 17, 44);
                flowPanel.Controls.Add(theChart);
                if (controlPanel.manualCheck.Enabled == false) controlPanel.exportButton.Enabled = true;
            }
            catch (NullReferenceException x) { MessageBox.Show("null reference:"+x.ToString()); return; }
        }
        private int getChartWidth() { return flowPanel.Width - controlPanel.Width - controlPanel.Margin.Left - 17; }
        private int getChartHeight() { return flowPanel.Height - 44; }

        private bool removeChart()
        {
            if (flowPanel == null) { return false; }
            if (flowPanel.InvokeRequired) { MessageBox.Show("Illegal cross-thread chart removal attempted"); return false; }
            if (flowPanel.Controls.Contains(theChart))
            {
                flowPanel.Controls.Remove(theChart);
                theChart = null;
                return true;
            }
            return false;
        }

        public void exportCsv_click(object sender, EventArgs args) { if (getResults(false)) { dumpPivotCsv(null); } }
        private int dumpPivotCsv(string path) { return theBenchPivot.dumpPivotCsv(path); } //make sure to check getResults when calling this or theBenchPivot may be null
        private static string[] splitString(string s, char c) { char[] delimiter = { c }; return (s.Split(delimiter)); }
        public static XmlNode getParamsNodeFromSeriesNode(XmlNode node) { return safeSelectSingleNode(node, "test_round/pmbenchmark/report/signature/params"); }

        /*private string replaceDirtyBenchSiblings(BenchSiblings dirty) //replace a dirty bench series with a clean one from a separate document, then write the document again
        {
            if (dirty == null) { MessageBox.Show("replaceDirtyBenchSiblings(" + dirty.ToString() + ") The BenchSiblings object you sent is null"); return null; }
            string path = replaceDir + "\\" + "errors_" + kernelFilenameStrings[dirty.benchParams.indexKernel] + "_" + deviceFilenameStrings[dirty.benchParams.indexDevice] + "_" + physMemValues[dirty.benchParams.indexMemory] + ".xml"; //once again filename is hard coded to what I had handy

            XmlDocument cleanDoc = new XmlDocument();
            try { cleanDoc.Load(path); }
            catch (Exception x)
            {
                MessageBox.Show("replaceDirtyBenchSiblings: Exception loading XML document at " + path + ":\n" + x.ToString());
                cleanDoc = null;
                return null;
            }
            XmlNode cleanNode = safeSelectSingleNode(cleanDoc, dirty.benchParams.getXPath());
            if (cleanNode == null) { MessageBox.Show("replaceDirtyBenchSiblings Error: XML node at XPath " + dirty.benchParams.getXPath() + " came back null"); return null; }
            dirty.seriesNode.InnerXml = cleanNode.InnerXml;
            return replaceDir + "\\" + "results_" + kernelFilenameStrings[dirty.benchParams.indexKernel] + "_" + deviceFilenameStrings[dirty.benchParams.indexDevice] + "_" + physMemValues[dirty.benchParams.indexMemory] + "_fixed.xml";
        }*/

        private bool writeXmlDocument(XmlDocument what, string where)
        {
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using (MemoryStream ms = new MemoryStream())
            {
                using (XmlTextWriter xw = new XmlTextWriter(ms, System.Text.Encoding.Unicode))
                {
                    xw.IndentChar = '\t';
                    xw.Formatting = Formatting.Indented;
                    what.WriteContentTo(xw);
                    xw.Flush();
                    ms.Flush();
                    ms.Position = 0;
                    using (StreamReader sr = new StreamReader(ms))
                    {
                        try
                        {
                            File.WriteAllText(where, sr.ReadToEnd());
                            return true;
                        }
                        catch (IOException x) { MessageBox.Show("(writeXmlDocument) Error writing file to " + where + "\n" + x.ToString()); return false; }
                    }
                }
            }
        }

        /*private int averageCounter = 0;
        public void importAverage(string[] filenames) //a last minute hack job to top all hack jobs
        {
            XmlDocument doc = new XmlDocument();
            XmlNode fakeSeries = doc.CreateNode(XmlNodeType.Element, "test_nice", doc.NamespaceURI);
            doc.AppendChild(fakeSeries);
            ParamSet bp = new ParamSet();
            try
            {
                for (int j = 0; j < filenames.Length; j++)
                {
                    XmlDocument tempdoc = new XmlDocument();
                    tempdoc.Load(filenames[j]);
                    XmlNode fakeRound = doc.CreateNode(XmlNodeType.Element, "test_round", doc.NamespaceURI);
                    XmlAttribute iter = doc.CreateAttribute("iter");
                    iter.Value = (j + 1).ToString();
                    fakeRound.Attributes.Append(iter);
                    fakeRound.AppendChild(doc.ImportNode(safeSelectSingleNode(tempdoc, "pmbenchmark"), true));
                    fakeSeries.AppendChild(fakeRound);
                    bp.setParamsFromNode(getParamsNodeFromSeriesNode(fakeSeries));
                    bp.operatingSystem = safeSelectSingleNode(tempdoc, "pmbenchmark/report/signature/pmbench_info/version_options").InnerText;
                }
            }
            catch (FileNotFoundException x)
            {
                MessageBox.Show("importAverage:\n" + x.ToString());
                return;
            }
            catch (ArgumentException x)
            {
                MessageBox.Show("importAverage: ArgumentException\n" + x.ToString());
                return;
            }
            BenchSiblings bs = new BenchSiblings(fakeSeries, doc, bp);
            bs.averageRound.customName = "Average " + averageCounter++; //bs.averageRound.getTotalSamples().ToString();
            addSeriesAverageToManualP_ivot(bs);
        }*/

        private string registerXmlDocName(string s, XmlDocument doc, bool allowrename)
        {
            int trynum = 0;
            string t = s;
            while (true)
            {
                try
                {
                    xmlFiles.Add(t, doc);
                    break;
                }
                catch (ArgumentException x)
                {
                    if (allowrename) t = s + trynum++;
                }
            }
            //MessageBox.Show("registered " + t);
            return t;
        }

        public void removeDeadXmlDoc(string docname)
        {
            try
            {
                XmlDocument doc = xmlFiles[docname];
                xmlFiles.Remove(docname);
                //MessageBox.Show("Successfully removed dead XML doc " + docname);
            }
            catch (KeyNotFoundException)
            {
                MessageBox.Show("removeDeadXmlDoc error: attempted to delete nonexistent XmlDocument " + docname);
            }
        }

        public void renameXmlDoc(string oldname, string newname)
        {

        }

        public void importSingle(string[] filenames)
        {
            for (int j = 0; j < filenames.Length; j++)
            {
                XmlDocument doc = new XmlDocument();
                XmlNode fakeSeries = doc.CreateNode(XmlNodeType.Element, "test_nice", doc.NamespaceURI);
                doc.AppendChild(fakeSeries);
                ParamSet bp = new ParamSet();
                XmlDocument tempdoc = new XmlDocument();
                tempdoc.Load(filenames[j]);
                XmlNode fakeRound = doc.CreateNode(XmlNodeType.Element, "test_round", doc.NamespaceURI);
                XmlAttribute iter = doc.CreateAttribute("iter");
                iter.Value = ("1").ToString();
                fakeRound.Attributes.Append(iter);
                fakeRound.AppendChild(doc.ImportNode(safeSelectSingleNode(tempdoc, "pmbenchmark"), true));
                fakeSeries.AppendChild(fakeRound);
                bp.setParamsFromNode(getParamsNodeFromSeriesNode(fakeSeries));
                bp.operatingSystem = safeSelectSingleNode(tempdoc, "pmbenchmark/report/signature/pmbench_info/version_options").InnerText;
                BenchSiblings bs = new BenchSiblings(fakeSeries, doc, bp);
                string[] splat1 = splitString(filenames[j], '\\');
                string[] splat2 = splitString(splat1[splat1.Length - 1], '.');
                bs.averageRound.customName = registerXmlDocName(splat2[0], doc, true);
                addSeriesAverageToManualPivot(bs);
            }
        }

        public void addSeriesAverageToManualPivot(BenchSiblings addme)
        {
            if (manualPivot == null)
            {
                List<BenchRound> br = new List<BenchRound>();
                br.Add(addme.averageRound);
                manualPivot = new BenchPivot(addme.benchParams, 9, br, this);
            }
            else manualPivot.cronies.Add(addme.averageRound);
            graphManual();
        }

        public void graphManual()
        {
            if (manualPivot == null)
            {
                MessageBox.Show("manual pivot is null, returning");
                return;
            }
            removeChart();
            if (manualPivot == null) { MessageBox.Show("PmGraph.graphManual: manualPivot is null after PmGraph.removeChart()."); }
            if (flowPanel.Controls.Contains(theChart)) MessageBox.Show("graphManual redundant flowpanel chart removal error"); //flowPanel.Controls.Remove(theChart);
            if (manualPivot == null) { MessageBox.Show("PmGraph.graphManual: manualPivot is null after flowpanel chart removal."); }

            manualPivot.destroyPivotChart(); //forgot why this is necessary
            theChart = manualPivot.getPreparedChart(getChartWidth(), getChartHeight(), controlPanel.fullCheck);
            if (theChart == null) { MessageBox.Show("PmGraph.graphManual: chart was assigned as null."); }
            try
            {
                theChart.Location = new Point(controlPanel.Width + controlPanel.Margin.Left + 17, 44);
                flowPanel.Controls.Add(theChart);
                theBenchPivot = manualPivot;
                controlPanel.setControlsEnabled(false, false, true);
            }
            catch (NullReferenceException x)
            {
                MessageBox.Show("PmGraph.graphManual: Null reference exception.\n" + "theChart is " + (theChart == null ? "INDEED" : "NOT") + " null.\n" + x.ToString());
            }
        }

        private int averageCounter = 0;
        private void averageSelectedButton_click(object sender, EventArgs e)
        {
            BenchSiblings bs = theBenchPivot.averageSelected(averageCounter++);
            addSeriesAverageToManualPivot(bs);
        }

        private bool nag = true;
        private void deleteSelectedButton_click(object sender, EventArgs e)
        {
            theBenchPivot.markDeleteSelected(nag);
            theBenchPivot.deleteSelected(nag);
        }

        public void exportCsvManual(object sender, EventArgs e)
        {
            manualPivot.dumpPivotCsv(null);
        }

        public void updateSelectionButtons(int i)
        {
            controlPanel.setManualButtonsEnabled(controlPanel.manualCheck.Checked && controlPanel.manualCheck.Enabled, i);
        }

        /*public string getTextBoxContents(bool clear)
        {
            string s = controlPanel.getTextBoxContents(clear);
            if (s.Equals(""))
            {

            }
        }*/

        public bool doesPivotHaveSelections()
        {
            return (theBenchPivot == null ? false : theBenchPivot.getChartSelectionCount() > 0);
        }

        /*public string setFieldText(string s)
        {
            if (s == null) controlPanel.nameAveragesField.Text = "";
            else controlPanel.nameAveragesField.Text += s;
            return controlPanel.nameAveragesField.Text;
        }*/

        public void selectAll_click(object sender, EventArgs e)
        {
            if (this.theBenchPivot == null) return;
            this.theBenchPivot.selectAll();
        }
    }
}
//delete single on right click context menu
