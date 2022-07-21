using Nop.Plugin.Api.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Api.Models.Response
{
    public class BaseResponse
    {
        public BaseResponse()
        {
            Error = new ErrorResponse();
            Code = (int)ErrorType.Ok;
        }
        public bool Success { get; set; }
        public int Code { get; set; }
        public ErrorResponse Error { get; set; }
    }
    public class ErrorResponse
    {
        public string Message { get; set; }
    }
}
