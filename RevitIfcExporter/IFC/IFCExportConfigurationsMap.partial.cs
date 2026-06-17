//
// BIM IFC export alternate UI library: this library works with Autodesk(R) Revit(R) to provide an alternate user interface for the export of IFC files from Revit.
// Copyright (C) 2016  Autodesk, Inc.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//
// ref: https://github.com/Autodesk/revit-ifc/blob/9a6fc912fb8510647f9d2bcc0d57b0451a2194b6/Source/IFCExporterUIOverride/IFCExportConfigurationsMap.cs
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Autodesk.Revit;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.DB.ExtensibleStorage;
using Revit.IFC.Common.Enums;
using Newtonsoft.Json;

namespace BIM.IFC.Export
{
    /// <summary>
    /// The map to store BuiltIn and Saved configurations.
    /// </summary>
    public partial class IFCExportConfigurationsMap
    {
        /// <summary>
        /// Gets the new duplicated setup name.
        /// </summary>
        /// <param name="configuration">The configuration to duplicate.</param>
        /// <returns>The new duplicated setup name.</returns>
        private string GetDuplicateSetupName(IFCExportConfiguration configuration)
        {
            return GetFirstIncrementalName(configuration.Name);
        }

        /// <summary>
        /// Gets the new incremental name for configuration.
        /// </summary>
        /// <param name="nameRoot">The name of a configuration.</param>
        /// <returns>the new incremental name for configuration.</returns>
        private string GetFirstIncrementalName(string nameRoot)
        {
            bool found = true;
            int number = 0;
            string newName = "";
            do
            {
                number++;
                newName = nameRoot + " " + number;
                if (!this.HasName(newName))
                    found = false;
            }
            while (found);

            return newName;
        }

        public string AddConfigurationFromJson(string json)
        {
            IFCExportConfiguration configuration = JsonConvert.DeserializeObject<IFCExportConfiguration>(json);
            if (configuration == null)
                throw new InvalidDataException($"Invalid input json");

            if (this.HasName(configuration.Name))
                configuration.Name = this.GetFirstIncrementalName(configuration.Name);

            if (configuration.IFCVersion == IFCVersion.IFCBCA)
                configuration.IFCVersion = IFCVersion.IFC2x3CV2;

            this.AddOrReplace(configuration);

            return configuration.Name;
        }

        public bool ConvertSavedConfigurationsFromJsonToLegacy(Document document)
        {
            if (m_jsonSchema == null)
            {
                m_jsonSchema = Schema.Lookup(s_jsonSchemaId);
            }
            if (m_jsonSchema != null)
            {
                IList<DataStorage> oldSavedConfigurations = GetSavedConfigurations(m_jsonSchema, document);
                if (oldSavedConfigurations.Count > 0)
                {
                    Transaction deleteTransaction = new Transaction(document, "Delete JSON-based IFC export setups");
                    try
                    {
                        deleteTransaction.Start("Delete JSON-based configuration");
                        List<ElementId> dataStorageToDelete = new List<ElementId>();
                        foreach (DataStorage dataStorage in oldSavedConfigurations)
                        {
                            dataStorageToDelete.Add(dataStorage.Id);
                        }
                        document.Delete(dataStorageToDelete);
                        deleteTransaction.Commit();
                    }
                    catch (System.Exception)
                    {
                        if (deleteTransaction.HasStarted())
                            deleteTransaction.RollBack();

                        return false;
                    }
                }
                else
                {
                    return false;
                }

                // update the configurations to new map schema.
                if (m_mapSchema == null)
                {
                    m_mapSchema = Schema.Lookup(s_mapSchemaId);
                }

                // Are there any setups to save or resave?
                List<IFCExportConfiguration> setupsToSave = new List<IFCExportConfiguration>();
                foreach (IFCExportConfiguration configuration in m_configurations.Values)
                {
                    if (configuration.IsBuiltIn)
                        continue;

                    // Store in-session settings in the cached in-session configuration
                    if (configuration.IsInSession)
                    {
                        IFCExportConfiguration.SetInSession(configuration);
                        continue;
                    }

                    setupsToSave.Add(configuration);
                }

                // If there are no setups to save, and if the schema is not present (which means there are no
                // previously existing setups which might have been deleted) we can skip the rest of this method.
                if (setupsToSave.Count <= 0 && m_mapSchema == null)
                    return false;

                if (m_mapSchema == null)
                {
                    SchemaBuilder builder = new SchemaBuilder(s_mapSchemaId);
                    builder.SetSchemaName("IFCExportConfigurationMap");
                    builder.AddMapField(s_configMapField, typeof(String), typeof(String));
                    m_mapSchema = builder.Finish();
                }

                // Overwrite all saved configs with the new list
                Transaction transaction = new Transaction(document, "Update IFC export setups");
                try
                {
                    transaction.Start();
                    IList<DataStorage> savedConfigurations = GetSavedConfigurations(m_mapSchema, document);
                    int savedConfigurationCount = savedConfigurations.Count<DataStorage>();
                    int savedConfigurationIndex = 0;
                    foreach (IFCExportConfiguration configuration in setupsToSave)
                    {
                        DataStorage configStorage;
                        if (savedConfigurationIndex >= savedConfigurationCount)
                        {
                            configStorage = DataStorage.Create(document);
                        }
                        else
                        {
                            configStorage = savedConfigurations[savedConfigurationIndex];
                            savedConfigurationIndex++;
                        }

                        Entity mapEntity = new Entity(m_mapSchema);
                        IDictionary<string, string> mapData = new Dictionary<string, string>();
                        mapData.Add(s_setupName, configuration.Name);
                        mapData.Add(s_setupVersion, configuration.IFCVersion.ToString());
                        mapData.Add(s_exchangeRequirement, configuration.ExchangeRequirement.ToString());
#if SinceRVT2025
                        mapData.Add(s_facilityType, configuration.FacilityType.ToString());
                        mapData.Add(s_facilityPredefinedType, configuration.FacilityPredefinedType.ToString());
#endif
                        mapData.Add(s_setupFileFormat, configuration.IFCFileType.ToString());
                        mapData.Add(s_setupSpaceBoundaries, configuration.SpaceBoundaries.ToString());
                        mapData.Add(s_setupQTO, configuration.ExportBaseQuantities.ToString());
                        mapData.Add(s_setupCurrentView, configuration.VisibleElementsOfCurrentView.ToString());
                        mapData.Add(s_splitWallsAndColumns, configuration.SplitWallsAndColumns.ToString());
                        mapData.Add(s_setupExport2D, configuration.Export2DElements.ToString());
#if SinceRVT2025
                        mapData.Add(s_setupExportCeilingGrids, configuration.ExportCeilingGrids.ToString());
#endif
                        mapData.Add(s_setupExportRevitProps, configuration.ExportInternalRevitPropertySets.ToString());
                        mapData.Add(s_setupExportIFCCommonProperty, configuration.ExportIFCCommonPropertySets.ToString());
                        mapData.Add(s_setupUse2DForRoomVolume, configuration.Use2DRoomBoundaryForVolume.ToString());
                        mapData.Add(s_setupUseFamilyAndTypeName, configuration.UseFamilyAndTypeNameForReference.ToString());
                        mapData.Add(s_setupExportPartsAsBuildingElements, configuration.ExportPartsAsBuildingElements.ToString());
                        mapData.Add(s_useActiveViewGeometry, configuration.UseActiveViewGeometry.ToString());
                        mapData.Add(s_setupExportSpecificSchedules, configuration.ExportSpecificSchedules.ToString());
                        mapData.Add(s_setupExportBoundingBox, configuration.ExportBoundingBox.ToString());
                        mapData.Add(s_setupExportSolidModelRep, configuration.ExportSolidModelRep.ToString());
#if SinceRVT2025
                        mapData.Add(s_setupExportMaterialPsets, configuration.ExportMaterialPsets.ToString());
#endif
                        mapData.Add(s_setupExportSchedulesAsPsets, configuration.ExportSchedulesAsPsets.ToString());
                        mapData.Add(s_setupExportUserDefinedPsets, configuration.ExportUserDefinedPsets.ToString());
                        mapData.Add(s_setupExportUserDefinedPsetsFileName, configuration.ExportUserDefinedPsetsFileName);
                        mapData.Add(s_setupExportUserDefinedParameterMapping, configuration.ExportUserDefinedParameterMapping.ToString());
                        mapData.Add(s_setupExportUserDefinedParameterMappingFileName, configuration.ExportUserDefinedParameterMappingFileName);
#if SinceRVT2025
                        var exportLinkedFiles = new IFCLinkedFileExportAs(configuration.ExportLinkedFiles);
                        mapData.Add(s_setupExportLinkedFiles, exportLinkedFiles.ToString());
#else
                        mapData.Add(s_setupExportLinkedFiles, configuration.ExportLinkedFiles.ToString());
#endif
                        mapData.Add(s_setupIncludeSiteElevation, configuration.IncludeSiteElevation.ToString());
                        mapData.Add(s_setupStoreIFCGUID, configuration.StoreIFCGUID.ToString());
                        mapData.Add(s_setupActivePhase, configuration.ActivePhaseId.ToString());
                        mapData.Add(s_setupExportRoomsInView, configuration.ExportRoomsInView.ToString());
                        mapData.Add(s_useOnlyTriangulation, configuration.UseOnlyTriangulation.ToString());
                        mapData.Add(s_excludeFilter, configuration.ExcludeFilter.ToString());
                        mapData.Add(s_setupSitePlacement, configuration.SitePlacement.ToString());
                        mapData.Add(s_useTypeNameOnlyForIfcType, configuration.UseTypeNameOnlyForIfcType.ToString());
                        mapData.Add(s_useVisibleRevitNameAsEntityName, configuration.UseVisibleRevitNameAsEntityName.ToString());
                        mapData.Add(s_setupTessellationLevelOfDetail, configuration.TessellationLevelOfDetail.ToString());
#if SinceRVT2025
                        mapData.Add(s_exportHostAsSingleEntity, configuration.ExportHostAsSingleEntity.ToString());
                        mapData.Add(s_ownerHistoryLastModified, configuration.OwnerHistoryLastModified.ToString());
                        mapData.Add(s_exportBarsInUniformRebarSetsAsSeparateIFCEntities, configuration.ExportBarsInUniformSetsAsSeparateIFCEntities.ToString());
                        mapData.Add(s_categoryMapping, configuration.CategoryMapping.ToString());
#if SinceRVT2027
                        mapData.Add(s_propertyMapping, configuration.PropertyMapping?.ToString() ?? string.Empty);
#endif
#endif
#if !SinceRVT2025
                        // For COBie v2.4
                        mapData.Add(s_cobieCompanyInfo, configuration.COBieCompanyInfo);
                        mapData.Add(s_cobieProjectInfo, configuration.COBieProjectInfo);
#endif
                        mapData.Add(s_includeSteelElements, configuration.IncludeSteelElements.ToString());
                        // Geo Reference info
                        mapData.Add(s_geoRefCRSName, string.IsNullOrEmpty(configuration.GeoRefCRSName) ? string.Empty : configuration.GeoRefCRSName);
                        mapData.Add(s_geoRefCRSDesc, string.IsNullOrEmpty(configuration.GeoRefCRSDesc) ? string.Empty : configuration.GeoRefCRSName);
                        mapData.Add(s_geoRefEPSGCode, string.IsNullOrEmpty(configuration.GeoRefEPSGCode) ? string.Empty : configuration.GeoRefCRSName);
                        mapData.Add(s_geoRefGeodeticDatum, string.IsNullOrEmpty(configuration.GeoRefGeodeticDatum) ? string.Empty : configuration.GeoRefCRSName);
                        mapData.Add(s_geoRefMapUnit, string.IsNullOrEmpty(configuration.GeoRefMapUnit) ? string.Empty : configuration.GeoRefCRSName);

                        mapEntity.Set<IDictionary<string, String>>(s_configMapField, mapData);
                        configStorage.SetEntity(mapEntity);
                    }

                    List<ElementId> elementsToDelete = new List<ElementId>();
                    for (; savedConfigurationIndex < savedConfigurationCount; savedConfigurationIndex++)
                    {
                        DataStorage configStorage = savedConfigurations[savedConfigurationIndex];
                        elementsToDelete.Add(configStorage.Id);
                    }
                    if (elementsToDelete.Count > 0)
                        document.Delete(elementsToDelete);

                    transaction.Commit();
                }
                catch (System.Exception)
                {
                    if (transaction.HasStarted())
                        transaction.RollBack();

                    return false;
                }
            }

            return true;
        }

        public bool ConvertSavedConfigurationsFromLegacyToJson(Document document)
        {
            // delete the old schema and the DataStorage.
            if (m_OldSchema == null)
            {
                m_OldSchema = Schema.Lookup(s_OldSchemaId);
            }
            if (m_OldSchema != null)
            {
                IList<DataStorage> oldSavedConfigurations = GetSavedConfigurations(m_OldSchema, document);
                if (oldSavedConfigurations.Count > 0)
                {
                    Transaction deleteTransaction = new Transaction(document, "Delete old IFC export setups");
                    try
                    {
                        deleteTransaction.Start("Delete old configuration");
                        List<ElementId> dataStorageToDelete = new List<ElementId>();
                        foreach (DataStorage dataStorage in oldSavedConfigurations)
                        {
                            dataStorageToDelete.Add(dataStorage.Id);
                        }
                        document.Delete(dataStorageToDelete);
                        deleteTransaction.Commit();
                    }
                    catch (System.Exception)
                    {
                        if (deleteTransaction.HasStarted())
                            deleteTransaction.RollBack();

                        return false;
                    }
                }
            }

            // delete the old schema and the DataStorage.
            if (m_mapSchema == null)
            {
                m_mapSchema = Schema.Lookup(s_mapSchemaId);
            }
            if (m_mapSchema != null)
            {
                IList<DataStorage> oldSavedConfigurations = GetSavedConfigurations(m_mapSchema, document);
                if (oldSavedConfigurations.Count > 0)
                {
                    Transaction deleteTransaction = new Transaction(document, "Delete old configuration");
                    try
                    {
                        deleteTransaction.Start("Delete old configuration");
                        List<ElementId> dataStorageToDelete = new List<ElementId>();
                        foreach (DataStorage dataStorage in oldSavedConfigurations)
                        {
                            dataStorageToDelete.Add(dataStorage.Id);
                        }
                        document.Delete(dataStorageToDelete);
                        deleteTransaction.Commit();
                    }
                    catch (System.Exception)
                    {
                        if (deleteTransaction.HasStarted())
                            deleteTransaction.RollBack();

                        return false;
                    }
                }
            }

            // update the configurations to new map schema.
            if (m_jsonSchema == null)
            {
                m_jsonSchema = Schema.Lookup(s_jsonSchemaId);
            }

            // Are there any setups to save or resave?
            List<IFCExportConfiguration> setupsToSave = new List<IFCExportConfiguration>();
            foreach (IFCExportConfiguration configuration in m_configurations.Values)
            {
                if (configuration.IsBuiltIn)
                    continue;

                // Store in-session settings in the cached in-session configuration
                if (configuration.IsInSession)
                {
                    IFCExportConfiguration.SetInSession(configuration);
                    continue;
                }

                setupsToSave.Add(configuration);
            }

            // If there are no setups to save, and if the schema is not present (which means there are no
            // previously existing setups which might have been deleted) we can skip the rest of this method.
            if (setupsToSave.Count <= 0 && m_jsonSchema == null)
                return false;

            if (m_jsonSchema == null)
            {
                SchemaBuilder builder = new SchemaBuilder(s_jsonSchemaId);
                builder.SetSchemaName("IFCExportConfigurationMap");
                builder.AddSimpleField(s_configMapField, typeof(String));
                m_jsonSchema = builder.Finish();
            }

            // It won't start any transaction if there is no change to the configurations
            if (setupsToSave.Count > 0)
            {
                // Overwrite all saved configs with the new list
                Transaction transaction = new Transaction(document, "Update IFC export setups");
                try
                {
                    transaction.Start("SaveConfigurationChanges");
                    IList<DataStorage> savedConfigurations = GetSavedConfigurations(m_jsonSchema, document);
                    int savedConfigurationCount = savedConfigurations.Count<DataStorage>();
                    int savedConfigurationIndex = 0;
                    foreach (IFCExportConfiguration configuration in setupsToSave)
                    {
                        DataStorage configStorage;
                        if (savedConfigurationIndex >= savedConfigurationCount)
                        {
                            configStorage = DataStorage.Create(document);
                        }
                        else
                        {
                            configStorage = savedConfigurations[savedConfigurationIndex];
                            savedConfigurationIndex++;
                        }

                        Entity mapEntity = new Entity(m_jsonSchema);
                        string configData = configuration.SerializeConfigToJson();
                        mapEntity.Set<string>(s_configMapField, configData);
                        configStorage.SetEntity(mapEntity);
                    }

                    List<ElementId> elementsToDelete = new List<ElementId>();
                    for (; savedConfigurationIndex < savedConfigurationCount; savedConfigurationIndex++)
                    {
                        DataStorage configStorage = savedConfigurations[savedConfigurationIndex];
                        elementsToDelete.Add(configStorage.Id);
                    }
                    if (elementsToDelete.Count > 0)
                        document.Delete(elementsToDelete);

                    transaction.Commit();
                }
                catch (System.Exception)
                {
                    if (transaction.HasStarted())
                        transaction.RollBack();

                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
