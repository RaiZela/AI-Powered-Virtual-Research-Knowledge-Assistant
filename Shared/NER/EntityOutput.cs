using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.NER;

public class EntityOutput
{
    public string Text { get; set; }
    public string Category { get; set; }
    public double ConfidenceScore { get; set; }
}
