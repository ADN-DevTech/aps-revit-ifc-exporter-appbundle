// (C) Copyright 2021 by Autodesk, Inc. 
//
// Permission to use, copy, modify, and distribute this software
// in object code form for any purpose and without fee is hereby
// granted, provided that the above copyright notice appears in
// all copies and that both that copyright notice and the limited
// warranty and restricted rights notice below appear in all
// supporting documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS. 
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK,
// INC. DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL
// BE UNINTERRUPTED OR ERROR FREE.
//
// Use, duplication, or disclosure by the U.S. Government is
// subject to restrictions set forth in FAR 52.227-19 (Commercial
// Computer Software - Restricted Rights) and DFAR 252.227-7013(c)
// (1)(ii)(Rights in Technical Data and Computer Software), as
// applicable.
//

using System;
using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using BIM.IFC.Export;
using Autodesk.Revit.DB.IFC;
using System.Reflection;
using Autodesk.Revit.Creation;
using Revit.IFC.Common.Extensions;

namespace RevitIfcExporter
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MainApp : IExternalDBApplication
    {
        public ExternalDBApplicationResult OnStartup(ControlledApplication application)
        {
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            // Hook up the CustomFailureHandling failure processor.
            Autodesk.Revit.ApplicationServices.Application.RegisterFailuresProcessor(new OpenDocumentFailuresProcessor());

            return ExternalDBApplicationResult.Succeeded;
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            return ExternalDBApplicationResult.Succeeded;
        }

        [Obsolete]
        public void HandleApplicationInitializedEvent(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            DesignAutomationData data = new DesignAutomationData(app, "InputFile.rvt");
            this.DoExport(data);
        }

        private void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            LogTrace("Design Automation Ready event triggered...");
            // Hook up the CustomFailureHandling failure processor.
            Autodesk.Revit.ApplicationServices.Application.RegisterFailuresProcessor(new ExportIfcFailuresProcessor());

            e.Succeeded = true;
            e.Succeeded = this.DoExport(e.DesignAutomationData);
        }

        private bool DoExport(DesignAutomationData data)
        {
            if (data == null)
                return false;

            Autodesk.Revit.ApplicationServices.Application app = data.RevitApp;
            if (app == null)
            {
                LogTrace("Error occured");
                LogTrace("Invalid Revit App");
                return false;
            }

            string modelPath = data.FilePath;
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                LogTrace("Error occured");
                LogTrace("Invalid File Path");
                return false;
            }

            var doc = data.RevitDoc;
            if (doc == null)
            {
                LogTrace("Error occured");
                LogTrace("Invalid Revit DB Document");
                return false;
            }

            var inputParams = JsonConvert.DeserializeObject<InputParams>(File.ReadAllText("params.json"));
            if (inputParams == null)
            {
                LogTrace("Invalid Input Params or Empty JSON Input");
                return false;
            }

            try
            {
                LogTrace("Collecting export configs...");

                var configurationsMap = new IFCExportConfigurationsMap();
                configurationsMap.AddBuiltInConfigurations();
                configurationsMap.AddSavedConfigurations(doc);

                if (inputParams.UseExportSettingFile)
                {
                    try
                    {
                        LogTrace("Importing user export configs...");
                        var json = File.ReadAllText("userExportSettings.json");
                        var eportSettingName = configurationsMap.AddConfigurationFromJson(json);
                        inputParams.ExportSettingName = eportSettingName;

                        LogTrace($"User export configs `{eportSettingName}` imported, and will use it to export IFC ...");
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Invalid `userExportSettings.json` or Empty JSON Input when `UseExportSettingFile` is true.");
                    }
                }

                if (!configurationsMap.HasName(inputParams.ExportSettingName))
                    throw new InvalidDataException($"Invalid input ExportSettingName: `{inputParams.ExportSettingName}`");

                var exportConfig = configurationsMap[inputParams.ExportSettingName];

                if (!string.IsNullOrWhiteSpace(inputParams.UserDefinedPropertySetsFilenameOverride))
                {
                    exportConfig.ExportUserDefinedPsets = true;
                    exportConfig.ExportUserDefinedPsetsFileName = inputParams.UserDefinedPropertySetsFilenameOverride;
                }

                if (!string.IsNullOrWhiteSpace(inputParams.UserDefinedParameterMappingFilenameOverride))
                {
                    exportConfig.ExportUserDefinedParameterMapping = true;
                    exportConfig.ExportUserDefinedParameterMappingFileName = inputParams.UserDefinedParameterMappingFilenameOverride;
                }

                if (inputParams.OnlyExportVisibleElementsInView == true) //!<<< Override the `Export only elements visible in view` option on the fly
                {
                    exportConfig.VisibleElementsOfCurrentView = true;
                }

                ElementId filterViewId = this.GetFilterViewId(doc, inputParams);
                if (filterViewId == ElementId.InvalidElementId)
                {
                    exportConfig.UseActiveViewGeometry = false;
                    exportConfig.VisibleElementsOfCurrentView = false;
                    LogTrace($"!!!Warning!!!- No view found with the specified `viewId`: `{inputParams.ViewId}`, so the view-related settings would not take effect, e.g.`IFCExportConfiguration.VisibleElementsOfCurrentView` or `IFCExportConfiguration.UseActiveViewGeometry`.");
                }

                if (exportConfig.UseActiveViewGeometry)
                {
#if SinceRVT2024
                    exportConfig.ActiveViewId = filterViewId;
#else
                    exportConfig.ActiveViewId = filterViewId.IntegerValue;
#endif
                }

                // Revit IFC addin uses absolute paths for UserDefinedPropertySets and UserDefinedParameterMappingFile, so change the paths to th relative ones.
                this.FixDependeciesPath(exportConfig);

                // Setup IFCExportOptions for caling Revit IFC export API.
                var exportOptions = new IFCExportOptions();
#if SinceRVT2027
                exportConfig.UpdateOptions(doc, exportOptions, filterViewId,
                    !OptionsUtil.UseLegacyParameterMapping());
#else
                exportConfig.UpdateOptions(doc, exportOptions, filterViewId);
#endif

                LogTrace("Creating export folder...");

                var exportPath = Path.Combine(Directory.GetCurrentDirectory(), "ifc");
                if (!Directory.Exists(exportPath))
                {
                    try
                    {
                        Directory.CreateDirectory(exportPath);
                    }
                    catch (Exception ex)
                    {
                        this.PrintError(ex);
                        return false;
                    }
                }

                LogTrace(string.Format("Export Path: `{0}`", exportPath));

                LogTrace("Starting the export task...");

                try
                {
                    LogTrace("- Deleteing obsolete IFCClassifications... ");
                    // Call this before the Export IFC transaction starts, as it has its own transaction.
                    IFCClassificationMgr.DeleteObsoleteSchemas(doc);
                    LogTrace("-- DONE... ");
                }
                catch (Exception ex)
                {
                    this.PrintError(ex);
                    LogTrace("-- Failed to delete obsolete IFCClassifications, Skip... ");
                    return false;
                }

                // This option should be rarely used, and is only for consistency with old files.  As such, it is set by environment variable only.
                string use2009GUID = null;
                try
                {
                    LogTrace("- Getting env variable `Assign2009GUIDToBuildingStoriesOnIFCExport`... ");
                    use2009GUID = Environment.GetEnvironmentVariable("Assign2009GUIDToBuildingStoriesOnIFCExport");
                    LogTrace("-- DONE... ");
                }
                catch (Exception ex)
                {
                    this.PrintError(ex);
                    LogTrace("-- Failed to get env variable `Assign2009GUIDToBuildingStoriesOnIFCExport`, Skip.... ");
                    return false;
                }

                bool use2009BuildingStoreyGUIDs = (use2009GUID != null && use2009GUID == "1");

                bool result = false;
                using (Transaction trans = new Transaction(doc, "Export IFC"))
                {
                    try
                    {
                        trans.Start();

                        LogTrace("- Setting SetFailureHandlingOptions: SetClearAfterRollback=false...");
                        FailureHandlingOptions failureOptions = trans.GetFailureHandlingOptions();
                        failureOptions.SetClearAfterRollback(false);
                        trans.SetFailureHandlingOptions(failureOptions);
                        LogTrace("-- DONE... ");

                        LogTrace("- Execting the export task...");
                        result = doc.Export(exportPath, doc.Title, exportOptions);

                        if (!result)
                            throw new InvalidOperationException("Failed to export IFC");

                        LogTrace("-- DONE... ");
                    }
                    catch (Exception ex)
                    {
                        LogTrace("- Receieve exception, will print it out after rolling changes back...");
                        throw ex;
                    }
                    finally
                    {
                        
                        //// Roll back the transaction started earlier, unless certain options are set.
                        if (result && (use2009BuildingStoreyGUIDs || exportConfig.StoreIFCGUID))
                        {
                            LogTrace("- Saving the changes made during exporting IFC...");
                            trans.Commit();
                        }
                        else
                        {
                            LogTrace("- Rolling back the changes made during exporting IFC...");
                            trans.RollBack(); //!<<< Dsicard changes in IFC export settings. This won't affect the exporting.
                        }
                        LogTrace("-- DONE... ");
                    }
                }
            }
            catch(Exception ex)
            {
                LogTrace("- Printing received exception...");
                this.PrintError(ex);
                LogTrace("-- DONE... ");
                return false;
            }

            LogTrace("Exporting completed...");

            return true;
        }

        private void FixDependeciesPath(IFCExportConfiguration configuration)
        {
            if (configuration.ExportUserDefinedPsets)
            {
                var userDefinedPsetsFileName = Path.GetFileName(configuration.ExportUserDefinedPsetsFileName);
                configuration.ExportUserDefinedPsetsFileName = Path.Combine(Directory.GetCurrentDirectory(), userDefinedPsetsFileName);
            }

            if (configuration.ExportUserDefinedParameterMapping)
            {
                var userDefinedParameterMappingFileName = Path.GetFileName(configuration.ExportUserDefinedParameterMappingFileName);
                configuration.ExportUserDefinedParameterMappingFileName = Path.Combine(Directory.GetCurrentDirectory(), userDefinedParameterMappingFileName);
            }
        }

        private ElementId GetFilterViewId(Autodesk.Revit.DB.Document document, InputParams inputParams)
        {
            ElementId filterViewId = ElementId.InvalidElementId;

            if (!string.IsNullOrWhiteSpace(inputParams.ViewId))
            {
                View filterView = document.GetElement(inputParams.ViewId) as View;
                filterViewId = (filterView == null) ? ElementId.InvalidElementId : filterView.Id;
            }
            else
            {
                View activeView = document.ActiveView;
                filterViewId = (activeView == null) ? ElementId.InvalidElementId : document.ActiveView.Id;
            }

            return filterViewId;
        }

        private string GetFileExtension(IFCFileFormat fileFormat)
        {
            string fileExt = string.Empty;

            switch(fileFormat)
            {
                case IFCFileFormat.IfcXML:
                    fileExt = "ifcXML";
                    break;
                case IFCFileFormat.IfcXMLZIP:
                case IFCFileFormat.IfcZIP:
                    fileExt = "ifcZIP";
                    break;
                default:
                    fileExt = "ifc";
                    break;
            }

            return fileExt;
        }

        private void PrintError(Exception ex)
        {
            LogTrace("Error occured");
            LogTrace(ex.Message);

            if (ex.InnerException != null)
                LogTrace(ex.InnerException.Message);
        }

        /// <summary>
        /// This will appear on the Design Automation output
        /// </summary>
        internal static void LogTrace(string format, params object[] args)
        {
#if DEBUG
            System.Diagnostics.Trace.WriteLine(string.Format(format, args));
#endif
            System.Console.WriteLine(format, args);
        }

    }
}