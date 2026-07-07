Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Input
Imports ArcGIS.Core.CIM
Imports ArcGIS.Core.Data
Imports ArcGIS.Core.Geometry
Imports ArcGIS.Desktop.Catalog
Imports ArcGIS.Desktop.Core
Imports ArcGIS.Desktop.Editing
Imports ArcGIS.Desktop.Extensions
Imports ArcGIS.Desktop.Framework
Imports ArcGIS.Desktop.Framework.Contracts
Imports ArcGIS.Desktop.Framework.Dialogs
Imports ArcGIS.Desktop.Framework.Threading.Tasks
Imports ArcGIS.Desktop.Layouts
Imports ArcGIS.Desktop.Mapping


Friend Class Module1
    Inherits ArcGIS.Desktop.Framework.Contracts.Module

    Private Shared _this As Module1 = Nothing

    Public Shared ReadOnly Property Current As Module1
        Get
            If _this Is Nothing Then
                _this = TryCast(FrameworkApplication.FindModule("BielefeldSearch_Module"), Module1)
            End If

            Return _this
        End Get
    End Property

    Public Property CurrentSearchText As String = "Niederwall"

End Class