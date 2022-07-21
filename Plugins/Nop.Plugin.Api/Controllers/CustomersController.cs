using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.Http.ModelBinding;
using Newtonsoft.Json;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Infrastructure;
using Nop.Plugin.Api.Attributes;
using Nop.Plugin.Api.Constants;
using Nop.Plugin.Api.Delta;
using Nop.Plugin.Api.DTOs;
using Nop.Plugin.Api.DTOs.Customers;
using Nop.Plugin.Api.Factories;
using Nop.Plugin.Api.Helpers;
using Nop.Plugin.Api.JSON.ActionResults;
using Nop.Plugin.Api.MappingExtensions;
using Nop.Plugin.Api.ModelBinders;
using Nop.Plugin.Api.Models.CustomersParameters;
using Nop.Plugin.Api.Serializers;
using Nop.Plugin.Api.Services;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Stores;


namespace Nop.Plugin.Api.Controllers
{
    [BearerTokenAuthorize]
    public class CustomersController : BaseApiController
    {
        private readonly ICustomerApiService _customerApiService;
        private readonly ICustomerRolesHelper _customerRolesHelper;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IEncryptionService _encryptionService;
        private readonly ICountryService _countryService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IMappingHelper _mappingHelper;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly ILanguageService _languageService;
        private readonly IFactory<Customer> _factory;

        // We resolve the customer settings this way because of the tests.
        // The auto mocking does not support concreate types as dependencies. It supports only interfaces.
        private CustomerSettings _customerSettings;

        private CustomerSettings CustomerSettings
        {
            get
            {
                if (_customerSettings == null)
                {
                    _customerSettings = EngineContext.Current.Resolve<CustomerSettings>();
                }

                return _customerSettings;
            }
        }

        public CustomersController(
            ICustomerApiService customerApiService, 
            IJsonFieldsSerializer jsonFieldsSerializer,
            IAclService aclService,
            ICustomerService customerService,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            IDiscountService discountService,
            ICustomerActivityService customerActivityService,
            ILocalizationService localizationService,
            ICustomerRolesHelper customerRolesHelper,
            IGenericAttributeService genericAttributeService,
            IEncryptionService encryptionService,
            IFactory<Customer> factory, 
            ICountryService countryService,
            IStateProvinceService stateProvinceService,
            IMappingHelper mappingHelper, 
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            IPictureService pictureService, ILanguageService languageService) : 
            base(jsonFieldsSerializer, aclService, customerService, storeMappingService, storeService, discountService, customerActivityService, localizationService,pictureService)
        {
            _customerApiService = customerApiService;
            _factory = factory;
            _countryService = countryService;
            _stateProvinceService = stateProvinceService;
            _mappingHelper = mappingHelper;
            _newsLetterSubscriptionService = newsLetterSubscriptionService;
            _languageService = languageService;
            _encryptionService = encryptionService;
            _genericAttributeService = genericAttributeService;
            _customerRolesHelper = customerRolesHelper;
        }

        /// <summary>
        /// Retrieve all customers of a shop
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(CustomersRootObject))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetCustomers(CustomersParametersModel parameters)
        {
            if (parameters.Limit < Configurations.MinLimit || parameters.Limit > Configurations.MaxLimit)
            {
                return Error(HttpStatusCode.BadRequest, "limit", "Invalid limit parameter");
            }

            if (parameters.Page < Configurations.DefaultPageValue)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid request parameters");
            }

            IList<CustomerDto> allCustomers = _customerApiService.GetCustomersDtos(parameters.CreatedAtMin, parameters.CreatedAtMax, parameters.Limit, parameters.Page, parameters.SinceId);

            var customersRootObject = new CustomersRootObject()
            {
                Customers = allCustomers
            };

            var json = _jsonFieldsSerializer.Serialize(customersRootObject, parameters.Fields);

            return new RawJsonActionResult(json);
        }

        /// <summary>
        /// Retrieve customer by spcified id
        /// </summary>
        /// <param name="id">Id of the customer</param>
        /// <param name="fields">Fields from the customer you want your json to contain</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(CustomersRootObject))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetCustomerById(int id, string fields = "")
        {
            if (id <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "id", "invalid id");
            }

            CustomerDto customer = _customerApiService.GetCustomerById(id);

            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }
            
            var customersRootObject = new CustomersRootObject();
            customersRootObject.Customers.Add(customer);

            var json = _jsonFieldsSerializer.Serialize(customersRootObject, fields);

            return new RawJsonActionResult(json);
        }


        /// <summary>
        /// Get a count of all customers
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(CustomersCountRootObject))]
        public IHttpActionResult GetCustomersCount()
        {
            var allCustomersCount = _customerApiService.GetCustomersCount();

            var customersCountRootObject = new CustomersCountRootObject()
            {
                Count = allCustomersCount
            };

            return Ok(customersCountRootObject);
        }

        /// <summary>
        /// Search for customers matching supplied query
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad Request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        public IHttpActionResult Search(CustomersSearchParametersModel parameters)
        {
            if (parameters.Limit <= Configurations.MinLimit || parameters.Limit > Configurations.MaxLimit)
            {
                return Error(HttpStatusCode.BadRequest, "limit" ,"Invalid limit parameter");
            }

            if (parameters.Page <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid page parameter");
            }
            
            IList<CustomerDto> customersDto = _customerApiService.Search(parameters.Query, parameters.Order, parameters.Page, parameters.Limit);

            var customersRootObject = new CustomersRootObject()
            {
                Customers = customersDto
            };

            var json = _jsonFieldsSerializer.Serialize(customersRootObject, parameters.Fields);

            return new RawJsonActionResult(json);
        }

        [HttpPost]
        [ResponseType(typeof(CustomersRootObject))]
        public IHttpActionResult CreateNewCustomer([ModelBinder(typeof(JsonModelBinder<CustomerDto>))] Delta<CustomerDto> customerDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            //If the validation has passed the customerDelta object won't be null for sure so we don't need to check for this.

            // Inserting the new customer
            Customer newCustomer = _factory.Initialize();
            customerDelta.Merge(newCustomer);

            foreach (var address in customerDelta.Dto.CustomerAddresses)
            {
                newCustomer.Addresses.Add(address.ToEntity());
            }
            
            _customerService.InsertCustomer(newCustomer);

            InsertFirstAndLastNameGenericAttributes(customerDelta.Dto.FirstName, customerDelta.Dto.LastName, newCustomer);

            int languageId = 0;

            if (!string.IsNullOrEmpty(customerDelta.Dto.LanguageId) && int.TryParse(customerDelta.Dto.LanguageId, out languageId)
                && _languageService.GetLanguageById(languageId) != null)
            {
                _genericAttributeService.SaveAttribute(newCustomer, SystemCustomerAttributeNames.LanguageId, languageId);
            }

            //password
            if (!string.IsNullOrWhiteSpace(customerDelta.Dto.Password))
            {
                AddPassword(customerDelta.Dto.Password, newCustomer);
            }
            
            // We need to insert the entity first so we can have its id in order to map it to anything.
            // TODO: Localization
            // TODO: move this before inserting the customer.
            if (customerDelta.Dto.RoleIds.Count > 0)
            {
                AddValidRoles(customerDelta, newCustomer);

                _customerService.UpdateCustomer(newCustomer);
            }

            // Preparing the result dto of the new customer
            // We do not prepare the shopping cart items because we have a separate endpoint for them.
            CustomerDto newCustomerDto = newCustomer.ToDto();

            // This is needed because the entity framework won't populate the navigation properties automatically
            // and the country will be left null. So we do it by hand here.
            PopulateAddressCountryNames(newCustomerDto);

            // Set the fist and last name separately because they are not part of the customer entity, but are saved in the generic attributes.
            newCustomerDto.FirstName = customerDelta.Dto.FirstName;
            newCustomerDto.LastName = customerDelta.Dto.LastName;

            newCustomerDto.LanguageId = customerDelta.Dto.LanguageId;

            //activity log
            _customerActivityService.InsertActivity("AddNewCustomer", _localizationService.GetResource("ActivityLog.AddNewCustomer"), newCustomer.Id);

            var customersRootObject = new CustomersRootObject();

            customersRootObject.Customers.Add(newCustomerDto);

            var json = _jsonFieldsSerializer.Serialize(customersRootObject, string.Empty);

            return new RawJsonActionResult(json);
        }
        
        [HttpPut]
        [ResponseType(typeof(CustomersRootObject))]
        public IHttpActionResult UpdateCustomer([ModelBinder(typeof(JsonModelBinder<CustomerDto>))] Delta<CustomerDto> customerDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            //If the validation has passed the customerDelta object won't be null for sure so we don't need to check for this.
            
            // Updating the customer
            Customer currentCustomer = _customerApiService.GetCustomerEntityById(int.Parse(customerDelta.Dto.Id));

            if (currentCustomer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            customerDelta.Merge(currentCustomer);

            if (customerDelta.Dto.RoleIds.Count > 0)
            {
                // Remove all roles
                while (currentCustomer.CustomerRoles.Count > 0)
                {
                    currentCustomer.CustomerRoles.Remove(currentCustomer.CustomerRoles.First());
                }

                AddValidRoles(customerDelta, currentCustomer);
            }

            if (customerDelta.Dto.CustomerAddresses.Count > 0)
            {
                var currentCustomerAddresses = currentCustomer.Addresses.ToDictionary(address => address.Id, address => address);

                foreach (var passedAddress in customerDelta.Dto.CustomerAddresses)
                {
                    int passedAddressId = int.Parse(passedAddress.Id);
                    Address addressEntity = passedAddress.ToEntity();

                    if (currentCustomerAddresses.ContainsKey(passedAddressId))
                    {
                        _mappingHelper.Merge(passedAddress, currentCustomerAddresses[passedAddressId]);
                    }
                    else
                    {
                        currentCustomer.Addresses.Add(addressEntity);
                    }
                }
            }

            _customerService.UpdateCustomer(currentCustomer);

            InsertFirstAndLastNameGenericAttributes(customerDelta.Dto.FirstName, customerDelta.Dto.LastName, currentCustomer);


            int languageId = 0;

            if (!string.IsNullOrEmpty(customerDelta.Dto.LanguageId) && int.TryParse(customerDelta.Dto.LanguageId, out languageId)
                && _languageService.GetLanguageById(languageId) != null)
            {
                _genericAttributeService.SaveAttribute(currentCustomer, SystemCustomerAttributeNames.LanguageId, languageId);
            }

            //password
            if (!string.IsNullOrWhiteSpace(customerDelta.Dto.Password))
            {
                AddPassword(customerDelta.Dto.Password, currentCustomer);
            }
            
            // TODO: Localization
           
            // Preparing the result dto of the new customer
            // We do not prepare the shopping cart items because we have a separate endpoint for them.
            CustomerDto updatedCustomer = currentCustomer.ToDto();

            // This is needed because the entity framework won't populate the navigation properties automatically
            // and the country name will be left empty because the mapping depends on the navigation property
            // so we do it by hand here.
            PopulateAddressCountryNames(updatedCustomer);

            // Set the fist and last name separately because they are not part of the customer entity, but are saved in the generic attributes.
            var firstNameGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "FirstName");

            if (firstNameGenericAttribute != null)
            {
                updatedCustomer.FirstName = firstNameGenericAttribute.Value;
            }

            var lastNameGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "LastName");

            if (lastNameGenericAttribute != null)
            {
                updatedCustomer.LastName = lastNameGenericAttribute.Value;
            }

            var languageIdGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "LanguageId");

            if (languageIdGenericAttribute != null)
            {
                updatedCustomer.LanguageId = languageIdGenericAttribute.Value;
            }

            //activity log
            _customerActivityService.InsertActivity("UpdateCustomer", _localizationService.GetResource("ActivityLog.UpdateCustomer"), currentCustomer.Id);

            var customersRootObject = new CustomersRootObject();

            customersRootObject.Customers.Add(updatedCustomer);

            var json = _jsonFieldsSerializer.Serialize(customersRootObject, string.Empty);

            return new RawJsonActionResult(json);
        }

        [HttpDelete]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult DeleteCustomer(int id)
        {
            if (id <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "id", "invalid id");
            }

            Customer customer = _customerApiService.GetCustomerEntityById(id);

            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            _customerService.DeleteCustomer(customer);

            //remove newsletter subscription (if exists)
            foreach (var store in _storeService.GetAllStores())
            {
                var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(customer.Email, store.Id);
                if (subscription != null)
                    _newsLetterSubscriptionService.DeleteNewsLetterSubscription(subscription);
            }

            //activity log
            _customerActivityService.InsertActivity("DeleteCustomer", _localizationService.GetResource("ActivityLog.DeleteCustomer"), customer.Id);
            
            return new RawJsonActionResult("{}");
        }

        private void InsertFirstAndLastNameGenericAttributes(string firstName, string lastName, Customer newCustomer)
        {
            // we assume that if the first name is not sent then it will be null and in this case we don't want to update it
            if (firstName != null)
            {
                _genericAttributeService.SaveAttribute(newCustomer, SystemCustomerAttributeNames.FirstName, firstName);
            }

            if (lastName != null)
            {
                _genericAttributeService.SaveAttribute(newCustomer, SystemCustomerAttributeNames.LastName, lastName);
            }
        }

        private void AddValidRoles(Delta<CustomerDto> customerDelta, Customer currentCustomer)
        {
            IList<CustomerRole> validCustomerRoles =
                _customerRolesHelper.GetValidCustomerRoles(customerDelta.Dto.RoleIds).ToList();

            // Add all newly passed roles
            foreach (var role in validCustomerRoles)
            {
                currentCustomer.CustomerRoles.Add(role);
            }
        }

        private void PopulateAddressCountryNames(CustomerDto newCustomerDto)
        {
            foreach (var address in newCustomerDto.CustomerAddresses)
            {
                SetCountryName(address);
            }

            if (newCustomerDto.BillingAddress != null)
            {
                SetCountryName(newCustomerDto.BillingAddress);
            }

            if (newCustomerDto.ShippingAddress != null)
            {
                SetCountryName(newCustomerDto.ShippingAddress);
            }
        }

        private void SetCountryName(AddressDto address)
        {
            if (string.IsNullOrEmpty(address.CountryName) && address.CountryId.HasValue)
            {
                Country country = _countryService.GetCountryById(address.CountryId.Value);
                address.CountryName = country.Name;
            }
        }
        
        private void PopulateAddressStateNames(CustomerDto newCustomerDto)
        {
            foreach (var address in newCustomerDto.CustomerAddresses)
            {
                SetStateName(address);
            }

            if (newCustomerDto.BillingAddress != null)
            {
                SetStateName(newCustomerDto.BillingAddress);
            }

            if (newCustomerDto.ShippingAddress != null)
            {
                SetStateName(newCustomerDto.ShippingAddress);
            }
        }

        private void SetStateName(AddressDto address)
        {
            if (string.IsNullOrEmpty(address.StateProvinceName) && address.StateProvinceId.HasValue)
            {
                StateProvince state = _stateProvinceService.GetStateProvinceById(address.StateProvinceId.Value);
                address.StateProvinceName = state.Name;
            }
        }

        private void AddPassword(string newPassword, Customer customer)
        {
            // TODO: call this method before inserting the customer.
            var customerPassword = new CustomerPassword
            {
                Customer = customer,
                PasswordFormat = CustomerSettings.DefaultPasswordFormat,
                CreatedOnUtc = DateTime.UtcNow
            };

            switch (CustomerSettings.DefaultPasswordFormat)
            {
                case PasswordFormat.Clear:
                    {
                        customerPassword.Password = newPassword;
                    }
                    break;
                case PasswordFormat.Encrypted:
                    {
                        customerPassword.Password = _encryptionService.EncryptText(newPassword);
                    }
                    break;
                case PasswordFormat.Hashed:
                    {
                        string saltKey = _encryptionService.CreateSaltKey(5);
                        customerPassword.PasswordSalt = saltKey;
                        customerPassword.Password = _encryptionService.CreatePasswordHash(newPassword, saltKey, CustomerSettings.HashedPasswordFormat);
                    }
                    break;
            }

            _customerService.InsertCustomerPassword(customerPassword);

            // TODO: remove this.
            _customerService.UpdateCustomer(customer);
        }

        #region Trajan API

        /// <summary>
        /// Retrieve customer by spcified UserID
        /// </summary>
        /// <param name="userid">Id of the customer</param>
        /// <param name="fields">Fields from the customer you want your json to contain</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(Trajan_CustomerDto))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetFullCustomerById(string userid, string fields = "")
        {
            if (userid == "" || userid == null)
            {
                return Error(HttpStatusCode.BadRequest, "userid", "invalid userid");
            }

            int id = _customerApiService.GetIdByUserId(userid);

            CustomerDto customer = _customerApiService.GetCustomerById(id);

            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            Trajan_CustomerDto t_Customer = new Trajan_CustomerDto();
            t_Customer.UserId = customer.UserID;
            t_Customer.Title = customer.Title;
            t_Customer.FirstName = customer.FirstName;
            t_Customer.LastName = customer.LastName;
            t_Customer.Email = customer.Email;
            if (customer.BillingAddress.Id != "0")
            {
                t_Customer.BillingPhone = customer.BillingAddress.PhoneNumber;
                t_Customer.BillingAddress1 = customer.BillingAddress.Address1;
                t_Customer.BillingAddress2 = customer.BillingAddress.Address2;
                t_Customer.BillingCity = customer.BillingAddress.City;
                int stateid = customer.BillingAddress.StateProvinceId ?? 0;
                if (stateid != 0) { t_Customer.BillingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                t_Customer.BillingPostCode = customer.BillingAddress.ZipPostalCode;
                int countryid = customer.BillingAddress.CountryId ?? 0;
                if (countryid != 0) { t_Customer.BillingCountry = _countryService.GetCountryById(countryid).ThreeLetterIsoCode; }
            }
            if (customer.ShippingAddress.Id != "0")
            {
                t_Customer.ShippingPhone = customer.ShippingAddress.PhoneNumber;
                t_Customer.ShippingAddress1 = customer.ShippingAddress.Address1;
                t_Customer.ShippingAddress2 = customer.ShippingAddress.Address2;
                t_Customer.ShippingCity = customer.ShippingAddress.City;
                int stateid = customer.ShippingAddress.StateProvinceId ?? 0;
                if (stateid != 0) { t_Customer.ShippingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                t_Customer.ShippingPostCode = customer.ShippingAddress.ZipPostalCode;
                int countryid = customer.ShippingAddress.CountryId ?? 0;
                if (countryid != 0) { t_Customer.ShippingCountry = _countryService.GetCountryById(countryid).ThreeLetterIsoCode; }
            }
            t_Customer.Gender = customer.Gender;
            t_Customer.DateOfBirth = customer.DateOfBirth;

            return Content(HttpStatusCode.OK, t_Customer);
        }

        /// <summary>
        /// Retrieve customer by spcified Email
        /// </summary>
        /// <param name="email">Email of the customer</param>
        /// <param name="fields">Fields from the customer you want your json to contain</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(Trajan_CustomerDto))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetFullCustomerByEmail(string email, string fields = "")
        {
            if (email == "")
            {
                return Error(HttpStatusCode.BadRequest, "email", "invalid email");
            }

            int id = _customerApiService.GetIdByEmail(email);

            CustomerDto customer = _customerApiService.GetCustomerById(id);

            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            Trajan_CustomerDto t_Customer = new Trajan_CustomerDto();
            t_Customer.UserId = customer.UserID;
            t_Customer.Title = customer.Title;
            t_Customer.FirstName = customer.FirstName;
            t_Customer.LastName = customer.LastName;
            t_Customer.Email = customer.Email;
            if (customer.BillingAddress.Id != "0")
            {
                t_Customer.BillingPhone = customer.BillingAddress.PhoneNumber;
                t_Customer.BillingAddress1 = customer.BillingAddress.Address1;
                t_Customer.BillingAddress2 = customer.BillingAddress.Address2;
                t_Customer.BillingCity = customer.BillingAddress.City;
                int stateid = customer.BillingAddress.StateProvinceId ?? 0;
                if (stateid != 0) { t_Customer.BillingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                t_Customer.BillingPostCode = customer.BillingAddress.ZipPostalCode;
                int countryid = customer.BillingAddress.CountryId ?? 0;
                if (countryid != 0) { t_Customer.BillingCountry = _countryService.GetCountryById(countryid).ThreeLetterIsoCode; }
            }
            else if (customer.ShippingAddress.Id != "0")
            {
                t_Customer.ShippingPhone = customer.ShippingAddress.PhoneNumber;
                t_Customer.ShippingAddress1 = customer.ShippingAddress.Address1;
                t_Customer.ShippingAddress2 = customer.ShippingAddress.Address2;
                t_Customer.ShippingCity = customer.ShippingAddress.City;
                int stateid = customer.ShippingAddress.StateProvinceId ?? 0;
                if (stateid != 0) { t_Customer.ShippingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                t_Customer.ShippingPostCode = customer.ShippingAddress.ZipPostalCode;
                int countryid = customer.ShippingAddress.CountryId ?? 0;
                if (countryid != 0) { t_Customer.ShippingCountry = _countryService.GetCountryById(countryid).ThreeLetterIsoCode; }
            }
            t_Customer.Gender = customer.Gender;
            t_Customer.DateOfBirth = customer.DateOfBirth;

            return Content(HttpStatusCode.OK, t_Customer);
        }

        /// <summary>
        /// Retrieve all customers of a shop
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(Trajan_CustomerDto))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetAllCustomers(CustomersParametersModel parameters)
        {
            if (parameters.Limit < Configurations.MinLimit || parameters.Limit > Configurations.MaxLimit)
            {
                return Error(HttpStatusCode.BadRequest, "limit", "Invalid limit parameter");
            }

            if (parameters.Page < Configurations.DefaultPageValue)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid request parameters");
            }

            IList<CustomerDto> allCustomers = _customerApiService.GetCustomersDtos(parameters.CreatedAtMin, parameters.CreatedAtMax, parameters.Limit, parameters.Page, parameters.SinceId);

            List<Trajan_CustomerDto> allTrajanCustomers = new List<Trajan_CustomerDto>();

            foreach(CustomerDto customer in allCustomers)
            {
                Trajan_CustomerDto t_Customer = new Trajan_CustomerDto();
                t_Customer.UserId = customer.UserID;
                t_Customer.Title = customer.Title;
                t_Customer.FirstName = customer.FirstName;
                t_Customer.LastName = customer.LastName;
                t_Customer.Email = customer.Email;
                if (customer.BillingAddress.Id != "0")
                {
                    t_Customer.BillingPhone = customer.BillingAddress.PhoneNumber;
                    t_Customer.BillingAddress1 = customer.BillingAddress.Address1;
                    t_Customer.BillingAddress2 = customer.BillingAddress.Address2;
                    t_Customer.BillingCity = customer.BillingAddress.City;
                    int stateid = customer.BillingAddress.StateProvinceId ?? 0;
                    if (stateid != 0) { t_Customer.BillingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                    t_Customer.BillingPostCode = customer.BillingAddress.ZipPostalCode;
                    int countryid = customer.BillingAddress.CountryId ?? 0;
                    if (countryid != 0) { t_Customer.BillingCountry = _countryService.GetCountryById(countryid).ThreeLetterIsoCode; }
                }
                if (customer.ShippingAddress.Id != "0") 
                {
                    t_Customer.ShippingPhone = customer.ShippingAddress.PhoneNumber;
                    t_Customer.ShippingAddress1 = customer.ShippingAddress.Address1;
                    t_Customer.ShippingAddress2 = customer.ShippingAddress.Address2;
                    t_Customer.ShippingCity = customer.ShippingAddress.City;
                    int stateid = customer.ShippingAddress.StateProvinceId ?? 0;
                    if (stateid != 0) { t_Customer.ShippingState = _stateProvinceService.GetStateProvinceById(stateid).Abbreviation; }
                    t_Customer.ShippingPostCode = customer.ShippingAddress.ZipPostalCode;
                    int countryid = customer.ShippingAddress.CountryId ?? 0;
                    if (countryid != 0) { t_Customer.ShippingCountry = _countryService.GetCountryById(countryid).ThreeLetterIsoCode; }
                }
                t_Customer.Gender = customer.Gender;
                t_Customer.DateOfBirth = customer.DateOfBirth;

                allTrajanCustomers.Add(t_Customer);
            }

            return Content(HttpStatusCode.OK, allTrajanCustomers);
        }

        [HttpPut]
        [ResponseType(typeof(Trajan_Customer_Result_Dto))]
        public IHttpActionResult UpdateCustomerById(Trajan_CustomerDto customerInfo)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            //If the validation has passed the customerDelta object won't be null for sure so we don't need to check for this.

            // Check for valid UserId
            if (customerInfo.UserId == "" || string.IsNullOrEmpty(customerInfo.UserId) || customerInfo.UserId == null)
            {
                return Error(HttpStatusCode.BadRequest, "UserId", "invalid UserId");
            }

            int id = _customerApiService.GetIdByUserId(customerInfo.UserId);

            // Retrieve existing customer object
            Customer currentCustomer = _customerApiService.GetCustomerEntityById(int.Parse(id.ToString()));
            if (currentCustomer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            // Updating the customer - just update the fields as appropriate
            currentCustomer.Title = customerInfo.Title != "" && customerInfo.Title != null ? customerInfo.Title : currentCustomer.Title;
            currentCustomer.Email = customerInfo.Email != "" && customerInfo.Email != null ? customerInfo.Email : currentCustomer.Email;
            currentCustomer.Username = customerInfo.Email != "" && customerInfo.Email != null ? customerInfo.Email : currentCustomer.Username;

            Address currentBillingAddresses = new Address();
            Address currentShippingAddresses = new Address();
            if (currentCustomer.BillingAddress != null)
            {
                currentBillingAddresses = currentCustomer.BillingAddress;
            }
             if (currentCustomer.ShippingAddress != null)
            {
                currentShippingAddresses = currentCustomer.ShippingAddress;
            }
            if (currentBillingAddresses != null)
            {
                var passedAddress = currentBillingAddresses.ToDto();

                if (customerInfo.FirstName != "" && customerInfo.FirstName != null) { passedAddress.FirstName = customerInfo.FirstName; }
                if (customerInfo.LastName != "" && customerInfo.LastName != null) { passedAddress.LastName = customerInfo.LastName; }
                if (customerInfo.Email != "" && customerInfo.Email != null) { passedAddress.Email = customerInfo.Email; }
                if (customerInfo.BillingAddress1 != "" && customerInfo.BillingAddress1 != null) { passedAddress.Address1 = customerInfo.BillingAddress1; }
                if (customerInfo.BillingAddress2 != "" && customerInfo.BillingAddress2 != null) { passedAddress.Address2 = customerInfo.BillingAddress2; }
                if (customerInfo.BillingCity != "" && customerInfo.BillingCity != null) { passedAddress.City = customerInfo.BillingCity; }
                if (customerInfo.BillingPostCode != "" && customerInfo.BillingPostCode != null) { passedAddress.ZipPostalCode = customerInfo.BillingPostCode; }
                if (customerInfo.BillingState != "" && customerInfo.BillingState != null) { passedAddress.StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(customerInfo.BillingState).Id; }
                if (customerInfo.BillingCountry != "" && customerInfo.BillingCountry != null) { passedAddress.CountryId = _countryService.GetCountryByThreeLetterIsoCode(customerInfo.BillingCountry).Id; }
                if (customerInfo.BillingPhone != "" && customerInfo.BillingPhone != null) { passedAddress.PhoneNumber = customerInfo.BillingPhone; }

                _mappingHelper.Merge(passedAddress, currentBillingAddresses);
            }
            else
            {
                Address addressEntity = new Address();

                if (customerInfo.FirstName != "" && customerInfo.FirstName != null) { addressEntity.FirstName = customerInfo.FirstName; }
                if (customerInfo.LastName != "" && customerInfo.LastName != null) { addressEntity.LastName = customerInfo.LastName; }
                if (customerInfo.Email != "" && customerInfo.Email != null) { addressEntity.Email = customerInfo.Email; }
                if (customerInfo.BillingAddress1 != "" && customerInfo.BillingAddress1 != null) { addressEntity.Address1 = customerInfo.BillingAddress1; }
                if (customerInfo.BillingAddress2 != "" && customerInfo.BillingAddress2 != null) { addressEntity.Address2 = customerInfo.BillingAddress2; }
                if (customerInfo.BillingCity != "" && customerInfo.BillingCity != null) { addressEntity.City = customerInfo.BillingCity; }
                if (customerInfo.BillingPostCode != "" && customerInfo.BillingPostCode != null) { addressEntity.ZipPostalCode = customerInfo.BillingPostCode; }
                if (customerInfo.BillingState != "" && customerInfo.BillingState != null) { addressEntity.StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(customerInfo.BillingState).Id; }
                if (customerInfo.BillingCountry != "" && customerInfo.BillingCountry != null) { addressEntity.CountryId = _countryService.GetCountryByThreeLetterIsoCode(customerInfo.BillingCountry).Id; }
                if (customerInfo.BillingPhone != "" && customerInfo.BillingPhone != null) { addressEntity.PhoneNumber = customerInfo.BillingPhone; }
                currentCustomer.BillingAddress = addressEntity;
            }

            if (currentShippingAddresses != null)
            {
                var passedAddress = currentShippingAddresses.ToDto();

                if (customerInfo.FirstName != "" && customerInfo.FirstName != null) { passedAddress.FirstName = customerInfo.FirstName; }
                if (customerInfo.LastName != "" && customerInfo.LastName != null) { passedAddress.LastName = customerInfo.LastName; }
                if (customerInfo.Email != "" && customerInfo.Email != null) { passedAddress.Email = customerInfo.Email; }
                if (customerInfo.ShippingAddress1 != "" && customerInfo.ShippingAddress1 != null) { passedAddress.Address1 = customerInfo.ShippingAddress1; }
                if (customerInfo.ShippingAddress2 != "" && customerInfo.ShippingAddress2 != null) { passedAddress.Address2 = customerInfo.ShippingAddress2; }
                if (customerInfo.ShippingCity != "" && customerInfo.ShippingCity != null) { passedAddress.City = customerInfo.ShippingCity; }
                if (customerInfo.ShippingPostCode != "" && customerInfo.ShippingPostCode != null) { passedAddress.ZipPostalCode = customerInfo.ShippingPostCode; }
                if (customerInfo.ShippingState != "" && customerInfo.ShippingState != null) { passedAddress.StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(customerInfo.ShippingState).Id; }
                if (customerInfo.ShippingCountry != "" && customerInfo.ShippingCountry != null) { passedAddress.CountryId = _countryService.GetCountryByThreeLetterIsoCode(customerInfo.ShippingCountry).Id; }
                if (customerInfo.ShippingPhone != "" && customerInfo.ShippingPhone != null) { passedAddress.PhoneNumber = customerInfo.ShippingPhone; }

                _mappingHelper.Merge(passedAddress, currentShippingAddresses);
            }
            else
            {
                Address addressEntity = new Address();

                if (customerInfo.FirstName != "" && customerInfo.FirstName != null) { addressEntity.FirstName = customerInfo.FirstName; }
                if (customerInfo.LastName != "" && customerInfo.LastName != null) { addressEntity.LastName = customerInfo.LastName; }
                if (customerInfo.Email != "" && customerInfo.Email != null) { addressEntity.Email = customerInfo.Email; }
                if (customerInfo.ShippingAddress1 != "" && customerInfo.ShippingAddress1 != null) { addressEntity.Address1 = customerInfo.ShippingAddress1; }
                if (customerInfo.ShippingAddress2 != "" && customerInfo.ShippingAddress2 != null) { addressEntity.Address2 = customerInfo.ShippingAddress2; }
                if (customerInfo.ShippingCity != "" && customerInfo.ShippingCity != null) { addressEntity.City = customerInfo.ShippingCity; }
                if (customerInfo.ShippingPostCode != "" && customerInfo.ShippingPostCode != null) { addressEntity.ZipPostalCode = customerInfo.ShippingPostCode; }
                if (customerInfo.ShippingState != "" && customerInfo.ShippingState != null) { addressEntity.StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(customerInfo.ShippingState).Id; }
                if (customerInfo.ShippingCountry != "" && customerInfo.ShippingCountry != null) { addressEntity.CountryId = _countryService.GetCountryByThreeLetterIsoCode(customerInfo.ShippingCountry).Id; }
                if (customerInfo.ShippingPhone != "" && customerInfo.ShippingPhone != null) { addressEntity.PhoneNumber = customerInfo.ShippingPhone; }
                currentCustomer.ShippingAddress = addressEntity;
            }

            _customerService.UpdateCustomer(currentCustomer);

            InsertFirstAndLastNameGenericAttributes(customerInfo.FirstName, customerInfo.LastName, currentCustomer);
            if (customerInfo.Gender != "" && customerInfo.Gender != null) { _genericAttributeService.SaveAttribute(currentCustomer, SystemCustomerAttributeNames.Gender, customerInfo.Gender); }
            if (customerInfo.DateOfBirth.HasValue && customerInfo.DateOfBirth != null) { _genericAttributeService.SaveAttribute(currentCustomer, SystemCustomerAttributeNames.DateOfBirth, customerInfo.DateOfBirth); }



            // TODO: Localization

            // Preparing the result dto of the new customer
            // We do not prepare the shopping cart items because we have a separate endpoint for them.
            CustomerDto updatedCustomer = currentCustomer.ToDto();

            // This is needed because the entity framework won't populate the navigation properties automatically
            // and the country name will be left empty because the mapping depends on the navigation property
            // so we do it by hand here.
            PopulateAddressCountryNames(updatedCustomer);
            PopulateAddressStateNames(updatedCustomer);

            // Set the first and last name separately because they are not part of the customer entity, but are saved in the generic attributes.
            var firstNameGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "FirstName");

            if (firstNameGenericAttribute != null)
            {
                updatedCustomer.FirstName = firstNameGenericAttribute.Value;
            }

            var lastNameGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "LastName");

            if (lastNameGenericAttribute != null)
            {
                updatedCustomer.LastName = lastNameGenericAttribute.Value;
            }

            var dateOfBirthIdGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "DateOfBirth");

            if (dateOfBirthIdGenericAttribute != null)
            {
                updatedCustomer.DateOfBirth = DateTime.Parse(dateOfBirthIdGenericAttribute.Value);
            }

            var genderIdGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "Gender");

            if (genderIdGenericAttribute != null)
            {
                updatedCustomer.Gender = genderIdGenericAttribute.Value;
            }

            //activity log
            _customerActivityService.InsertActivity("UpdateCustomer", _localizationService.GetResource("ActivityLog.UpdateCustomer"), currentCustomer.Id);

            //return Trajan specific result
            var result = new Trajan_Customer_Result_Dto()
            { UserID = updatedCustomer.UserID };

            return Content(HttpStatusCode.OK, result);
        }

        [HttpPost]
        [ResponseType(typeof(Trajan_Customer_Result_Dto))]
        public IHttpActionResult CreateCustomer(Trajan_CustomerDto customerInfo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // If the validation has passed the customerDelta object won't be null for sure so we don't need to check for this.

            // Check for existing UserId
            if (customerInfo.UserId != null && customerInfo.UserId != "")
            {
                int id = _customerApiService.GetIdByUserId(customerInfo.UserId);
                if (id != 0) { return Error(HttpStatusCode.BadRequest, "UserId", "Customer already exists"); }
            }

            // Inserting the new customer
            Customer newCustomer = _factory.Initialize();
            newCustomer.Title = customerInfo.Title;
            newCustomer.Username = customerInfo.Email;
            newCustomer.Email = customerInfo.Email;

            // validate unique user
            if (_customerService.GetCustomerByEmail(newCustomer.Email) != null)
            {
                return Error(HttpStatusCode.NotFound, "customer", _localizationService.GetResource("Account.Register.Errors.EmailAlreadyExists"));
            }

            _customerService.InsertCustomer(newCustomer);
            if (customerInfo.UserId != null && customerInfo.UserId != "")
            {
                newCustomer.UserID = customerInfo.UserId;
            }
            else
            {
                newCustomer.UserID = "T" + newCustomer.Id.ToString();
            }
            newCustomer.Email = customerInfo.Email != "" ? customerInfo.Email : newCustomer.UserID + "@monitoryou.com";
            newCustomer.Username = newCustomer.Username != "" ? customerInfo.Email : newCustomer.UserID + "@monitoryou.com";

            if ((customerInfo.ShippingAddress1 != "" && customerInfo.ShippingAddress1 != null)
               || (customerInfo.ShippingAddress2 != "" && customerInfo.ShippingAddress2 != null)
               || (customerInfo.ShippingCity != "" && customerInfo.ShippingCity != null)
               || (customerInfo.ShippingPostCode != "" && customerInfo.ShippingPostCode != null)
               || (customerInfo.ShippingState != "" && customerInfo.ShippingState != null)
               || (customerInfo.ShippingCountry != "" && customerInfo.ShippingCountry != null)
               || (customerInfo.ShippingPhone != "" && customerInfo.ShippingPhone != null))
            {
                var shipAddress = new AddressDto();
                shipAddress.CreatedOnUtc = DateTime.UtcNow;
                if (customerInfo.FirstName != "" && customerInfo.FirstName != null) { shipAddress.FirstName = customerInfo.FirstName; }
                if (customerInfo.LastName != "" && customerInfo.LastName != null) { shipAddress.LastName = customerInfo.LastName; }
                shipAddress.Email = newCustomer.Email;
                if (customerInfo.ShippingAddress1 != "" && customerInfo.ShippingAddress1 != null) { shipAddress.Address1 = customerInfo.ShippingAddress1; }
                if (customerInfo.ShippingAddress2 != "" && customerInfo.ShippingAddress2 != null) { shipAddress.Address2 = customerInfo.ShippingAddress2; }
                if (customerInfo.ShippingCity != "" && customerInfo.ShippingCity != null) { shipAddress.City = customerInfo.ShippingCity; }
                if (customerInfo.ShippingPostCode != "" && customerInfo.ShippingPostCode != null) { shipAddress.ZipPostalCode = customerInfo.ShippingPostCode; }
                if (customerInfo.ShippingState != "" && customerInfo.ShippingState != null) { shipAddress.StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(customerInfo.ShippingState).Id; }
                if (customerInfo.ShippingCountry != "" && customerInfo.ShippingCountry != null) { shipAddress.CountryId = _countryService.GetCountryByThreeLetterIsoCode(customerInfo.ShippingCountry).Id; }
                if (customerInfo.ShippingPhone != "" && customerInfo.ShippingPhone != null) { shipAddress.PhoneNumber = customerInfo.ShippingPhone; }
                shipAddress.CustomAttributes = "ship";
                newCustomer.Addresses.Add(shipAddress.ToEntity());
            }

            if ((customerInfo.BillingAddress1 != "" && customerInfo.BillingAddress1 != null)
                || (customerInfo.BillingAddress2 != "" && customerInfo.BillingAddress2 != null)
                || (customerInfo.BillingCity != "" && customerInfo.BillingCity != null)
                || (customerInfo.BillingPostCode != "" && customerInfo.BillingPostCode != null)
                || (customerInfo.BillingState != "" && customerInfo.BillingState != null)
                || (customerInfo.BillingCountry != "" && customerInfo.BillingCountry != null)
                || (customerInfo.BillingPhone != "" && customerInfo.BillingPhone != null))
            {
                var billAddress = new AddressDto();
                billAddress.CreatedOnUtc = DateTime.UtcNow;
                if (customerInfo.FirstName != "" && customerInfo.FirstName != null) { billAddress.FirstName = customerInfo.FirstName; }
                if (customerInfo.LastName != "" && customerInfo.LastName != null) { billAddress.LastName = customerInfo.LastName; }
                billAddress.Email = newCustomer.Email;
                if (customerInfo.BillingAddress1 != "" && customerInfo.BillingAddress1 != null) { billAddress.Address1 = customerInfo.BillingAddress1; }
                if (customerInfo.BillingAddress2 != "" && customerInfo.BillingAddress2 != null) { billAddress.Address2 = customerInfo.BillingAddress2; }
                if (customerInfo.BillingCity != "" && customerInfo.BillingCity != null) { billAddress.City = customerInfo.BillingCity; }
                if (customerInfo.BillingPostCode != "" && customerInfo.BillingPostCode != null) { billAddress.ZipPostalCode = customerInfo.BillingPostCode; }
                if (customerInfo.BillingState != "" && customerInfo.BillingState != null) { billAddress.StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(customerInfo.BillingState).Id; }
                if (customerInfo.BillingCountry != "" && customerInfo.BillingCountry != null) { billAddress.CountryId = _countryService.GetCountryByThreeLetterIsoCode(customerInfo.BillingCountry).Id; }
                if (customerInfo.BillingPhone != "" && customerInfo.BillingPhone != null) { billAddress.PhoneNumber = customerInfo.BillingPhone; }
                billAddress.CustomAttributes = "bill";
                newCustomer.Addresses.Add(billAddress.ToEntity());
            }

            _customerService.UpdateCustomer(newCustomer);
            
            //Update the billing and shipping addresses
            newCustomer.ShippingAddress = newCustomer.Addresses.Where(x => x.CustomAttributes == "ship").FirstOrDefault();
            newCustomer.BillingAddress = newCustomer.Addresses.Where(x => x.CustomAttributes == "bill").FirstOrDefault();
            _customerService.UpdateCustomer(newCustomer);

            // Insert Generic Attributes
            InsertFirstAndLastNameGenericAttributes(customerInfo.FirstName, customerInfo.LastName, newCustomer);

            int languageId = 0;
            _genericAttributeService.SaveAttribute(newCustomer, SystemCustomerAttributeNames.LanguageId, languageId);

            _genericAttributeService.SaveAttribute(newCustomer, SystemCustomerAttributeNames.Gender, customerInfo.Gender);
            _genericAttributeService.SaveAttribute(newCustomer, SystemCustomerAttributeNames.DateOfBirth, customerInfo.DateOfBirth);

            // Set the roles to 'Registered' 
            var roles = new List<int>() { 3 };
            IList<CustomerRole> validCustomerRoles =
                _customerRolesHelper.GetValidCustomerRoles(roles).ToList();

            // Add all newly passed roles
            foreach (var role in validCustomerRoles)
            {
                newCustomer.CustomerRoles.Add(role);
            }

            // Preparing the result dto of the new customer
            // We do not prepare the shopping cart items because we have a separate endpoint for them.
            CustomerDto newCustomerDto = newCustomer.ToDto();

            // This is needed because the entity framework won't populate the navigation properties automatically
            // and the country will be left null. So we do it by hand here.
            PopulateAddressCountryNames(newCustomerDto);
            PopulateAddressStateNames(newCustomerDto);

            // Set the fist and last name separately because they are not part of the customer entity, but are saved in the generic attributes.
            newCustomerDto.FirstName = customerInfo.FirstName;
            newCustomerDto.LastName = customerInfo.LastName;

            newCustomerDto.LanguageId = "0";

            //activity log
            _customerActivityService.InsertActivity("AddNewCustomer", _localizationService.GetResource("ActivityLog.AddNewCustomer"), newCustomer.Id);

            var customersRootObject = new CustomersRootObject();

            customersRootObject.Customers.Add(newCustomerDto);

            //return Trajan specific result
            var result = new Trajan_Customer_Result_Dto()
            { UserID = newCustomerDto.UserID };

            return Content(HttpStatusCode.OK, result);

        }

        // No longer required
        [HttpPost]
        [ResponseType(typeof(CustomersRootObject))]
        public IHttpActionResult CreateCustomer2([ModelBinder(typeof(JsonModelBinder<CustomerDto>))] Delta<CustomerDto> customerDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            //If the validation has passed the customerDelta object won't be null for sure so we don't need to check for this.

            // Inserting the new customer
            Customer newCustomer = _factory.Initialize();
            customerDelta.Merge(newCustomer);

            //validate unique user
            if (_customerService.GetCustomerByEmail(newCustomer.Email) != null)
            {
                return Error(HttpStatusCode.NotFound, "customer", _localizationService.GetResource("Account.Register.Errors.EmailAlreadyExists"));
            }

            foreach (var address in customerDelta.Dto.CustomerAddresses)
            {
                newCustomer.Addresses.Add(address.ToEntity());
            }

            _customerService.InsertCustomer(newCustomer);
            newCustomer.UserID = "T" + newCustomer.Id.ToString();
            _customerService.UpdateCustomer(newCustomer);

            InsertFirstAndLastNameGenericAttributes(customerDelta.Dto.FirstName, customerDelta.Dto.LastName, newCustomer);

            int languageId = 0;

            if (!string.IsNullOrEmpty(customerDelta.Dto.LanguageId) && int.TryParse(customerDelta.Dto.LanguageId, out languageId)
                && _languageService.GetLanguageById(languageId) != null)
            {
                _genericAttributeService.SaveAttribute(newCustomer, SystemCustomerAttributeNames.LanguageId, languageId);
            }

            //password
            if (!string.IsNullOrWhiteSpace(customerDelta.Dto.Password))
            {
                AddPassword(customerDelta.Dto.Password, newCustomer);
            }

            // We need to insert the entity first so we can have its id in order to map it to anything.
            // TODO: Localization
            // TODO: move this before inserting the customer.
            if (customerDelta.Dto.RoleIds.Count > 0)
            {
                AddValidRoles(customerDelta, newCustomer);

                _customerService.UpdateCustomer(newCustomer);
            }

            // Preparing the result dto of the new customer
            // We do not prepare the shopping cart items because we have a separate endpoint for them.
            CustomerDto newCustomerDto = newCustomer.ToDto();

            // This is needed because the entity framework won't populate the navigation properties automatically
            // and the country will be left null. So we do it by hand here.
            PopulateAddressCountryNames(newCustomerDto);

            // Set the fist and last name separately because they are not part of the customer entity, but are saved in the generic attributes.
            newCustomerDto.FirstName = customerDelta.Dto.FirstName;
            newCustomerDto.LastName = customerDelta.Dto.LastName;

            newCustomerDto.LanguageId = customerDelta.Dto.LanguageId;

            //activity log
            _customerActivityService.InsertActivity("AddNewCustomer", _localizationService.GetResource("ActivityLog.AddNewCustomer"), newCustomer.Id);

            var customersRootObject = new CustomersRootObject();

            customersRootObject.Customers.Add(newCustomerDto);

            var json = _jsonFieldsSerializer.Serialize(customersRootObject, "userid");

            //return new RawJsonActionResult(json);
            return Content(HttpStatusCode.OK, string.Format("Customer id {0} created", json));
        }

        [HttpPut]
        [ResponseType(typeof(CustomersRootObject))]
        public IHttpActionResult UpdateCustomerById2([ModelBinder(typeof(JsonModelBinder<CustomerDto>))] Delta<CustomerDto> customerDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            //If the validation has passed the customerDelta object won't be null for sure so we don't need to check for this.

            // Updating the customer
            //Customer currentCustomer = _customerApiService.GetCustomerEntityById(int.Parse(customerDelta.Dto.Id));
            Customer currentCustomer = _customerApiService.GetCustomerEntityById(int.Parse(customerDelta.Dto.Id));

            if (currentCustomer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            customerDelta.Merge(currentCustomer);

            if (customerDelta.Dto.RoleIds.Count > 0)
            {
                // Remove all roles
                while (currentCustomer.CustomerRoles.Count > 0)
                {
                    currentCustomer.CustomerRoles.Remove(currentCustomer.CustomerRoles.First());
                }

                AddValidRoles(customerDelta, currentCustomer);
            }

            if (customerDelta.Dto.CustomerAddresses.Count > 0)
            {
                var currentCustomerAddresses = currentCustomer.Addresses.ToDictionary(address => address.Id, address => address);

                foreach (var passedAddress in customerDelta.Dto.CustomerAddresses)
                {
                    int passedAddressId = int.Parse(passedAddress.Id);
                    Address addressEntity = passedAddress.ToEntity();

                    if (currentCustomerAddresses.ContainsKey(passedAddressId))
                    {
                        _mappingHelper.Merge(passedAddress, currentCustomerAddresses[passedAddressId]);
                    }
                    else
                    {
                        currentCustomer.Addresses.Add(addressEntity);
                    }
                }
            }

            _customerService.UpdateCustomer(currentCustomer);

            InsertFirstAndLastNameGenericAttributes(customerDelta.Dto.FirstName, customerDelta.Dto.LastName, currentCustomer);


            int languageId = 0;

            if (!string.IsNullOrEmpty(customerDelta.Dto.LanguageId) && int.TryParse(customerDelta.Dto.LanguageId, out languageId)
                && _languageService.GetLanguageById(languageId) != null)
            {
                _genericAttributeService.SaveAttribute(currentCustomer, SystemCustomerAttributeNames.LanguageId, languageId);
            }

            //password
            if (!string.IsNullOrWhiteSpace(customerDelta.Dto.Password))
            {
                AddPassword(customerDelta.Dto.Password, currentCustomer);
            }

            // TODO: Localization

            // Preparing the result dto of the new customer
            // We do not prepare the shopping cart items because we have a separate endpoint for them.
            CustomerDto updatedCustomer = currentCustomer.ToDto();

            // This is needed because the entity framework won't populate the navigation properties automatically
            // and the country name will be left empty because the mapping depends on the navigation property
            // so we do it by hand here.
            PopulateAddressCountryNames(updatedCustomer);

            // Set the fist and last name separately because they are not part of the customer entity, but are saved in the generic attributes.
            var firstNameGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "FirstName");

            if (firstNameGenericAttribute != null)
            {
                updatedCustomer.FirstName = firstNameGenericAttribute.Value;
            }

            var lastNameGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "LastName");

            if (lastNameGenericAttribute != null)
            {
                updatedCustomer.LastName = lastNameGenericAttribute.Value;
            }

            var languageIdGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "LanguageId");

            if (languageIdGenericAttribute != null)
            {
                updatedCustomer.LanguageId = languageIdGenericAttribute.Value;
            }

            //activity log
            _customerActivityService.InsertActivity("UpdateCustomer", _localizationService.GetResource("ActivityLog.UpdateCustomer"), currentCustomer.Id);

            var customersRootObject = new CustomersRootObject();

            customersRootObject.Customers.Add(updatedCustomer);

            var json = _jsonFieldsSerializer.Serialize(customersRootObject, string.Empty);

            //return new RawJsonActionResult(json);
            return Content(HttpStatusCode.OK, string.Format("Customer id {0} updated", updatedCustomer.Id.ToString()));
        }

        /// <summary>
        /// Retrieve customer Id by spcified UserID
        /// </summary>
        /// <param name="userid">UserId of the customer</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [ResponseType(typeof(CustomersRootObject))]
        [GetRequestsErrorInterceptorActionFilter]
        public IHttpActionResult GetCustomerIdByUserId(string userid)
        {
            if (userid == "")
            {
                return Error(HttpStatusCode.BadRequest, "userid", "invalid userid");
            }

            int id = _customerApiService.GetIdByUserId(userid);

            CustomerDto customer = _customerApiService.GetCustomerById(id);

            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            var customersRootObject = new CustomersRootObject();
            customersRootObject.Customers.Add(customer);

            var json = _jsonFieldsSerializer.Serialize(customersRootObject, "id");

            return new RawJsonActionResult(json);
        }

        #endregion
    }
}