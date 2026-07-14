Imports ArcGIS.Desktop.Framework.Contracts
Imports ArcGIS.Desktop.Framework.Dialogs

Friend Class AddressSelectionButton
    Inherits Button

    Protected Overrides Async Sub OnClick()

        Try
            Await SearchResultsDockPaneViewModel.ShowAddressSelectionAsync()
        Catch ex As Exception
            MessageBox.Show(ex.ToString(), "Fehler in der Adress-Auswahlsuche")
        End Try

    End Sub

End Class