using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.Http.ModelBinding;
using FluentValidation.Results;
using Nop.Admin.Extensions;
using Nop.Admin.Models.Common;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Infrastructure;
using Nop.Plugin.Api.Attributes;
using Nop.Plugin.Api.Constants;
using Nop.Plugin.Api.Delta;
using Nop.Plugin.Api.DTOs;
using Nop.Plugin.Api.DTOs.Customers;
using Nop.Plugin.Api.DTOs.OrderItems;
using Nop.Plugin.Api.DTOs.Orders;
using Nop.Plugin.Api.Extensions;
using Nop.Plugin.Api.Factories;
using Nop.Plugin.Api.Helpers;
using Nop.Plugin.Api.JSON.ActionResults;
using Nop.Plugin.Api.MappingExtensions;
using Nop.Plugin.Api.ModelBinders;
using Nop.Plugin.Api.Models.OrdersParameters;
using Nop.Plugin.Api.Models.Response;
using Nop.Plugin.Api.Serializers;
using Nop.Plugin.Api.Services;
using Nop.Plugin.Api.Validators;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;

namespace Nop.Plugin.Api.Controllers
{
    [BearerTokenAuthorize]
    public class OrdersController : BaseApiController
    {
        private readonly IOrderApiService _orderApiService;
        private readonly IProductService _productService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IShippingService _shippingService;
        private readonly IDTOHelper _dtoHelper;        
        private readonly IProductAttributeConverter _productAttributeConverter;
        private readonly IStoreContext _storeContext;
        private readonly IFactory<Order> _factory;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ICustomerApiService _customerApiService;
        private readonly ILanguageService _languageService;
        private readonly IWorkContext _workContext;
        private readonly TaxSettings _taxSettings;
        private readonly IWebHelper _webHelper;
        private readonly ICustomNumberFormatter _customNumberFormatter;
        private readonly IAddressService _addressService;

        // We resolve the order settings this way because of the tests.
        // The auto mocking does not support concreate types as dependencies. It supports only interfaces.
        private OrderSettings _orderSettings;

        private OrderSettings OrderSettings
        {
            get
            {
                if (_orderSettings == null)
                {
                    _orderSettings = EngineContext.Current.Resolve<OrderSettings>();
                }

                return _orderSettings;
            }
        }

        public OrdersController(IOrderApiService orderApiService,
            IJsonFieldsSerializer jsonFieldsSerializer,
            IAclService aclService,
            ICustomerService customerService,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            IDiscountService discountService,
            ICustomerActivityService customerActivityService,
            ILocalizationService localizationService,
            IProductService productService,
            IStateProvinceService stateProvinceService,
            IFactory<Order> factory,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IShoppingCartService shoppingCartService,
            IGenericAttributeService genericAttributeService,
            IStoreContext storeContext,
            IShippingService shippingService,
            IPictureService pictureService,
            IDTOHelper dtoHelper,
            ICustomerApiService customerApiService,
            ILanguageService languageService,
            IWorkContext workContext,
            TaxSettings taxSettings,
            IWebHelper webHelper,
            ICustomNumberFormatter customNumberFormatter,
            IAddressService addressService,
            IProductAttributeConverter productAttributeConverter)
            : base(jsonFieldsSerializer, aclService, customerService, storeMappingService,
                 storeService, discountService, customerActivityService, localizationService,pictureService)
        {
            _orderApiService = orderApiService;
            _factory = factory;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _stateProvinceService = stateProvinceService;
            _shoppingCartService = shoppingCartService;
            _genericAttributeService = genericAttributeService;
            _storeContext = storeContext;
            _shippingService = shippingService;
            _dtoHelper = dtoHelper;
            _productService = productService;
            _productAttributeConverter = productAttributeConverter;
            _customerApiService = customerApiService;
            _languageService = languageService;
            _workContext = workContext;
            _taxSettings = taxSettings;
            _webHelper = webHelper;
            _customNumberFormatter = customNumberFormatter;
            _addressService = addressService;
        }

        /// <summary>
        /// Receive a list of all Orders
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad Request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(OrdersRootObject))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetOrders(OrdersParametersModel parameters)
        {
            if (parameters.Page < Configurations.DefaultPageValue)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid page parameter");
            }

            if (parameters.Limit < Configurations.MinLimit || parameters.Limit > Configurations.MaxLimit)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid limit parameter");
            }

            var storeId = _storeContext.CurrentStore.Id;

            IList<Order> orders = _orderApiService.GetOrders(parameters.Ids, parameters.CreatedAtMin,
                parameters.CreatedAtMax,
                parameters.Limit, parameters.Page, parameters.SinceId,
                parameters.Status, parameters.PaymentStatus, parameters.ShippingStatus,
                parameters.CustomerId, storeId);

            IList<OrderDto> ordersAsDtos = orders.Select(x => _dtoHelper.PrepareOrderDTO(x)).ToList();

            var ordersRootObject = new OrdersRootObject()
            {
                Orders = ordersAsDtos
            };

            var json = _jsonFieldsSerializer.Serialize(ordersRootObject, parameters.Fields);

            return new RawJsonActionResult(json);
        }

        /// <summary>
        /// Receive a count of all Orders
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(OrdersCountRootObject))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetOrdersCount(OrdersCountParametersModel parameters)
        {
            var storeId = _storeContext.CurrentStore.Id;

            int ordersCount = _orderApiService.GetOrdersCount(parameters.CreatedAtMin, parameters.CreatedAtMax, parameters.Status,
                                                              parameters.PaymentStatus, parameters.ShippingStatus, parameters.CustomerId, storeId);

            var ordersCountRootObject = new OrdersCountRootObject()
            {
                Count = ordersCount
            };

            return Ok(ordersCountRootObject);
        }

        /// <summary>
        /// Retrieve order by spcified id
        /// </summary>
        ///   /// <param name="id">Id of the order</param>
        /// <param name="fields">Fields from the order you want your json to contain</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(OrdersRootObject))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetOrderById(int id, string fields = "")
        {
            if (id <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "id", "invalid id");
            }

            Order order = _orderApiService.GetOrderById(id);

            if (order == null)
            {
                return Error(HttpStatusCode.NotFound, "order", "not found");
            }

            var ordersRootObject = new OrdersRootObject();

            OrderDto orderDto = _dtoHelper.PrepareOrderDTO(order);
            ordersRootObject.Orders.Add(orderDto);

            var json = _jsonFieldsSerializer.Serialize(ordersRootObject, fields);

            return new RawJsonActionResult(json);
        }

        /// <summary>
        /// Retrieve all orders for customer
        /// </summary>
        /// <param name="customer_id">Id of the customer whoes orders you want to get</param>
        /// <response code="200">OK</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(OrdersRootObject))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetOrdersByCustomerId(int customer_id)
        {
            IList<OrderDto> ordersForCustomer = _orderApiService.GetOrdersByCustomerId(customer_id).Select(x => _dtoHelper.PrepareOrderDTO(x)).ToList();

            var ordersRootObject = new OrdersRootObject()
            {
                Orders = ordersForCustomer
            };

            return Ok(ordersRootObject);
        }

        [HttpPost]
        [ResponseType(typeof(OrdersRootObject))]
        public IHttpActionResult CreateOrder([ModelBinder(typeof(JsonModelBinder<OrderDto>))] Delta<OrderDto> orderDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            // We doesn't have to check for value because this is done by the order validator.
            Customer customer = _customerService.GetCustomerById(orderDelta.Dto.CustomerId.Value);
            
            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            bool shippingRequired = false;

            if (orderDelta.Dto.OrderItemDtos != null)
            {
                bool shouldReturnError = ValidateEachOrderItem(orderDelta.Dto.OrderItemDtos);

                if (shouldReturnError)
                {
                    return Error(HttpStatusCode.BadRequest);
                }

                shouldReturnError = AddOrderItemsToCart(orderDelta.Dto.OrderItemDtos, customer, orderDelta.Dto.StoreId ?? _storeContext.CurrentStore.Id);

                if (shouldReturnError)
                {
                    return Error(HttpStatusCode.BadRequest);
                }

                shippingRequired = IsShippingAddressRequired(orderDelta.Dto.OrderItemDtos);
            }

            if (shippingRequired)
            {
                bool isValid = true;

                isValid &= SetShippingOption(orderDelta.Dto.ShippingRateComputationMethodSystemName,
                                            orderDelta.Dto.ShippingMethod,
                                            orderDelta.Dto.StoreId ?? _storeContext.CurrentStore.Id,
                                            customer, 
                                            BuildShoppingCartItemsFromOrderItemDtos(orderDelta.Dto.OrderItemDtos.ToList(), 
                                                                                    customer.Id, 
                                                                                    orderDelta.Dto.StoreId ?? _storeContext.CurrentStore.Id));

                isValid &= ValidateAddress(orderDelta.Dto.ShippingAddress, "shipping_address");

                if (!isValid)
                {
                    return Error(HttpStatusCode.BadRequest);
                }
            }

            if (!OrderSettings.DisableBillingAddressCheckoutStep)
            {
                bool isValid = ValidateAddress(orderDelta.Dto.BillingAddress, "billing_address");

                if (!isValid)
                {
                    return Error(HttpStatusCode.BadRequest);
                }
            }

            Order newOrder = _factory.Initialize();
            orderDelta.Merge(newOrder);

            customer.BillingAddress = newOrder.BillingAddress;
            customer.ShippingAddress = newOrder.ShippingAddress;
            // If the customer has something in the cart it will be added too. Should we clear the cart first? 
            newOrder.Customer = customer;

            // The default value will be the currentStore.id, but if it isn't passed in the json we need to set it by hand.
            if (!orderDelta.Dto.StoreId.HasValue)
            {
                newOrder.StoreId = _storeContext.CurrentStore.Id;
            }
            
            PlaceOrderResult placeOrderResult = PlaceOrder(newOrder, customer);

            if (!placeOrderResult.Success)
            {
                foreach (var error in placeOrderResult.Errors)
                {
                    ModelState.AddModelError("order placement", error);
                }

                return Error(HttpStatusCode.BadRequest);
            }

            _customerActivityService.InsertActivity("AddNewOrder",
                 _localizationService.GetResource("ActivityLog.AddNewOrder"), newOrder.Id);

            var ordersRootObject = new OrdersRootObject();

            OrderDto placedOrderDto = _dtoHelper.PrepareOrderDTO(placeOrderResult.PlacedOrder);

            ordersRootObject.Orders.Add(placedOrderDto);

            var json = _jsonFieldsSerializer.Serialize(ordersRootObject, string.Empty);

            return new RawJsonActionResult(json);
        }

        [HttpDelete]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult DeleteOrder(int id)
        {
            if (id <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "id", "invalid id");
            }
            
            Order orderToDelete = _orderApiService.GetOrderById(id);

            if (orderToDelete == null)
            {
                return Error(HttpStatusCode.NotFound, "order", "not found");
            }

            _orderProcessingService.DeleteOrder(orderToDelete);

            //activity log
            _customerActivityService.InsertActivity("DeleteOrder", _localizationService.GetResource("ActivityLog.DeleteOrder"), orderToDelete.Id);

            return new RawJsonActionResult("{}");
        }

        [HttpPut]
        [ResponseType(typeof(OrdersRootObject))]
        public IHttpActionResult UpdateOrder([ModelBinder(typeof(JsonModelBinder<OrderDto>))] Delta<OrderDto> orderDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            Order currentOrder = _orderApiService.GetOrderById(int.Parse(orderDelta.Dto.Id));

            if (currentOrder == null)
            {
                return Error(HttpStatusCode.NotFound, "order", "not found");
            }

            Customer customer = currentOrder.Customer;

            bool shippingRequired = currentOrder.OrderItems.Any(item => !item.Product.IsFreeShipping);

            if (shippingRequired)
            {
                bool isValid = true;

                if (!string.IsNullOrEmpty(orderDelta.Dto.ShippingRateComputationMethodSystemName) ||
                    !string.IsNullOrEmpty(orderDelta.Dto.ShippingMethod))
                {
                    var storeId = orderDelta.Dto.StoreId ?? _storeContext.CurrentStore.Id;

                    isValid &= SetShippingOption(orderDelta.Dto.ShippingRateComputationMethodSystemName ?? currentOrder.ShippingRateComputationMethodSystemName,
                        orderDelta.Dto.ShippingMethod, 
                        storeId,
                        customer, BuildShoppingCartItemsFromOrderItems(currentOrder.OrderItems.ToList(), customer.Id, storeId));
                }

                if (orderDelta.Dto.ShippingAddress != null)
                {
                    isValid &= ValidateAddress(orderDelta.Dto.ShippingAddress, "shipping_address");
                }

                if (isValid)
                {
                    currentOrder.ShippingMethod = orderDelta.Dto.ShippingMethod;
                }
                else
                {
                    return Error(HttpStatusCode.BadRequest);
                }
            }

            if (!OrderSettings.DisableBillingAddressCheckoutStep && orderDelta.Dto.BillingAddress != null)
            {
                bool isValid = ValidateAddress(orderDelta.Dto.BillingAddress, "billing_address");

                if (!isValid)
                {
                    return Error(HttpStatusCode.BadRequest);
                }
            }

            orderDelta.Merge(currentOrder);
            
            customer.BillingAddress = currentOrder.BillingAddress;
            customer.ShippingAddress = currentOrder.ShippingAddress;

            _orderService.UpdateOrder(currentOrder);

            _customerActivityService.InsertActivity("UpdateOrder",
                 _localizationService.GetResource("ActivityLog.UpdateOrder"), currentOrder.Id);

            var ordersRootObject = new OrdersRootObject();

            OrderDto placedOrderDto = _dtoHelper.PrepareOrderDTO(currentOrder);
            placedOrderDto.ShippingMethod = orderDelta.Dto.ShippingMethod;

            ordersRootObject.Orders.Add(placedOrderDto);

            var json = _jsonFieldsSerializer.Serialize(ordersRootObject, string.Empty);

            return new RawJsonActionResult(json);
        }

        private bool SetShippingOption(string shippingRateComputationMethodSystemName, string shippingOptionName, int storeId, Customer customer, List<ShoppingCartItem> shoppingCartItems)
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(shippingRateComputationMethodSystemName))
            {
                isValid = false;

                ModelState.AddModelError("shipping_rate_computation_method_system_name",
                    "Please provide shipping_rate_computation_method_system_name");
            }
            else if (string.IsNullOrEmpty(shippingOptionName))
            {
                isValid = false;

                ModelState.AddModelError("shipping_option_name", "Please provide shipping_option_name");
            }
            else
            {
                GetShippingOptionResponse shippingOptionResponse = _shippingService.GetShippingOptions(shoppingCartItems, customer.ShippingAddress, customer,
                        shippingRateComputationMethodSystemName, storeId);

                var shippingOptions = new List<ShippingOption>();

                if (shippingOptionResponse.Success)
                {
                    shippingOptions = shippingOptionResponse.ShippingOptions.ToList();

                    ShippingOption shippingOption = shippingOptions
                        .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(shippingOptionName, StringComparison.InvariantCultureIgnoreCase));
                    
                    _genericAttributeService.SaveAttribute(customer,
                        SystemCustomerAttributeNames.SelectedShippingOption,
                        shippingOption, storeId);
                }
                else
                {
                    isValid = false;

                    foreach (var errorMessage in shippingOptionResponse.Errors)
                    {
                        ModelState.AddModelError("shipping_option", errorMessage);
                    }
                }
            }

            return isValid;
        }

        private List<ShoppingCartItem> BuildShoppingCartItemsFromOrderItems(List<OrderItem> orderItems, int customerId, int storeId)
        {
            var shoppingCartItems = new List<ShoppingCartItem>();

            foreach (var orderItem in orderItems)
            {
                shoppingCartItems.Add(new ShoppingCartItem()
                {
                    ProductId = orderItem.ProductId,
                    CustomerId = customerId,
                    Quantity = orderItem.Quantity,
                    RentalStartDateUtc = orderItem.RentalStartDateUtc,
                    RentalEndDateUtc = orderItem.RentalEndDateUtc,
                    StoreId = storeId,
                    Product = orderItem.Product,
                    ShoppingCartType = ShoppingCartType.ShoppingCart
                });
            }

            return shoppingCartItems;
        }

        private List<ShoppingCartItem> BuildShoppingCartItemsFromOrderItemDtos(List<OrderItemDto> orderItemDtos, int customerId, int storeId)
        {
            var shoppingCartItems = new List<ShoppingCartItem>();

            foreach (var orderItem in orderItemDtos)
            {
                shoppingCartItems.Add(new ShoppingCartItem()
                {
                    ProductId = orderItem.ProductId.Value, // required field
                    CustomerId = customerId,
                    Quantity = orderItem.Quantity ?? 1,
                    RentalStartDateUtc = orderItem.RentalStartDateUtc,
                    RentalEndDateUtc = orderItem.RentalEndDateUtc,
                    StoreId = storeId,
                    Product = _productService.GetProductById(orderItem.ProductId.Value),
                    ShoppingCartType = ShoppingCartType.ShoppingCart
                });
            }

            return shoppingCartItems;
        }

        private PlaceOrderResult PlaceOrder(Order newOrder, Customer customer)
        {
            var processPaymentRequest = new ProcessPaymentRequest();

            processPaymentRequest.StoreId = newOrder.StoreId;
            processPaymentRequest.CustomerId = customer.Id;
            processPaymentRequest.PaymentMethodSystemName = processPaymentRequest.PaymentMethodSystemName = _workContext.CurrentCustomer.GetAttribute<string>(
                    SystemCustomerAttributeNames.SelectedPaymentMethod,
                    _genericAttributeService, _storeContext.CurrentStore.Id);

            PlaceOrderResult placeOrderResult = _orderProcessingService.PlaceOrder(processPaymentRequest);

            return placeOrderResult;
        }

        private bool ValidateEachOrderItem(ICollection<OrderItemDto> orderItems)
        {
            bool shouldReturnError = false;

            foreach (var orderItem in orderItems)
            {
                var orderItemDtoValidator = new OrderItemDtoValidator("post", null);
                ValidationResult validation = orderItemDtoValidator.Validate(orderItem);

                if (validation.IsValid)
                {
                    Product product = _productService.GetProductById(orderItem.ProductId.Value);

                    if (product == null)
                    {
                        ModelState.AddModelError("order_item.product", string.Format("Product not found for order_item.product_id = {0}", orderItem.ProductId));
                        shouldReturnError = true;
                    }
                }
                else
                {
                    foreach (var error in validation.Errors)
                    {
                        ModelState.AddModelError("order_item", error.ErrorMessage);
                    }

                    shouldReturnError = true;
                }
            }

            return shouldReturnError;
        }

        private bool IsShippingAddressRequired(ICollection<OrderItemDto> orderItems)
        {
            bool shippingAddressRequired = false;

            foreach (var orderItem in orderItems)
            {
                Product product = _productService.GetProductById(orderItem.ProductId.Value);

                shippingAddressRequired |= product.IsShipEnabled;
            }

            return shippingAddressRequired;
        }

        private bool AddOrderItemsToCart(ICollection<OrderItemDto> orderItems, Customer customer, int storeId)
        {
            bool shouldReturnError = false;

            foreach (var orderItem in orderItems)
            {
                Product product = _productService.GetProductById(orderItem.ProductId.Value);

                if (!product.IsRental)
                {
                    orderItem.RentalStartDateUtc = null;
                    orderItem.RentalEndDateUtc = null;
                }

                string attributesXml = _productAttributeConverter.ConvertToXml(orderItem.Attributes, product.Id);                

                IList<string> errors = _shoppingCartService.AddToCart(customer, product,
                    ShoppingCartType.ShoppingCart, storeId,attributesXml,
                    0M, orderItem.RentalStartDateUtc, orderItem.RentalEndDateUtc,
                    orderItem.Quantity ?? 1);

                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        ModelState.AddModelError("order", error);
                    }

                    shouldReturnError = true;
                }
            }

            return shouldReturnError;
        }
        
        private bool ValidateAddress(AddressDto address, string addressKind)
        {
            bool addressValid = true;

            if (address == null)
            {
                ModelState.AddModelError(addressKind, string.Format("{0} address required", addressKind));
                addressValid = false;
            }
            else
            {
                var addressValidator = new AddressDtoValidator();
                ValidationResult validationResult = addressValidator.Validate(address);

                foreach (var validationFailure in validationResult.Errors)
                {
                    ModelState.AddModelError(addressKind, validationFailure.ErrorMessage);
                }

                addressValid = validationResult.IsValid;
            }

            return addressValid;
        }

        #region Trajan API

        /// <summary>
        /// Receive a list of all pending Orders (ShippingStatus = NotYetShipped)
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad Request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetPendingOrders()
        {
            OrdersParametersModel parameters = new OrdersParametersModel();

            var storeId = _storeContext.CurrentStore.Id;

            parameters.ShippingStatus = ShippingStatus.NotYetShipped;

            IList<Order> orders = _orderApiService.GetOrders(parameters.Ids, parameters.CreatedAtMin,
                parameters.CreatedAtMax,
                parameters.Limit, parameters.Page, parameters.SinceId,
                parameters.Status, parameters.PaymentStatus, parameters.ShippingStatus,
                parameters.CustomerId, storeId);

            IList<OrderDto> ordersAsDtos = orders.Select(x => _dtoHelper.PrepareOrderDTO(x)).ToList();

            var ordersRootObject = new OrdersRootObject()
            {
                Orders = ordersAsDtos
            };

            List<Trajan_OrderDto> trajan_OrderList = new List<Trajan_OrderDto>();

            foreach(OrderDto ord in ordersAsDtos)
            {
                Trajan_OrderDto t_Order = new Trajan_OrderDto();
                t_Order.OrderNumber = ord.Id;
                t_Order.OrderTotal = ord.OrderTotal.ToString();
                t_Order.UserID = _customerService.GetCustomerById(ord.CustomerId.Value).UserID;
                if (ord.ShippingAddress.Id != "0")
                {
                    t_Order.ShippingAddress1 = ord.ShippingAddress.Address1;
                    t_Order.ShippingAddress2 = ord.ShippingAddress.Address2;
                    t_Order.ShippingCity = ord.ShippingAddress.City;
                    int stateid = ord.ShippingAddress.StateProvinceId ?? 0;
                    if (stateid != 0) { t_Order.ShippingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                    t_Order.ShippingPostCode = ord.ShippingAddress.ZipPostalCode;
                }
                if (ord.BillingAddress.Id != "0")
                {
                    t_Order.BillingAddress1 = ord.BillingAddress.Address1;
                    t_Order.BillingAddress2 = ord.BillingAddress.Address2;
                    t_Order.BillingCity = ord.BillingAddress.City;
                    int stateid = ord.BillingAddress.StateProvinceId ?? 0;
                    if (stateid != 0) { t_Order.BillingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                    t_Order.BillingPostCode = ord.BillingAddress.ZipPostalCode;
                }
                t_Order.OrderStatus = ord.OrderStatus;
                t_Order.PaymentStatus = ord.PaymentStatus;
                t_Order.ShippingStatus = ord.ShippingStatus;
                t_Order.OriginalSampleID = ord.OriginalSampleID;
                trajan_OrderList.Add(t_Order);
            }
            return Content(HttpStatusCode.OK, trajan_OrderList);
        }

        /// <summary>
        /// Receive a list of all pending Orders (ShippingStatus = NotYetShipped)
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad Request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetAllOrders()
        {
            OrdersParametersModel parameters = new OrdersParametersModel();

            var storeId = _storeContext.CurrentStore.Id;

            //parameters.ShippingStatus = ShippingStatus.NotYetShipped;

            IList<Order> orders = _orderApiService.GetOrders(parameters.Ids, parameters.CreatedAtMin,
                parameters.CreatedAtMax,
                parameters.Limit, parameters.Page, parameters.SinceId,
                parameters.Status, parameters.PaymentStatus, parameters.ShippingStatus,
                parameters.CustomerId, storeId);

            IList<OrderDto> ordersAsDtos = orders.Select(x => _dtoHelper.PrepareOrderDTO(x)).ToList();

            var ordersRootObject = new OrdersRootObject()
            {
                Orders = ordersAsDtos
            };

            List<Trajan_OrderDto> trajan_OrderList = new List<Trajan_OrderDto>();

            foreach (OrderDto ord in ordersAsDtos)
            {
                Trajan_OrderDto t_Order = new Trajan_OrderDto();
                t_Order.OrderNumber = ord.Id;
                t_Order.OrderTotal = ord.OrderTotal.ToString();
                t_Order.UserID = _customerService.GetCustomerById(ord.CustomerId.Value).UserID;
                if (ord.ShippingAddress.Id != "0")
                {
                    t_Order.ShippingAddress1 = ord.ShippingAddress.Address1;
                    t_Order.ShippingAddress2 = ord.ShippingAddress.Address2;
                    t_Order.ShippingCity = ord.ShippingAddress.City;
                    int stateid = ord.ShippingAddress.StateProvinceId ?? 0;
                    if (stateid != 0) { t_Order.ShippingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                    t_Order.ShippingPostCode = ord.ShippingAddress.ZipPostalCode;
                }
                if (ord.BillingAddress.Id != "0")
                {
                    t_Order.BillingAddress1 = ord.BillingAddress.Address1;
                    t_Order.BillingAddress2 = ord.BillingAddress.Address2;
                    t_Order.BillingCity = ord.BillingAddress.City;
                    int stateid = ord.BillingAddress.StateProvinceId ?? 0;
                    if (stateid != 0) { t_Order.BillingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                    t_Order.BillingPostCode = ord.BillingAddress.ZipPostalCode;
                }
                t_Order.OrderStatus = ord.OrderStatus;
                t_Order.PaymentStatus = ord.PaymentStatus;
                t_Order.ShippingStatus = ord.ShippingStatus;
                trajan_OrderList.Add(t_Order);
            }
            return Content(HttpStatusCode.OK, trajan_OrderList);
        }

        [HttpPut]
        public IHttpActionResult UpdateOrders(string orderNumber)
        {
            var response = new GeneralResponseModel<Trajan_Order_Result_Dto>();
            // Here we display the errors if the validation has failed at some point.
            if (orderNumber == "" || orderNumber == null)
            {
                response.Success = false;
                response.Code = (int)ErrorType.NotOk;
                response.Error.Message = "Invalid order number";
                return Ok(response);
                //return Error(HttpStatusCode.BadRequest, "OrderNumber", "invalid OrderNumber");
            }

            Order currentOrder = _orderApiService.GetOrderById(int.Parse(orderNumber));

            if (currentOrder == null)
            {
                response.Success = false;
                response.Code = (int)ErrorType.NotOk;
                response.Error.Message = "Order not found";
                return Ok(response);
                //return Error(HttpStatusCode.NotFound, "order", "not found");
            }

            currentOrder.ShippingStatus = ShippingStatus.Shipped;
            if(currentOrder.PaymentStatus == Core.Domain.Payments.PaymentStatus.Paid) { currentOrder.OrderStatus = OrderStatus.Complete; }

            _orderService.UpdateOrder(currentOrder);

            _customerActivityService.InsertActivity("UpdateOrder",
                 _localizationService.GetResource("ActivityLog.UpdateOrder"), currentOrder.Id);
            response.Success = true;
            response.Code = (int)ErrorType.Ok;
            response.Payload = new Trajan_Order_Result_Dto() { OrderNumber = currentOrder.Id };
            return Ok(response);
            //return Content(HttpStatusCode.OK, string.Format("Order id {0} updated", currentOrder.Id.ToString()));
        }

        [HttpGet]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetOrderItems(string orderNumber)
        {
            // Here we display the errors if the validation has failed at some point.
            if (orderNumber == "" || orderNumber == null)
            {
                return Error(HttpStatusCode.BadRequest, "OrderNumber", "invalid OrderNumber");
            }

            Order currentOrder = _orderApiService.GetOrderById(int.Parse(orderNumber));

            if (currentOrder == null)
            {
                return Error(HttpStatusCode.NotFound, "order", "not found");
            }

            OrderDto ord = _dtoHelper.PrepareOrderDTO(currentOrder);

            Trajan_OrderDto t_Order = new Trajan_OrderDto();
            if (ord.OrderItemDtos.Count > 0)
            {
                List<Trajan_OrderDto_Item> itemList = new List<Trajan_OrderDto_Item>();
                foreach (OrderItemDto item in ord.OrderItemDtos)
                {
                    Trajan_OrderDto_Item t_item = new Trajan_OrderDto_Item();
                    t_item.OrderNumber = ord.Id;
                    t_item.sku = item.ProductId.HasValue ? _productService.GetProductById(item.ProductId.Value).Sku : "";
                    t_item.SalesPrice = item.UnitPriceInclTax.ToString();
                    t_item.Quantity = item.Quantity.ToString();
                    t_item.OriginalSampleID = ord.OriginalSampleID;
                    itemList.Add(t_item);
                }
                t_Order.OrderItems = itemList;
                //t_Order.OriginalSampleID = ord.OriginalSampleID;

                return Content(HttpStatusCode.OK, t_Order.OrderItems);
            }
            else
            {
                return Content(HttpStatusCode.OK, "No items found.");
            }

        }
        #endregion



        private bool ValidateNewOrderItem(NewOrderItemDto orderItem)
        {
            bool shouldReturnError = false;

            var orderItemDtoValidator = new NewOrderItemDtoValidator("post", null);
            ValidationResult validation = orderItemDtoValidator.Validate(orderItem);

            if (validation.IsValid)
            {
                Product product = _productService.GetProductById(orderItem.ProductId.Value);

                if (product == null)
                {
                    ModelState.AddModelError("order_item.product", string.Format("Product not found for order_item.product_id = {0}", orderItem.ProductId));
                    shouldReturnError = true;
                }
            }
            else
            {
                foreach (var error in validation.Errors)
                {
                    ModelState.AddModelError("order_item", error.ErrorMessage);
                }

                shouldReturnError = true;
            }
            

            return shouldReturnError;
        }

        private bool AddNewOrderItemToCart(NewOrderItemDto orderItem, Customer customer, int storeId)
        {
            bool shouldReturnError = false;

            
                Product product = _productService.GetProductBySku(orderItem.SKU);


                IList<string> errors = _shoppingCartService.AddToCart(customer, product,
                    ShoppingCartType.ShoppingCart, storeId, 
                    quantity: orderItem.Quantity ?? 1);

                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        ModelState.AddModelError("order", error);
                    }

                    shouldReturnError = true;
                }
            

            return shouldReturnError;
        }



        //----------------------------------------


        //[HttpPost]
        //[GetRequestsErrorInterceptorActionFilter]
        ////[ResponseType(typeof(Trajan_Order_Result_Dto))]
        //public IHttpActionResult CreateNewOrder(Trajan_New_OrderDto new_OrderDto)
        //{
        //    // Here we display the errors if the validation has failed at some point.
        //    if (!ModelState.IsValid)
        //    {
        //        return Error();
        //    }

        //    if (new_OrderDto.UserId == "" || string.IsNullOrEmpty(new_OrderDto.UserId) || new_OrderDto.UserId == null)
        //    {
        //        return Error(HttpStatusCode.BadRequest, "UserId", "invalid UserId");
        //    }

        //    // We doesn't have to check for value because this is done by the order validator.
        //    var customerId = _customerApiService.GetIdByUserId(new_OrderDto.UserId);

        //    if (customerId == null)
        //    {
        //        return Error(HttpStatusCode.NotFound, "customer", "UserId not found");
        //    }
        //    var customer = _customerApiService.GetCustomerEntityById(customerId);
        //    if (customer == null)
        //    {
        //        return Error(HttpStatusCode.NotFound, "customer", "not found");
        //    }
        //    var storeId = _storeContext.CurrentStore.Id;
        //    var proudct = _productService.GetProductBySku(new_OrderDto.SKU);
        //    var newOrderItemDto = new NewOrderItemDto
        //    {
        //        SKU = new_OrderDto.SKU,
        //        UnitPriceInclTax = new_OrderDto.SalesPrice,
        //        Quantity = new_OrderDto.Quantity,
        //        ProductId = proudct.Id
        //    };

        //    bool shippingRequired = false;

        //    bool shouldReturnError = ValidateNewOrderItem(newOrderItemDto);

        //    if (shouldReturnError)
        //    {
        //        return Error(HttpStatusCode.BadRequest);
        //    }

        //    shouldReturnError = AddNewOrderItemToCart(newOrderItemDto, customer, storeId);

        //    if (shouldReturnError)
        //    {
        //        return Error(HttpStatusCode.BadRequest);
        //    }

        //    //shippingRequired = IsShippingAddressRequired(orderDelta.Dto.OrderItemDtos);



        //    OrderItemDto orderItem = null;
        //    //orderItem.Id = proudct;

        //    Language customerLanguage = _languageService.GetLanguageById(
        //        customer.GetAttribute<int>(SystemCustomerAttributeNames.LanguageId, storeId));
        //    if (customerLanguage == null || !customerLanguage.Published)
        //        customerLanguage = _workContext.WorkingLanguage;

        //    TaxDisplayType customerTaxDisplayType;
        //    if (_taxSettings.AllowCustomersToSelectTaxDisplayType)
        //        customerTaxDisplayType = (TaxDisplayType)customer.GetAttribute<int>(SystemCustomerAttributeNames.TaxDisplayTypeId, storeId);
        //    else
        //        customerTaxDisplayType = _taxSettings.TaxDisplayType;


        //    var billingAddress = new AddressModel
        //    {
        //        Email = customer.Email,
        //        Address1 = new_OrderDto.BillingAddress1,
        //        Address2 = new_OrderDto.BillingAddress2,
        //        City = new_OrderDto.BillingCity,
        //        StateProvinceName = new_OrderDto.BillingState,
        //        ZipPostalCode = new_OrderDto.BillingPostCode,
        //    };
        //    var newBillingAddress = billingAddress.ToEntity();
        //    customer.Addresses.Add(newBillingAddress);
        //    customer.BillingAddress = newBillingAddress;

        //    var shippingAddress = new AddressModel
        //    {
        //        Email = customer.Email,
        //        Address1 = new_OrderDto.ShippingAddress1,
        //        Address2 = new_OrderDto.ShippingAddress2,
        //        City = new_OrderDto.ShippingCity,
        //        StateProvinceName = new_OrderDto.ShippingState,
        //        ZipPostalCode = new_OrderDto.ShippingPostCode,
        //    };
        //    var newShippingAddress = shippingAddress.ToEntity();
        //    customer.Addresses.Add(newShippingAddress);
        //    customer.ShippingAddress = newShippingAddress;

        //    //_customerService.UpdateCustomer(customer);
        //    var order = new Order
        //    {
        //        StoreId = storeId,
        //        OrderGuid = Guid.NewGuid(),
        //        CustomerId = customer.Id,
        //        CustomerLanguageId = customerLanguage.Id,
        //        CustomerTaxDisplayType = customerTaxDisplayType,
        //        CustomerIp = _webHelper.GetCurrentIpAddress(),
        //        BillingAddress = newBillingAddress,
        //        ShippingAddress = newShippingAddress,
        //        //CreatedOnUtc = DateTime.UtcNow,
        //        CustomOrderNumber = string.Empty
        //    };

        //    //_orderService.InsertOrder(order);

        //    PlaceOrderResult placeOrderResult = PlaceOrder(order, customer);

        //    if (!placeOrderResult.Success)
        //    {
        //        foreach (var error in placeOrderResult.Errors)
        //        {
        //            ModelState.AddModelError("order placement", error);
        //        }

        //        return Error(HttpStatusCode.BadRequest);
        //    }

        //    _customerActivityService.InsertActivity("AddNewOrder",
        //         _localizationService.GetResource("ActivityLog.AddNewOrder"), order.Id);

        //    //generate and set custom order number
        //    //order.CustomOrderNumber = _customNumberFormatter.GenerateOrderCustomNumber(order);
        //    //_orderService.UpdateOrder(order);

        //    return Content(HttpStatusCode.OK, order.Id);
        //}




        //-----------------------------------------





        [HttpPost]
        [GetRequestsErrorInterceptorActionFilter]
        //[ResponseType(typeof(Trajan_Order_Result_Dto))]
        public IHttpActionResult CreateNewOrder(Trajan_New_OrderDto new_OrderDto)
        {
            var response = new GeneralResponseModel<Trajan_Order_Result_Dto>();
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            if (new_OrderDto.UserId == "" || string.IsNullOrEmpty(new_OrderDto.UserId) || new_OrderDto.UserId == null)
            {
                response.Success = false;
                response.Code = (int)ErrorType.NotOk;
                response.Error.Message = "UserID is empty. Please enter valid UserID";
                return Ok(response);
                //return Error(HttpStatusCode.BadRequest, "UserId", "Invalid UserId");
            }

            // We doesn't have to check for value because this is done by the order validator.
            var customerId = _customerApiService.GetIdByUserId(new_OrderDto.UserId);

            //if (customerId == null)
            //{
            //    return Error(HttpStatusCode.NotFound, "customer", "UserId not found");
            //}
            var customer = _customerApiService.GetCustomerEntityById(customerId);
            if (customer == null)
            {
                response.Success = false;
                response.Code = (int)ErrorType.NotOk;
                response.Error.Message = "Customer not found. Please enter valid UserID";
                return Ok(response);
                //return Error(HttpStatusCode.NotFound, "Customer", "Customer not found");
            }
            var storeId = _storeContext.CurrentStore.Id;

            var product = _productService.GetProductBySku(new_OrderDto.SKU);

            if(product == null)
            {
                response.Success = false;
                response.Code = (int)ErrorType.NotOk;
                response.Error.Message = "Product not found. Please enter valid SKU";
                return Ok(response);
                //return Error(HttpStatusCode.NotFound, "Product", "Not found");
            }

            Language customerLanguage = _languageService.GetLanguageById(
                customer.GetAttribute<int>(SystemCustomerAttributeNames.LanguageId, storeId));
            if (customerLanguage == null || !customerLanguage.Published)
                customerLanguage = _workContext.WorkingLanguage;

            TaxDisplayType customerTaxDisplayType;
            if (_taxSettings.AllowCustomersToSelectTaxDisplayType)
                customerTaxDisplayType = (TaxDisplayType)customer.GetAttribute<int>(SystemCustomerAttributeNames.TaxDisplayTypeId, storeId);
            else
                customerTaxDisplayType = _taxSettings.TaxDisplayType;

            var billingAddress = new AddressModel
            {
                FirstName = customer.GetAttribute<string>(SystemCustomerAttributeNames.FirstName),
                LastName = customer.GetAttribute<string>(SystemCustomerAttributeNames.LastName),
                Email = customer.Email,
                Address1 = new_OrderDto.BillingAddress1,
                Address2 = new_OrderDto.BillingAddress2,
                City = new_OrderDto.BillingCity,
                StateProvinceName = _stateProvinceService.GetStateProvinceByAbbreviation(new_OrderDto.BillingState).Name,
                StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(new_OrderDto.BillingState).Id,
                CountryId = _stateProvinceService.GetStateProvinceByAbbreviation(new_OrderDto.BillingState).CountryId,
                CountryName = _stateProvinceService.GetStateProvinceByAbbreviation(new_OrderDto.BillingState).Country.Name,
                ZipPostalCode = new_OrderDto.BillingPostCode,
            };
            var newBillingAddress = billingAddress.ToEntity();
            newBillingAddress.CreatedOnUtc = DateTime.UtcNow;
            if (newBillingAddress.CountryId == 0)
                newBillingAddress.CountryId = null;
            if (newBillingAddress.StateProvinceId == 0)
                newBillingAddress.StateProvinceId = null;

            _addressService.InsertAddress(newBillingAddress);
            customer.Addresses.Add(newBillingAddress);
            customer.BillingAddress = newBillingAddress;

            var shippingAddress = new AddressModel
            {
                FirstName = customer.GetAttribute<string>(SystemCustomerAttributeNames.FirstName),
                LastName = customer.GetAttribute<string>(SystemCustomerAttributeNames.LastName),
                Email = customer.Email,
                Address1 = new_OrderDto.ShippingAddress1,
                Address2 = new_OrderDto.ShippingAddress2,
                City = new_OrderDto.ShippingCity,
                StateProvinceName = _stateProvinceService.GetStateProvinceByAbbreviation(new_OrderDto.BillingState).Name,
                StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(new_OrderDto.BillingState).Id,
                CountryId = _stateProvinceService.GetStateProvinceByAbbreviation(new_OrderDto.ShippingState).CountryId,
                CountryName = _stateProvinceService.GetStateProvinceByAbbreviation(new_OrderDto.ShippingState).Country.Name,
                ZipPostalCode = new_OrderDto.ShippingPostCode,
            };
            var newShippingAddress = shippingAddress.ToEntity();

            newShippingAddress.CreatedOnUtc = DateTime.UtcNow;
            if (newShippingAddress.CountryId == 0)
                newShippingAddress.CountryId = null;
            if (newShippingAddress.StateProvinceId == 0)
                newShippingAddress.StateProvinceId = null;

            _addressService.InsertAddress(newShippingAddress);
            customer.Addresses.Add(newShippingAddress);
            customer.ShippingAddress = newShippingAddress;

            _customerService.UpdateCustomer(customer);
            var order = new Order
            {
                StoreId = storeId,
                OrderGuid = Guid.NewGuid(),
                CustomerId = customer.Id,
                Customer = customer,
                CustomerLanguageId = customerLanguage.Id,
                CustomerTaxDisplayType = customerTaxDisplayType,
                CustomerIp = _webHelper.GetCurrentIpAddress(),
                OrderStatus = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                ShippingStatus = ShippingStatus.NotYetShipped,
                BillingAddress = newBillingAddress,
                ShippingAddress = newShippingAddress,
                CreatedOnUtc = DateTime.UtcNow,
                CustomOrderNumber = string.Empty,
                OriginalSampleID = new_OrderDto.OriginalSampleID
            };

            _orderService.InsertOrder(order);

            order.CustomOrderNumber = _customNumberFormatter.GenerateOrderCustomNumber(order);
            _orderService.UpdateOrder(order);

            var newOrderItemDto = new OrderItem
            {
                OrderItemGuid = Guid.NewGuid(),
                Order = order,
                ProductId = product.Id,
                UnitPriceInclTax = new_OrderDto.SalesPrice,
                Quantity = new_OrderDto.Quantity
            };
            

            order.OrderItems.Add(newOrderItemDto);
            _orderService.UpdateOrder(order);

            //PlaceOrderResult placeOrderResult = PlaceOrder(order, customer);

            //if (!placeOrderResult.Success)
            //{
            //    foreach (var error in placeOrderResult.Errors)
            //    {
            //        ModelState.AddModelError("order placement", error);
            //    }

            //    return Error(HttpStatusCode.BadRequest);
            //}

            _customerActivityService.InsertActivity("AddNewOrder",
                 _localizationService.GetResource("ActivityLog.AddNewOrder"), order.Id);

            //generate and set custom order number
            //order.CustomOrderNumber = _customNumberFormatter.GenerateOrderCustomNumber(order);
            //_orderService.UpdateOrder(order);
            //return Trajan specific result
            var result = new Trajan_Order_Result_Dto()
            { OrderNumber = order.Id };

            
            response.Payload = new Trajan_Order_Result_Dto() { OrderNumber = order.Id };
            response.Success = true;
            //return Content(HttpStatusCode.OK, string.Format("Order created successfully. OrderNumber: {0}", order.Id.ToString()));
            return Ok(response);
            //return Content(HttpStatusCode.OK, result);
        }

    }
}