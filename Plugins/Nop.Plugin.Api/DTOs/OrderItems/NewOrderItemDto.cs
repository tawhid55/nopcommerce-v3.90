using FluentValidation.Attributes;
using Newtonsoft.Json;
using Nop.Plugin.Api.Attributes;
using Nop.Plugin.Api.DTOs.Products;
using Nop.Plugin.Api.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Api.DTOs.OrderItems
{
    [Validator(typeof(OrderItemDtoValidator))]
    [JsonObject(Title = "new_order_item")]
    public class NewOrderItemDto
    {
        [JsonProperty("sku")]
        public string SKU { get; set; }

        [JsonProperty("unit_price_incl_tax")]
        public decimal? UnitPriceInclTax { get; set; }

        [JsonProperty("quantity")]
        public int? Quantity { get; set; }

        [JsonProperty("product_id")]
        public int? ProductId { get; set; }
    }
}
