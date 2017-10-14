#include <libxml/encoding.h>
#include <libxml/parser.h>
#include <libxml/tree.h>
#include <libxml/xmlmemory.h>
#include <libxml/xmlstring.h>
#include <libxml/xmlwriter.h>
#include <libxml/xpath.h>


#include "system.h"
#include "pmbench.h"

xmlDoc* xdoc = NULL;
static const char xmloutput_version[] = "0.1";
xmlNodePtr pmbenchmarknode = NULL;
xmlNodePtr postrunnode = NULL;
xmlNodePtr sysmeminfonode = NULL;


static xmlChar tempbuf[57];

/* catch libxml header change */
#if LIBXML_VERSION < 20904 
  #define XFMT_TYPE  BAD_CAST
#else
  #define XFMT_TYPE 
#endif
xmlChar* floatToXmlChar(double f)
{
    if (xmlStrPrintf(tempbuf, 57, XFMT_TYPE"%0.4f", f) == -1) {
	printf("floatToXmlChar(%f): Error\n", f);
	return BAD_CAST "Error";
    }
    return tempbuf;
}

xmlChar* signedIntToXmlChar(int64_t i)
{
    if (xmlStrPrintf(tempbuf, 57, XFMT_TYPE"%"PRId64, i) == -1) {
	printf("signedIntToXmlChar(%"PRId64"): Error\n", i);
	return BAD_CAST "Error";
    }
    return tempbuf;
}

xmlChar* unsignedIntToXmlChar(uint64_t i)
{
    if (xmlStrPrintf(tempbuf, 57, XFMT_TYPE"%"PRIu64, i) == -1) {
	printf("unsignedIntToXmlChar(%"PRIu64"): Error\n", i);
	return BAD_CAST "Error";
    }
    return tempbuf;
}

xmlChar* byteToXmlChar(uint8_t b)
{
    if (xmlStrPrintf(tempbuf, 57, XFMT_TYPE"%02x", b) == -1) {
	printf("byteToXmlChar(%u): Error\n", b);
	return BAD_CAST "Error";
    }
    return tempbuf;
}

static
xmlNodePtr makeParamsNode(const parameters *p, xmlNodePtr signaturenode)
{
    xmlNodePtr paramsnode = xmlNewChild(signaturenode, NULL, BAD_CAST "params", NULL);
    xmlNewChild(paramsnode, NULL, BAD_CAST "duration", unsignedIntToXmlChar(p->duration_sec));
    xmlNewChild(paramsnode, NULL, BAD_CAST "mapsize", unsignedIntToXmlChar(p->mapsize_mib));
    xmlNewChild(paramsnode, NULL, BAD_CAST "setsize", unsignedIntToXmlChar(p->setsize_mib));
    xmlNewChild(paramsnode, NULL, BAD_CAST "initialize", signedIntToXmlChar(p->init_garbage));
    xmlNewChild(paramsnode, NULL, BAD_CAST "shape", floatToXmlChar(p->shape));
    xmlNewChild(paramsnode, NULL, BAD_CAST "delay", unsignedIntToXmlChar(p->delay));
    xmlNewChild(paramsnode, NULL, BAD_CAST "quiet", unsignedIntToXmlChar(p->quiet));
    xmlNewChild(paramsnode, NULL, BAD_CAST "cold", unsignedIntToXmlChar(p->cold));
#ifdef PMB_THREAD
    xmlNewChild(paramsnode, NULL, BAD_CAST "jobs", unsignedIntToXmlChar(p->jobs));
#endif
    xmlNewChild(paramsnode, NULL, BAD_CAST "offset", signedIntToXmlChar(p->offset));
    xmlNewChild(paramsnode, NULL, BAD_CAST "ratio", signedIntToXmlChar(p->ratio));
    if (p->pattern && p->pattern->name) { xmlNewChild(paramsnode, NULL, BAD_CAST "pattern", BAD_CAST p->pattern->name); }
    if (p->access && p->access->name) { xmlNewChild(paramsnode, NULL, BAD_CAST "access", BAD_CAST p->access->name); }
    if (p->tsops && p->tsops->name) { xmlNewChild(paramsnode, NULL, BAD_CAST "tsops", BAD_CAST p->tsops->name); }
#ifdef XALLOC
    xmlNewChild(paramsnode, NULL, BAD_CAST "xalloc_mib", unsignedIntToXmlChar(p->xalloc_mib));
    xmlNewChild(paramsnode, NULL, BAD_CAST "xalloc_path", BAD_CAST p->xalloc_path);
#endif
    return paramsnode;
}

static
xmlNodePtr makeResultNode(xmlNodePtr reportnode)
{
    xmlNodePtr resultnode = xmlNewChild(reportnode, NULL, BAD_CAST "result", NULL);
    int i;

    for (i = 0; i < params.jobs; i++) {
	struct bench_result* presult = get_result(i);
	float pure_lat = mean_us(bench) - mean_us(numgen);
	//result_thread[thread_num]
	xmlNodePtr rn = xmlNewChild(resultnode, NULL, BAD_CAST "result_thread", NULL);
	xmlNewProp(rn, BAD_CAST "thread_num", unsignedIntToXmlChar(i+1));
	//result_netavg
	xmlNodePtr netavgnode = xmlNewChild(rn, NULL, BAD_CAST "result_netavg", NULL);
	//netavg_us
	xmlNewChild(netavgnode, NULL, BAD_CAST "netavg_us", floatToXmlChar(pure_lat));
	//netavg_clk
	xmlNewChild(netavgnode, NULL, BAD_CAST "netavg_clk", signedIntToXmlChar(us_to_clk(pure_lat)));
	//result_details
	xmlNodePtr detailsnode = xmlNewChild(rn, NULL, BAD_CAST "result_details", NULL);
	//details_latency
	xmlNodePtr latencynode = xmlNewChild(detailsnode, NULL, BAD_CAST "details_latency", NULL);
	//latency_us
	xmlNewChild(latencynode, NULL, BAD_CAST "latency_us", floatToXmlChar(mean_us(bench))); //%0.4f
	//latency_clk
	xmlNewChild(latencynode, NULL, BAD_CAST "latency_clk", signedIntToXmlChar((int)mean_clk(bench)));
	//details_samples
	xmlNewChild(detailsnode, NULL, BAD_CAST "details_samples", unsignedIntToXmlChar(presult->total_bench_count)); //%"PRIu64"
	//details_overhead
	xmlNodePtr overheadnode = xmlNewChild(detailsnode, NULL, BAD_CAST "details_overhead", NULL);
	//overhead_us
	xmlNewChild(overheadnode, NULL, BAD_CAST "overhead_us", floatToXmlChar(mean_us(numgen))); //%0.4f
	//overhead_clk
	xmlNewChild(overheadnode, NULL, BAD_CAST "overhead_clk", signedIntToXmlChar((int)mean_clk(numgen)));
	//details_total
	xmlNewChild(detailsnode, NULL, BAD_CAST "details_total", unsignedIntToXmlChar(presult->total_numgen_count)); //%"PRIu64"
	if (!params.cold) {
	    //warmup_details
	    xmlNodePtr warmupnode = xmlNewChild(rn, NULL, BAD_CAST "warmup_details", NULL);
	    //warmup_latency
	    xmlNodePtr wlatencynode = xmlNewChild(warmupnode, NULL, BAD_CAST "warmup_latency", NULL);
	    //warmup_us
	    xmlNewChild(wlatencynode, NULL, BAD_CAST "warmup_us", floatToXmlChar(mean_us(warmup))); //%0.4f
	    //warmup_clk
	    xmlNewChild(wlatencynode, NULL, BAD_CAST "warmup_clk", signedIntToXmlChar((int)mean_clk(warmup)));
	    //warmup_total
	    xmlNewChild(warmupnode, NULL, BAD_CAST "warmup_total", unsignedIntToXmlChar(presult->total_warmup_count)); //%"PRIu64"
	}
    }
    return resultnode;
}

static
xmlNodePtr makeBucketNode(xmlNodePtr node, int i, int lo, int hi, uint64_t sum_count)
{
    xmlNodePtr bucketnode = xmlNewChild(node, NULL, BAD_CAST "histo_bucket", NULL);
    xmlNewProp(bucketnode, BAD_CAST "index", signedIntToXmlChar(i));
    xmlNodePtr intervalnode = xmlNewChild(bucketnode, NULL, BAD_CAST "bucket_interval", NULL);
    xmlNewChild(intervalnode, NULL, BAD_CAST "interval_lo", signedIntToXmlChar(lo));
    xmlNewChild(intervalnode, NULL, BAD_CAST "interval_hi", signedIntToXmlChar(hi));
    xmlNewChild(bucketnode, NULL, BAD_CAST "sum_count", unsignedIntToXmlChar(sum_count));
    return bucketnode;
}

static
xmlNodePtr makeHistogramNode(char *buf, int writehisto, xmlNodePtr node)
{
    xmlNodePtr histonode = xmlNewChild(node, NULL, BAD_CAST "histogram", NULL);
    if (writehisto) xmlNewProp(histonode, BAD_CAST "type", BAD_CAST "write");
    else xmlNewProp(histonode, BAD_CAST "type", BAD_CAST "read");
    //extremely low latencies
    uint64_t *bucket0 = get_histogram_bucket(buf, writehisto, 0);
    makeBucketNode(histonode, 0, 0, 8, bucket0[0]); 
    //buckets 1-15
    int i;
    uint64_t sum_count;
    for (i = 1; i < 16; ++i) {
	uint64_t *bucket = get_histogram_bucket(buf, writehisto, i);
	sum_count = 0;
	int j;
	for (j = 0; j < 16; ++j) { sum_count += bucket[j]; }
	xmlNodePtr bucketnode = makeBucketNode(histonode, i, i+7, i+8, sum_count); 
	xmlNodePtr hexesnode = xmlNewChild(bucketnode, NULL, BAD_CAST "bucket_hexes", NULL);

	for (j = 0; j < 16; j++) {
	    xmlNodePtr hexnode = xmlNewChild(hexesnode, NULL, BAD_CAST "hex", unsignedIntToXmlChar(bucket[j]));
	    xmlNewProp(hexnode, BAD_CAST "index", signedIntToXmlChar(j));
	}
    }
    //large latencies (bucket 0)
    for (i = 0; i < 7; ++i) { makeBucketNode(histonode, 0, i+23, i+24, bucket0[8+i]); }
    //largest latencies (2^30:32)
    makeBucketNode(histonode, 0, 30, 32, bucket0[15]);
    return histonode;
}

static
xmlNodePtr makeSysMemItemNode(xmlNodePtr sysmemnode, char * name, const sys_mem_item *info)
{
    xmlNodePtr sysmemitemnode = xmlNewChild(sysmemnode, NULL, BAD_CAST "sys_mem_item", NULL);
    xmlNewProp(sysmemitemnode, BAD_CAST "name", BAD_CAST name);
    xmlNodePtr node = xmlNewChild(sysmemitemnode, NULL, BAD_CAST "mem_item_info", NULL);
#ifdef _WIN32
    xmlNewChild(node, NULL, BAD_CAST "AvailPhys", signedIntToXmlChar(sys_stat_mem_get(info, 0)));
    xmlNewChild(node, NULL, BAD_CAST "dwMemoryLoad", signedIntToXmlChar(sys_stat_mem_get(info, 1)));
    xmlNewChild(node, NULL, BAD_CAST "TotalPageFile", signedIntToXmlChar(sys_stat_mem_get(info, 2)));
    xmlNewChild(node, NULL, BAD_CAST "AvailPageFile", signedIntToXmlChar(sys_stat_mem_get(info, 3)));
    xmlNewChild(node, NULL, BAD_CAST "AvailVirtual", signedIntToXmlChar(sys_stat_mem_get(info, 4)));
#else
    xmlNewChild(node, NULL, BAD_CAST "free_kib", signedIntToXmlChar(sys_stat_mem_get(info, 0)));
    xmlNewChild(node, NULL, BAD_CAST "buffer_kib", signedIntToXmlChar(sys_stat_mem_get(info, 1)));
    xmlNewChild(node, NULL, BAD_CAST "cache_kib", signedIntToXmlChar(sys_stat_mem_get(info, 2)));
    xmlNewChild(node, NULL, BAD_CAST "active_kib", signedIntToXmlChar(sys_stat_mem_get(info, 3)));
    xmlNewChild(node, NULL, BAD_CAST "inactive_kib", signedIntToXmlChar(sys_stat_mem_get(info, 4)));
    xmlNewChild(node, NULL, BAD_CAST "pgpgin", signedIntToXmlChar(sys_stat_mem_get(info, 5)));
    xmlNewChild(node, NULL, BAD_CAST "pgpgout", signedIntToXmlChar(sys_stat_mem_get(info, 6)));
    xmlNewChild(node, NULL, BAD_CAST "pswpin", signedIntToXmlChar(sys_stat_mem_get(info, 7)));
    xmlNewChild(node, NULL, BAD_CAST "pswpout", signedIntToXmlChar(sys_stat_mem_get(info, 8)));
    xmlNewChild(node, NULL, BAD_CAST "pgmajfault", signedIntToXmlChar(sys_stat_mem_get(info, 9)));
#endif
    return sysmemitemnode;
}

static
xmlNodePtr makeMemItemDeltaNode(xmlNodePtr memitemnode, const sys_mem_item *before, const sys_mem_item *after)
{
    xmlNodePtr deltanode = xmlNewChild(memitemnode, NULL, BAD_CAST "mem_item_delta", NULL);
#ifdef _WIN32
    xmlNewChild(deltanode, NULL, BAD_CAST "AvailPhys", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 0)));
    xmlNewChild(deltanode, NULL, BAD_CAST "dwMemoryLoad", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 1)));
    xmlNewChild(deltanode, NULL, BAD_CAST "TotalPageFile", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 2)));
    xmlNewChild(deltanode, NULL, BAD_CAST "AvailPageFile", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 3)));
    xmlNewChild(deltanode, NULL, BAD_CAST "AvailVirtual", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 4)));
#else
    xmlNewChild(deltanode, NULL, BAD_CAST "free_kib", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 0)));
    xmlNewChild(deltanode, NULL, BAD_CAST "buffer_kib", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 1)));
    xmlNewChild(deltanode, NULL, BAD_CAST "cache_kib", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 2)));
    xmlNewChild(deltanode, NULL, BAD_CAST "active_kib", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 3)));
    xmlNewChild(deltanode, NULL, BAD_CAST "inactive_kib", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 4)));
    xmlNewChild(deltanode, NULL, BAD_CAST "pgpgin", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 5)));
    xmlNewChild(deltanode, NULL, BAD_CAST "pgpgout", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 6)));
    xmlNewChild(deltanode, NULL, BAD_CAST "pswpin", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 7)));
    xmlNewChild(deltanode, NULL, BAD_CAST "pswpout", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 8)));
    xmlNewChild(deltanode, NULL, BAD_CAST "pgmajfault", signedIntToXmlChar(sys_stat_mem_get_delta(before, after, 9)));
#endif
    return deltanode;
}

static
xmlNodePtr makeOsInfoNode(xmlNodePtr signaturenode, char *hostname)
{
    xmlNodePtr osinfonode = xmlNewChild(signaturenode, NULL, BAD_CAST "os_info", NULL);
    xmlNewChild(osinfonode, NULL, BAD_CAST "hostname", BAD_CAST hostname);
    xmlNodePtr versioninfonode = xmlNewChild(osinfonode, NULL, BAD_CAST "version_info", NULL);
#ifdef _WIN32
    xmlNewChild(versioninfonode, NULL, BAD_CAST "os_name", BAD_CAST "windows");
    xmlNewChild(versioninfonode, NULL, BAD_CAST "arch", BAD_CAST sys_get_cpu_arch());
    xmlNewChild(versioninfonode, NULL, BAD_CAST "maj", signedIntToXmlChar(sys_get_os_version_value(1)));
    xmlNewChild(versioninfonode, NULL, BAD_CAST "min", signedIntToXmlChar(sys_get_os_version_value(2)));
    xmlNewChild(versioninfonode, NULL, BAD_CAST "build", signedIntToXmlChar(sys_get_os_version_value(3)));
#else
    xmlNewChild(versioninfonode, NULL, BAD_CAST "os_name", BAD_CAST sys_get_os_version_string(0));
    xmlNewChild(versioninfonode, NULL, BAD_CAST "arch", BAD_CAST sys_get_cpu_arch());
    xmlNewChild(versioninfonode, NULL, BAD_CAST "release", BAD_CAST sys_get_os_version_string(4)); 
#endif
    return osinfonode;
}

xmlNodePtr makeTimeInfoNode(xmlNodePtr signaturenode)
{
    xmlNodePtr timeinfonode = xmlNewChild(signaturenode, NULL, BAD_CAST "time_info", NULL);
#ifdef _WIN32
    xmlNewChild(timeinfonode, NULL, BAD_CAST "date", BAD_CAST sys_get_time_info_string(9));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "time", BAD_CAST sys_get_time_info_string(10));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "year", BAD_CAST sys_get_time_info_string(5));
#else
    xmlNewChild(timeinfonode, NULL, BAD_CAST "wday", signedIntToXmlChar(sys_get_time_info_value(6)));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "year", signedIntToXmlChar(sys_get_time_info_value(5)));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "mon", signedIntToXmlChar(sys_get_time_info_value(4)));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "mday", signedIntToXmlChar(sys_get_time_info_value(3)));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "hour", signedIntToXmlChar(sys_get_time_info_value(2)));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "min", signedIntToXmlChar(sys_get_time_info_value(1)));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "sec", signedIntToXmlChar(sys_get_time_info_value(0)));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "yday", signedIntToXmlChar(sys_get_time_info_value(7)));
    xmlNewChild(timeinfonode, NULL, BAD_CAST "isdst", signedIntToXmlChar(sys_get_time_info_value(8)));
#endif
    return timeinfonode;
}

static
void makeSysMemInfoNode(xmlNodePtr reportnode)
{
    sysmeminfonode = xmlNewChild(reportnode, NULL, BAD_CAST "sys_mem_info", NULL);
    if (params.cold) {
	xmlNodePtr prerunnode = makeSysMemItemNode(sysmeminfonode, "pre-run", &mem_info_before_warmup);	//1

	if (mem_info_middle_run.recorded) {
	    makeMemItemDeltaNode(prerunnode, &mem_info_before_warmup, &mem_info_middle_run);	//2
	    xmlNodePtr midrunnode = makeSysMemItemNode(sysmeminfonode, "mid-run", &mem_info_middle_run);//3
	    makeMemItemDeltaNode(midrunnode, &mem_info_middle_run, &mem_info_after_run);	//4
	} else {
	    makeMemItemDeltaNode(prerunnode, &mem_info_before_warmup, &mem_info_after_run);	//5
	}
    } else {
	xmlNodePtr prewarmupnode = makeSysMemItemNode(sysmeminfonode, "pre-warmup", &mem_info_before_warmup);		//6
	makeMemItemDeltaNode(prewarmupnode, &mem_info_before_warmup, &mem_info_before_run);	//7
	xmlNodePtr prerunnode = makeSysMemItemNode(sysmeminfonode, "pre-run", &mem_info_before_run);//8

	if (mem_info_middle_run.recorded) {
	    makeMemItemDeltaNode(prerunnode, &mem_info_before_run, &mem_info_middle_run);	//9
	    xmlNodePtr midrunnode = makeSysMemItemNode(sysmeminfonode, "mid-run", &mem_info_middle_run);//10
	    makeMemItemDeltaNode(midrunnode, &mem_info_middle_run, &mem_info_after_run); //11
	} else {
	    makeMemItemDeltaNode(prerunnode, &mem_info_before_run, &mem_info_after_run); //12
	}
    }
    postrunnode = makeSysMemItemNode(sysmeminfonode, "post-run", &mem_info_after_run);	//13
}

static
xmlNodePtr makeCacheInfoNode(xmlNodePtr machineinfonode, int cachetypes)
{
    xmlNodePtr cacheinfonode = xmlNewChild(machineinfonode, NULL, BAD_CAST "cache_info", NULL);
    if (cachetypes == 0) {
	xmlNewProp(cacheinfonode, BAD_CAST "deterministic", BAD_CAST "0");
    } else {
	xmlNewProp(cacheinfonode, BAD_CAST "deterministic", BAD_CAST "1");
	int j;
	for (j = 0; j < cachetypes; j++) {
	    xmlNodePtr cachenode = xmlNewChild(cacheinfonode, NULL, BAD_CAST "cache", NULL);
	    xmlNewProp(cachenode, BAD_CAST "type", BAD_CAST get_cache_type(j));
	    xmlNewChild(cachenode, NULL, BAD_CAST "level", signedIntToXmlChar(get_cache_info(j, 4)));
	    xmlNewChild(cachenode, NULL, BAD_CAST "capacity", signedIntToXmlChar(get_cache_info(j, 5))); 
	    xmlNewChild(cachenode, NULL, BAD_CAST "sets", signedIntToXmlChar(get_cache_info(j, 0))); 
	    xmlNewChild(cachenode, NULL, BAD_CAST "linesize", signedIntToXmlChar(get_cache_info(j, 1))); 
	    xmlNewChild(cachenode, NULL, BAD_CAST "partitions", signedIntToXmlChar(get_cache_info(j, 2))); 
	    xmlNewChild(cachenode, NULL, BAD_CAST "ways", signedIntToXmlChar(get_cache_info(j, 3)));
	}
    }
    return cacheinfonode;
}

static
xmlNodePtr makeTlbInfoNode(xmlNodePtr machineinfonode, int tlblength)
{
    xmlNodePtr tlbinfonode = xmlNewChild(machineinfonode, NULL, BAD_CAST "tlb_info", NULL);
    int j;
    for (j = 0; j < tlblength; j++) {
	xmlNodePtr tlbitemnode = xmlNewChild(tlbinfonode, NULL, BAD_CAST "tlb_item", NULL);
	xmlNewProp(tlbitemnode, BAD_CAST "index", unsignedIntToXmlChar(j));
	xmlNewProp(tlbitemnode, BAD_CAST "id", byteToXmlChar(get_tlb_info(j)));
	//do something to get the actual contents here... will take a while to xmlify
    }
    return tlbinfonode;
}

/*
 * this must be called after print_con_report() to populate some global variables (XXX: fix this)
 */
void
__attribute__((cold))
print_xml_report(char* buf, const parameters* p, int is_interrupted)
{
    //signature
    //pmbench_info
    xmlNodePtr reportnode = NULL, signaturenode = NULL;

    LIBXML_TEST_VERSION;
    xdoc = xmlNewDoc(BAD_CAST "1.0");
    pmbenchmarknode = xmlNewDocNode(xdoc, NULL, BAD_CAST "pmbenchmark", NULL);

    xmlDocSetRootElement(xdoc, pmbenchmarknode);
    reportnode = xmlNewChild(pmbenchmarknode, NULL, BAD_CAST "report", NULL);
    signaturenode = xmlNewChild(reportnode, NULL, BAD_CAST "signature", NULL);
    xmlNodePtr pmbenchinfonode = xmlNewChild(signaturenode, NULL, BAD_CAST "pmbench_info", NULL);
    xmlNewChild(pmbenchinfonode, NULL, BAD_CAST "version_number", BAD_CAST PMBENCH_VERSION_STR);
    xmlNewChild(pmbenchinfonode, NULL, BAD_CAST "version_options", BAD_CAST COMPILE_OPT_TAGS);
    xmlNewChild(pmbenchinfonode, NULL, BAD_CAST "version_xmloutput", BAD_CAST xmloutput_version);

    //os_info
    char *hostname = sys_get_hostname(); 

    //version_info

    makeOsInfoNode(signaturenode, hostname);
    //time_info
    if (gl_goodtime) makeTimeInfoNode(signaturenode);
    
    //uuid
    char * uuid = sys_get_uuid();
    xmlNewChild(signaturenode, NULL, BAD_CAST "uuid", BAD_CAST uuid);
    
    //params
    makeParamsNode(p, signaturenode);

    if (is_interrupted) {
	xmlNewChild(signaturenode, NULL, BAD_CAST "interrupted", BAD_CAST "1");
    }

    //machine_info
    xmlNodePtr machineinfonode = NULL;
    char modelstr[48];
    if (__cpuid_obtain_brand_string(modelstr)) {
	machineinfonode = xmlNewChild(reportnode, NULL, BAD_CAST "machine_info", NULL);
	xmlNewChild(machineinfonode, NULL, BAD_CAST "modelname", BAD_CAST modelstr);
    } else {
	machineinfonode = xmlNewChild(reportnode, NULL, BAD_CAST "machine_info", NULL);
	xmlNewChild(machineinfonode, NULL, BAD_CAST "modelname", BAD_CAST "unsupported"); 
    }

    //freq_khz
    xmlNewChild(machineinfonode, NULL, BAD_CAST "freq_khz", unsignedIntToXmlChar(freq_khz));

    //tlb_info
    makeTlbInfoNode(machineinfonode, gl_tlb_info_buf_len);

    //cache_info
    makeCacheInfoNode(machineinfonode, gl_det_cache_info_len);

    //result
    makeResultNode(reportnode);
    
    //statistics
    if (p->access == &histogram_access) {
	xmlNodePtr statisticsnode = xmlNewChild(reportnode, NULL, BAD_CAST "statistics", NULL);
	if (p->ratio > 0) {
	    makeHistogramNode(buf, 0, statisticsnode);
	}
	if (p->ratio < 100) {
	    makeHistogramNode(buf, 1, statisticsnode);
	}
    }

    //sys_mem_info
    makeSysMemInfoNode(reportnode);
}

void 
__attribute__((cold))
print_xml_report_post_unmap(const char* path)
{
    makeMemItemDeltaNode(postrunnode, &mem_info_after_run, &mem_info_after_unmap);
    makeSysMemItemNode(sysmeminfonode, "post-unmap", &mem_info_after_unmap);
    xmlKeepBlanksDefault(0); //try moving this up
    xmlSaveFormatFileEnc(path, xdoc, "utf-8", 1);
}




// original temporarilly saved prior to refactoring
#if 0
void
__attribute__((cold))
print_report(char* buf, const parameters* p)
{
    printf("\n------------- Benchmark signature -------------\n");
    //signature
    //pmbench_info
    sys_print_pmbench_info();
#ifdef PMB_XML
    xmlNodePtr reportnode = NULL, signaturenode = NULL;
    if (p->xml) {
	LIBXML_TEST_VERSION;
	xdoc = xmlNewDoc(BAD_CAST "1.0");
	pmbenchmarknode = xmlNewDocNode(xdoc, NULL, BAD_CAST "pmbenchmark", NULL);

	xmlDocSetRootElement(xdoc, pmbenchmarknode);
	reportnode = xmlNewChild(pmbenchmarknode, NULL, BAD_CAST "report", NULL);
	signaturenode = xmlNewChild(reportnode, NULL, BAD_CAST "signature", NULL);
	xmlNodePtr pmbenchinfonode = xmlNewChild(signaturenode, NULL, BAD_CAST "pmbench_info", NULL);
	xmlNewChild(pmbenchinfonode, NULL, BAD_CAST "version_number", BAD_CAST pmbench_version);
	xmlNewChild(pmbenchinfonode, NULL, BAD_CAST "version_options", BAD_CAST COMPILE_OPT_TAGS);
	xmlNewChild(pmbenchinfonode, NULL, BAD_CAST "version_xmloutput", BAD_CAST xmloutput_version);
    }

    //os_info
    char *hostname = 
#endif
	sys_print_hostname(); //you must call this for any of the below get functions to work
    //version_info
#ifdef _WIN32
    printf("OS/kernel type : Windows %s version %d.%d (build %d)\n", 
	    sys_get_cpu_arch(), 
	    sys_get_os_version_value(1), 
	    sys_get_os_version_value(2), 
	    sys_get_os_version_value(3));
#else
    printf("OS/kernel type : %s %s %s\n", 
	    sys_get_os_version_string(0), 
	    sys_get_os_version_string(4), 
	    sys_get_cpu_arch());
#endif
#ifdef PMB_XML
    if (p->xml) makeOsInfoNode(signaturenode, hostname);
    //time_info
    int goodtime = 
#endif
	sys_print_time_info();
#ifdef PMB_XML
    if (p->xml && goodtime) makeTimeInfoNode(signaturenode);
    //uuid
    char * uuid = 
#endif
	sys_print_uuid();
#ifdef PMB_XML
    if (p->xml) xmlNewChild(signaturenode, NULL, BAD_CAST "uuid", BAD_CAST uuid);
#endif
    //params
    printf("Parameters used:\n");
    print_params(p);
#ifdef PMB_XML
    if (p->xml) { makeParamsNode(p, signaturenode); }
#endif
    if (control.interrupted)
    {
#ifdef PMB_XML
	if (p->xml) { xmlNewChild(signaturenode, NULL, BAD_CAST "interrupted", BAD_CAST "1"); }
#endif
	printf("\nNote: User interruption ended the benchmark earlier than scheduled.\n");
    }

    //machine_info
#ifdef PMB_XML
    xmlNodePtr machineinfonode = NULL;
#endif
    printf("\n------------- Machine information -------------\n");
    {
	char modelstr[48];
	if (__cpuid_obtain_model_string(modelstr)) 
	{
#ifdef PMB_XML
	    if (p->xml) 
	    { 
		machineinfonode = xmlNewChild(reportnode, NULL, BAD_CAST "machine_info", NULL);
		xmlNewChild(machineinfonode, NULL, BAD_CAST "modelname", BAD_CAST modelstr);
	    }
#endif
	    printf("CPU model name: %s\n", modelstr);
	} 
	else 
	{
#ifdef PMB_XML
	    if (p->xml)
	    { 
		machineinfonode = xmlNewChild(reportnode, NULL, BAD_CAST "machine_info", NULL);
		xmlNewChild(machineinfonode, NULL, BAD_CAST "modelname", BAD_CAST "unsupported"); 
	    }
#endif
	    printf("CPU model string unsupported.\n");
	}
    }
    //freq_khz
    printf("rdtsc/perfc frequency: %u K cycles per second\n", freq_khz);   		
#ifdef PMB_XML
    if (p->xml) { xmlNewChild(machineinfonode, NULL, BAD_CAST "freq_khz", unsignedIntToXmlChar(freq_khz)); }
#endif

    //tlb_info
    printf(" -- TLB info --\n");
#ifdef PMB_XML
    int tlblength = 
#endif
	print_tlb_info();
#ifdef PMB_XML
    if (p->xml) { makeTlbInfoNode(machineinfonode, tlblength); }
#endif
    //cache_info
    printf(" -- Cache info --\n");
#ifdef PMB_XML
    int cachetypes = 
#endif
	print_cache_info(tlblength);
#ifdef PMB_XML
    if (p->xml) makeCacheInfoNode(machineinfonode, cachetypes);
#endif
    //result
    printf("\n----------- Average access latency ------------\n");
    print_result();
#ifdef PMB_XML
    if (p->xml) makeResultNode(reportnode);
#endif
    //statistics
    printf("\n----------------- Statistics ------------------\n");
    p->access->report(buf, p->ratio);
#ifdef PMB_XML
    if (p->xml && p->access == &histogram_access) {
	xmlNodePtr statisticsnode = xmlNewChild(reportnode, NULL, BAD_CAST "statistics", NULL);
	if (p->ratio > 0) 	{ makeHistogramNode(buf, 0, statisticsnode); }
	if (p->ratio < 100) 	{ makeHistogramNode(buf, 1, statisticsnode); }
    }
#endif
    //sys_mem_info
    printf("\n---------- System memory information ----------\n");
    sys_stat_mem_print_header();
    if (params.cold) {
	printf("pre-run   :"); sys_stat_mem_print(&mem_info_before_warmup);//1
	printf("  (delta) :");
	if (mem_info_middle_run.recorded) {
	    sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_middle_run);//2
	    printf("mid-run   :"); sys_stat_mem_print(&mem_info_middle_run);	//3
	    printf("  (delta) :"); sys_stat_mem_print_delta(&mem_info_middle_run, &mem_info_after_run);	//4
	}
	else sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_after_run); //5
    } else {
	printf("pre-warmup:"); sys_stat_mem_print(&mem_info_before_warmup);//6
	printf("  (delta) :"); sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_before_run);	//7
	printf("pre-run   :"); sys_stat_mem_print(&mem_info_before_run);	//8
	printf("  (delta) :");
	if (mem_info_middle_run.recorded) {
	    sys_stat_mem_print_delta(&mem_info_before_run, &mem_info_middle_run);//9
	    printf("mid-run   :"); sys_stat_mem_print(&mem_info_middle_run);	//10
	    printf("  (delta) :"); sys_stat_mem_print_delta(&mem_info_middle_run, &mem_info_after_run);	//11
	}
	else sys_stat_mem_print_delta(&mem_info_before_run, &mem_info_after_run);//12
    }
    printf("post-run  :"); sys_stat_mem_print(&mem_info_after_run);		//13
#ifdef PMB_XML		
    if (p->xml) makeSysMemInfoNode(reportnode);
#endif
}

#endif
