Imports System.Collections.ObjectModel
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Controls
Imports System.Windows.Input
Imports ArcGIS.Core.CIM
Imports ArcGIS.Core.Geometry
Imports ArcGIS.Desktop.Framework
Imports ArcGIS.Desktop.Framework.Contracts
Imports ArcGIS.Desktop.Framework.Dialogs
Imports ArcGIS.Desktop.Framework.Threading.Tasks
Imports ArcGIS.Desktop.Mapping
Friend Class SearchResultsDockPaneViewModel
    Inherits DockPane

    Private Const DockPaneId As String = "BielefeldSearch_ResultsDockPane"
    Private Shared _highlightOverlay As IDisposable
    Private _selectedResult As SearchResult
    Private Shared _searchCts As CancellationTokenSource

    Private Enum SelectionSearchMode
        None
        Address
        Parcel
    End Enum

    Private Class SelectionHistoryEntry
        Public Property Mode As SelectionSearchMode
        Public Property StepNumber As Integer
        Public Property Catalog As String
        Public Property Term As String
        Public Property Info As String
        Public Property PathParts As List(Of String)
    End Class

    Private _selectionMode As SelectionSearchMode = SelectionSearchMode.None
    Private _selectionStep As Integer = 0
    Private _selectionInfo As String
    Private _selectionPath As String
    Private _canGoBack As Boolean
    Private _currentCatalog As String
    Private _currentTerm As String
    Private ReadOnly _selectionHistory As New Stack(Of SelectionHistoryEntry)
    Private ReadOnly _selectionPathParts As New List(Of String)

    Private _ignoreSelectedResultChanges As Boolean = False
    Private _suppressSelectionUntil As DateTime = DateTime.MinValue

    Public Property BackCommand As ICommand

    Public Property SelectionInfo As String
        Get
            Return _selectionInfo
        End Get
        Set(value As String)
            SetProperty(_selectionInfo, value, NameOf(SelectionInfo))
        End Set
    End Property

    Public Property SelectionPath As String
        Get
            Return _selectionPath
        End Get
        Set(value As String)
            SetProperty(_selectionPath, value, NameOf(SelectionPath))
        End Set
    End Property

    Public Property CanGoBack As Boolean
        Get
            Return _canGoBack
        End Get
        Set(value As Boolean)
            SetProperty(_canGoBack, value, NameOf(CanGoBack))
        End Set
    End Property

    Public Sub New()
        Results = New ObservableCollection(Of SearchResult)()

        BackCommand = New DelegateCommand(
        Async Sub()
            Await GoBackAsync()
        End Sub
    )
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

                If _ignoreSelectedResultChanges Then
                    Return
                End If

                If DateTime.UtcNow < _suppressSelectionUntil Then
                    Return
                End If

                If value IsNot Nothing Then
                    StartHandleSelectedResult(value)
                End If

            End If

        End Set
    End Property

    Private Async Sub StartHandleSelectedResult(result As SearchResult)
        Await HandleSelectedResultAsync(result)
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

    Public Shared Async Function ShowAddressSelectionAsync() As Task

        Dim pane = GetAndActivatePane()

        If pane Is Nothing Then
            Return
        End If

        Await pane.StartAddressSelectionAsync()

    End Function

    Public Shared Async Function ShowParcelSelectionAsync() As Task

        Dim pane = GetAndActivatePane()

        If pane Is Nothing Then
            Return
        End If

        Await pane.StartParcelSelectionAsync()

    End Function

    Private Shared Function GetAndActivatePane() As SearchResultsDockPaneViewModel

        Dim dockPane = FrameworkApplication.DockPaneManager.Find(DockPaneId)

        If dockPane Is Nothing Then
            MessageBox.Show(
            "DockPane wurde nicht gefunden." & vbCrLf &
            "Gesuchte ID: " & DockPaneId,
            "Debug"
        )
            Return Nothing
        End If

        Dim pane = TryCast(dockPane, SearchResultsDockPaneViewModel)

        If pane Is Nothing Then
            MessageBox.Show(
            "DockPane wurde gefunden, aber hat den falschen Typ." & vbCrLf &
            "Typ: " & dockPane.GetType().FullName,
            "Debug"
        )
            Return Nothing
        End If

        pane.Activate()

        Return pane

    End Function

    Private Async Function StartAddressSelectionAsync() As Task

        _selectionMode = SelectionSearchMode.Address
        _selectionStep = 1
        _selectionHistory.Clear()
        ClearSelectionPath()
        UpdateCanGoBack()

        Await LoadCatalogResultsAsync(
            catalog:="getbuchstaben",
            term:=Nothing,
            nextStep:=1,
            info:="Adress-Auswahlsuche - Schritt 1: Anfangsbuchstaben auswählen",
            addToHistory:=False
        )

    End Function

    Private Async Function StartParcelSelectionAsync() As Task

        _selectionMode = SelectionSearchMode.Parcel
        _selectionStep = 1
        _selectionHistory.Clear()
        ClearSelectionPath()
        UpdateCanGoBack()

        Await LoadCatalogResultsAsync(
            catalog:="getgemarkungen",
            term:=Nothing,
            nextStep:=1,
            info:="Flurstück-Auswahlsuche - Schritt 1: Gemarkung auswählen",
            addToHistory:=False
        )

    End Function

    Private Async Function HandleSelectedResultAsync(result As SearchResult) As Task

        If result Is Nothing Then
            Return
        End If

        Dim term As String = GetSelectionTerm(result)

        If _selectionMode = SelectionSearchMode.Address Then

            Select Case _selectionStep

                Case 1
                    If String.IsNullOrWhiteSpace(term) Then
                        MessageBox.Show("Für diesen Buchstaben wurde kein Schlüsselwert gefunden.", "Adress-Auswahlsuche")
                        Return
                    End If

                    Await LoadCatalogResultsAsync(
                        catalog:="getstrassen",
                        term:=term,
                        nextStep:=2,
                        info:="Adress-Auswahlsuche - Schritt 2: Straße auswählen"
                    )

                    AddSelectionPathPart(result)

                    Return

                Case 2
                    If String.IsNullOrWhiteSpace(term) Then
                        MessageBox.Show("Für diese Straße wurde kein Schlüsselwert gefunden.", "Adress-Auswahlsuche")
                        Return
                    End If

                    Await LoadCatalogResultsAsync(
                        catalog:="gethausnummern",
                        term:=term,
                        nextStep:=3,
                        info:="Adress-Auswahlsuche - Schritt 3: Hausnummer auswählen"
                    )

                    AddSelectionPathPart(result)

                    Return

                Case 3
                    If Not String.IsNullOrWhiteSpace(result.Geom) Then
                        SetFinalSelectionPathPart(result)
                        Await ZoomToResultAsync(result)
                        Return
                    End If

                    MessageBox.Show("Die ausgewählte Hausnummer enthält keine Geometrie.", "Adress-Auswahlsuche")
                    Return

            End Select

        ElseIf _selectionMode = SelectionSearchMode.Parcel Then

            Select Case _selectionStep

                Case 1
                    If String.IsNullOrWhiteSpace(term) Then
                        MessageBox.Show("Für diese Gemarkung wurde kein Schlüsselwert gefunden.", "Flurstück-Auswahlsuche")
                        Return
                    End If

                    Await LoadCatalogResultsAsync(
                        catalog:="getflure",
                        term:=term,
                        nextStep:=2,
                        info:="Flurstück-Auswahlsuche - Schritt 2: Flur auswählen"
                    )

                    AddSelectionPathPart(result)

                    Return

                Case 2
                    If String.IsNullOrWhiteSpace(term) Then
                        MessageBox.Show("Für diese Flur wurde kein Schlüsselwert gefunden.", "Flurstück-Auswahlsuche")
                        Return
                    End If

                    Await LoadCatalogResultsAsync(
                        catalog:="getflurstuecke",
                        term:=term,
                        nextStep:=3,
                        info:="Flurstück-Auswahlsuche - Schritt 3: Flurstück auswählen"
                    )

                    AddSelectionPathPart(result)

                    Return

                Case 3
                    If Not String.IsNullOrWhiteSpace(result.Geom) Then
                        SetFinalSelectionPathPart(result)
                        Await ZoomToResultAsync(result)
                        Return
                    End If

                    MessageBox.Show("Das ausgewählte Flurstück enthält keine Geometrie.", "Flurstück-Auswahlsuche")
                    Return

            End Select

        End If

        ' Normale Suche:
        ' Hier darf direkt gezoomt werden, sobald eine Geometrie vorhanden ist.
        If Not String.IsNullOrWhiteSpace(result.Geom) Then
            Await ZoomToResultAsync(result)
            Return
        End If

        MessageBox.Show("Dieser Eintrag enthält keine Geometrie.", "Suche")

    End Function

    Private Function GetSelectionTerm(result As SearchResult) As String

        If result Is Nothing Then
            Return Nothing
        End If

        If Not String.IsNullOrWhiteSpace(result.Key) Then
            Return result.Key
        End If

        Return result.Label

    End Function

    Private Sub AddSelectionPathPart(result As SearchResult)

        If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.Label) Then
            Return
        End If

        _selectionPathParts.Add(result.Label.Trim())
        UpdateSelectionPathText()

    End Sub

    Private Sub SetFinalSelectionPathPart(result As SearchResult)

        If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.Label) Then
            Return
        End If

        ' In Schritt 3 sollen nur die ersten beiden Teile bestehen bleiben:
        ' Adresse: Buchstabe - Straße - Hausnummer
        ' Flurstück: Gemarkung - Flur - Flurstück
        While _selectionPathParts.Count > 2
            _selectionPathParts.RemoveAt(_selectionPathParts.Count - 1)
        End While

        _selectionPathParts.Add(result.Label.Trim())
        UpdateSelectionPathText()

    End Sub

    Private Sub UpdateSelectionPathText()

        If _selectionPathParts.Count = 0 Then
            SelectionPath = Nothing
            Return
        End If

        SelectionPath = String.Join(" - ", _selectionPathParts)

    End Sub

    Private Sub ClearSelectionPath()

        _selectionPathParts.Clear()
        SelectionPath = Nothing

    End Sub

    Private Sub RestoreSelectionPath(parts As List(Of String))

        _selectionPathParts.Clear()

        If parts IsNot Nothing Then
            _selectionPathParts.AddRange(parts)
        End If

        UpdateSelectionPathText()

    End Sub

    Private Sub UpdateCanGoBack()

        CanGoBack = _selectionHistory.Count > 0

    End Sub

    Private Async Function LoadCatalogResultsAsync(
        catalog As String,
        term As String,
        nextStep As Integer,
        info As String,
        Optional addToHistory As Boolean = True
    ) As Task

        If addToHistory AndAlso _selectionMode <> SelectionSearchMode.None AndAlso _selectionStep > 0 Then
            _selectionHistory.Push(New SelectionHistoryEntry With {
                .Mode = _selectionMode,
                .StepNumber = _selectionStep,
                .Catalog = _currentCatalog,
                .Term = _currentTerm,
                .Info = SelectionInfo,
                .PathParts = New List(Of String)(_selectionPathParts)
            })
        End If

        UpdateCanGoBack()

        If _searchCts IsNot Nothing Then
            Try
                _searchCts.Cancel()
            Catch
                ' Ignorieren
            End Try
        End If

        _searchCts = New CancellationTokenSource()
        Dim token = _searchCts.Token

        _ignoreSelectedResultChanges = True
        _suppressSelectionUntil = DateTime.UtcNow.AddMilliseconds(500)

        Try

            Results.Clear()
            SelectedResult = Nothing

            SelectionInfo = info
            _selectionStep = nextStep
            _currentCatalog = catalog
            _currentTerm = term

            Dim hits = Await SearchService.SearchCatalogAsync(catalog, term, token)

            If token.IsCancellationRequested Then
                Return
            End If

            Results.Clear()

            For Each hit In hits
                Results.Add(hit)
            Next

            SelectedResult = Nothing

            ' Wichtig:
            ' Die ListBox braucht nach dem Befüllen noch einen UI-Zyklus.
            ' Sonst kann SelectedItem danach automatisch auf den ersten Eintrag springen.
            Await Task.Yield()

            SelectedResult = Nothing

            ' Kleine Zusatzwartezeit, weil ArcGIS/WPF die Auswahl bei langen Adresslisten
            ' teilweise verzögert aktualisiert.
            Await Task.Delay(250)

            SelectedResult = Nothing

        Catch ex As OperationCanceledException
            ' Ignorieren

        Catch ex As Exception

            MessageBox.Show(ex.ToString(), "Fehler in der Auswahlsuche")

            Results.Clear()
            Results.Add(New SearchResult With {
            .Label = "Fehler bei der Auswahlsuche: " & ex.Message,
            .Geom = Nothing,
            .Sml = 0
        })

        Finally

            _ignoreSelectedResultChanges = False

        End Try

    End Function

    Private Async Function GoBackAsync() As Task

        If _selectionHistory.Count = 0 Then
            UpdateCanGoBack()
            Return
        End If

        Dim previous = _selectionHistory.Pop()

        _selectionMode = previous.Mode
        _selectionStep = previous.StepNumber
        RestoreSelectionPath(previous.PathParts)

        Await LoadCatalogResultsAsync(
            catalog:=previous.Catalog,
            term:=previous.Term,
            nextStep:=previous.StepNumber,
            info:=previous.Info,
            addToHistory:=False
        )

        UpdateCanGoBack()

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

        _ignoreSelectedResultChanges = True
        _suppressSelectionUntil = DateTime.UtcNow.AddMilliseconds(500)

        Results.Clear()
        SelectedResult = Nothing

        _selectionMode = SelectionSearchMode.None
        _selectionStep = 0
        _selectionHistory.Clear()
        SelectionInfo = Nothing
        ClearSelectionPath()
        _currentCatalog = Nothing
        _currentTerm = Nothing
        UpdateCanGoBack()

        _ignoreSelectedResultChanges = False

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