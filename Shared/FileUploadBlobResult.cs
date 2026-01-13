using System;
using System.Collections.Generic;
using System.Text;

namespace Shared;

public class FileUploadBlobResult
{
    public string Id { get; set; }
    public string OriginalName { get; set; }
    public string ContainerName { get; set; }
    public long Size { get; set; }
}
