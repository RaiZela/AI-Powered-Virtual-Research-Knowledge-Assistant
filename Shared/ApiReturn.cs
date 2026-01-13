using System;
using System.Collections.Generic;
using System.Text;

namespace Shared;

public class ApiReturn<T> where T : class
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public T Result { get; set; }
}
