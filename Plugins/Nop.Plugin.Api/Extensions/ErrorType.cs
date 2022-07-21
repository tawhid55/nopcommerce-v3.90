using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Api.Extensions
{
    public enum ErrorType
    {
        Ok = 200,
        NotOk = 400,
        AuthenticationError = 600
    }
}
