using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.Tasks.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Offline
{
    public partial class MainWindow : Window
    {
        private const string flameImageUrl = "http://static.arcgis.com/images/Symbols/SafetyHealth/esriCrimeMarker_56_Gradient.png";
        private string featureLayerServiceUrl = string.Empty;
        private string basemapLayerServiceUrl = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            GetLayerUrls();
        }

        private async void GetLayerUrls()
        {
            await MyMapView.LayersLoadedAsync();

            var basemapLayer = MyMapView.Map.Layers["Basemap"] as ArcGISTiledMapServiceLayer;
            if (basemapLayer != null) { this.basemapLayerServiceUrl = basemapLayer.ServiceUri; }

            var featureLayer = MyMapView.Map.Layers["POI"] as FeatureLayer;
            var table = featureLayer.FeatureTable as ServiceFeatureTable;
            if (table != null) { this.featureLayerServiceUrl = table.ServiceUri; }

        }

        private void MyMapView_LayerLoaded(object sender, LayerLoadedEventArgs e)
        {
            if (e.LoadError == null)
                return;

            Debug.WriteLine(string.Format("Error while loading layer : {0} - {1}", e.Layer.ID, e.LoadError.Message));
        }

        private void MyMapView_MapViewTapped(object sender, MapViewInputEventArgs e)
        {
            try
            {
                // get the map overlay element by name
                var mapOverlay = this.FindName("mapTip") as FrameworkElement;
                // verify that the overlay was found
                if (mapOverlay == null) { return; }

                mapOverlay.Visibility = System.Windows.Visibility.Collapsed;
                // get the location tapped on the map
                var mapPoint = e.Location;

                // get a buffer that is about 8 pixels in equivalent map distance
                var mapUnitsPerPixel = this.MyMapView.Extent.Width / this.MyMapView.ActualWidth;
                var bufferDist = mapUnitsPerPixel * 8;
                var searchBuffer = GeometryEngine.Buffer(mapPoint, bufferDist);

                if (OnlineCheckBox.IsChecked == true)
                {
                    this.QueryOnline(mapOverlay, searchBuffer);
                }
                else
                {
                    this.QueryOffline(mapOverlay, searchBuffer);
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }

        private async void QueryOnline(FrameworkElement mapTipContainer, Geometry searchArea)
        {

            // create a task to query a layer with the map point
            var uri = new Uri(this.featureLayerServiceUrl);
            var queryTask = new QueryTask(uri);

            var query = new Query(searchArea);
            query.OutFields.Add("name");
            query.OutFields.Add("city");
            query.ReturnGeometry = true;

            // execute the query and check for a result
            var result = await queryTask.ExecuteAsync(query);
            if (result.FeatureSet.Features.Count > 0)
            {
                // get the first feature in the results 
                var feature = result.FeatureSet.Features[0];

                // set the overlay's data context with the feature; show the overlay
                mapTipContainer.DataContext = feature;
                mapTipContainer.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private async void QueryOffline(FrameworkElement mapTipContainer, Geometry searchArea)
        {
            var featureLayer = this.MyMapView.Map.Layers["POI"] as FeatureLayer;
            if (featureLayer == null) { return; }

            var featureTable = featureLayer.FeatureTable as GeodatabaseFeatureTable;
            if (featureTable == null) { return; }

            var filter = new SpatialQueryFilter();
            filter.Geometry = searchArea;
            var features = await featureTable.QueryAsync(filter);
            if (features.Count() == 0) { return; }

            mapTipContainer.DataContext = features.FirstOrDefault();
            mapTipContainer.Visibility = System.Windows.Visibility.Visible;

        }

        private async void GoOffline()
        {
            this.MyMapView.Map.Layers.Clear();
            var localTileLayer = new ArcGISLocalTiledLayer(this.LocalTilesPathTextBlock.Text);
            await localTileLayer.InitializeAsync();
            this.MyMapView.Map.Layers.Add(localTileLayer);

            var localGdb = await Geodatabase.OpenAsync(this.LocalDataPathTextBlock.Text);
            var localPoiTable = localGdb.FeatureTables.FirstOrDefault();

            var localPoiLayer = new FeatureLayer(localPoiTable);
            localPoiLayer.ID = "POI";
            localPoiLayer.DisplayName = localPoiTable.Name;
            var flamePictureMarker = new PictureMarkerSymbol();
            await flamePictureMarker.SetSourceAsync(new Uri(flameImageUrl));
            flamePictureMarker.Height = 24;
            flamePictureMarker.Width = 24;
            var simpleRenderer = new SimpleRenderer();
            simpleRenderer.Symbol = flamePictureMarker;
            localPoiLayer.Renderer = simpleRenderer;

            await localPoiLayer.InitializeAsync();
            this.MyMapView.Map.Layers.Add(localPoiLayer);
        }

        private async void GetOnline()
        {
            this.MyMapView.Map.Layers.Clear();

            // add the basemap layer first (topo)
            var uri = new Uri(this.basemapLayerServiceUrl);
            var onlineTiledServiceLayer = new ArcGISTiledMapServiceLayer(uri);
            onlineTiledServiceLayer.ID = "Basemap";
            await onlineTiledServiceLayer.InitializeAsync();
            this.MyMapView.Map.Layers.Add(onlineTiledServiceLayer);
            
            // finally, add the online feature layer
            uri = new Uri(this.featureLayerServiceUrl);
            var onlineMarineLayer = new FeatureLayer(uri);
            onlineMarineLayer.ID = "POI";
            await onlineMarineLayer.InitializeAsync();
            this.MyMapView.Map.Layers.Add(onlineMarineLayer);
        }

        // provide a callback to execute when the GeodatabaseSyncTask completes (successfully or with an exception)
        private async void GdbCompleteCallback(Esri.ArcGISRuntime.Tasks.Offline.GeodatabaseStatusInfo statusInfo, Exception ex)
        {
            // if unsuccessful, report the exception and return
            if (ex != null)
            {
                this.Dispatcher.Invoke(() => this.GenerateGdbStatusTextBlock.Text = "An exception occured: " + ex.Message);
                return;
            }

            // if successful, read the generated geodatabase from the server
            var client = new ArcGISHttpClient();
            var gdbStream = client.GetOrPostAsync(statusInfo.ResultUri, null);

            var geodatabasePath = System.IO.Path.Combine(@"C:\Temp\Cache", "PoiLocal.geodatabase");

            // create a local path for the geodatabase, if it doesn't already exist
            if (!System.IO.Directory.Exists(@"C:\Temp\Cache"))
            {
                System.IO.Directory.CreateDirectory(@"C:\Temp\Cache");
            }

            await Task.Factory.StartNew(async delegate
            {
                using (var stream = System.IO.File.Create(geodatabasePath))
                {
                    await gdbStream.Result.Content.CopyToAsync(stream);
                }
                this.Dispatcher.Invoke(() => this.LocalDataPathTextBlock.Text = geodatabasePath);
                this.Dispatcher.Invoke(() => this.GenerateGdbProgressBar.Visibility = System.Windows.Visibility.Hidden);
                this.Dispatcher.Invoke(() => this.StatusPanel.Visibility = System.Windows.Visibility.Collapsed);
            });
        }

        // store a private varaiable to manage cancellation of the task
        private CancellationTokenSource _syncCancellationTokenSource;
        // call GenerateGeodatabaseAsync from a button click
        private async void GetDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // cancel if an earlier call was made
                if (_syncCancellationTokenSource != null)
                {
                    _syncCancellationTokenSource.Cancel();
                }

                // get a cancellation token
                _syncCancellationTokenSource = new CancellationTokenSource();
                var cancelToken = _syncCancellationTokenSource.Token;

                // create a new GeodatabaseSyncTask with the uri of the feature server to pull from
                var serverUrl = this.featureLayerServiceUrl.Substring(0, this.featureLayerServiceUrl.LastIndexOf('/'));
                var uri = new Uri(serverUrl);
                var gdbTask = new GeodatabaseSyncTask(uri);

                // create parameters for the task: layers and extent to include, out spatial reference, and sync model
                var layers = new List<int>(new int[1] { 0 });
                var extent = MyMapView.Extent;
                var gdbParams = new GenerateGeodatabaseParameters(layers, extent)
                {
                    OutSpatialReference = MyMapView.SpatialReference,
                    SyncModel = SyncModel.PerLayer
                };

                // Create a System.Progress<T> object to report status as the task executes
                var progress = new Progress<GeodatabaseStatusInfo>();
                progress.ProgressChanged += (s, info) =>
                {
                    this.GenerateGdbStatusTextBlock.Text = "Generate GDB: " + info.Status;
                    this.GenerateGdbProgressBar.Visibility = System.Windows.Visibility.Visible;
                };

                // call GenerateGeodatabaseAsync, pass in the parameters and the callback to execute when it's complete
                this.GenerateGdbProgressBar.Visibility = System.Windows.Visibility.Visible;
                this.GenerateGdbStatusTextBlock.Text = "Generate GDB: Job submitted ...";
                
                // show progress bar and label
                this.StatusPanel.Visibility = System.Windows.Visibility.Visible;

                // generate the database
                var gdbResult = await gdbTask.GenerateGeodatabaseAsync(gdbParams, GdbCompleteCallback, new TimeSpan(0, 0, 3), progress, cancelToken);

            }
            catch (Exception ex)
            {
                this.Dispatcher.Invoke(() => this.GenerateGdbStatusTextBlock.Text = "Unable to create offline database: " + ex.Message);
            }
        }

        private async void AddNewSpotButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = this.MyMapView.Editor;
            var newPoint = await editor.RequestPointAsync();

            var featureLayer = this.MyMapView.Map.Layers["POI"] as FeatureLayer;
            var table = featureLayer.FeatureTable as GeodatabaseFeatureTable;

            var newFeature = new GeodatabaseFeature(table.Schema);
            newFeature.Geometry = newPoint;
            newFeature.Attributes["Name"] = this.NameTextBox.Text;
            newFeature.Attributes["City"] = this.CityComboBox.SelectedItem.ToString();
            var result = await table.AddAsync(newFeature);
        }
    }
}
