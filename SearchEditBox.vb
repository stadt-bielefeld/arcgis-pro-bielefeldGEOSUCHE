Imports ArcGIS.Desktop.Framework.Contracts
Imports System.Threading
Imports System.Threading.Tasks

Friend Class SearchEditBox
    Inherits EditBox

    Private _debounceCts As CancellationTokenSource

    Protected Overrides Sub OnTextChange(text As String)
        MyBase.OnTextChange(text)

        Module1.Current.CurrentSearchText = text

        If _debounceCts IsNot Nothing Then
            Try
                _debounceCts.Cancel()
            Catch
                ' Ignorieren
            End Try
        End If

        If String.IsNullOrWhiteSpace(text) OrElse text.Trim().Length < 3 Then
            Return
        End If

        _debounceCts = New CancellationTokenSource()

        DebouncedSearchAsync(text, _debounceCts.Token)
    End Sub

    Private Async Sub DebouncedSearchAsync(searchText As String, token As CancellationToken)
        Try
            Await Task.Delay(350, token)

            If token.IsCancellationRequested Then
                Return
            End If

            Await SearchResultsDockPaneViewModel.ShowAndSearchAsync(searchText)

        Catch ex As OperationCanceledException
            ' Ignorieren: neuer Tastendruck kam dazwischen.
        Catch ex As Exception
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                ex.ToString(),
                "Fehler in der Suche"
            )
        End Try
    End Sub

End Class