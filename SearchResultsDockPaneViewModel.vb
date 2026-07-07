Imports ArcGIS.Core.Geometry
Imports ArcGIS.Desktop.Framework
Imports ArcGIS.Desktop.Framework.Contracts
Imports ArcGIS.Desktop.Framework.Threading.Tasks
Imports ArcGIS.Desktop.Mapping
Imports System.Collections.ObjectModel
Imports System.Threading
Imports System.Threading.Tasks
Imports ArcGIS.Desktop.Framework.Dialogs
Imports ArcGIS.Core.CIM
Friend Class SearchResultsDockPaneViewModel
    Inherits DockPane

    Private Const DockPaneId As String = "BielefeldSearch_ResultsDockPane"
    Private Shared _highlightOverlay As IDisposable
    Private _selectedResult As SearchResult
    Private Shared _searchCts As CancellationTokenSource

    Public Sub New()
        Results = New ObservableCollection(Of SearchResult)()
    End Sub

    Private _results As ObservableCollection(Of SearchResult)

    Public Property Results As ObservableCollection(Of SearchResult)
        Get
            Return _results
        End Get
        Set(value As ObservableCollection(Of SearchResult))
            SetProperty(_results, value, NameOf(Results))
        End Set
    End Property

    Public Property SelectedResult As SearchResult
        Get
            Return _selectedResult
        End Get
        Set(value As SearchResult)
            If SetProperty(_selectedResult, value, NameOf(SelectedResult)) Then

                If value IsNot Nothing Then
                    StartZoomToResult(value)
                End If
            End If
        End Set
    End Property

    Private Async Sub StartZoomToResult(result As SearchResult)
        Await ZoomToResultAsync(result)
    End Sub

    Public Shared Async Function ShowAndSearchAsync(term As String) As Task

        'MessageBox.Show("ShowAndSearchAsync wurde aufgerufen mit: " & term, "Debug")

        Dim dockPane = FrameworkApplication.DockPaneManager.Find(DockPaneId)

        If dockPane Is Nothing Then
            MessageBox.Show(
            "DockPane wurde nicht gefunden." & vbCrLf &
            "Gesuchte ID: " & DockPaneId,
            "Debug"
        )
            Return
        End If

        Dim pane = TryCast(dockPane, SearchResultsDockPaneViewModel)

        If pane Is Nothing Then
            MessageBox.Show(
            "DockPane wurde gefunden, aber hat den falschen Typ." & vbCrLf &
            "Typ: " & dockPane.GetType().FullName,
            "Debug"
        )
            Return
        End If

        pane.Activate()

        'MessageBox.Show("DockPane gefunden. Suche startet jetzt.", "Debug")

        Await pane.SearchInternalAsync(term)

    End Function

    Private Function CreateHighlightSymbol(geometry As Geometry) As CIMSymbolReference

        Select Case geometry.GeometryType

            Case GeometryType.Point, GeometryType.Multipoint

                Dim addInFolder As String = System.IO.Path.GetDirectoryName(
    GetType(SearchResultsDockPaneViewModel).Assembly.Location
)

                Dim markerPath As String = System.IO.Path.Combine(
                    addInFolder,
                    "Images",
                    "geocoder-marker.svg"
                )

                Dim pictureMarker = SymbolFactory.Instance.ConstructMarkerFromFile(markerPath)

                pictureMarker.Size = 28
                pictureMarker.OffsetY = 14

                Dim pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(pictureMarker)

                Return pointSymbol.MakeSymbolReference()

            Case GeometryType.Polyline

                Dim lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
                ColorFactory.Instance.RedRGB,
                4.0,
                SimpleLineStyle.Solid
            )

                Return lineSymbol.MakeSymbolReference()

            Case GeometryType.Polygon, GeometryType.Envelope

                Dim outline = SymbolFactory.Instance.ConstructStroke(
                ColorFactory.Instance.RedRGB,
                3.0,
                SimpleLineStyle.Solid
            )

                Dim fillSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                ColorFactory.Instance.CreateRGBColor(255, 0, 0, 25),
                SimpleFillStyle.Solid,
                outline
            )

                Return fillSymbol.MakeSymbolReference()

            Case Else

                Dim defaultSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                ColorFactory.Instance.RedRGB,
                14,
                SimpleMarkerStyle.Circle
            )

                Return defaultSymbol.MakeSymbolReference()

        End Select

    End Function

    Public Shared Sub ClearHighlight()

        If _highlightOverlay IsNot Nothing Then
            _highlightOverlay.Dispose()
            _highlightOverlay = Nothing
        End If

    End Sub

    Private Async Function SearchInternalAsync(term As String) As Task

        If _searchCts IsNot Nothing Then
            Try
                _searchCts.Cancel()
            Catch
                ' Ignorieren
            End Try
        End If

        _searchCts = New CancellationTokenSource()
        Dim token = _searchCts.Token

        Results.Clear()

        If String.IsNullOrWhiteSpace(term) OrElse term.Trim().Length < 3 Then
            Return
        End If

        Try


            Dim hits = Await SearchService.SearchAsync(term, token)

            If token.IsCancellationRequested Then
                Return
            End If

            Results.Clear()

            For Each hit In hits
                Results.Add(hit)
            Next

            'MessageBox.Show(
            '"Treffer von SearchService: " & hits.Count.ToString() & vbCrLf &
            '"Treffer in Results: " & Results.Count.ToString() & vbCrLf &
            '"Erster Treffer: " & If(Results.Count > 0, Results(0).Label, "-"),
            '"Debug Results"
            ')

        Catch ex As OperationCanceledException
            ' Ignorieren.
        Catch ex As Exception
            MessageBox.Show(ex.ToString(), "Fehler in SearchInternalAsync")

            Results.Clear()
            Results.Add(New SearchResult With {
        .Label = "Fehler bei der Suche: " & ex.Message,
        .Geom = Nothing,
        .Sml = 0
    })
        End Try

    End Function

    Private Async Function ZoomToResultAsync(result As SearchResult) As Task

        If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.Geom) Then
            Return
        End If

        If MapView.Active Is Nothing Then
            Return
        End If

        Await QueuedTask.Run(Sub()

                                 ' API liefert Bielefeld-Koordinaten wahrscheinlich in EPSG:25832.
                                 Dim sourceSr As SpatialReference = SpatialReferenceBuilder.CreateSpatialReference(25832)

                                 Dim geometry As Geometry = GeometryEngine.Instance.ImportFromWKT(
                                     WktImportFlags.WktImportDefaults,
                                     result.Geom,
                                     sourceSr
                                 )

                                 If geometry Is Nothing Then
                                     Return
                                 End If

                                 Dim targetGeometry As Geometry = geometry

                                 If MapView.Active.Map IsNot Nothing AndAlso
   MapView.Active.Map.SpatialReference IsNot Nothing AndAlso
   MapView.Active.Map.SpatialReference.Wkid <> sourceSr.Wkid Then

                                     targetGeometry = GeometryEngine.Instance.Project(
        geometry,
        MapView.Active.Map.SpatialReference
    )
                                 End If

                                 ' Vorheriges Highlight entfernen
                                 If _highlightOverlay IsNot Nothing Then
                                     _highlightOverlay.Dispose()
                                     _highlightOverlay = Nothing
                                 End If

                                 ' Neues Highlight erzeugen
                                 Dim highlightSymbol = CreateHighlightSymbol(targetGeometry)
                                 _highlightOverlay = MapView.Active.AddOverlay(targetGeometry, highlightSymbol)


                                 If TypeOf targetGeometry Is MapPoint Then

                                     ' Punkte immer im Maßstab 1:2000 anzeigen.
                                     Dim point = DirectCast(targetGeometry, MapPoint)

                                     Dim camera As Camera = MapView.Active.Camera
                                     camera.X = point.X
                                     camera.Y = point.Y
                                     camera.Scale = 2000

                                     MapView.Active.ZoomTo(camera, TimeSpan.FromMilliseconds(300))

                                 Else

                                     Dim extent As Envelope = targetGeometry.Extent

                                     If extent Is Nothing OrElse extent.IsEmpty Then
                                         Return
                                     End If

                                     ' Etwas Rand um die Geometrie geben, damit das Highlight nicht am Kartenrand klebt.
                                     Dim paddedExtent As Envelope = extent.Expand(1.2, 1.2, True)

                                     ' Erst auf die komplette Geometrie zoomen.
                                     MapView.Active.ZoomTo(paddedExtent, TimeSpan.FromMilliseconds(300))

                                     ' Danach prüfen: Wenn der resultierende Maßstab kleiner/weiter draußen ist als 1:2000,
                                     ' dann beibehalten. Wenn ArcGIS sehr weit reingezoomt hat, maximal auf 1:2000 setzen.
                                     Dim camera As Camera = MapView.Active.Camera

                                     If camera.Scale < 2000 Then
                                         camera.Scale = 2000
                                         MapView.Active.ZoomTo(camera, TimeSpan.FromMilliseconds(100))
                                     End If

                                 End If

                             End Sub)
        Await FrameworkApplication.SetCurrentToolAsync("BielefeldSearch_ClearHighlightMapTool")

    End Function

End Class