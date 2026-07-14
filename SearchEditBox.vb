Imports ArcGIS.Desktop.Framework.Contracts
Imports System.Threading
Imports System.Threading.Tasks

Friend Class SearchEditBox
    Inherits EditBox

    Private Const PlaceholderText As String = "Suche"

    Private _debounceCts As CancellationTokenSource
    Private _isPlaceholderActive As Boolean = True

    Public Sub New()
        Text = PlaceholderText
    End Sub

    Protected Overrides Sub OnTextChange(text As String)
        MyBase.OnTextChange(text)

        If _isPlaceholderActive AndAlso text = PlaceholderText Then
            Module1.Current.CurrentSearchText = ""
            Return
        End If

        _isPlaceholderActive = False

        Module1.Current.CurrentSearchText = text

        If _debounceCts IsNot Nothing Then
            Try
                _debounceCts.Cancel()
            Catch
                ' Ignorieren
            End Try
        End If

        If String.IsNullOrWhiteSpace(text) OrElse
           text.Trim().Length < 3 OrElse
           text.Trim().Equals(PlaceholderText, StringComparison.OrdinalIgnoreCase) Then
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

            If String.IsNullOrWhiteSpace(searchText) OrElse
               searchText.Trim().Equals(PlaceholderText, StringComparison.OrdinalIgnoreCase) Then
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