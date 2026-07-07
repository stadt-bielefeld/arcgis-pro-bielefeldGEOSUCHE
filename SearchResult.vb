Imports System.Text.Json.Serialization

Public Class SearchResult

    <JsonPropertyName("label")>
    Public Property Label As String

    <JsonPropertyName("geom")>
    Public Property Geom As String

    <JsonPropertyName("sml")>
    Public Property Sml As Object

End Class