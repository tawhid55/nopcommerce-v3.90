using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Api.Models.Response
{
    public class GeneralResponseModel<TResult> : BaseResponse
    {
        public TResult Payload { get; set; }
    }
}
