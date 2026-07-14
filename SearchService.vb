Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Public Class SearchService

    Private Shared ReadOnly _httpClient As New HttpClient With {
        .Timeout = TimeSpan.FromSeconds(10)
    }

    Private Shared ReadOnly _jsonOptions As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True
    }

    Public Shared Async Function SearchAsync(term As String, cancellationToken As CancellationToken) As Task(Of List(Of SearchResult))

        If String.IsNullOrWhiteSpace(term) OrElse term.Trim().Length < 3 Then
            Return New List(Of SearchResult)()
        End If

        Dim encodedTerm As String = Uri.EscapeDataString(term.Trim())
        Dim url As String = $"https://stadtplan.bielefeld.de/search/?term={encodedTerm}"

        Return Await GetResultsAsync(url, cancellationToken)

    End Function

    Public Shared Async Function SearchCatalogAsync(catalog As String, term As String, cancellationToken As CancellationToken) As Task(Of List(Of SearchResult))

        If String.IsNullOrWhiteSpace(catalog) Then
            Return New List(Of SearchResult)()
        End If

        Dim encodedCatalog As String = Uri.EscapeDataString(catalog.Trim())
        Dim url As String = $"https://stadtplan.bielefeld.de/search/?catalog={encodedCatalog}"

        If Not String.IsNullOrWhiteSpace(term) Then
            url &= $"&term={Uri.EscapeDataString(term.Trim())}"
        End If

        Return Await GetResultsAsync(url, cancellationToken)

    End Function

    Private Shared Async Function GetResultsAsync(url As String, cancellationToken As CancellationToken) As Task(Of List(Of SearchResult))

        Using response As HttpResponseMessage = Await _httpClient.GetAsync(url, cancellationToken)

            response.EnsureSuccessStatusCode()

            Dim json As String = Await response.Content.ReadAsStringAsync(cancellationToken)

            Dim results = JsonSerializer.Deserialize(Of List(Of SearchResult))(json, _jsonOptions)

            If results Is Nothing Then
                Return New List(Of SearchResult)()
            End If

            Return results

        End Using

    End Function

End Class