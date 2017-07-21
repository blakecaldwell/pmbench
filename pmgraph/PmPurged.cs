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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml;

namespace PmGraphNS
{


/////////////////////////////////////////////////////////////
// leftover code 
public class ControlPanel_old : FlowLayoutPanel
{
    public Button resultsButton, loadAutomaticButton, exportButtonOld;
    public Button autoExportButton, verifyButton, cancelButton;

    public CheckBox autoCheck, fullCheck;

    public ComboBox dropKernel, dropDevice, dropMemory, dropMapsize;
    public ComboBox dropJobs, dropDelay, dropRatio;

    enum Knobs { Kernel = 0, Device, Memory, Mapsize, Jobs, Delay, Ratio }; 
    private RadioButton[] radioSelect;
    private ComboBox[] comboSelect;

    public RadioButton radioSelected, radioNone; // used by PmGraph
    public int radioIndex;	    // used by PmGraph

    private bool tempAutoChecked;

    public CheckBox manualCheck;
    private Label manualLabel;
    //public TextBox nameAveragesField;
    
//  private Button importManualAverageButton;

    public int currentKernelIndex, currentDeviceIndex, currentMemoryIndex, currentMapsizeIndex, currentJobsIndex, currentDelayIndex, currentRatioIndex;
    private int tempKernelIndex, tempDeviceIndex, tempMemoryIndex, tempMapsizeIndex, tempJobsIndex, tempDelayIndex, tempRatioIndex, tempRadioIndex;

    private FlowLayoutPanel loadValidateRow = null, exportRow = null;
    private Panel selectPanel;

    private CancellationTokenSource cancelSource;

    // below members are duplicated to pass compile
    private PmGraph pmgraph;
    private static Size radioSize = new Size(90, 18);

    public ControlPanel_old(PmGraph p)
    {
	pmgraph = p;

	this.Width = 78;
	this.Height = pmgraph.Height;

	Padding checkPadding = new Padding(6, 8, 0, 0);
	Padding checkLabelPadding = new Padding(0, 8, 0, 0); ;

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
	fullCheck.CheckStateChanged += new EventHandler(pmgraph.showFullChanged_action);
	Label labelNone = new Label();
	labelNone.Text = "No pivot variable";
	radioNone = initRadioButton("None", "test iteration");
	radioNone.Margin = new Padding(0, 0, 0, 0);


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

	// initSelectionControl_old();

	// these need to go
	// old exportButton recovered code
	exportButtonOld = initButton("Expt old", 
		pmgraph.exportCsvAuto_old_click, false);
	autoExportButton = initButton("Auto export",
		pmgraph.autoCsvDump_click, false);

	/*
	this.Controls.Add(exportButtonOld);
	this.Controls.Add(autoExportButton);
	*/

	FlowDirection = FlowDirection.TopDown;

	cancelButton = new Button();
	cancelButton.Text = "Cancel";
	cancelButton.Width = 50;
	cancelButton.Click += new EventHandler(cancel_click);
	cancelButton.Enabled = true;


// JY below left for future reference
//	    this.Controls.AddRange(new Control[]
//	    {
	//initControlRow2(new Control[] { manualLabel, manualCheck }, 130, 20, FlowDirection.RightToLeft, new Padding(4, 0, 0, 0)),

/* JY    initControlRow(new Control[]
	    {
		importManualSingleButton = initButton("Import", importSingleBenches_click, true),
		exportManualButton = initButton("Export", pmgraph.exportCsvManual_click, false)
	    }, 
	    220, 
	    28, 
	    FlowDirection.LeftToRight, 
	    actionButtonPadding
	) ,
*/ //JY
	/*initControlRow(new Control[]
	{
	    importManualAverageButton = initButton("Import & avg", importAverageBenches_click, true)
	}, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),*/
	//nameAveragesField = initTextBox("Enter name"),

/* JY	    initControlRow(new Control[]
	{
	    averageSelectedButton = initButton("Average selected", pmgraph.averageSelectedButton_click, false),
	    deleteSelectedButton = initButton("Delete seleted", pmgraph.deleteSelectedButton_click, false)
	}, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
	initControlRow(new Control[]
	{
	    selectAllButton = initButton("Select all", pmgraph.selectAll_click, false),
	    helpButton = initButton("Instructions", helpButton_click, true)
	    //autoCheck, labelUpdate
	}, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
*/ //JY
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
	    loadAutomaticButton = initButton("Load XML", pmgraph.loadXmlFiles_click, true),
	    verifyButton = initButton("Validate", pmgraph.validate_click, false)
	}, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
	initControlRow(new Control[]
	{
	    resultsButton = initButton("Results", pmgraph.getResults_click, false),
	    //autoCheck, labelUpdate
	}, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),
	exportRow = initControlRow(new Control[]
	{
	    exportButtonOld = initButton("Export CSV", pmgraph.exportCsvAuto_old_click, false),
	    autoExportButton = initButton("Auto export", pmgraph.autoCsvDump_click, false)
	}, 220, 28, FlowDirection.LeftToRight, actionButtonPadding),*/
	/*initControlRow(new Control[]
	{
	    fullCheck, fullLabel
	}, 220, 28, FlowDirection.LeftToRight, actionButtonPadding)*/
//	    });

    }


    private ComboBox initDropMenu(object[] itemNames, int i)
    {
	ComboBox cb = new ComboBox();
	cb.Items.AddRange(itemNames);
	cb.DropDownStyle = ComboBoxStyle.DropDownList;
	cb.SelectedIndex = i;
	cb.SelectedValueChanged += new EventHandler(pmgraph.dropSelectionChanged_action);
	return cb;
    }


    private void initSelectionControl_old()
    {
	// place all selection controls to select Panel
	selectPanel = new Panel();
	selectPanel.BorderStyle = BorderStyle.Fixed3D;
	selectPanel.Size = new Size(78, 180);

	String[] knobStrs = {
	    "Kernel", "Device", "Memory", "Mapsize", 
	    "Jobs", "Delay", "Ratio" };
	String[] labelStrs = {
	    "OS/Kernel", "Swap device", "Phys mem", "Map size",
	    "Threads", "Delay", "RW ratio" };

	radioSelect = new RadioButton[7];
	comboSelect = new ComboBox[7];

	for (int i = 0; i < 7; i++) {
	    radioSelect[i] = initRadioButton(knobStrs[i], labelStrs[i]);
	    comboSelect[i] = initDropMenu(
		new object[] {
		    "None" }, 
		    0);
	    radioSelect[i].Location = new Point(2, i*25 + 3);
	    comboSelect[i].Location = new Point(92, i*25);
	}


	selectPanel.Controls.AddRange(radioSelect);
	selectPanel.Controls.AddRange(comboSelect);
	
	this.Controls.Add(selectPanel);
    }
/*
    private FlowLayoutPanel initControlRow2(Control[] controls, int w, int h, FlowDirection d, Padding p)
    {
	FlowLayoutPanel flp = initControlRow(controls, w, h, d, p);
	return flp;
    }
*/

/* JY
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

*/ //JY

    public void setControlsEnabled(bool t1, bool t2, bool t3)
    {
	return;
	/*
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
	exportButtonOld.Enabled = t2;
	resultsButton.Enabled = t2;
	autoExportButton.Enabled = t2;
	autoCheck.Enabled = t3;
	*/
    }

    private bool dropdownsEnabled = false;
    private bool autoActionButtonsEnabled = false;

    private void manualCheck_click(object sender, EventArgs args)
    {
	if (manualCheck.Checked == true) {
	    //loadAutomaticButton.Enabled = false;
	    //dropdownsEnabled = radioDelay.Enabled;
	    //autoActionButtonsEnabled = autoExportButton.Enabled;
	    setControlsEnabled(false, false, true);
	    //importManualAverageButton.Enabled = true;
	    //exportManualButton.Enabled = true;
	    //importManualSingleButton.Enabled = true;
	    //nameAveragesField.Enabled = true;
	} else {
	    setControlsEnabled(dropdownsEnabled, autoActionButtonsEnabled, autoActionButtonsEnabled);
	    //exportManualButton.Enabled = false;
	    //importManualAverageButton.Enabled = false;
	    //importManualSingleButton.Enabled = false;
	    //nameAveragesField.Enabled = false;
	}
    }

    public void setManualButtonsEnabled_old(bool t1, int i) //, bool t2)
    {
	manualCheck.Enabled = t1;
	//exportManualButton.Enabled = t1;
	//importManualAverageButton.Enabled = t;
	//importManualSingleButton.Enabled = t1;
	//deleteSelectedButton.Enabled = (t1 && i > 0);
	//averageSelectedButton.Enabled = (t1 && i > 2);
    }


    private void cancel_click(object sender, EventArgs args)
    {
	cancelSource.Cancel();
    }

    public void setCancelButton(CancellationTokenSource cancel, int row)
    {
	if (cancel == null) {
	    if (exportRow.Controls.Contains(cancelButton)) {
		exportRow.Controls.Remove(cancelButton);
	    } else if (loadValidateRow.Controls.Contains(cancelButton)) {
		loadValidateRow.Controls.Remove(cancelButton);
	    } else {
		MB.S("(ControlPanel.setCancelButton) Error: Attempted to remove nonexistent cancel button");
	    }
	} else {
	    cancelSource = cancel;
	    if (row == 0) {
		loadValidateRow.Controls.Add(cancelButton);
	    } else if (row == 2) {
		exportRow.Controls.Add(cancelButton);
	    } else {
		MB.S("(ControlPanel.setCancelButton) Error: Attempted to set cancel button on nonexistent row " + row);
	    }
	}
	return;
    }

    private int getRadioIndexFromRadioSelected()
    {
	switch (radioSelected.Name) {
	case "radioKernel":
	    return 0;
	case "radioDevice":
	    return 1;
	case "radioMemory":
	    return 2;
	case "radioMapsize":
	    return 3;
	case "radioJobs":
	    return 4;
	case "radioDelay":
	    return 5;
	case "radioRatio":
	    return 6;
	case "radioNone":
	    return 7;
	default:
	    return 8;
	}
    }

    private void radio_click(object o, EventArgs args)
    {
	RadioButton b = o as RadioButton;

	if (b == null) {
	    MB.S("radio_click: sender is not radio button");
	    return;
	}
	
	if (!b.Checked) return; 

	exportButtonOld.Enabled = false;
	radioSelected = b;
	radioIndex = getRadioIndexFromRadioSelected();
//Console.WriteLine("radioIndex : {0}", radioIndex);
	pmgraph.dropSelectionChanged(autoCheck);
    }

    private static Button initButton(string text, EventHandler e, bool enable)
    {
	Button b = new Button();
	b.Text = text;
	b.Click += new EventHandler(e);
	b.Enabled = enable;
	return b;
    }

    private ComboBox getDropMenu(int menu)
    {
	// for invalid id it will raise IndexOutOfRangeException
	return comboSelect[menu];
    }

    private RadioButton initRadioButton(string name, string text)
    {
	RadioButton rb = new RadioButton();
	rb.Name = "radio" + name;
	rb.Size = radioSize;
	rb.Text = text;
	rb.CheckedChanged += new EventHandler(radio_click);
	return rb;
    }

    public int getPivotVariableCount(int i)
    {
	switch (i) {
	case 0:
	    return dropKernel.Items.Count;
	case 1:
	    return dropDevice.Items.Count;
	case 2:
	    return dropMemory.Items.Count;
	case 3:
	    return dropMapsize.Items.Count;
	case 4:
	    return dropJobs.Items.Count;
	case 5:
	    return dropDelay.Items.Count;
	case 6:
	    return dropRatio.Items.Count;
	case 7:
	    return 0; // dropNice.Items.Count;
	case 8:
	    return 6;
	default:
	    return 0;
	}
    }

    public string getKey1Value(int i, int j)
    {
	switch (i) {
	case 0: 
	    return dropKernel.Items[j].ToString();
	case 1: 
	    return dropDevice.Items[j].ToString();
	case 2: 
	    return dropMemory.Items[j].ToString();
	default: 
	    return "ERROR";
	}
    }

    private int getDropSelectedIndex(ComboBox cb)
    {
	if (cb.InvokeRequired) {
	    return (int)this.Invoke(new Func<int>(() => getDropSelectedIndex(cb))); 
	} else {
	    return cb.SelectedIndex;
	}
    }

    private int getDropSelectedIndex(int i)
    {
	return getDropSelectedIndex(getDropMenu(i));
    }

    private int getDropSelectedValue(ComboBox cb)
    {
	if (cb.InvokeRequired) {
	    return (int)this.Invoke(new Func<int>(() => getDropSelectedIndex(cb)));
	} else {
	    if (cb.Name.Equals(dropKernel.Name) ||
		cb.Name.Equals(dropDevice.Name))
	    { 
		MB.S("(ControlPanel_old.getDropSelectedValue) Error: ComboBox " + cb.Name + " has non-integer member values");
		return -1;
	    }

	    return (int)(cb.SelectedItem);
	}
    }


    public void setDropMenuSelected(int menu, int selection)
    {
	ComboBox cb = getDropMenu(menu);

	if (cb.InvokeRequired) {
	    this.Invoke((MethodInvoker)delegate () {setDropMenuSelected(menu, selection); });
	} else if (selection >= 0 && selection < cb.Items.Count) {
	    cb.SelectedIndex = selection;
	} else {
	    MB.S("setDropMenuSelected(" + menu + ", " + selection + 
		    ") Error: seletion index out of range");
	}
    }

    private string getKeyElementFromDropdowns(int menu)
    {
	ComboBox cb = getDropMenu(menu);

	if (cb.InvokeRequired) {
	    return (string)this.Invoke(new Func<string>(() => getKeyElementFromDropdowns(menu)));
	} else {
	    return cb.SelectedIndex.ToString();
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

    private string getDropdownValueFromIndex(int menu, int index)
    {
	ComboBox cb = getDropMenu(menu);

	if (cb.InvokeRequired) {
	    return (string)this.Invoke(new Func<string>(() => getDropdownValueFromIndex(menu, index)));
	} else {
	    return cb.Items[index].ToString();
	}
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

    private string getPivotKeyElement(int ri, bool saved)
    {
	if (radioIndex == ri) return ("*");

	switch (ri) {
	case 0: 
	    return (saved ? currentKernelIndex.ToString() : getDropSelectedIndex(ri).ToString());
	case 1: 
	    return (saved ? currentDeviceIndex.ToString() : getDropSelectedIndex(ri).ToString());
	case 2: 
	    return (saved ? currentMemoryIndex.ToString() : getDropSelectedIndex(ri).ToString());
	case 3: 
	    return (saved ? currentMapsizeIndex.ToString() : getDropSelectedIndex(ri).ToString());
	case 4: 
	    return (saved ? currentJobsIndex.ToString() : getDropSelectedIndex(ri).ToString());
	case 5: 
	    return (saved ? currentDelayIndex.ToString() : getDropSelectedIndex(ri).ToString());
	case 6: 
	    return (saved ? currentRatioIndex.ToString() : getDropSelectedIndex(ri).ToString());
	//case 7: 
	//  return (saved ? currentNiceIndex.ToString() : getDropSelectedIndex(ri).ToString());
	default: 
	    return ("(getPivotKeyElement(" + ri + ", " + saved.ToString() + ") Error");
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


    private RadioButton getRadioButton(int id)
    {
	// for invalid id it will raise IndexOutOfRangeException
	return radioSelect[id];
    }

    public void setRadioIndex(int i)
    {
	RadioButton current = getRadioButton(radioIndex);
	RadioButton newbutton = getRadioButton(i);

	if (current.InvokeRequired || 
	    newbutton.InvokeRequired) {
	    this.Invoke((MethodInvoker)delegate () { setRadioIndex(i); });
	} else {
	    radioIndex = i;
	    newbutton.Select();
	}
    }
}   



/////////////////////////////////////////////////////////////
// PmGraph partial for storing old code stuffs.. 
// Below either needs update or removal
public partial class PmGraph : System.Windows.Forms.Form
{
    private Dictionary<string, BenchSiblings> allBenchSiblings;
    private Dictionary<string, Harness> allHarnesses;

    private ControlPanel_old controlPanel_old;

    private Harness auto_old;

    private string[] kernelFilenameStrings = { "Fedora23_native", "Fedora23_Xen", "Windows10_native", "Windows10_Xen" };
    private string[] deviceFilenameStrings = { "chatham", "NANDSSD", "RAMDISK" };
    private int[] physMemValues = { 256, 512, 1024, 2048, 4096, 8192, 16384 };

    private int totalValidated = 0, failedValidation = 0;



    public void dropSelectionChanged_action(object o, EventArgs args)
    {
	dropSelectionChanged(controlPanel_old.autoCheck);
    }

    public void showFullChanged_action(object o, EventArgs args)
    {
	//MB.S("PmGraph.showFullChanged_action: details checkbox is " + (controlPanel.fullCheck.Checked ? "now" : "no longer") + " checked.");

//	auto_old.setFull(controlPanel.fullCheck.Checked);
	dropSelectionChanged(controlPanel_old.autoCheck);
    }

    public void dropSelectionChanged(CheckBox t)
    {
	if (t.InvokeRequired) {
	    this.Invoke((MethodInvoker) delegate() 
		    {
			dropSelectionChanged(t);
		    });
	} else {
	    controlPanel_old.exportButtonOld.Enabled = false;
	    if (t.Checked) getResults(false);
	}
    }

    private void loadActionHandler(int i)
    {
	controlPanel_old.loadAutomaticButton.Text = i.ToString();
    }

    private void garbageWaitHandler(int i)
    {
	if (i <= 0) {
	    this.Text = "pmbench XML processor";
	} else {
	    this.Text = "Waiting for the garbage man (" + i + "s)";
	}
    }

    public async void loadXmlFiles_click(object sender, EventArgs args)
    {
	controlPanel_old.loadAutomaticButton.Enabled = false;
	controlPanel_old.setControlsEnabled(false, false, false);
	string folderPath = null;
	using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
	{
	    try {
		folderDialog.RootFolder = Environment.SpecialFolder.DesktopDirectory;
	    }
	    catch (Exception x) {
		MB.S("Exception setting folder dialog root:\n" + x.ToString());
		controlPanel_old.setControlsEnabled(true, false, false);
		controlPanel_old.loadAutomaticButton.Enabled = true;
		return;
	    }

	    folderDialog.ShowNewFolderButton = false;
	    folderDialog.Description = "Select folder containing results XML files";
	    if (folderDialog.ShowDialog() == DialogResult.OK) {
		folderPath = folderDialog.SelectedPath;
		controlPanel_old.setManualButtonsEnabled_old(false, 0); //disableManual();
	    } else {
		controlPanel_old.setControlsEnabled(true, false, true);
		controlPanel_old.loadAutomaticButton.Enabled = true;
		return;
	    }
	}

	System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;

	using (CancellationTokenSource cancelSource = new CancellationTokenSource())
	{
	    controlPanel_old.saveTempIndices();
	    Progress<int> loadProgress = new Progress<int>(loadActionHandler);
	    controlPanel_old.setCancelButton(cancelSource, 0);
	    int added = await Task.Factory.StartNew(() => loadXmlLoop(folderPath, loadProgress, cancelSource.Token), TaskCreationOptions.LongRunning);

	    controlPanel_old.setCancelButton(null, 0);
	    controlPanel_old.restoreTempIndices();
	    System.Windows.Forms.Cursor.Current = Cursors.Default;

	    if (added == 0) {
		MB.S("Results files not found");
		controlPanel_old.loadAutomaticButton.Enabled = true;
		return;
	    } else if (added == -1) {
		controlPanel_old.loadAutomaticButton.Text = "Load XML";
		controlPanel_old.loadAutomaticButton.Enabled = true;
	    } else {
		controlPanel_old.setControlsEnabled(true, true, true);
		controlPanel_old.exportButtonOld.Enabled = false;
	    }
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
		    if (token.IsCancellationRequested) {
			MB.S("Cancelled with " + added + " files loaded");
			return -1;
		    }
		    //attempt to load the file
		    string path = folderPath + "\\" + "results_"
			+ kernelFilenameStrings[i] + "_" 
			+ deviceFilenameStrings[j] + "_"
			+ physMemValues[k] + "_final.xml";

		    XmlDocument doc = new XmlDocument();
		    try {
			doc.Load(path);
		    }
		    catch (FileNotFoundException) {
			doc = null;
			continue;
		    }
		    catch (Exception x) {
			MB.S("Non-404 exception on " + path + ":\n" + x.ToString());
			doc = null;
			continue;
		    }

		    try {
			xmlFiles.Add(i + "_" + j + "_" + k, doc);
			added++;
			progress.Report(added);
		    }
		    catch (ArgumentNullException x) {
			MB.S("(loadXmlLoop) Null argument exception:\n" + x.ToString());
			return added;
		    }
		    catch (ArgumentException x) {
	// Jisoo: uncommented the followin two lines
			MB.S("Exception adding key " + i + "_" + j + 
				"_" + k + " to dictionary:\n" + x.ToString());
			return added;
		    }
		}
	    }
	}
	return added;
    }

    public async void validate_click(object sender, EventArgs args)
    {
	controlPanel_old.saveTempIndices();
	controlPanel_old.radioNone.Select();
	controlPanel_old.setControlsEnabled(false, false, false);
	controlPanel_old.autoCheck.Checked = false;

	using (CancellationTokenSource cancelSource = new CancellationTokenSource())
	{
	    Progress<int> cancelProgress = new Progress<int>(garbageWaitHandler);
	    controlPanel_old.setCancelButton(cancelSource, 2);
	    System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;
	    string dirtybenches = await Task.Factory.StartNew(() => validateLoop(cancelProgress, cancelSource.Token), TaskCreationOptions.LongRunning);
	    controlPanel_old.setCancelButton(null, 0);
	    System.Windows.Forms.Cursor.Current = Cursors.Default;
	    if (failedValidation == 0) {
		MB.S("All parameter sets are clean, or were fixed automatically");
	    } else if (totalValidated > 0) {
		Clipboard.SetText(dirtybenches);
		MB.S(failedValidation + " of " + totalValidated + " parameter sets have integer overflow and need to be re-tested. These parameter sets have beeen copied to the clipboard.");
	    }
	    dirtybenches = null;
	}
	controlPanel_old.restoreTempIndices();
	controlPanel_old.setControlsEnabled(true, true, true);
	if (totalValidated > 0) {
	    controlPanel_old.verifyButton.Enabled = false;
	}
    }

    private string validateLoop(IProgress<int> progress, CancellationToken token)
    {
	string dirtybenches = "";
//       Dictionary<string, string> rewrites = new Dictionary<string, string>();
	BenchSiblings bench = null;
	for (int j = 0; j < controlPanel_old.dropKernel.Items.Count; j++) //0
	{
	    if (j == 3) continue;

	    controlPanel_old.setDropMenuSelected(0, j);
	    for (int k = 0; k < controlPanel_old.dropDevice.Items.Count; k++) //1
	    {
		controlPanel_old.setDropMenuSelected(1, k);
		for (int m = 1; m < controlPanel_old.dropMemory.Items.Count; m++) //2
		{
		    controlPanel_old.setDropMenuSelected(2, m);
		    if (getDocFromKey(controlPanel_old.getKey1FromDropdowns()) == null) { continue; }
		    for (int n = m; n < controlPanel_old.dropMapsize.Items.Count; n++) //3
		    {
			controlPanel_old.setDropMenuSelected(3, n);
			for (int p = 0; p < controlPanel_old.dropJobs.Items.Count; p++) //4
			{
			    controlPanel_old.setDropMenuSelected(4, p);
			    for (int q = 0; q < controlPanel_old.dropDelay.Items.Count; q++) //5
			    {
				controlPanel_old.setDropMenuSelected(5, q);
				for (int r = 0; r < controlPanel_old.dropRatio.Items.Count; r++) //6
				{
				    controlPanel_old.setDropMenuSelected(6, r);
				    //for (int s = 0; s < controlPanel_old.dropNice.Items.Count; s++) //7
				    //{
					if (token.IsCancellationRequested)
					{
					    MB.S("Canceled with " + ((int)(totalValidated - failedValidation)).ToString() + " valid and " + failedValidation + " invalid benchmarks");
					    totalValidated = 0;
					    failedValidation = 0;
					    return "";
					}
					//controlPanel_old.setDropMenuSelected(7, s);
					bench = getBenchSiblingsObjectFromKeyAndKey(controlPanel_old.getKey1FromDropdowns(), controlPanel_old.getKey2FromDropdowns());
					if (bench == null) { continue; }
					totalValidated++;
					/*if (false)
					{
					    failedValidation++;
					    dirtybenches += bench.benchParams.printReadableParams() + "\n";
					}*/
					bench = null;
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

    public async void autoCsvDump_click(object sender, EventArgs args) //user clicked the auto export button, iterate and export all possible combinations pivot CSVs
    {
	detachChart();
	controlPanel_old.saveTempIndices();
	controlPanel_old.setControlsEnabled(false, false, false);
	controlPanel_old.autoCheck.Checked = false;

	string folderPath;
	using (FolderBrowserDialog folderDialog = new FolderBrowserDialog()) //get output directory
	{
	    folderDialog.ShowNewFolderButton = true;
	    folderDialog.Description = "Select folder to mass export CSV files";
	    DialogResult result = folderDialog.ShowDialog();

	    if (result == DialogResult.OK) {
		folderPath = folderDialog.SelectedPath;
	    } else {
		controlPanel_old.exportButtonOld.Enabled = true;
		controlPanel_old.autoExportButton.Enabled = true;
		return;
	    }
	}

	int files;
	using (CancellationTokenSource cancelSource = new CancellationTokenSource()) //loop preparation and execution
	{
	    controlPanel_old.saveTempIndices();
	    Progress<int> autoCsvDumpProgress = new Progress<int>();
	    controlPanel_old.setCancelButton(cancelSource, 2);
	    System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;
	    files = await Task.Factory.StartNew(() => autoCsvDumpLoop(folderPath, autoCsvDumpProgress, cancelSource.Token), TaskCreationOptions.LongRunning);
	    controlPanel_old.setCancelButton(null, 2);
	    controlPanel_old.restoreTempIndices();
	    System.Windows.Forms.Cursor.Current = Cursors.Default;
	    MB.S("Wrote " + files + " files to " + folderPath);
	    controlPanel_old.restoreTempIndices();
	    controlPanel_old.setControlsEnabled(true, true, true);
	}
    }

    private int autoCsvDumpLoop(string folderPath, IProgress<int> progress, CancellationToken token)
    {
	int files = 0;
	for (int i = 0; i < 9; i++) //8
	{
	    controlPanel_old.setRadioIndex(i);
	    for (int j = 0; j < controlPanel_old.dropKernel.Items.Count; j++) //0
	    {
		controlPanel_old.setDropMenuSelected(0, j);
		for (int k = 0; k < controlPanel_old.dropDevice.Items.Count; k++) //1
		{
		    controlPanel_old.setDropMenuSelected(1, k);
		    for (int m = 1; m < controlPanel_old.dropMemory.Items.Count; m++) //2
		    {
			controlPanel_old.setDropMenuSelected(2, m);
			if (i > 2 && getDocFromKey(controlPanel_old.getKey1FromDropdowns()) == null) { continue; }
			for (int n = m; n < controlPanel_old.dropMapsize.Items.Count; n++) //3
			{
			    controlPanel_old.setDropMenuSelected(3, n);
			    for (int p = 0; p < controlPanel_old.dropJobs.Items.Count; p++) //4
			    {
				controlPanel_old.setDropMenuSelected(4, p);
				for (int q = 0; q < controlPanel_old.dropDelay.Items.Count; q++) //5
				{
				    controlPanel_old.setDropMenuSelected(5, q);
				    for (int r = 0; r < controlPanel_old.dropRatio.Items.Count; r++) //6
				    {
					controlPanel_old.setDropMenuSelected(6, r);
					//for (int s = 0; s < controlPanel_old.dropNice.Items.Count; s++) //7
					//{
					    if (token.IsCancellationRequested) { return files; }
					    //controlPanel_old.setDropMenuSelected(7, s);
					    if (getResults(false))
					    {
						if (!auto_old.dumped && auto_old.rounds.Count > 1)
						{
						    files += auto_old.exportCsv(folderPath);
						    auto_old.dumped = true;
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
	MB.S("autoCsvDumpLoop: Exiting");
	return files;
    }

    private XmlDocument getDocFromKey(string key)
    {
	try {
	    XmlDocument doc = xmlFiles[key];
	    return doc;
	}
	catch (KeyNotFoundException) {
	    return null;
	}
    }

    private XmlNode getBenchSiblingsNodeFromDocAndKey(XmlDocument doc, string key2)
    {
	//select the node with the user-provided parameters
	try {
	    return SafeXmlParse.selNode(doc, controlPanel_old.getNodeSelectionPathFromKey2(key2));
	}
	catch (System.Xml.XPath.XPathException x) {
	    MB.S("getBenchesFromDocAndKey: Malformed XPath\n" + x.ToString());
	    return null;
	}
    }

    private Harness makeHarness(BenchSiblings bench, int ri)
    {
	if (controlPanel_old.radioIndex == 8) {
	    return new Harness(bench.benchParams, ri, bench.Trials, this);
	} else {
	    BenchSiblings b;
	    List<BenchRound> brs = new List<BenchRound>();
	    for (int i = 0; i < controlPanel_old.getPivotVariableCount(ri); i++)
	    {
		b = getBenchSiblingsObjectFromKeyAndKey(controlPanel_old.makePivotCronyKey1(ri, i), controlPanel_old.makePivotCronyKey2(ri, i));
		if (b != null) {
		    brs.Add(b.getAverageRound());
		}
	    }
	    b = null;
	    return new Harness(bench.benchParams, ri, brs, this);
	}
    }

    private BenchSiblings getBenchSiblingsObjectFromKeyAndKey(string key1, string key2)
    {
	BenchSiblings bench = null;

	try {
	    bench = allBenchSiblings[key1 + "_" + key2];
	}
	catch (KeyNotFoundException) {
	    XmlDocument doc = getDocFromKey(key1);
	    if (doc == null) {
		return null;
	    } else {
		XmlNode sn = getBenchSiblingsNodeFromDocAndKey(doc, key2);
		if (sn == null) return null;

		ParamSet bp = ParamSet.makeParamsFromKeysAndNode(key1, key2, getParamsNodeFromSeriesNode(sn));
		if (bp == null) return null;

//		string[] splat = splitString(key1, '_');
		char[] delimiter = { '_' };
		string[] splat = key1.Split(delimiter);
		bp.operatingSystem = controlPanel_old.getKey1Value(0, int.Parse(splat[0]));
		bp.swapDevice = controlPanel_old.getKey1Value(1, int.Parse(splat[1]));
		bp.valueMemory = int.Parse(controlPanel_old.getKey1Value(2, int.Parse(splat[2])));
		splat = null;
		bench = new BenchSiblings(sn, doc, bp);
		sn = null;
	    }
	    allBenchSiblings[key1 + "_" + key2] = bench; //removing this causes duplicates
	}
	return bench;
    }

    private Harness getHarnessFromKey(string s)
    {
	try {
	    return allHarnesses[s];
	}
	catch (KeyNotFoundException) {
	    return null;
	}
    }

    private Harness getHarnessFromDropdowns()
    {
	Harness hns = getHarnessFromKey(controlPanel_old.getPivotKeys(false));

	if (hns == null) {
	    BenchSiblings bench = getBenchSiblingsObjectFromKeyAndKey(controlPanel_old.getKey1FromDropdowns(), controlPanel_old.getKey2FromDropdowns());
	    if (bench == null) return null;

	    hns = makeHarness(bench, controlPanel_old.radioIndex);
	    allHarnesses[controlPanel_old.getPivotKeys(false)] = hns;
	}
	return hns;
    }


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
		    try {
			File.WriteAllText(where, sr.ReadToEnd());
			return true;
		    }
		    catch (IOException x) {
			MB.S("(writeXmlDocument) Error writing file to " + where + "\n" + x.ToString());
			return false;
		    }
		}
	    }
	}
    }


    private void updateChart()
    {
Console.WriteLine("updateChart called");
	try {
	    if (mainPanel.InvokeRequired) return;

	    if (auto_old == null) {
		MB.S("auto_oldcoe is null!");
		return;
	    }
	    currentChart = auto_old.getPreparedChart();

	    currentChart.Width = calculateChartWidth();
	    currentChart.Height = calculateChartHeight();

	    currentChart.Location = new Point(
		    controlPanel_old.Width +
		    controlPanel_old.Margin.Left +
		    controlPanel_old.Margin.Right, 0);
	    mainPanel.Controls.Add(currentChart);


	    if (controlPanel_old.manualCheck.Enabled == false) {
		controlPanel_old.exportButtonOld.Enabled = true;
	    }
	}
	catch (NullReferenceException x) {
	    MB.S("null reference:"+x.ToString());
	    return;
	}
    }

    private bool getResults(bool click)
    {
Console.WriteLine("getResults called");
	controlPanel_old.exportButtonOld.Enabled = false;

	if (click) {
	    if (controlPanel_old.dropMemory.SelectedIndex > 
		    controlPanel_old.dropMapsize.SelectedIndex) {
		MB.S("Physical memory should not exceed map size.");
		return false;
	    }
	}

	if (!mainPanel.InvokeRequired) detachChart();

	if (controlPanel_old.manualCheck.Checked == false) {
	    if (getDocFromKey(controlPanel_old.getKey1FromDropdowns()) == null) { 
		return false;
	    }
	    controlPanel_old.updateSavedIndices();
	    auto_old = getHarnessFromDropdowns();
	}
	if (auto_old == null) return false;

	updateChart();

	return true;
    }

    public void getResults_click(object o, EventArgs args)
    {
	getResults(true);
    }

    public void exportCsvAuto_old_click(object sender, EventArgs args)
    {
	// checking getResults - auto_old may be null.
	if (getResults(false)) {
	    auto_old.exportCsv(null);
	}
    }

    public void updateSelectionButtons_old(int i)
    {
	controlPanel_old.setManualButtonsEnabled_old(
		controlPanel_old.manualCheck.Checked 
		&& controlPanel_old.manualCheck.Enabled, 
		i);
    }

}


} // namespace PmGraphSpace

//delete single on right click context menu
