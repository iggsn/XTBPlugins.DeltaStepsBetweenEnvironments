﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using System.Reflection;
using System.Diagnostics;
using Carfup.XTBPlugins.Forms;
using Carfup.XTBPlugins.AppCode;

namespace Carfup.XTBPlugins.DeltaStepsBetweenEnvironments
{
    public partial class DeltaStepsBetweenEnvironments : PluginControlBase, IXrmToolBoxPluginControl, IGitHubPlugin
    {
        #region varibables
        private string[] solutionsList = null;
        private ConnectionDetail sourceDetail = null;
        private ConnectionDetail targetDetail = null;
        IOrganizationService sourceService = null;
        IOrganizationService targetService = null;
        List<CarfupStep> stepsCrmSource = new List<CarfupStep>();
        List<CarfupStep> stepsCrmTarget = new List<CarfupStep>();
        private static string solutionPluginStepsName = null;
        public event EventHandler OnRequestConnection;
        internal PluginSettings settings = new PluginSettings();
        LogUsage log = null;
        Comparing comparing = Comparing.Solution;
        ControllerManager controller = null;
        string whatToCompare = "solution";
        
        public string RepositoryName
        {
            get
            {
                return "XTBPlugins.DeltaStepsBetweenEnvironments";
            }
        }

        public string UserName
        {
            get
            {
                return "carfup";
            }
        }

        #endregion

        public DeltaStepsBetweenEnvironments()
        {
            InitializeComponent();
            sourceDetail = this.ConnectionDetail;
            sourceService = this.Service;
            buttonCompare.Visible = false;
        }

        private void toolStripButtonClose_Click(object sender, EventArgs e)
        {
            this.log.LogData(EventType.Event, LogAction.PluginClosed);

            // Saving settings for the next usage of plugin
            SaveSettings();

            // Making sure that all message are sent if stats are enabled
            this.log.Flush();
            CloseTool();
        }

        //Select the solution from where we will query the steps
        private void comboBoxSolutionsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (canProceed())
            {
                solutionPluginStepsName = comboBoxSolutionsAssembliesList.SelectedItem.ToString();

                isSolutionOrAssemblyExistingInTargetEnv();
            }
        }

        private void isSolutionOrAssemblyExistingInTargetEnv()
        {
            string whatToCompare = Wording.getComparingInfo(comparing);

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Checking if the {whatToCompare} name exists in the target environment...",
                Work = (bw, e) =>
                {
                    e.Result = (comparing == Comparing.Solution) ? controller.dataManager.isSolutionExistingInTargetEnv(solutionPluginStepsName) : controller.dataManager.isAssemblyExistingInTargetEnv(solutionPluginStepsName);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        string logAction = (comparing == Comparing.Solution) ? LogAction.SolutionExistingInTargetEnvChecked : LogAction.AssemblyExistingInTargetEnvChecked;
                        this.log.LogData(EventType.Exception, logAction, e.Error);

                        MessageBox.Show(this, e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if((int)e.Result != 1)
                        MessageBox.Show($"The {whatToCompare} doesn't exist in the Target environment. \rThe compare function will return a \"Perfect match\" in this case.\r\r You will still have the possibility to copy steps from the Source to Target environment.", "Informaton", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                },
                ProgressChanged = e => { SetWorkingMessage(e.UserState.ToString()); }
            });
        }

        private void comboBoxTargetEnvironmentList_Click(object sender, EventArgs e)
        {
            if (OnRequestConnection != null)
            {
                var aaa = new RequestConnectionEventArgs
                {
                    ActionName = "TargetOrganization",
                    Control = this
                };
                OnRequestConnection(this, aaa);
            }
        }

        public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object parameter = null)
        {
            controller = new ControllerManager(sourceService, targetService);

            if (actionName == "TargetOrganization")
            {
                targetService = newService;
                targetDetail = connectionDetail;
                SetConnectionLabel(connectionDetail, "Target");
                controller.targetService = targetService;
            }
            else
            {
                sourceService = newService;
                sourceDetail = connectionDetail;
                SetConnectionLabel(connectionDetail, "Source");
                controller.sourceService = sourceService;
            }

            if (targetService != null && sourceService != null)
                buttonCompare.Visible = true;
            else
                buttonCompare.Visible = false;
        }
        private void SetConnectionLabel(ConnectionDetail detail, string serviceType)
        {
            switch (serviceType)
            {
                case "Source":
                    labelSourceEnvironment.Text = detail.ConnectionName;
                    labelSourceEnvironment.ForeColor = Color.Green;
                    break;

                case "Target":
                    labelTargetEnvironment.Text = detail.ConnectionName;
                    labelTargetEnvironment.ForeColor = Color.Green;
                    break;
            }
        }

        private void btnChangeTargetEnvironment_Click(object sender, EventArgs e)
        {
            if (OnRequestConnection != null)
            {
                var arg = new RequestConnectionEventArgs
                {
                    ActionName = "TargetOrganization",
                    Control = this
                };
                OnRequestConnection(this, arg);
            }


        }

        // We compare the same solution name in both environments
        private void buttonCompare_Click(object sender, EventArgs evt)
        {
            compareBothSolutions();
        }


        private void compareBothSolutions()
        {
            if (solutionPluginStepsName == null)
            {
                MessageBox.Show($"Please select a solution first.");
                return;
            }

            string[] diffCrmSourceTarget = null;
            string[] diffCrmTargetSource = null;

            stepsCrmSource.Clear();
            stepsCrmTarget.Clear();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Comparing the 2 Solutions...",
                Work = (bw, e) =>
                {
                    if(comparing == Comparing.Solution)
                    {
                        stepsCrmSource = controller.dataManager.querySteps(sourceService, solutionPluginStepsName);  //querySteps(sourceService, stepsCrmSource);
                        stepsCrmTarget = controller.dataManager.querySteps(targetService, solutionPluginStepsName);  //querySteps(targetService, stepsCrmTarget);
                       
                    }
                    else if(comparing == Comparing.Assembly)
                    {
                        stepsCrmSource = controller.dataManager.queryStepsAssembly(sourceService, solutionPluginStepsName);  //querySteps(sourceService, stepsCrmSource);
                        stepsCrmTarget = controller.dataManager.queryStepsAssembly(targetService, solutionPluginStepsName);  //querySteps(targetService, stepsCrmTarget);
                  //      diffCrmSourceTarget = stepsCrmSource.Select(x => x.Attributes["name"].ToString()).Except(stepsCrmTarget.Select(x => x.Attributes["name"].ToString())).ToArray();
                  //      diffCrmTargetSource = stepsCrmTarget.Select(x => x.Attributes["name"].ToString()).Except(stepsCrmSource.Select(x => x.Attributes["name"].ToString())).ToArray();
                    }

                    diffCrmSourceTarget = stepsCrmSource.Select(x => x.stepName).Except(stepsCrmTarget.Select(x => x.stepName)).ToArray();
                    diffCrmTargetSource = stepsCrmTarget.Select(x => x.stepName).Except(stepsCrmSource.Select(x => x.stepName)).ToArray();
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.SolutionsCompared, e.Error);
                        MessageBox.Show(this, e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (diffCrmSourceTarget.Count() == 0)
                    {
                        //listBoxSourceTarget.Visible = false;
                        labelSourceTargetMatch.Visible = true;
                    }
                    else // there are steps in source but not target
                    {
                        labelSourceTargetMatch.Visible = false;
                        fillListViewItems(listViewSourceTarget, stepsCrmSource, diffCrmSourceTarget);
                    }

                    if (diffCrmTargetSource.Count() == 0)
                    {
                        //listBoxTargetSource.Visible = false;
                        labelTargetSourceMatch.Visible = true;
                    }
                    else // there are steps in source but not target
                    {
                        labelTargetSourceMatch.Visible = false;
                        fillListViewItems(listViewTargetSource, stepsCrmTarget, diffCrmTargetSource);
                    }

                    this.log.LogData(EventType.Event, LogAction.SolutionsCompared);
                },
                ProgressChanged = e => { SetWorkingMessage(e.UserState.ToString()); }
            });
        }
        // Loading solutions from the Source environment
        private void buttonLoadSolutionsAssemblies_Click(object sender, EventArgs evt)
        {
            if (canProceed())
            {
                comboBoxSolutionsAssembliesList.Items.Clear();
                string logAction = (comparing == Comparing.Solution) ? LogAction.SolutionsLoaded : LogAction.AssembliesLoaded;

                WorkAsync(new WorkAsyncInfo
                {
                    Message = $"Loading CRM {Wording.getComparingInfo(comparing, true, true)}...",
                    Work = (bw, e) =>
                    {
                        this.log.LogData(EventType.Event, logAction);
                        solutionsList = (comparing == Comparing.Solution) ? controller.dataManager.loadSolutions() : controller.dataManager.loadAssemblies();
                    },
                    PostWorkCallBack = e =>
                    {
                        if (e.Error != null)
                        {
                            
                            this.log.LogData(EventType.Exception, logAction, e.Error);
                            MessageBox.Show(this, e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        this.log.LogData(EventType.Event, $"{Wording.getComparingInfo(comparing, true, true)} retrieved");
                        if (solutionsList != null)
                            comboBoxSolutionsAssembliesList.Items.AddRange(solutionsList);

                    },
                    ProgressChanged = e => { SetWorkingMessage(e.UserState.ToString()); }
                });
            }
        }

        private void buttonChangeSource_Click(object sender, EventArgs e)
        {
            if (OnRequestConnection != null)
            {
                var arg = new RequestConnectionEventArgs
                {
                    ActionName = "SourceOrganization",
                    Control = this
                };
                OnRequestConnection(this, arg);
            }
        }

        // We check if both environments are selected otherwise : error message
        public bool canProceed()
        {
            if (sourceService == null || targetService == null)
            {
                this.log.LogData(EventType.Event, LogAction.CanProceed);
                MessageBox.Show("Make sure you are connected to a Source AND Target environments first.");
                return false;
            }
            return true;
        }

        // Copying a step from the target to source environment
        private void buttonCopyTargetToSource_Click(object sender, EventArgs evt)
        {
            var selectedStep = stepsCrmTarget.Where(x => x.stepName == listViewTargetSource.SelectedItems.ToString()).FirstOrDefault();

            if (selectedStep == null)
                return;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Creating the step in the environment...",
                Work = (bw, e) =>
                {
                    // retrieving the 3 data mandatory to have a proper step created
                    var pluginType = controller.dataManager.getPluginType(selectedStep.plugintypeName);
                    var sdkMessage = controller.dataManager.getSdkMessage(selectedStep.messageName);
                    var messageFilter = controller.dataManager.getMessageFilter(selectedStep.entityName);

                    if (pluginType == null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.PluginTypeRetrievedTargetToSource);
                        MessageBox.Show($"Sorry, but we didn't find the necessary Plugin Type information in the destination system...");
                        return;
                    }

                    if (sdkMessage == null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.SDKMessageRetrievedTargetToSource);
                        MessageBox.Show($"Sorry, but we didn't find the necessary SDK Message information in the destination system...");
                        return;
                    }

                    if (messageFilter == null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.MessageFilterRetrievedTargetToSource);
                        MessageBox.Show($"Sorry, but we didn't find the necessary Message Filter information in the destination system...");
                        return;
                    }

                    this.log.LogData(EventType.Event, LogAction.PluginTypeRetrievedTargetToSource);
                    this.log.LogData(EventType.Event, LogAction.SDKMessageRetrievedTargetToSource);
                    this.log.LogData(EventType.Event, LogAction.MessageFilterRetrievedTargetToSource);

                    // Preparing the object step
                    Entity newStepToCreate = new Entity("sdkmessageprocessingstep");
                    newStepToCreate["plugintypeid"] = new EntityReference("plugintype", pluginType.Id);
                    newStepToCreate["sdkmessageid"] = new EntityReference("plugintype", sdkMessage.Id);
                    newStepToCreate["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", messageFilter.Id);
                    newStepToCreate["name"] = selectedStep.stepName;
                    newStepToCreate["configuration"] = selectedStep.stepConfiguration;
                    newStepToCreate["mode"] = selectedStep.stepMode;
                    newStepToCreate["rank"] = selectedStep.stepRank;
                    newStepToCreate["stage"] = selectedStep.stepStage;
                    newStepToCreate["supporteddeployment"] = selectedStep.stepSupporteddeployment;
                    newStepToCreate["invocationsource"] = selectedStep.stepInvocationsource;
                    newStepToCreate["configuration"] = selectedStep.stepConfiguration;
                    newStepToCreate["filteringattributes"] = selectedStep.stepFilteringattributes;
                    newStepToCreate["description"] = selectedStep.stepDescription;
                    newStepToCreate["asyncautodelete"] = selectedStep.stepAsyncautodelete;
                    newStepToCreate["customizationlevel"] = selectedStep.stepCustomizationlevel;

                    e.Result = targetService.Create(newStepToCreate);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.StepCreatedTargetToSource, e.Error);
                        MessageBox.Show(this, e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if ((Guid)e.Result != null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.StepCreatedTargetToSource, e.Error);
                        MessageBox.Show($"Your step was successfully copied to the default solution of source environment.");
                        //listBoxSourceTarget.Items.Add(returnAliasedValue(selectedStep, "step.name"));

                        labelSourceTargetMatch.Visible = false;
                        labelTargetSourceMatch.Visible = false;
                    }


                },
                ProgressChanged = e => { SetWorkingMessage(e.UserState.ToString()); }
            });
        }

        // Copying a step from the source to target environment
        private void buttonCopySourceToTarget_Click(object sender, EventArgs evt)
        {
            var selectedStep = stepsCrmSource.Where(x => x.stepName == listViewSourceTarget.SelectedItems.ToString()).FirstOrDefault();

            if (selectedStep == null)
                return;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Creating the step in the environment...",
                Work = (bw, e) =>
                {
                    // retrieving the 3 data mandatory to have a proper step created
                    var pluginType = controller.dataManager.getPluginType(selectedStep.plugintypeName);
                    var sdkMessage = controller.dataManager.getSdkMessage(selectedStep.messageName);
                    var messageFilter = controller.dataManager.getMessageFilter(selectedStep.entityName);

                    if (pluginType == null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.PluginTypeRetrievedSourceToTarget);
                        MessageBox.Show($"Sorry, but we didn't find the necessary Plugin Type information in the destination system...");
                        return;
                    }

                    if (sdkMessage == null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.SDKMessageRetrievedSourceToTarget);
                        MessageBox.Show($"Sorry, but we didn't find the necessary SDK Message information in the destination system...");
                        return;
                    }

                    if (messageFilter == null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.MessageFilterRetrievedSourceToTarget);
                        MessageBox.Show($"Sorry, but we didn't find the necessary Message Filter information in the destination system...");
                        return;
                    }


                    this.log.LogData(EventType.Event, LogAction.PluginTypeRetrievedSourceToTarget);
                    this.log.LogData(EventType.Event, LogAction.SDKMessageRetrievedSourceToTarget);
                    this.log.LogData(EventType.Event, LogAction.MessageFilterRetrievedSourceToTarget);

                    // Preparing the object step
                    Entity newStepToCreate = new Entity("sdkmessageprocessingstep");
                    newStepToCreate["plugintypeid"] = new EntityReference("plugintype", pluginType.Id);
                    newStepToCreate["sdkmessageid"] = new EntityReference("plugintype", sdkMessage.Id);
                    newStepToCreate["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", messageFilter.Id);
                    newStepToCreate["name"] = selectedStep.stepName;
                    newStepToCreate["configuration"] = selectedStep.stepConfiguration;
                    newStepToCreate["mode"] = selectedStep.stepMode;
                    newStepToCreate["rank"] = selectedStep.stepRank;
                    newStepToCreate["stage"] = selectedStep.stepStage;
                    newStepToCreate["supporteddeployment"] = selectedStep.stepSupporteddeployment;
                    newStepToCreate["invocationsource"] = selectedStep.stepInvocationsource;
                    newStepToCreate["configuration"] = selectedStep.stepConfiguration;
                    newStepToCreate["filteringattributes"] = selectedStep.stepFilteringattributes;
                    newStepToCreate["description"] = selectedStep.stepDescription;
                    newStepToCreate["asyncautodelete"] = selectedStep.stepAsyncautodelete;
                    newStepToCreate["customizationlevel"] = selectedStep.stepCustomizationlevel;

                    e.Result = targetService.Create(newStepToCreate);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        this.log.LogData(EventType.Exception, LogAction.StepCreeatedSourceToTarget, e.Error);
                        MessageBox.Show(this, e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if(e.Result != null)
                    {
                        this.log.LogData(EventType.Event, LogAction.StepCreeatedSourceToTarget);
                        MessageBox.Show($"Your step was successfully copied to the default solution of target environment.");
                        //listBoxTargetSource.Items.Add(returnAliasedValue(selectedStep, "step.name"));

                        labelSourceTargetMatch.Visible = false;
                        labelTargetSourceMatch.Visible = false;
                    }
                        

                },
                ProgressChanged = e => { SetWorkingMessage(e.UserState.ToString()); }
            });
        }

       
        // action when the option form is opened
        private void toolStripButtonOptions_Click(object sender, EventArgs e)
        {
            var allowLogUsage = settings.AllowLogUsage;            
            var optionDlg = new Options(this);
            if (optionDlg.ShowDialog(this) == DialogResult.OK)
            {
                settings = optionDlg.GetSettings();
                if (allowLogUsage != settings.AllowLogUsage)
                {
                    if (settings.AllowLogUsage == true)
                    {
                        this.log.updateForceLog();
                        this.log.LogData(EventType.Event, LogAction.StatsAccepted);
                    }
                    else if (!settings.AllowLogUsage == true)
                    {
                        this.log.updateForceLog();
                        this.log.LogData(EventType.Event, LogAction.StatsDenied);
                    }
                }
            }
        }

        // will save personal settings
        public void SaveSettings()
        {
            this.log.LogData(EventType.Event, LogAction.SettingsSaved);
            SettingsManager.Instance.Save(typeof(DeltaStepsBetweenEnvironments), settings);
        }

        private void DeltaStepsBetweenEnvironments_Load(object sender, EventArgs e)
        {
            // initializing log class
            log = new LogUsage(this);
            this.log.LogData(EventType.Event, LogAction.PluginOpened);
            LoadSetting();
        }

        // either loading previous settings from user or creating default ones and prompt the messagae for  the stats
        private void LoadSetting()
        {
            try
            {
                if (SettingsManager.Instance.TryLoad<PluginSettings>(typeof(DeltaStepsBetweenEnvironments), out settings))
                {
                    return;
                }
                else
                    settings = new PluginSettings();
            }
            catch (InvalidOperationException ex) {
                this.log.LogData(EventType.Exception, LogAction.SettingLoaded, ex);
            }
            

            this.log.LogData(EventType.Event, LogAction.SettingLoaded);

            if (!settings.AllowLogUsage.HasValue)
            {
                this.log.PromptToLog();
                this.SaveSettings();
            }
        }

        // return the current version of the plugin
        public static string CurrentVersion
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                return fileVersionInfo.ProductVersion;
            }
        }

        private void radioButtonCompareSolution_Click(object sender, EventArgs e)
        {
            comparing = Comparing.Solution;
            buttonLoadSolutionsAssemblies.Text = "Load Solutions";
            labelComparing.Text = "Select the solution to compare :";
            comboBoxSolutionsAssembliesList.Items.Clear();
        }

        private void radioButtonCompareAssembly_Click(object sender, EventArgs e)
        {
            comparing = Comparing.Assembly;
            buttonLoadSolutionsAssemblies.Text = "Load Assemblies";
            labelComparing.Text = "Select the assembly to compare :";
            comboBoxSolutionsAssembliesList.Items.Clear();
        }

        private void fillListViewItems(ListView listView, List<CarfupStep> stepsList, string[] diff)
        {
            listView.Items.Clear();

            foreach (var step in stepsList.Where(x => diff.Contains(x.stepName)))
            {
                string createon = step.createOn.ToLocalTime().ToString("dd-MMM-yyyy HH:mm");
                string modifiedon = step.modifiedOn.ToLocalTime().ToString("dd-MMM-yyyy HH:mm");

                var item = new ListViewItem();
                item.Text = step.stepName;
                item.SubItems.Add(step.entityName);
                item.SubItems.Add(step.messageName);
                item.SubItems.Add(createon);
                item.SubItems.Add(modifiedon);
                item.Tag = step.entity.Id;

                listView.Items.Add((ListViewItem)item.Clone());
            }
            listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }
    }
}