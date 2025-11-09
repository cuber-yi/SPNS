
public class MqttOptions
{
    public string Server { get; set; }
    public int Port { get; set; }
    public string ClientId { get; set; }
    public string StructureTopic { get; set; }
    public string StaticCalculateTopic { get; set; }
    public string DynamicCalculateTopic { get; set; }
    public string ResultTopic { get; set; }
    public string FeedbackTopic { get; set; }
}

public class FilePathsOptions
{
    public string Structure { get; set; }
    public string StaticCalculate { get; set; }
    public string DynamicCalculate { get; set; }
    public string Output { get; set; }
}

public class OptimizationOptions
{
    public int PopulationSize { get; set; }
    public int MaxIterations { get; set; }
    public double StepSize { get; set; }
}
