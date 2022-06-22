using Kinza.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Kinza.Services;
using Plugin.Geolocator;
using Xamarin.Forms;
using Xamarin.Forms.Maps;
using Xamarin.Forms.Xaml;

namespace Kinza.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class OrderPage : ContentPage
    {
        private Position position;
        private ProductService productService;
        private Position positionMinsk = new Position(53.9, 27.56667);
        public OrderPage(OrderPageViewModel orderPageViewModel)
        {
            InitializeComponent();
            BindingContext = orderPageViewModel;
        }

        protected async override void OnAppearing()
        {
            productService = new ProductService();
            mapName.MoveToRegion(
                MapSpan.FromCenterAndRadius(positionMinsk, Distance.FromMiles(0.4)));
            AddPin(positionMinsk);
            Street.PropertyChanged += Street_PropertyChanged;
            Home.PropertyChanged += Street_PropertyChanged;
            await InitializaLocationManagerAsync();

        }

        private async void Street_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                var addressXml = await productService.GetCoordinate("Minsk " + Street.Text + " " + Home.Text);
                if (!string.IsNullOrEmpty(addressXml))
                {
                    Position position = ConvertAddressToGeo(addressXml);
                    mapName.MoveToRegion(MapSpan.FromCenterAndRadius(position, Distance.FromMiles(0.4)));
                    AddPin(position);
                }

            }
            catch (Exception ex)
            {
                
            }
        }

        private void AddPin(Position position)
        {
            Pin pin = new Pin();
            pin.Position = position;
            pin.Label = "Это Вы";
            mapName.Pins.Clear();
            mapName.Pins.Add(pin);
        }

        private Position ConvertAddressToGeo(string address)
        {
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(address);
            XmlNodeList geo = xml.SelectNodes("/GeocodeResponse/result/geometry/location");
            if (geo != null)
            {
                var lat = double.Parse(geo[0].SelectSingleNode("lat").InnerText, System.Globalization.CultureInfo.InvariantCulture);
                var lon = double.Parse(geo[0].SelectSingleNode("lng").InnerText, System.Globalization.CultureInfo.InvariantCulture);
                return new Position(lat, lon);
            }
            return positionMinsk;
        }

        private async Task InitializaLocationManagerAsync()
        {
            try
            {
                var locator = CrossGeolocator.Current;
                locator.DesiredAccuracy = 50;
                var position = await locator.GetPositionAsync(TimeSpan.FromSeconds(5), null);
                if (position != null)
                {
                    Position newPosition = new Position(position.Latitude, position.Longitude);
                    mapName.MoveToRegion(
                        MapSpan.FromCenterAndRadius(newPosition, Distance.FromMiles(0.4)));
                    AddPin(newPosition);
                    var addressXml = await productService.GetAddress(newPosition);
                    XmlDocument xml = new XmlDocument();
                    xml.LoadXml(addressXml);
                    XmlNodeList geo = xml.SelectNodes("/GeocodeResponse/result");
                    if (geo != null)
                    {
                        var innerText = geo[0].InnerText;
                        var items = innerText.Split(' ');
                        Street.Text = items[1];
                        Home.Text = items[2].Replace(",", "");
                    }
                }
            }
            catch (Exception ex)
            {
                // ignored
            }
        }
    }
}