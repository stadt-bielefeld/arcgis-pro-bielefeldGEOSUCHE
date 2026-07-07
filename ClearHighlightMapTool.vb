Imports ArcGIS.Desktop.Framework
Imports ArcGIS.Desktop.Framework.Threading.Tasks
Imports ArcGIS.Desktop.Mapping
Imports System.Threading.Tasks
Imports System.Windows.Input

Friend Class ClearHighlightMapTool
    Inherits MapTool

    Private _isLeftMouseDown As Boolean = False
    Private _mouseDownPoint As System.Windows.Point
    Private _lastMousePoint As System.Windows.Point
    Private _hasDragged As Boolean = False

    Private Const DragTolerance As Double = 5.0

    Public Sub New()
        IsSketchTool = False
        UseSnapping = False
    End Sub

    Protected Overrides Sub OnToolMouseDown(args As MapViewMouseButtonEventArgs)

        If args.ChangedButton = MouseButton.Left Then
            _isLeftMouseDown = True
            _hasDragged = False
            _mouseDownPoint = args.ClientPoint
            _lastMousePoint = args.ClientPoint

            ' Wir behandeln MouseDown, damit MouseMove sauber beim Tool bleibt.
            args.Handled = True
        End If

    End Sub

    Protected Overrides Sub OnToolMouseMove(args As MapViewMouseEventArgs)

        If Not _isLeftMouseDown Then
            Return
        End If

        Dim totalDx As Double = args.ClientPoint.X - _mouseDownPoint.X
        Dim totalDy As Double = args.ClientPoint.Y - _mouseDownPoint.Y
        Dim totalDistance As Double = System.Math.Sqrt((totalDx * totalDx) + (totalDy * totalDy))

        If totalDistance > DragTolerance Then
            _hasDragged = True
        End If

        If _hasDragged Then
            PanMap(args.ClientPoint)
        End If

    End Sub

    Protected Overrides Sub OnToolMouseUp(args As MapViewMouseButtonEventArgs)

        If args.ChangedButton <> MouseButton.Left Then
            Return
        End If

        args.Handled = True

    End Sub

    Protected Overrides Function HandleMouseUpAsync(args As MapViewMouseButtonEventArgs) As Task

        If args.ChangedButton = MouseButton.Left Then

            Dim wasClick As Boolean = Not _hasDragged

            _isLeftMouseDown = False
            _hasDragged = False

            If wasClick Then

                SearchResultsDockPaneViewModel.ClearHighlight()

                Task.Run(Async Function()
                             Await Task.Delay(200)
                             Await FrameworkApplication.SetCurrentToolAsync(Nothing)
                         End Function)

            End If

        End If

        Return Task.CompletedTask

    End Function

    Private _isPanning As Boolean = False

    Private Sub PanMap(currentClientPoint As System.Windows.Point)

        If _isPanning Then
            Return
        End If

        Dim previousClientPoint As System.Windows.Point = _lastMousePoint

        _isPanning = True

        QueuedTask.Run(Sub()

                           Dim activeMapView As ArcGIS.Desktop.Mapping.MapView =
                           ArcGIS.Desktop.Mapping.MapView.Active

                           If activeMapView Is Nothing OrElse activeMapView.Camera Is Nothing Then
                               Return
                           End If

                           Dim previousMapPoint = activeMapView.ClientToMap(previousClientPoint)
                           Dim currentMapPoint = activeMapView.ClientToMap(currentClientPoint)

                           If previousMapPoint Is Nothing OrElse currentMapPoint Is Nothing Then
                               Return
                           End If

                           Dim dxMap As Double = currentMapPoint.X - previousMapPoint.X
                           Dim dyMap As Double = currentMapPoint.Y - previousMapPoint.Y

                           If System.Math.Abs(dxMap) < 0.000001 AndAlso System.Math.Abs(dyMap) < 0.000001 Then
                               Return
                           End If

                           Dim camera = activeMapView.Camera

                           camera.X -= dxMap
                           camera.Y -= dyMap

                           activeMapView.ZoomTo(camera)

                       End Sub).ContinueWith(Sub(t)

                                                 _lastMousePoint = currentClientPoint
                                                 _isPanning = False

                                             End Sub)

    End Sub

End Class