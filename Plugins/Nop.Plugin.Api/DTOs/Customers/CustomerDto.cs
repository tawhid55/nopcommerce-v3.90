using System;
using System.Collections.Generic;
using FluentValidation.Attributes;
using Newtonsoft.Json;
using Nop.Plugin.Api.Attributes;
using Nop.Plugin.Api.DTOs.ShoppingCarts;
using Nop.Plugin.Api.Validators;

namespace Nop.Plugin.Api.DTOs.Customers
{
    [JsonObject(Title = "customer")]
    [Validator(typeof(CustomerDtoValidator))]
    public class CustomerDto : BaseCustomerDto
    {
        private ICollection<ShoppingCartItemDto> _shoppingCartItems;
        private ICollection<AddressDto> _addresses;

        [JsonIgnore]
        [JsonProperty("password")]
        public string Password { get; set; }

        #region Navigation properties

        /// <summary>
        /// Gets or sets shopping cart items
        /// </summary>
        [JsonProperty("shopping_cart_items")]
        [DoNotMap]
        public ICollection<ShoppingCartItemDto> ShoppingCartItems
        {
            get
            {
                if (_shoppingCartItems == null)
                {
                    _shoppingCartItems = new List<ShoppingCartItemDto>();
                }

                return _shoppingCartItems;
            }
            set { _shoppingCartItems = value; }
        }

        /// <summary>
        /// Default billing address
        /// </summary>
        [JsonProperty("billing_address")]
        public AddressDto BillingAddress { get; set; }

        /// <summary>
        /// Default shipping address
        /// </summary>
        [JsonProperty("shipping_address")]
        public AddressDto ShippingAddress { get; set; }

        /// <summary>
        /// Gets or sets customer addresses
        /// </summary>
        [JsonProperty("addresses")]
        public ICollection<AddressDto> CustomerAddresses
        {
            get
            {
                if (_addresses == null)
                {
                    _addresses = new List<AddressDto>();
                }

                return _addresses;
            }
            set { _addresses = value; }
        }
        #endregion
    }


    [JsonObject(Title = "trajan_customer")]
    public class Trajan_CustomerDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("userid")]
        public string UserId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("shippingaddress1")]
        public string ShippingAddress1 { get; set; }

        [JsonProperty("shippingaddress2")]
        public string ShippingAddress2 { get; set; }

        [JsonProperty("shippingcity")]
        public string ShippingCity { get; set; }

        [JsonProperty("shippingpostcode")]
        public string ShippingPostCode { get; set; }

        [JsonProperty("shippingphone")]
        public string ShippingPhone { get; set; }

        [JsonProperty("shippingstate")]
        public string ShippingState { get; set; }

        [JsonProperty("shippingcountry")]
        public string ShippingCountry { get; set; }

        [JsonProperty("billingaddress1")]
        public string BillingAddress1 { get; set; }

        [JsonProperty("billingaddress2")]
        public string BillingAddress2 { get; set; }

        [JsonProperty("billingcity")]
        public string BillingCity { get; set; }

        [JsonProperty("billingpostcode")]
        public string BillingPostCode { get; set; }

        [JsonProperty("billingphone")]
        public string BillingPhone { get; set; }

        [JsonProperty("billingstate")]
        public string BillingState { get; set; }

        [JsonProperty("billingcountry")]
        public string BillingCountry { get; set; }

        [JsonProperty("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [JsonProperty("gender")]
        public string Gender { get; set; }

    }

    [JsonObject(Title = "trajan_customer")]
    public class Trajan_Customer_Result_Dto
    {
        [JsonProperty("userid")]
        public string UserID { get; set; }
    }
}
