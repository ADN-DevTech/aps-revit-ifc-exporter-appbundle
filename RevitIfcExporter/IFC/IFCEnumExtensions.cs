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

using Autodesk.Revit.DB;
#if SinceRVT2025
using Revit.IFC.Export.Toolkit.IFC4x3;
#endif
using Revit.IFC.Common.Enums;

namespace BIM.IFC.Export
{
    internal static class IFCEnumExtensions
    {
        /// <summary>
        /// Converts the <see cref="IFCVersion"/> to string.
        /// </summary>
        /// <returns>The string of IFCVersion.</returns>
        public static string ToLabel(this IFCVersion version)
        {
            switch (version)
            {
                case IFCVersion.IFC2x2:
                    return "IFC 2x2 Coordination View";
                case IFCVersion.IFC2x3:
                    return "IFC 2x3 Coordination View";
                case IFCVersion.IFCBCA:
                case IFCVersion.IFC2x3CV2:
                    return "IFC 2x3 Coordination View 2.0";
                case IFCVersion.IFC4:
                    return "IFC4 for General Use";
                case IFCVersion.IFCCOBIE:
                    return "IFC 2x3 GSA Concept Design BIM 2010";
                case IFCVersion.IFC2x3FM:
                    return "IFC2x3 COBie 2.4 Design Deliverable View";
                case IFCVersion.IFC4DTV:
                    return "IFC4 Design Transfer View";
                case IFCVersion.IFC4RV:
                    return "IFC4 Reference Vie";
                case IFCVersion.IFC2x3BFM:
                    return "IFC 2x3 Basic FM Handover View";
#if SinceRVT2023 && !SinceRVT2027
                case IFCVersion.IFC4x3:
                    return "IFC4x3";
#endif
#if SinceRVT2027
                case IFCVersion.IFC4x3RV:
                    return "IFC 4.3 Reference View";
                case IFCVersion.IFC4x3DTV:
                    return "IFC 4.3 Design Transfer View";
#endif
#if SinceRVT2024
                case IFCVersion.IFCSG:
                    return "IFC-SG Regulatory Requirements View";
#endif
                default:
                    return "Unrecognized IFC version";
            }
        }

        /// <summary>
        /// Converts the <see cref="KnownERNames"/> to string.
        /// </summary>
        /// <returns>The string of .</returns>
        public static string ToShortLabel(this KnownERNames erName)
        {
            switch (erName)
            {
                case KnownERNames.Architecture:
                    return "Architecture";
                case KnownERNames.BuildingService:
                    return "BuildingService";
                case KnownERNames.Structural:
                    return "Structural";
                default:
                    return string.Empty;
            }
        }


        /// <summary>
        /// Get the UI Name for the Exchange Requirement (ER). Note that this string may be localized
        /// </summary>
        /// <param name="erEnum">The ER Enum value</param>
        /// <returns>The localized ER name string</returns>
        public static string ToFullLabel(this KnownERNames erEnum)
        {
            switch (erEnum)
            {
                case KnownERNames.Architecture:
                    return "Architecture";
                case KnownERNames.BuildingService:
                    return "MEP Reference Exchange";
                case KnownERNames.Structural:
                    return "Structural Reference Exchange";
                default:
                    return string.Empty;
            }
        }

#if SinceRVT2025

        /// <summary>
        /// Get the UI Name for the Facility type. Note that this string may be localized.
        /// </summary>
        /// <param name="facilityTypeEnum">The facility type enum value.</param>
        /// <returns>The localized facility type name.</returns>
        public static string ToFullLabel(this KnownFacilityTypes facilityTypeEnum)
        {
            switch (facilityTypeEnum)
            {
                case KnownFacilityTypes.Bridge:
                    return "Bridge (IfcBridge)";
                case KnownFacilityTypes.Building:
                    return "Building (IfcBuilding)";
                case KnownFacilityTypes.MarineFacility:
                    return "Marine Facility (IfcMarineFacility)";
                case KnownFacilityTypes.Railway:
                    return "Railway (IfcRailway)";
                case KnownFacilityTypes.Road:
                    return "Road (IfcRoad)";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Get the UI Name for the IfcBridgeTypeEnum. Note that this string may be localized.
        /// </summary>
        /// <param name="bridgeTypeEnum">The bridge type enum value.</param>
        /// <returns>The localized bridge type name.</returns>
        public static string ToFullLabel(this IFCBridgeType facilityTypeEnum)
        {
            switch (facilityTypeEnum)
            {
                case IFCBridgeType.ARCHED:
                    return "Arched (ARCHED)";
                case IFCBridgeType.CABLE_STAYED:
                    return "Cable Stayed (CABLESTAYED)";
                case IFCBridgeType.CANTILEVER:
                    return "Cantilever (CANTILEVER)";
                case IFCBridgeType.CULVERT:
                    return "Culvert (CULVERT)";
                case IFCBridgeType.FRAMEWORK:
                    return "Framework (FRAMEWORK)";
                case IFCBridgeType.GIRDER:
                    return "Girder (GIRDER)";
                case IFCBridgeType.NOTDEFINED:
                    return "Not Defined";
                case IFCBridgeType.SUSPENSION:
                    return "Suspension (SUSPENSION)";
                case IFCBridgeType.TRUSS:
                    return "Truss (TRUSS)";
                case IFCBridgeType.USERDEFINED:
                    return "User Defined";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Get the UI Name for the IfcMarineFacilityTypeEnum. Note that this string may be localized.
        /// </summary>
        /// <param name="marineFacilityTypeEnum">The marine facility type enum value.</param>
        /// <returns>The localized marine facility type name.</returns>
        public static string ToFullLabel(this IFCMarineFacilityType facilityTypeEnum)
        {
            switch (facilityTypeEnum)
            {
                case IFCMarineFacilityType.BARRIERBEACH:
                    return "Barrier Beach (BARRIERBEACH)";
                case IFCMarineFacilityType.BREAKWATER:
                    return "Breakwater (BREAKWATER)";
                case IFCMarineFacilityType.CANAL:
                    return "Canal (CANAL)";
                case IFCMarineFacilityType.DRYDOCK:
                    return "Dry Dock (DRYDOCK)";
                case IFCMarineFacilityType.FLOATINGDOCK:
                    return "Floating Dock (FLOATINGDOCK)";
                case IFCMarineFacilityType.HYDROLIFT:
                    return "Hydrolift (HYDROLIFT)";
                case IFCMarineFacilityType.JETTY:
                    return "Jetty (JETTY)";
                case IFCMarineFacilityType.LAUNCHRECOVERY:
                    return "Launch Recovery Facility (LAUNCHRECOVERYFACILITY)";
                case IFCMarineFacilityType.MARINEDEFENCE:
                    return "Marine Defense (MARINEDEFENCE)";
                case IFCMarineFacilityType.NAVIGATIONALCHANNEL:
                    return "Navigational Channel (NAVIGATIONALCHANNEL)";
                case IFCMarineFacilityType.NOTDEFINED:
                    return "Not Defined";
                case IFCMarineFacilityType.PORT:
                    return "Port (PORT)";
                case IFCMarineFacilityType.QUAY:
                    return "Quay (QUAY)";
                case IFCMarineFacilityType.REVETMENT:
                    return "Revetment (REVETMENT)";
                case IFCMarineFacilityType.SHIPLIFT:
                    return "Shiplift (SHIPLIFT)";
                case IFCMarineFacilityType.SHIPLOCK:
                    return "Ship Lock (SHIPLOCK)";
                case IFCMarineFacilityType.SHIPYARD:
                    return "Shipyard (SHIPYARD";
                case IFCMarineFacilityType.SLIPWAY:
                    return "Slipway (SLIPWAY)";
                case IFCMarineFacilityType.USERDEFINED:
                    return "User Defined";
                case IFCMarineFacilityType.WATERWAY:
                    return "Waterway (WATERWAY)";
                case IFCMarineFacilityType.WATERWAYSHIPLIFT:
                    return "Waterway Shiplift (WATERWAYSHIPLIFT)";
                default:
                    return string.Empty;
            }
        }
#endif
    }
}