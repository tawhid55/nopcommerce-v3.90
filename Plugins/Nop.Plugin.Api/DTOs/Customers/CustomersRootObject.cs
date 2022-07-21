using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nop.Plugin.Api.DTOs.Customers
{
    public class CustomersRootObject : ISerializableObject
    {
        public CustomersRootObject()
        {
            Customers = new List<CustomerDto>();
        }

        [JsonProperty("customers")]
        public IList<CustomerDto> Customers { get; set; }

        public string GetPrimaryPropertyName()
        {
            return "customers";
        }

        public Type GetPrimaryPropertyType()
        {
            return typeof(CustomerDto);
        }
    }
    public class TrajanCustomersRootObject : ISerializableObject
    {
        public TrajanCustomersRootObject()
        {
            Customers = new List<Trajan_CustomerDto>();
        }

        [JsonProperty("customers")]
        public IList<Trajan_CustomerDto> Customers { get; set; }

        public string GetPrimaryPropertyName()
        {
            return "customers";
        }

        public Type GetPrimaryPropertyType()
        {
            return typeof(Trajan_CustomerDto);
        }
    }
}