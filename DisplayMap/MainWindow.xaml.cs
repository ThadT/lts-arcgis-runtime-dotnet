using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Tasks.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace DisplayMap
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MyMapView_LayerLoaded(object sender, LayerLoadedEventArgs e)
        {
            if (e.LoadError == null)
                return;

            Debug.WriteLine(string.Format("Error while loading layer : {0} - {1}", e.Layer.ID, e.LoadError.Message));
        }


        private async void MyMapView_MapViewTapped(object sender, MapViewInputEventArgs e)
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
                var searchBufferWgs = GeometryEngine.Project(searchBuffer, SpatialReferences.Wgs84);

                // create a task to query a layer with the map point
                var uri = new Uri("http://services2.arcgis.com/zLGtbpFgxhQlMMAY/arcgis/rest/services/CelebrityHotspots/FeatureServer/0");
                var queryTask = new QueryTask(uri);

                var query = new Query(searchBuffer);
                query.OutFields.Add("name");
                query.OutFields.Add("city");
                query.OutFields.Add("pic");
                query.ReturnGeometry = true;

                // execute the query and check for a result
                var result = await queryTask.ExecuteAsync(query);
                if (result.FeatureSet.Features.Count > 0)
                {
                    // get the first feature in the results 
                    var feature = result.FeatureSet.Features[0];

                    // set the overlay's data context with the feature; show the overlay
                    mapOverlay.DataContext = feature;
                    mapOverlay.Visibility = System.Windows.Visibility.Visible;
                }

            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }

        private async void LoadWebMapButton_Click(object sender, RoutedEventArgs e)
        {
            // get a reference to the portal (ArcGIS Online)
            var arcGISOnline = await ArcGISPortal.CreateAsync();

            // get the id of the web map to load
            var webMapId = WebMapIdTextBox.Text.Trim();
            ArcGISPortalItem webMapItem = null;

            // get a portal item using its ID (ArcGISWebException is thrown if the item is not found)
            try
            {
                webMapItem = await ArcGISPortalItem.CreateAsync(arcGISOnline, webMapId);
            }
            catch (ArcGISWebException exp)
            {
                MessageBox.Show("Unable to access item '" + webMapId + "'.");
                return;
            }

            // check type: if the item is not a web map, return
            if (webMapItem.Type != Esri.ArcGISRuntime.Portal.ItemType.WebMap) { return; }

            // get the web map represented by the portal item
            var webMap = await Esri.ArcGISRuntime.WebMap.WebMap.FromPortalItemAsync(webMapItem);

            // create a WebMapViewModel and load the web map
            var webMapVM = await Esri.ArcGISRuntime.WebMap.WebMapViewModel.LoadAsync(webMap, arcGISOnline);

            // get the map from the view model; display it in the app's map view
            this.MyMapView.Map = webMapVM.Map;
            
            // get the list of bookmarks for the web map; bind them to the combo box
            this.BookmarksComboBox.ItemsSource = webMap.Bookmarks;
            this.BookmarksComboBox.DisplayMemberPath = "Name";
        }

        private async void BookmarksComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems == null || e.AddedItems.Count == 0) { return; }

            var bookmark = e.AddedItems[0] as Esri.ArcGISRuntime.WebMap.Bookmark;
            if (bookmark == null) { return; }

            await this.MyMapView.SetViewAsync(bookmark.Extent);
        }

    }
}
