Imports ArcGIS.Desktop.Framework.Contracts
Imports ArcGIS.Desktop.Framework.Dialogs

Friend Class SearchButton
    Inherits Button

    Protected Overrides Async Sub OnClick()
        Dim searchText As String = Module1.Current.CurrentSearchText

        If String.IsNullOrWhiteSpace(searchText) Then
            MessageBox.Show("Kein Suchbegriff vorhanden.", "Suche")
            Return
        End If

        'MessageBox.Show("Suche nach: " & searchText, "Suche")

        Await SearchResultsDockPaneViewModel.ShowAndSearchAsync(searchText)
    End Sub

End Class