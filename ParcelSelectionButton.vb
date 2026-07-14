Imports ArcGIS.Desktop.Framework.Contracts
Imports ArcGIS.Desktop.Framework.Dialogs

Friend Class ParcelSelectionButton
    Inherits Button

    Protected Overrides Async Sub OnClick()

        Try
            Await SearchResultsDockPaneViewModel.ShowParcelSelectionAsync()
        Catch ex As Exception
            MessageBox.Show(ex.ToString(), "Fehler in der Flurstück-Auswahlsuche")
        End Try

    End Sub

End Class