using Kinza.DataAccess.Entity;
using Kinza.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Akavache;
using Kinza.DataAccess.Enum;
using Kinza.DataAccess.Pay;
using Kinza.Pages;
using Kinza.Resources;
using Plugin.Geolocator;
using Xamarin.Forms;
using Xamarin.Forms.Maps;

namespace Kinza.ViewModel
{
    public class OrderPageViewModel : BackViewModel
    {
        private BaseService _baseService;
        private string home, apartament, housing, name, street, comment;
        private List<string> addresses, _paySource, itemsCards;
        private int entrance, floor, addressSelectedIndex = 0, _cardsSelectedIndex = 0, _paySelectedIndex = -1;
        private bool intercom, isToggledSite, isToggledCard, isToggledNal;
        private ObservableCollection<AddressEntity> addressList;
        private ObservableCollection<ViewCardModel> cardList;
        private AddressEntity address;
        private UserEntity userEntity;
        private readonly IPayService _payService;
        public readonly INavigationService _navigationService;
        public readonly IAddressService _addressService;
        public readonly IProductService _productService;
        private ICommand onTapBackCommand;
        private Plugin.Geolocator.Abstractions.Position position;
        public ICommand OnTapBackCommand => onTapBackCommand ?? (onTapBackCommand = new Command(async () => { await BackMenuClick(); }));
        //TODO async
        private async Task BackMenuClick()
        {
            await _navigationService.Pop();
        }

        public OrderPageViewModel(INavigationService navigationService, IAddressService addressService, IProductService productService,IPayService payService) : base(navigationService)
        {
            _baseService = new LoginService(new CryptService());
            _navigationService = navigationService;
            _addressService = addressService;
            _productService = productService;
            _payService = payService;
            Task.Run(OnLoad);
        }

        //TODO async
        private async Task OnLoad()
        {
            try
            {
                PinAddress = new Position();
                CountBasket = await CountAllBasket();
                userEntity = await _baseService.GetAkavache<UserEntity>("user");
                cardList = await _payService.GetUserCards(userEntity.AuthToken);
                List<string> listAddress = new List<string>();
                List<string> listCard = new List<string>();
                addressList = await _addressService.GetAddress(userEntity.AuthToken);
               
                foreach (var item in cardList)
                {
                    listCard.Add(ConvertNumberCard(item));
                }
                ItemsCards = listCard;
                if (cardList.Count == 1)
                {
                    CardsSelectedIndex = 0;
                }
                PaySource = new List<string>();
                PaySource.Add("Картой онлайн");
                PaySource.Add("Картой курьеру");
                PaySource.Add("Наличными");
                foreach (var item in addressList)
                {
                    listAddress.Add(item.Name);
                }
                AddressItems = listAddress;
            }
            catch (Exception ex)
            {

            }
        }
        private string ConvertNumberCard(ViewCardModel item)
        {
            return "XXXX XXXX XXXX " + item.Last4 + " | " + item.ExpMonth + "/" + item.ExpYear;
        }
        private ICommand _confirmOrder;

        public ICommand ConfirmOrder => _confirmOrder ?? (_confirmOrder = new Command(async () => { await ConfirmOrderClick(); }));
        //TODO async
        private async Task ConfirmOrderClick()
        {
            IsLoading = true;
            AddressEntity currentAddress;
            try
            {
                currentAddress = new AddressEntity
                {
                    Street = Street,
                    House = Home,
                    Entrance = Entrance,
                    Apartment = Apartament,
                    Housing = Housing,
                    Floor = Floor,
                    Intercom = Intercom,
                    Comment = Comment
                };
            }
            catch (Exception)
            {
                await Application.Current.MainPage.DisplayAlert(Strings.LoginPage_Error, Strings.OrderPage_AddressErrorMessage, Strings.LoginPage_Ok);
                IsLoading = false;
                return;
            }
            CreateOrderRequestModel promocode = new CreateOrderRequestModel { Address = currentAddress };
            List<CartLine> productList = await _baseService.GetAkavache<List<CartLine>>("GetBasket");
            //BlobCache.LocalMachine.GetObject<List<CartLine>>("GetBasket");
            promocode.Products = productList.ToArray();

            if (PaySelectedIndex == 0)
            {

                if (CardsSelectedIndex == -1)
                {
                    await Application.Current.MainPage.DisplayAlert(Strings.LoginPage_Error, Strings.OrderPage_PayErrorMessage, Strings.LoginPage_Ok);
                    IsLoading = false;
                    return;
                }
                else
                {
                    promocode.PaymentType = PaymentType.Card;
                    promocode.CardId = cardList[CardsSelectedIndex].Id;
                }
            }
            if (PaySelectedIndex == 2)
            {
                promocode.PaymentType = PaymentType.CashCourier;
            }
            if (PaySelectedIndex == 1)
            {
                promocode.PaymentType = PaymentType.CardCourier;
            }

            if (PaySelectedIndex == -1)
            {
                await Application.Current.MainPage.DisplayAlert(Strings.LoginPage_Error, Strings.OrderPage_PayErrorMessage, Strings.LoginPage_Ok);
                IsLoading = false;
                return;
            }
            OrderEntity order = await _productService.CreateOrder(promocode, userEntity.AuthToken);
            if (order == null)
            {
                await Application.Current.MainPage.DisplayAlert(Strings.LoginPage_Error, Strings.OrderPage_ErrorMessage, Strings.LoginPage_Ok);
                IsLoading = false;
            }
            else
            {
                if (order.Payment.IsPaid)
                {
                    await BlobCache.LocalMachine.Invalidate("GetBasket");
                    await Application.Current.MainPage.DisplayAlert(Strings.OrderPage_Message, Strings.OrderPage_ConfirmMessage, Strings.LoginPage_Ok);
                    Constants.ButtonId= 0;
                    _navigationService.MainPage<ProductsPage>();
                    IsLoading = false;
                }
                else
                {
                    if (promocode.PaymentType != PaymentType.Card)
                    {
                        await BlobCache.LocalMachine.Invalidate("GetBasket");
                        await Application.Current.MainPage.DisplayAlert(Strings.OrderPage_Message, Strings.OrderPage_ConfirmMessage, Strings.LoginPage_Ok);
                        Constants.ButtonId = 0;
                        _navigationService.MainPage<ProductsPage>();
                        IsLoading = false;
                    }
                    
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert(Strings.LoginPage_Error, Strings.OrderPage_ErrorPay, Strings.LoginPage_Ok);
                        IsLoading = false;
                    }
                }
            }
            IsLoading = false;
        }
        //TODO async
        private async Task<Plugin.Geolocator.Abstractions.Position> InitializaLocationManagerAsync()
        {
            try
            {
                if (CrossGeolocator.Current.IsGeolocationEnabled)
                {
                    var locator = CrossGeolocator.Current;
                    locator.DesiredAccuracy = 50;
                    position = await locator.GetPositionAsync(TimeSpan.FromSeconds(10000), null);
                    return position;
                }
               return new Plugin.Geolocator.Abstractions.Position();
            }
            catch (Exception ex)
            {
                return new Plugin.Geolocator.Abstractions.Position();
            }
        }

        #region  Init

        public List<string> PaySource
        {
            get => _paySource;

            set
            {
                _paySource = value;
                OnPropertyChanged(nameof(PaySource));
            }
        }
        public List<string> ItemsCards
        {
            get => itemsCards;

            set
            {
                itemsCards = value;
                OnPropertyChanged(nameof(ItemsCards));
            }
        }
        public List<string> AddressItems
        {
            get => addresses;

            set
            {
                addresses = value;
                OnPropertyChanged(nameof(AddressItems));
            }
        }
        public int PaySelectedIndex
        {
            get => _paySelectedIndex;
            set
            {
                _paySelectedIndex = value;
                OnPropertyChanged(nameof(PaySelectedIndex));
                if (PaySelectedIndex == 0)
                {
                    CardVisible = true;
                }
                else
                {
                    CardVisible = false;
                }
            }
        }
        public int CardsSelectedIndex
        {
            get => _cardsSelectedIndex;
            set
            {
                _cardsSelectedIndex = value;
                OnPropertyChanged(nameof(CardsSelectedIndex));
            }
        }
        public int AddressSelectedIndex
        {
            get => addressSelectedIndex;
            set
            {
                addressSelectedIndex = value;

                // trigger some action to take such as updating other labels or fields
                OnPropertyChanged(nameof(AddressSelectedIndex));
                address = addressList[addressSelectedIndex];
                Street = address.Street;
                Home = address.House;
                if (address.Entrance.HasValue)
                    Entrance = address.Entrance.Value;
                Apartament = address.Apartment;
                Housing = address.Housing;
                if (address.Floor.HasValue)
                    Floor = address.Floor.Value;
                if (address.Intercom.HasValue)
                    Intercom = address.Intercom.Value;
                Comment = address.Comment;
            }
        }
        private string _countBasket;
        public string CountBasket
        {
            get => _countBasket;

            set
            {
                _countBasket = value;
                OnPropertyChanged(nameof(CountBasket));
            }
        }
        public string Street
        {
            get => street;

            set
            {
                street = value;
                OnPropertyChanged(nameof(Street));
            }
        }

        public string Home
        {
            get => home;

            set
            {
                home = value;
                OnPropertyChanged(nameof(Home));
            }
        }

        public int Entrance
        {
            get => entrance;

            set
            {
                entrance = value;
                OnPropertyChanged(nameof(Entrance));
            }
        }

        public string Apartament
        {
            get => apartament;

            set
            {
                apartament = value;
                OnPropertyChanged(nameof(Apartament));
            }
        }

        public string Housing
        {
            get => housing;

            set
            {
                housing = value;
                OnPropertyChanged(nameof(Housing));
            }
        }

        public int Floor
        {
            get => floor;

            set
            {
                floor = value;
                OnPropertyChanged(nameof(Floor));
            }
        }

        public bool IsToggledSite
        {
            get => isToggledSite;

            set
            {
                isToggledSite = value;
                OnPropertyChanged(nameof(IsToggledSite));
            }
        }

        public string Comment
        {
            get => comment;

            set
            {
                comment = value;
                OnPropertyChanged(nameof(Comment));
            }
        }

        public bool IsToggledCard
        {
            get => isToggledCard;

            set
            {
                isToggledCard = value;
                OnPropertyChanged(nameof(IsToggledCard));
            }
        }

        public bool IsToggledNal
        {
            get => isToggledNal;

            set
            {
                isToggledNal = value;
                OnPropertyChanged(nameof(IsToggledNal));
            }
        }

        public bool Intercom
        {
            get => intercom;

            set
            {
                intercom = value;
                OnPropertyChanged(nameof(Intercom));
            }
        }

        private bool _isLoading = false, _cardVisible = false;
        public bool CardVisible
        {
            get => _cardVisible;

            private set
            {
                _cardVisible = value;
                OnPropertyChanged(nameof(CardVisible));
            }
        }
        public bool IsLoading
        {
            get => _isLoading;

            private set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private Position _pinAddress;
        public Position PinAddress
        {
            get => _pinAddress;

            set
            {
                _pinAddress = value;
                OnPropertyChanged(nameof(Intercom));
            }
        }



        #endregion
    }
}