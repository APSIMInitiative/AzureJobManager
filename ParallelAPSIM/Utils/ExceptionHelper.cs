using System;
using System.Net;
using System.Net.Http;

namespace ParallelAPSIM.Utils
{
    public static class ExceptionHelper
    {
        public static Exception UnwrapAggregateException(AggregateException ae)
        {
            if (ae.InnerException != null)
            {
                var ex = ae.InnerException as HttpRequestException;

                if (ex != null)
                {
                    var webEx = ex.InnerException as WebException;

                    if (webEx != null)
                    {
                        return webEx;
                    }

                    return ex;
                }

                return ae.InnerException;
            }

            return ae;
        }
    }
}
