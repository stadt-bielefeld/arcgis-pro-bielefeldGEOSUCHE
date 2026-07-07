Imports System.Windows.Controls
Imports ArcGIS.Desktop.Framework



Partial Public Class SearchResultsDockPaneView
        Inherits UserControl

        Public Sub New()
            InitializeComponent()

            AddHandler Me.Loaded, AddressOf SearchResultsDockPaneView_Loaded
        End Sub

        Private Sub SearchResultsDockPaneView_Loaded(sender As Object, e As Windows.RoutedEventArgs)
            Dim pane = FrameworkApplication.DockPaneManager.Find("BielefeldSearch_ResultsDockPane")
            Me.DataContext = pane
        End Sub

    End Class

