Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports ArcGIS.Desktop.Framework.Dialogs

Public Class SearchService

    Private Shared ReadOnly _httpClient As New HttpClient With {
        .Timeout = TimeSpan.FromSeconds(10)
    }

    Public Shared Async Function SearchAsync(term As String, cancellationToken As CancellationToken) As Task(Of List(Of SearchResult))

        If String.IsNullOrWhiteSpace(term) OrElse term.Trim().Length < 3 Then
            Return New List(Of SearchResult)()
        End If

        Dim encodedTerm As String = Uri.EscapeDataString(term.Trim())
        Dim url As String = $"https://stadtplan.bielefeld.de/search/?term={encodedTerm}"

        'MessageBox.Show("HTTP-Aufruf startet:" & vbCrLf & url, "Debug SearchService")

        Using response As HttpResponseMessage = Await _httpClient.GetAsync(url, cancellationToken)

            'MessageBox.Show(
            '    "HTTP-Status: " & CInt(response.StatusCode).ToString() & " " & response.ReasonPhrase,
            '    "Debug SearchService"
            ')

            response.EnsureSuccessStatusCode()

            Dim json As String = Await response.Content.ReadAsStringAsync(cancellationToken)

            'MessageBox.Show("JSON:" & vbCrLf & json, "Debug SearchService")

            Dim options As New JsonSerializerOptions With {
                .PropertyNameCaseInsensitive = True
            }

            Dim results = JsonSerializer.Deserialize(Of List(Of SearchResult))(json, options)

            'MessageBox.Show(
            '    "Anzahl: " & If(results Is Nothing, "Nothing", results.Count.ToString()),
            '    "Debug SearchService"
            ')

            If results Is Nothing Then
                Return New List(Of SearchResult)()
            End If

            Return results
        End Using

    End Function

End Class